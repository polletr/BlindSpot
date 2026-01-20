using System;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SonarImpactStreak : MonoBehaviour
{
    public Action<SonarImpactStreak> OnFinished;

    SpriteRenderer sr;
    Tween tween;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void OnDisable()
    {
        tween?.Kill();
        tween = null;
    }

    public void Play(
        Vector2 center,
        float width,
        float height,
        float rotationDeg,
        Color color,
        float lifetime)
    {
        gameObject.SetActive(true);

        transform.position = new Vector3(center.x, center.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);

        float safeWidth = Mathf.Max(0.01f, width);
        float safeHeight = Mathf.Max(0.01f, height);

        Vector2 spriteSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        float sx = safeWidth / Mathf.Max(0.001f, spriteSize.x);
        float sy = safeHeight / Mathf.Max(0.001f, spriteSize.y);

        transform.localScale = new Vector3(sx, sy, 1f);

        tween?.Kill();

        // FORCE a visible starting alpha, regardless of prefab state
        float startA = Mathf.Max(0.01f, color.a);
        sr.color = new Color(color.r, color.g, color.b, startA);

        float dur = Mathf.Max(0.05f, lifetime); // avoid "instant vanish"

        tween = sr.DOFade(0f, dur)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => OnFinished?.Invoke(this));
    }
}
