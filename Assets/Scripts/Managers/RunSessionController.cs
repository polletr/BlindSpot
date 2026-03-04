using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class RunSessionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DungeonRunManager runManager;
    [SerializeField] private PlayerController playerPrefab;
    [SerializeField] private Transform playerContainer;

    [Header("Runtime Wiring")]
    [SerializeField] private RectTransform crosshairUI;
    [SerializeField] private Camera crosshairCamera;
    [SerializeField] private SonarConeVisualPool sonarConePool;

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

        if (crosshairCamera == null)
            crosshairCamera = Camera.main;

        if (sonarConePool == null && SonarPoolHub.TryGet(out var cone, out _))
        {
            if (sonarConePool == null)
                sonarConePool = cone;
        }

    }

    private void OnEnable()
    {
        if (GameFlowManager.HasInstance)
        {
            var flow = GameFlowManager.Instance;
            if (flow != null)
                flow.RegisterRunSession(this);
        }

        if (generateOnStart && Application.isPlaying)
            BeginNewRun();
    }

    private void OnDisable()
    {
        if (GameFlowManager.HasInstance)
        {
            var flow = GameFlowManager.Instance;
            if (flow != null)
                flow.UnregisterRunSession(this);
        }

    }

    public void BeginNewRun()
    {
        if (_generationInProgress)
            return;

        if (runManager == null)
        {
            Debug.LogWarning("[RunSessionController] Missing references; cannot start run generation.");
            return;
        }

        _generationInProgress = true;
        DisablePlayerInput();
        runManager.GenerateDungeon();

        if (runManager.TryGetCurrentPlayerSpawnPosition(out Vector2 spawnPosition))
        {
            SpawnOrMovePlayer(spawnPosition);
            EnablePlayerInput();
        }
        else
        {
            Debug.LogWarning("[RunSessionController] Level generated without a player spawn point.");
        }

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

        if (runManager.TryGetCurrentPlayerSpawnPosition(out Vector2 spawnPosition))
        {
            SpawnOrMovePlayer(spawnPosition);
            EnablePlayerInput();
        }
        else
        {
            Debug.LogWarning("[RunSessionController] Next dungeon generated without a player spawn point.");
        }

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

        ConfigurePlayerRuntimeReferences(_activePlayer);
        _activePlayer.Respawn(spawnPosition);
        PlayerSpawned?.Invoke(_activePlayer);
        RebindEnemiesToCurrentPlayer(_activePlayer);
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
            cursor.SetAimOrigin(player.transform);

            if (crosshairUI != null)
                cursor.SetCrosshair(crosshairUI);
            else
                Debug.LogWarning("[RunSessionController] crosshairUI is not assigned; mouse cursor UI will not render.");

            if (crosshairCamera != null)
                cursor.SetCamera(crosshairCamera);
            else
                Debug.LogWarning("[RunSessionController] crosshairCamera is not assigned; aim projection may be incorrect.");
        }

        var flashlight = player.GetComponent<PlayerFlashlight>();
        if (flashlight != null)
        {
            if (sonarConePool == null && SonarPoolHub.TryGet(out var cone, out _))
            {
                if (sonarConePool == null)
                    sonarConePool = cone;
            }

            if (sonarConePool != null)
                flashlight.conePool = sonarConePool;
            else
                Debug.LogWarning("[RunSessionController] sonarConePool is not assigned; flashlight cone visual cannot be created.");

            // Ensure the continuous cone visual is (re)initialized after runtime pool wiring.
            flashlight.ForceFlashlightState(flashlight.flashlightEnabled);
        }
    }

    private static void RebindEnemiesToCurrentPlayer(PlayerController player)
    {
        if (player == null)
            return;

        var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            var enemy = enemies[i];
            if (enemy == null)
                continue;

            enemy.SetPlayerTarget(player);
        }
    }

}
