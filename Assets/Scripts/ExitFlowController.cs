using UnityEngine;

public class ExitFlowController : Singleton<ExitFlowController>
{
    [SerializeField] private ExitUpgradeUI upgradeUI;

    public void OnExitReached()
    {
        Time.timeScale = 0f; // pause gameplay
        upgradeUI.Show(OnUpgradeSelected);
    }

    private void OnUpgradeSelected(RunUpgrade upgrade)
    {
        Time.timeScale = 1f;

        var upgradeManager = UpgradeManager.Instance;
        if (upgradeManager != null && upgrade != null)
            upgradeManager.ApplyUpgrade(upgrade);

        GameFlowManager.Instance?.HandleDungeonCompleted();
    }
}
