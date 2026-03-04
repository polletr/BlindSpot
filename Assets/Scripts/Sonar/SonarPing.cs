using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SonarPing : MonoBehaviour
{
    [Header("Line of sight")]
    public LayerMask obstacleMask;     // Obstacles only
    public LayerMask revealableMask;   // Revealables only
    public bool piercingUpgrade = false;

    [Header("Visual")]
    public SonarImpactPool impactPool;

    [Header("Click Ping (Radar)")]
    public bool clickPingEnabled = true;
    [Min(0.25f)] public float clickPingRange = 9f;
    [Min(0.1f)] public float clickPingSpeed = 14f;
    [Min(0.01f)] public float clickPingThickness = 0.45f;
    [Range(0f, 1f)] public float clickPingEdgeAlpha = 0.7f;
    [Range(0f, 1f)] public float clickPingMidAlpha = 0.18f;
    public Color clickPingColor = new Color(0.75f, 0.95f, 1f, 1f);
    [Range(32, 256)] public int clickPingRayCount = 128;
    [Min(0f)] public float clickPingRevealDuration = 1.0f;
    [Min(0f)] public float clickPingEndFade = 0.22f;
    [Range(0.1f, 1f)] public float clickPingEndThicknessScale = 0.65f;
    [Range(0f, 1f)] public float clickPingColorFadeStart01 = 0.55f;
    [Range(0f, 1f)] public float clickPingColorFadeEndAlpha = 0.08f;
    [Min(0f)] public float clickPingTailLength = 0.55f;
    [Range(0f, 1f)] public float clickPingTailAlpha = 0.35f;
    [Header("Click Ping Impacts")]
    public bool clickPingImpactPops = true;
    [Range(1, 16)] public int clickPingImpactRayStep = 4;
    [Min(0.01f)] public float clickPingImpactWidth = 0.3f;
    [Min(0.01f)] public float clickPingImpactHeight = 0.1f;
    [Min(0.01f)] public float clickPingImpactLifetime = 0.12f;
    [Range(0f, 2f)] public float clickPingImpactAlpha = 0.9f;
    [Tooltip("Small z offset so the ping ring can render above floor sprites.")]
    public float clickPingZOffset = 0.01f;

    [Header("Pool Resolution")]
    [Tooltip("Automatically subscribe to pool hub events so runtime-spawned players get valid pools.")]
    public bool autoResolvePoolsFromHub = true;

    Coroutine _clickPingRoutine;
    MeshFilter _clickPingMeshFilter;
    MeshRenderer _clickPingMeshRenderer;
    Mesh _clickPingMesh;
    MaterialPropertyBlock _clickPingMpb;
    Material _clickPingMaterialInstance;
    Vector2[] _clickPingRayDirs;
    float[] _clickPingStopDistances;
    Vector2[] _clickPingHitPoints;
    Vector2[] _clickPingHitNormals;
    bool[] _clickPingHasObstacleHit;
    bool[] _clickPingImpactTriggered;
    readonly List<PingRevealCandidate> _pendingPingReveals = new();

    static readonly int ColorId = Shader.PropertyToID("_Color");

    struct PingRevealCandidate
    {
        public Revealable revealable;
        public float triggerDistance;
    }

    private void Awake()
    {
        EnsureClickPingVisual();
    }
    PlayerVisionField VisionField => PlayerVisionField.Instance;
    const string EnemyLayerName = "Enemies";
    const string RevealableLayerName = "Revealables";
    const string BlopsLayerName = "Blops";

    void OnEnable()
    {
        EnsureRevealableMask();
        if (autoResolvePoolsFromHub)
        {
            SonarPoolHub.PoolsChanged += HandlePoolsChanged;
            TryResolvePoolsFromHub();
        }
    }

    void OnDisable()
    {
        if (autoResolvePoolsFromHub)
            SonarPoolHub.PoolsChanged -= HandlePoolsChanged;

        StopClickPing();
        SetClickPingVisible(false);
    }

    void OnValidate()
    {
        EnsureRevealableMask();
        clickPingThickness = Mathf.Max(0.01f, clickPingThickness);
        clickPingRange = Mathf.Max(0.25f, clickPingRange);
        clickPingSpeed = Mathf.Max(0.1f, clickPingSpeed);
    }

    void EnsureRevealableMask()
    {
        int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
        if (enemyLayer >= 0)
            revealableMask |= (1 << enemyLayer);

        int revealableLayer = LayerMask.NameToLayer(RevealableLayerName);
        if (revealableLayer >= 0)
            revealableMask |= (1 << revealableLayer);

        int blopsLayer = LayerMask.NameToLayer(BlopsLayerName);
        if (blopsLayer >= 0)
            revealableMask |= (1 << blopsLayer);
    }

    // ----------------------------------------------------
    void TryResolvePoolsFromHub()
    {
        if (!autoResolvePoolsFromHub)
            return;

        if (impactPool != null)
            return;

        if (SonarPoolHub.TryGet(out _, out var impact))
        {
            if (impactPool == null)
                impactPool = impact;
        }
    }

    void HandlePoolsChanged(SonarConeVisualPool cone, SonarImpactPool impact)
    {
        if (!autoResolvePoolsFromHub)
            return;

        impactPool = impact;
    }

    // Public API
    // ----------------------------------------------------

    public bool IsClickPingRunning => _clickPingRoutine != null;
    public bool CanLaunchClickPing => clickPingEnabled && isActiveAndEnabled && !IsClickPingRunning;

    public void TriggerClickPing()
    {
        if (!CanLaunchClickPing)
            return;

        _clickPingRoutine = StartCoroutine(ClickPingRoutine());
    }

    void StopClickPing()
    {
        if (_clickPingRoutine != null)
        {
            StopCoroutine(_clickPingRoutine);
            _clickPingRoutine = null;
        }

        _pendingPingReveals.Clear();
        SetClickPingVisible(false);
    }

    IEnumerator ClickPingRoutine()
    {
        EnsureClickPingVisual();
        if (_clickPingMesh == null || _clickPingMeshRenderer == null)
        {
            _clickPingRoutine = null;
            yield break;
        }

        Vector2 origin = transform.position;
        float revealMaxRange = Mathf.Max(0.01f, clickPingRange);
        float speed = Mathf.Max(0.01f, clickPingSpeed);
        float fadeTime = Mathf.Max(0f, clickPingEndFade);
        float fadeTravelDistance = speed * fadeTime;
        float visualMaxRange = revealMaxRange + fadeTravelDistance;

        BuildClickPingRayStops(origin, visualMaxRange);
        BuildPingRevealCandidates(origin);

        SetClickPingVisible(true);

        float elapsed = 0f;
        float travelDuration = revealMaxRange / speed;

        while (elapsed < travelDuration)
        {
            elapsed += Time.deltaTime;
            float radius = Mathf.Min(revealMaxRange, elapsed * speed);
            TriggerPendingPingReveals(radius);
            TriggerClickPingImpacts(radius);
            BuildClickPingMesh(origin, radius, 1f, 1f);
            yield return null;
        }

        TriggerPendingPingReveals(revealMaxRange + clickPingThickness);
        if (fadeTime > 0f)
        {
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeTime)
            {
                fadeElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(fadeElapsed / fadeTime);
                t = t * t * (3f - 2f * t);
                float radius = revealMaxRange + (speed * fadeElapsed);
                float alphaMul = 1f - t;
                float thicknessScale = Mathf.Lerp(1f, Mathf.Clamp(clickPingEndThicknessScale, 0.1f, 1f), t);
                TriggerClickPingImpacts(radius);
                BuildClickPingMesh(origin, radius, alphaMul, thicknessScale);
                yield return null;
            }
        }
        else
        {
            TriggerClickPingImpacts(visualMaxRange + clickPingThickness);
        }

        SetClickPingVisible(false);
        _pendingPingReveals.Clear();
        _clickPingRoutine = null;
    }

    void BuildClickPingRayStops(Vector2 origin, float maxVisualRange)
    {
        int count = Mathf.Max(8, clickPingRayCount);

        if (_clickPingRayDirs == null || _clickPingRayDirs.Length != count)
            _clickPingRayDirs = new Vector2[count];

        if (_clickPingStopDistances == null || _clickPingStopDistances.Length != count)
            _clickPingStopDistances = new float[count];
        if (_clickPingHitPoints == null || _clickPingHitPoints.Length != count)
            _clickPingHitPoints = new Vector2[count];
        if (_clickPingHitNormals == null || _clickPingHitNormals.Length != count)
            _clickPingHitNormals = new Vector2[count];
        if (_clickPingHasObstacleHit == null || _clickPingHasObstacleHit.Length != count)
            _clickPingHasObstacleHit = new bool[count];
        if (_clickPingImpactTriggered == null || _clickPingImpactTriggered.Length != count)
            _clickPingImpactTriggered = new bool[count];

        float maxRange = Mathf.Max(0.01f, maxVisualRange);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float ang = t * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            _clickPingRayDirs[i] = dir;

            float dist = maxRange;
            _clickPingHasObstacleHit[i] = false;
            _clickPingImpactTriggered[i] = false;
            _clickPingHitPoints[i] = origin + dir * maxRange;
            _clickPingHitNormals[i] = -dir;
            if (!piercingUpgrade)
            {
                RaycastHit2D hit = Physics2D.Raycast(origin, dir, maxRange, obstacleMask);
                if (hit.collider != null)
                {
                    dist = Mathf.Max(0f, hit.distance);
                    _clickPingHasObstacleHit[i] = true;
                    _clickPingHitPoints[i] = hit.point;
                    _clickPingHitNormals[i] = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : -dir;
                }
            }

            _clickPingStopDistances[i] = dist;
        }
    }

    void BuildPingRevealCandidates(Vector2 origin)
    {
        _pendingPingReveals.Clear();

        Collider2D[] candidates = Physics2D.OverlapCircleAll(origin, clickPingRange, revealableMask);
        if (candidates == null || candidates.Length == 0)
            return;

        PlayerVisionField vision = VisionField;
        Dictionary<Revealable, float> nearestDistanceByReveal = new();

        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D col = candidates[i];
            if (col == null) continue;

            var reveal = col.GetComponentInParent<Revealable>();
            if (reveal == null || !reveal.CanBeRevealed)
                continue;

            Vector2 closest = col.ClosestPoint(origin);
            if (vision != null && vision.ContainsPoint(closest))
                continue;

            Vector2 toTarget = closest - origin;
            float dist = toTarget.magnitude;
            if (dist <= 0.001f || dist > clickPingRange)
                continue;

            Vector2 dir = toTarget / dist;
            if (!piercingUpgrade)
            {
                RaycastHit2D block = Physics2D.Raycast(origin, dir, dist, obstacleMask);
                if (block.collider != null)
                    continue;
            }

            if (nearestDistanceByReveal.TryGetValue(reveal, out float existing))
            {
                if (dist < existing)
                    nearestDistanceByReveal[reveal] = dist;
            }
            else
            {
                nearestDistanceByReveal.Add(reveal, dist);
            }
        }

        foreach (var kvp in nearestDistanceByReveal)
        {
            _pendingPingReveals.Add(new PingRevealCandidate
            {
                revealable = kvp.Key,
                triggerDistance = kvp.Value
            });
        }

        _pendingPingReveals.Sort((a, b) => a.triggerDistance.CompareTo(b.triggerDistance));
    }

    void TriggerPendingPingReveals(float currentRadius)
    {
        if (_pendingPingReveals.Count == 0)
            return;

        for (int i = _pendingPingReveals.Count - 1; i >= 0; i--)
        {
            PingRevealCandidate candidate = _pendingPingReveals[i];
            if (candidate.revealable == null)
            {
                _pendingPingReveals.RemoveAt(i);
                continue;
            }

            if (currentRadius + (clickPingThickness * 0.5f) < candidate.triggerDistance)
                continue;

            if (candidate.revealable.CanBeRevealed)
                candidate.revealable.Reveal(clickPingRevealDuration);

            _pendingPingReveals.RemoveAt(i);
        }
    }

    void TriggerClickPingImpacts(float currentRadius)
    {
        if (!clickPingImpactPops || impactPool == null)
            return;
        if (_clickPingStopDistances == null || _clickPingHasObstacleHit == null || _clickPingImpactTriggered == null)
            return;

        int step = Mathf.Max(1, clickPingImpactRayStep);
        float threshold = currentRadius + (clickPingThickness * 0.5f);
        Color impactColor = new Color(clickPingColor.r, clickPingColor.g, clickPingColor.b, clickPingImpactAlpha);

        for (int i = 0; i < _clickPingStopDistances.Length; i += step)
        {
            if (_clickPingImpactTriggered[i] || !_clickPingHasObstacleHit[i])
                continue;

            if (threshold < _clickPingStopDistances[i])
                continue;

            _clickPingImpactTriggered[i] = true;
            SpawnClickPingImpact(i, impactColor);
        }
    }

    void SpawnClickPingImpact(int index, Color color)
    {
        if (impactPool == null) return;
        if (_clickPingHitPoints == null || _clickPingHitNormals == null) return;
        if (index < 0 || index >= _clickPingHitPoints.Length) return;

        SonarImpactStreak streak = impactPool.Get();
        if (streak == null) return;

        Vector2 point = _clickPingHitPoints[index];
        Vector2 normal = _clickPingHitNormals[index];
        float rotationDeg = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg + 90f;

        streak.Play(
            point,
            clickPingImpactWidth,
            clickPingImpactHeight,
            rotationDeg,
            color,
            clickPingImpactLifetime);
    }

    void EnsureClickPingVisual()
    {
        if (_clickPingMeshFilter != null && _clickPingMeshRenderer != null && _clickPingMesh != null)
            return;

        Transform existing = transform.Find("__ClickPingVisual");
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("__ClickPingVisual");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, clickPingZOffset);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }

        _clickPingMeshFilter = go.GetComponent<MeshFilter>();
        if (_clickPingMeshFilter == null)
            _clickPingMeshFilter = go.AddComponent<MeshFilter>();

        _clickPingMeshRenderer = go.GetComponent<MeshRenderer>();
        if (_clickPingMeshRenderer == null)
            _clickPingMeshRenderer = go.AddComponent<MeshRenderer>();

        if (_clickPingMesh == null)
        {
            _clickPingMesh = new Mesh { name = "ClickPingRingMesh" };
            _clickPingMesh.MarkDynamic();
        }
        _clickPingMeshFilter.sharedMesh = _clickPingMesh;

        if (_clickPingMaterialInstance == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");

            if (shader != null)
                _clickPingMaterialInstance = new Material(shader);
        }

        if (_clickPingMaterialInstance != null)
            _clickPingMeshRenderer.sharedMaterial = _clickPingMaterialInstance;

        _clickPingMpb ??= new MaterialPropertyBlock();
        go.layer = gameObject.layer;
        _clickPingMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _clickPingMeshRenderer.receiveShadows = false;
        _clickPingMeshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _clickPingMeshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        var spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            _clickPingMeshRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            _clickPingMeshRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        }

        SetClickPingVisible(false);
    }

    void SetClickPingVisible(bool visible)
    {
        if (_clickPingMeshRenderer == null) return;

        if (!visible && _clickPingMesh != null)
            _clickPingMesh.Clear();

        _clickPingMeshRenderer.enabled = visible;
    }

    void BuildClickPingMesh(Vector2 origin, float radius, float alphaMultiplier, float thicknessScale = 1f)
    {
        if (_clickPingMesh == null || _clickPingMeshRenderer == null)
            return;

        if (_clickPingRayDirs == null || _clickPingStopDistances == null || _clickPingRayDirs.Length == 0)
        {
            _clickPingMesh.Clear();
            return;
        }

        int count = _clickPingRayDirs.Length;
        int ringPoints = count + 1; // close the loop
        const int bandsPerPoint = 5; // tail start, tail mid, inner edge, center, outer edge
        int vertexCount = ringPoints * bandsPerPoint;
        int triangleCount = count * (bandsPerPoint - 1) * 2 * 3;

        Vector3[] vertices = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] triangles = new int[triangleCount];

        float effectiveThickness = Mathf.Max(0.01f, clickPingThickness * Mathf.Max(0.05f, thicknessScale));
        float halfThickness = Mathf.Max(0.005f, effectiveThickness * 0.5f);
        float tailLength = Mathf.Max(0f, clickPingTailLength);
        float edgeAlpha = Mathf.Clamp01(clickPingEdgeAlpha) * Mathf.Clamp01(alphaMultiplier);
        float midAlpha = Mathf.Clamp01(clickPingMidAlpha) * Mathf.Clamp01(alphaMultiplier);
        float tailAlpha = Mathf.Clamp01(clickPingTailAlpha) * Mathf.Clamp01(alphaMultiplier);
        Color edgeColor = new Color(clickPingColor.r, clickPingColor.g, clickPingColor.b, edgeAlpha);
        Color midColor = new Color(clickPingColor.r, clickPingColor.g, clickPingColor.b, midAlpha);
        Color tailMidColor = new Color(clickPingColor.r, clickPingColor.g, clickPingColor.b, midAlpha * tailAlpha);
        Color tailStartColor = new Color(clickPingColor.r, clickPingColor.g, clickPingColor.b, 0f);

        for (int i = 0; i < ringPoints; i++)
        {
            int src = (i == count) ? 0 : i;
            Vector2 dir = _clickPingRayDirs[src];
            float stop = _clickPingStopDistances[src];
            float ringCenter = Mathf.Min(radius, stop);
            float inner = Mathf.Max(0f, ringCenter - halfThickness);
            float outer = Mathf.Min(stop, ringCenter + halfThickness);

            // Keep a visible band when the wave front hits a wall.
            if (ringCenter >= stop && stop > 0f)
                inner = Mathf.Max(0f, stop - effectiveThickness);

            float tailStart = Mathf.Max(0f, inner - tailLength);
            float tailMid = Mathf.Lerp(inner, tailStart, 0.5f);

            int vi = i * bandsPerPoint;
            vertices[vi] = dir * tailStart;
            vertices[vi + 1] = dir * tailMid;
            vertices[vi + 2] = dir * inner;
            vertices[vi + 3] = dir * ringCenter;
            vertices[vi + 4] = dir * outer;

            colors[vi] = tailStartColor;
            colors[vi + 1] = tailMidColor;
            colors[vi + 2] = edgeColor; // strong inner ring edge
            colors[vi + 3] = midColor;  // softer center
            colors[vi + 4] = edgeColor; // strong outer ring edge
        }

        int ti = 0;
        for (int i = 0; i < count; i++)
        {
            int a = i * bandsPerPoint;
            int b = (i + 1) * bandsPerPoint;

            for (int band = 0; band < bandsPerPoint - 1; band++)
            {
                int a0 = a + band;
                int a1 = a + band + 1;
                int b0 = b + band;
                int b1 = b + band + 1;

                triangles[ti++] = a0; triangles[ti++] = a1; triangles[ti++] = b1;
                triangles[ti++] = a0; triangles[ti++] = b1; triangles[ti++] = b0;
            }
        }

        _clickPingMesh.Clear();
        _clickPingMesh.vertices = vertices;
        _clickPingMesh.colors = colors;
        _clickPingMesh.triangles = triangles;
        _clickPingMesh.RecalculateBounds();
        _clickPingMesh.bounds = new Bounds(Vector3.zero, Vector3.one * (clickPingRange * 2f + 1f));

        Transform tr = _clickPingMeshRenderer.transform;
        tr.position = new Vector3(origin.x, origin.y, transform.position.z + clickPingZOffset);
        tr.rotation = Quaternion.identity;
        tr.localScale = Vector3.one;

        if (_clickPingMpb != null)
        {
            _clickPingMeshRenderer.GetPropertyBlock(_clickPingMpb);
            _clickPingMpb.SetColor(ColorId, Color.white);
            _clickPingMeshRenderer.SetPropertyBlock(_clickPingMpb);
        }
    }

    void OnDestroy()
    {
        if (_clickPingMesh != null)
        {
            Destroy(_clickPingMesh);
            _clickPingMesh = null;
        }

        if (_clickPingMaterialInstance != null)
        {
            Destroy(_clickPingMaterialInstance);
            _clickPingMaterialInstance = null;
        }
    }
}
