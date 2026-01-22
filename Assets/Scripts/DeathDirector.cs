using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;

public class DeathDirector : Singleton<DeathDirector>
{
    [Header("References")]
    [SerializeField] private CinemachineCamera deathCamera;
    [SerializeField] private CanvasGroup continueUI;     // optional (alpha fade)

    [Header("Prefabs")]
    [SerializeField] private DeathCircleVisual deathCirclePrefab;

    [Header("Camera Tuning")]
    [SerializeField] private float zoomOrthoSize = 2.8f;
    [SerializeField] private float zoomDuration = 0.25f;
    [SerializeField] private float holdDuration = 0.5f;

    [Header("Time")]
    [SerializeField] private float hitStop = 0.08f;
    [SerializeField] private float slowMoScale = 0.2f;
    [SerializeField] private float slowMoDuration = 0.9f;

    [Header("Slice Animation")]
    [SerializeField] private float sliceDuration = 0.35f;
    [SerializeField] private float separationMax = 0.04f;

    private float _baseOrtho;
    private Tween _seqTween;
    private DeathCircleVisual _activeVisual;

    private bool HasZoomTarget
    {
        get
        {
            if (deathCamera != null)
                return true;
            return false;
        }
    }

    private float CameraOrthoSize
    {
        get
        {
            if (deathCamera != null)
                return deathCamera.Lens.OrthographicSize;
            return _baseOrtho;
        }
        set
        {
            if (deathCamera != null)
                deathCamera.Lens.OrthographicSize = value;
        }
    }

    private void Awake()
    {
        if (HasZoomTarget)
            _baseOrtho = CameraOrthoSize;
        else
            _baseOrtho = 5f;

        if (continueUI)
        {
            continueUI.alpha = 0f;
            continueUI.interactable = false;
            continueUI.blocksRaycasts = false;
        }
    }

    public void PlayDeath(in DeathHitContext ctx, SpriteRenderer playerSpriteRenderer)
    {
        // Prevent double plays
        if (_seqTween != null && _seqTween.IsActive()) _seqTween.Kill();

        if (!HasZoomTarget)
            Debug.LogWarning("[DeathDirector] No camera or Cinemachine reference assigned; zoom effect disabled.");

        // Snapshot sprite/tint
        var sprite = playerSpriteRenderer.sprite;
        var tint = playerSpriteRenderer.color;

        // Hide player render (keep gameplay object if you want, but freeze controls elsewhere)
        playerSpriteRenderer.enabled = false;

        // Spawn visual overlay at player pos (world)
        if (_activeVisual) Destroy(_activeVisual.gameObject);
        _activeVisual = Instantiate(deathCirclePrefab, ctx.PlayerWorldPos, Quaternion.identity);
        _activeVisual.InitFromPlayer(sprite, tint);
        _activeVisual.SetCut(ctx);

        // Camera: zoom in

        // Time control + animation sequence
        Sequence seq = DOTween.Sequence();

        // Hit stop (hard freeze)
        seq.AppendCallback(() => Time.timeScale = 0f);
        seq.AppendInterval(hitStop); // NOTE: scaled time is 0, so this won't advance unless we use unscaled
        // So we use an unscaled tween for hit stop:
        seq.Kill();
        Sequence unscaled = DOTween.Sequence().SetUpdate(true);

        unscaled.AppendCallback(() => Time.timeScale = 0f);
        unscaled.AppendInterval(hitStop);
        unscaled.AppendCallback(() => Time.timeScale = slowMoScale);

        // Zoom (scaled by unscaled update so it continues during slowmo cleanly)
        if (HasZoomTarget)
        {
            unscaled.Append(DOTween.To(
                () => CameraOrthoSize,
                x => CameraOrthoSize = x,
                zoomOrthoSize,
                zoomDuration
            ).SetEase(Ease.OutCubic));
        }
        else
        {
            unscaled.AppendInterval(zoomDuration);
        }

        // Slice reveal
        unscaled.Append(DOTween.To(
            () => 0f,
            p => _activeVisual.SetProgress(p),
            1f,
            sliceDuration
        ).SetEase(Ease.OutQuart));

        // Separation drift (overlapping)
        unscaled.Join(DOTween.To(
            () => 0f,
            s => _activeVisual.SetSeparation(s),
            separationMax,
            sliceDuration * 1.15f
        ).SetEase(Ease.OutCubic));

        // Hold, then UI
        unscaled.AppendInterval(holdDuration);

        if (continueUI)
        {
            unscaled.AppendCallback(() =>
            {
                continueUI.blocksRaycasts = true;
                continueUI.interactable = true;
            });
            unscaled.Append(DOTween.To(() => continueUI.alpha, a => continueUI.alpha = a, 1f, 0.25f).SetEase(Ease.OutQuad));
        }

        // End slowmo (keep frozen if you want until button; here we keep slowmo and wait for button)
        _seqTween = unscaled;
    }

    // Call this from your Continue button
    public void ContinueToShop(SpriteRenderer playerSpriteRenderer)
    {
        if (_seqTween != null && _seqTween.IsActive()) _seqTween.Kill();

        // Restore time/camera/UI
        Time.timeScale = 1f;
        if (HasZoomTarget)
            CameraOrthoSize = _baseOrtho;

        if (continueUI)
        {
            continueUI.alpha = 0f;
            continueUI.interactable = false;
            continueUI.blocksRaycasts = false;
        }

        if (_activeVisual) Destroy(_activeVisual.gameObject);

        // Load shop scene.
        // SceneManager.LoadScene("Shop");

        // If you keep player object, re-enable sprite if needed (or keep dead state)
        // playerSpriteRenderer.enabled = true; // only if you want to show it again
    }
}
