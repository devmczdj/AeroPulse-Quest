using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

// Runs production native-ray/direct-touch paths and layout transactions in Play Mode.
// Editor injection does not establish Quest latency, comfort or physical hand tracking.
public static class AeroWidgets020Checks
{
    static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Widgets020: " + message); }

    static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;

    static AeroDragHandle Handle(Transform root, bool resize = false)
    {
        foreach (var handle in root.GetComponentsInChildren<AeroDragHandle>(true))
            if (string.IsNullOrEmpty(handle.CatalogId) && handle.Resize == resize && !handle.Depth)
                return handle;
        throw new InvalidOperationException("Missing manipulation handle: " + root.name);
    }

    static Vector3 Center(Button button) => button.transform.TransformPoint(((RectTransform)button.transform).rect.center);
    static Ray Aim(Transform target) => new Ray(target.position - target.forward * .15f, target.forward);
    static TextMeshProUGUI Value(Transform root) => root.Find("Value").GetComponent<TextMeshProUGUI>();

    static void Horizontal(Camera eye, Transform root, string context)
    {
        var a = eye.WorldToScreenPoint(root.position - root.right * .05f);
        var b = eye.WorldToScreenPoint(root.position + root.right * .05f);
        Require(a.z > .01f && b.z > .01f, context + ": projection is behind the eye");
        Require(Mathf.Abs(a.y - b.y) < .1f, context + ": projected text baseline is tilted");
        Require(Quaternion.Angle(root.rotation, eye.transform.rotation) < .05f,
            context + ": visual orientation does not match the eye");
    }

    public static void Run()
    {
        Require(Application.isPlaying, "Enter Play Mode first");
        using (var f = new Fixture())
        {
            var h = f.Hud; var l = h.Layout; var w = h.Widgets; var eye = f.Eye;
            Require(w.Items.Count == 5, "Expected the five separate metric widgets");
            var roots = new Dictionary<string, Transform>();
            foreach (var item in w.Items)
            {
                Require(!roots.ContainsKey(item.Id), "Duplicate catalog ID: " + item.Id);
                roots.Add(item.Id, item.Root);
                Require(l.WidgetRoot(item.Id) == item.Root, "Catalog/layout roots disagree: " + item.Id);
                Require(Handle(item.Root).PanelId == item.Id, "Move handle targets another panel");
            }

            f.Reset(); l.BeginEdit(false);
            Require(w.IsOpen, "Beginning edit did not open the independent catalog");
            Require(GameObject.Find("LayoutSideTools")==null, "Per-window right toolbar remains");
            f.Main.CloseButton.onClick.Invoke();
            Require(!h.IsCardOpen && w.IsOpen && l.Editing, "Main close also closed catalog editing");
            var paceEntry=f.Item("pace");var clickRay=Aim(paceEntry.EntryButton.transform);
            f.Native(clickRay,false);f.Native(clickRay,true);f.Native(clickRay,false);f.Clear();
            Require(paceEntry.Root.gameObject.activeSelf&&Vector3.Distance(paceEntry.Root.position,paceEntry.EntryButton.transform.position)>.2f,"Native click leaves new widget covering catalog");
            l.CloseWidget("pace");
            var heart = f.Item("heart");
            var entryRay = Aim(heart.EntryButton.transform);
            f.Native(entryRay, false); f.Native(entryRay, true);
            Require(l.Dragging && l.DragOwner == "right" && l.SelectedId == "heart",
                "Native catalog pinch did not capture heart");
            Require(heart.Root.gameObject.activeSelf && !w.IsOpen, "Catalog drag preview is missing or obscured");
            var previewStart = heart.Root.position;
            var moved = new Ray(entryRay.origin + new Vector3(.25f, .10f, 0), entryRay.direction);
            for (int i = 0; i < 10; i++) f.Native(moved, true);
            Require(!Near(previewStart, heart.Root.position), "Native catalog drag did not move the preview");
            f.Native(moved, false); f.Clear();
            Require(!l.Dragging && w.IsOpen && heart.Root.gameObject.activeSelf,
                "Native catalog release did not commit a visible heart");
            AssertInstances(w, roots);

            // Direct fingertip taps cover the catalog Button callback, separate from its ray drag handle.
            foreach (var item in w.Items)
            {
                if (item.Id == "heart") continue;
                f.Tap(item.EntryButton);
                Require(item.Root.gameObject.activeSelf && l.SelectedId == item.Id,
                    "Catalog tap did not add/select " + item.Id);
                AssertInstances(w, roots);
            }
            var existingPosition = heart.Root.position;
            var status = heart.EntryButton.transform.Find("EntryStatus").GetComponent<TextMeshProUGUI>();
            Require(status.text == "编辑", "Existing catalog entry does not say 编辑");
            f.Tap(heart.EntryButton);
            Require(l.SelectedId == "heart" && Near(existingPosition, heart.Root.position),
                "Tapping an existing entry moved or duplicated its widget");

            h.OpenFromHand();
            f.Click(w.DoneButton);
            Require(!l.Editing && !w.IsOpen, "Catalog Done did not end editing");
            foreach (var item in w.Items)
            {
                foreach (var handle in item.Root.GetComponentsInChildren<AeroDragHandle>(true))
                    Require(!handle.gameObject.activeInHierarchy, "A manipulation handle survived Done");
                Require(l.WidgetClose(item.Id).gameObject.activeInHierarchy && l.WidgetClose(item.Id).IsInteractable(),
                    "Done hid or disabled the permanent widget close control");
            }
            f.Main.CloseButton.onClick.Invoke();
            Require(!h.IsCardOpen && l.HasVisibleWidgets, "Closing main also closed the metric widgets");
            foreach (var item in w.Items)
                Require(item.Root.gameObject.activeSelf, "Main close removed " + item.Id);
            foreach (var item in w.Items)
            {
                f.Click(l.WidgetClose(item.Id));
                Require(!item.Root.gameObject.activeSelf, "Native close failed with main hidden: " + item.Id);
                Require(!h.IsCardOpen, "Closing a widget reopened main");
            }
            Require(!l.HasVisibleWidgets, "Visible-widget state remained set after all closes");
            foreach (var item in w.Items) l.AddWidget(item.Id);
            AssertInstances(w, roots);
            foreach (var item in w.Items)
                Require(item.Root.gameObject.activeSelf && item.Root == roots[item.Id], "Re-add did not reuse " + item.Id);

            // Shared route state updates even while its independent widget is hidden.
            var next = l.WidgetRoot("next");
            l.CloseWidget("next");
            var priorName = f.Main.Route.NextName;
            f.Main.Route.Advance();
            Require(Value(next).text == f.Main.Route.NextName && Value(next).text != priorName,
                "Hidden next-stop widget did not follow shared route state");
            l.AddWidget("next");
            Require(Value(next).text == f.Main.Route.NextName, "Reopening next-stop reset route text");
            var timer = l.WidgetRoot("timer");
            var timerText = Value(timer).text;
            Require(!string.IsNullOrWhiteSpace(timerText) && timerText.Contains(":"), "Session timer has no formatted value");
            l.CloseWidget("timer");
            Require(w.isActiveAndEnabled, "Hiding timer disabled its session clock controller");
            l.AddWidget("timer");
            Require(!string.IsNullOrWhiteSpace(Value(timer).text), "Reopened timer has no value");

            // Both the main card and a metric use the same orientation/placement contract.
            foreach (var key in new[] { "main", "heart" })
            foreach (var anchor in new[] { AeroWindowAnchor.Follow, AeroWindowAnchor.World, AeroWindowAnchor.View })
            {
                f.Reset(); l.BeginEdit(false);
                if (key != "main") l.AddWidget(key); else l.SelectWidget("main");
                var root = l.WidgetRoot(key);
                root.SetPositionAndRotation(eye.transform.position + Vector3.forward * .65f, Quaternion.identity);
                root.GetComponent<HudSoftFollow>().CaptureOffset();
                l.SetPlacement(anchor); l.PrepareFrame();
                var origin = root.position; var radius = Vector3.Distance(origin, eye.transform.position);
                var handle = Handle(root); var ray = Aim(handle.transform);
                l.StartDrag("right", handle, ray, .15f);
                Require(l.Dragging, "Orbit capture failed: " + key + "/" + anchor);
                var diagonal = new Ray(ray.origin + new Vector3(.35f, .25f, 0), ray.direction);
                f.Hold(diagonal);
                Require(!Near(origin, root.position) && Mathf.Abs(Vector3.Distance(root.position, eye.transform.position) - radius) < .0001f,
                    "Orbit did not preserve its radius: " + key + "/" + anchor);
                Horizontal(eye, root, "diagonal orbit " + key + "/" + anchor);
                l.StepDrag(true, false, diagonal, null, .02f);
                var committed = root.position;
                l.PrepareFrame();
                Require(Near(committed, root.position), "Orbit release jumped");
                Horizontal(eye, root, "released " + key + "/" + anchor);
                eye.transform.rotation = Quaternion.Euler(-12, 18, 31);
                l.PrepareFrame(); l.Tick(.02f, false);
                Horizontal(eye, root, "head roll while editing " + key + "/" + anchor);
                if (anchor == AeroWindowAnchor.World) Require(Near(committed, root.position), "Fixed position followed the head");
                l.EndEdit();
                eye.transform.rotation = Quaternion.Euler(-15, 52, -27);
                for (int i = 0; i < 180; i++)
                {
                    l.PrepareFrame(); l.Tick(.02f, false);
                    Horizontal(eye, root, "follow stage " + key + "/" + anchor);
                }
                if (anchor == AeroWindowAnchor.World) Require(Near(committed, root.position), "Fixed position drifted after Done");
            }

            foreach (var key in new[] { "main", "heart" })
            {
                f.Reset(); l.BeginEdit(false); if (key != "main") l.AddWidget(key);
                var palm = eye.transform.position + new Vector3(-.15f, -.25f, .45f);
                var handRotation = Quaternion.Euler(-65, 25, 32);
                h.HandEntry.Sample(true, palm, handRotation, false, Vector3.zero, .02f);
                Require(h.HandEntry.AnchorTracked, "Tilted hand fixture is not a valid dock");
                l.SetPlacement(AeroWindowAnchor.Hand); l.PrepareFrame();
                var root = l.WidgetRoot(key); var surface = h.HandEntry.ContactSurface;
                var normal = surface.rotation * Vector3.forward; var start = root.position;
                var handle = Handle(root); var ray = Aim(handle.transform);
                l.StartDrag("right", handle, ray, .15f);
                var tangent = surface.rotation * new Vector3(.08f, .04f, 0);
                var shifted = new Ray(ray.origin + tangent, ray.direction);
                f.Hold(shifted);
                Require(Near(root.position, start + tangent) && Mathf.Abs(Vector3.Dot(root.position - start, normal)) < .0001f,
                    "Hand move left the captured hand plane: " + key);
                Require(Vector3.Dot(root.forward, normal) > .9999f, "Hand move changed the panel normal");
                l.StepDrag(true, false, shifted, null, .02f); var committed = root.position; l.PrepareFrame();
                Require(Near(committed, root.position), "Hand move jumped on release");
                eye.transform.rotation = Quaternion.Euler(-8, 17, 24); l.PrepareFrame();
                var projectedUp = Vector3.ProjectOnPlane(eye.transform.up, normal).normalized;
                Require(Vector3.Dot(root.forward, normal) > .9999f && Vector3.Dot(root.up, projectedUp) > .9999f,
                    "Hand facing lost its normal or added wrist twist");
                ray = Aim(handle.transform); start = root.position;
                l.StartDrag("right", handle, ray, .15f); f.Hold(new Ray(ray.origin + tangent * .5f, ray.direction));
                h.HandEntry.Sample(false, palm, handRotation, false, Vector3.zero, .02f); l.PrepareFrame();
                Require(!l.Dragging && Near(start, root.position) && !root.GetComponent<Canvas>().enabled,
                    "Lost hand tracking committed the pending move or left it rendered");
            }

            foreach (var key in new[] { "main", "heart" })
            {
                f.Reset(); l.BeginEdit(false); if (key != "main") l.AddWidget(key);
                var root = l.WidgetRoot(key); var rect = (RectTransform)root;
                var start = root.position; var scale = root.localScale;
                var size = root.GetComponent<HudSoftFollow>().UserScale;
                var rotation = root.rotation;
                var pivot = root.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMax, 0));
                var handle = Handle(root, true); var point = handle.transform.position;
                var ray = new Ray(eye.transform.position, (point - eye.transform.position).normalized);
                l.StartDrag("right", handle, ray, Vector3.Distance(eye.transform.position, point));
                var larger = pivot + (point - pivot) * 1.25f;
                var enlargedRay = new Ray(eye.transform.position, (larger - eye.transform.position).normalized);
                f.Hold(enlargedRay);
                Require(Mathf.Abs(root.GetComponent<HudSoftFollow>().UserScale - size * 1.25f) < .002f,
                    "Resize preview did not change size");
                eye.transform.rotation = Quaternion.Euler(-13, 22, 35);
                for (int i = 0; i < 8; i++)
                {
                    l.PrepareFrame(); l.StepDrag(true, true, enlargedRay, null, .02f); l.PrepareFrame(); l.Tick(.02f, false);
                    Require(Near(pivot, root.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMax, 0))) &&
                        Quaternion.Angle(root.rotation, rotation) < .05f, "Head turn moved the captured resize pivot");
                }
                l.StepDrag(false, true, enlargedRay, null, .02f);
                Require(!l.Dragging && Near(start, root.position) && Near(scale, root.localScale) &&
                    Mathf.Abs(root.GetComponent<HudSoftFollow>().UserScale - size) < .001f,
                    "Resize cancellation committed the preview");
            }

            f.Reset(); l.BeginEdit(false);
            entryRay = Aim(heart.EntryButton.transform);
            f.Native(entryRay, false); f.Native(entryRay, true);
            Require(l.Dragging && heart.Root.gameObject.activeSelf, "New-widget cancellation fixture did not capture");
            f.NativeOff(); h.ProcessInput();
            Require(!l.Dragging && !heart.Root.gameObject.activeSelf && !l.HasVisibleWidgets && w.IsOpen,
                "Native tracking loss left a newly dragged widget visible");
            f.Clear(); f.Native(entryRay, false); f.Native(entryRay, true);
            Require(l.Dragging, "Second catalog capture failed after cancellation");
            l.CancelDrag("Widgets020 explicit cancellation");
            Require(!heart.Root.gameObject.activeSelf && !l.HasVisibleWidgets, "Explicit catalog cancellation left a widget");
            AssertInstances(w, roots);

            Debug.Log("[AeroPulse Widgets020] PASS native catalog drag/release/cancel, direct catalog taps, five unique reusable widgets, independent native closes, catalog Done, route linkage, timer value, main/heart Follow/World/View projected baseline, tilted-hand plane/normal/loss, resize pivot and cancellation. Timer elapsed-time continuity and Quest experience remain device/session checks.");
        }
    }

    static void AssertInstances(AeroWidgetCatalog catalog, Dictionary<string, Transform> roots)
    {
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var item in catalog.Items)
        {
            Require(item.Root == roots[item.Id], "Widget root was replaced: " + item.Id);
            int count = 0;
            foreach (var canvas in canvases)
                if (canvas.gameObject.scene.IsValid() && canvas.name == "Widget_" + item.Id) count++;
            Require(count == 1, "Expected one live/inactive canvas for " + item.Id + ", got " + count);
        }
    }

    sealed class Fixture : IDisposable
    {
        public readonly PinchHudExperiment Hud;
        public readonly Camera Eye;
        public readonly AeroHudView Main;
        readonly TrackedPoseDriver driver;
        readonly bool driverEnabled, handEnabled, directInput, keepOpen;
        readonly float checkDelta;
        readonly Pose eyeLocal, parentPose;
        readonly int routeIndex;

        public Fixture()
        {
            Hud = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>(); Eye = Camera.main;
            Require(Hud != null && Eye != null && Hud.Widgets != null && Hud.Layout != null, "Widget scene is missing");
            keepOpen=Hud.Widgets.KeepOpen;Hud.Widgets.SetKeepOpen(true);
            Main = Hud.Follow.GetComponent<AeroHudView>(); driver = Eye.GetComponent<TrackedPoseDriver>();
            driverEnabled = driver != null && driver.enabled;
            handEnabled = Hud.HandEntry.enabled; directInput = Hud.DirectInputForChecks; checkDelta = Hud.Layout.DeltaForChecks;
            eyeLocal = new Pose(Eye.transform.localPosition, Eye.transform.localRotation);
            parentPose = new Pose(Eye.transform.parent.position, Eye.transform.parent.rotation);
            routeIndex = Main.Route.CurrentIndex;
            if (driver != null) driver.enabled = false;
            Hud.HandEntry.enabled = false; Hud.DirectInputForChecks = true; Hud.Layout.DeltaForChecks = .02f;
            Hud.SendMessage("OnApplicationFocus", true);
            if (MetaAimHand.left == null) MetaAimHand.left = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Left);
            if (MetaAimHand.right == null) MetaAimHand.right = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
        }

        public AeroWidgetCatalog.Item Item(string id)
        {
            foreach (var item in Hud.Widgets.Items) if (item.Id == id) return item;
            throw new InvalidOperationException("Missing catalog item: " + id);
        }

        public void Reset()
        {
            if (Hud.Layout.Editing) Hud.Layout.EndEdit();
            Eye.transform.parent.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Eye.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            Hud.ResetViewOptions(); Hud.OpenDataFromShortcut(); Clear();
        }

        public void NativeOff()
        {
            AeroHandRayChecks.Set(MetaAimHand.left, false, false, Vector3.zero, Quaternion.identity);
            AeroHandRayChecks.Set(MetaAimHand.right, false, false, Vector3.zero, Quaternion.identity);
            Hud.DirectTouch.Sample(false, Vector3.zero, false, Vector3.zero, .02f);
        }

        public void Clear() { NativeOff(); Hud.ClearPresses(); Hud.ProcessInput(); }

        public void Native(Ray ray, bool pressed)
        {
            Hud.DirectTouch.Sample(false, Vector3.zero, false, Vector3.zero, .02f);
            AeroHandRayChecks.Frame(MetaAimHand.right, ray.origin, Quaternion.LookRotation(ray.direction), pressed, Eye.transform.parent);
            Hud.ProcessInput();
        }

        public void Click(Button button)
        {
            Clear(); var center = Center(button);
            var ray = new Ray(center - button.transform.forward * .15f, button.transform.forward);
            Native(ray, false); Native(ray, true); Native(ray, false); Clear();
        }

        public void Tap(Button button)
        {
            Clear(); var center = Center(button); var normal = -button.transform.forward;
            Hud.DirectTouch.Sample(false, Vector3.zero, true, center + normal * .03f, .02f);
            Hud.ProcessInput();
            Hud.DirectTouch.Sample(false, Vector3.zero, true, center + normal * .001f, .02f);
            Hud.ProcessInput();
            Hud.DirectTouch.Sample(false, Vector3.zero, true, center + normal * .03f, .02f);
            Hud.ProcessInput(); Clear();
        }

        public void Hold(Ray ray)
        { for (int i = 0; i < 9; i++) Hud.Layout.StepDrag(true, true, ray, null, .02f); }

        public void Dispose()
        {
            try
            {
                if (Hud.Layout.Dragging) Hud.Layout.CancelDrag("Widgets020 cleanup");
                if (Hud.Layout.Editing) Hud.Layout.EndEdit();
                NativeOff(); Hud.ClearPresses();
                Hud.ResetViewOptions(); Main.ShowMap(false); Main.CloseButton.onClick.Invoke();
                for (int i = 0; i < Main.Route.Count && Main.Route.CurrentIndex != routeIndex; i++) Main.Route.Advance();
            }
            finally
            {
                Hud.Widgets.SetKeepOpen(keepOpen);Hud.Layout.DeltaForChecks = checkDelta; Hud.DirectInputForChecks = directInput; Hud.HandEntry.enabled = handEnabled;
                Eye.transform.parent.SetPositionAndRotation(parentPose.position, parentPose.rotation);
                Eye.transform.SetLocalPositionAndRotation(eyeLocal.position, eyeLocal.rotation);
                if (driver != null) driver.enabled = driverEnabled;
                Physics.SyncTransforms();
            }
        }
    }
}
