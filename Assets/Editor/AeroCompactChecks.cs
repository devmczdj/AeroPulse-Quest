using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;

public static class AeroCompactChecks
{
    static void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
    public static void Run()
    {
        var hud=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();hud.OpenFromHand();
        var view=UnityEngine.Object.FindObjectsByType<AeroHudView>(FindObjectsInactive.Include)[0];view.ShowMap(false);
        var camera=Camera.main;
        void Click(UnityEngine.UI.Button button)
        {
            var origin=camera.transform.position+new Vector3(0.25f,-0.3f,0.2f);
            var aim=Quaternion.LookRotation(button.transform.position-origin);
            AeroHandRayChecks.Frame(MetaAimHand.right,origin,aim,false,camera.transform.parent);hud.ProcessInput();
            AeroHandRayChecks.Frame(MetaAimHand.right,origin,aim,true,camera.transform.parent);hud.ProcessInput();
        }
        Click(view.SettingsButton);Click(view.SettingsView.TabButtons[0]);
        Require(view.SettingsView.CurrentTab==AeroHudSettingsView.SettingsTab.Display,"Display settings navigation failed");
        Click(view.SettingsView.CompactOptions[1]);var compact=hud.Compact;
        Require(compact.Side==AeroCompactHud.Placement.Left&&compact.gameObject.activeSelf&&compact.transform.localPosition.x<0,"Left HUD selection failed");
        Require(compact.transform.parent==camera.transform&&ReferenceEquals(compact.Route,view.Route),"Compact anchor/state not shared");
        var local=compact.transform.localPosition;var world=compact.transform.position;
        camera.transform.rotation*=Quaternion.Euler(0,40,0);
        Require(Vector3.Distance(local,compact.transform.localPosition)<0.001f&&Vector3.Distance(world,compact.transform.position)>0.05f,"Small HUD is not view-relative");
        hud.ShowSkyMap();var skyPosition=hud.Sky.transform.position;camera.transform.rotation*=Quaternion.Euler(0,30,0);
        Require(Vector3.Distance(skyPosition,hud.Sky.transform.position)<0.001f,"Compact choice made sky follow head");
        view.Route.Advance();
        Require(compact.NextLabel.text.Contains(view.Route.NextName),"Compact next stop stale");
        foreach(var text in compact.GetComponentsInChildren<TextMeshProUGUI>())
        {text.ForceMeshUpdate();Require(!text.isTextOverflowing,"Compact text overflow: "+text.name);}
        hud.ShowMapDetail();view.ShowMap(false);Click(view.SettingsButton);
        Click(view.SettingsView.CompactOptions[2]);
        Require(compact.Side==AeroCompactHud.Placement.Right&&compact.transform.localPosition.x>0,"Right HUD selection failed");
        Click(view.SettingsView.CompactOptions[0]);
        Require(compact.Side==AeroCompactHud.Placement.Hidden&&!compact.gameObject.activeSelf,"Hidden readout stays visible");
        compact.SetPlacement(AeroCompactHud.Placement.Left);
        hud.SendMessage("OnApplicationFocus",false);Require(!compact.gameObject.activeSelf,"Unfocused readout visible");
        hud.SendMessage("OnApplicationFocus",true);Require(compact.gameObject.activeSelf,"Focus recovery lost compact preference");
        Click(view.SettingsView.TabButtons[1]);Click(view.SettingsView.EntryOptions[1]);Require(hud.HandEntry.ShortcutsEnabled,"Shortcut entry cycle failed");Click(view.SettingsView.EntryOptions[2]);Require(!hud.HandEntry.EnabledForEntry,"Entry setting did not switch");
        Click(view.CloseButton);hud.ProcessInput();
        Require(!hud.IsCardOpen&&compact.FallbackVisible&&GameObject.Find("Hotspot-SPORTS-HUD")==null,"Fallback missing or old orb restored");
        Click(compact.OpenButton);hud.ProcessInput();
        Require(hud.IsCardOpen&&!compact.FallbackVisible,"Fallback cannot open or remains blocking UI");
        if(!view.IsSettingsOpen)Click(view.SettingsButton);Click(view.SettingsView.EntryOptions[0]);Require(hud.HandEntry.EnabledForEntry,"Hand entry cannot restore");
        Click(view.SettingsView.ResetButton);Click(view.SettingsView.ConfirmResetButton);
        Require(compact.Side==AeroCompactHud.Placement.Hidden&&hud.HandEntry.EnabledForEntry,"Default layout reset failed");
        if(view.SettingsView.LayoutPage)view.SettingsView.TogglePage();
        view.ShowMap(false);view.CloseButton.onClick.Invoke();
        Debug.Log("[AeroPulse Compact] PASS: real-ray left/right/hidden, shared next state, head/world separation, focus, fallback open and default reset");
    }
}
