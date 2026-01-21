using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple UI card for a run upgrade option.
/// Displays title + description + a quick summary of buffs/debuffs,
/// and invokes a callback when selected.
/// </summary>
public class UpgradeCard : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text effectsText;     // optional: "Buffs / Debuffs" summary
    [SerializeField] private Image iconImage;          // optional
    [SerializeField] private GameObject disabledOverlay; // optional (e.g., "Locked")

    private RunUpgrade _upgrade;
    private Action<RunUpgrade> _onSelected;

    private void Reset()
    {
        // Best-effort auto-wire for convenience (safe if missing)
        button = GetComponentInChildren<Button>();
        var texts = GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length > 0) titleText = texts[0];
        if (texts.Length > 1) descriptionText = texts[1];
        if (texts.Length > 2) effectsText = texts[2];
    }

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    /// <summary>
    /// Initialize the card with an upgrade and a selection callback.
    /// </summary>
    public void Setup(RunUpgrade upgrade, Action<RunUpgrade> onSelected, bool interactable = true)
    {
        _upgrade = upgrade;
        _onSelected = onSelected;

        if (disabledOverlay != null)
            disabledOverlay.SetActive(!interactable);

        if (button != null)
            button.interactable = interactable;

        if (_upgrade == null)
        {
            SetTextSafe(titleText, "—");
            SetTextSafe(descriptionText, "");
            SetTextSafe(effectsText, "");
            SetIcon(null);
            return;
        }

        SetTextSafe(titleText, string.IsNullOrWhiteSpace(_upgrade.upgradeName) ? _upgrade.name : _upgrade.upgradeName);
        SetTextSafe(descriptionText, _upgrade.description ?? "");

        // Optional icon support (only if your RunUpgrade has an icon field)
        // If you add `public Sprite icon;` to RunUpgrade, uncomment:
        // SetIcon(_upgrade.icon);

        if (effectsText != null)
            effectsText.text = BuildEffectsSummary(_upgrade);
    }

    private void HandleClick()
    {
        if (_upgrade == null) return;
        _onSelected?.Invoke(_upgrade);
    }

    private static void SetTextSafe(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    private void SetIcon(Sprite sprite)
    {
        if (iconImage == null) return;

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }

    /// <summary>
    /// Very basic summary builder based on your current RunUpgrade fields.
    /// Expand this as you add more stats/effects.
    /// </summary>
    private static string BuildEffectsSummary(RunUpgrade u)
    {
        // Example formatting; adjust to match your UI tone.
        // Buffs

        /*        string buffs = "";
                if (u.velocityMultiplier > 1f)
                {
                    var pct = Mathf.RoundToInt((u.velocityMultiplier - 1f) * 100f);
                    buffs += $"+{pct}% Damage\n";
                }
                if (u.dashDistanceMultiplier > 0)
                    buffs += $"+{u.dashDistanceMultiplier} Dash Distance\n";

                // Debuffs
                string debuffs = "";
                if (u.damageMultiplier < 1f)
                {
                    var pct = Mathf.RoundToInt((1f - u.damageMultiplier) * 100f);
                    debuffs += $"-{pct}% Damage\n";
                }
                if (u.energyDelta < 0)
                    debuffs += $"{u.energyDelta} Max Energy\n"; // already negative

                if (string.IsNullOrEmpty(buffs)) buffs = "None\n";
                if (string.IsNullOrEmpty(debuffs)) debuffs = "None\n";
        */
        return "Test";
        //return $"BUFFS:\n{buffs}\nDEBUFFS:\n{debuffs}".Trim();
    }
}

