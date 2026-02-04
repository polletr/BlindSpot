using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent bootstrapper that owns scene transitions and high-level dungeon flow.
/// </summary>
public class GameFlowManager : Singleton<GameFlowManager>
{
    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string gameplaySceneName = "SampleScene";

    private RunSessionController _runSession;
    private PlayerController _trackedPlayer;
    private bool _isLoadingRunScene;

    public RunSessionController ActiveRunSession => _runSession;
    public PlayerController CurrentPlayer => _trackedPlayer;

    public event Action<RunSessionController> RunSessionChanged;
    public event Action<PlayerController> PlayerSpawned;
    public event Action PlayerDied;
    public event Action PlayerRespawned;
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
        if (SceneManager.GetActiveScene().name == gameplaySceneName && _runSession != null)
        {
            StartFreshRun();
            return;
        }

        if (_isLoadingRunScene)
            return;

        StartCoroutine(LoadRunSceneRoutine());
    }

    public void RestartRun()
    {
        if (_runSession == null)
        {
            StartRunFromMenu();
            return;
        }

        StartFreshRun();
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(mainMenuSceneName, LoadSceneMode.Single);
    }

    public void HandleDungeonCompleted()
    {
        var dungeon = DungeonRunManager.Instance;
        if (dungeon == null)
            return;

        if (dungeon.dungeonIndex >= dungeon.maxDungeon)
        {
            TriggerVictory();
        }
        else
        {
            _runSession?.BeginNextDungeon();
        }
    }

    private IEnumerator LoadRunSceneRoutine()
    {
        _isLoadingRunScene = true;
        Time.timeScale = 1f;
        yield return SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Single);
        _isLoadingRunScene = false;

        // RunSessionController registers itself in OnEnable; wait a frame then bootstrap.
        yield return null;
        if (_runSession != null)
            StartFreshRun();
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
        if (scene.name != gameplaySceneName)
            return;

        if (_runSession == null)
        {
            var session = FindFirstObjectByType<RunSessionController>();
            RegisterRunSession(session);
        }

        if (!_isLoadingRunScene && _runSession != null)
            StartFreshRun();
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
}
