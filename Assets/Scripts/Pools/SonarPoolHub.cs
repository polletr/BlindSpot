using System;
using UnityEngine;

/// <summary>
/// Broadcasts the runtime sonar pools so late-spawned players can subscribe without inspector wiring.
/// Drop this next to the pool instances in the scene.
/// </summary>
public class SonarPoolHub : MonoBehaviour
{
    [SerializeField] private SonarConeVisualPool conePool;
    [SerializeField] private SonarImpactPool impactPool;

    private static SonarConeVisualPool _currentCone;
    private static SonarImpactPool _currentImpact;

    public static event Action<SonarConeVisualPool, SonarImpactPool> PoolsChanged;

    private void OnEnable()
    {
        Publish();
    }

    private void OnDisable()
    {
        if (_currentCone == conePool)
            _currentCone = null;
        if (_currentImpact == impactPool)
            _currentImpact = null;

        Publish();
    }

    public static bool TryGet(out SonarConeVisualPool cone, out SonarImpactPool impact)
    {
        cone = _currentCone;
        impact = _currentImpact;
        return cone != null && impact != null;
    }

    private void Publish()
    {
        if (conePool != null)
            _currentCone = conePool;
        if (impactPool != null)
            _currentImpact = impactPool;

        PoolsChanged?.Invoke(_currentCone, _currentImpact);
    }
}
