using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.XR;

// Exercises production reference frames and transactions; never writes a profile file.
// Scene injection validates mapping, not real startup tracking or Quest comfort.
public static class AeroLayoutPersistence021Checks
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("LayoutPersistence021: " + message); }
    static bool Close(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
    static AeroPanelState Panel(AeroLayoutState state, string id)
    {
        foreach (var p in state.panels) if (p.id == id) return p;
        throw new InvalidOperationException("Missing saved panel " + id);
    }
    static AeroDragHandle Move(Transform root)
    {
        foreach (var h in root.GetComponentsInChildren<AeroDragHandle>(true))
            if (string.IsNullOrEmpty(h.CatalogId) && !h.Resize && !h.Depth) return h;
        throw new InvalidOperationException("Missing move handle " + root.name);
    }
    static TextMeshProUGUI Value(Transform root) => root.Find("Value").GetComponent<TextMeshProUGUI>();
    static void SameState(AeroLayoutState expected, AeroLayoutState actual, string context)
    {
        Require(expected.panels.Length == actual.panels.Length, context + ": panel count changed");
        foreach (var a in expected.panels)
        {
            var b = Panel(actual, a.id);
            Require(a.visible == b.visible && a.placed == b.placed && a.anchor == b.anchor &&
                a.pinned == b.pinned && a.followMode == b.followMode && a.speed == b.speed,
                context + ": visibility/anchor/follow flags differ for " + a.id);
            Require(Close(a.position, b.position), context + ": reference-frame offset differs for " + a.id);
            Require(Mathf.Abs(a.physicalScale - b.physicalScale) < .000001f && Mathf.Abs(a.userScale - b.userScale) < .00001f,
                context + ": physical scale or user multiplier differs for " + a.id);
        }
    }

    public static void Run()
    {
        Require(Application.isPlaying, "Enter Play Mode first");
        var hud = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>(); var eye = Camera.main;
        Require(hud != null && eye != null && hud.Near != null, "Missing initialized HUD");
        var layout = hud.Layout; var widgets = hud.Widgets; var profiles = hud.Profiles;
        Require(!layout.Dragging && (profiles == null || !profiles.ModalOpen), "Close modal/finish active manipulation before checking");
        var original = layout.CaptureLayout();
        var roots = new Dictionary<string, Transform>(); var worldPoses = new Dictionary<string, Pose>();
        foreach (var p in original.panels)
        {
            var root = layout.WidgetRoot(p.id); roots.Add(p.id, root);
            worldPoses.Add(p.id, new Pose(root.position, root.rotation));
        }
        bool wasEditing = layout.Editing, wasOpen = widgets.IsOpen, wasProfiles = widgets.ShowingProfiles;
        string selectedId = layout.SelectedId;
        var eyeLocal = new Pose(eye.transform.localPosition, eye.transform.localRotation);
        var trackingPose = new Pose(eye.transform.parent.position, eye.transform.parent.rotation);
        var driver = eye.GetComponent<TrackedPoseDriver>(); bool driverEnabled = driver != null && driver.enabled;
        bool direct = hud.DirectInputForChecks, handEnabled = hud.HandEntry.enabled;
        float checkDelta = layout.DeltaForChecks;
        var main = hud.Follow.GetComponent<AeroHudView>(); int routeIndex = main.Route.CurrentIndex;
        var dirtyProperty = profiles != null ? profiles.GetType().GetProperty("IsDirty") : null;
        bool wasDirty = profiles != null && profiles.IsDirty;
        string storeBefore = profiles != null ? JsonUtility.ToJson(profiles.Store.Data) : "";
        var handTracked = typeof(HandBackHudEntry).GetField("<AnchorTracked>k__BackingField", PrivateInstance);
        var handContact = typeof(HandBackHudEntry).GetField("<ContactSurface>k__BackingField", PrivateInstance);
        var handSurface = typeof(HandBackHudEntry).GetField("<Surface>k__BackingField", PrivateInstance);
        object oldTracked = handTracked?.GetValue(hud.HandEntry), oldContact = handContact?.GetValue(hud.HandEntry), oldSurface = handSurface?.GetValue(hud.HandEntry);
        var sessionField = typeof(AeroWidgetCatalog).GetField("sessionStart", PrivateInstance);
        Require(sessionField != null, "Session clock origin not found");
        float sessionStart = (float)sessionField.GetValue(widgets);
        void MoveBy(string id, Vector3 offset)
        {
            layout.SelectWidget(id); var handle = Move(roots[id]); var p = handle.transform.position;
            Require(layout.StartNearDrag("right", handle, p), "Cannot begin setup manipulation for " + id);
            for (int i = 0; i < 10; i++) layout.StepNearDrag(true, true, p + offset, .02f);
            layout.StepNearDrag(true, false, p + offset, .02f);
        }
        void Choose(string id, AeroWindowAnchor anchor)
        { layout.SelectWidget(id); layout.SetPlacement(anchor); layout.PrepareFrame(); }
        try
        {
            if (driver != null) driver.enabled = false;
            hud.DirectInputForChecks = true; hud.HandEntry.enabled = false; layout.DeltaForChecks = .02f;
            hud.SendMessage("OnApplicationFocus", true);
            if (layout.Editing) layout.EndEdit();
            eye.transform.parent.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            eye.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            hud.ResetViewOptions(); hud.OpenDataFromShortcut(); layout.BeginEdit(false); widgets.SetPage(false);
            foreach (var item in widgets.Items) layout.AddWidget(item.Id);
            var palm = eye.transform.position + new Vector3(-.15f, -.25f, .45f);
            var handRotation = Quaternion.Euler(-65, 25, 32);
            hud.HandEntry.Sample(true, palm, handRotation, false, Vector3.zero, .02f);
            Require(hud.HandEntry.AnchorTracked, "Setup hand is not tracked");
            Choose("main", AeroWindowAnchor.Follow); layout.TogglePin(); MoveBy("main", new Vector3(-.07f, .03f, 0));
            Choose("heart", AeroWindowAnchor.Follow); layout.ToggleSpeed(); MoveBy("heart", new Vector3(.08f, .03f, 0));
            Choose("pace", AeroWindowAnchor.View); MoveBy("pace", new Vector3(-.06f, -.04f, 0));
            Choose("distance", AeroWindowAnchor.Hand);
            MoveBy("distance", hud.HandEntry.ContactSurface.rotation * new Vector3(.05f, -.03f, 0));
            Choose("next", AeroWindowAnchor.Follow); layout.TogglePin();
            Choose("timer", AeroWindowAnchor.Follow); layout.CloseWidget("timer");
            // A catalog ray parallel to the existing hand panel cannot begin a valid
            // manipulation. Startup rollback must reopen the catalog and keep the old widget.
            UnityEngine.UI.Button handEntry = null;
            foreach (var item in widgets.Items) if (item.Id == "distance") handEntry = item.EntryButton;
            Require(handEntry != null, "Missing hand widget catalog entry");
            var entryHandle = handEntry.GetComponent<AeroDragHandle>();
            var handTangent = hud.HandEntry.ContactSurface.rotation * Vector3.right;
            var parallelRay = new Ray(handEntry.transform.position - handTangent * .15f, handTangent);
            var beforeInvalidStart = layout.CaptureLayout();
            layout.StartDrag("right", entryHandle, parallelRay, .15f);
            Require(!layout.Dragging && widgets.IsOpen && roots["distance"].gameObject.activeSelf,
                "Invalid catalog drag left hidden catalog, active capture, or lost widget");
            SameState(beforeInvalidStart, layout.CaptureLayout(), "invalid catalog start rollback");
            var handStart = roots["distance"].position;
            MoveBy("distance", handTangent * .025f);
            Require(Close(roots["distance"].position, handStart + handTangent * .025f),
                "Failed catalog start poisoned the next near transaction");
            layout.Undo();
            SameState(beforeInvalidStart, layout.CaptureLayout(), "subsequent drag and undo after invalid start");

            // A direct API attempt must not convert an existing far capture into near.
            var handMove = Move(roots["distance"]);
            var farRay = new Ray(handMove.transform.position - handMove.transform.forward * .15f, handMove.transform.forward);
            layout.StartDrag("right", handMove, farRay, .15f);
            Require(layout.Dragging && !layout.NearDragging, "Far capture setup failed");
            Require(!layout.StartNearDrag("right", handMove, handMove.transform.position) &&
                layout.Dragging && !layout.NearDragging && layout.DragOwner == "right",
                "Same-hand near API replaced an existing far transaction");
            layout.CancelDrag("Persistence check far ownership cleanup");
            Require(widgets.IsOpen, "Cancel after rejected near takeover left catalog hidden");
            SameState(beforeInvalidStart, layout.CaptureLayout(), "far capture survives rejected near takeover");

            // Different sizes ensure physical scale and user multiplier are both retained.
            foreach (var item in widgets.Items)
                item.Root.GetComponent<HudSoftFollow>().SetUserScale(item.Id == "distance" ? 1.35f : item.Id == "pace" ? .85f : 1.1f);
            hud.Follow.SetUserScale(1.2f);
            var saved = layout.CaptureLayout();
            Require(AeroLayoutProfileStore.ValidateState(saved, out string error), "Production capture failed validation: " + error);
            Require(saved.panels.Length == 6 && Panel(saved, "main").anchor == (int)AeroWindowAnchor.World &&
                Panel(saved, "heart").anchor == (int)AeroWindowAnchor.Follow && Panel(saved, "pace").anchor == (int)AeroWindowAnchor.View &&
                Panel(saved, "distance").anchor == (int)AeroWindowAnchor.Hand && !Panel(saved, "timer").visible,
                "Fixture did not produce all four reference frames plus hidden timer");
            var originalWorldPosition = roots["main"].position;
            var originalFollowPosition = roots["heart"].position;
            var baseline = AeroLayoutProfileStore.Clone(saved);

            // A later activation/new startup uses the current eye, not an old room origin.
            eye.transform.SetPositionAndRotation(new Vector3(2.3f, 1.75f, -1.1f), Quaternion.Euler(0, 67, 0));
            Require(layout.ApplyLayout(saved), "Valid snapshot was rejected"); layout.PrepareFrame();
            SameState(baseline, layout.CaptureLayout(), "six-panel round trip");
            Require(Close(roots["main"].position, eye.transform.TransformPoint(Panel(saved, "main").position)) &&
                Vector3.Distance(roots["main"].position, originalWorldPosition) > 1,
                "World panel reused the previous room coordinate");
            Require(Close(roots["heart"].position, eye.transform.TransformPoint(Panel(saved, "heart").position)) &&
                Vector3.Distance(roots["heart"].position, originalFollowPosition) > 1,
                "Follow offset did not rebase to the current view reference");
            Require(Close(roots["pace"].position, eye.transform.TransformPoint(Panel(saved, "pace").position)), "View offset not restored relative to eye");
            Require(Close(roots["distance"].position, hud.HandEntry.ContactSurface.position +
                hud.HandEntry.ContactSurface.rotation * Panel(saved, "distance").position), "Hand offset not restored relative to hand");
            foreach (var p in saved.panels)
            {
                Require(layout.WidgetRoot(p.id) == roots[p.id], "Applying layout recreated panel " + p.id);
                Require(roots[p.id].gameObject.activeSelf == p.visible, "Visibility mismatch for " + p.id);
            }
            // Snapshots do not alias one another or live layout state.
            Panel(saved, "pace").position += Vector3.one;
            Panel(saved, "timer").visible = true;
            SameState(baseline, layout.CaptureLayout(), "input snapshot isolation");
            var detachedCapture = layout.CaptureLayout(); Panel(detachedCapture, "main").position = Vector3.zero;
            SameState(baseline, layout.CaptureLayout(), "output snapshot isolation");
            saved = AeroLayoutProfileStore.Clone(baseline);

            // Fixed world remains fixed while view-anchored panels keep their eye-local offsets.
            var pinned = roots["main"].position;
            eye.transform.SetPositionAndRotation(eye.transform.position + new Vector3(.15f, .04f, .02f), Quaternion.Euler(-12, 89, 24));
            layout.PrepareFrame();
            Require(Close(roots["main"].position, pinned), "World restore secretly follows head movement");
            Require(Close(roots["pace"].position, eye.transform.TransformPoint(Panel(saved, "pace").position)), "View restore lost local offset after head motion");
            Require(Quaternion.Angle(roots["main"].rotation, eye.transform.rotation) < .05f, "Restored world text tilts relative to eye");
            Require(Mathf.Abs(roots["heart"].GetComponent<HudSoftFollow>().UserScale - Panel(saved, "heart").userScale) < .0001f,
                "Follow size changed while turning");

            // Repeated enables at the same new eye pose must not accumulate another transform.
            Require(layout.ApplyLayout(saved), "Second enable at moved eye failed");
            var onceRebased = new Dictionary<string, Vector3>();
            foreach (var p in saved.panels) onceRebased.Add(p.id, roots[p.id].position);
            Require(layout.ApplyLayout(saved), "Repeated enable failed");
            foreach (var p in saved.panels)
                Require(Close(roots[p.id].position, onceRebased[p.id]), "Repeated enable accumulated a transform for " + p.id);
            Require(Close(roots["main"].position, eye.transform.TransformPoint(Panel(saved, "main").position)),
                "Re-enabling World layout did not use the newly moved view");

            // Missing hand hides a requested-visible widget without losing its saved local pose.
            hud.HandEntry.Sample(false, palm, handRotation, false, Vector3.zero, .02f);
            Require(layout.ApplyLayout(saved), "Snapshot could not restore while hand unavailable"); layout.PrepareFrame();
            var handRoot = roots["distance"]; var handState = Panel(saved, "distance");
            Require(handRoot.gameObject.activeSelf && !handRoot.GetComponent<Canvas>().enabled,
                "Untracked hand widget is visible or desired visibility was forgotten");
            foreach (var collider in handRoot.GetComponentsInChildren<Collider>(true))
                Require(!collider.enabled, "Untracked hand widget still intercepts input");
            Require(Close(Panel(layout.CaptureLayout(), "distance").position, handState.position), "Untracked restore overwrote hand-local offset");
            var recoveredPalm = eye.transform.position + new Vector3(-.18f, -.26f, .45f);
            var recoveredRotation = Quaternion.Euler(-70, 14, 26);
            hud.HandEntry.Sample(true, recoveredPalm, recoveredRotation, false, Vector3.zero, .02f); layout.PrepareFrame();
            Require(hud.HandEntry.AnchorTracked && handRoot.GetComponent<Canvas>().enabled,
                "Hand reacquisition did not reveal saved widget");
            Require(Close(handRoot.position, hud.HandEntry.ContactSurface.position + hud.HandEntry.ContactSurface.rotation * handState.position),
                "Hand reacquisition used old world coordinates");

            // Profiles carry no route progress or clock origin, including hidden timer widgets.
            main.Route.Advance(); int progressedRoute = main.Route.CurrentIndex;
            string next = Value(roots["next"]).text, timer = Value(roots["timer"]).text;
            Require(layout.ApplyLayout(saved), "Layout application failed during live data check");
            Require(main.Route.CurrentIndex == progressedRoute && Value(roots["next"]).text == next,
                "Layout restoration reset live route progress");
            Require((float)sessionField.GetValue(widgets) == sessionStart && Value(roots["timer"]).text == timer,
                "Layout restoration restarted/overwrote the session clock");

            // Switching during a genuine near capture must cancel that input owner first.
            layout.SelectWidget("main"); var handle = Move(roots["main"]);
            var grip = handle.transform.position - handle.transform.forward * .008f;
            void NearFrame(Vector3 p, float gap)
            { hud.Near.Sample(false, Vector3.zero, Vector3.zero, true, p + Vector3.up * gap * .5f, p - Vector3.up * gap * .5f, .02f); }
            hud.Near.ResetGates(); NearFrame(grip, .055f); NearFrame(grip, .018f);
            Require(layout.NearDragging && hud.Near.Captured, "Near transaction setup did not capture");
            for (int i = 0; i < 10; i++) NearFrame(grip + Vector3.right * .09f, .018f);
            Require(layout.ApplyLayout(saved), "Applying profile during capture failed");
            Require(!layout.Dragging && !hud.Near.Captured, "Profile application left a captured input owner");
            NearFrame(Move(roots["main"]).transform.position, .018f);
            Require(!layout.Dragging, "Held hand re-grabbed after profile switch");
            SameState(saved, layout.CaptureLayout(), "cancel and apply");

            var validNow = layout.CaptureLayout(); var invalid = AeroLayoutProfileStore.Clone(validNow);
            Panel(invalid, "pace").position.x = float.NaN;
            Require(!layout.ApplyLayout(invalid), "Invalid snapshot was accepted");
            SameState(validNow, layout.CaptureLayout(), "invalid snapshot atomic rejection");
            Require(profiles == null || JsonUtility.ToJson(profiles.Store.Data) == storeBefore,
                "Mapping tests modified the production profile collection");
            Debug.Log("[AeroPulse LayoutPersistence021] PASS invalid catalog-start rollback/retry, far-owner near-takeover rejection, six-panel flags/scale round trip, World/Follow rebasing, View local pose, Hand deferred rendering/reacquisition, input/output isolation, world pin after head motion, live route/clock continuity, active near cancellation, invalid snapshot rejection. No profile file writes; real XR startup remains device validation.");
        }
        finally
        {
            hud.Near.ResetGates(); if (layout.Dragging) layout.CancelDrag("Persistence check cleanup");
            if (layout.Editing) layout.EndEdit();
            // Restore coordinate sources before applying the caller's saved layout.
            eye.transform.parent.SetPositionAndRotation(trackingPose.position, trackingPose.rotation);
            eye.transform.SetLocalPositionAndRotation(eyeLocal.position, eyeLocal.rotation);
            if (handTracked != null) handTracked.SetValue(hud.HandEntry, oldTracked);
            if (handContact != null) handContact.SetValue(hud.HandEntry, oldContact);
            if (handSurface != null) handSurface.SetValue(hud.HandEntry, oldSurface);
            layout.ApplyLayout(original);
            if (wasEditing) { layout.BeginEdit(false); layout.ApplyLayout(original); layout.SelectWidget(selectedId); }
            foreach (var pair in worldPoses) roots[pair.Key].SetPositionAndRotation(pair.Value.position, pair.Value.rotation);
            widgets.SetPage(wasProfiles); widgets.SetOpen(wasOpen);
            for (int i = 0; i < main.Route.Count && main.Route.CurrentIndex != routeIndex; i++) main.Route.Advance();
            if (profiles != null && dirtyProperty != null) { dirtyProperty.SetValue(profiles, wasDirty); profiles.Refresh(); }
            hud.DirectInputForChecks = direct; hud.HandEntry.enabled = handEnabled; layout.DeltaForChecks = checkDelta;
            if (driver != null) driver.enabled = driverEnabled;
            hud.ClearPresses(); Physics.SyncTransforms();
        }
    }
}
