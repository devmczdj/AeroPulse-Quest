using System;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Hands;
using UnityEngine.UI;

public static class AeroLayout018Checks
{
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static bool Near(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<.001f;
    public static void Run()
    {
        Require(Application.isPlaying,"Play mode required");
        var h=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var v=h.Follow.GetComponent<AeroHudView>();var l=h.Layout;var c=Camera.main;var card=v.transform;
        c.GetComponent<TrackedPoseDriver>().enabled=false;c.transform.parent.SetPositionAndRotation(Vector3.zero,Quaternion.identity);c.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
        h.SendMessage("OnApplicationFocus",true);h.HandEntry.enabled=false;h.DirectInputForChecks=true;l.DeltaForChecks=.02f;
        if(MetaAimHand.left==null)MetaAimHand.left=MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Left);
        if(MetaAimHand.right==null)MetaAimHand.right=MetaAimHand.CreateHand(UnityEngine.XR.InputDeviceCharacteristics.Right);
        void Off(){AeroHandRayChecks.Set(MetaAimHand.left,false,false,Vector3.zero,Quaternion.identity);AeroHandRayChecks.Set(MetaAimHand.right,false,false,Vector3.zero,Quaternion.identity);h.DirectTouch.Sample(false,Vector3.zero,false,Vector3.zero,.02f);h.ClearPresses();}
        void Point(Vector3 point,bool press){var origin=c.transform.position+new Vector3(.25f,-.25f,.15f);AeroHandRayChecks.Frame(MetaAimHand.right,origin,Quaternion.LookRotation(point-origin),press,c.transform.parent);h.ProcessInput();}
        void Click(Button b){var p=b.transform.position;Point(p,false);Point(p,true);Off();}
        Vector3 palm=c.transform.position+new Vector3(-.15f,-.25f,.45f);var rotation=Quaternion.Euler(-90,0,0);
        void Track(bool valid=true){h.HandEntry.Sample(valid,palm,rotation,false,Vector3.zero,.02f);l.PrepareFrame();}
        void Move(AeroDragHandle handle,Vector3 delta)
        {var origin=handle.transform.position-handle.transform.forward*.15f;var ray=new Ray(origin,handle.transform.forward);l.StartDrag("right",handle,ray,.15f);for(int i=0;i<9;i++)l.StepDrag(true,true,new Ray(origin+delta,ray.direction),null,.02f);l.StepDrag(true,false,new Ray(origin+delta,ray.direction),null,.02f);}
        try
        {
            Off();h.ResetViewOptions();h.OpenDataFromShortcut();Track();bool sky=h.Sky.Visible;
            // Actual production rays navigate into edit and operate the icon buttons.
            Click(v.SettingsButton);Click(v.SettingsView.TabButtons[2]);Click(v.SettingsView.EditCardButton);
            Require(l.Editing&&l.CardHandle.gameObject.activeInHierarchy&&!l.WorldZone.gameObject.activeInHierarchy,"Edit entry or obsolete zones incorrect");
            var position=card.position;Click(l.PinButton);Require(l.MainAnchor==AeroWindowAnchor.World&&Near(position,card.position),"Pin changes pose or fails");
            Click(l.PinButton);Require(l.MainAnchor==AeroWindowAnchor.Follow&&Near(position,card.position),"Unpin jumps");
            Click(l.SpeedButton);Require(h.Follow.Mode==HudSoftFollow.FollowMode.Quick,"Quick follow icon failed");
            Click(l.PinButton);Click(l.PinButton);Require(h.Follow.Mode==HudSoftFollow.FollowMode.Quick,"Pin erased speed preference");Click(l.SpeedButton);
            // Production pinch captures the top handle, and a held ray does not snap.
            var target=l.CardHandle.transform.position;Point(target,false);Point(target,true);Require(l.Dragging&&l.DragOwner=="right","Native ray did not grab handle");
            for(int i=0;i<10;i++)Point(target,true);Point(target,false);Require(!l.Dragging&&Near(position,card.position),"Stationary native grab jumped");Off();
            position=card.position;Move(l.CardHandle,Vector3.zero);Require(Near(position,card.position),"15cm grab jumps by distance clamp");
            Move(l.CardHandle,new Vector3(.12f,.05f,0));Require(l.MainAnchor==AeroWindowAnchor.Follow&&Near(card.position,position+new Vector3(.12f,.05f,0)),"Free drag loses placement/offset");
            var offset=h.Follow.Offset;l.EndEdit();c.transform.rotation=Quaternion.Euler(0,45,0);
            for(int i=0;i<180;i++)l.Tick(.02f,false);
            Require(Near(card.position,c.transform.position+c.transform.rotation*offset.position),"Soft follow recentres or fails to preserve custom offset");
            position=card.position;v.CloseButton.onClick.Invoke();h.OpenFromHand();Require(Near(position,card.position),"Reopen erased offset");
            v.ShowMap(true);v.ShowMap(false);Require(Near(position,card.position),"Content switch moves panel");
            card.position=c.transform.position+Vector3.left*2;h.Follow.CaptureOffset();h.ProcessInput();
            Require(h.Compact.FallbackVisible,"Offscreen custom follow has no recovery");Click(h.Compact.OpenButton);
            Require(Vector3.Angle(c.transform.forward,card.position-c.transform.position)<1,"Custom follow recall failed");
            h.SetFollowMode(HudSoftFollow.FollowMode.Quick);h.Recenter();Require(h.Follow.Mode==HudSoftFollow.FollowMode.Quick,"Recall erased follow speed");h.SetFollowMode(HudSoftFollow.FollowMode.Soft);
            c.transform.rotation=Quaternion.identity;h.Recenter();l.BeginEdit(false);Track();
            // Resize about top-left; ratio is independent of distance and placement.
            var rect=(RectTransform)card;var pivot=card.TransformPoint(new Vector3(rect.rect.xMin,rect.rect.yMax,0));
            var handlePoint=l.ResizeHandle.transform.position;var ray=new Ray(c.transform.position,(handlePoint-c.transform.position).normalized);float distance=Vector3.Distance(c.transform.position,handlePoint);
            var originalScale=card.localScale;l.StartDrag("right",l.ResizeHandle,ray,distance);
            var enlarged=pivot+(handlePoint-pivot)*1.3f;var bigger=new Ray(c.transform.position,(enlarged-c.transform.position).normalized);
            for(int i=0;i<9;i++)l.StepDrag(true,true,bigger,null,.02f);
            Require(Mathf.Abs(h.Follow.UserScale-1.3f)<.002f&&Near(pivot,card.TransformPoint(new Vector3(rect.rect.xMin,rect.rect.yMax,0))),"Resize ratio or fixed opposite corner failed");
            l.StepDrag(true,false,bigger,null,.02f);Require(l.MainAnchor==AeroWindowAnchor.Follow,"Resize changed placement");l.Undo();Require(Near(card.localScale,originalScale)&&Mathf.Abs(h.Follow.UserScale-1)<.001f,"Resize undo incomplete");
            var sizeBefore=h.Follow.UserScale;var poseBefore=card.position;
            handlePoint=l.ResizeHandle.transform.position;ray=new Ray(c.transform.position,(handlePoint-c.transform.position).normalized);distance=Vector3.Distance(c.transform.position,handlePoint);
            l.StartDrag("right",l.ResizeHandle,ray,distance);var beyond=pivot+(handlePoint-pivot)*4;var hugeRay=new Ray(c.transform.position,(beyond-c.transform.position).normalized);
            for(int i=0;i<9;i++)l.StepDrag(true,true,hugeRay,null,.02f);
            Require(Mathf.Approximately(h.Follow.UserScale,HudSoftFollow.MaxScale),"Resize max does not clamp");
            l.StepDrag(false,true,hugeRay,null,.02f);Require(Near(card.position,poseBefore)&&Mathf.Approximately(h.Follow.UserScale,sizeBefore),"Lost resize committed preview");
            // Stale undo cannot bring old scale back after a distance edit.
            Move(l.CardHandle,new Vector3(.04f,0,0));l.EndEdit();h.SetPanelDistance(2.2f);var scale=card.localScale;l.BeginEdit(false);l.Undo();Require(Near(scale,card.localScale)&&Mathf.Abs(h.Follow.Distance-2.2f)<.001f,"Distance change leaves stale undo");l.EndEdit();h.SetPanelDistance(.65f);
            l.BeginEdit(false);Track();Click(l.HandButton);Require(l.MainAnchor==AeroWindowAnchor.Hand,"Hand icon did not select hand placement");
            position=card.position;Move(l.CardHandle,new Vector3(.03f,.02f,0));var custom=card.position;Require(l.MainAnchor==AeroWindowAnchor.Hand,"Hand drag changed category");palm+=Vector3.right*.04f;Track();Require(Near(custom+Vector3.right*.04f,card.position),"Hand custom offset not retained");
            // Slider capture freezes attached geometry, loss cancels as before.
            l.EndEdit();v.ToggleSettings();v.SettingsView.SelectTab(AeroHudSettingsView.SettingsTab.Input);var slider=v.SettingsView.HeightSlider;
            var centre=slider.Button.transform.position;var normal=-slider.Button.transform.forward;
            h.DirectTouch.Sample(false,Vector3.zero,true,centre+normal*.03f,.02f);h.DirectTouch.Sample(false,Vector3.zero,true,centre+normal*.001f,.02f);
            Require(slider.Interacting,"Hand slider capture failed");position=card.position;palm+=new Vector3(.03f,.02f,0);Track();Require(Near(position,card.position),"Attached panel moves during slider capture");
            h.DirectTouch.Sample(false,Vector3.zero,false,Vector3.zero,.02f);Require(!slider.Interacting,"Lost slider remained captured");Off();
            if(v.IsSettingsOpen)v.ToggleSettings();l.BeginEdit(false);Click(l.HandButton);Track(false);h.ProcessInput();
            Require(h.Compact.FallbackVisible,"Lost hand editor has no reachable recovery");Click(h.Compact.OpenButton);Require(!l.Editing&&l.MainAnchor==AeroWindowAnchor.Follow&&card.GetComponent<Canvas>().enabled,"Recovery cannot exit lost hand editing");Track();
            l.BeginEdit(false);Click(l.ViewButton);position=card.position;Move(l.CardHandle,new Vector3(.05f,0,0));Require(l.MainAnchor==AeroWindowAnchor.View,"View drag changed category");var local=c.transform.InverseTransformPoint(card.position);c.transform.rotation=Quaternion.Euler(0,15,0);l.PrepareFrame();Require(Near(card.position,c.transform.TransformPoint(local)),"View local offset incorrect");
            // Compact has independent placement, scale, and undo; hidden selection never opens it.
            Require(!l.SelectCompactButton.IsInteractable(),"Hidden compact is implicitly editable");l.SelectPanel(true);Require(!l.SelectedCompact,"Selecting hidden compact opens it");
            h.SetCompactPlacement(AeroCompactHud.Placement.Left);l.PrepareFrame();Click(l.SelectCompactButton);Require(l.SelectedCompact,"Compact selection failed");var mainPosition=card.position;var small=h.Compact.transform;var smallBefore=small.position;Move(l.CompactHandle,new Vector3(.03f,0,0));Require(Near(mainPosition,card.position)&&!Near(smallBefore,small.position)&&l.CompactAnchor==AeroWindowAnchor.View,"Compact movement affects main or loses anchor");Click(l.UndoButton);Require(Near(smallBefore,small.position),"Compact undo ray blocked by main panel");
            l.StartDrag("right",l.CompactHandle,new Ray(small.position-Vector3.forward*.15f,Vector3.forward),.15f);for(int i=0;i<9;i++)l.StepDrag(true,true,new Ray(small.position-Vector3.forward*.1f,Vector3.forward),null,.02f);l.StepDrag(false,true,default,null,.02f);Require(!l.Dragging&&Near(smallBefore,small.position),"Lost manipulation did not restore");
            Click(l.DoneButton);Require(!l.Editing,"Compact done ray blocked by main panel");Require(h.Sky.Visible==sky&&l.SkyHandle==null,"Layout changed independent sky");
            Debug.Log("[AeroPulse Layout018] PASS icon navigation/native grab; pin continuity; offset follow/reopen; hand/view independent placement; proportional resize/pivot/undo; stale undo; attached slider freeze; lost hand recall; compact isolation; sky independence");
        }
        finally{Off();if(l.Editing)l.EndEdit();h.DirectInputForChecks=false;h.HandEntry.enabled=true;c.transform.rotation=Quaternion.identity;h.ResetViewOptions();v.ShowMap(false);v.CloseButton.onClick.Invoke();l.DeltaForChecks=0;}
    }
}
