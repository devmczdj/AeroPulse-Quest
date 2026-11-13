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
        Require(!h.IsCardOpen&&!h.Sky.Visible&&!h.Layout.Editing&&!h.Layout.Dragging,"Cold scene retains open/edit state");
        Require(h.HandEntry.EnabledForEntry&&!h.HandEntry.ShortcutsEnabled&&Mathf.Abs(h.HandEntry.HeightOffset-0.035f)<0.0001f,"Cold hand defaults incorrect");
        Require(h.Follow.Mode==HudSoftFollow.FollowMode.Soft&&h.Compact.Side==AeroCompactHud.Placement.Hidden&&h.Layout.MainAnchor==AeroWindowAnchor.Follow,"Cold placement defaults incorrect");
        Require(v.Route.CurrentIndex==0&&ReferenceEquals(v.Route,h.Sky.Route)&&ReferenceEquals(v.Route,h.Compact.Route),"Cold route state not fresh/shared");
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
            Require(h.IsCardOpen&&!h.Sky.Visible,"Repeated journey card missing");
            if(v.IsMapOpen)Click(v.MapButton);
            Click(v.MapButton);Require(v.IsMapOpen,"Repeated MAP failed");
            int before=v.Route.CurrentIndex;Click(v.SkyButton);Require(h.Sky.Visible&&!v.gameObject.activeSelf,"Repeated SKY failed");
            Click(h.Sky.AdvanceButton);Require(v.Route.CurrentIndex==(before+1)%v.Route.Count,"Repeated ADVANCE failed");
            Click(h.Sky.DetailButton);Require(v.gameObject.activeSelf&&v.IsMapOpen,"Repeated DETAIL failed");
            Click(v.CloseButton);Require(!h.IsCardOpen,"Repeated HIDE failed");
            h.OpenFromHand();Require(h.IsCardOpen&&v.Route.CurrentIndex==(before+1)%v.Route.Count,"Repeated reopen lost route");
        }
        h.ResetViewOptions();v.CloseButton.onClick.Invoke();h.HandEntry.enabled=true;
        Debug.Log("[AeroPulse Final] PASS 20 native-ray journeys: MAP/SKY/ADVANCE/DETAIL/HIDE/reopen and shared route");
    }
}
