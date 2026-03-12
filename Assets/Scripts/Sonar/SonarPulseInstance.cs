using DG.Tweening;
using FMODUnity;
using Unity.Android.Gradle.Manifest;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SonarPulseInstance : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private CircleCollider2D triggerCollider;
    [SerializeField] private EventReference OnSonarPing;

    [Header("Animation")]
    [SerializeField] private float radiusGrowthSpeed = 12f;
    [SerializeField] private Ease radiusEase = Ease.OutQuad;
    [SerializeField] private Ease fadeEase = Ease.OutSine;

    private Material _runtimeMaterial;
    private Color _baseColor;
    private SonarPulseData _data;
    private Vector3 _worldPos;
    private Sequence _pulseSequence;
    private float _currentRadius;
    private float _currentAlpha = 1f;

    private static readonly int ColorProp = Shader.PropertyToID("_Color");
    private static readonly int PulseRadiusProp = Shader.PropertyToID("_PulseRadius");
    private static readonly int PulseCenterProp = Shader.PropertyToID("_PulseCenter");

    private void Awake()
    {
        CacheReferences();

        if (spriteRenderer != null)
        {
            _runtimeMaterial = new Material(spriteRenderer.material);
            spriteRenderer.material = _runtimeMaterial;
        }
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void Play(SonarPulseData data, Vector3 worldPosition)
    {
        _data = data;
        _worldPos = worldPosition;
        _pulseSequence?.Kill();

        if (!_data.player)
            triggerCollider.enabled = false;

        _baseColor = _data.pulseColor;


        SetPulseState(0f, 1f);
        PlaySequence();
    }

    private void PlaySequence()
    {
        float growDuration = GetGrowthDuration();
        float fadeDuration = Mathf.Max(0f, _data.fadeOutTime);

        _pulseSequence = DOTween.Sequence().SetLink(gameObject);
        _pulseSequence.Append(
            DOTween.To(
                    () => _currentRadius,
                    value => SetPulseState(value, 1f),
                    _data.maxRadius,
                    growDuration)
                .SetEase(radiusEase));

        if (fadeDuration > 0f)
        {
            _pulseSequence.Append(
                DOTween.To(
                        () => _currentAlpha,
                        value => SetPulseState(_currentRadius, value),
                        0f,
                        fadeDuration)
                    .SetEase(fadeEase));
        }

        Debug.Log(_worldPos);
        AudioManager.PlayAt(OnSonarPing, _worldPos);

        _pulseSequence.OnComplete(() => Destroy(gameObject));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent<Revealable>(out var revealableObj))
        {
            revealableObj.Reveal(_data.revealTime);
        }
    }

    private float GetGrowthDuration()
    {
        if (radiusGrowthSpeed > 0f)
            return Mathf.Max(0.01f, _data.maxRadius / radiusGrowthSpeed);

        return Mathf.Max(0.01f, _data.duration);
    }

    private void SetPulseState(float radius, float alpha)
    {
        _currentRadius = Mathf.Clamp(radius, 0f, _data != null ? _data.maxRadius : radius);
        _currentAlpha = Mathf.Clamp01(alpha);

        if (_runtimeMaterial != null)
        {
            if (_runtimeMaterial.HasProperty(PulseRadiusProp))
                _runtimeMaterial.SetFloat(PulseRadiusProp, _currentRadius);

            if (_runtimeMaterial.HasProperty(PulseCenterProp))
                _runtimeMaterial.SetVector(PulseCenterProp, _worldPos);

            if (_runtimeMaterial.HasProperty(ColorProp))
            {
                Color color = _baseColor;
                color.a = _currentAlpha;
                _runtimeMaterial.color = color;
            }
        }

        if (spriteRenderer != null)
        {
            Color color = _baseColor;
            color.a = _currentAlpha;
            spriteRenderer.color = color;
        }

        if (triggerCollider != null && _data.player)
            triggerCollider.radius = _currentRadius;
    }

    private void CacheReferences()
    {
        if (visualRoot == null)
            visualRoot = transform;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (triggerCollider == null)
            triggerCollider = GetComponentInChildren<CircleCollider2D>(true);
    }

    private void OnDestroy()
    {
        _pulseSequence?.Kill();

        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
    }
}
