using UnityEngine;

public class Revealable : MonoBehaviour
{
    [Tooltip("Only these renderers will be revealed (e.g., sonar overlay).")]
    [SerializeField] private SpriteRenderer[] renderers;

    public bool hideOnStart = true;
    public float fadeOutTime = 0.25f;

    float visibleUntil;
    float currentAlpha;

    void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<SpriteRenderer>();

        if (hideOnStart) SetAlpha(0f);
        else currentAlpha = (renderers.Length > 0) ? renderers[0].color.a : 1f;
    }

    public void Reveal(float duration)
    {
        Debug.Log("Revealing");
        visibleUntil = Mathf.Max(visibleUntil, Time.time + duration);
        SetAlpha(1f);
    }

    void Update()
    {
        if (Time.time <= visibleUntil) return;

        currentAlpha = Mathf.MoveTowards(currentAlpha, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeOutTime));
        SetAlpha(currentAlpha);
    }

    void SetAlpha(float a)
    {
        currentAlpha = a;
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i]) continue;
            var c = renderers[i].color;
            c.a = a;
            renderers[i].color = c;
        }
    }
}
