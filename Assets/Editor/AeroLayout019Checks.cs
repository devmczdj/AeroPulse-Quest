using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class AeroLayout019Checks
{
    static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static bool Near(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<.001f;
    public static void Run()
    {
        var h=UnityEngine.Object.FindAnyObjectByType<PinchHudExperiment>();var l=h.Layout;var c=Camera.main;var t=h.Follow.transform;
        c.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>().enabled=false;
        c.transform.parent.SetPositionAndRotation(Vector3.zero,Quaternion.identity);c.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
        h.SendMessage("OnApplicationFocus",true);h.HandEntry.enabled=false;h.DirectInputForChecks=true;l.DeltaForChecks=.02f;
        Ray Aim(AeroDragHandle handle)=>new Ray(handle.transform.position-handle.transform.forward*.15f,handle.transform.forward);
        void Hold(Ray ray){for(int i=0;i<9;i++)l.StepDrag(true,true,ray,null,.02f);}
        try
        {
            foreach(var mode in new[]{AeroWindowAnchor.Follow,AeroWindowAnchor.World,AeroWindowAnchor.Hand,AeroWindowAnchor.View})
            {
                if(l.Editing)l.EndEdit();h.ResetViewOptions();h.OpenDataFromShortcut();
                h.HandEntry.Sample(true,c.transform.position+new Vector3(-.15f,-.25f,.45f),Quaternion.Euler(-90,0,0),false,Vector3.zero,.02f);
                l.BeginEdit(false);if(mode==AeroWindowAnchor.World)l.TogglePin();else l.SetPlacement(mode);
                Require(l.EnvironmentButton.transform.parent.GetComponentsInChildren<Button>(true).Length==3,"Bottom rail contains extra actions");
                Require(l.UndoButton.transform.parent!=l.EnvironmentButton.transform.parent,"Tools were not moved to side");
                foreach(var b in new[]{l.EnvironmentButton,l.HandButton,l.ViewButton})Require(b.GetComponentInChildren<TextMeshProUGUI>().enabled,"Placement caption hidden");
                var centre=c.transform.position;var original=t.position;float radius=Vector3.Distance(centre,original);var scale=t.localScale;
                var ray=Aim(l.CardHandle);l.StartDrag("right",l.CardHandle,ray,.15f);Hold(ray);
                Require(Near(t.position,original)&&Near(t.localScale,scale),"Stationary off-eye orbit start jumped");
                var moved=new Ray(ray.origin+new Vector3(.08f,.04f,0),ray.direction);Hold(moved);
                Require(Mathf.Abs(Vector3.Distance(centre,t.position)-radius)<.0001f&&!Near(original,t.position),"Orbit changes radius or does not move");
                var pose=t.position;var invalid=new Ray(ray.origin,Vector3.Cross(original-centre,Vector3.up).normalized);Hold(invalid);Require(Near(pose,t.position),"Parallel orbit ray moved panel");
                l.StepDrag(true,false,moved,null,.02f);Require(l.MainAnchor==mode,"Orbit changed placement category");l.PrepareFrame();Require(Near(pose,t.position),"Orbit release jumps after attachment update");
                original=t.position;centre=c.transform.position;radius=Vector3.Distance(centre,original);var axis=(original-centre).normalized;scale=t.localScale;float userScale=h.Follow.UserScale;
                ray=Aim(l.DepthHandle);l.StartDrag("right",l.DepthHandle,ray,.15f);Hold(ray);Require(Near(t.position,original),"Depth start jumped");
                Hold(new Ray(ray.origin,Quaternion.Euler(0,20,0)*ray.direction));Require(Near(t.position,original),"Wrist rotation changes distance");
                Hold(new Ray(ray.origin+Vector3.Cross(axis,Vector3.up).normalized*.09f,ray.direction));Require(Near(t.position,original),"Lateral movement changes distance");
                var farther=new Ray(ray.origin+axis*.12f,ray.direction);Hold(farther);
                Require(Mathf.Abs(Vector3.Distance(centre,t.position)-(radius+.12f))<.001f&&Vector3.Cross(t.position-original,axis).magnitude<.001f,"Depth is not radial-only");
                Require(Near(scale,t.localScale)&&Mathf.Approximately(userScale,h.Follow.UserScale),"Depth resized the HUD");
                pose=t.position;l.StepDrag(true,false,farther,null,.02f);l.PrepareFrame();Require(Near(pose,t.position)&&l.MainAnchor==mode,"Depth commit jumps or changes mode");
                Require(Mathf.Abs(h.Follow.Distance-Vector3.Distance(centre,t.position))<.001f,"Distance/offset no longer agree");
                l.Undo();Require(Near(original,t.position)&&Near(scale,t.localScale),"Depth undo incomplete");
                ray=Aim(l.CardHandle);l.StartDrag("right",l.CardHandle,ray,.15f);Hold(new Ray(ray.origin+Vector3.right*.05f,ray.direction));pose=t.position;
                c.transform.position+=new Vector3(.04f,0,.02f);c.transform.rotation*=Quaternion.Euler(0,8,0);l.PrepareFrame();Require(Near(pose,t.position),"Head motion moved captured panel");
                l.StepDrag(true,false,ray,null,.02f);l.PrepareFrame();l.PrepareFrame();Require(Near(pose,t.position),"Current reference rebasing jumps on release");
                original=t.position;ray=Aim(l.DepthHandle);l.StartDrag("right",l.DepthHandle,ray,.15f);Hold(new Ray(ray.origin+axis*.1f,ray.direction));
                l.StartDrag("left",l.CardHandle,ray,.15f);Require(l.DragOwner=="right","Second hand stole manipulation");l.StepDrag(false,true,ray,null,.02f);Require(Near(original,t.position)&&!l.Dragging,"Loss committed pending depth");
            }
            l.EndEdit();h.ResetViewOptions();h.OpenDataFromShortcut();l.BeginEdit(false);Require(l.SelectCompactButton.IsInteractable(),"Hidden compact entry disabled");l.SelectPanel(true);Require(l.SelectedCompact&&h.Compact.gameObject.activeSelf,"Compact cannot be enabled from editor");
            var small=h.Compact.transform;var main=t.position;var start=small.position;var cr=Aim(l.CompactDepthHandle);var u=(start-c.transform.position).normalized;
            l.StartDrag("right",l.CompactDepthHandle,cr,.15f);Hold(new Ray(cr.origin+u*.15f,cr.direction));l.StepDrag(true,false,cr,null,.02f);
            Require(Near(main,t.position)&&!Near(start,small.position)&&l.CompactAnchor==AeroWindowAnchor.View,"Compact depth is not independent");l.Undo();Require(Near(start,small.position),"Compact depth undo failed");
            foreach(var text in UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include))if(text.enabled&&text.gameObject.activeInHierarchy){text.ForceMeshUpdate();Require(!text.isTextOverflowing,"Text overflow: "+text.name);foreach(char ch in text.text)Require(char.IsWhiteSpace(ch)||text.font.HasCharacter(ch,true),"Missing glyph: "+ch);}
            Require(l.SkyHandle==null,"Sky acquired editing controls");
            Debug.Log("[AeroPulse Layout019] PASS three labeled placements, side tools, orbit radius/off-eye grabs, radial-only depth, size independence, frozen frame/rebase, modes/undo/cancel/owner, compact activation and isolation, glyphs");
        }
        finally{if(l.Editing)l.EndEdit();h.ResetViewOptions();h.Follow.GetComponent<AeroHudView>().ShowMap(false);h.Follow.GetComponent<AeroHudView>().CloseButton.onClick.Invoke();h.HandEntry.enabled=true;h.DirectInputForChecks=false;l.DeltaForChecks=0;}
    }
}
