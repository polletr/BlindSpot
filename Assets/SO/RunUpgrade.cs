using UnityEngine;

[CreateAssetMenu(menuName = "Run/Upgrade")]
public class RunUpgrade : ScriptableObject
{
    public string upgradeName;
    [TextArea] public string description;

    public float velocityTempMultiplier = 1f;
    public float dashDistanceTempMultiplier = 1f;
    public float flashlightAngleTempMultiplier = 1f;
    public float flashlightRangeTempMultiplier = 1f;
    public float roundLightTempMultiplier = 1f;
    public float currencyCollectorTempMultiplier = 1f;
    public float scanCooldownTempMultiplier = 1f;
    public float roundLightIntensityTempMultiplier = 1f;
    public float dashCooldownTempMultiplier = 1f;
    public float enemySpeedTempMultiplier = 1f;
    public float enemyDetectionRadiusTempMultiplier = 1f;
    public float enemyLoseSightRadiusTempMultiplier = 1f;
    public float enemyAmountTempMultiplier = 1f;
    public float blopsAmountTempMultiplier = 1f;

    public bool radarAlwaysOnTemp = false;
}
