using DG.Tweening;
using UnityEngine;

public class EnemyVisibility : MonoBehaviour
{
    [SerializeField] SpriteRenderer mainRenderer;         // Lit triangle
    [SerializeField] float fadeDuration = 0.25f;

    public Transform player { get; set; }
    bool currentlyVisible;
    Tween fadeTween;
    float visibleAlpha = 1f;
    float forcedVisibleUntil;
    bool forceVisiblePersistent;
    bool wasForcedVisible;
    public bool IsVisible => ShouldRenderVisible();

    void Awake()
    {
        if (!mainRenderer)
        {
            mainRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    void OnEnable()
    {
        if (!mainRenderer) return;

        visibleAlpha = Mathf.Clamp01(mainRenderer.color.a <= 0f ? 1f : mainRenderer.color.a);
        mainRenderer.enabled = true;
        currentlyVisible = false;
        forcedVisibleUntil = 0f;
        forceVisiblePersistent = false;
        wasForcedVisible = false;
        fadeTween?.Kill();
        SetRendererAlpha(0f);
    }

    void OnDisable()
    {
        fadeTween?.Kill();
        if (PlayerVisionField.Instance != null)
            PlayerVisionField.Instance.ForceExit(this);
    }


    public void SetVisionContact(bool inside, bool instant = false)
    {
        if (!mainRenderer) return;

        if (currentlyVisible == inside && !instant) return;

        currentlyVisible = inside;
        UpdateRendererVisibility(instant);
    }

    public void ForceReveal(float duration, bool instant = true)
    {
        if (!mainRenderer) return;

        forcedVisibleUntil = Mathf.Max(forcedVisibleUntil, Time.time + Mathf.Max(0f, duration));
        wasForcedVisible = ShouldForceVisible();
        UpdateRendererVisibility(instant);
    }

    public void SetForcedVisiblePersistent(bool enabled, bool instant = true)
    {
        if (!mainRenderer) return;

        forceVisiblePersistent = enabled;
        wasForcedVisible = ShouldForceVisible();
        UpdateRendererVisibility(instant);
    }

    void Update()
    {
        if (!mainRenderer) return;

        bool forcingNow = ShouldForceVisible();
        if (wasForcedVisible && !forcingNow && !currentlyVisible)
            UpdateRendererVisibility(false);

        wasForcedVisible = forcingNow;
    }

    void UpdateRendererVisibility(bool instant = false)
    {
        if (!mainRenderer) return;

        fadeTween?.Kill();

        float targetAlpha = ShouldRenderVisible() ? visibleAlpha : 0f;

        if (instant || fadeDuration <= 0f)
        {
            SetRendererAlpha(targetAlpha);
            return;
        }

        fadeTween = mainRenderer.DOFade(targetAlpha, fadeDuration)
            .SetEase(Ease.Linear);
    }

    void SetRendererAlpha(float alpha)
    {
        Color current = mainRenderer.color;
        current.a = alpha;
        mainRenderer.color = current;
    }

    bool ShouldForceVisible()
    {
        return forceVisiblePersistent || Time.time <= forcedVisibleUntil;
    }

    bool ShouldRenderVisible()
    {
        return currentlyVisible || ShouldForceVisible();
    }
}
