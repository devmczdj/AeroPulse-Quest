using UnityEngine;

public enum AeroGlassOpacityMode
{
    Auto,
    Manual
}

// Samples the readable environment cubemap, then uses white-text contrast to choose glass opacity.
public sealed class AeroGlassOpacityController : MonoBehaviour
{
    const string ModeKey = "AeroPulse.GlassOpacityMode";
    const string ManualKey = "AeroPulse.GlassManualTransparency";
    const float DefaultManual = .22f;
    const float MinimumTransparency = .05f;
    const float MaximumTransparency = .60f;
    const float AutoOpaqueTransparency = .04f;
    const float AutoClearTransparency = .55f;
    const float LowContrast = 3f;
    const float HighContrast = 7f;
    const float SampleInterval = .35f;
    const float HighlightWeight = .4f;
    const int HorizontalSamples = 7;
    const int VerticalSamples = 5;
    const float HorizontalHalfSpan = .32f;
    const float VerticalHalfSpan = .18f;

    public AeroGlassOpacityMode Mode { get; private set; }
    public float ManualTransparency { get; private set; }
    public float EffectiveTransparency { get; private set; }
    public float BackgroundLuminance { get; private set; } = .5f;
    public float BackgroundContrast { get; private set; } = 1f;
    public float CurrentAlphaScale => 1f - EffectiveTransparency;
    public bool Ready { get; private set; }
    public static float SharedAlphaScale { get; private set; } = 1f;

    Camera eye;
    Cubemap panorama;
    float nextSample;

    public static AeroGlassOpacityController Build(Camera camera, Transform parent = null)
    {
        var root = new GameObject("GlassOpacityController");
        if (parent != null) root.transform.SetParent(parent, false);
        var controller = root.AddComponent<AeroGlassOpacityController>();
        controller.eye = camera;
        controller.panorama = Resources.Load<Cubemap>("AeroGlassPanorama");
        controller.Mode = (AeroGlassOpacityMode)Mathf.Clamp(
            PlayerPrefs.GetInt(ModeKey, (int)AeroGlassOpacityMode.Auto), 0, 1);
        controller.ManualTransparency = Mathf.Clamp(
            PlayerPrefs.GetFloat(ManualKey, DefaultManual), MinimumTransparency, MaximumTransparency);
        controller.EffectiveTransparency = controller.Mode == AeroGlassOpacityMode.Manual
            ? controller.ManualTransparency : .2f;
        controller.SampleEnvironment();
        controller.Apply();
        controller.Ready = true;
        return controller;
    }

    public void SetMode(AeroGlassOpacityMode mode)
    {
        Mode = mode;
        PlayerPrefs.SetInt(ModeKey, (int)mode);
        PlayerPrefs.Save();
        if (mode == AeroGlassOpacityMode.Auto) SampleEnvironment();
        Apply();
    }

    public void SetManualTransparency(float value)
    {
        ManualTransparency = Mathf.Clamp(value, MinimumTransparency, MaximumTransparency);
        Mode = AeroGlassOpacityMode.Manual;
        EffectiveTransparency = ManualTransparency;
        PlayerPrefs.SetFloat(ManualKey, ManualTransparency);
        PlayerPrefs.SetInt(ModeKey, (int)Mode);
        PlayerPrefs.Save();
        Apply();
    }

    public void ResetDefaults()
    {
        ManualTransparency = DefaultManual;
        EffectiveTransparency = .2f;
        Mode = AeroGlassOpacityMode.Auto;
        PlayerPrefs.DeleteKey(ManualKey);
        PlayerPrefs.SetInt(ModeKey, (int)Mode);
        PlayerPrefs.Save();
        SampleEnvironment();
        Apply();
    }

    void Update()
    {
        if (Mode != AeroGlassOpacityMode.Auto || Time.unscaledTime < nextSample) return;
        nextSample = Time.unscaledTime + SampleInterval;
        SampleEnvironment();
        Apply();
    }

    void SampleEnvironment()
    {
        if (eye == null || panorama == null)
        {
            BackgroundLuminance = .5f;
        }
        else
        {
            Vector3 forward = eye.transform.forward;
            Vector3 right = eye.transform.right;
            Vector3 up = eye.transform.up;
            float weightedSum = 0f;
            float weightSum = 0f;
            float brightest = 0f;
            for (int y = 0; y < VerticalSamples; y++)
            {
                float normalizedY = VerticalSamples == 1 ? 0f : y / (VerticalSamples - 1f) * 2f - 1f;
                for (int x = 0; x < HorizontalSamples; x++)
                {
                    float normalizedX = x / (HorizontalSamples - 1f) * 2f - 1f;
                    Vector3 direction = (forward + right * (normalizedX * HorizontalHalfSpan) +
                        up * (normalizedY * VerticalHalfSpan)).normalized;
                    float luminance = Luminance(direction);
                    float weight = (1f - Mathf.Abs(normalizedX) * .25f) *
                        (1f - Mathf.Abs(normalizedY) * .35f);
                    weightedSum += luminance * weight;
                    weightSum += weight;
                    brightest = Mathf.Max(brightest, luminance);
                }
            }
            float average = weightedSum / Mathf.Max(.001f, weightSum);
            BackgroundLuminance = Mathf.Lerp(average, brightest, HighlightWeight);
        }
        BackgroundContrast = ContrastRatio(RelativeLuminance(AeroUiTokens.Ink), BackgroundLuminance);
        if (Mode == AeroGlassOpacityMode.Auto)
        {
            float target = TargetTransparencyForLuminance(BackgroundLuminance);
            EffectiveTransparency = Mathf.MoveTowards(EffectiveTransparency, target,
                Mathf.Max(.08f, Time.unscaledDeltaTime * .5f));
        }
    }

    float Luminance(Vector3 direction)
    {
        return RelativeLuminance(Sample(direction));
    }

    public static float TargetTransparencyForLuminance(float backgroundLuminance)
    {
        float contrast = ContrastRatio(RelativeLuminance(AeroUiTokens.Ink), backgroundLuminance);
        float t = SmoothStep01(Mathf.InverseLerp(LowContrast, HighContrast, contrast));
        return Mathf.Lerp(AutoOpaqueTransparency, AutoClearTransparency, t);
    }

    static float ContrastRatio(float first, float second)
    {
        float lighter = Mathf.Max(first, second);
        float darker = Mathf.Min(first, second);
        return (lighter + .05f) / (darker + .05f);
    }

    static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    static float RelativeLuminance(Color color)
    {
        return LinearChannel(color.r) * .2126f +
            LinearChannel(color.g) * .7152f +
            LinearChannel(color.b) * .0722f;
    }

    static float LinearChannel(float value)
    {
        value = Mathf.Clamp01(value);
        return value <= .04045f
            ? value / 12.92f
            : Mathf.Pow((value + .055f) / 1.055f, 2.4f);
    }

    Color Sample(Vector3 direction)
    {
        if (panorama == null) return new Color(.5f, .5f, .5f, 1f);
        direction.Normalize();
        float ax = Mathf.Abs(direction.x), ay = Mathf.Abs(direction.y), az = Mathf.Abs(direction.z);
        CubemapFace face;
        float u, v;
        if (ax >= ay && ax >= az)
        {
            if (direction.x >= 0f) { face = CubemapFace.PositiveX; u = -direction.z / ax; v = -direction.y / ax; }
            else { face = CubemapFace.NegativeX; u = direction.z / ax; v = -direction.y / ax; }
        }
        else if (ay >= az)
        {
            if (direction.y >= 0f) { face = CubemapFace.PositiveY; u = direction.x / ay; v = direction.z / ay; }
            else { face = CubemapFace.NegativeY; u = direction.x / ay; v = -direction.z / ay; }
        }
        else
        {
            if (direction.z >= 0f) { face = CubemapFace.PositiveZ; u = direction.x / az; v = -direction.y / az; }
            else { face = CubemapFace.NegativeZ; u = -direction.x / az; v = -direction.y / az; }
        }
        int x = Mathf.Clamp(Mathf.RoundToInt((u * .5f + .5f) * (panorama.width - 1)), 0, panorama.width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt((v * .5f + .5f) * (panorama.height - 1)), 0, panorama.height - 1);
        return panorama.GetPixel(face, x, y);
    }

    void Apply()
    {
        float scale = CurrentAlphaScale;
        SharedAlphaScale = scale;
        for (int i = AeroGlassSurface.Active.Count - 1; i >= 0; i--)
        {
            var surface = AeroGlassSurface.Active[i];
            if (surface == null) { AeroGlassSurface.Active.RemoveAt(i); continue; }
            surface.ApplyScale(scale);
        }
    }

#if UNITY_EDITOR
    public void RefreshForChecks()
    {
        SampleEnvironment();
        EffectiveTransparency = Mode == AeroGlassOpacityMode.Manual
            ? ManualTransparency
            : TargetTransparencyForLuminance(BackgroundLuminance);
        Apply();
    }
#endif
}
