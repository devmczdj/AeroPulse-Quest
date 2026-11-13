using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;

// Joint poses are in tracking-origin space. Unity XR Hands defines palm as root -Y.
public class HandBackHudEntry : MonoBehaviour
{
    public bool EnabledForEntry = true;
    public bool Visible => tile != null && tile.activeSelf;
    public Pose Surface { get; private set; }
    public Pose ContactSurface { get; private set; }
    public float HeightOffset { get; private set; } = 0.035f;
    TextMeshProUGUI caption;
    public void RequireRelease() => gate = default;
    public void CycleHeight() { HeightOffset = HeightOffset < 0.03f ? 0.035f : HeightOffset < 0.04f ? 0.045f : 0.025f; RequireRelease(); }
    public void ResetHeight() { HeightOffset = 0.035f; RequireRelease(); }
    Camera eye;
    PinchHudExperiment hud;
    XRHandSubsystem subsystem;
    readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
    GameObject tile;
    UnityEngine.UI.Image fill;
    HandBackTouchGate gate;
    bool focused = true;

    public void Initialize(Camera camera, PinchHudExperiment owner)
    {
        eye = camera; hud = owner;
        tile = new GameObject("HandBackHUD", typeof(RectTransform), typeof(Canvas));
        var canvas = tile.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = eye;
        var rect = (RectTransform)tile.transform;
        rect.sizeDelta = new Vector2(58, 38); rect.localScale = Vector3.one * 0.001f;
        fill = AeroHudView.AddPanelImage(tile.transform, "TouchPad", AeroUiSprites.Pill,
            Vector2.zero, new Vector2(58, 38), AeroUiTokens.ButtonFill);
        caption = AeroHudView.Text(tile.transform, "HandHUDLabel", "HUD", Vector2.zero, new Vector2(54, 34),
            18, AeroUiFonts.SemiBold, AeroUiTokens.Ink, TextAlignmentOptions.Center, 0, 0, true);
        tile.SetActive(false);
    }

    void Update()
    {
        if (subsystem == null || !subsystem.running)
        {
            SubsystemManager.GetSubsystems(subsystems);
            subsystem = subsystems.Find(s => s.running);
        }
        Pose palm = default, tip = default;
        Quaternion root = Quaternion.identity;
        bool left = subsystem != null && subsystem.running && subsystem.leftHand.isTracked &&
                    subsystem.leftHand.GetJoint(XRHandJointID.Palm).TryGetPose(out palm);
        bool right = subsystem != null && subsystem.running && subsystem.rightHand.isTracked &&
                     subsystem.rightHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out tip);
        if (left) root = subsystem.leftHand.rootPose.rotation;
        SampleTracking(left, palm.position, root, right, tip.position,
                       eye != null ? eye.transform.parent : null, Time.unscaledDeltaTime);
    }

    public void SampleTracking(bool left, Vector3 palm, Quaternion root, bool right, Vector3 tip,
                               Transform space, float dt)
    {
        if (space != null)
        {
            palm = space.TransformPoint(palm);
            root = space.rotation * root;
            tip = space.TransformPoint(tip);
        }
        Sample(left, palm, root, right, tip, dt);
    }

    // Shared by the real joint reader and deterministic Editor samples.
    public void Sample(bool leftValid, Vector3 palm, Quaternion rootRotation,
                       bool rightValid, Vector3 tip, float dt)
    {
        if (tile == null || eye == null || hud == null) return;
        Vector3 normal = rootRotation * Vector3.up;
        // Raising the visual must not move the physical hand-contact proxy.
        var center = palm + normal * 0.014f;
        ContactSurface = new Pose(center, Quaternion.LookRotation(-normal, rootRotation * Vector3.forward));
        Surface = new Pose(palm + normal * HeightOffset, ContactSurface.rotation);
        float distance = Vector3.Distance(eye.transform.position, center);
        bool show = EnabledForEntry && focused && hud.isActiveAndEnabled && leftValid &&
                    distance >= 0.20f && distance <= 1.0f &&
                    Vector3.Dot(normal, (eye.transform.position - center).normalized) >= 0.2f;
        tile.SetActive(show);
        if (show) tile.transform.SetPositionAndRotation(Surface.position, Surface.rotation);
        Vector3 local = Quaternion.Inverse(ContactSurface.rotation) * (tip - center);
        float height = Vector3.Dot(tip - center, normal);
        bool inside = Mathf.Abs(local.x) <= 0.029f && Mathf.Abs(local.y) <= 0.019f;
        bool touch = gate.Step(show && rightValid, inside, height, dt);
        fill.color = show && rightValid && inside && height >= -0.006f && height <= 0.035f
            ? AeroUiTokens.ButtonHover : AeroUiTokens.ButtonFill;
        if (touch)
        {
            Debug.Log("[AeroPulse HandBack] TOUCH TOGGLE");
            hud.ToggleFromHand();
        }
        caption.text = hud.IsCardOpen ? "HIDE" : "HUD";
    }

    void OnApplicationFocus(bool value) { focused = value; gate = default; if (!value && tile != null) tile.SetActive(false); }
    void OnDisable() { gate = default; if (tile != null) tile.SetActive(false); }
    void OnDestroy() { if (tile != null) Destroy(tile); }
}

public struct HandBackTouchGate
{
    bool armed;
    float contact;
    public bool Step(bool valid, bool inside, float height, float dt)
    {
        if (!valid) { armed = false; contact = 0; return false; }
        if (height > 0.028f) { armed = true; contact = 0; return false; }
        if (!armed || !inside || height < -0.006f || height > 0.014f) { contact = 0; return false; }
        contact += Mathf.Clamp(dt, 0, 0.04f);
        if (contact < 0.06f) return false;
        armed = false; contact = 0; return true;
    }
}
