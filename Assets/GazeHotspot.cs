using System.Collections;
using UnityEngine;

// AeroPulse MVP v0.2.1 - a floating landmark marker that reacts to the player's gaze.
public class GazeHotspot : MonoBehaviour
{
    public int id = 1;
    public string label = "PEAK";
    public Color baseColor = new Color(1f, 0.62f, 0.15f);
    public Color activeColor = new Color(0.25f, 0.95f, 0.55f);
    public bool Activated { get; private set; }

    Renderer rend;
    Transform panel;
    float baseScale = 0.5f;
    bool gazed;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        baseScale = transform.localScale.x;
        if (rend != null) rend.material.color = baseColor;
        BuildPanel();
    }

    void BuildPanel()
    {
        // Info card floating above the hotspot: big number + label.
        // Latin letters/digits only, so the built-in font always renders.
        var go = new GameObject("InfoPanel");
        go.transform.SetParent(transform, false);
        float inv = 1f / Mathf.Max(baseScale, 0.001f);
        go.transform.localScale = Vector3.one * inv;
        go.transform.localPosition = new Vector3(0f, 1.1f * inv, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.text = id + "\n" + label;
        tm.fontSize = 64;
        tm.characterSize = 0.14f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        panel = go.transform;
        go.SetActive(false);
    }

    void Update()
    {
        float target = baseScale * (gazed && !Activated ? 1.18f : 1f);
        float s = Mathf.Lerp(transform.localScale.x, target, Time.deltaTime * 8f);
        transform.localScale = Vector3.one * s;
        if (panel != null && panel.gameObject.activeSelf)
        {
            Vector3 player = new Vector3(0f, 1.6f, 0f);
            Vector3 away = panel.position - player;
            if (away.sqrMagnitude > 0.001f)
                panel.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }
    }

    public void SetGazed(bool value) { gazed = value; }

    public void Activate()
    {
        if (Activated) return;
        Activated = true;
        if (rend != null) rend.material.color = activeColor;
        if (panel != null) panel.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(Pop());
    }

    IEnumerator Pop()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2.2f;
            float k = 1f + Mathf.Sin(Mathf.Min(t, 1f) * Mathf.PI) * 0.45f;
            transform.localScale = Vector3.one * baseScale * k;
            yield return null;
        }
        transform.localScale = Vector3.one * baseScale;
    }
}
