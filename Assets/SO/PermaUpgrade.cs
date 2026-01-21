using UnityEngine;

[CreateAssetMenu(menuName = "Perma/Upgrade")]
public class PermaUpgrade : ScriptableObject
{
    public string upgradeName;
    [TextArea] public string description;

    public float velocityPermMultiplier = 1f;
    public float dashDistancePermMultiplier = 1f;
    public float flashlightAnglePermMultiplier = 1f;
    public float flashlightRangePermMultiplier = 1f;
    public float roundLightPermMultiplier = 1f;
    public float currencyCollectorPermMultiplier = 1f;
    public float scanCooldownPermMultiplier = 1f;
    public float roundLightIntensityPermMultiplier = 1f;
    public float dashCooldownPermMultiplier = 1f;
    public bool radarAlwaysOnPerm = false;

}
