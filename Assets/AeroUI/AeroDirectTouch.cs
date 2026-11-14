using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Direct index-tip input for the existing world-space controls. No pinch is required.
// Samples and geometry are world-space; the owner suppresses that hand's ray while Near.
public sealed class AeroDirectTouch : MonoBehaviour
{
    const float ArmDistance = 0.015f;
    const float PressDistance = 0.003f;
    const float ReleaseDistance = 0.018f;
    const float NearDistance = 0.08f;
    const float BackLimit = -0.03f;
    const float MinimumFingerAdvance = 0.003f;
    const float EdgeMargin = 0.003f;

    public bool NearLeft { get; private set; }
    public bool NearRight { get; private set; }
    public bool AnyNear => NearLeft || NearRight;
    public Button HoverLeft { get; private set; }
    public Button HoverRight { get; private set; }
    public Button PressedButtonThisSample { get; private set; }
    public bool SliderStartedThisSample { get; private set; }
    public Transform IgnoreLeftRoot { get; set; }
    public Transform IgnoreRightRoot { get; set; }
    public AeroValueSlider LeftSlider => left.Slider;
    public AeroValueSlider RightSlider => right.Slider;

    sealed class Target
    {
        public Button Button;
        public RectTransform Rect;
        public Canvas Canvas;
        public Collider Collider;
        public AeroValueSlider Slider;
        public bool HasPreviousPose;
        public Vector3 PreviousOrigin, PreviousNormal;
        public Matrix4x4 PreviousInverse;
    }
    sealed class Finger
    {
        public readonly string Id;
        public bool HasPrevious, Locked;
        public Vector3 Previous, ArmPoint, ReleasePoint, ReleaseNormal;
        public Target Armed;
        public AeroValueSlider Slider;
        public Target SliderTarget;
        public Vector3 SliderOrigin, SliderNormal;
        public Finger(string id) { Id=id; }
    }
    readonly List<Target> targets = new List<Target>(64);
    readonly Finger left = new Finger("poke-left"), right = new Finger("poke-right");
    PinchHudExperiment owner;
    Button pendingButton;
    bool resetting;

    public void Initialize(PinchHudExperiment hud) { owner=hud; }

    public void RebuildTargets(IEnumerable<Button> buttons)
    {
        ResetGates();targets.Clear();
        if(buttons==null)return;
        var seen=new HashSet<Button>();
        foreach(var button in buttons)
        {
            if(button==null||!seen.Add(button))continue;
            var rect=button.transform as RectTransform;if(rect==null)continue;
            var slider=button.GetComponentInParent<AeroValueSlider>(true);
            if(slider!=null&&slider.Button!=button)slider=null;
            targets.Add(new Target {Button=button,Rect=rect,Canvas=button.GetComponentInParent<Canvas>(true),
                Collider=button.GetComponent<Collider>(),Slider=slider});
        }
    }

    public void ResetGates()
    {
        if(resetting)return;
        resetting=true;
        try
        {
            Reset(left);Reset(right);pendingButton=null;
            foreach(var target in targets)target.HasPreviousPose=false;
            NearLeft=NearRight=false;HoverLeft=HoverRight=null;
        }
        finally { resetting=false; }
    }
    void Reset(Finger finger)
    {
        CancelSlider(finger);
        finger.HasPrevious=false;finger.Armed=null;
        // A consumed touch keeps its world-space release plane even if the UI changes.
        // A new/recovered overlapping sample cannot arm another button.
    }

    public void Sample(bool leftValid,Vector3 leftTip,bool rightValid,Vector3 rightTip,float dt)
    {
        pendingButton=null;PressedButtonThisSample=null;SliderStartedThisSample=false;
        Step(left,leftValid,leftTip,IgnoreLeftRoot,out bool nearL,out Button hoverL);
        Step(right,rightValid,rightTip,IgnoreRightRoot,out bool nearR,out Button hoverR);
        NearLeft=nearL;NearRight=nearR;HoverLeft=hoverL;HoverRight=hoverR;
        // Both fingers must compare against the same previous target frame. Cache once,
        // before callbacks can hide/move controls; ResetGates invalidates this on a UI change.
        CacheTargetPoses();
        if(pendingButton!=null&&owner!=null)
        {
            var button=pendingButton;pendingButton=null;PressedButtonThisSample=button;
            // Dispatch after both samples. The callback may hide/reparent controls and reset gates.
            owner.ActivateButton(button);
            ResetGates();NearLeft=nearL;NearRight=nearR;
            PressedButtonThisSample=button;
        }
    }

    void Step(Finger finger,bool valid,Vector3 tip,Transform ignore,out bool near,out Button hover)
    {
        near=false;hover=null;
        if(!valid||!Finite(tip))
        {
            CancelSlider(finger);finger.HasPrevious=false;finger.Armed=null;finger.Locked=false;
            return;
        }
        if(finger.Slider!=null)
        {
            var target=finger.SliderTarget;float depth=Vector3.Dot(tip-finger.SliderOrigin,finger.SliderNormal);
            bool owns=finger.Slider.Interacting&&finger.Slider.InteractionOwner==finger.Id;
            if(!owns||!Usable(target))
                CancelSlider(finger);
            else if(depth>=ReleaseDistance)
                EndSlider(finger);
            else if(depth<BackLimit||!Inside(target,tip,0.05f,0.012f))
                CancelSlider(finger);
            else
            {
                near=true;hover=target.Button;
                finger.Slider.SetFromWorldPoint(tip);
                finger.Previous=tip;finger.HasPrevious=true;return;
            }
        }
        var candidate=Find(tip,ignore,out float currentDepth);
        near=candidate!=null;hover=candidate!=null?candidate.Button:null;
        if(finger.Locked)
        {
            if(Vector3.Dot(tip-finger.ReleasePoint,finger.ReleaseNormal)>=ReleaseDistance)
                finger.Locked=false;
            else { finger.Previous=tip;finger.HasPrevious=true;return; }
        }
        if(candidate==null)
        {
            finger.Armed=null;finger.Previous=tip;finger.HasPrevious=true;return;
        }
        Vector3 origin=Origin(candidate),normal=-candidate.Rect.forward;
        bool history=finger.HasPrevious&&candidate.HasPreviousPose;
        float previousDepth=history?Vector3.Dot(finger.Previous-candidate.PreviousOrigin,candidate.PreviousNormal):currentDepth;
        bool crossing=history&&previousDepth>PressDistance&&currentDepth<=PressDistance;
        // Permit a fast press that jumped over the near-hover band, but require a clear
        // front-side previous sample and a crossing point inside this same target.
        if(finger.Armed!=candidate&&crossing&&previousDepth>=ArmDistance)
        {
            finger.Armed=candidate;finger.ArmPoint=finger.Previous;
        }
        if(currentDepth>=ArmDistance)
        {
            if(finger.Armed!=candidate){finger.Armed=candidate;finger.ArmPoint=tip;}
        }
        if(crossing&&finger.Armed==candidate&&currentDepth>=BackLimit&&
           Vector3.Dot(finger.ArmPoint-tip,normal)>=MinimumFingerAdvance&&
           Vector3.Distance(finger.Previous,tip)<0.3f)
        {
            float fraction=(previousDepth-PressDistance)/(previousDepth-currentDepth);
            Vector3 previousLocal=candidate.PreviousInverse.MultiplyPoint3x4(finger.Previous);
            Vector3 currentLocal=candidate.Rect.InverseTransformPoint(tip);
            Vector3 crossingLocal=Vector3.Lerp(previousLocal,currentLocal,Mathf.Clamp01(fraction));
            if(InsideLocal(candidate,crossingLocal,EdgeMargin,EdgeMargin)&&pendingButton==null)
            {
                // A control moving into a stationary finger has no absolute fingertip
                // advance and cannot pass the above condition.
                finger.Armed=null;finger.Locked=true;finger.ReleasePoint=origin;finger.ReleaseNormal=normal;
                if(candidate.Slider!=null)
                {
                    if(candidate.Slider.BeginInteraction(finger.Id))
                    {
                        finger.Slider=candidate.Slider;finger.SliderTarget=candidate;
                        finger.SliderOrigin=origin;finger.SliderNormal=normal;
                        candidate.Slider.SetFromWorldPoint(tip);
                        SliderStartedThisSample=true;PressedButtonThisSample=candidate.Button;
                        if(owner!=null&&owner.Feedback!=null)owner.Feedback.Confirm(candidate.Button);
                    }
                }
                else if(left.Slider==null&&right.Slider==null)
                    pendingButton=candidate.Button;
            }
        }
        finger.Previous=tip;finger.HasPrevious=true;
    }

    void EndSlider(Finger finger)
    {
        var slider=finger.Slider;finger.Slider=null;finger.SliderTarget=null;
        // Clear first: the end callback may update layout and call ResetGates again.
        if(slider!=null&&slider.Interacting&&slider.InteractionOwner==finger.Id)slider.EndInteraction(finger.Id);
    }
    void CancelSlider(Finger finger)
    {
        var slider=finger.Slider;finger.Slider=null;finger.SliderTarget=null;
        // Tracking, focus and context loss cancel preview instead of committing it.
        if(slider!=null&&slider.Interacting&&slider.InteractionOwner==finger.Id)slider.CancelInteraction();
    }
    void CacheTargetPoses()
    {
        foreach(var target in targets)
        {
            target.HasPreviousPose=Usable(target);
            if(!target.HasPreviousPose)continue;
            target.PreviousOrigin=Origin(target);target.PreviousNormal=-target.Rect.forward;
            target.PreviousInverse=target.Rect.worldToLocalMatrix;
        }
    }
    Target Find(Vector3 tip,Transform ignore,out float selectedDepth)
    {
        Target selected=null;float best=float.PositiveInfinity;selectedDepth=0;
        foreach(var target in targets)
        {
            if(!Usable(target)||(ignore!=null&&target.Rect.IsChildOf(ignore)))continue;
            float depth=Vector3.Dot(tip-Origin(target),-target.Rect.forward);
            if(depth>NearDistance||depth<BackLimit||!Inside(target,tip,EdgeMargin,EdgeMargin))continue;
            float score=Mathf.Abs(depth);
            if(score<best){selected=target;best=score;selectedDepth=depth;}
        }
        return selected;
    }
    static bool Usable(Target t) => t!=null&&t.Button!=null&&t.Button.isActiveAndEnabled&&t.Button.IsInteractable()&&
        (t.Canvas==null||t.Canvas.enabled)&&(t.Collider==null||t.Collider.enabled);
    static Vector3 Origin(Target t) => t.Rect.TransformPoint(t.Rect.rect.center);
    static bool Inside(Target t,Vector3 point,float marginX,float marginY)
        =>InsideLocal(t,t.Rect.InverseTransformPoint(point),marginX,marginY);
    static bool InsideLocal(Target t,Vector3 p,float marginX,float marginY)
    {
        Rect r=t.Rect.rect;
        Vector3 scale=t.Rect.lossyScale;
        float mx=marginX/Mathf.Max(Mathf.Abs(scale.x),0.00001f),my=marginY/Mathf.Max(Mathf.Abs(scale.y),0.00001f);
        return p.x>=r.xMin-mx&&p.x<=r.xMax+mx&&p.y>=r.yMin-my&&p.y<=r.yMax+my;
    }
    static bool Finite(Vector3 p) => !float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&
        !float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
    void OnDisable(){ResetGates();}
}
