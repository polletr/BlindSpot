using UnityEngine;
using UnityEngine.Serialization;

public class Revealable : MonoBehaviour
{
    [Tooltip("Single sprite renderer used by reveal visuals.")]
    [FormerlySerializedAs("renderers")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Single particle system used by reveal visuals.")]
    [SerializeField] private ParticleSystem particleSystem;

    [Tooltip("When false, reveal uses particles. When true, reveal uses sprite.")]
    [SerializeField] private bool isVisionUpgradeOn;

    public bool hideOnStart = true;
    public float fadeOutTime = 0.25f;

    struct ParticleColorState
    {
        public ParticleSystem particleSystem;
        public ParticleSystem.MinMaxGradient initialStartColor;
    }

    ParticleColorState particleColorState;
    float visibleUntil;
    float currentAlpha;
    bool insidePlayerVision;

    public bool CanBeRevealed => !insidePlayerVision;

    void Awake()
    {
        if (!spriteRenderer)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (!particleSystem)
            particleSystem = GetComponentInChildren<ParticleSystem>(true);

        CacheParticleColorState();
        ApplyVisualMode();

        if (hideOnStart) SetAlpha(0f);
        else currentAlpha = ResolveInitialAlpha();
    }

    public void SetVisionUpgradeState(bool enabled)
    {
        isVisionUpgradeOn = enabled;
        ApplyVisualMode();
        SetAlpha(currentAlpha);
    }

    public void Reveal(float duration)
    {
        if (insidePlayerVision) return;
        visibleUntil = Mathf.Max(visibleUntil, Time.time + duration);
        SetAlpha(0.05f);
    }

    void Update()
    {
        UpdateParticleRendererWhenIdle();

        if (insidePlayerVision) return;
        if (Time.time <= visibleUntil) return;

        currentAlpha = Mathf.MoveTowards(currentAlpha, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeOutTime));
        SetAlpha(currentAlpha);
    }

    void ApplyVisualMode()
    {
        if (spriteRenderer)
            spriteRenderer.enabled = isVisionUpgradeOn;

        if (!particleSystem) return;

        var particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer)
            particleRenderer.enabled = !isVisionUpgradeOn;

        if (isVisionUpgradeOn)
        {
            if (particleSystem.isPlaying)
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        else if (currentAlpha > 0.0001f)
        {
            if (!particleSystem.isPlaying) particleSystem.Play(true);
        }
    }

    void SetAlpha(float a)
    {
        currentAlpha = a;

        if (spriteRenderer)
        {
            var c = spriteRenderer.color;
            c.a = a;
            spriteRenderer.color = c;
        }

        if (isVisionUpgradeOn)
        {
            ApplyParticleAlpha(particleColorState, 0f);
        }
        else
        {
            ApplyParticleAlpha(particleColorState, a);
        }
    }

    void CacheParticleColorState()
    {
        if (!particleSystem)
        {
            particleColorState = default;
            return;
        }

        var main = particleSystem.main;
        particleColorState = new ParticleColorState
        {
            particleSystem = particleSystem,
            initialStartColor = main.startColor
        };
    }

    float ResolveInitialAlpha()
    {
        if (spriteRenderer)
            return spriteRenderer.color.a;

        if (particleColorState.particleSystem)
            return ExtractAlpha(particleColorState.initialStartColor);

        return 0.05f;
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
        if (!particleSystem) return;

        if (currentAlpha > 0.0001f && !isVisionUpgradeOn) return;

        var particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (!particleRenderer) return;

        if (!particleSystem.IsAlive(true))
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
            SetAlpha(0f);
        }
    }
}
