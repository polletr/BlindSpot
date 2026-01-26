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
            SetIcon(null);
            return;
        }

        SetTextSafe(titleText, string.IsNullOrWhiteSpace(_upgrade.upgradeName) ? _upgrade.name : _upgrade.upgradeName);
        SetTextSafe(descriptionText, _upgrade.description ?? "");

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

}

