using UnityEngine;
using UnityEngine.UI;

public class BlopOrbUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image liquidFill;

    [Header("Smoothing")]
    [SerializeField] private bool smooth = true;
    [SerializeField] private float smoothSpeed = 10f;

    private int currentBlops;
    private int currentMax;
    private float targetFill;

    private void Awake()
    {
        if (liquidFill != null)
            liquidFill.fillAmount = 0f;
    }

    private void Update()
    {
        if (!smooth || liquidFill == null) return;

        liquidFill.fillAmount = Mathf.MoveTowards(
            liquidFill.fillAmount,
            targetFill,
            smoothSpeed * Time.unscaledDeltaTime
        );
    }
    public void ApplyFill(int value, int capacity)
    {
        currentMax = Mathf.Max(1, capacity);
        currentBlops = Mathf.Clamp(value, 0, currentMax);
        targetFill = (float)currentBlops / currentMax;

        if (!smooth && liquidFill != null)
            liquidFill.fillAmount = targetFill;
    }
}
