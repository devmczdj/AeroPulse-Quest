using System;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

public static class AeroToolsOcclusion023Checks
{
    static void Require(bool v,string m){if(!v)throw new InvalidOperationException("ToolsOcclusion023: "+m);}
    public static void Run()
    {
        var h=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var l=h.Layout;var w=h.Widgets;var c=Camera.main;
        Require(Application.isPlaying,"Play Mode required");var old=l.CaptureLayout();var pose=new Pose(c.transform.position,c.transform.rotation);
        var driver=c.GetComponent<TrackedPoseDriver>();bool driven=driver.enabled,hand=h.HandEntry.enabled,tools=h.ToolsEntry.enabled,direct=h.DirectInputForChecks,keep=w.KeepOpen,dirty=h.Profiles.IsDirty;
        try
        {
            driver.enabled=false;h.HandEntry.enabled=false;h.ToolsEntry.enabled=false;h.DirectInputForChecks=true;h.SendMessage("OnApplicationFocus",true);
            c.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);if(l.Editing)l.EndEdit();h.ResetViewOptions();h.OpenDataFromShortcut();l.BeginEdit(false);w.SetKeepOpen(true);w.SetPage(false);
            var main=h.Follow.transform;main.SetPositionAndRotation(c.transform.TransformPoint(new Vector3(0,-.05f,.35f)),c.transform.rotation);h.Follow.CaptureOffset();l.PrepareFrame();Physics.SyncTransforms();
            var row=w.Items[0].EntryButton;var ray=new Ray(c.transform.position,(row.transform.position-c.transform.position).normalized);
            Require(l.Raycast(ray,2,out var hit)&&hit.collider.gameObject==row.gameObject,"Nearer panel stole catalog button ray");
            var blank=w.CatalogRoot.TransformPoint(new Vector3(0,-170,0));ray=new Ray(c.transform.position,(blank-c.transform.position).normalized);
            Require(l.Raycast(ray,2,out hit)&&hit.transform==w.CatalogRoot,"Blank catalog backdrop let a ray through");
            Require(l.CoveredByTools(main,main.position),"Closer panel was not covered by the visual overlay");
            h.DirectTouch.ResetGates();h.DirectTouch.Sample(false,Vector3.zero,true,main.position-main.forward*.03f,.02f);h.DirectTouch.Sample(false,Vector3.zero,true,main.position-main.forward*.001f,.02f);
            Require(h.DirectTouch.PressedButtonThisSample==null,"Hidden body received direct touch");
            var handle=l.CardHandle;var grip=handle.transform.position-handle.transform.forward*.008f;
            Require(l.CoveredByTools(handle.transform,grip),"Near test handle is not behind palette");
            h.Near.ResetGates();h.Near.Sample(false,Vector3.zero,Vector3.zero,true,grip+Vector3.up*.028f,grip-Vector3.up*.028f,.02f);h.Near.Sample(false,Vector3.zero,Vector3.zero,true,grip+Vector3.up*.009f,grip-Vector3.up*.009f,.02f);
            Require(!l.Dragging,"Hidden handle accepted near capture");
            var close=l.WidgetClose("main");var point=close.transform.position;
            Require(!l.CoveredByTools(close.transform,point),"Exposed close area was incorrectly blocked");
            h.DirectTouch.ResetGates();h.DirectTouch.Sample(false,Vector3.zero,true,point-close.transform.forward*.03f,.02f);h.DirectTouch.Sample(false,Vector3.zero,true,point-close.transform.forward*.001f,.02f);
            Require(h.DirectTouch.PressedButtonThisSample==close&&!h.IsCardOpen,"Exposed child close stopped responding");
            h.OpenFromHand();l.ToggleTools();h.Near.ResetGates();grip=l.CardHandle.transform.position-l.CardHandle.transform.forward*.008f;
            h.Near.Sample(false,Vector3.zero,Vector3.zero,true,grip+Vector3.up*.028f,grip-Vector3.up*.028f,.02f);h.Near.Sample(false,Vector3.zero,Vector3.zero,true,grip+Vector3.up*.009f,grip-Vector3.up*.009f,.02f);
            Require(l.NearDragging,"Closing tools did not restore near editing");h.Near.ResetGates();l.EndEdit();
            h.ToolsEntry.SetPaused(false);h.ToolsEntry.Sample(true,new Vector3(.22f,1.35f,.5f),Quaternion.Euler(-90,0,0),false,Vector3.zero,.02f);
            Require(h.ToolsEntry.Visible,"Right entry setup failed");var entry=h.ToolsEntry.Button;
            main.SetPositionAndRotation(entry.transform.position-entry.transform.forward*.05f,entry.transform.rotation);Physics.SyncTransforms();
            ray=new Ray(entry.transform.position-entry.transform.forward*.15f,entry.transform.forward);
            Require(l.Raycast(ray,1,out hit)&&hit.collider.gameObject==entry.gameObject,"Visible utility entry lost ray priority");
            Require(l.Raycast(ray,1,out hit,h.ToolsEntry.Root)&&hit.collider.gameObject!=entry.gameObject,"Own-right ray entered its wrist control");
            Debug.Log("[AeroPulse ToolsOcclusion023] PASS catalog/front-panel priority, blank backdrop, direct/near cover, exposed child touch, restored editing and wrist entry priority");
        }
        finally
        {
            h.Near.ResetGates();if(l.Dragging)l.CancelDrag("");if(l.Editing)l.EndEdit();c.transform.SetPositionAndRotation(pose.position,pose.rotation);l.ApplyLayout(old);
            h.ToolsEntry.Sample(false,Vector3.zero,Quaternion.identity,false,Vector3.zero,.02f);h.HandEntry.enabled=hand;h.ToolsEntry.enabled=tools;h.DirectInputForChecks=direct;w.SetKeepOpen(keep);driver.enabled=driven;
            typeof(AeroLayoutProfileView).GetProperty("IsDirty").SetValue(h.Profiles,dirty);h.Profiles.Refresh();
        }
    }
}
