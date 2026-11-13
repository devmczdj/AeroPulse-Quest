using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;
public static class AeroShortcutChecks
{
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static void Run()
    {
        var hud=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var camera=Camera.main;
        camera.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);hud.ResetViewOptions();hud.OpenDataFromShortcut();
        var view=UnityEngine.Object.FindObjectsByType<AeroHudView>(FindObjectsInactive.Include)[0];var reader=hud.HandEntry;reader.enabled=false;
        void Track(bool valid=true)
        {
            Vector3 palm=camera.transform.position+camera.transform.forward*0.5f-camera.transform.right*0.2f-Vector3.up*0.3f;
            reader.Sample(valid,palm,camera.transform.rotation*Quaternion.Euler(-90,0,0),false,Vector3.zero,0.02f);
        }
        void Point(Transform target,bool pressed)
        {
            var origin=camera.transform.position+new Vector3(0.25f,-0.3f,0.2f);
            AeroHandRayChecks.Frame(MetaAimHand.right,origin,Quaternion.LookRotation(target.position-origin),pressed,camera.transform.parent);hud.ProcessInput();
        }
        void Click(UnityEngine.UI.Button b){Point(b.transform,false);Point(b.transform,true);}
        Click(view.SettingsButton);if(!view.SettingsView.LayoutPage)Click(view.SkyButton);Click(view.SettingsView.EntryButton);Track();
        Require(reader.EnabledForEntry&&reader.ShortcutsEnabled&&reader.ShortcutsVisible,"Shortcut mode not visible");
        foreach(var text in reader.ShortcutRoot.GetComponentsInChildren<TextMeshProUGUI>())
        {text.ForceMeshUpdate();Require(!text.isTextOverflowing,"Hand shortcut label overflow: "+text.name);}
        view.CloseButton.onClick.Invoke();Track();Click(reader.SkyShortcut);
        Require(hud.Sky.Visible&&hud.IsCardOpen,"Hand SKY cannot open from closed state");
        Click(reader.DataShortcut);Require(view.gameObject.activeSelf&&!view.IsMapOpen&&!hud.Sky.Visible,"Hand DATA does not return to metrics");
        hud.Follow.SetMode(HudSoftFollow.FollowMode.Fixed);hud.Layout.SetAnchorFromFollowMode();
        var prior=view.transform.position;camera.transform.rotation=Quaternion.Euler(0,100,0);Track();Click(reader.RecallShortcut);
        Require(Vector3.Distance(prior,view.transform.position)>0.1f&&Vector3.Angle(camera.transform.forward,view.transform.position-camera.transform.position)<2,"Hand RECALL did not use current view");
        view.CloseButton.onClick.Invoke();Track(false);Require(!reader.ShortcutsVisible,"Lost hand leaves shortcuts active");
        // Recover while holding a pinch aimed at SKY; clear gates must prevent an automatic click.
        Track();Point(reader.SkyShortcut.transform,false);Track(false);Point(reader.SkyShortcut.transform,true);Track();Point(reader.SkyShortcut.transform,true);
        Require(!hud.IsCardOpen,"Shortcut recovery while pinching opened UI");Click(reader.SkyShortcut);Require(hud.Sky.Visible,"Fresh shortcut after recovery fails");
        hud.Layout.BeginEdit(false);Track();Require(!reader.ShortcutsVisible,"Layout editor still exposes hand shortcut colliders");hud.Layout.EndEdit();
        hud.ResetViewOptions();Require(!reader.ShortcutsEnabled&&reader.EnabledForEntry,"Reset did not restore single hand-back entry");
        if(view.SettingsView.LayoutPage)view.SettingsView.TogglePage();view.ShowMap(false);view.CloseButton.onClick.Invoke();reader.enabled=true;
        Debug.Log("[AeroPulse Shortcuts] PASS: selectable DATA/SKY/RECALL, closed-state rays, labels, recovery held-pinch gate, edit suppression and single-entry reset");
    }
}
