using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Simple button glue for the main menu scene.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public void HandlePlayPressed()
    {
        GameFlowManager.Instance?.StartRunFromMenu();
    }

    public void HandleQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

