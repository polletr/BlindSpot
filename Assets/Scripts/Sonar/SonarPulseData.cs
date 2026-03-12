using UnityEngine;

[System.Serializable]
public class SonarPulseData
{
    public float maxRadius = 5f;
    public float duration = 0.8f;
    public float fadeOutTime = 0.2f;

    [Header("Visual")]
    public AnimationCurve radiusCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

}