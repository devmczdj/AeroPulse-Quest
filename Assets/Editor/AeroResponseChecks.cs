using System;
using UnityEngine;
using UnityEngine.XR.Hands;
public static class AeroResponseChecks
{
    static void Require(bool v,string m){if(!v)throw new InvalidOperationException(m);}
    public static void Run()
    {
        var fusion=new PinchSourceLatch();
        Require(!fusion.Step(1,true,0,out _),"Stale native pinch masks released joint");
        Require(fusion.Step(0,false,1,out _),"Joint pinch ignored");
        Require(!fusion.Step(1,true,0,out _),"Joint release latched by native channel");
        fusion.Step(1,true,-1,out bool sourceChanged);Require(sourceChanged,"Source switch not signalled");
        var h=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var v=h.Follow.GetComponent<AeroHudView>();var c=Camera.main;
        c.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);h.ResetViewOptions();h.OpenDataFromShortcut();h.HandEntry.enabled=false;
        AeroHandRayChecks.Set(MetaAimHand.left,false,false,Vector3.zero,Quaternion.identity);
        void Point(Transform target,bool pressed){var p=c.transform.position+new Vector3(.25f,-.3f,.2f);AeroHandRayChecks.Frame(MetaAimHand.right,p,Quaternion.LookRotation(target.position-p),pressed,c.transform.parent);h.ProcessInput();}
        for(int i=0;i<30;i++){bool was=v.IsMapOpen;Point(v.MapButton.transform,false);Point(v.MapButton.transform,true);Require(v.IsMapOpen!=was,"Aimed button did not dispatch on press frame");}
        v.ShowMap(false);
        // One tracking sample starts the pinch just outside the button, then the real ray reaches it.
        var miss=new GameObject("PressTimingMiss");miss.transform.position=c.transform.position+new Vector3(3,2,1);
        Point(miss.transform,false);Point(miss.transform,true);Point(v.MapButton.transform,true);
        Require(v.IsMapOpen,"Early pinch edge was consumed before aim arrived");
        Point(v.MapButton.transform,true);Require(v.IsMapOpen,"Held pinch doubleclicked");
        AeroHandRayChecks.Set(MetaAimHand.right,false,true,Vector3.zero,Quaternion.identity);h.ProcessInput();Point(v.MapButton.transform,true);
        Require(v.IsMapOpen,"Tracking recovery held pinch clicked");Point(v.MapButton.transform,false);Point(v.MapButton.transform,true);Require(!v.IsMapOpen,"Fresh press after recovery failed");
        // A press waiting for aim cannot cross an open/close or other-hand UI transition.
        h.ToggleHandEntry();h.ToggleHandEntry(); // FRONT backup
        Point(miss.transform,false);Point(miss.transform,true);v.CloseButton.onClick.Invoke();h.ProcessInput();
        Point(h.Compact.OpenButton.transform,true);Require(!h.IsCardOpen,"Pending press crossed HUD hide and reopened it");
        Point(h.Compact.OpenButton.transform,false);Point(h.Compact.OpenButton.transform,true);Require(h.IsCardOpen,"Fresh backup press failed");
        v.CloseButton.onClick.Invoke();Point(miss.transform,false);Point(miss.transform,true);h.OpenFromHand();Point(v.MapButton.transform,true);
        Require(!v.IsMapOpen,"Pending press clicked newly shown HUD");
        h.ResetViewOptions();
        // Hover alone must not freeze follow; holding an actual button still stabilizes rotation.
        Point(v.MapButton.transform,false);Require(!h.FollowPaused,"Hover froze soft follow");
        Point(v.MapButton.transform,true);Require(h.FollowPaused,"Active press not stabilized");
        var before=v.transform.position;var delta=new Vector3(.6f,0,.25f);c.transform.position+=delta;h.Follow.Tick(.02f,h.FollowPaused);
        Require(Vector3.Distance(v.transform.position,before+delta)<.002f,"Walking leaves HUD behind while pressing");
        h.ToggleHandEntry();h.HandEntry.Sample(true,c.transform.position+new Vector3(-.2f,-.1f,.5f),Quaternion.Euler(-90,0,0),false,Vector3.zero,.02f);
        Point(h.HandEntry.SkyShortcut.transform,false);Point(h.HandEntry.SkyShortcut.transform,true);Require(!h.FollowPaused,"Independent sky shortcut paused HUD");
        var eye=new GameObject("FollowEdgeEye");var panel=new GameObject("FollowEdgePanel");var follow=panel.AddComponent<HudSoftFollow>();follow.Initialize(eye.transform);follow.SnapToView();
        var first=panel.transform.position;for(int i=0;i<20;i++){eye.transform.rotation=Quaternion.Euler(0,i%2==0?19:17,0);follow.Tick(.02f,false);}
        Require(Vector3.Distance(first,panel.transform.position)>.01f,"Threshold jitter restarts follow delay");
        follow.SnapToView();first=panel.transform.position;eye.transform.rotation=Quaternion.Euler(0,45,0);for(int i=0;i<70;i++)follow.Tick(.02f,true);
        Require(Vector3.Distance(first,panel.transform.position)>.01f,"Long button hold freezes forever");
        follow.SetMode(HudSoftFollow.FollowMode.Fixed);first=panel.transform.position;eye.transform.position+=Vector3.right*2;follow.SetMode(HudSoftFollow.FollowMode.Soft);follow.Tick(.02f,false);
        Require(Vector3.Distance(first,panel.transform.position)<.01f,"Fixed-to-soft carries stale walking delta");
        UnityEngine.Object.DestroyImmediate(panel);UnityEngine.Object.DestroyImmediate(eye);
        UnityEngine.Object.DestroyImmediate(miss);h.ResetViewOptions();v.ShowMap(false);v.CloseButton.onClick.Invoke();h.HandEntry.enabled=true;
        Debug.Log("[AeroPulse Response015] PASS 30 same-frame native-ray clicks, early-edge pose arrival, no held repeat/recovery click, hover follows, translation while pressing");
    }
}
