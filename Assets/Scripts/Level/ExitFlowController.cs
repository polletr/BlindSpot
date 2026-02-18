using UnityEngine;

public class ExitFlowController : Singleton<ExitFlowController>
{
    [SerializeField] private ExitUpgradeUI upgradeUI;
    private bool _selectionCommitted;

    public void OnExitReached()
    {
        _selectionCommitted = false;
        GameFlowManager.Instance?.HandlePlayerReachedExit();
        Time.timeScale = 0f; // pause gameplay
        bool canSelectUpgrade = upgradeUI != null && upgradeUI.Show(OnUpgradeSelected);
        if (!canSelectUpgrade)
        {
            Time.timeScale = 1f;
            Debug.LogWarning("[ExitFlowController] Exit upgrade UI could not present options. Dungeon progression was cancelled.");
        }
    }

    private void OnUpgradeSelected(RunUpgrade upgrade)
    {
        if (_selectionCommitted)
            return;

        _selectionCommitted = true;
        if (upgradeUI != null)
            upgradeUI.Hide();

        Time.timeScale = 1f;

        var upgradeManager = UpgradeManager.Instance;
        if (upgradeManager != null && upgrade != null)
            upgradeManager.ApplyUpgrade(upgrade);

        GameFlowManager.Instance?.HandleDungeonCompleted();
    }
}
