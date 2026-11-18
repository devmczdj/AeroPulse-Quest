using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
public static class AeroSkyChecks
{
    static void Require(bool v,string m){if(!v)throw new InvalidOperationException(m);}
    public static void Run()
    {
        var hud=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var c=Camera.main;
        c.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
        hud.ResetViewOptions();hud.OpenDataFromShortcut();var view=hud.Follow.GetComponent<AeroHudView>();
        hud.ShowSkyMap();var sky=hud.Sky;
        Require(sky.Visible&&hud.IsCardOpen,"Sky and HUD cannot coexist");
        Require(ReferenceEquals(view.Route,sky.Route),"Sky route cloned");
        Require(sky.GetComponentsInChildren<Button>(true).Length==0&&sky.GetComponentsInChildren<Collider>(true).Length==0&&sky.GetComponentsInChildren<GraphicRaycaster>(true).Length==0,"Passive sky still has UI input surfaces");
        Require(sky.transform.Find("SkyTitle")==null&&sky.transform.Find("SkyGrabHandle")==null,"Sky still has HUD title/handle");
        Require(sky.GetComponentsInChildren<Graphic>(true).Length<25,"Sky still builds hundreds of line UI objects");
        var pos=sky.transform.position;var rot=sky.transform.rotation;
        if(hud.IsCardOpen)hud.ToggleFromHand();Require(sky.Visible&&!hud.IsCardOpen,"HUD HIDE closed sky");
        hud.OpenFromHand();Require(sky.Visible&&hud.IsCardOpen,"Hand SHOW altered independent sky");
        hud.ShowMapDetail();Require(sky.Visible&&view.IsMapOpen,"Detail view consumed sky");
        hud.Recenter();Require(Vector3.Distance(pos,sky.transform.position)<0.001f,"HUD recenter moved sky");
        var delta=new Vector3(2,0.25f,-1);c.transform.position+=delta;sky.Tick(0.02f);
        Require(Vector3.Distance(sky.transform.position,pos+delta)<0.002f&&Quaternion.Angle(rot,sky.transform.rotation)<0.01f,"Sky does not follow translation independently of heading");
        c.transform.rotation=Quaternion.Euler(0,50,0);for(int i=0;i<20;i++)sky.Tick(0.02f);
        Require(Quaternion.Angle(rot,sky.transform.rotation)<0.01f,"Sky rigidly head locked");
        c.transform.rotation=Quaternion.Euler(0,150,0);for(int i=0;i<140;i++)sky.Tick(0.02f);
        Require(Quaternion.Angle(rot,sky.transform.rotation)>10,"Sky left behind after large sustained turn");
        int start=view.Route.CurrentIndex;var current=sky.CurrentMarker.localPosition;view.Route.Advance();
        Require(sky.Route.CurrentIndex==(start+1)%5&&Vector3.Distance(current,sky.CurrentMarker.localPosition)>0.01f,"Shared current marker stale");
        bool cardOpen=hud.IsCardOpen;hud.ToggleSkyMap();Require(!sky.Visible&&hud.IsCardOpen==cardOpen,"Sky toggle altered HUD");
        if(hud.IsCardOpen)hud.ToggleFromHand();hud.OpenFromHand();Require(!sky.Visible,"HUD reopen resurrected disabled sky");
        hud.ToggleSkyMap();hud.SendMessage("OnApplicationFocus",false);hud.SendMessage("OnApplicationFocus",true);
        Require(sky.Visible,"Focus recovery lost sky preference");
        hud.ToggleSkyMap();hud.SendMessage("OnApplicationFocus",false);hud.SendMessage("OnApplicationFocus",true);
        Require(!sky.Visible,"Focus recovery reopened disabled sky");
        c.transform.rotation=Quaternion.identity;hud.ShowSkyMap();hud.RecenterSky();hud.ResetViewOptions();view.ShowMap(false);if(hud.IsCardOpen)hud.ToggleFromHand();
        Debug.Log("[AeroPulse Sky015] PASS independent HUD/toggles/focus, no buttons/colliders/window chrome, curved batched geometry, body translation, stable heading and sustained-turn recovery, shared route");
    }
}
