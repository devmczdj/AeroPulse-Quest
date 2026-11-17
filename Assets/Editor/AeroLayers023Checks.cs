using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

// Real layout transactions and native-ray/direct-poke selection, with saves redirected to temp.
public static class AeroLayers023Checks
{
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Layers023: " + message); }
    static AeroEditLayer Layer(AeroPanelState p) => p.anchor == (int)AeroWindowAnchor.Hand ? AeroEditLayer.Hand : p.anchor == (int)AeroWindowAnchor.View ? AeroEditLayer.View : AeroEditLayer.Environment;
    static AeroDragHandle Move(Transform root) { foreach (var h in root.GetComponentsInChildren<AeroDragHandle>(true)) if (!h.Resize && !h.Depth) return h; throw new InvalidOperationException("Missing move handle"); }
    static string State(AeroWindowLayout layout) => JsonUtility.ToJson(layout.CaptureLayout());

    public static void Run()
    {
        Require(Application.isPlaying, "Enter Play Mode first");
        var h = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>(); var l = h.Layout; var w = h.Widgets; var eye = Camera.main;
        Require(!l.Dragging && !h.Profiles.ModalOpen, "Finish current manipulation/modal first");
        var original = l.CaptureLayout(); var poses = new Dictionary<string, Pose>();
        foreach (var p in original.panels) { var t = l.WidgetRoot(p.id); poses[p.id] = new Pose(t.position, t.rotation); }
        bool editing = l.Editing, preview = l.IsPreviewing, open = w.IsOpen, keep = w.KeepOpen, page = w.ShowingProfiles, direct = h.DirectInputForChecks, handEnabled = h.HandEntry.enabled, dirty = h.Profiles.IsDirty;
        string selected = l.SelectedId; var layerBefore = l.EditingLayer;
        var driver = eye.GetComponent<TrackedPoseDriver>(); bool driven = driver.enabled;
        var eyePose = new Pose(eye.transform.localPosition, eye.transform.localRotation); var parentPose = new Pose(eye.transform.parent.position, eye.transform.parent.rotation);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var trackedField = typeof(HandBackHudEntry).GetField("<AnchorTracked>k__BackingField", flags); var tracked = trackedField.GetValue(h.HandEntry);
        var contactField = typeof(HandBackHudEntry).GetField("<ContactSurface>k__BackingField", flags); var contact = contactField.GetValue(h.HandEntry);
        var route = h.Follow.GetComponent<AeroHudView>().Route; int routeIndex = route.CurrentIndex;
        var session = typeof(AeroWidgetCatalog).GetField("sessionStart", flags); var clockStart = session.GetValue(w);
        string temp = Path.Combine(Path.GetTempPath(), "aero-layers023-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        if (MetaAimHand.left == null) MetaAimHand.left = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Left);
        if (MetaAimHand.right == null) MetaAimHand.right = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
        void Off()
        {
            AeroHandRayChecks.Set(MetaAimHand.left, false, false, Vector3.zero, Quaternion.identity); AeroHandRayChecks.Set(MetaAimHand.right, false, false, Vector3.zero, Quaternion.identity);
            h.Near.Sample(false, Vector3.zero, Vector3.zero, false, Vector3.zero, Vector3.zero, .02f); h.DirectTouch.Sample(false, Vector3.zero, false, Vector3.zero, .02f); h.ClearPresses();
        }
        void RayClick(Button button)
        {
            Off(); var p = button.transform.position; var rotation = Quaternion.LookRotation(button.transform.forward);
            foreach (bool down in new[] { false, true, false }) { AeroHandRayChecks.Frame(MetaAimHand.right, p - button.transform.forward * .15f, rotation, down, eye.transform.parent); h.ProcessInput(); }
            Off();
        }
        void Tap(Button button)
        {
            Off(); var p = button.transform.position; var normal = -button.transform.forward;
            foreach (float depth in new[] { .03f, .001f, .03f }) { h.DirectTouch.Sample(false, Vector3.zero, true, p + normal * depth, .02f); h.ProcessInput(); }
            Off();
        }
        AeroWidgetCatalog.Item Item(string id) { foreach (var item in w.Items) if (item.Id == id) return item; throw new InvalidOperationException("Missing item"); }
        void CatalogDrag(string id, bool cancel)
        {
            if (!w.IsOpen) l.ToggleTools(); w.SetPage(false); var button = Item(id).EntryButton;
            var ray = new Ray(button.transform.position - button.transform.forward * .15f, button.transform.forward);
            l.StartDrag("right", button.GetComponent<AeroDragHandle>(), ray, .15f);
            Require(l.Dragging && !w.IsOpen, "Catalog drag did not hide palette during preview");
            var moved = new Ray(ray.origin + Vector3.right * .1f, ray.direction);
            for (int i = 0; i < 10; i++) l.StepDrag(true, true, moved, null, .02f);
            l.StepDrag(!cancel, !cancel ? false : true, moved, null, .02f); Off();
            Require(!l.Dragging, "Catalog transaction did not end");
        }
        try
        {
            driver.enabled = false; h.HandEntry.enabled = false; h.DirectInputForChecks = true; h.SendMessage("OnApplicationFocus", true);
            eye.transform.parent.SetPositionAndRotation(Vector3.zero, Quaternion.identity); eye.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            if (l.Editing) l.EndEdit(); h.ResetViewOptions(); h.OpenDataFromShortcut(); l.BeginEdit(false); w.SetKeepOpen(true); w.SetPage(false);
            trackedField.SetValue(h.HandEntry, true); contactField.SetValue(h.HandEntry, new Pose(new Vector3(-.15f, 1.3f, .45f), Quaternion.identity));
            l.AddWidget("heart"); l.AddWidget("pace"); l.SetPlacement(AeroWindowAnchor.Hand); l.AddWidget("distance"); l.SetPlacement(AeroWindowAnchor.View); l.AddWidget("next"); l.SetPlacement(AeroWindowAnchor.Follow); l.TogglePin();
            l.SetEditingLayer(AeroEditLayer.Environment); var beforeFilter = State(l); var stored = l.CaptureLayout();
            foreach (AeroEditLayer layer in Enum.GetValues(typeof(AeroEditLayer)))
            {
                l.SetEditingLayer(layer); l.PrepareFrame();
                Require(State(l) == beforeFilter, "Layer filtering changed a persisted layout field");
                foreach (var p in stored.panels)
                {
                    var root = l.WidgetRoot(p.id); Require(root.gameObject.activeSelf == p.visible, "Filtering changed real visibility for " + p.id);
                    if (!p.visible) continue;
                    bool shown = Layer(p) == layer; Require(root.GetComponent<Canvas>().enabled == shown, "Wrong layer rendered: " + p.id);
                    Require(l.CanInteract(root) == shown, "Filtered body remained interactable: " + p.id);
                    if (!shown) foreach (var col in root.GetComponentsInChildren<Collider>(true)) Require(!col.enabled, "Filtered collider remained enabled: " + col.name);
                    foreach (var handle in root.GetComponentsInChildren<AeroDragHandle>(true)) Require(handle.gameObject.activeInHierarchy == (shown && l.SelectedId == p.id), "Unselected component exposes manipulation handles");
                }
            }
            l.SetEditingLayer(AeroEditLayer.Hand);
            using (h.Profiles.UseStoreForChecks(Path.Combine(temp, "profiles.json")))
            {
                Require(h.Profiles.SaveAs("LAYER CHECK"), "Saving under a layer filter failed"); var reloaded = new AeroLayoutProfileStore(Path.Combine(temp, "profiles.json")); Require(reloaded.Load(), "Saved layout cannot reload");
                Require(JsonUtility.ToJson(reloaded.Find(reloaded.Data.activeId).layout) == beforeFilter, "Saved filter became permanent hidden components");
            }
            l.SetEditingLayer(AeroEditLayer.View); route.Advance();
            Require(l.WidgetRoot("next").Find("Value").GetComponent<TextMeshProUGUI>().text == route.NextName && Equals(clockStart, session.GetValue(w)), "Filtering reset/paused data controller state");
            l.SelectWidget("heart"); string chosen = l.SelectedId; var chosenLayer = l.EditingLayer; var beforePreview = State(l);
            l.SetPreview(true); Require(l.IsPreviewing && !w.IsOpen, "All-preview did not close tools");
            foreach (var p in l.CaptureLayout().panels)
            {
                var root = l.WidgetRoot(p.id); if (p.visible) Require(root.GetComponent<Canvas>().enabled, "Preview omitted enabled component " + p.id);
                foreach (var handle in root.GetComponentsInChildren<AeroDragHandle>(true)) Require(!handle.gameObject.activeInHierarchy, "Preview exposes a drag handle");
                Require(!l.CanInteract(root), "Preview allows component editing");
            }
            Require(!l.CanInteract(h.Compact.OpenButton.transform), "Preview still allows layout-changing recall");
            h.ActivateButton(l.WidgetClose("heart")); h.ToggleFromHand(); l.SetPlacement(AeroWindowAnchor.Hand);
            Require(State(l) == beforePreview, "Preview mutated layout through background control");
            l.ToggleTools(); Require(w.IsOpen && l.IsPreviewing, "Tools could not reopen from preview");
            l.SetPreview(false); Require(l.SelectedId == chosen && l.EditingLayer == chosenLayer, "Returning from preview lost current selection/layer");
            RayClick(w.LayerButtons[(int)AeroEditLayer.Hand]); Require(!l.IsPreviewing && l.EditingLayer == AeroEditLayer.Hand && w.IsOpen, "Layer control failed or closed palette");
            l.SetEditingLayer(AeroEditLayer.Environment); if (w.IsOpen) l.ToggleTools();
            RayClick(l.WidgetRoot("next").GetComponent<Button>()); Require(l.SelectedId == "next", "Ray body selection failed");
            RayClick(l.WidgetRoot("heart").GetComponent<Button>()); Require(l.SelectedId == "heart", "Second body selection failed");
            Tap(l.WidgetClose("heart")); Require(!l.WidgetRoot("heart").gameObject.activeSelf, "Coplanar body swallowed child close poke");

            w.SetKeepOpen(false); l.BeginEdit(false); l.AddWidget("heart");
            Require(!w.IsOpen && l.Editing && l.WidgetRoot("heart").gameObject.activeSelf, "Single add did not close only the tools");
            l.ToggleTools(); var heartPose = l.WidgetRoot("heart").position; l.AddWidget("heart");
            Require(w.IsOpen && Vector3.Distance(heartPose, l.WidgetRoot("heart").position) < .001f, "Existing selection closed tools or moved component");
            CatalogDrag("timer", false); Require(!w.IsOpen && l.WidgetRoot("timer").gameObject.activeSelf, "Single drag commit did not close palette");
            l.CloseWidget("timer"); l.BeginEdit(false); var beforeCancel = State(l); CatalogDrag("timer", true);
            Require(w.IsOpen && State(l) == beforeCancel, "Cancel closed tools or kept preview state");
            w.SetKeepOpen(true); CatalogDrag("timer", false); Require(w.IsOpen && l.WidgetRoot("timer").gameObject.activeSelf, "Continuous drag commit closed palette");
            l.ToggleTools(); l.SetPaused(true); l.SetPaused(false); Require(!w.IsOpen, "Focus restore reopened deliberately closed tools");
            w.SetKeepOpen(false);l.BeginEdit(false);w.SetPage(true);
            string singlePath=Path.Combine(temp,"single-save.json");
            using(h.Profiles.UseStoreForChecks(singlePath))
            {
                Require(h.Profiles.SaveAs("SINGLE SAVE")&&!w.IsOpen&&l.Editing,"Successful single save did not close only tools");
                l.ToggleTools();Directory.CreateDirectory(singlePath+".tmp");
                Require(!h.Profiles.SaveAs("FAILED SAVE")&&w.IsOpen,"Failed save closed tools");
                Directory.Delete(singlePath+".tmp");
            }
            l.SetPreview(true);h.ToolsEntry.Sample(false,Vector3.zero,Quaternion.identity,false,Vector3.zero,.02f);Off();h.ProcessInput();
            Require(h.Compact.ToolsFallbackVisible&&l.CanInteract(h.Compact.OpenButton.transform),"Right tracking loss leaves no tool recovery entry");
            var recoveryBefore=State(l);var recover=h.Compact.OpenButton;var recoveryPoint=recover.transform.position;
            foreach(bool down in new[]{false,true,false}){AeroHandRayChecks.Frame(MetaAimHand.left,recoveryPoint-recover.transform.forward*.15f,Quaternion.LookRotation(recover.transform.forward),down,eye.transform.parent);h.ProcessInput();}
            Off();Require(w.IsOpen&&l.IsPreviewing&&State(l)==recoveryBefore,"Left-ray tool recovery changed the preview layout");
            l.SetEditingLayer(AeroEditLayer.Hand);var beforeReveal=State(l);h.ToggleFromHand();
            Require(h.IsCardVisible&&State(l)==beforeReveal,"Left entry changed hidden main persistence instead of revealing its layer");
            l.SetEditingLayer(AeroEditLayer.Hand); var beforeDone = State(l); l.EndEdit();
            Require(!w.IsOpen && !l.Editing && State(l) == beforeDone, "Done altered real layout or left tools open");
            foreach (var p in l.CaptureLayout().panels) if (p.visible) Require(l.WidgetRoot(p.id).GetComponent<Canvas>().enabled, "Done failed to restore enabled layer " + p.id);
            Debug.Log("[AeroPulse Layers023] PASS temporary filters/save isolation, selected handles, preview read-only, layer/body ray selection and child-close poke, single/continuous commit/cancel, closed-tools focus restore and independent live data. Quest hand feel remains pending.");
        }
        finally
        {
            Off(); if (l.Dragging) l.CancelDrag("Layers023 cleanup"); if (l.Editing) l.EndEdit();
            eye.transform.parent.SetPositionAndRotation(parentPose.position, parentPose.rotation); eye.transform.SetLocalPositionAndRotation(eyePose.position, eyePose.rotation);
            trackedField.SetValue(h.HandEntry, tracked); contactField.SetValue(h.HandEntry, contact); l.ApplyLayout(original);
            while (route.CurrentIndex != routeIndex) route.Advance();
            if (editing) { l.BeginEdit(false); l.SetEditingLayer(layerBefore); l.SelectWidget(selected); l.SetPreview(preview); if (!open && w.IsOpen) l.ToggleTools(); else if (open && !w.IsOpen) l.ToggleTools(); }
            foreach (var p in original.panels) { var t = l.WidgetRoot(p.id); var pose = poses[p.id]; t.SetPositionAndRotation(pose.position, pose.rotation); t.GetComponent<HudSoftFollow>().SyncView(); }
            w.SetKeepOpen(keep); w.SetPage(page); h.DirectInputForChecks = direct; h.HandEntry.enabled = handEnabled; driver.enabled = driven;
            typeof(AeroLayoutProfileView).GetProperty("IsDirty").SetValue(h.Profiles, dirty); h.Profiles.Refresh(); Directory.Delete(temp, true); Physics.SyncTransforms();
        }
    }
}
