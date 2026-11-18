using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;

public static class AeroViewOptionsChecks
{
    static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        var hud = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
        var camera = Camera.main;
        hud.HandEntry.enabled = false;
        hud.OpenFromHand();
        var view = GameObject.Find("PinchHudCard").GetComponent<AeroHudView>();
        view.ShowMap(false);
        void Click(UnityEngine.UI.Button button)
        {
            var origin = camera.transform.position+new Vector3(0.25f,-0.3f,0.2f);
            var aim = Quaternion.LookRotation(button.transform.position-origin);
            AeroHandRayChecks.Frame(MetaAimHand.right,origin,aim,false,camera.transform.parent); hud.ProcessInput();
            AeroHandRayChecks.Frame(MetaAimHand.right,origin,aim,true,camera.transform.parent); hud.ProcessInput();
        }
        Click(view.SettingsButton);
        Require(view.IsSettingsOpen,"Settings entry ray failed");
        var settings = view.SettingsView;
        Click(settings.TabButtons[(int)AeroHudSettingsView.SettingsTab.Layout]);
        Click(settings.ModeOptions[1]);
        Require(hud.Follow.Mode==HudSoftFollow.FollowMode.Quick,"Follow setting not bound");
        hud.SetPanelDistance(2.2f);
        Require(Mathf.Approximately(hud.Follow.Distance,2.2f),"Distance setting not bound");
        Click(settings.TabButtons[(int)AeroHudSettingsView.SettingsTab.Input]);
        hud.SetHandHeight(.045f);
        Require(Mathf.Approximately(hud.HandEntry.HeightOffset,0.045f),"Height setting not bound");
        var palm = new Vector3(-0.15f,1.3f,0.45f);
        var root = Quaternion.Euler(-90,0,0);
        void CheckHeight(float expected)
        {
            hud.HandEntry.Sample(true,palm,root,false,Vector3.zero,0.02f);
            Require(Mathf.Abs(Vector3.Distance(hud.HandEntry.Surface.position,palm)-expected)<0.001f,
                    "Visual pad height did not update");
            Require(Vector3.Distance(hud.HandEntry.ContactSurface.position,palm+root*Vector3.up*0.014f)<0.001f,
                    "Visual height moved physical touch proxy");
        }
        CheckHeight(0.045f);
        hud.SetHandHeight(.025f); CheckHeight(0.025f);
        hud.SetHandHeight(.035f); CheckHeight(0.035f);
        foreach(var text in view.GetComponentsInChildren<TextMeshProUGUI>())
        { text.ForceMeshUpdate(); Require(!text.isTextOverflowing,"Settings text overflow: "+text.name); }
        Click(settings.ResetButton);Click(settings.ConfirmResetButton);
        Require(hud.Follow.Mode==HudSoftFollow.FollowMode.Soft && Mathf.Approximately(hud.Follow.Distance,.65f) &&
                Mathf.Approximately(hud.HandEntry.HeightOffset,0.035f),"Reset defaults failed");
        Click(view.SettingsButton);
        Require(!view.IsSettingsOpen && !view.IsMapOpen,"DONE did not return to data");
        Click(view.MapButton); int position=view.Route.CurrentIndex;
        Click(view.SettingsButton); Click(view.SettingsButton);
        Require(view.IsMapOpen && view.Route.CurrentIndex==position,"Settings lost map mode/state");
        var follow=hud.Follow;
        var pointerField=typeof(PinchHudExperiment).GetField("rightPointer",BindingFlags.NonPublic|BindingFlags.Instance);
        var pointer=pointerField.GetValue(hud); var type=pointer.GetType();
        void Pointer(bool over,bool pressed,UnityEngine.UI.Button button)
        { type.GetField("overCard").SetValue(pointer,over); type.GetField("pressed").SetValue(pointer,pressed); type.GetField("button").SetValue(pointer,button); }
        camera.transform.rotation=Quaternion.identity; follow.SnapToView();
        camera.transform.rotation=Quaternion.Euler(0,70,0);
        Pointer(true,true,null);
        Require(!hud.FollowPaused,"Blank card / idle pinch still freezes follow");
        for(int i=0;i<300;i++) follow.Tick(0.02f,hud.FollowPaused);
        Require(Vector3.Distance(follow.transform.position,camera.transform.position+camera.transform.forward*follow.Distance)<0.02f,"Soft follow did not return");
        Pointer(true,true,view.CloseButton); var held=follow.transform.position;
        camera.transform.rotation=Quaternion.Euler(0,95,0);
        Require(hud.FollowPaused,"Active button press no longer pauses");
        for(int i=0;i<20;i++) follow.Tick(0.02f,hud.FollowPaused);
        Require(Vector3.Distance(held,follow.transform.position)<0.001f,"Active button press moved panel");
        camera.transform.rotation=Quaternion.Euler(0,150,0);
        Require(!hud.FollowPaused,"Offscreen button target blocks recovery");
        Pointer(false,false,null);
        follow.CycleMode(); // Quick
        follow.SnapToView(); camera.transform.rotation=Quaternion.Euler(0,220,0);
        for(int i=0;i<300;i++) follow.Tick(0.02f,false);
        Require(Vector3.Distance(follow.transform.position,camera.transform.position+camera.transform.forward*follow.Distance)<0.02f,"Quick follow failed");
        follow.CycleMode(); held=follow.transform.position; camera.transform.rotation=Quaternion.Euler(0,290,0);
        for(int i=0;i<200;i++) follow.Tick(0.02f,false);
        Require(Vector3.Distance(held,follow.transform.position)<0.001f,"Fixed mode follows head");
        hud.Recenter();
        Require(Vector3.Distance(follow.transform.position,camera.transform.position+camera.transform.forward*follow.Distance)<0.02f,"Manual recenter failed in fixed mode");
        hud.ResetViewOptions(); view.ShowMap(false); if(hud.IsCardOpen)hud.ToggleFromHand();
        hud.HandEntry.enabled=true;
        Debug.Log("[AeroPulse ViewOptions] PASS: real ray settings, distance/height/reset, data/map return, empty-card and pinch follow recovery, button pause/escape, quick/fixed/recenter");
    }
}
