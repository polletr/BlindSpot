using UnityEngine;
using UnityEngine.Serialization;

public class Revealable : MonoBehaviour
{
    [Tooltip("Main sprite of the object. Hidden by default, shown when inside player vision.")]
    [FormerlySerializedAs("renderers")]
    [FormerlySerializedAs("spriteRenderer")]
    [SerializeField] private SpriteRenderer mainSpriteRenderer;

    [Tooltip("Default reveal hint visual shown when hit by flashlight (outside player vision).")]
    [FormerlySerializedAs("particleSystem")]
    [SerializeField] private ParticleSystem revealParticleSystem;

    [Tooltip("Optional alternate reveal hint visual shown instead of particles when upgrade visual is active.")]
    [SerializeField] private SpriteRenderer revealUpgradeSpriteRenderer;

    [Tooltip("When true, this script controls the main sprite visibility and alpha.")]
    [SerializeField] private bool controlMainSpriteVisibility = true;

    [Tooltip("If true, swap flashlight reveal from particles to upgrade sprite when upgrade flag is active.")]
    [SerializeField] private bool useUpgradeDrivenRevealVisual = true;

    [Tooltip("Manual fallback/test toggle for upgrade reveal visual.")]
    [FormerlySerializedAs("isVisionUpgradeOn")]
    [SerializeField] private bool forceUpgradeRevealVisual;

    [Tooltip("Main sprite starts hidden until player vision overlaps.")]
    public bool hideOnStart = true;

    [Tooltip("How fast flashlight reveal hint fades out when flashlight is no longer on target.")]
    public float fadeOutTime = 0.25f;

    [Tooltip("Target alpha used for flashlight reveal hint visuals.")]
    [Range(0f, 1f)] public float flashlightRevealAlpha = 1f;

    struct ParticleColorState
    {
        public ParticleSystem particleSystem;
        public ParticleSystem.MinMaxGradient initialStartColor;
    }

    ParticleColorState particleColorState;
    private UpgradeManager upgradeManager;
    float visibleUntil;
    float currentRevealAlpha;
    bool insidePlayerVision;
    bool upgradeRevealVisualActive;

    public bool CanBeRevealed => !insidePlayerVision;

    void Awake()
    {
        if (!mainSpriteRenderer)
            mainSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (!revealParticleSystem)
            revealParticleSystem = GetComponentInChildren<ParticleSystem>(true);

        CacheParticleColorState();
        currentRevealAlpha = hideOnStart ? 0f : ResolveInitialAlpha();

        if (controlMainSpriteVisibility)
            SetMainSpriteAlpha(hideOnStart ? 0f : 1f);

        ApplyRevealVisuals();
    }

    void OnEnable()
    {
        TryHookUpgradeManager();
        RefreshUpgradeDrivenState(forceRefresh: true);
    }

    public void SetVisionUpgradeState(bool enabled)
    {
        forceUpgradeRevealVisual = enabled;
        RefreshUpgradeDrivenState(forceRefresh: true);
    }

    public void Reveal(float duration)
    {
        if (insidePlayerVision) return;
        visibleUntil = Mathf.Max(visibleUntil, Time.time + duration);
        currentRevealAlpha = Mathf.Max(currentRevealAlpha, flashlightRevealAlpha);
        ApplyRevealVisuals();
    }

    void Update()
    {
        UpdateParticleRendererWhenIdle();

        if (insidePlayerVision)
        {
            ApplyRevealVisuals();
            return;
        }

        if (Time.time > visibleUntil)
        {
            currentRevealAlpha = Mathf.MoveTowards(
                currentRevealAlpha,
                0f,
                Time.deltaTime / Mathf.Max(0.01f, fadeOutTime));
        }

        ApplyRevealVisuals();
    }

    void TryHookUpgradeManager()
    {
        if (upgradeManager != null)
            return;

        upgradeManager = UpgradeManager.Instance;
        if (upgradeManager != null)
            upgradeManager.UpgradesChanged += HandleUpgradesChanged;
    }

    void HandleUpgradesChanged(UpgradeSnapshot snapshot)
    {
        RefreshUpgradeDrivenState(forceRefresh: true);
    }

    void RefreshUpgradeDrivenState(bool forceRefresh = false)
    {
        bool shouldUseUpgradeVisual = forceUpgradeRevealVisual;
        if (useUpgradeDrivenRevealVisual && upgradeManager != null && upgradeManager.RevealableAltVisualEnabled)
            shouldUseUpgradeVisual = true;

        if (!forceRefresh && shouldUseUpgradeVisual == upgradeRevealVisualActive)
            return;

        upgradeRevealVisualActive = shouldUseUpgradeVisual;
        ApplyRevealVisuals();
    }

    void ApplyRevealVisuals()
    {
        if (insidePlayerVision)
        {
            if (controlMainSpriteVisibility)
                SetMainSpriteAlpha(1f);

            SetUpgradeSpriteAlpha(0f, false);
            ApplyParticleAlpha(particleColorState, 0f);
            return;
        }

        if (controlMainSpriteVisibility)
            SetMainSpriteAlpha(0f);

        bool showHint = currentRevealAlpha > 0.0001f;
        bool useUpgradeSprite = showHint && upgradeRevealVisualActive && revealUpgradeSpriteRenderer != null;

        if (useUpgradeSprite)
        {
            SetUpgradeSpriteAlpha(currentRevealAlpha, true);
            ApplyParticleAlpha(particleColorState, 0f);
        }
        else
        {
            SetUpgradeSpriteAlpha(0f, false);
            ApplyParticleAlpha(particleColorState, showHint ? currentRevealAlpha : 0f);
        }
    }

    void CacheParticleColorState()
    {
        if (!revealParticleSystem)
        {
            particleColorState = default;
            return;
        }

        var main = revealParticleSystem.main;
        particleColorState = new ParticleColorState
        {
            particleSystem = revealParticleSystem,
            initialStartColor = main.startColor
        };
    }

    float ResolveInitialAlpha()
    {
        if (revealUpgradeSpriteRenderer)
            return revealUpgradeSpriteRenderer.color.a;

        if (particleColorState.particleSystem)
            return ExtractAlpha(particleColorState.initialStartColor);

        return Mathf.Clamp01(flashlightRevealAlpha);
    }

    static float ExtractAlpha(ParticleSystem.MinMaxGradient gradient)
    {
        switch (gradient.mode)
        {
            case ParticleSystemGradientMode.Color:
                return gradient.color.a;
            case ParticleSystemGradientMode.TwoColors:
                return Mathf.Max(gradient.colorMin.a, gradient.colorMax.a);
            case ParticleSystemGradientMode.Gradient:
                return ExtractGradientAlpha(gradient.gradient);
            case ParticleSystemGradientMode.TwoGradients:
                return Mathf.Max(
                    ExtractGradientAlpha(gradient.gradientMin),
                    ExtractGradientAlpha(gradient.gradientMax));
            case ParticleSystemGradientMode.RandomColor:
                return ExtractGradientAlpha(gradient.gradient);
            default:
                return 1f;
        }
    }

    static float ExtractGradientAlpha(Gradient gradient)
    {
        if (gradient == null || gradient.alphaKeys == null || gradient.alphaKeys.Length == 0) return 1f;
        float max = 0f;
        for (int i = 0; i < gradient.alphaKeys.Length; i++)
            max = Mathf.Max(max, gradient.alphaKeys[i].alpha);
        return max;
    }

    static void ApplyParticleAlpha(ParticleColorState state, float alpha)
    {
        if (!state.particleSystem) return;

        var main = state.particleSystem.main;
        main.startColor = WithAlpha(state.initialStartColor, alpha);

        bool show = alpha > 0.0001f;
        var particleRenderer = state.particleSystem.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer && show) particleRenderer.enabled = true;

        if (show)
        {
            if (!state.particleSystem.isPlaying) state.particleSystem.Play(true);
        }
        else
        {
            if (state.particleSystem.isPlaying)
                state.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (particleRenderer && !state.particleSystem.IsAlive(true))
                particleRenderer.enabled = false;
        }
    }

    void UpdateParticleRendererWhenIdle()
    {
        if (!revealParticleSystem) return;

        if (currentRevealAlpha > 0.0001f && !upgradeRevealVisualActive && !insidePlayerVision) return;

        var particleRenderer = revealParticleSystem.GetComponent<ParticleSystemRenderer>();
        if (!particleRenderer) return;

        if (!revealParticleSystem.IsAlive(true))
            particleRenderer.enabled = false;
    }

    static ParticleSystem.MinMaxGradient WithAlpha(ParticleSystem.MinMaxGradient source, float alpha)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                var c = source.color;
                c.a = alpha;
                return new ParticleSystem.MinMaxGradient(c);

            case ParticleSystemGradientMode.TwoColors:
                var cMin = source.colorMin;
                var cMax = source.colorMax;
                cMin.a = alpha;
                cMax.a = alpha;
                return new ParticleSystem.MinMaxGradient(cMin, cMax);

            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(CopyGradientWithAlpha(source.gradient, alpha));

            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    CopyGradientWithAlpha(source.gradientMin, alpha),
                    CopyGradientWithAlpha(source.gradientMax, alpha));

            case ParticleSystemGradientMode.RandomColor:
                return new ParticleSystem.MinMaxGradient(CopyGradientWithAlpha(source.gradient, alpha));

            default:
                var fallback = source.color;
                fallback.a = alpha;
                return new ParticleSystem.MinMaxGradient(fallback);
        }
    }

    static Gradient CopyGradientWithAlpha(Gradient source, float alpha)
    {
        var gradient = new Gradient();
        if (source == null)
        {
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) });
            return gradient;
        }

        var colorKeys = source.colorKeys;
        var alphaKeys = source.alphaKeys;
        for (int i = 0; i < alphaKeys.Length; i++)
            alphaKeys[i].alpha = alpha;

        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    void OnDisable()
    {
        if (upgradeManager != null)
        {
            upgradeManager.UpgradesChanged -= HandleUpgradesChanged;
            upgradeManager = null;
        }

        if (PlayerVisionField.Instance != null)
            PlayerVisionField.Instance.ForceExit(this);
    }

    public void SetVisionContact(bool inside)
    {
        if (insidePlayerVision == inside) return;

        insidePlayerVision = inside;

        if (insidePlayerVision)
        {
            visibleUntil = 0f;
            currentRevealAlpha = 0f;
        }

        ApplyRevealVisuals();
    }

    void SetMainSpriteAlpha(float alpha)
    {
        if (!mainSpriteRenderer) return;

        var color = mainSpriteRenderer.color;
        color.a = alpha;
        mainSpriteRenderer.color = color;
    }

    void SetUpgradeSpriteAlpha(float alpha, bool enabled)
    {
        if (!revealUpgradeSpriteRenderer) return;

        revealUpgradeSpriteRenderer.enabled = enabled;
        var color = revealUpgradeSpriteRenderer.color;
        color.a = alpha;
        revealUpgradeSpriteRenderer.color = color;
    }
}
