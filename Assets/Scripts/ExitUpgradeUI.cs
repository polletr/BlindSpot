using UnityEngine;

public class ExitUpgradeUI : MonoBehaviour
{
    [SerializeField] private UpgradeCard cardLeft;
    [SerializeField] private UpgradeCard cardRight;

    public void Show(System.Action<RunUpgrade> onSelected)
    {
        gameObject.SetActive(true);

        var manager = UpgradeManager.Instance;
        if (manager == null)
        {
            ConfigureCard(cardLeft, null, 0, null);
            ConfigureCard(cardRight, null, 0, null);
            return;
        }

        var options = manager.GetRandomUpgrades(2);
        ConfigureCard(cardLeft, options, 0, onSelected);
        ConfigureCard(cardRight, options, 1, onSelected);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private static void ConfigureCard(UpgradeCard card, RunUpgrade[] options, int index, System.Action<RunUpgrade> onSelected)
    {
        if (card == null)
            return;

        bool hasOption = options != null && index < options.Length && options[index] != null;
        card.gameObject.SetActive(hasOption);
        if (!hasOption)
            return;

        card.Setup(options[index], onSelected);
    }
}
