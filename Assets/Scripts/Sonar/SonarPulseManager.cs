using UnityEngine;

public class SonarPulseManager : Singleton<SonarPulseManager>
{
    [Header("References")]
    [SerializeField] private SonarPulseInstance pulsePrefab;
    [SerializeField] private Transform pulseContainer;

    public void PlayPulse(Vector3 worldPosition, SonarPulseData data)
    {
        if (pulsePrefab == null)
        {
            Debug.LogWarning("SonarPulseManager: No pulse prefab assigned.");
            return;
        }

        SonarPulseInstance pulse = Instantiate(
            pulsePrefab,
            worldPosition,
            Quaternion.identity,
            pulseContainer
        );

        pulse.Play(data);
    }
}