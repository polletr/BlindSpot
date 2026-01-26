using System.Collections;
using UnityEngine;

public class ExitUpgradeUI : MonoBehaviour
{
    [SerializeField] private UpgradeCard cardLeft;
    [SerializeField] private UpgradeCard cardRight;
    [SerializeField] private float fadeDuration = 0.25f;

    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        EnsureCanvasGroup();
        if (canvasGroup != null && !gameObject.activeSelf)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void Show(System.Action<RunUpgrade> onSelected)
    {
        EnsureCanvasGroup();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        StartFade(1f, null);

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
        StartFade(0f, () => gameObject.SetActive(false));
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

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void StartFade(float targetAlpha, System.Action onComplete)
    {
        EnsureCanvasGroup();
        if (canvasGroup == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        bool isShowing = targetAlpha > 0f;
        if (isShowing)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
        else
        {
            canvasGroup.interactable = false;
        }

        fadeRoutine = StartCoroutine(FadeCanvas(Mathf.Clamp01(targetAlpha), () =>
        {
            if (!isShowing)
            {
                canvasGroup.blocksRaycasts = false;
            }

            onComplete?.Invoke();
        }));
    }

    private IEnumerator FadeCanvas(float targetAlpha, System.Action onComplete)
    {
        float startAlpha = canvasGroup.alpha;

        if (Mathf.Approximately(fadeDuration, 0f))
        {
            canvasGroup.alpha = targetAlpha;
            onComplete?.Invoke();
            fadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        onComplete?.Invoke();
        fadeRoutine = null;
    }
}
