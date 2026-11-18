using System;
using UnityEngine;
using UnityEngine.XR.Hands;

public static class AeroHandBackChecks
{
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void RunGate()
    {
        var gate=new HandBackTouchGate();
        Require(!gate.Step(true,true,.04f,.02f),"Front hover toggled");
        Require(!gate.Step(true,false,-.04f,.02f),"Outside backside sample toggled");
        Require(!gate.Step(true,true,-.02f,.02f),"Backside re-entry reused the previous front arm");
        Require(!gate.Step(true,true,.002f,.02f),"Back-to-front re-entry toggled");
        Require(!gate.Step(true,true,.04f,.02f)&&gate.Step(true,true,.002f,.02f),"Fresh front crossing after re-entry failed");
        Require(!gate.Step(true,true,.002f,.02f),"Held contact repeated");
        gate=default;
        Require(!gate.Step(true,false,.04f,.02f)&&!gate.Step(true,true,.002f,.02f),"Outside front hover armed a side entry");
        gate.Step(true,true,.04f,.02f);gate.Step(true,false,.01f,.02f);
        Require(!gate.Step(true,true,.002f,.02f),"Leaving bounds retained a pending press");
        gate.Step(true,true,.04f,.02f);gate.Step(false,true,.01f,.02f);
        Require(!gate.Step(true,true,.002f,.02f),"Tracking recovery retained a pending press");
        gate.Step(true,true,.04f,.02f);gate.Step(true,true,-.04f,.02f);
        Require(!gate.Step(true,true,-.02f,.02f),"Back-limit re-entry retained a pending press");
        gate.Step(true,true,.04f,.02f);
        Require(gate.Step(true,true,-.02f,.02f),"Valid fast front crossing was rejected");
        Debug.Log("[AeroPulse HandBack018] PASS front crossing, side/back invalidation, release and tracking recovery");
    }
    public static void Run()
    {
        RunGate();
        var hud = UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();
        AeroHandRayChecks.Run();
        AeroHandRayChecks.RunMap();
        var entry = hud.HandEntry;
        // Disable the live reader while injecting samples into the same processing method.
        entry.enabled = false;
        var eye = Camera.main;
        eye.transform.SetPositionAndRotation(new Vector3(0,1.6f,0), Quaternion.identity);
        var root = Quaternion.Euler(-90,0,0); // Back +Y faces the eye (-Z).
        var palm = new Vector3(-0.15f,1.3f,0.45f);
        void Sample(Vector3 tip, bool left = true, bool right = true)
            => entry.Sample(left, palm, root, right, tip, 0.02f);
        Sample(Vector3.zero, true, false);
        Require(entry.Visible, "Hand-back tile missing");
        Require(GameObject.Find("Hotspot-SPORTS-HUD") == null, "Old orb still visible");
        Vector3 center = entry.Surface.position, normal = root * Vector3.up;
        Vector3 contactAnchor = entry.ContactSurface.position;
        Vector3 far = center + normal * 0.05f, near = center + normal * 0.002f;
        for (int i=0;i<6;i++) Sample(near);
        Require(!hud.IsCardOpen, "Initial overlapping finger auto-opened");
        Sample(far);
        for (int i=0;i<4;i++) Sample(center - normal * 0.04f);
        Require(!hud.IsCardOpen, "Backside contact opened");
        Sample(far);
        for (int i=0;i<4;i++) Sample(near + Vector3.right * 0.1f);
        Require(!hud.IsCardOpen, "Outside tile opened");
        Sample(far);
        for(int i=0;i<4;i++) Sample(near);
        Require(hud.IsCardOpen && entry.Visible, "Fresh touch failed or toggle tile hidden");
        for(int i=0;i<6;i++) Sample(near);
        Require(hud.IsCardOpen,"Held contact closed newly opened HUD");
        Sample(far);for(int i=0;i<4;i++) Sample(near);
        Require(!hud.IsCardOpen && entry.Visible,"Second touch cannot hide HUD");
        for(int i=0;i<6;i++) Sample(near);
        Require(!hud.IsCardOpen,"Held contact reopened after toggle");
        Sample(far);for(int i=0;i<4;i++) Sample(near);
        Require(hud.IsCardOpen,"Third fresh touch cannot reopen HUD");
        var view = GameObject.Find("PinchHudCard").GetComponent<AeroHudView>();
        view.ShowMap(true); view.Route.Advance();
        if(hud.IsCardOpen)hud.ToggleFromHand();
        for(int i=0;i<6;i++) Sample(near);
        Require(!hud.IsCardOpen, "Held contact reopened after HIDE");
        Sample(far); Sample(near, false);
        Require(!entry.Visible, "Lost left tracking did not hide tile");
        for(int i=0;i<6;i++) Sample(near);
        Require(!hud.IsCardOpen, "Tracking recovery contact auto-opened");
        Sample(far);
        Sample(near, true, false);
        for(int i=0;i<6;i++) Sample(near);
        Require(!hud.IsCardOpen, "Lost right tracking remained armed");
        Sample(far); for(int i=0;i<4;i++) Sample(near);
        Require(hud.IsCardOpen && view.IsMapOpen, "Map state lost on hand reopen");
        if(hud.IsCardOpen)hud.ToggleFromHand();
        entry.Sample(true,palm,Quaternion.Euler(90,0,0),true,far,0.02f);
        Require(!entry.Visible, "Palm-side tile visible");
        entry.SendMessage("OnApplicationFocus",false); Sample(far);
        Require(!entry.Visible, "Unfocused tile visible");
        entry.SendMessage("OnApplicationFocus",true);
        // Exercise the production tracking-space conversion, not only world-space samples.
        var space = eye.transform.parent;
        var localPalm = space.InverseTransformPoint(palm);
        var localRoot = Quaternion.Inverse(space.rotation) * root;
        entry.SampleTracking(true, localPalm, localRoot, false, Vector3.zero, space, 0.02f);
        Require(Vector3.Distance(entry.ContactSurface.position, contactAnchor)<0.001f, "Tracking translation wrong");
        Require(Quaternion.Angle(entry.Surface.rotation, Quaternion.LookRotation(-normal,root*Vector3.forward))<0.01f,
                "Tracking rotation wrong");
        // A held pinch may not click HIDE in the same frame as hand-touch opening.
        Vector3 origin = new Vector3(0.25f,1.3f,0.2f);
        Quaternion aim = Quaternion.LookRotation(view.CloseButton.transform.position-origin);
        void RightAim(bool pressed) => AeroHandRayChecks.Frame(MetaAimHand.right,origin,aim,pressed,space);
        RightAim(false); hud.ProcessInput();
        Sample(far);
        RightAim(true);
        for(int i=0;i<4;i++) Sample(near);
        hud.ProcessInput();
        Require(hud.IsCardOpen,"Hand opening also clicked HIDE on the same pinch");
        RightAim(false); hud.ProcessInput();
        RightAim(true); hud.ProcessInput();
        Require(!hud.IsCardOpen,"Fresh pinch cannot click HIDE after hand opening");
        RightAim(false);
        entry.enabled = true;
        Debug.Log("[AeroPulse HandBack] PASS: HUD/map regression, back-only tile, front-side fresh touch, hold/release, lost tracking/recovery, focus and state persistence");
    }
}
