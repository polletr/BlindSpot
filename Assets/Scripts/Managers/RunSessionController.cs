using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class RunSessionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DungeonRunManager runManager;
    [SerializeField] private RoomGenerator roomGenerator;
    [SerializeField] private PlayerController playerPrefab;
    [SerializeField] private Transform playerContainer;

    [Header("Runtime Wiring")]
    [SerializeField] private RectTransform crosshairUI;
    [SerializeField] private Camera crosshairCamera;
    [SerializeField] private SonarConeVisualPool sonarConePool;
    [SerializeField] private SonarImpactPool sonarImpactPool;

    [Header("Behaviour")]
    [SerializeField] private bool generateOnStart = false;

    private PlayerController _activePlayer;
    private PlayerInput _activePlayerInput;
    private bool _generationInProgress;

    public bool IsGenerating => _generationInProgress;
    public PlayerController ActivePlayer => _activePlayer;

    public event Action<RoomGenerationResult> RoomGenerationFinished;
    public event Action<PlayerController> PlayerSpawned;

    private void Awake()
    {
        if (runManager == null)
            runManager = DungeonRunManager.Instance;

        if (roomGenerator == null)
            roomGenerator = RoomGenerator.Instance;
    }

    private void OnEnable()
    {
        if (GameFlowManager.HasInstance)
            GameFlowManager.Instance.RegisterRunSession(this);

        if (roomGenerator != null)
            roomGenerator.GenerationCompleted += HandleGenerationCompleted;

        if (generateOnStart && Application.isPlaying)
            BeginNewRun();
    }

    private void OnDisable()
    {
        if (GameFlowManager.HasInstance)
            GameFlowManager.Instance.UnregisterRunSession(this);

        if (roomGenerator != null)
            roomGenerator.GenerationCompleted -= HandleGenerationCompleted;
    }

    public void BeginNewRun()
    {
        if (_generationInProgress)
            return;

        if (runManager == null || roomGenerator == null)
        {
            Debug.LogWarning("[RunSessionController] Missing references; cannot start run generation.");
            return;
        }

        _generationInProgress = true;
        DisablePlayerInput();
        runManager.GenerateCurrentDungeon();

        if (_generationInProgress)
            _generationInProgress = false;
    }

    public void BeginNextDungeon()
    {
        if (_generationInProgress)
            return;

        if (runManager == null)
        {
            Debug.LogWarning("[RunSessionController] Missing DungeonRunManager; cannot advance.");
            return;
        }

        _generationInProgress = true;
        DisablePlayerInput();
        runManager.GoToNextDungeon();

        if (_generationInProgress)
            _generationInProgress = false;
    }

    public void SetPlayerInputEnabled(bool enabled)
    {
        if (enabled)
            EnablePlayerInput();
        else
            DisablePlayerInput();
    }

    private void HandleGenerationCompleted(RoomGenerationResult result)
    {
        _generationInProgress = false;
        RoomGenerationFinished?.Invoke(result);
        SpawnOrMovePlayer(result.PlayerSpawnPosition);
        EnablePlayerInput();
    }

    private void SpawnOrMovePlayer(Vector2 spawnPosition)
    {
        if (_activePlayer == null)
        {
            _activePlayer = FindFirstObjectByType<PlayerController>();
            if (_activePlayer != null)
                _activePlayerInput = _activePlayer.GetComponent<PlayerInput>();
        }

        if (_activePlayer == null)
        {
            if (playerPrefab == null)
            {
                Debug.LogWarning("[RunSessionController] No player prefab assigned and no player found in scene.");
                return;
            }

            _activePlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity, playerContainer);
            _activePlayerInput = _activePlayer.GetComponent<PlayerInput>();
        }

        _activePlayer.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);

        _activePlayer.Respawn(spawnPosition);
        ConfigurePlayerRuntimeReferences(_activePlayer);
        PlayerSpawned?.Invoke(_activePlayer);
    }

    private void DisablePlayerInput()
    {
        if (_activePlayerInput == null)
            return;

        if (_activePlayerInput.enabled)
            _activePlayerInput.DeactivateInput();

        _activePlayerInput.enabled = false;
    }

    private void EnablePlayerInput()
    {
        if (_activePlayerInput == null)
            return;

        if (!_activePlayerInput.enabled)
            _activePlayerInput.enabled = true;

        _activePlayerInput.ActivateInput();
        var defaultMap = _activePlayerInput.defaultActionMap;
        if (!string.IsNullOrEmpty(defaultMap))
        {
            var current = _activePlayerInput.currentActionMap;
            if (current == null || current.name != defaultMap)
            {
                try
                {
                    _activePlayerInput.SwitchCurrentActionMap(defaultMap);
                }
                catch (System.ArgumentException)
                {
                    // Default map not found on this asset; leave as-is.
                }
            }
        }
    }
    private void ConfigurePlayerRuntimeReferences(PlayerController player)
    {
        if (player == null)
            return;

        var cursor = player.GetComponent<VirtualAimCursor>();
        if (cursor != null)
        {
            if (crosshairUI != null)
                cursor.SetCrosshair(crosshairUI);
            if (crosshairCamera != null)
                cursor.SetCamera(crosshairCamera);
        }

        var sonar = player.GetComponent<SonarPing>();
        if (sonar != null)
        {
            if (sonarConePool != null)
                sonar.conePool = sonarConePool;
            if (sonarImpactPool != null)
                sonar.impactPool = sonarImpactPool;
        }
    }

}
