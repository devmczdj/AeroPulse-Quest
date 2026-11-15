using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.Hands;

// Editor injection drives the same index-tip processing and native ray paths as the player.
// This is not physical Quest latency, hand-occlusion or comfort evidence.
public static class AeroTouchChecks
{
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static Vector3 Center(Button b)=>b.transform.TransformPoint(((RectTransform)b.transform).rect.center);
    static Vector3 Front(Button b,float depth)=>Center(b)-b.transform.forward*depth;
    static Vector3 SliderPoint(AeroValueSlider s,float fraction,float depth=0)
    {
        float width=((RectTransform)s.Button.transform).rect.width-20;
        return s.Button.transform.TransformPoint(new Vector3((fraction-.5f)*width,0,0))-s.Button.transform.forward*depth;
    }
    public static void Run()
    {
        Require(Application.isPlaying,"Enter Play Mode first");
        var h=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var c=Camera.main;
        Require(h!=null&&h.DirectTouch!=null,"Direct input missing");
        var v=h.Follow.GetComponent<AeroHudView>();var s=v.SettingsView;var touch=h.DirectTouch;
        c.GetComponent<TrackedPoseDriver>().enabled=false;
        c.transform.parent.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        c.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
        h.DirectInputForChecks=true;h.HandEntry.enabled=false;h.SendMessage("OnApplicationFocus",true);
        if(MetaAimHand.left==null)MetaAimHand.left=MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Left);
        if(MetaAimHand.right==null)MetaAimHand.right=MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
        void NativeOff()
        {AeroHandRayChecks.Set(MetaAimHand.left,false,false,Vector3.zero,Quaternion.identity);AeroHandRayChecks.Set(MetaAimHand.right,false,false,Vector3.zero,Quaternion.identity);}
        void Frame(bool left,Vector3 lp,bool right,Vector3 rp)
        {touch.Sample(left,lp,right,rp,.02f);h.ProcessInput();}
        void Finger(bool left,Vector3 point)
        {if(left)Frame(true,point,false,Vector3.zero);else Frame(false,Vector3.zero,true,point);}
        void Clear()
        {h.ClearPresses();Frame(false,Vector3.zero,false,Vector3.zero);}
        void Tap(Button b,bool left=false)
        {Finger(left,Front(b,.03f));Finger(left,Front(b,.001f));Finger(left,Front(b,.03f));}
        void Ray(Vector3 point,bool pressed,MetaAimHand hand)
        {
            touch.Sample(false,Vector3.zero,false,Vector3.zero,.02f);
            var origin=c.transform.position+new Vector3(hand==MetaAimHand.left?-.25f:.25f,-.25f,.15f);
            AeroHandRayChecks.Frame(hand,origin,Quaternion.LookRotation(point-origin),pressed,c.transform.parent);h.ProcessInput();
        }
        int clicks=0;UnityAction count=()=>clicks++;v.MapButton.onClick.AddListener(count);
        try
        {
            NativeOff();h.ResetViewOptions();h.OpenDataFromShortcut();Clear();
            foreach(bool left in new[]{false,true})
            {
                int start=clicks;Tap(v.MapButton,left);Require(clicks==start+1,"Fresh index press did not fire once");
                Finger(left,Front(v.MapButton,.001f));for(int i=0;i<8;i++)Finger(left,Front(v.MapButton,.001f));
                Require(clicks==start+2,"Fresh second press failed or held press repeated");
                Finger(left,Front(v.SettingsButton,.001f));Require(!v.IsSettingsOpen,"Sliding pressed finger onto another button clicked it");Clear();
            }
            int before=clicks;
            Finger(false,Front(v.MapButton,.001f));Finger(false,Front(v.MapButton,0));Require(clicks==before,"Initial overlapping finger clicked");
            Frame(false,Vector3.zero,false,Vector3.zero);Finger(false,Front(v.MapButton,.001f));Require(clicks==before,"Tracking recovery overlapping finger clicked");
            Finger(false,Front(v.MapButton,.03f));Finger(false,Front(v.MapButton,-.02f));Require(clicks==before+1,"Fast front-to-back crossing missed");Clear();
            before=clicks;Finger(false,Front(v.MapButton,-.02f));Finger(false,Front(v.MapButton,.002f));Require(clicks==before,"Backside approach clicked");Clear();

            Vector3 original=v.MapButton.transform.position;Vector3 stationary=Front(v.MapButton,.03f);
            Finger(false,stationary);v.MapButton.transform.position-=v.MapButton.transform.forward*.03f;
            Finger(false,stationary);Require(clicks==before,"UI movement into a stationary finger clicked");v.MapButton.transform.position=original;Clear();
            // Regression: earlier 5 mm of finger travel must not authorize a later
            // crossing caused entirely by the panel moving 24 mm into the held finger.
            Finger(false,Front(v.MapButton,.030f));stationary=Front(v.MapButton,.025f);Finger(false,stationary);
            v.MapButton.transform.position-=v.MapButton.transform.forward*.024f;Finger(false,stationary);
            Require(clicks==before,"Partially advanced stationary finger clicked when UI moved 24 mm");
            v.MapButton.transform.position=original;Clear();
            // A slow tap accumulates its travel; no individual frame reaches 3 mm.
            Finger(false,Front(v.MapButton,.030f));
            for(int i=1;i<=580;i++)Finger(false,Front(v.MapButton,.030f-i*.00005f));
            Require(clicks==before+1,"Slow 0.5 mm/frame tap was rejected");before=clicks;Clear();
            // A hand-docked panel and fingertip may translate together. Their relative
            // gap never crossed the plane, even if the old tip looks front of the new pose.
            Vector3 panelPosition=v.transform.position;Vector3 overlap=Front(v.MapButton,.001f);
            Finger(false,overlap);Vector3 together=v.MapButton.transform.forward*.03f;
            v.transform.position+=together;Finger(false,overlap+together);
            Require(clicks==before,"Rigid panel/finger translation synthesized a crossing");
            v.transform.position=panelPosition;Clear();
            // Relative front approach remains valid when the panel also moves sideways.
            var approach=Front(v.MapButton,.03f);Finger(false,approach);v.transform.position+=Vector3.right*.01f;
            Finger(false,Front(v.MapButton,.001f));Require(clicks==before+1,"Moving panel rejected genuine relative front press");
            v.transform.position=panelPosition;Clear();before=clicks;
            v.MapButton.interactable=false;Tap(v.MapButton);Require(clicks==before,"Disabled button accepts touch");v.MapButton.interactable=true;Clear();
            v.MapButton.gameObject.SetActive(false);Tap(v.MapButton);Require(clicks==before,"Hidden button accepts touch");v.MapButton.gameObject.SetActive(true);Clear();

            // Both hands crossing the same control in one sample still produce one activation.
            var front=Front(v.MapButton,.03f);var contact=Front(v.MapButton,.001f);
            Frame(true,front,true,front);Frame(true,contact,true,contact);Require(clicks==before+1,"Two hands double-activated the button");Clear();

            // A simultaneous native pinch cannot duplicate the near index press.
            before=clicks;var origin=c.transform.position+new Vector3(.25f,-.25f,.15f);var aim=Quaternion.LookRotation(Center(v.MapButton)-origin);
            AeroHandRayChecks.Frame(MetaAimHand.right,origin,aim,false,c.transform.parent);Finger(false,Front(v.MapButton,.03f));
            AeroHandRayChecks.Frame(MetaAimHand.right,origin,aim,true,c.transform.parent);Finger(false,Front(v.MapButton,.001f));
            Require(clicks==before+1&&touch.NearRight,"Near/ray priority produced a duplicate or missed poke");
            Finger(false,Front(v.MapButton,.2f));Require(clicks==before+1,"Leaving near mode with held pinch clicked again");
            Ray(Center(v.MapButton),false,MetaAimHand.right);Ray(Center(v.MapButton),true,MetaAimHand.right);
            Require(clicks==before+2,"Fresh far pinch failed after near mode");NativeOff();Clear();

            v.ShowMap(false);Tap(v.SettingsButton);Tap(s.TabButtons[1]);Require(v.IsSettingsOpen&&s.CurrentTab==AeroHudSettingsView.SettingsTab.Input,"Direct navigation failed");
            var height=s.HeightSlider;float initial=h.HandEntry.HeightOffset;
            Finger(false,SliderPoint(height,.5f,.03f));Finger(false,SliderPoint(height,.5f,.001f));
            Require(height.Interacting&&height.InteractionOwner=="poke-right","Direct slider did not capture");
            Finger(false,SliderPoint(height,.83f,.001f));Require(Mathf.Abs(height.Value-.042f)<.0002f&&Mathf.Approximately(h.HandEntry.HeightOffset,initial),"Direct slider preview/step or deferred commit failed");
            Finger(false,SliderPoint(height,.83f,.03f));Require(!height.Interacting&&Mathf.Abs(h.HandEntry.HeightOffset-.042f)<.0002f,"Direct slider release failed to commit");Clear();

            // Only one pointer may own a slider; a second finger cannot take over.
            initial=h.HandEntry.HeightOffset;
            Frame(false,Vector3.zero,true,SliderPoint(height,.6f,.03f));Frame(false,Vector3.zero,true,SliderPoint(height,.6f,.001f));
            Frame(true,SliderPoint(height,.1f,.03f),true,SliderPoint(height,.6f,.001f));
            Frame(true,SliderPoint(height,.1f,.001f),true,SliderPoint(height,.6f,.001f));
            Require(height.InteractionOwner=="poke-right"&&Mathf.Abs(height.Value-.037f)<.0002f,"Second hand stole or changed slider capture");
            Frame(false,Vector3.zero,false,Vector3.zero);
            Require(!height.Interacting&&Mathf.Approximately(height.Value,initial)&&Mathf.Approximately(h.HandEntry.HeightOffset,initial),"Tracking loss committed slider preview");Clear();

            // A left fingertip can also drag, including clamping past the end of the rail.
            Finger(true,SliderPoint(height,.5f,.03f));Finger(true,SliderPoint(height,.5f,.001f));
            Finger(true,SliderPoint(height,1.1f,.001f));Require(Mathf.Approximately(height.Value,.045f),"Direct slider endpoint did not clamp");
            Finger(true,SliderPoint(height,1.1f,.03f));Require(Mathf.Approximately(h.HandEntry.HeightOffset,.045f),"Left direct slider release failed");Clear();

            initial=h.HandEntry.HeightOffset;Finger(false,SliderPoint(height,.5f,.03f));Finger(false,SliderPoint(height,.2f,.001f));
            h.SendMessage("OnApplicationFocus",false);Require(!height.Interacting&&Mathf.Approximately(height.Value,initial)&&Mathf.Approximately(h.HandEntry.HeightOffset,initial),"Focus loss committed slider");h.SendMessage("OnApplicationFocus",true);Clear();
            Finger(false,SliderPoint(height,.5f,.03f));Finger(false,SliderPoint(height,.2f,.001f));s.SelectTab(AeroHudSettingsView.SettingsTab.Display);
            Require(!height.Interacting&&Mathf.Approximately(height.Value,initial)&&Mathf.Approximately(h.HandEntry.HeightOffset,initial),"Page change committed hidden slider");Clear();

            // The original far ray drives the same slider model and release contract.
            foreach(var hand in new[]{MetaAimHand.right,MetaAimHand.left})
            {
                NativeOff();s.SelectTab(AeroHudSettingsView.SettingsTab.Layout);h.SetPanelDistance(.65f);Clear();
                var distance=s.DistanceSlider;initial=h.Follow.Distance;
                Ray(SliderPoint(distance,.3f),false,hand);Ray(SliderPoint(distance,.3f),true,hand);
                Require(distance.Interacting&&distance.InteractionOwner==(hand==MetaAimHand.left?"ray-left":"ray-right"),"Ray slider did not capture");
                Ray(SliderPoint(distance,1.2f),true,hand);Require(Mathf.Approximately(distance.Value,2.2f)&&Mathf.Approximately(h.Follow.Distance,initial),"Ray slider clamp/deferred commit failed");
                Ray(SliderPoint(distance,1.2f),false,hand);Require(!distance.Interacting&&Mathf.Approximately(h.Follow.Distance,2.2f),"Ray slider release did not commit");
                h.SetPanelDistance(.65f);Clear();Ray(SliderPoint(distance,.2f),false,hand);Ray(SliderPoint(distance,.2f),true,hand);Ray(SliderPoint(distance,.7f),true,hand);
                AeroHandRayChecks.Set(hand,false,true,Vector3.zero,Quaternion.identity);h.ProcessInput();
                Require(!distance.Interacting&&Mathf.Approximately(distance.Value,.65f)&&Mathf.Approximately(h.Follow.Distance,.65f),"Lost ray committed its preview");
                NativeOff();Clear();
            }

            // The left-hand entry uses its visible surface, confirming an index tap without pinch.
            v.ShowMap(false);v.CloseButton.onClick.Invoke();h.ResetViewOptions();NativeOff();Clear();
            Vector3 palm=c.transform.position+new Vector3(-.15f,-.25f,.45f);Quaternion back=Quaternion.Euler(-90,0,0);Vector3 normal=back*Vector3.up;
            h.HandEntry.Sample(true,palm,back,false,Vector3.zero,.02f);var surface=h.HandEntry.Surface.position;
            h.HandEntry.Sample(true,palm,back,true,surface+normal*.04f,.02f);
            h.HandEntry.Sample(true,palm,back,true,surface+normal*.002f,.02f);
            Require(h.IsCardOpen,"Visible hand-back plane tap did not open without pinch");
            for(int i=0;i<8;i++)h.HandEntry.Sample(true,palm,back,true,surface+normal*.002f,.02f);
            Require(h.IsCardOpen,"Held hand-back contact repeated");
            h.HandEntry.Sample(true,palm,back,true,surface+normal*.04f,.02f);h.HandEntry.Sample(true,palm,back,true,surface+normal*.002f,.02f);
            Require(!h.IsCardOpen,"Fresh hand-back tap did not hide");
            // Relative hand-back contact permits movement by the left palm itself;
            // translating both hands with an unchanged gap must not count as a tap.
            h.HandEntry.Sample(true,palm,back,false,Vector3.zero,.02f);
            surface=h.HandEntry.Surface.position;var heldTip=surface+normal*.04f;
            h.HandEntry.Sample(true,palm,back,true,heldTip,.02f);
            var sharedMotion=normal*.02f+Vector3.right*.01f;palm+=sharedMotion;heldTip+=sharedMotion;
            h.HandEntry.Sample(true,palm,back,true,heldTip,.02f);
            Require(!h.IsCardOpen,"Rigid movement of both hands toggled hand-back entry");
            palm+=normal*.038f;h.HandEntry.Sample(true,palm,back,true,heldTip,.02f);
            Require(h.IsCardOpen,"Left palm approaching a stationary right fingertip failed to toggle");
            for(int i=0;i<8;i++)h.HandEntry.Sample(true,palm,back,true,heldTip,.02f);
            Require(h.IsCardOpen,"Held palm-driven contact repeated");
            AeroHandBackChecks.RunGate();
            Debug.Log("[AeroPulse Touch018] PASS both index fingers, fast/slow/front/back/overlap/recovery/moving-UI gates, hidden/disabled, near/ray arbitration, direct+ray sliders preview/release/cancel/owner lock/clamp, relative two-hand entry");
        }
        finally
        {
            v.MapButton.onClick.RemoveListener(count);v.MapButton.interactable=true;
            h.SendMessage("OnApplicationFocus",true);h.ClearPresses();NativeOff();touch.Sample(false,Vector3.zero,false,Vector3.zero,.02f);
            h.DirectInputForChecks=false;h.HandEntry.enabled=true;h.ResetViewOptions();h.ShowSkyMap();v.ShowMap(false);v.CloseButton.onClick.Invoke();
        }
    }
}
