using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

// Exercises real catalog controls and production ray input, with all writes redirected to temp.
public static class AeroProfilesUi021Checks
{
    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException("ProfilesUI021: " + message); }
    static Button Find(AeroWidgetCatalog catalog, string name)
    {
        foreach (var button in catalog.CatalogRoot.GetComponentsInChildren<Button>(true)) if (button.name == name) return button;
        throw new InvalidOperationException("Missing profile button " + name);
    }
    static void Clear(PinchHudExperiment h)
    {
        AeroHandRayChecks.Set(MetaAimHand.left, false, false, Vector3.zero, Quaternion.identity);
        AeroHandRayChecks.Set(MetaAimHand.right, false, false, Vector3.zero, Quaternion.identity);
        h.Near.Sample(false, Vector3.zero, Vector3.zero, false, Vector3.zero, Vector3.zero, .02f);
        h.DirectTouch.Sample(false, Vector3.zero, false, Vector3.zero, .02f);
        h.ClearPresses(); h.ProcessInput();
    }
    static void Click(PinchHudExperiment h, Button button)
    {
        Require(button.gameObject.activeInHierarchy && button.IsInteractable(), "Cannot click " + button.name);
        Clear(h);
        var point = button.transform.position;
        var rotation = Quaternion.LookRotation(button.transform.forward);
        foreach (bool pressed in new[] { false, true, false })
        {
            AeroHandRayChecks.Frame(MetaAimHand.right, point - button.transform.forward * .15f, rotation, pressed, Camera.main.transform.parent);
            h.ProcessInput();
        }
        Clear(h);
    }

    public static void Run()
    {
        Require(Application.isPlaying, "Enter Play Mode first");
        var h = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
        Require(h != null && h.Profiles != null, "Profile scene is missing");
        var v = h.Profiles; var l = h.Layout; var w = h.Widgets;
        bool direct = h.DirectInputForChecks, editing = l.Editing, profilePage = w.ShowingProfiles;
        var oldLayout = l.CaptureLayout();
        string directory = Path.Combine(Path.GetTempPath(), "aeropulse-profile-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        h.DirectInputForChecks = true; h.SendMessage("OnApplicationFocus", true);
        if (MetaAimHand.left == null) MetaAimHand.left = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Left);
        if (MetaAimHand.right == null) MetaAimHand.right = MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
        using (v.UseStoreForChecks(Path.Combine(directory, "profiles.json")))
        {
            try
            {
                l.ResetAll(); l.BeginEdit(false); l.AddWidget("heart");
                Click(h, w.ProfilesTab);
                Require(w.ShowingProfiles && w.ProfilesPage.gameObject.activeInHierarchy && !w.ComponentsPage.gameObject.activeInHierarchy, "Tabs did not switch page");
                Click(h, Find(w, "SaveCurrentProfile"));
                Require(v.ModalOpen && !w.DoneButton.gameObject.activeInHierarchy && !w.ComponentsTab.interactable, "Naming dialog leaves background controls active");
                Click(h, Find(w, "NamePreset0")); Click(h, Find(w, "ConfirmProfileName"));
                Require(v.Store.Data.profiles.Length == 1 && v.ActiveName == "日常 1" && !v.IsDirty && !v.ModalOpen, "First GUI save failed");
                string first = v.Store.Data.activeId;
                l.CloseWidget("heart"); l.AddWidget("pace");
                Require(v.IsDirty, "Widget changes did not mark dirty");
                string savedBeforeCancel = File.ReadAllText(Path.Combine(directory, "profiles.json"));
                Click(h, Find(w, "SaveNewProfile")); Click(h, Find(w, "NamePreset2")); Click(h, Find(w, "CancelProfileName"));
                Require(v.IsDirty && v.Store.Data.profiles.Length == 1 && File.ReadAllText(Path.Combine(directory, "profiles.json")) == savedBeforeCancel, "Cancel naming mutated saved data");
                Click(h, Find(w, "SaveNewProfile"));
                Click(h, Find(w, "NameClear")); Click(h, Find(w, "NameKey0"));
                Click(h, Find(w, "NameCharacterSet")); Click(h, Find(w, "NameKey1"));
                Click(h, Find(w, "ConfirmProfileName"));
                Require(v.ActiveName == "A1" && v.Store.Data.profiles.Length == 2 && !v.IsDirty, "Alphanumeric name/save-as failed");
                string second = v.Store.Data.activeId;
                l.CloseWidget("pace"); l.AddWidget("distance");
                Click(h, Find(w, "ProfileRow0"));
                Require(v.ModalOpen, "Dirty switch skipped confirmation");
                bool mainOpen = h.IsCardOpen;
                h.ActivateButton(l.WidgetClose("main"));
                Require(h.IsCardOpen == mainOpen, "Background main close bypassed confirmation modal");
                var handle = l.CardHandle;
                l.StartDrag("right", handle, new Ray(handle.transform.position - handle.transform.forward * .1f, handle.transform.forward), .1f);
                Require(!l.Dragging, "Background drag bypassed confirmation modal");
                Require(!l.StartNearDrag("right", handle, handle.transform.position) && !l.Dragging, "Near drag bypassed confirmation modal");
                Click(h, Find(w, "CancelProfileSwitch"));
                Require(!v.ModalOpen && v.IsDirty && v.Store.Data.activeId == second && l.WidgetRoot("distance").gameObject.activeSelf, "Cancel discarded current layout");
                Click(h, Find(w, "ProfileRow0")); Click(h, Find(w, "DiscardThenSwitch"));
                Require(!v.IsDirty && v.Store.Data.activeId == first && l.WidgetRoot("heart").gameObject.activeSelf && !l.WidgetRoot("distance").gameObject.activeSelf, "Discard/enable failed");
                Click(h, Find(w, "RenameProfile")); Click(h, Find(w, "NamePreset2")); Click(h, Find(w, "ConfirmProfileName"));
                Require(v.ActiveName == "演示 1", "Rename failed");
                l.CloseWidget("heart");
                Click(h, Find(w, "ProfileRow1")); Click(h, Find(w, "SaveThenSwitch"));
                Require(!v.IsDirty && v.Store.Data.activeId == second && l.WidgetRoot("pace").gameObject.activeSelf, "Save/enable failed");
                foreach (var panel in v.Store.Find(first).layout.panels) if (panel.id == "heart") Require(!panel.visible, "Save-before-switch did not persist edit");
                Require(!v.SaveAs("A1") && v.Store.Data.profiles.Length == 2, "Duplicate name accepted");
                string routeValue = l.WidgetRoot("next").Find("Value").GetComponent<TextMeshProUGUI>().text;
                string timerValue = l.WidgetRoot("timer").Find("Value").GetComponent<TextMeshProUGUI>().text;
                l.CloseWidget("pace"); l.AddWidget("timer");
                v.ReloadStoreForChecks(); Require(v.RestoreSavedLayout(), "Reloaded saved layout failed");
                Require(v.Store.Data.activeId == second && !v.IsDirty && l.WidgetRoot("pace").gameObject.activeSelf && !l.WidgetRoot("timer").gameObject.activeSelf, "Restart restore used unsaved preview");
                Require(l.WidgetRoot("next").Find("Value").GetComponent<TextMeshProUGUI>().text == routeValue && l.WidgetRoot("timer").Find("Value").GetComponent<TextMeshProUGUI>().text == timerValue, "Profile restore rewrote live values");
                for (int i = 3; i <= 12; i++) Require(v.SaveAs("TEST " + i), "Could not save profile " + i);
                Require(!Find(w, "SaveNewProfile").IsInteractable(), "Limit not shown in UI");
                Click(h, Find(w, "PreviousProfilePage"));
                Require(Find(w, "NextProfilePage").IsInteractable(), "Pagination cannot return to last page");
                Click(h, Find(w, "NextProfilePage"));
                Require(!Find(w, "NextProfilePage").IsInteractable(), "Pagination exceeds final page");
                Click(h, w.DoneButton);
                Require(!l.Editing && !w.IsOpen, "Done did not end profile editing");
                Debug.Log("[AeroPulse ProfilesUI021] PASS real ray tabs, naming presets/keyboard, save-as/rename, dirty cancel/discard/save switch, modal input isolation, temp-file reload/live data preservation, limit/pagination and Done. Quest readability and hand feel remain pending.");
            }
            finally
            {
                if (v.ModalOpen) Find(w, "CancelProfileName").onClick.Invoke();
                if (l.Dragging) l.CancelDrag("ProfilesUI021 cleanup");
                if (l.Editing) l.EndEdit();
                Clear(h); l.ApplyLayout(oldLayout);
                if (editing) l.BeginEdit(false);
                w.SetPage(profilePage);
                h.DirectInputForChecks = direct;
                Directory.Delete(directory, true);
            }
        }
    }
}
