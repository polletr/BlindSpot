using UnityEngine;

public class DropTriggerForwarder : MonoBehaviour
{
    public enum TriggerType { Magnet, Collect }

    [SerializeField] private TriggerType type;
    [SerializeField] private CurrencyDrop drop;

    private void Reset()
    {
        drop = GetComponentInParent<CurrencyDrop>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (drop == null) return;

        switch (type)
        {
            case TriggerType.Magnet:
                drop.OnMagnetTriggerEnter(other);
                break;
            case TriggerType.Collect:
                drop.OnCollectTriggerEnter(other);
                break;
        }
    }
}
