using System;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Hands;

// Deterministic production-controller/transaction checks. This is not Quest hand
// occlusion, actual proximity accuracy, latency or comfort acceptance.
public static class AeroNear021Checks
{
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Near021: " + message); }
    static bool Close(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
    static AeroDragHandle Handle(Transform root, int kind = 0)
    {
        foreach (var h in root.GetComponentsInChildren<AeroDragHandle>(true))
            if (string.IsNullOrEmpty(h.CatalogId) && (kind == 2 ? h.Resize : kind == 1 ? h.Depth : !h.Resize && !h.Depth)) return h;
        throw new InvalidOperationException("Missing handle");
    }
    static Vector3 Point(AeroDragHandle h, float depth = .008f)
        => h.transform.position - h.transform.forward * depth;

    public static void Run()
    {
        Require(Application.isPlaying, "Enter Play Mode first");
        var hud = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
        var eye = Camera.main;
        Require(hud != null && eye != null, "Missing scene");
        var near = hud.GetComponent<AeroNearManipulation>(); var layout = hud.Layout;
        Require(near != null, "Near controller is not initialized");
        var driver = eye.GetComponent<TrackedPoseDriver>(); bool driverEnabled = driver != null && driver.enabled;
        bool direct = hud.DirectInputForChecks, handEnabled = hud.HandEntry.enabled;
        float checkDelta = layout.DeltaForChecks;
        var localEye = new Pose(eye.transform.localPosition, eye.transform.localRotation);
        var parentPose = new Pose(eye.transform.parent.position, eye.transform.parent.rotation);
        var main = hud.Follow.GetComponent<AeroHudView>();
        if (MetaAimHand.left == null) MetaAimHand.left = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Left);
        if (MetaAimHand.right == null) MetaAimHand.right = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
        void NativeOff()
        {
            AeroHandRayChecks.Set(MetaAimHand.left, false, false, Vector3.zero, Quaternion.identity);
            AeroHandRayChecks.Set(MetaAimHand.right, false, false, Vector3.zero, Quaternion.identity);
        }
        void Sample(bool leftValid, Vector3 lp, float lg, bool rightValid, Vector3 rp, float rg)
        {
            near.Sample(leftValid, lp + Vector3.up * lg * .5f, lp - Vector3.up * lg * .5f,
                rightValid, rp + Vector3.up * rg * .5f, rp - Vector3.up * rg * .5f, .02f);
        }
        void Finger(Vector3 p, float gap, bool leftHand = false)
        { if (leftHand) Sample(true, p, gap, false, Vector3.zero, 0); else Sample(false, Vector3.zero, 0, true, p, gap); }
        void Hold(Vector3 p, bool leftHand = false)
        { for (int i = 0; i < 12; i++) Finger(p, .018f, leftHand); }
        void Grab(AeroDragHandle h, bool leftHand = false)
        { near.ResetGates(); var p = Point(h); Finger(p, .055f, leftHand); Finger(p, .018f, leftHand); Require(layout.NearDragging && near.Captured, "Fresh near pinch did not capture " + h.name); }
        void Reset()
        {
            near.SetPaused(false); near.ResetGates();
            if (layout.Editing) layout.EndEdit();
            eye.transform.parent.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            eye.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            hud.ResetViewOptions(); hud.OpenDataFromShortcut(); layout.BeginEdit(false);
            if(hud.Widgets.IsOpen)layout.ToggleTools();
            NativeOff(); hud.DirectTouch.Sample(false, Vector3.zero, false, Vector3.zero, .02f);
            near.RebuildTargets(); layout.PrepareFrame();
        }
        try
        {
            if (driver != null) driver.enabled = false;
            hud.DirectInputForChecks = true; hud.HandEntry.enabled = false; layout.DeltaForChecks = .02f;
            hud.SendMessage("OnApplicationFocus", true);
            Reset(); var move = layout.CardHandle; var point = Point(move);
            Finger(point, .018f); Require(!layout.Dragging, "Initial held pinch captured");
            Finger(point, .055f); Require(near.NearRight && near.HoverRight == move && !layout.Dragging, "Open-hand proximity did not highlight only");
            Finger(point, .018f); Require(layout.NearDragging && layout.DragOwner == "right", "Fresh right pinch did not own capture");
            var start = hud.Follow.transform.position; float radius = Vector3.Distance(start, eye.transform.position);
            Hold(point + Vector3.right * .12f);
            Require(!Close(start, hud.Follow.transform.position), "Near orbit did not move");
            Require(Mathf.Abs(Vector3.Distance(hud.Follow.transform.position, eye.transform.position) - radius) < .001f, "Near orbit changed radius");
            Require(Quaternion.Angle(hud.Follow.transform.rotation, eye.transform.rotation) < .05f, "Near orbit tilted text");
            var preview = hud.Follow.transform.position;
            Finger(point + Vector3.right * .12f, .055f);
            Require(!layout.Dragging && !near.Captured && near.ConsumedThisSample && Close(preview, hud.Follow.transform.position), "Near release failed to commit exactly once");

            // A pinch made away from a handle cannot acquire when carried into it.
            near.ResetGates(); point = Point(move);
            Finger(point + Vector3.up * .2f, .055f); Finger(point, .018f);
            Require(!layout.Dragging, "Closing while newly entering proximity captured without prior hover");
            Finger(point, .055f); Finger(point + Vector3.up * .2f, .018f); Finger(point, .018f);
            Require(!layout.Dragging, "Held approach captured");
            near.ResetGates(); Finger(Point(move, -.03f), .055f); Finger(Point(move, -.03f), .018f);
            Require(!layout.Dragging && !near.NearRight, "Backside handle captured");
            Finger(new Vector3(float.NaN, 0, 0), .055f); Require(!layout.Dragging && !near.AnyNear, "Invalid joints produced a candidate");

            // Tracking loss and focus/context cancellation roll back preview and gate recovery.
            near.ResetGates(); point = Point(move); start = hud.Follow.transform.position;
            Grab(move); Hold(point + Vector3.right * .08f);
            Sample(false, Vector3.zero, 0, false, Vector3.zero, 0);
            Require(!layout.Dragging && Close(start, hud.Follow.transform.position), "Tracking loss committed movement");
            point = Point(move); Finger(point, .018f); Require(!layout.Dragging, "Held tracking recovery re-grabbed");
            Finger(point, .055f); Finger(point, .018f); Require(layout.NearDragging, "Released recovery did not rearm");
            Hold(point + Vector3.left * .06f); hud.SendMessage("OnApplicationFocus", false);
            Require(!layout.Dragging && Close(start, hud.Follow.transform.position), "Focus loss did not roll back");
            hud.SendMessage("OnApplicationFocus", true); Finger(Point(move), .018f);
            Require(!layout.Dragging, "Focus recovery accepted held pinch");

            // Two hands cannot steal capture; a new release is needed after ownership ends.
            Reset(); move = layout.CardHandle; point = Point(move); Grab(move);
            for (int i = 0; i < 12; i++) Sample(true, point, i == 0 ? .055f : .018f, true, point + Vector3.right * .06f, .018f);
            Require(layout.DragOwner == "right", "Left hand stole right capture");
            Sample(true, point, .018f, true, point + Vector3.right * .06f, .055f);
            Sample(true, Point(move), .018f, false, Vector3.zero, 0);
            Require(!layout.Dragging, "Other held hand captured on owner release");
            Grab(move, true); Require(layout.DragOwner == "left" && near.NearLeft, "Left hand cannot own near capture");
            Hold(Point(move) + Vector3.left * .05f, true); Finger(Point(move), .055f, true);
            Require(!layout.Dragging, "Left release failed");

            // Owner routing suppresses a simultaneously held native ray and prevents re-click.
            Reset(); move = layout.CardHandle; point = Point(move);
            var origin = move.transform.position - move.transform.forward * .15f;
            AeroHandRayChecks.Frame(MetaAimHand.right, origin, move.transform.rotation, false, eye.transform.parent);
            Finger(point, .055f); hud.ProcessInput();
            AeroHandRayChecks.Frame(MetaAimHand.right, origin, move.transform.rotation, true, eye.transform.parent);
            Finger(point, .018f); hud.ProcessInput();
            Require(layout.NearDragging, "Native ray displaced near capture");
            Hold(point + Vector3.right * .05f); Finger(point + Vector3.right * .05f, .055f); hud.ProcessInput();
            Require(!layout.Dragging, "Release also began a far drag");
            Finger(point + Vector3.up * .4f, .018f); hud.ProcessInput();
            Require(!layout.Dragging, "Leaving near mode while native pinch held began a far drag");
            NativeOff(); near.ResetGates();

            // Hand mode uses its captured plane and normal, including a tilted hand.
            Reset(); var palm = eye.transform.position + new Vector3(-.15f, -.25f, .45f);
            hud.HandEntry.Sample(true, palm, Quaternion.Euler(-65, 25, 32), false, Vector3.zero, .02f);
            Require(hud.HandEntry.AnchorTracked, "Hand fixture invalid");
            layout.SetPlacement(AeroWindowAnchor.Hand); layout.PrepareFrame();
            move = layout.CardHandle; point = Point(move); start = hud.Follow.transform.position;
            var normal = hud.HandEntry.ContactSurface.rotation * Vector3.forward;
            var tangent = hud.HandEntry.ContactSurface.rotation * new Vector3(.05f, .03f, 0);
            Grab(move); Hold(point + tangent + normal * .06f);
            Require(Close(hud.Follow.transform.position, start + tangent), "Hand near move left tangent plane");
            Finger(point + tangent, .055f); layout.PrepareFrame();
            Require(Close(hud.Follow.transform.position, start + tangent), "Hand release changed position");
            var depth = layout.DepthHandle; point = Point(depth); start = hud.Follow.transform.position;
            Grab(depth); Hold(point - normal * .04f + tangent);
            Require(Close(hud.Follow.transform.position, start - normal * .04f), "Hand depth did not isolate normal");
            Finger(point - normal * .04f, .055f); layout.PrepareFrame();
            Grab(move); Hold(Point(move) + tangent);
            hud.HandEntry.Sample(false, palm, Quaternion.identity, false, Vector3.zero, .02f); layout.PrepareFrame();
            Require(!layout.Dragging, "Dock hand tracking loss left capture");

            // Corner resize shares the fixed upper-left pivot and user-scale limits.
            Reset(); var resize = layout.ResizeHandle; point = Point(resize, 0);
            var rect = (RectTransform)hud.Follow.transform;
            var pivot = rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMax, 0));
            var beforeScale = hud.Follow.UserScale;
            Grab(resize); var larger = pivot + (point - pivot) * 1.2f; Hold(larger);
            Require(hud.Follow.UserScale > beforeScale + .1f, "Near corner resize did not grow");
            Require(Close(pivot, rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMax, 0))), "Near corner resize moved pivot");
            near.ResetGates(); Require(Mathf.Abs(hud.Follow.UserScale - beforeScale) < .0001f, "Resize context cancellation committed scale");
            layout.EndEdit(); Finger(Point(resize), .055f); Finger(Point(resize), .018f);
            Require(!layout.Dragging && !near.AnyNear, "Hidden editing handles accepted near capture");
            Debug.Log("[AeroPulse Near021] PASS fresh proximity pinch, left/right ownership, orbit, release, backdrop/invalid/held approach rejection, loss/focus rollback and rearm, native-ray exclusion, tilted hand tangent/normal, hand tracking loss, resize pivot/cancel, hidden handles. Quest thresholds and physical occlusion remain unverified.");
        }
        finally
        {
            near.SetPaused(false); near.ResetGates(); NativeOff();
            if (layout.Editing) layout.EndEdit();
            hud.ResetViewOptions(); main.ShowMap(false); main.CloseButton.onClick.Invoke();
            hud.DirectInputForChecks = direct; hud.HandEntry.enabled = handEnabled; layout.DeltaForChecks = checkDelta;
            eye.transform.parent.SetPositionAndRotation(parentPose.position, parentPose.rotation);
            eye.transform.SetLocalPositionAndRotation(localEye.position, localEye.rotation);
            if (driver != null) driver.enabled = driverEnabled;
            Physics.SyncTransforms();
        }
    }
}
