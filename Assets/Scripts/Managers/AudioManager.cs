using UnityEngine;
using FMOD.Studio;
using FMODUnity;

public class AudioManager : Singleton<AudioManager>
{
    private EventInstance _musicInstance;
    private EventReference _currentMusicEvent;

    protected override void OnDestroy()
    {
        StopMusic(true);
        base.OnDestroy();
    }

    public void PlayOneShot(EventReference audioEvent)
    {
        if (audioEvent.IsNull)
            return;

        RuntimeManager.PlayOneShot(audioEvent);
    }

    public void PlayOneShot(EventReference audioEvent, Vector3 worldPosition)
    {
        if (audioEvent.IsNull)
            return;

        RuntimeManager.PlayOneShot(audioEvent, worldPosition);
    }

    public void PlayOneShotAttached(EventReference audioEvent, GameObject target)
    {
        if (audioEvent.IsNull || target == null)
            return;

        RuntimeManager.PlayOneShotAttached(audioEvent, target);
    }

    public void PlayMusic(EventReference musicEvent, bool restartIfSame = false)
    {
        if (musicEvent.IsNull)
            return;

        if (IsCurrentMusic(musicEvent))
        {
            if (!restartIfSame)
                return;

            StopMusic(true);
        }

        StopMusic(true);

        _musicInstance = RuntimeManager.CreateInstance(musicEvent);
        _currentMusicEvent = musicEvent;
        _musicInstance.start();

    }

    public void StopMusic(bool allowFadeout = true)
    {
        if (!_musicInstance.isValid())
            return;

        _musicInstance.stop(allowFadeout ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
        _musicInstance.release();
        _musicInstance.clearHandle();
        _currentMusicEvent = default;
    }

    public void SetMusicParameter(string parameterName, float value)
    {
        if (!_musicInstance.isValid() || string.IsNullOrEmpty(parameterName))
            return;

        _musicInstance.setParameterByName(parameterName, value);
    }

    public static void Play(EventReference audioEvent)
    {
        if (!HasInstance)
            return;

        Instance.PlayOneShot(audioEvent);
    }

    public static void PlayAt(EventReference audioEvent, Vector3 worldPosition)
    {
        if (!HasInstance)
            return;

        Instance.PlayOneShot(audioEvent, worldPosition);
    }

    public static void PlayAttached(EventReference audioEvent, GameObject target)
    {
        if (!HasInstance)
            return;

        Instance.PlayOneShotAttached(audioEvent, target);
    }

    public static void PlayBgm(EventReference musicEvent, bool restartIfSame = false)
    {
        if (!HasInstance)
            return;

        Instance.PlayMusic(musicEvent, restartIfSame);
    }

    public static void StopBgm(bool allowFadeout = true)
    {
        if (!HasInstance)
            return;

        Instance.StopMusic(allowFadeout);
    }

    public static void SetBgmParameter(string parameterName, float value)
    {
        if (!HasInstance)
            return;

        Instance.SetMusicParameter(parameterName, value);
    }

    private bool IsCurrentMusic(EventReference candidate)
    {
        return _musicInstance.isValid() && _currentMusicEvent.Guid.Equals(candidate.Guid);
    }
}
