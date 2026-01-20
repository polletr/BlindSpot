using UnityEngine;

public class CurrencyCollector : MonoBehaviour
{
    [Tooltip("Where drops should fly to (e.g., chest/center). If null, uses this transform.")]
    public Transform collectTarget;

    public Transform Target => collectTarget != null ? collectTarget : transform;

    private void Reset()
    {
        collectTarget = transform;
    }
}

