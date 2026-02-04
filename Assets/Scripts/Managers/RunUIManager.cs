using DG.Tweening;
using UnityEngine;

/// <summary>
/// Minimal UI glue for the run scene death/victory overlays.
/// Wire the button OnClick events to the public handlers.
/// </summary>
public class RunUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private BlopOrbUI blopOrbUI;

    [Header("Fade Settings")]
    [SerializeField] private float panelFadeDuration = 0.25f;
    [SerializeField] private Ease panelFadeEase = Ease.OutQuad;

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
        flow.BlopsChanged += BlopChanged;
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
        flow.BlopsChanged -= BlopChanged;
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
        FadeCanvasObject(deathPanel, true);
        FadeCanvasObject(victoryPanel, false);
        if (blopOrbUI != null)
            FadeCanvasObject(blopOrbUI.gameObject, false);
    }

    private void HideDeath()
    {
        FadeCanvasObject(deathPanel, false);
        if (blopOrbUI != null)
            FadeCanvasObject(blopOrbUI.gameObject, true);
    }

    private void ShowVictory()
    {
        FadeCanvasObject(victoryPanel, true);
        FadeCanvasObject(deathPanel, false);
        if (blopOrbUI != null)
            FadeCanvasObject(blopOrbUI.gameObject, false);
    }

    private void HideAll()
    {
        FadeCanvasObject(deathPanel, false, 0f);
        FadeCanvasObject(victoryPanel, false, 0f);
        if (blopOrbUI != null)
            FadeCanvasObject(blopOrbUI.gameObject, true, 0f);
    }

    private void BlopChanged(int currentAmount, int maxAmount)
    {
        blopOrbUI?.ApplyFill(currentAmount, maxAmount);
    }

    /// <summary>
    /// Fades any UI object by ensuring it has a CanvasGroup (recursively) and animating the alpha.
    /// </summary>
    /// <param name="target">The root object to fade.</param>
    /// <param name="show">True to fade in, false to fade out.</param>
    /// <param name="durationOverride">Optional override duration; if omitted uses panelFadeDuration.</param>
    public void FadeCanvasObject(GameObject target, bool show, float? durationOverride = null)
    {
        if (target == null)
            return;

        if (show)
        {
            if (!target.activeSelf)
                target.SetActive(true);
        }
        else if (!target.activeInHierarchy)
        {
            target.SetActive(false);
            return;
        }

        var groups = target.GetComponentsInChildren<CanvasGroup>(includeInactive: true);
        if (groups.Length == 0)
        {
            var fallback = target.GetComponent<CanvasGroup>();
            if (fallback == null)
                fallback = target.AddComponent<CanvasGroup>();
            groups = new[] { fallback };
        }

        float duration = durationOverride ?? panelFadeDuration;
        int remaining = groups.Length;

        void HandleComplete()
        {
            remaining--;
            if (remaining <= 0 && !show)
                target.SetActive(false);
        }

        foreach (var group in groups)
        {
            if (group == null)
            {
                HandleComplete();
                continue;
            }

            group.DOKill();
            group.interactable = show;
            group.blocksRaycasts = show;

            if (duration <= 0f)
            {
                group.alpha = show ? 1f : 0f;
                HandleComplete();
                continue;
            }

            group.DOFade(show ? 1f : 0f, duration)
                .SetEase(panelFadeEase)
                .OnComplete(HandleComplete);
        }

        if (duration <= 0f && !show)
            target.SetActive(false);
    }
}
