using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SonarConeVisual : MonoBehaviour
{
    public Action<SonarConeVisual> OnFinished;

    [Range(0f, 1f)] public float maxAlpha = 0.12f;
    [Tooltip("Time constant (seconds) used to smooth flashlight aim.")]
    public float aimSmoothTime = 0.08f;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    Mesh mesh;
    MeshRenderer mr;
    MeshFilter mf;
    MaterialPropertyBlock mpb;
    Color baseColor;

    Tween _rangeTween;
    Tween _colorTween;

    Transform _follow;
    Vector2 _forward = Vector2.right;
    Vector2 _targetForward = Vector2.right;
    Vector2 _smoothedForward = Vector2.right;
    Vector2 _forwardVelocity;

    float _angleDeg;
    LayerMask _obstacleMask;
    bool _piercing;
    int _rayCount;

    float _currentRange;
    float _currentAlpha;

    bool _continuousActive;
    float _restRange;
    float _restAlpha = 1f;

    Color _restColorRGB = Color.white;
    Color _currentColor = Color.white;

    Vector2 _manualOrigin;
    bool _hasManualOrigin;

    void Awake()
    {
        mr = GetComponent<MeshRenderer>();
        mf = GetComponent<MeshFilter>();

        mesh = new Mesh { name = "SonarConeMesh" };
        mf.sharedMesh = mesh;
        mesh.MarkDynamic();

        mpb = new MaterialPropertyBlock();
        baseColor = (mr.sharedMaterial != null && mr.sharedMaterial.HasProperty(BaseColorID))
            ? mr.sharedMaterial.GetColor(BaseColorID)
            : Color.white;

        _restColorRGB = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
        _currentColor = baseColor;

        mr.enabled = false;
        SetAlpha(0f);
    }

    void OnDisable()
    {
        _rangeTween?.Kill();
        _rangeTween = null;
        _colorTween?.Kill();
        _colorTween = null;
        _continuousActive = false;
        _forwardVelocity = Vector2.zero;
    }

    public void BeginContinuous(
        Transform follow,
        float range,
        float angleDeg,
        LayerMask obstacleMask,
        bool piercing,
        int rayCount,
        float alpha = -1f,
        Color? colorOverride = null)
    {
        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

        _follow = follow;
        _angleDeg = angleDeg;
        _obstacleMask = obstacleMask;
        _piercing = piercing;
        _rayCount = Mathf.Max(2, rayCount);

        _restRange = Mathf.Max(0f, range);
        _restAlpha = (alpha >= 0f) ? Mathf.Clamp01(alpha) : maxAlpha;
        _currentRange = _restRange;
        _currentAlpha = _restAlpha;

        if (colorOverride.HasValue)
            _restColorRGB = colorOverride.Value;

        SetColorRGB(_restColorRGB);

        mr.enabled = true;
        _continuousActive = true;
        _targetForward = (_forward.sqrMagnitude > 0.0001f) ? _forward.normalized : Vector2.right;
        _smoothedForward = _targetForward;
        _forwardVelocity = Vector2.zero;

        Vector2 origin = (_follow != null) ? (Vector2)_follow.position : (Vector2)transform.position;
        BuildMesh(origin, _targetForward, _currentRange, _angleDeg, _obstacleMask, _piercing, _rayCount);
    }

    public void EndContinuous()
    {
        _continuousActive = false;
        _rangeTween?.Kill();
        _rangeTween = null;
        _colorTween?.Kill();
        _colorTween = null;
        mr.enabled = false;
        mesh.Clear();
    }

    public void SetRestColor(Color color, bool applyImmediately = true)
    {
        _restColorRGB = color;
        if (applyImmediately && _continuousActive)
            SetColorRGB(_restColorRGB);
    }

    public void SetAim(Vector2 origin, Vector2 forward)
    {
        if (forward.sqrMagnitude > 0.0001f)
        {
            _targetForward = forward.normalized;
            if (!_continuousActive || aimSmoothTime <= 0f)
            {
                _smoothedForward = _targetForward;
                _forwardVelocity = Vector2.zero;
            }
        }

        if (_continuousActive)
        {
            _manualOrigin = origin;
            _hasManualOrigin = true;
            return;
        }

        BuildMesh(origin, _targetForward, _currentRange, _angleDeg, _obstacleMask, _piercing, _rayCount);
    }

    public void SetVisible(bool visible)
    {
        mr.enabled = visible;
        if (!visible)
            mesh.Clear();
    }

    public Tween AnimateFlashlightOff(float duration, Ease ease)
    {
        return AnimateFlashlightTo(0f, 0f, duration, ease);
    }

    public Tween AnimateFlashlightOn(float duration, Ease ease)
    {
        float targetRange = (_restRange > 0f) ? _restRange : _currentRange;
        float targetAlpha = (_restAlpha > 0f) ? _restAlpha : maxAlpha;
        return AnimateFlashlightTo(targetRange, targetAlpha, duration, ease);
    }

    public Tween AnimateColor(Color color, float duration, Ease ease)
    {
        if (!_continuousActive) return null;

        _colorTween?.Kill();
        float dur = Mathf.Max(0.01f, duration);
        Vector3 target = new Vector3(color.r, color.g, color.b);

        var tween = DOTween.To(
                () => new Vector3(_currentColor.r, _currentColor.g, _currentColor.b),
                rgb => SetColorRGB(rgb),
                target,
                dur)
            .SetEase(ease)
            .OnComplete(() => _colorTween = null);

        _colorTween = tween;
        return tween;
    }

    public void Retract(float time)
    {
        if (_continuousActive)
        {
            AnimateFlashlightOff(time, Ease.InCubic);
            return;
        }

        _rangeTween?.Kill();
        _rangeTween = DOTween.To(() => _currentRange, x => _currentRange = x, 0f, Mathf.Max(0.01f, time))
            .SetEase(Ease.InCubic);
    }

    public void Play(
        Transform follow,
        Vector2 forward,
        float range,
        float angleDeg,
        LayerMask obstacleMask,
        bool piercing,
        int rayCount,
        float expandTime,
        float holdTime,
        float retractTime)
    {
        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

        _continuousActive = false;

        _follow = follow;
        _forward = forward.normalized;
        _targetForward = _forward;
        _smoothedForward = _forward;
        _angleDeg = angleDeg;
        _obstacleMask = obstacleMask;
        _piercing = piercing;
        _rayCount = Mathf.Max(2, rayCount);

        mr.enabled = true;
        _currentRange = 0f;
        SetAlpha(0f);

        _rangeTween?.Kill();

        Sequence seq = DOTween.Sequence();

        seq.Append(DOTween.To(() => _currentRange, x => _currentRange = x, range, Mathf.Max(0.01f, expandTime))
            .SetEase(Ease.OutCubic));
        seq.Join(DOTween.To(() => _currentAlpha, a => SetAlpha(a), maxAlpha, Mathf.Max(0.01f, expandTime))
            .SetEase(Ease.OutCubic));

        seq.AppendInterval(Mathf.Max(0f, holdTime));

        seq.Append(DOTween.To(() => _currentRange, x => _currentRange = x, 0f, Mathf.Max(0.01f, retractTime))
            .SetEase(Ease.InCubic));
        seq.Join(DOTween.To(() => _currentAlpha, a => SetAlpha(a), 0f, Mathf.Max(0.01f, retractTime))
            .SetEase(Ease.InCubic));

        seq.OnComplete(() =>
        {
            mr.enabled = false;
            mesh.Clear();
            OnFinished?.Invoke(this);
        });

        _rangeTween = seq;
    }

    void Update()
    {
        if (!mr.enabled) return;

        Vector2 origin = (_follow != null) ? (Vector2)_follow.position : (Vector2)transform.position;
        if (_hasManualOrigin)
        {
            origin = _manualOrigin;
            _hasManualOrigin = false;
        }

        if (_continuousActive && aimSmoothTime > 0.0001f)
        {
            _smoothedForward = Vector2.SmoothDamp(
                _smoothedForward,
                _targetForward,
                ref _forwardVelocity,
                Mathf.Max(0.0001f, aimSmoothTime),
                Mathf.Infinity,
                Time.deltaTime);

            if (_smoothedForward.sqrMagnitude > 0.0001f)
                _forward = _smoothedForward.normalized;
        }
        else if (_targetForward.sqrMagnitude > 0.0001f)
        {
            _forward = _targetForward;
        }

        BuildMesh(origin, _forward, _currentRange, _angleDeg, _obstacleMask, _piercing, _rayCount);
    }

    void SetAlpha(float a)
    {
        _currentAlpha = Mathf.Clamp01(a);
        _currentColor.a = _currentAlpha;
        ApplyColor();
    }

    void SetColorRGB(Color color)
    {
        SetColorRGB(new Vector3(color.r, color.g, color.b));
    }

    void SetColorRGB(Vector3 rgb)
    {
        _currentColor.r = rgb.x;
        _currentColor.g = rgb.y;
        _currentColor.b = rgb.z;
        _currentColor.a = _currentAlpha;
        ApplyColor();
    }

    void ApplyColor()
    {
        mr.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorID, _currentColor);
        mr.SetPropertyBlock(mpb);
    }

    Tween AnimateFlashlightTo(float range, float alpha, float duration, Ease ease)
    {
        if (!_continuousActive) return null;

        _rangeTween?.Kill();

        float dur = Mathf.Max(0.01f, duration);
        float clampedRange = Mathf.Max(0f, range);
        float clampedAlpha = Mathf.Clamp01(alpha);

        var seq = DOTween.Sequence();
        seq.Join(DOTween.To(() => _currentRange, x => _currentRange = x, clampedRange, dur).SetEase(ease));
        seq.Join(DOTween.To(() => _currentAlpha, a => SetAlpha(a), clampedAlpha, dur).SetEase(ease));
        seq.OnComplete(() => _rangeTween = null);

        _rangeTween = seq;
        return seq;
    }

    void BuildMesh(Vector2 origin, Vector2 forward, float range, float angleDeg,
        LayerMask obstacleMask, bool piercing, int rayCount)
    {
        if (range <= 0.001f)
        {
            mesh.Clear();
            return;
        }

        transform.position = origin;
        transform.rotation = Quaternion.identity;

        float half = angleDeg * 0.5f;
        float startAng = -half;
        float step = angleDeg / Mathf.Max(1, (rayCount - 1));

        var verts = new List<Vector3>(rayCount + 1) { Vector3.zero };
        var tris = new List<int>(rayCount * 3);

        for (int i = 0; i < rayCount; i++)
        {
            float a = startAng + step * i;
            Vector2 dir = Rotate(forward, a);

            Vector2 endWorld = origin + dir * range;

            if (!piercing)
            {
                var hit = Physics2D.Raycast(origin, dir, range, obstacleMask);
                if (hit.collider != null) endWorld = hit.point;
            }

            Vector2 endLocal = endWorld - origin;
            verts.Add(endLocal);
        }

        for (int i = 1; i < verts.Count - 1; i++)
        {
            tris.Add(0); tris.Add(i); tris.Add(i + 1);
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (range * 2f + 1f));
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float cs = Mathf.Cos(r);
        float sn = Mathf.Sin(r);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }
}
