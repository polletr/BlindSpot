using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Keeps a Cinemachine camera locked onto the player spawned by the active run session.
/// </summary>
public class CameraFollowBinder : MonoBehaviour
{
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private RunSessionController sessionOverride;
    [SerializeField] private bool alsoSetLookAt = true;

    private RunSessionController _boundSession;

    private void Awake()
    {
        if (virtualCamera == null)
            virtualCamera = GetComponent<CinemachineCamera>();
    }

    private void OnEnable()
    {
        if (GameFlowManager.HasInstance)
            GameFlowManager.Instance.RunSessionChanged += HandleRunSessionChanged;

        BindToSession(ResolveSession());
    }

    private void OnDisable()
    {
        if (GameFlowManager.HasInstance)
            GameFlowManager.Instance.RunSessionChanged -= HandleRunSessionChanged;

        UnbindSession();
    }

    private RunSessionController ResolveSession()
    {
        if (sessionOverride != null)
            return sessionOverride;

        if (GameFlowManager.HasInstance && GameFlowManager.Instance.ActiveRunSession != null)
            return GameFlowManager.Instance.ActiveRunSession;

        return FindFirstObjectByType<RunSessionController>();
    }

    private void HandleRunSessionChanged(RunSessionController session)
    {
        if (sessionOverride != null)
            return;

        BindToSession(session);
    }

    private void BindToSession(RunSessionController session)
    {
        if (_boundSession == session)
        {
            if (_boundSession != null && _boundSession.ActivePlayer != null)
                ApplyTarget(_boundSession.ActivePlayer);
            return;
        }

        UnbindSession();

        _boundSession = session;
        if (_boundSession != null)
        {
            _boundSession.PlayerSpawned += HandlePlayerSpawned;
            if (_boundSession.ActivePlayer != null)
                ApplyTarget(_boundSession.ActivePlayer);
        }
    }

    private void UnbindSession()
    {
        if (_boundSession != null)
            _boundSession.PlayerSpawned -= HandlePlayerSpawned;

        _boundSession = null;
    }

    private void HandlePlayerSpawned(PlayerController player)
    {
        ApplyTarget(player);
    }

    private void ApplyTarget(PlayerController player)
    {
        if (virtualCamera == null || player == null)
            return;

        var target = virtualCamera.Target;
        target.TrackingTarget = player.transform;
        if (alsoSetLookAt)
        {
            target.CustomLookAtTarget = true;
            target.LookAtTarget = player.transform;
        }

        virtualCamera.Target = target;
    }
}
