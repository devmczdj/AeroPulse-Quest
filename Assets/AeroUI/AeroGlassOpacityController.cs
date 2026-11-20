using UnityEngine;

public enum AeroGlassOpacityMode
{
    Auto,
    Manual
}

// Samples the readable environment cubemap and applies one alpha multiplier to registered glass.
public sealed class AeroGlassOpacityController : MonoBehaviour
{
    const string ModeKey = "AeroPulse.GlassOpacityMode";
    const string ManualKey = "AeroPulse.GlassManualTransparency";
    const float DefaultManual = .22f;
    const float MinimumTransparency = .05f;
    const float MaximumTransparency = .60f;
    const float AutoMinimum = .06f;
    const float AutoMaximum = .38f;
    const float SampleInterval = .5f;

    public AeroGlassOpacityMode Mode { get; private set; }
    public float ManualTransparency { get; private set; }
    public float EffectiveTransparency { get; private set; }
    public float BackgroundLuminance { get; private set; } = .5f;
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
            Vector3 down = -eye.transform.up;
            float sum = Luminance(forward) + Luminance((forward - right * .22f + down * .15f).normalized) +
                Luminance((forward + right * .22f + down * .15f).normalized) +
                Luminance((forward + down * .3f).normalized);
            BackgroundLuminance = sum * .25f;
        }
        if (Mode == AeroGlassOpacityMode.Auto)
        {
            float t = Mathf.InverseLerp(.22f, .82f, BackgroundLuminance);
            float target = Mathf.Lerp(AutoMaximum, AutoMinimum, t);
            EffectiveTransparency = Mathf.MoveTowards(EffectiveTransparency, target,
                Mathf.Max(.06f, Time.unscaledDeltaTime * .35f));
        }
    }

    float Luminance(Vector3 direction)
    {
        var color = Sample(direction);
        return color.r * .2126f + color.g * .7152f + color.b * .0722f;
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
            : Mathf.Lerp(AutoMaximum, AutoMinimum, Mathf.InverseLerp(.22f, .82f, BackgroundLuminance));
        Apply();
    }
#endif
}
