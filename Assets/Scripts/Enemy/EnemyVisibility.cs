using UnityEngine;

public class EnemyVisibility : MonoBehaviour
{
    public SpriteRenderer mainRenderer;         // Lit triangle
    public float visibleRadius = 2.5f;          // match your light radius
    public float visibleRadiusHysteresis = 0.3f; // prevents flicker at edge

    public Transform player { get; set; }
    bool currentlyVisible;

    void Reset()
    {
        mainRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        if (!player || !mainRenderer) return;

        float d = Vector2.Distance(player.position, transform.position);

        float on = visibleRadius;
        float off = visibleRadius + visibleRadiusHysteresis;

        if (!currentlyVisible && d <= on) currentlyVisible = true;
        else if (currentlyVisible && d >= off) currentlyVisible = false;

        mainRenderer.enabled = currentlyVisible;
    }
}
