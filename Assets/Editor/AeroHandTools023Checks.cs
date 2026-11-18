using System;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Hands;

// Joint/ray injection checks the shared production path, not Quest fit or real tracking.
public static class AeroHandTools023Checks
{
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("HandTools023: " + message); }

    public static void Run()
    {
        Require(Application.isPlaying, "Enter Play Mode first");
        var h = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
        var tools = h.GetComponent<AeroHandToolsEntry>(); var camera = Camera.main;
        Require(tools != null && !h.Profiles.ModalOpen, "Missing launcher or close naming first");
        var layout = h.Layout; var widgets = h.Widgets; var before = layout.CaptureLayout();
        bool editing = layout.Editing, open = widgets.IsOpen, direct = h.DirectInputForChecks;
        bool reader = tools.enabled, leftReader = h.HandEntry.enabled, dirty = h.Profiles.IsDirty;
        var eye = new Pose(camera.transform.position, camera.transform.rotation);
        var driver = camera.GetComponent<TrackedPoseDriver>(); bool driven = driver != null && driver.enabled;
        if (MetaAimHand.left == null) MetaAimHand.left = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Left);
        if (MetaAimHand.right == null) MetaAimHand.right = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
        void NativeOff()
        {
            AeroHandRayChecks.Set(MetaAimHand.left, false, false, Vector3.zero, Quaternion.identity);
            AeroHandRayChecks.Set(MetaAimHand.right, false, false, Vector3.zero, Quaternion.identity);
        }
        var palm = new Vector3(.15f, 1.3f, .45f); var rotation = Quaternion.Euler(-90, 0, 0);
        var normal = rotation * Vector3.up;
        void Sample(Vector3 tip, bool right = true, bool left = true)
            => tools.Sample(right, palm, rotation, left, tip, .02f);
        try
        {
            if (driver != null) driver.enabled = false;
            tools.enabled = false; h.HandEntry.enabled = false; h.DirectInputForChecks = true;
            h.SendMessage("OnApplicationFocus", true); tools.SetPaused(false);
            camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            if (layout.Dragging) layout.CancelDrag("Check setup"); if (layout.Editing) layout.EndEdit();
            widgets.SetOpen(false); NativeOff(); h.ClearPresses();
            Sample(Vector3.zero, true, false);
            Require(!tools.Visible&&!tools.Button.IsInteractable(),"Normal mode exposes wrist tools");
            tools.Button.onClick.Invoke();layout.ToggleTools();Require(!layout.Editing&&!widgets.IsOpen,"Normal wrist action entered editing");
            layout.BeginEdit(false);if(widgets.IsOpen)layout.ToggleTools();Sample(Vector3.zero,true,false);
            Require(tools.Visible && tools.Button.GetComponent<Collider>().enabled, "Editor right-hand entry is not visible/hittable");
            bool mainOpen = h.IsCardOpen;
            var point = tools.Button.transform.position; var aim = Quaternion.LookRotation(tools.Button.transform.forward);
            // Test far rays before touch activation marks this frame as consumed.
            foreach (bool pressed in new[] { false, true })
            {
                AeroHandRayChecks.Frame(MetaAimHand.right, point - tools.Button.transform.forward * .15f, aim, pressed, camera.transform.parent);
                h.ProcessInput();
            }
            Require(!widgets.IsOpen, "Right hand selected its own wrist entry"); NativeOff(); h.ClearPresses();
            foreach (bool pressed in new[] { false, true })
            {
                AeroHandRayChecks.Frame(MetaAimHand.left, point - tools.Button.transform.forward * .15f, aim, pressed, camera.transform.parent);
                h.ProcessInput();
            }
            Require(widgets.IsOpen && h.IsCardOpen == mainOpen, "Left far ray did not open tools independently from main HUD");
            NativeOff(); h.ClearPresses(); tools.Button.onClick.Invoke();
            Require(!widgets.IsOpen, "Shared button callback did not toggle tools closed");

            var center = tools.Surface.position; var far = center + normal * .04f; var near = center + normal * .002f;
            Sample(near); Sample(near); Require(!widgets.IsOpen, "Initial overlapping finger opened tools");
            Sample(far); Sample(near); Require(widgets.IsOpen && tools.Visible, "Fresh left-index touch did not open tools");
            Sample(near); Sample(near); Require(widgets.IsOpen, "Held finger repeated toggle");
            Sample(far); Sample(near); Require(!widgets.IsOpen, "Second released touch did not close tools");
            Sample(far); Sample(near, false); Require(!tools.Visible, "Lost right anchor remained visible");
            Sample(near); Require(!widgets.IsOpen, "Right tracking recovery auto-opened tools");
            Sample(far); Sample(near, true, false); Sample(near);
            Require(!widgets.IsOpen, "Left fingertip tracking recovery auto-opened tools");
            Sample(far); Sample(near + Vector3.right * .1f); Sample(near);
            Require(!widgets.IsOpen, "Side re-entry reused touch arm");
            Sample(far); Sample(center - normal * .04f); Sample(near);
            Require(!widgets.IsOpen, "Backside re-entry reused touch arm");
            tools.SetPaused(true); Sample(far); tools.Button.onClick.Invoke();
            Require(!tools.Visible && !widgets.IsOpen, "Paused launcher remained interactive");
            tools.SetPaused(false); Sample(near); Require(!widgets.IsOpen, "Focus recovery reused contact");
            tools.Sample(true, palm, Quaternion.Euler(90, 0, 0), true, near, .02f);
            Require(!tools.Visible, "Palm-facing entry remained visible");

            var space = new GameObject("HandToolsTrackingSpaceCheck").transform;
            try
            {
                space.SetPositionAndRotation(new Vector3(1, .2f, -.5f), Quaternion.Euler(0, 35, 0));
                tools.SampleTracking(true, space.InverseTransformPoint(palm), Quaternion.Inverse(space.rotation) * rotation,
                    false, Vector3.zero, space, .02f);
                Require(tools.Visible && Vector3.Distance(tools.Surface.position, center) < .001f,
                    "Tracking origin conversion changed right-hand pose");
            }
            finally { UnityEngine.Object.DestroyImmediate(space.gameObject); }
            Debug.Log("[AeroPulse HandTools023] PASS left far ray, own-hand rejection, left touch, hold/release, tracking/focus recovery, side/back bounds and tracking-space conversion. Quest hand fit remains pending.");
        }
        finally
        {
            NativeOff(); h.ClearPresses(); if (layout.Dragging) layout.CancelDrag("Check cleanup");
            if (layout.Editing) layout.EndEdit(); camera.transform.SetPositionAndRotation(eye.position, eye.rotation);
            layout.ApplyLayout(before); if (editing) layout.BeginEdit(false); widgets.SetOpen(open);
            tools.SetPaused(false); tools.Sample(false, Vector3.zero, Quaternion.identity, false, Vector3.zero, .02f);
            tools.enabled = reader; h.HandEntry.enabled = leftReader; h.DirectInputForChecks = direct;
            if (driver != null) driver.enabled = driven;
            typeof(AeroLayoutProfileView).GetProperty("IsDirty").SetValue(h.Profiles, dirty); h.Profiles.Refresh();
        }
    }
}
