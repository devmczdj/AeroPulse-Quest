using UnityEngine;

// A short opacity-only entrance keeps the collider and follow target stable.
[RequireComponent(typeof(CanvasGroup))]
public class HudReveal : MonoBehaviour
{
    public float duration = 0.18f;
    CanvasGroup group;
    float startedAt;
    bool playing;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        Play();
    }

    public void Play()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        startedAt = Time.unscaledTime;
        playing = true;
    }

    void Update()
    {
        if (!playing) return;
        float t = duration <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
        group.alpha = Mathf.SmoothStep(0f, 1f, t);
        if (t >= 1f) playing = false;
    }
}
