using System;
using UnityEngine;

public class BlopWallet : MonoBehaviour
{
    [Header("Balance")]
    [SerializeField, Min(0)] private int currentAmount;
    [SerializeField, Min(1)] private int capacity = 10;

    public bool HasBlops => currentAmount > 0;

    public bool CanCollect => currentAmount < capacity;

    private void OnValidate()
    {
        SetCapacity(capacity);
        SetTotal(currentAmount);
    }

    public void SetCapacity(int newCapacity, bool clampCurrent = true)
    {
        newCapacity = Mathf.Max(1, newCapacity);
        if (capacity == newCapacity)
        {
            if (clampCurrent)
                SetTotal(currentAmount);
            return;
        }

        capacity = newCapacity;

        if (clampCurrent && currentAmount > capacity)
            currentAmount = capacity;

        NotifyChanged();
    }

    public void SetTotal(int value)
    {
        int clamped = Mathf.Clamp(value, 0, capacity);
        if (clamped == currentAmount) return;

        currentAmount = clamped;
        NotifyChanged();
    }

    public void Add(int amount)
    {
        if (amount == 0) return;

        int newTotal = Mathf.Clamp(currentAmount + amount, 0, capacity);
        if (newTotal == currentAmount) return;

        currentAmount = newTotal;
        NotifyChanged();
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (currentAmount < amount) return false;

        currentAmount -= amount;
        NotifyChanged();
        return true;
    }

    private void NotifyChanged()
    {
        GameFlowManager.Instance.HandleBlopsChanged(currentAmount, capacity);
    }
}
