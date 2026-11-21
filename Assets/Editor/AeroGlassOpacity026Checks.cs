using System;
using UnityEngine;

public static class AeroGlassOpacity026Checks
{
    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("GlassOpacity026: " + message);
    }

    public static void Run()
    {
        Require(Application.isPlaying, "Play Mode required");
        var owner = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
        Require(owner != null && owner.GlassOpacity != null, "Glass opacity controller is missing");
        var controller = owner.GlassOpacity;
        var originalMode = controller.Mode;
        float originalManual = controller.ManualTransparency;
        try
        {
            Require(AeroGlassSurface.Active.Count >= 5, "Not enough registered glass surfaces");
            var mainGlass = owner.Follow.GetComponent<AeroHudView>()?.GlassSurface;
            Require(mainGlass != null && mainGlass.GetComponent<AeroGlassSurface>() != null,
                "Main HUD glass is not registered");

            controller.SetManualTransparency(.45f);
            Require(controller.Mode == AeroGlassOpacityMode.Manual, "Slider value did not select manual mode");
            Require(Mathf.Approximately(controller.EffectiveTransparency, .45f), "Manual transparency was not applied");
            foreach (var surface in AeroGlassSurface.Active)
            {
                if (surface == null) continue;
                Require(Mathf.Abs(surface.CurrentAlpha - surface.BaseAlpha * .55f) < .01f,
                    "Registered glass ignored the manual transparency scale");
            }

            controller.SetMode(AeroGlassOpacityMode.Auto);
            controller.RefreshForChecks();
            Require(controller.BackgroundLuminance >= 0f && controller.BackgroundLuminance <= 1f,
                "Environment luminance is outside 0..1");
            Require(controller.BackgroundContrast >= 1f,
                "White-text contrast ratio is outside the valid range");
            Require(controller.EffectiveTransparency >= .039f && controller.EffectiveTransparency <= .551f,
                "Automatic transparency is outside the intended range");
            float darkTarget = AeroGlassOpacityController.TargetTransparencyForLuminance(.02f);
            float midTarget = AeroGlassOpacityController.TargetTransparencyForLuminance(.18f);
            float whiteTarget = AeroGlassOpacityController.TargetTransparencyForLuminance(.98f);
            Require(darkTarget > midTarget + .15f && midTarget > whiteTarget + .05f,
                "Contrast policy did not make dark backgrounds clearer than light backgrounds");
            Require(darkTarget >= .54f && whiteTarget <= .041f,
                "Contrast policy did not reach the opaque/clear endpoints");
            Debug.Log("[AeroPulse GlassOpacity026] luminance=" + controller.BackgroundLuminance.ToString("F3") +
                " contrast=" + controller.BackgroundContrast.ToString("F3") +
                " transparency=" + controller.EffectiveTransparency.ToString("F3"));
            Debug.Log("[AeroPulse GlassOpacity026] PASS manual mode, white-text contrast policy, dark/light response and environment sampling.");
        }
        finally
        {
            if (originalMode == AeroGlassOpacityMode.Manual) controller.SetManualTransparency(originalManual);
            else controller.SetMode(AeroGlassOpacityMode.Auto);
        }
    }
}
