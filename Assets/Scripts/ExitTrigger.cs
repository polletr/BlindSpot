using UnityEngine;

public class ExitTrigger : MonoBehaviour
{
    [SerializeField] private LayerMask playerLayer;
    private bool _used;

    private void OnTriggerEnter(Collider other)
    {
        if (_used) return;
        if ((playerLayer.value & (1 << other.gameObject.layer)) == 0) return;

        _used = true;

        ExitFlowController.Instance.OnExitReached();
    }
}
