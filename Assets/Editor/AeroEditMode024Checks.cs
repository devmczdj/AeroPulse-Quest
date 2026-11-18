using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Hands;

// Production input routes, with profile files isolated from the user's saved layouts.
public static class AeroEditMode024Checks
{
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException("EditMode024: "+message);}
    static string State(AeroWindowLayout l)=>JsonUtility.ToJson(l.CaptureLayout());
    public static void Run()
    {
        Require(Application.isPlaying,"Play Mode required");
        var h=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var l=h.Layout;var w=h.Widgets;var v=h.Follow.GetComponent<AeroHudView>();var s=v.SettingsView;var c=Camera.main;var tools=h.ToolsEntry;
        var original=l.CaptureLayout();var poses=new Dictionary<string,Pose>();foreach(var p in original.panels){var t=l.WidgetRoot(p.id);poses[p.id]=new Pose(t.position,t.rotation);}
        var eyePose=new Pose(c.transform.localPosition,c.transform.localRotation);var parentPose=new Pose(c.transform.parent.position,c.transform.parent.rotation);
        var driver=c.GetComponent<TrackedPoseDriver>();bool driven=driver.enabled,direct=h.DirectInputForChecks,leftReader=h.HandEntry.enabled,rightReader=tools.enabled,editing=l.Editing,preview=l.IsPreviewing,open=w.IsOpen,keep=w.KeepOpen;
        var layer=l.EditingLayer;string selection=l.SelectedId;bool map=v.IsMapOpen,settings=v.IsSettingsOpen;var tab=s.CurrentTab;
        bool mainOpen=h.IsCardOpen, profilePage=w.ShowingProfiles, resetConfirm=s.ResetConfirmationOpen;
        int entryMode=!h.HandEntry.EnabledForEntry?2:h.HandEntry.ShortcutsEnabled?1:0;
        var compactSide=h.Compact.Side;
        string directory=Path.Combine(Path.GetTempPath(),"aero-editmode024-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);string path=Path.Combine(directory,"profiles.json");
        if(MetaAimHand.left==null)MetaAimHand.left=MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Left);
        if(MetaAimHand.right==null)MetaAimHand.right=MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
        void Off(){AeroHandRayChecks.Set(MetaAimHand.left,false,false,Vector3.zero,Quaternion.identity);AeroHandRayChecks.Set(MetaAimHand.right,false,false,Vector3.zero,Quaternion.identity);h.DirectTouch.Sample(false,Vector3.zero,false,Vector3.zero,.02f);h.Near.Sample(false,Vector3.zero,Vector3.zero,false,Vector3.zero,Vector3.zero,.02f);h.ClearPresses();}
        void Native(Button b,bool pressed,bool left=false){AeroHandRayChecks.Frame(left?MetaAimHand.left:MetaAimHand.right,b.transform.position-b.transform.forward*.15f,Quaternion.LookRotation(b.transform.forward),pressed,c.transform.parent);h.ProcessInput();}
        void Click(Button b){Require(b.gameObject.activeInHierarchy&&b.IsInteractable(),"Inactive click target "+b.name);Off();Native(b,false);Native(b,true);Native(b,false);Off();}
        void Poke(Button b){Off();var p=b.transform.position;foreach(float d in new[]{.03f,.001f,.03f})h.DirectTouch.Sample(false,Vector3.zero,true,p-b.transform.forward*d,.02f);Off();}
        var palm=new Vector3(.15f,1.3f,.45f);var rotation=Quaternion.Euler(-90,0,0);var normal=rotation*Vector3.up;
        void TrackTools(float height,bool finger=true){var p=palm+normal*h.HandEntry.HeightOffset;tools.Sample(true,palm,rotation,finger,p+normal*height,.02f);}
        void NormalSafe(string context)
        {
            TrackTools(.04f,false);Require(!l.Editing&&!w.IsOpen&&!tools.Visible&&!tools.Button.IsInteractable(),context+": tool entry escaped normal mode");
            foreach(var p in l.CaptureLayout().panels)
            {
                if(p.id!="main")foreach(var collider in l.WidgetRoot(p.id).GetComponentsInChildren<Collider>(true))Require(!collider.enabled,context+": read-only widget still intercepts input");
                var close=l.WidgetClose(p.id);
                bool shouldOfferClose=p.id=="main";
                Require(close.gameObject.activeSelf==shouldOfferClose&&close.IsInteractable()==shouldOfferClose,context+": incorrect close control for "+p.id);
                foreach(var handle in l.WidgetRoot(p.id).GetComponentsInChildren<AeroDragHandle>(true))Require(!handle.gameObject.activeInHierarchy,context+": handle remains active");
            }
        }
        using(h.Profiles.UseStoreForChecks(path))
        try
        {
            driver.enabled=false;h.HandEntry.enabled=false;tools.enabled=false;h.DirectInputForChecks=true;h.SendMessage("OnApplicationFocus",true);tools.SetPaused(false);
            c.transform.parent.SetPositionAndRotation(Vector3.zero,Quaternion.identity);c.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
            if(l.Editing)l.EndEdit();
            l.ResetAll();h.SetHandEntryMode(0);h.SetCompactPlacement(AeroCompactHud.Placement.Hidden);
            h.OpenDataFromShortcut();NormalSafe("Initial");
            // Fixture creation uses the layout API; user entry is verified through settings below.
            l.BeginEdit(false);w.SetKeepOpen(true);l.AddWidget("heart");l.EndEdit();var preset=l.CaptureLayout();
            Array.Find(preset.panels,p=>p.id=="main").visible=true;
            Require(h.Profiles.Store.TryAdd("PRESET",preset,out var first),"First preset fixture failed");Require(h.Profiles.Store.TryAdd("SECOND",preset,out var second),"Second preset fixture failed");
            Require(h.Profiles.Store.TryActivate(first.id),"Preset activation fixture failed");h.Profiles.ReloadStoreForChecks();Require(h.Profiles.RestoreSavedLayout(),"Startup restore failed");
            NormalSafe("Restored profile");string before=State(l),disk=File.ReadAllText(path);
            foreach(var p in preset.panels){if(p.id=="main")continue;var close=l.WidgetClose(p.id);h.ActivateButton(close);close.onClick.Invoke();l.CloseWidget(p.id);}
            l.ToggleTools();l.AddWidget("pace");l.SelectWidget("heart");l.SetPlacement(AeroWindowAnchor.View);l.TogglePin();l.ToggleSpeed();l.Undo();l.ResetSelected();
            Require(!h.Profiles.SaveCurrent()&&!h.Profiles.SaveAs("FORBIDDEN")&&!h.Profiles.RenameCurrent("FORBIDDEN"),"Normal mode accepted profile edits");h.Profiles.RequestActivate(second.id);
            TrackTools(.04f);TrackTools(.002f);tools.Button.onClick.Invoke();
            Off();foreach(bool down in new[]{false,true,false})Native(tools.Button,down,true);Off();
            var x=l.WidgetClose("heart");Poke(x);foreach(bool down in new[]{false,true,false})Native(x,down);Off();
            Require(State(l)==before&&File.ReadAllText(path)==disk&&!h.Profiles.ModalOpen,"Normal input/API changed preset or opened editor");NormalSafe("After normal interactions");
            h.SendMessage("OnApplicationFocus",false);h.SendMessage("OnApplicationFocus",true);TrackTools(.002f);NormalSafe("Focus recovery");
            Require(State(l)==before,"Focus recovery changed layout");
            var away=AeroLayoutProfileStore.Clone(preset);foreach(var p in away.panels)if(p.id=="heart"){p.anchor=(int)AeroWindowAnchor.World;p.pinned=true;p.followMode=(int)HudSoftFollow.FollowMode.Fixed;p.position=new Vector3(0,0,-1);}
            l.ApplyLayout(away);var fixedMetric=l.WidgetRoot("heart").position;Require(!l.NeedsRecall,"Offscreen metric enabled normal recall affordance");h.Recenter();
            Require(Vector3.Distance(fixedMetric,l.WidgetRoot("heart").position)<.001f,"Normal recall repositioned a preset metric");l.ApplyLayout(preset);
            bool dirtyBeforeFront=h.Profiles.IsDirty;
            h.SetHandEntryMode(2);
            h.CloseCardForChecks();
            Require(!h.IsCardOpen&&l.WidgetRoot("heart").gameObject.activeSelf,"Front-mode close hid a metric or left the main HUD open");
            h.ProcessInput();
            Require(h.Compact.FallbackVisible&&!l.Editing,"Front entry did not return after hiding");
            Click(h.Compact.OpenButton);
            h.OpenFromHand();
            Require(h.IsCardOpen&&!l.Editing&&l.WidgetRoot("heart").gameObject.activeSelf,"Front ray could not reopen just the main HUD");
            Require(State(l)==before&&File.ReadAllText(path)==disk&&h.Profiles.IsDirty==dirtyBeforeFront,"Front-mode open/close changed the preset");
            h.SetHandEntryMode(0);
            // The normal left-HUD route remains usable for reaching settings.
            h.ToggleFromHand();Require(!h.IsCardOpen&&l.WidgetRoot("heart").gameObject.activeSelf,"Normal HUD hide removed a metric");Native(x,false);Require(!GameObject.Find("RightHandRay").GetComponent<LineRenderer>().enabled,"Read-only metric view still draws an interaction ray");Off();h.ToggleFromHand();Require(h.IsCardOpen&&!l.Editing,"Normal HUD open entered editing");
            Click(v.SettingsButton);s.SelectTab(AeroHudSettingsView.SettingsTab.Layout);
            Require(s.LayoutPage&&!l.Editing&&!tools.Visible,"Layout page exposed edit tools before entry");
            s.EditCardButton.onClick.Invoke();
            Require(l.Editing&&!w.IsOpen,"Explicit settings edit should keep catalog hidden");
            Require(h.Feedback.Visible&&h.Feedback.Message.Contains("左手手背"),"Edit entry did not teach the hand-back quick setting");
            l.OpenTools();Require(w.IsOpen,"Right-hand tool entry could not reveal components");
            w.CloseButton.onClick.Invoke();Require(!w.IsOpen&&l.Editing,"Catalog close did not hide tools");l.OpenTools();
            Require(l.WidgetClose("heart").gameObject.activeInHierarchy,"Editor close control is missing");l.CloseWidget("heart");Require(!l.WidgetRoot("heart").gameObject.activeSelf,"Editor close failed");l.AddWidget("heart");
            l.OpenTools();w.DoneButton.onClick.Invoke();Require(!w.IsOpen&&!l.Editing,"Done did not end editing");NormalSafe("Done");
            // Held ray input cannot delete on an edit boundary; release then press can.
            l.BeginEdit(false);Off();l.EndEdit();l.BeginEdit(false);
            Require(l.WidgetRoot("heart").gameObject.activeSelf,"Edit boundary changed a widget");
            l.OpenTools();l.CloseWidget("heart");Require(!l.WidgetRoot("heart").gameObject.activeSelf,"Fresh editor close no longer works");Off();l.AddWidget("heart");
            // A finger already at the hand entry must also release across mode changes.
            l.EndEdit();TrackTools(.002f);l.BeginEdit(false);l.OpenTools();TrackTools(.002f);TrackTools(.002f);Require(w.IsOpen,"Held touch toggled tools after entry");TrackTools(.04f);TrackTools(.002f);Require(!w.IsOpen&&l.Editing,"Released editor touch cannot toggle palette");l.EndEdit();TrackTools(.002f);NormalSafe("Final normal mode");
            // A temporary visit to settings must not change a widgets-only profile.
            var metricsOnly=AeroLayoutProfileStore.Clone(preset);
            Array.Find(metricsOnly.panels,p=>p.id=="main").visible=false;
            Require(h.Profiles.Store.TryAdd("METRICS",metricsOnly,out var metrics),"Metrics-only fixture failed");
            h.Profiles.ReloadStoreForChecks();Require(h.Profiles.RestoreSavedLayout()&&!h.IsCardOpen,"Expected hidden main HUD after restore");
            h.ToggleFromHand();Require(h.IsCardOpen,"Could not visit settings from the metrics-only profile");
            if(!v.IsSettingsOpen)v.ToggleSettings();s.SelectTab(AeroHudSettingsView.SettingsTab.Layout);l.BeginEdit(false);
            Require(!h.Profiles.IsDirty,"Opening settings changed the metrics-only profile");
            Require(h.Profiles.SaveCurrent()&&!Array.Find(h.Profiles.Store.Find(metrics.id).layout.panels,p=>p.id=="main").visible,"Saving while settings were open persisted the temporary HUD");
            h.Profiles.RequestActivate(second.id);
            Require(!h.Profiles.ModalOpen&&h.Profiles.Store.Data.activeId==second.id,"Visiting settings prevented a clean switch");
            Debug.Log("[AeroPulse EditMode024] PASS restored-profile normal lock, stale/direct/ray/touch rejection, settings-only edit path, front-entry main close/reopen, editor metric close, immediate Done hide and held-input boundaries; physical Quest behavior pending");
        }
        finally
        {
            Off();if(l.Dragging)l.CancelDrag("");if(l.Editing)l.EndEdit();c.transform.parent.SetPositionAndRotation(parentPose.position,parentPose.rotation);c.transform.SetLocalPositionAndRotation(eyePose.position,eyePose.rotation);l.ApplyLayout(original);h.RestoreMainVisibility(mainOpen);
            h.SetHandEntryMode(entryMode);h.SetCompactPlacement(compactSide);
            foreach(var p in original.panels){var t=l.WidgetRoot(p.id);var saved=poses[p.id];t.SetPositionAndRotation(saved.position,saved.rotation);t.GetComponent<HudSoftFollow>().SyncView();}
            if(editing){l.BeginEdit(false);l.SetEditingLayer(layer);l.SelectWidget(selection);l.SetPreview(preview);if(!open&&w.IsOpen)l.ToggleTools();else if(open&&!w.IsOpen)l.ToggleTools();}
            v.ShowMap(map);if(settings&&!v.IsSettingsOpen)v.ToggleSettings();s.SelectTab(tab);if(resetConfirm)s.RequestReset();w.SetKeepOpen(keep);w.SetPage(profilePage);tools.Sample(false,Vector3.zero,Quaternion.identity,false,Vector3.zero,.02f);
            h.HandEntry.enabled=leftReader;tools.enabled=rightReader;h.DirectInputForChecks=direct;driver.enabled=driven;Directory.Delete(directory,true);
        }
    }
}
