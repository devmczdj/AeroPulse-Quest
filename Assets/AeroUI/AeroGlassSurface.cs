using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Registers runtime glass backings so one policy can tune alpha without fading text.
public sealed class AeroGlassSurface : MonoBehaviour
{
    public static readonly List<AeroGlassSurface> Active = new List<AeroGlassSurface>();
    public float BaseAlpha { get; private set; }
    public float CurrentAlpha => image != null ? image.color.a : 0f;

    Image image;
    Color baseColor;

    public void Initialize(Color color)
    {
        image = GetComponent<Image>();
        baseColor = color;
        BaseAlpha = color.a;
        if (!Active.Contains(this)) Active.Add(this);
        ApplyScale(AeroGlassOpacityController.SharedAlphaScale);
    }

    public void ApplyScale(float scale)
    {
        if (image == null) image = GetComponent<Image>();
        if (image == null) return;
        var color = baseColor;
        color.a = Mathf.Clamp01(baseColor.a * Mathf.Clamp(scale, .2f, 1.1f));
        image.color = color;
    }

    void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
        if (image != null) ApplyScale(AeroGlassOpacityController.SharedAlphaScale);
    }

    void OnDestroy()
    {
        Active.Remove(this);
    }
}
