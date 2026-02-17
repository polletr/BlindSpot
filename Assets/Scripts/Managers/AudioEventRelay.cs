using FMODUnity;
using UnityEngine;

public class AudioEventRelay : MonoBehaviour
{
    public enum RelayMode
    {
        OneShot2D,
        OneShotAtPosition3D,
        OneShotAttached3D,
        PlayBgm,
        StopBgm
    }

    [Header("Event")]
    [SerializeField] private EventReference audioEvent;
    [SerializeField] private RelayMode mode = RelayMode.OneShot2D;

    [Header("Behaviour")]
    [SerializeField] private bool triggerOnEnable;
    [SerializeField] private bool restartMusicIfSame;
    [SerializeField] private bool stopBgmAllowFadeout = true;

    public void Trigger()
    {
        switch (mode)
        {
            case RelayMode.OneShot2D:
                AudioManager.Play(audioEvent);
                break;

            case RelayMode.OneShotAtPosition3D:
                AudioManager.PlayAt(audioEvent, transform.position);
                break;

            case RelayMode.OneShotAttached3D:
                AudioManager.PlayAttached(audioEvent, gameObject);
                break;

            case RelayMode.PlayBgm:
                AudioManager.PlayBgm(audioEvent, restartMusicIfSame);
                break;

            case RelayMode.StopBgm:
                AudioManager.StopBgm(stopBgmAllowFadeout);
                break;
        }
    }

    private void OnEnable()
    {
        if (triggerOnEnable)
            Trigger();
    }
}
