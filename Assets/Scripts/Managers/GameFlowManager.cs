using System;
using System.Collections;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent bootstrapper that owns scene transitions and high-level dungeon flow.
/// </summary>
public class GameFlowManager : Singleton<GameFlowManager>
{
    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string gameplaySceneName = "SampleScene";

    [Header("Music (FMOD)")]
    [SerializeField] private EventReference mainMenuMusicEvent;
    [SerializeField] private EventReference gameplayMusicEvent;

    [Header("Screen Fade")]
    [SerializeField, Min(0f)] private float fadeToBlackDuration = 0.2f;
    [SerializeField, Min(0f)] private float fadeFromBlackDuration = 0.25f;
    [SerializeField, Min(0f)] private float postSpawnBlackHold = 2f;
    [SerializeField] private Font font;

    private RunSessionController _runSession;
    private PlayerController _trackedPlayer;
    private bool _isLoadingRunScene;
    private bool _isTransitioning;
    private CanvasGroup _fadeCanvasGroup;
    private Text _fadeDungeonLabel;
    private Coroutine _transitionRoutine;

    public RunSessionController ActiveRunSession => _runSession;
    public PlayerController CurrentPlayer => _trackedPlayer;

    public event Action<RunSessionController> RunSessionChanged;
    public event Action<PlayerController> PlayerSpawned;
    public event Action PlayerDied;
    public event Action PlayerRespawned;
    public event Action<PlayerController> PlayerReachedExit;
    public event Action GameWon;
    public event Action<int, int> BlopsChanged;

    protected override void Awake()
    {
        IsPersistent = true;
        base.Awake();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        UpdateSceneMusic(SceneManager.GetActiveScene().name);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnhookPlayer();
    }

    public void RegisterRunSession(RunSessionController session)
    {
        if (session == null)
            return;

        if (_runSession == session)
            return;

        if (_runSession != null)
            _runSession.PlayerSpawned -= HandlePlayerSpawned;

        _runSession = session;
        _runSession.PlayerSpawned += HandlePlayerSpawned;
        RunSessionChanged?.Invoke(_runSession);

        if (_runSession.ActivePlayer != null)
            HandlePlayerSpawned(_runSession.ActivePlayer);
    }

    public void UnregisterRunSession(RunSessionController session)
    {
        if (session == null || _runSession != session)
            return;

        _runSession.PlayerSpawned -= HandlePlayerSpawned;
        _runSession = null;
        RunSessionChanged?.Invoke(null);
        UnhookPlayer();
    }

    public void StartRunFromMenu()
    {
        if (_isTransitioning)
            return;

        StartRunTransition();
    }

    public void RestartRun()
    {
        if (_isTransitioning)
            return;

        StartRunTransition();
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(mainMenuSceneName, LoadSceneMode.Single);
    }

    public void HandleDungeonCompleted()
    {
        if (_isTransitioning)
            return;

        var dungeon = DungeonRunManager.Instance;
        if (dungeon == null)
            return;

        if (dungeon.dungeonIndex >= dungeon.maxDungeon)
        {
            TriggerVictory();
        }
        else
        {
            StartNextDungeonTransition();
        }
    }

    public void HandlePlayerReachedExit()
    {
        if (_trackedPlayer != null)
            _trackedPlayer.SetTemporaryInvincibility(true);

        PlayerReachedExit?.Invoke(_trackedPlayer);
    }

    private void StartRunTransition()
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        _transitionRoutine = StartCoroutine(StartRunTransitionRoutine());
    }

    private void StartNextDungeonTransition()
    {
        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        _transitionRoutine = StartCoroutine(AdvanceDungeonRoutine());
    }

    private IEnumerator StartRunTransitionRoutine()
    {
        _isTransitioning = true;
        _isLoadingRunScene = true;
        Time.timeScale = 1f;

        EnsureFadeOverlay();
        ClearFadeOverlayDungeonLabel();
        yield return FadeOverlay(targetAlpha: 1f, fadeToBlackDuration);

        yield return SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Single);

        // RunSessionController registers itself in OnEnable.
        float waitTimeout = 2f;
        while (_runSession == null && waitTimeout > 0f)
        {
            waitTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        _isLoadingRunScene = false;
        StartFreshRun();
        UpdateFadeOverlayDungeonLabel();

        // Let camera follow and player visuals settle before revealing the scene.
        yield return null;
        if (postSpawnBlackHold > 0f)
            yield return new WaitForSecondsRealtime(postSpawnBlackHold);

        yield return FadeOverlay(targetAlpha: 0f, fadeFromBlackDuration);
        ClearFadeOverlayDungeonLabel();
        _isTransitioning = false;
        _transitionRoutine = null;
    }

    private IEnumerator AdvanceDungeonRoutine()
    {
        _isTransitioning = true;
        Time.timeScale = 1f;

        EnsureFadeOverlay();
        ClearFadeOverlayDungeonLabel();
        yield return FadeOverlay(targetAlpha: 1f, fadeToBlackDuration);

        if (_runSession == null)
        {
            var session = FindFirstObjectByType<RunSessionController>();
            if (session != null)
                RegisterRunSession(session);
        }

        _runSession?.BeginNextDungeon();
        UpdateFadeOverlayDungeonLabel();

        // Let camera follow and player visuals settle before revealing the scene.
        yield return null;
        if (postSpawnBlackHold > 0f)
            yield return new WaitForSecondsRealtime(postSpawnBlackHold);

        yield return FadeOverlay(targetAlpha: 0f, fadeFromBlackDuration);
        ClearFadeOverlayDungeonLabel();
        _isTransitioning = false;
        _transitionRoutine = null;
    }

    private void StartFreshRun()
    {
        Time.timeScale = 1f;
        ResetRunState();
        _runSession?.BeginNewRun();
    }

    private void ResetRunState()
    {
        var upgrades = UpgradeManager.Instance;
        upgrades?.ResetState();

        var dungeon = DungeonRunManager.Instance;
        if (dungeon != null)
            dungeon.dungeonIndex = 1;
    }

    private void HandlePlayerSpawned(PlayerController player)
    {
        if (player == null)
            return;

        if (_trackedPlayer != player)
        {
            UnhookPlayer();
            _trackedPlayer = player;
            _trackedPlayer.PlayerDied += HandlePlayerDeath;
            _trackedPlayer.PlayerRespawned += HandlePlayerRespawned;
        }

        _trackedPlayer.SetTemporaryInvincibility(false);

        PlayerSpawned?.Invoke(player);
    }

    private void HandlePlayerDeath(PlayerController player)
    {
        Time.timeScale = 0f;
        PlayerDied?.Invoke();
    }

    private void HandlePlayerRespawned(PlayerController player)
    {
        Time.timeScale = 1f;
        PlayerRespawned?.Invoke();
    }

    private void UnhookPlayer()
    {
        if (_trackedPlayer == null)
            return;

        _trackedPlayer.PlayerDied -= HandlePlayerDeath;
        _trackedPlayer.PlayerRespawned -= HandlePlayerRespawned;
        _trackedPlayer = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateSceneMusic(scene.name);

        if (scene.name != gameplaySceneName)
            return;

        if (_runSession == null)
        {
            var session = FindFirstObjectByType<RunSessionController>();
            RegisterRunSession(session);
        }

        if (!_isLoadingRunScene && !_isTransitioning && _runSession != null)
            StartFreshRun();
    }

    private void UpdateSceneMusic(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        if (sceneName == mainMenuSceneName)
        {
            if (!mainMenuMusicEvent.IsNull)
                AudioManager.PlayBgm(mainMenuMusicEvent);
            else
                AudioManager.StopBgm();
            return;
        }

        if (sceneName == gameplaySceneName)
        {
            if (!gameplayMusicEvent.IsNull)
                AudioManager.PlayBgm(gameplayMusicEvent);
            else
                AudioManager.StopBgm();
            return;
        }

        AudioManager.StopBgm();
    }

    private void TriggerVictory()
    {
        Time.timeScale = 0f;
        if (_runSession != null)
            _runSession.SetPlayerInputEnabled(false);

        GameWon?.Invoke();
    }

    public void HandleBlopsChanged(int current, int total)
    {
        BlopsChanged?.Invoke(current, total);
    }

    private void EnsureFadeOverlay()
    {
        if (_fadeCanvasGroup != null)
            return;

        var fadeRoot = new GameObject("ScreenFadeOverlay");
        fadeRoot.transform.SetParent(transform, false);

        var canvas = fadeRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        fadeRoot.AddComponent<GraphicRaycaster>();
        var scaler = fadeRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _fadeCanvasGroup = fadeRoot.AddComponent<CanvasGroup>();
        _fadeCanvasGroup.alpha = 0f;
        _fadeCanvasGroup.interactable = false;
        _fadeCanvasGroup.blocksRaycasts = false;

        var imageGO = new GameObject("Black");
        imageGO.transform.SetParent(fadeRoot.transform, false);
        var image = imageGO.AddComponent<Image>();
        image.color = Color.black;

        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var labelGO = new GameObject("DungeonLabel");
        labelGO.transform.SetParent(fadeRoot.transform, false);

        _fadeDungeonLabel = labelGO.AddComponent<Text>();
        _fadeDungeonLabel.alignment = TextAnchor.MiddleCenter;
        _fadeDungeonLabel.color = Color.white;
        _fadeDungeonLabel.fontSize = 82;
        _fadeDungeonLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        _fadeDungeonLabel.verticalOverflow = VerticalWrapMode.Overflow;
        _fadeDungeonLabel.raycastTarget = false;
        _fadeDungeonLabel.font = font;
        if (_fadeDungeonLabel.font == null)
            _fadeDungeonLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _fadeDungeonLabel.text = string.Empty;

        var labelRect = _fadeDungeonLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(900f, 120f);
    }

    private IEnumerator FadeOverlay(float targetAlpha, float duration)
    {
        if (_fadeCanvasGroup == null)
            yield break;

        targetAlpha = Mathf.Clamp01(targetAlpha);
        if (duration <= 0f)
        {
            _fadeCanvasGroup.alpha = targetAlpha;
            _fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.001f;
            yield break;
        }

        float startAlpha = _fadeCanvasGroup.alpha;
        float elapsed = 0f;

        _fadeCanvasGroup.blocksRaycasts = true;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        _fadeCanvasGroup.alpha = targetAlpha;
        _fadeCanvasGroup.blocksRaycasts = targetAlpha > 0.001f;
    }

    private void UpdateFadeOverlayDungeonLabel()
    {
        if (_fadeDungeonLabel == null)
            return;

        int dungeonNumber = 1;
        var dungeon = DungeonRunManager.Instance;
        if (dungeon != null)
            dungeonNumber = Mathf.Max(1, dungeon.dungeonIndex);

        _fadeDungeonLabel.text = $"{dungeonNumber}";
    }

    private void ClearFadeOverlayDungeonLabel()
    {
        if (_fadeDungeonLabel == null)
            return;

        _fadeDungeonLabel.text = string.Empty;
    }
}
