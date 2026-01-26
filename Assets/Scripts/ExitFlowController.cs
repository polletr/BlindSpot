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

        UpgradeManager.Instance.ApplyUpgrade(upgrade);
        // MapFlowController.Instance.LoadNextMap();
    }
}
