using UnityEngine;

public class CurrencyWallet : MonoBehaviour
{
    [SerializeField] private int total;

    public int Total => total;

    public void Add(int amount)
    {
        if (amount <= 0) return;
        total += amount;
        // Optionally: raise an event for UI updates.
        // OnCurrencyChanged?.Invoke(total);
    }
}
