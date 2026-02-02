using UnityEngine;

/// <summary>
/// Minimal UI glue for the run scene death/victory overlays.
/// Wire the button OnClick events to the public handlers.
/// </summary>
public class RunUIManager : MonoBehaviour
{
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private GameObject victoryPanel;

    private void OnEnable()
    {
        if (!GameFlowManager.HasInstance)
        {
            HideAll();
            return;
        }

        var flow = GameFlowManager.Instance;
        flow.PlayerDied += ShowDeath;
        flow.PlayerRespawned += HideDeath;
        flow.GameWon += ShowVictory;
        HideAll();
    }

    private void OnDisable()
    {
        if (!GameFlowManager.HasInstance)
            return;

        var flow = GameFlowManager.Instance;
        flow.PlayerDied -= ShowDeath;
        flow.PlayerRespawned -= HideDeath;
        flow.GameWon -= ShowVictory;
    }

    public void HandleRestartPressed()
    {
        GameFlowManager.Instance?.RestartRun();
    }

    public void HandleMainMenuPressed()
    {
        GameFlowManager.Instance?.ReturnToMainMenu();
    }

    public void HandleVictoryContinuePressed()
    {
        GameFlowManager.Instance?.ReturnToMainMenu();
    }

    private void ShowDeath()
    {
        if (deathPanel != null)
            deathPanel.SetActive(true);
        if (victoryPanel != null)
            victoryPanel.SetActive(false);
    }

    private void HideDeath()
    {
        if (deathPanel != null)
            deathPanel.SetActive(false);
    }

    private void ShowVictory()
    {
        if (victoryPanel != null)
            victoryPanel.SetActive(true);
        if (deathPanel != null)
            deathPanel.SetActive(false);
    }

    private void HideAll()
    {
        if (deathPanel != null)
            deathPanel.SetActive(false);
        if (victoryPanel != null)
            victoryPanel.SetActive(false);
    }
}
