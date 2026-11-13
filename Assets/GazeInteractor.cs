using UnityEngine;

// AeroPulse MVP v0.2.1 - sits on the main camera. Casts a ray from the
// centre of the view; dwelling on a hotspot for a moment activates it.
// Also builds the centre reticle (procedural sprites, no assets needed).
public class GazeInteractor : MonoBehaviour
{
    public float dwellSeconds = 1.2f;
    public float maxDistance = 30f;
    public bool pinchExperimentEnabled;
    PinchHudExperiment pinchExperiment;

    GazeHotspot current;
    float timer;
    Transform reticle;
    SpriteRenderer[] reticleSprites;

    void Start()
    {
        BuildReticle();
        reticleSprites = reticle.GetComponentsInChildren<SpriteRenderer>();
        if (pinchExperimentEnabled)
        {
            reticle.gameObject.SetActive(false);
            reticle.localPosition = new Vector3(0f, 0f, 0.6f);
            reticle.localScale = Vector3.one * 0.3f;
            pinchExperiment = gameObject.AddComponent<PinchHudExperiment>();
            pinchExperiment.Initialize(GetComponent<Camera>(), maxDistance);
        }
    }

    void BuildReticle()
    {
        var ringTex = MakeRingTexture();
        var dotTex = MakeDotTexture();
        var holder = new GameObject("GazeReticle");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = new Vector3(0f, 0f, 2.0f);
        reticle = holder.transform;

        var ring = new GameObject("Ring");
        ring.transform.SetParent(holder.transform, false);
        var sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(ringTex, new Rect(0, 0, ringTex.width, ringTex.height), new Vector2(0.5f, 0.5f), 256f);
        ring.transform.localScale = Vector3.one * 0.10f;

        var dot = new GameObject("Dot");
        dot.transform.SetParent(holder.transform, false);
        var sd = dot.AddComponent<SpriteRenderer>();
        sd.sprite = Sprite.Create(dotTex, new Rect(0, 0, dotTex.width, dotTex.height), new Vector2(0.5f, 0.5f), 256f);
        dot.transform.localScale = Vector3.one * 0.035f;
    }

    static Texture2D MakeRingTexture()
    {
        int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        var center = new Vector2(s / 2f, s / 2f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = (d > 44f && d < 60f) ? 1f : 0f;
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static Texture2D MakeDotTexture()
    {
        int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        var center = new Vector2(s / 2f, s / 2f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = d < 22f ? 1f : 0f;
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    void Update()
    {
        if (pinchExperiment != null)
        {
            var color = pinchExperiment.HasTrackedHand ? Color.cyan : Color.gray;
            foreach (var sprite in reticleSprites)
                sprite.color = color;
            return;
        }
        GazeHotspot hit = null;
        var ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit info, maxDistance))
            hit = info.collider.GetComponent<GazeHotspot>();

        if (hit != current)
        {
            if (current != null) current.SetGazed(false);
            current = hit;
            timer = 0f;
            if (current != null) current.SetGazed(true);
        }

        if (current != null && !current.Activated)
        {
            timer += Time.deltaTime;
            float k = 1f + 0.8f * Mathf.Clamp01(timer / dwellSeconds);
            if (reticle != null) reticle.localScale = Vector3.one * k;
            if (timer >= dwellSeconds) current.Activate();
        }
        else if (reticle != null)
        {
            reticle.localScale = Vector3.one;
        }
    }
}
