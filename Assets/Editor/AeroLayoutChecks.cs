using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;

public static class AeroLayoutChecks
{
    static void Require(bool value,string message) {if(!value)throw new InvalidOperationException(message);}
    public static void Run()
    {
        var hud=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var camera=Camera.main;
        camera.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);hud.ResetViewOptions();hud.ShowMapDetail();
        var view=UnityEngine.Object.FindObjectsByType<AeroHudView>(FindObjectsInactive.Include)[0];view.ShowMap(false);
        var layout=hud.Layout;layout.DeltaForChecks=0.02f;var reader=hud.HandEntry;reader.enabled=false;
        var palm=new Vector3(-0.2f,1.3f,0.5f);var handRotation=Quaternion.Euler(-90,0,0);
        void Track(bool valid=true) {reader.Sample(valid,palm,handRotation,false,Vector3.zero,0.02f);layout.PrepareFrame();}
        Track();
        void Point(Transform target,bool pressed,MetaAimHand hand=null)
        {
            if(hand==null)hand=MetaAimHand.right;
            var origin=camera.transform.position+new Vector3(hand==MetaAimHand.left?-0.25f:0.25f,-0.3f,0.2f);
            AeroHandRayChecks.Frame(hand,origin,Quaternion.LookRotation(target.position-origin),pressed,camera.transform.parent);hud.ProcessInput();
        }
        void Click(UnityEngine.UI.Button b) {Point(b.transform,false);Point(b.transform,true);}
        void Grab(AeroDragHandle handle) {Point(handle.transform,false);Point(handle.transform,true);}
        void Hold(AeroDockZone zone) {for(int i=0;i<9;i++)Point(zone.transform,true);}
        var card=view.transform;
        Click(view.MapButton);Require(!layout.Dragging,"Normal map click starts dragging");Click(view.MapButton);
        Click(view.SettingsButton);if(!view.SettingsView.LayoutPage)Click(view.SkyButton);Click(view.SettingsView.EditCardButton);
        Require(layout.Editing&&layout.CardHandle.gameObject.activeInHierarchy&&layout.SkyHandle==null,"Explicit edit UI missing");
        var start=card.position;Click(view.MapButton);Require(!view.IsMapOpen&&!layout.Dragging,"Layout mode still clicks content");
        Grab(layout.CardHandle);Require(layout.Dragging&&layout.DragOwner=="right","Handle press did not own selection");Point(layout.CardHandle.transform,false);
        Require(!layout.Dragging&&layout.MainAnchor==AeroWindowAnchor.Follow&&Vector3.Distance(start,card.position)<0.001f,"Short handle tap changed placement");
        Grab(layout.CardHandle);Hold(layout.WorldZone);
        Require(layout.DragActive&&layout.MainAnchor==AeroWindowAnchor.Follow,"Preview committed before release");
        var preview=card.position;Require(Vector3.Distance(preview,layout.WorldZone.transform.position)<0.001f,"Actual world-zone ray did not preview dock");
        var beam=GameObject.Find("RightHandRay").GetComponent<LineRenderer>();var actual=(beam.GetPosition(1)-beam.GetPosition(0)).normalized;
        var origin=camera.transform.position+new Vector3(0.25f,-0.3f,0.2f);
        Require(Vector3.Dot(actual,(layout.WorldZone.transform.position-origin).normalized)>0.99999f,"Dock preview snapped ray direction");
        Point(layout.WorldZone.transform,false);Require(!layout.Dragging&&layout.MainAnchor==AeroWindowAnchor.World&&Vector3.Distance(preview,card.position)<0.001f,"Release differs from preview");
        camera.transform.rotation=Quaternion.Euler(0,25,0);hud.SendMessage("LateUpdate");Require(Vector3.Distance(preview,card.position)<0.001f,"World dock follows head");
        Click(layout.UndoButton);Require(layout.MainAnchor==AeroWindowAnchor.Follow&&Vector3.Distance(start,card.position)<0.001f,"World dock undo failed");
        camera.transform.rotation=Quaternion.identity;hud.Recenter();
        Grab(layout.CardHandle);Hold(layout.ViewZone);Point(layout.ViewZone.transform,false);
        Require(layout.MainAnchor==AeroWindowAnchor.View,"View dock commit failed");
        var local=camera.transform.InverseTransformPoint(card.position);var world=card.position;camera.transform.rotation=Quaternion.Euler(0,35,0);layout.PrepareFrame();
        Require(Vector3.Distance(local,camera.transform.InverseTransformPoint(card.position))<0.001f&&Vector3.Distance(world,card.position)>0.1f,"View dock not head-relative");
        Click(layout.UndoButton);camera.transform.rotation=Quaternion.identity;hud.Recenter();Track();
        Grab(layout.CardHandle);Hold(layout.HandZone);Point(layout.HandZone.transform,false);
        Require(layout.MainAnchor==AeroWindowAnchor.Hand&&Mathf.Abs(card.localScale.x-0.00055f)<0.000001f,"Hand dock did not commit expected scale");
        world=card.position;palm+=Vector3.right*0.1f;Track();Require(Vector3.Distance(world,card.position)>0.09f,"Hand dock did not follow left pose");
        Track(false);Require(!card.GetComponent<Canvas>().enabled,"Lost hand keeps window visible");
        foreach(var collider in card.GetComponentsInChildren<Collider>(true))Require(!collider.enabled,"Lost hand leaves collider active");
        Click(layout.UndoButton);Require(card.GetComponent<Canvas>().enabled&&layout.MainAnchor==AeroWindowAnchor.Follow,"Undo from lost hand stays invisible");Track();
        // Other hand cannot take an owned drag; lost owner cancels to the pre-grab pose.
        start=card.position;Grab(layout.CardHandle);for(int i=0;i<8;i++)Point(layout.WorldZone.transform,true);
        Point(layout.CardHandle.transform,true,MetaAimHand.left);Require(layout.DragOwner=="right","Second hand stole drag");
        AeroHandRayChecks.Set(MetaAimHand.right,false,true,Vector3.zero,Quaternion.identity);hud.ProcessInput();
        Require(!layout.Dragging&&Vector3.Distance(start,card.position)<0.001f,"Tracking loss did not restore previous placement");
        AeroHandRayChecks.Set(MetaAimHand.left,false,false,Vector3.zero,Quaternion.identity);Point(layout.CardHandle.transform,true);
        Require(!layout.Dragging,"Tracking recovery while pinching grabbed handle");
        Grab(layout.CardHandle);Hold(layout.WorldZone);hud.SendMessage("OnApplicationFocus",false);
        Require(!layout.Dragging&&Vector3.Distance(start,card.position)<0.001f,"Focus loss did not cancel");hud.SendMessage("OnApplicationFocus",true);
        Click(layout.ResetButton);Click(layout.DoneButton);Require(!layout.Editing&&!layout.WorldZone.gameObject.activeInHierarchy&&!layout.CardHandle.gameObject.activeInHierarchy,"Exit left handles/zones active");
        // A placed hand window can always be recalled after hand loss.
        layout.BeginEdit(false);Track();Grab(layout.CardHandle);Hold(layout.HandZone);Point(layout.HandZone.transform,false);Click(layout.DoneButton);Track(false);hud.ProcessInput();
        Require(hud.Compact.FallbackVisible,"Lost hand dock has no recovery control");Click(hud.Compact.OpenButton);
        Require(layout.MainAnchor==AeroWindowAnchor.Follow&&card.GetComponent<Canvas>().enabled,"Hand recall did not recover visible front window");
        // Sky is now a passive environment layer, not another draggable window.
        Track();hud.ShowSkyMap();var skyPosition=hud.Sky.transform.position;layout.BeginEdit(true);
        Require(!layout.Editing&&layout.SkyHandle==null,"Sky entered obsolete window editor");
        Require(hud.Sky.Visible,"HUD layout action hid the independent sky");
        foreach(var text in UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
        {text.ForceMeshUpdate();Require(!text.isTextOverflowing,"Layout text overflow: "+text.name);}
        layout.ResetAll();layout.EndEdit();hud.ShowMapDetail();view.ShowMap(false);view.CloseButton.onClick.Invoke();reader.enabled=true;layout.DeltaForChecks=0;
        Debug.Log("[AeroPulse Layout] PASS: explicit edit, hold threshold, true-ray preview/release, world/view/hand, undo/reset, ownership, lost tracking/focus, recovery; sky cannot enter window layout");
    }
}
