using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem.XR;

public static class AeroLayoutReview022Checks
{
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException("LayoutReview022: "+message);}
    public static void TrackingIsolation()=>Run(true);
    public static void ToolbarRecovery()=>Run(false);
    static void Run(bool isolation)
    {
        var h=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var l=h.Layout;var c=Camera.main;
        Require(Application.isPlaying,"Play Mode required");
        var saved=l.CaptureLayout();var eye=new Pose(c.transform.position,c.transform.rotation);
        var driver=c.GetComponent<TrackedPoseDriver>();bool driven=driver.enabled,direct=h.DirectInputForChecks,hand=h.HandEntry.enabled,dirty=h.Profiles.IsDirty;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var field=typeof(HandBackHudEntry).GetField("<AnchorTracked>k__BackingField",flags);var tracked=field.GetValue(h.HandEntry);
        var contact=typeof(HandBackHudEntry).GetField("<ContactSurface>k__BackingField",flags);var oldContact=contact.GetValue(h.HandEntry);
        try
        {
            driver.enabled=false;h.DirectInputForChecks=true;h.HandEntry.enabled=false;h.SendMessage("OnApplicationFocus",true);
            c.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);h.ResetViewOptions();h.OpenDataFromShortcut();l.BeginEdit(false);
            field.SetValue(h.HandEntry,true);contact.SetValue(h.HandEntry,new Pose(new Vector3(-.15f,1.3f,.45f),Quaternion.identity));
            l.AddWidget("heart");l.SetPlacement(AeroWindowAnchor.Hand);l.PrepareFrame();
            if(isolation)
            {
                l.CloseWidget("heart");l.SelectWidget("main");var handle=l.CardHandle;var p=handle.transform.position-handle.transform.forward*.008f;
                h.Near.Sample(false,Vector3.zero,Vector3.zero,true,p+Vector3.up*.028f,p-Vector3.up*.028f,.02f);
                h.Near.Sample(false,Vector3.zero,Vector3.zero,true,p+Vector3.up*.009f,p-Vector3.up*.009f,.02f);
                Require(h.Near.Captured&&l.NearDragging,"Near capture setup failed");
                field.SetValue(h.HandEntry,false);l.PrepareFrame();
                Require(l.NearDragging&&h.Near.Captured,"Hidden hand panel tracking cancelled another panel's grab");
                h.Near.Sample(false,Vector3.zero,Vector3.zero,false,Vector3.zero,Vector3.zero,.02f);
                Require(!l.Dragging,"Owning hand loss no longer cancels");
            }
            else
            {
                field.SetValue(h.HandEntry,false);l.PrepareFrame();l.AddWidget("pace");l.PrepareFrame();Physics.SyncTransforms();
                foreach(var b in new[]{l.EnvironmentButton,l.ViewButton})Require(b.GetComponent<Collider>().enabled,"Shared toolbar retained disabled collider");
                var button=l.ViewButton;var ray=new Ray(button.transform.position-button.transform.forward*.15f,button.transform.forward);
                Require(l.Raycast(ray,1,out var hit)&&hit.collider.gameObject==button.gameObject,"Recovered toolbar cannot receive ray");
                h.ActivateButton(button);var state=l.CaptureLayout();foreach(var p in state.panels)if(p.id=="pace")Require(p.anchor==(int)AeroWindowAnchor.View,"Placement activation failed");
            }
            Debug.Log("[AeroPulse LayoutReview022] PASS "+(isolation?"tracking isolation":"shared toolbar recovery"));
        }
        finally
        {
            h.Near.ResetGates();if(l.Dragging)l.CancelDrag("");if(l.Editing)l.EndEdit();
            c.transform.SetPositionAndRotation(eye.position,eye.rotation);field.SetValue(h.HandEntry,tracked);contact.SetValue(h.HandEntry,oldContact);
            l.ApplyLayout(saved);h.DirectInputForChecks=direct;h.HandEntry.enabled=hand;driver.enabled=driven;
            typeof(AeroLayoutProfileView).GetProperty("IsDirty").SetValue(h.Profiles,dirty);
        }
    }
}
