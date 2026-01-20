using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using DG.Tweening;

public class SonarPing : MonoBehaviour
{
    // Throttle outlines per collider face and avoid per-frame allocations
    readonly Dictionary<ColliderFaceKey, float> _lastFaceImpactTime = new();
    readonly HashSet<ColliderFaceKey> _facesHitThisScan = new();

    [Header("Flashlight Cone (Always On)")]
    public bool flashlightEnabled = true;

    [Tooltip("How often the flashlight cone updates aim/origin (visual + optional wall outlines).")]
    public float flashlightUpdateInterval = 0.03f;

    [Tooltip("If true, flashlight cone also draws wall outlines continuously (helps navigation).")]
    public bool flashlightDrawWallOutlines = false;

    [Header("Flashlight Colors")]
    public Color flashlightColor = new Color(0.85f, 0.92f, 1f, 1f);
    public Color pulseColor = new Color(1f, 0.58f, 0.33f, 1f);
    public float pulseColorBlendTime = 0.12f;
    public float pulseColorFadeTime = 0.4f;
    public Ease pulseColorEase = Ease.InOutSine;

    [Header("Pulse (On Click)")]
    [Tooltip("How long the flashlight takes to shrink before the scan burst.")]
    public float pulseRetractTime = 0.5f;
    [Tooltip("How long the flashlight takes to expand back out after shrinking.")]
    public float pulseReturnTime = 0.35f;
    public Ease pulseRetractEase = Ease.InCubic;
    public Ease pulseReturnEase = Ease.OutCubic;
    public float pulseRevealDuration = 0.6f;       // revealables stay visible during this pulse window
    public float pulseRevealPerTick = 0.15f;       // how long each tick reveal call lasts
    public float pulseScanInterval = 0.05f;        // how often we refresh reveal during the pulse
    public float pulseCooldown = 1.0f;

    [Header("Tuning")]
    public float range = 7f;
    [Range(5f, 120f)] public float angleDeg = 45f;

    [Header("Line of sight")]
    public LayerMask obstacleMask;     // Obstacles only
    public LayerMask revealableMask;   // Revealables only
    public bool piercingUpgrade = false;

    [Header("Ray fan quality")]
    [Range(12, 180)] public int rayCount = 60;

    [Header("Visual")]
    public SonarConeVisualPool conePool;

    [Header("Wall Impact Streaks")]
    public SonarImpactPool impactPool;
    public Color impactColor = new Color(0.62f, 0.92f, 1f, 0.8f);
    public float impactLifetime = 0.35f;
    [Range(1, 10)] public int impactStride = 4;
    [FormerlySerializedAs("impactCooldownPerBin")] public float impactCooldownPerCollider = 0.08f;

    [Header("Outline sizing")]
    public float outlineThickness = 0.06f;
    public float outlineLengthPadding = 0.06f;

    // Aim source
    Func<Vector2> _aimProvider;
    Vector2 _cachedAimDir = Vector2.right;

    // Runtime
    float _nextPulseReadyTime;
    Coroutine _flashlightRoutine;
    Coroutine _pulseRoutine;

    // Persistent visual instance (from pool)
    SonarConeVisual _flashlightVisual; // assume this is the type returned by conePool.Get()

    void OnEnable()
    {
        if (flashlightEnabled)
            StartFlashlight();
    }

    void OnDisable()
    {
        StopFlashlight();
        StopPulse();
        HideFlashlightVisual();
    }

    // ----------------------------------------------------
    // Public API
    // ----------------------------------------------------

    public void SetAimProvider(Func<Vector2> aimProvider)
    {
        _aimProvider = aimProvider;
    }

    public bool CanPulse()
    {
        if (Time.time < _nextPulseReadyTime) return false;
        if (_pulseRoutine != null) return false; // prevent stacking pulses
        return true;
    }

    /// <summary>
    /// Click action: retract flashlight briefly, then run a short reveal pulse.
    /// </summary>
    public void DoPulse()
    {
        if (!CanPulse()) return;

        _nextPulseReadyTime = Time.time + pulseCooldown;

        if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
        _pulseRoutine = StartCoroutine(PulseRoutine());
    }

    // ----------------------------------------------------
    // Flashlight (Always-on cone)
    // ----------------------------------------------------

    void StartFlashlight()
    {
        if (_flashlightRoutine != null) StopCoroutine(_flashlightRoutine);
        _flashlightRoutine = StartCoroutine(FlashlightRoutine());
    }

    void StopFlashlight()
    {
        if (_flashlightRoutine != null)
        {
            StopCoroutine(_flashlightRoutine);
            _flashlightRoutine = null;
        }
    }

    IEnumerator FlashlightRoutine()
    {
        EnsureFlashlightVisual();

        while (flashlightEnabled)
        {
            Vector2 origin = transform.position;
            Vector2 dir = GetAimDir();

            // Keep the visual cone on and aligned.
            // This assumes your visual has some way to be "updated".
            // If your current visual only supports Play(), add an Update/Set method (see note below).
            UpdateFlashlightVisual(origin, dir);

            if (flashlightDrawWallOutlines)
                SpawnWallOutlines(origin, dir, range);

            yield return new WaitForSeconds(flashlightUpdateInterval);
        }
    }

    void EnsureFlashlightVisual()
    {
        if (conePool == null) return;

        if (_flashlightVisual == null)
        {
            _flashlightVisual = conePool.Get();
            _flashlightVisual.BeginContinuous(transform, range, angleDeg, obstacleMask, piercingUpgrade, rayCount, colorOverride: flashlightColor);
        }
        else
        {
            _flashlightVisual.SetRestColor(flashlightColor);
        }
    }

    void HideFlashlightVisual()
    {
        if (_flashlightVisual == null) return;
        _flashlightVisual.SetVisible(false);
    }

    void ShowFlashlightVisual()
    {
        if (_flashlightVisual == null) return;
        _flashlightVisual.SetVisible(true);
    }

    void UpdateFlashlightVisual(Vector2 origin, Vector2 dir)
    {
        if (_flashlightVisual == null) return;
        _flashlightVisual.SetAim(origin, dir);
    }

    // ----------------------------------------------------
    // Pulse (reveal burst)
    // ----------------------------------------------------

    void StopPulse()
    {
        if (_pulseRoutine != null)
        {
            StopCoroutine(_pulseRoutine);
            _pulseRoutine = null;
        }

        if (_flashlightVisual != null)
        {
            StartFlashlightColorTween(flashlightColor, pulseColorFadeTime, pulseColorEase);

            if (flashlightEnabled && gameObject.activeInHierarchy)
                _flashlightVisual.AnimateFlashlightOn(0.1f, pulseReturnEase);
        }
    }

    IEnumerator PulseRoutine()
    {
        StartFlashlightColorTween(pulseColor, pulseColorBlendTime, pulseColorEase);

        yield return AnimateFlashlightTween(false, pulseRetractTime, pulseRetractEase);
        yield return AnimateFlashlightTween(true, pulseReturnTime, pulseReturnEase);

        // Reveal burst window
        float end = Time.time + pulseRevealDuration;

        while (Time.time < end)
        {
            Vector2 origin = transform.position;
            Vector2 dir = GetAimDir();

            RevealInCone(origin, dir, pulseRevealPerTick);

            // Optional: do wall outlines only during pulse (if you want "scan" feel)
            // SpawnWallOutlines(origin, dir, range);

            yield return new WaitForSeconds(pulseScanInterval);
        }

        StartFlashlightColorTween(flashlightColor, pulseColorFadeTime, pulseColorEase);

        _pulseRoutine = null;
    }

    IEnumerator AnimateFlashlightTween(bool turnOn, float duration, Ease ease)
    {
        if (_flashlightVisual == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Tween tween = turnOn
            ? _flashlightVisual.AnimateFlashlightOn(duration, ease)
            : _flashlightVisual.AnimateFlashlightOff(duration, ease);

        if (tween != null)
            yield return tween.WaitForCompletion();
        else
            yield return new WaitForSeconds(duration);
    }

    Tween StartFlashlightColorTween(Color targetColor, float duration, Ease ease)
    {
        if (_flashlightVisual == null) return null;
        return _flashlightVisual.AnimateColor(targetColor, duration, ease);
    }

    // ----------------------------------------------------
    // Aim
    // ----------------------------------------------------

    Vector2 GetAimDir()
    {
        if (_aimProvider != null)
        {
            Vector2 dir = _aimProvider.Invoke();
            if (dir.sqrMagnitude > 0.0001f)
            {
                _cachedAimDir = dir.normalized;
                return _cachedAimDir;
            }
        }

        Vector2 tr = transform.right;
        if (tr.sqrMagnitude > 0.0001f)
            _cachedAimDir = tr.normalized;

        return _cachedAimDir;
    }

    // ----------------------------------------------------
    // Reveal + Outlines (your existing logic)
    // ----------------------------------------------------

    void RevealInCone(Vector2 origin, Vector2 forward, float durationOverride)
    {
        Collider2D[] candidates = Physics2D.OverlapCircleAll(origin, range, revealableMask);
        float half = angleDeg * 0.5f;

        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D col = candidates[i];

            Vector2 closest = col.ClosestPoint(origin);
            Vector2 toTarget = closest - origin;

            float dist = toTarget.magnitude;
            if (dist <= 0.001f) continue;

            Vector2 dir = toTarget / dist;

            float ang = Vector2.Angle(forward, dir);
            if (ang > half) continue;

            if (!piercingUpgrade)
            {
                RaycastHit2D block = Physics2D.Raycast(origin, dir, dist, obstacleMask);
                if (block.collider != null) continue;
            }

            var reveal = col.GetComponentInParent<Revealable>();
            if (reveal != null)
                reveal.Reveal(durationOverride);
        }
    }

    void SpawnWallOutlines(Vector2 origin, Vector2 forward, float useRange)
    {
        if (impactPool == null) return;

        _facesHitThisScan.Clear();

        float half = angleDeg * 0.5f;
        float startAng = -half;
        float step = angleDeg / Mathf.Max(1, (rayCount - 1));

        for (int i = 0; i < rayCount; i += impactStride)
        {
            float a = startAng + step * i;
            Vector2 dir = Rotate(forward, a);

            RaycastHit2D hit = Physics2D.Raycast(origin, dir, useRange, obstacleMask);
            if (hit.collider == null) continue;

            var box = hit.collider as BoxCollider2D;
            if (box == null) continue;

            WallFace face = DetermineFace(box, hit.normal);
            var key = new ColliderFaceKey(box, face);

            _facesHitThisScan.Add(key);
        }

        foreach (var key in _facesHitThisScan)
        {
            if (_lastFaceImpactTime.TryGetValue(key, out float last) && Time.time < last + impactCooldownPerCollider)
                continue;

            _lastFaceImpactTime[key] = Time.time;
            SpawnOutlineForFace(key);
        }

        _facesHitThisScan.Clear();
    }

    void SpawnOutlineForFace(ColliderFaceKey key)
    {
        var box = key.Collider as BoxCollider2D;
        if (box == null) return;

        Vector2 size = GetWorldSize(box);
        Vector3 center = box.transform.TransformPoint(box.offset);

        Vector2 right = box.transform.right;
        Vector2 up = box.transform.up;

        float extX = size.x * 0.5f;
        float extY = size.y * 0.5f;

        float lengthPad = Mathf.Max(0f, outlineLengthPadding) * 2f;
        float thickness = Mathf.Max(0.005f, outlineThickness);

        Vector2 normalDir;
        Vector2 tangentDir;
        float faceLength;
        float normalOffset;

        switch (key.Face)
        {
            case WallFace.Right:
                normalDir = right;
                tangentDir = up;
                faceLength = size.y;
                normalOffset = extX;
                break;
            case WallFace.Left:
                normalDir = -right;
                tangentDir = up;
                faceLength = size.y;
                normalOffset = extX;
                break;
            case WallFace.Top:
                normalDir = up;
                tangentDir = right;
                faceLength = size.x;
                normalOffset = extY;
                break;
            case WallFace.Bottom:
                normalDir = -up;
                tangentDir = right;
                faceLength = size.x;
                normalOffset = extY;
                break;
            default:
                return;
        }

        Vector3 faceCenter = center + (Vector3)(normalDir * (normalOffset + thickness * 0.5f));
        float rotation = Mathf.Atan2(tangentDir.y, tangentDir.x) * Mathf.Rad2Deg;

        float width = faceLength + lengthPad;
        float height = thickness;

        var outline = impactPool.Get();
        outline.Play(faceCenter, width, height, rotation, impactColor, impactLifetime);
    }

    static Vector2 GetWorldSize(BoxCollider2D box)
    {
        Vector3 scale = box.transform.lossyScale;
        float width = Mathf.Abs(box.size.x * scale.x);
        float height = Mathf.Abs(box.size.y * scale.y);

        return new Vector2(Mathf.Max(0.01f, width), Mathf.Max(0.01f, height));
    }

    static WallFace DetermineFace(BoxCollider2D box, Vector2 worldNormal)
    {
        Vector2 n = worldNormal;
        if (n.sqrMagnitude < 0.0001f)
            n = box.transform.right;
        else
            n.Normalize();

        Vector2 right = box.transform.right;
        Vector2 up = box.transform.up;

        float dotRight = Vector2.Dot(n, right);
        float dotUp = Vector2.Dot(n, up);

        if (Mathf.Abs(dotRight) > Mathf.Abs(dotUp))
            return dotRight >= 0f ? WallFace.Right : WallFace.Left;

        return dotUp >= 0f ? WallFace.Top : WallFace.Bottom;
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float cs = Mathf.Cos(r);
        float sn = Mathf.Sin(r);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    enum WallFace : byte { Right, Left, Top, Bottom }

    readonly struct ColliderFaceKey : IEquatable<ColliderFaceKey>
    {
        public readonly Collider2D Collider;
        public readonly WallFace Face;

        public ColliderFaceKey(Collider2D collider, WallFace face)
        {
            Collider = collider;
            Face = face;
        }

        public bool Equals(ColliderFaceKey other) => Collider == other.Collider && Face == other.Face;
        public override bool Equals(object obj) => obj is ColliderFaceKey other && Equals(other);

        public override int GetHashCode()
        {
            int id = Collider != null ? Collider.GetInstanceID() : 0;
            return (id * 397) ^ (int)Face;
        }
    }
}
