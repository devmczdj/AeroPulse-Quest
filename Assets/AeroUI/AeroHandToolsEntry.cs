using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;

// Right hand opens editing tools; the existing left-hand entry still owns HUD visibility.
[DefaultExecutionOrder(-15)]
public sealed class AeroHandToolsEntry : MonoBehaviour
{
    public Transform Root { get; private set; }
    public UnityEngine.UI.Button Button { get; private set; }
    public bool AnchorTracked { get; private set; }
    public bool Visible => Root != null && Root.gameObject.activeSelf;
    public Pose Surface { get; private set; }
    public bool ActivatedThisFrame => activatedFrame == Time.frameCount;

    Camera eye;
    PinchHudExperiment owner;
    XRHandSubsystem subsystem;
    readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
    HandBackTouchGate gate;
    UnityEngine.UI.Image fill;
    bool paused, touchHover;
    UnityEngine.UI.Button hoverLeft;
    int activatedFrame = -1;

    bool Blocked => paused || owner == null || !owner.isActiveAndEnabled || owner.Layout == null ||
        owner.Layout.Dragging || AeroValueSlider.Active != null ||
        owner.Profiles != null && (owner.Profiles.ModalOpen || owner.Profiles.KeyboardInputActive);

    public void Initialize(Camera camera, PinchHudExperiment hud)
    {
        eye = camera; owner = hud;
        var rect = AeroHudView.NewRect("RightHandToolsEntry", null, Vector2.zero, new Vector2(76, 40));
        Root = rect; rect.localScale = Vector3.one * .001f;
        var canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = eye;canvas.sortingOrder=110;
        rect.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        Button = AeroHudView.ActionButton(rect, "ToggleHudTools", "TOOLS", Vector2.zero,
            new Vector2(76, 40), new Vector3(76, 40, 8));
        AeroUiIcons.Apply(Button, "settings", 21);
        ((RectTransform)Button.transform.Find("ActionIcon")).anchoredPosition = new Vector2(-22, 0);
        AeroHudView.Text(Button.transform, "ToolsCaption", "工具", new Vector2(12, 0), new Vector2(42, 34),
            18, AeroUiFonts.Interface, AeroUiTokens.Ink, TextAlignmentOptions.Center, 0, 0, true);
        fill = Button.GetComponent<UnityEngine.UI.Image>();
        Button.onClick.AddListener(Activate);
        Root.gameObject.SetActive(false);
    }

    static bool Allowed(MetaAimHand hand) => hand == null || !hand.added ||
        (((MetaAimFlags)hand.aimFlags.ReadValue() & (MetaAimFlags.SystemGesture | MetaAimFlags.MenuPressed)) == 0);

    void Update()
    {
        if (subsystem == null || !subsystem.running)
        {
            SubsystemManager.GetSubsystems(subsystems);
            subsystem = subsystems.Find(s => s.running);
        }
        Pose palm = default, tip = default;
        bool right = subsystem != null && subsystem.running && subsystem.rightHand.isTracked &&
            Allowed(MetaAimHand.right) && subsystem.rightHand.GetJoint(XRHandJointID.Palm).TryGetPose(out palm);
        bool left = subsystem != null && subsystem.running && subsystem.leftHand.isTracked &&
            Allowed(MetaAimHand.left) && subsystem.leftHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out tip);
        var rotation = right ? subsystem.rightHand.rootPose.rotation : Quaternion.identity;
        SampleTracking(right, palm.position, rotation, left, tip.position, eye != null ? eye.transform.parent : null, Time.unscaledDeltaTime);
    }

    public void SampleTracking(bool rightValid, Vector3 palm, Quaternion rotation, bool leftValid,
        Vector3 tip, Transform space, float dt)
    {
        if (space != null)
        {
            palm = space.TransformPoint(palm); rotation = space.rotation * rotation;
            tip = space.TransformPoint(tip);
        }
        Sample(rightValid, palm, rotation, leftValid, tip, dt);
    }

    public void Sample(bool rightValid, Vector3 palm, Quaternion rotation, bool leftValid, Vector3 tip, float dt)
    {
        if (Root == null || eye == null || owner == null) return;
        var normal = rotation * Vector3.up;
        var contact = palm + normal * .014f;
        float distance = Vector3.Distance(eye.transform.position, contact);
        AnchorTracked = !paused && owner.isActiveAndEnabled && rightValid && distance >= .20f && distance <= 1f &&
            Vector3.Dot(normal, (eye.transform.position - contact).normalized) >= .2f;
        float height = owner.HandEntry != null ? owner.HandEntry.HeightOffset : .035f;
        Surface = new Pose(palm + normal * height, Quaternion.LookRotation(-normal, rotation * Vector3.forward));
        bool show = AnchorTracked && !Blocked;
        if (Root.gameObject.activeSelf != show) Root.gameObject.SetActive(show);
        Button.interactable = show;
        if (show) Root.SetPositionAndRotation(Surface.position, Surface.rotation);
        var local = Quaternion.Inverse(Surface.rotation) * (tip - Surface.position);
        bool inside = Mathf.Abs(local.x) <= .038f && Mathf.Abs(local.y) <= .020f;
        float depth = Vector3.Dot(tip - Surface.position, normal);
        touchHover = show && leftValid && inside && depth >= -.006f && depth <= .035f;
        if (gate.Step(show && leftValid, inside, depth, dt)) Activate();
        Paint();
    }

    void Activate()
    {
        if (!Visible || !Button.IsInteractable() || Blocked) return;
        RequireRelease();
        activatedFrame = Time.frameCount;
        owner.Layout.ToggleTools();
        owner.ClearPresses();
        Paint();
    }

    public void RequireRelease() { gate = default; touchHover = false; }
    public void SetPaused(bool value)
    {
        paused = value; RequireRelease(); hoverLeft = null;
        if (value)
        {
            AnchorTracked = false;
            if (Root != null) Root.gameObject.SetActive(false);
            if (Button != null) Button.interactable = false;
        }
    }
    public void SetHover(UnityEngine.UI.Button left, UnityEngine.UI.Button right)
    { hoverLeft = left; Paint(); }
    void Paint()
    {
        if (fill == null) return;
        fill.color = touchHover || hoverLeft == Button ? AeroUiTokens.ButtonHover :
            owner != null && owner.Widgets != null && owner.Widgets.IsOpen ? new Color(.2f, .31f, .28f, 1) : AeroUiTokens.ButtonFill;
    }
    void OnEnable() => SetPaused(!Application.isFocused);
    void OnApplicationFocus(bool focused) => SetPaused(!focused);
    void OnDisable() => SetPaused(true);
    void OnDestroy()
    {
        if (Root != null)
        {
            if (Application.isPlaying) Destroy(Root.gameObject); else DestroyImmediate(Root.gameObject);
        }
    }
}
