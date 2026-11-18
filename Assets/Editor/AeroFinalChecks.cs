using System;
using UnityEngine;
using UnityEngine.XR.Hands;
using TMPro;

public static class AeroFinalChecks
{
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static T[] All<T>() where T:UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None);
    public static void Cold()
    {
        Require(Application.isPlaying,"Play Mode required");
        Require(All<PinchHudExperiment>().Length==1&&All<AeroHudView>().Length==1&&All<AeroSkyMap>().Length==1&&All<AeroCompactHud>().Length==1,"Duplicate or missing window owner after reload");
        Require(All<HandBackHudEntry>().Length==1&&All<AeroWindowLayout>().Length==1&&All<AeroDockZone>().Length==3,"Duplicate or missing hand/layout state after reload");
        Require(All<UnityEngine.EventSystems.EventSystem>().Length==1,"Duplicate EventSystem after reload");
        var h=All<PinchHudExperiment>()[0];var v=All<AeroHudView>()[0];
        Require(!h.IsCardOpen&&h.Sky.Visible&&!h.Layout.Editing&&!h.Layout.Dragging,"Cold scene retains open/edit state");
        Require(h.HandEntry.EnabledForEntry&&!h.HandEntry.ShortcutsEnabled&&Mathf.Abs(h.HandEntry.HeightOffset-0.035f)<0.0001f,"Cold hand defaults incorrect");
        Require(h.Follow.Mode==HudSoftFollow.FollowMode.Soft&&h.Compact.Side==AeroCompactHud.Placement.Hidden&&h.Layout.MainAnchor==AeroWindowAnchor.Follow,"Cold placement defaults incorrect");
        Require(v.Route.CurrentIndex==0&&ReferenceEquals(v.Route,h.Sky.Route)&&ReferenceEquals(v.Route,h.Compact.Route),"Cold route state not fresh/shared");
        Require(!h.ToolsEntry.Visible&&!h.Widgets.IsOpen,"Cold scene exposes editing tools");
        foreach(var item in h.Widgets.Items)Require(!h.Layout.WidgetClose(item.Id).gameObject.activeSelf&&!h.Layout.WidgetClose(item.Id).IsInteractable(),"Cold scene exposes widget close");
        foreach(var text in All<TextMeshProUGUI>()){text.ForceMeshUpdate();Require(!text.isTextOverflowing,"Text overflow: "+text.name);}
        Debug.Log("[AeroPulse Final] PASS cold/reloaded scene: one owner/window set, defaults, shared fresh route, text fit");
    }
    public static void RepeatedJourney()
    {
        var h=All<PinchHudExperiment>()[0];var v=All<AeroHudView>()[0];var c=Camera.main;
        c.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>().enabled=false;
        c.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
        h.HandEntry.enabled=false;h.ResetViewOptions();h.OpenDataFromShortcut();
        if(MetaAimHand.left==null)MetaAimHand.left=MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Left);
        if(MetaAimHand.right==null)MetaAimHand.right=MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
        AeroHandRayChecks.Set(MetaAimHand.left,false,false,Vector3.zero,Quaternion.identity);
        void Click(UnityEngine.UI.Button b)
        {
            var p=c.transform.position+new Vector3(0.25f,-0.3f,0.2f);var q=Quaternion.LookRotation(b.transform.position-p);
            AeroHandRayChecks.Frame(MetaAimHand.right,p,q,false,c.transform.parent);h.ProcessInput();
            AeroHandRayChecks.Frame(MetaAimHand.right,p,q,true,c.transform.parent);h.ProcessInput();
        }
        for(int i=0;i<20;i++)
        {
            Require(h.IsCardOpen,"Repeated journey card missing");
            if(!v.IsMapOpen)v.ToggleSettings();
            v.ShowMap(true);Require(v.IsMapOpen,"Repeated MAP failed");
            int before=v.Route.CurrentIndex;bool skyWas=h.Sky.Visible;h.ToggleSkyMap();
            Require(h.Sky.Visible!=skyWas&&v.gameObject.activeSelf,"Sky toggle changed HUD visibility");
            v.MapView.AdvanceButton.onClick.Invoke();Require(v.Route.CurrentIndex==(before+1)%v.Route.Count,"Repeated ADVANCE failed");
            h.ShowMapDetail();Require(v.gameObject.activeSelf&&v.IsMapOpen,"Repeated DETAIL failed");
            h.CloseCardForChecks();Require(!h.IsCardOpen,"Main close did not hide the HUD");
            h.OpenFromHand();Require(h.IsCardOpen&&v.Route.CurrentIndex==(before+1)%v.Route.Count,"Repeated reopen lost route");
        }
        h.ResetViewOptions();h.RestoreMainVisibility(false);h.HandEntry.enabled=true;
        Debug.Log("[AeroPulse Final] PASS 20 native-ray journeys: MAP/SKY/ADVANCE/DETAIL/HIDE/reopen and shared route");
    }
}
