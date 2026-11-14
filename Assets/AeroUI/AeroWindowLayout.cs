using System;
using TMPro;
using UnityEngine;

// Explicit layout editing. Rays stay unmodified; only a released window commits a dock.
public class AeroWindowLayout : MonoBehaviour
{
    public bool Editing { get; private set; }
    public bool HasSkyPlacement { get; private set; }
    public bool Dragging => dragOwner != null;
    public string DragOwner => dragOwner;
    public bool DragActive => Dragging && holdTime >= 0.15f;
    public AeroWindowAnchor MainAnchor { get; private set; }
    public AeroWindowAnchor SkyAnchor { get; private set; } = AeroWindowAnchor.World;
    public AeroDockZone WorldZone, ViewZone, HandZone;
    public AeroDragHandle CardHandle, SkyHandle;
    public UnityEngine.UI.Button DoneButton, UndoButton, ResetButton;
    public string MainLabel => MainAnchor==AeroWindowAnchor.Hand?"HAND DOCK":MainAnchor==AeroWindowAnchor.View?"VIEW DOCK":owner.Follow.ModeLabel;
    public bool NeedsRecall
    {
        get
        {
            if(!owner.IsCardOpen||Editing)return false;
            return (MainAnchor==AeroWindowAnchor.Hand&&!owner.HandEntry.AnchorTracked) ||
                ((MainAnchor==AeroWindowAnchor.World||owner.Follow.Mode==HudSoftFollow.FollowMode.Fixed) &&
                 Vector3.Angle(eye.transform.forward,card.position-eye.transform.position)>70f);
        }
    }
#if UNITY_EDITOR
    public float DeltaForChecks;
#endif
    PinchHudExperiment owner; Camera eye; Transform card,sky; GameObject toolbar;
    TextMeshProUGUI status;
    UnityEngine.UI.Button[] buttons;
    bool editingSky, handAvailable=true;
    string dragOwner;
    float holdTime, holdDistance;
    Vector3 grabOffset;
    AeroDockZone candidate;
    State before,undo; bool hasUndo;
    Pose mainViewLocal,skyViewLocal;
    readonly RaycastHit[] hits=new RaycastHit[64];
    Transform ActiveWindow => owner.Sky.Visible?sky:card;
    Transform EditedWindow => editingSky?sky:card;
    struct State
    {
        public bool Sky,CustomSky; public Pose Pose,ViewLocal; public Vector3 Scale;
        public AeroWindowAnchor Anchor; public HudSoftFollow.FollowMode Follow;
    }
    public void Initialize(PinchHudExperiment hud,Camera camera,Transform detail,Transform skyLayer)
    {
        owner=hud;eye=camera;card=detail;sky=skyLayer;
        CardHandle=Handle(card,false,252);SkyHandle=null;
        WorldZone=Zone("WorldDock",AeroWindowAnchor.World,"固定在环境");
        ViewZone=Zone("ViewDock",AeroWindowAnchor.View,"跟随视野");
        HandZone=Zone("HandDock",AeroWindowAnchor.Hand,"跟随左手");
        toolbar=new GameObject("LayoutToolbar",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
        toolbar.transform.SetParent(camera.transform,false);toolbar.transform.localPosition=new Vector3(0,-0.43f,1.35f);toolbar.transform.localScale=Vector3.one*0.001f;
        ((RectTransform)toolbar.transform).sizeDelta=new Vector2(660,126);
        var canvas=toolbar.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
        status=AeroHudView.Text(toolbar.transform,"LayoutStatus","",new Vector2(-320,44),new Vector2(640,42),18,AeroUiFonts.Interface,AeroUiTokens.Ink,TextAlignmentOptions.Left,0,0);
        DoneButton=AeroHudView.ActionButton(toolbar.transform,"LayoutDone","退出编辑",new Vector2(-230,-4),new Vector2(200,56),new Vector3(210,72,8));
        UndoButton=AeroHudView.ActionButton(toolbar.transform,"LayoutUndo","撤销放置",new Vector2(0,-4),new Vector2(200,56),new Vector3(210,72,8));
        ResetButton=AeroHudView.ActionButton(toolbar.transform,"LayoutReset","复位主卡",new Vector2(230,-4),new Vector2(200,56),new Vector3(210,72,8));
        buttons=new[]{DoneButton,UndoButton,ResetButton};
        foreach(var b in buttons){var label=b.GetComponentInChildren<TextMeshProUGUI>();label.font=AeroUiFonts.Interface;label.characterSpacing=0;}
        DoneButton.onClick.AddListener(EndEdit);UndoButton.onClick.AddListener(Undo);ResetButton.onClick.AddListener(ResetAll);
        SetEditUI(false);
    }
    AeroDragHandle Handle(Transform parent,bool isSky,float y)
    {
        var image=AeroHudView.AddPanelImage(parent,isSky?"SkyGrabHandle":"CardGrabHandle",AeroUiSprites.Pill,new Vector2(0,y),new Vector2(260,44),AeroUiTokens.ButtonFill);
        image.gameObject.AddComponent<BoxCollider>().size=new Vector3(270,58,10);
        AeroHudView.Text(image.transform,"GrabLabel","捏住把手移动",Vector2.zero,new Vector2(248,40),22,AeroUiFonts.Interface,AeroUiTokens.Mint,TextAlignmentOptions.Center,0,0,true);
        var handle=image.gameObject.AddComponent<AeroDragHandle>();handle.Sky=isSky;image.gameObject.SetActive(false);return handle;
    }
    AeroDockZone Zone(string name,AeroWindowAnchor anchor,string label)
    {
        var go=new GameObject(name,typeof(BoxCollider),typeof(LineRenderer));
        if(anchor==AeroWindowAnchor.Hand)go.transform.localScale=Vector3.one*0.45f;
        go.GetComponent<BoxCollider>().size=new Vector3(0.5f,0.34f,0.025f);
        var line=go.GetComponent<LineRenderer>();line.sharedMaterial=Resources.Load<Material>("AeroHandRay");line.useWorldSpace=false;line.positionCount=5;line.startWidth=line.endWidth=0.003f;line.startColor=line.endColor=AeroUiTokens.Mint;
        line.SetPositions(new[]{new Vector3(-0.25f,-0.17f,0),new Vector3(-0.25f,0.17f,0),new Vector3(0.25f,0.17f,0),new Vector3(0.25f,-0.17f,0),new Vector3(-0.25f,-0.17f,0)});
        var text=new GameObject("DockLabel",typeof(RectTransform),typeof(Canvas));text.transform.SetParent(go.transform,false);text.transform.localPosition=new Vector3(0,0.23f,0);text.transform.localScale=Vector3.one*0.001f;
        var canvas=text.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=eye;
        AeroHudView.Text(text.transform,"Caption",label,Vector2.zero,new Vector2(600,70),26,AeroUiFonts.Interface,AeroUiTokens.Ink,TextAlignmentOptions.Center,0,0,true);
        var zone=go.AddComponent<AeroDockZone>();zone.Anchor=anchor;go.SetActive(false);return zone;
    }
    public void BeginEdit(bool isSky)
    {
        if(isSky){owner.RecenterSky();return;}
        if(Dragging)CancelDrag("CANCELLED");
        owner.ShowDetailForLayout();
        editingSky=isSky;Editing=true;if(hasUndo&&undo.Sky!=isSky)hasUndo=false;SetEditUI(true);
        PlaceZones();status.text="布局编辑：捏住把手开始，松手即放置";owner.ClearPresses();
    }
    public void EndEdit() { if(Dragging)CancelDrag("已取消本次拖动");Editing=false;SetEditUI(false);owner.ClearPresses();owner.RefreshSettings("已退出布局编辑"); }
    void SetEditUI(bool show)
    {
        CardHandle.gameObject.SetActive(show&&!editingSky);if(SkyHandle!=null)SkyHandle.gameObject.SetActive(false);
        toolbar.SetActive(show);SetDockTargets(show&&DragActive);
        card.GetComponent<AeroHudView>().SetLayoutEditing(show);
    }
    void SetDockTargets(bool show)
    {
        WorldZone.gameObject.SetActive(show);ViewZone.gameObject.SetActive(show);
        HandZone.gameObject.SetActive(show&&!editingSky&&owner.HandEntry.AnchorTracked);
    }
    void HighlightDock(AeroDockZone selected)
    {
        foreach(var zone in new[]{WorldZone,ViewZone,HandZone})
        {var line=zone.GetComponent<LineRenderer>();var tint=AeroUiTokens.Mint;tint.a=selected==null||zone==selected?1f:.25f;line.startColor=line.endColor=tint;}
    }
    Pose ViewPose(bool isSky)
    {
        Vector3 p=eye.transform.TransformPoint(isSky?new Vector3(-0.9f,1.55f,3.6f):new Vector3(-0.9f,0.15f,owner.Follow.Distance));
        return new Pose(p,Quaternion.LookRotation(p-eye.transform.position,eye.transform.up));
    }
    Pose HandPose()
    {
        var h=owner.HandEntry.ContactSurface;
        return new Pose(h.position-(h.rotation*Vector3.forward)*0.12f+(h.rotation*Vector3.up)*0.18f,h.rotation);
    }
    void PlaceZones()
    {
        var forward=Vector3.ProjectOnPlane(eye.transform.forward,Vector3.up).normalized;
        if(forward.sqrMagnitude<0.01f)forward=Vector3.forward;
        var right=Vector3.Cross(Vector3.up,forward);
        var p=eye.transform.position+forward*(editingSky?3.6f:owner.Follow.Distance)+right*0.9f+Vector3.up*(editingSky?1.55f:0.15f);
        WorldZone.transform.SetPositionAndRotation(p,Quaternion.LookRotation(p-eye.transform.position,Vector3.up));
        var v=ViewPose(editingSky);ViewZone.transform.SetPositionAndRotation(v.position,v.rotation);
        UpdateHandZone();Physics.SyncTransforms();
    }
    void UpdateHandZone()
    {
        if(!Editing)return;
        bool valid=DragActive&&!editingSky&&owner.HandEntry.AnchorTracked;HandZone.gameObject.SetActive(valid);
        if(valid){var h=HandPose();HandZone.transform.SetPositionAndRotation(h.position,h.rotation);}
    }
    State Capture(bool isSky)
    {
        var t=isSky?sky:card;
        return new State {Sky=isSky,CustomSky=HasSkyPlacement,Pose=new Pose(t.position,t.rotation),Scale=t.localScale,Anchor=isSky?SkyAnchor:MainAnchor,
            ViewLocal=isSky?skyViewLocal:mainViewLocal,Follow=owner.Follow.Mode};
    }
    void Restore(State state)
    {
        var t=state.Sky?sky:card;t.SetPositionAndRotation(state.Pose.position,state.Pose.rotation);t.localScale=state.Scale;
        if(state.Sky){SkyAnchor=state.Anchor;skyViewLocal=state.ViewLocal;HasSkyPlacement=state.CustomSky;}
        else{MainAnchor=state.Anchor;mainViewLocal=state.ViewLocal;owner.Follow.SetMode(state.Follow);if(state.Anchor!=AeroWindowAnchor.Hand)SetHandRendering(true);}
        PrepareFrame();Physics.SyncTransforms();
    }
    public void StartDrag(string hand,AeroDragHandle handle,Ray ray,float distance)
    {
        if(!Editing||Dragging||handle.Sky!=editingSky)return;
        before=Capture(editingSky);dragOwner=hand;holdTime=0;holdDistance=Mathf.Clamp(distance,0.35f,4.5f);
        grabOffset=EditedWindow.position-ray.GetPoint(distance);candidate=null;status.text="继续捏住，准备移动";
    }
    public bool Raycast(Ray ray,float distance,out RaycastHit hit)
    {
        if(!Dragging)return Physics.Raycast(ray,out hit,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        int count=Physics.RaycastNonAlloc(ray,hits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        hit=default;float nearest=distance+1;bool found=false;
        for(int i=0;i<count;i++)if(!hits[i].collider.transform.IsChildOf(EditedWindow)&&hits[i].distance<nearest)
        {nearest=hits[i].distance;hit=hits[i];found=true;}
        return found;
    }
    public void StepDrag(bool valid,bool pressed,Ray ray,AeroDockZone zone,float dt)
    {
        if(!Dragging)return;
        if(!valid || (candidate!=null&&candidate.Anchor==AeroWindowAnchor.Hand&&!owner.HandEntry.AnchorTracked)){CancelDrag("TRACKING LOST - RESTORED");return;}
        if(!pressed)
        {
            if(holdTime<0.15f){CancelDrag("TAP IGNORED - HOLD TO MOVE");return;}
            undo=before;hasUndo=true;
            var anchor=candidate!=null?candidate.Anchor:AeroWindowAnchor.World;
            Commit(anchor);dragOwner=null;candidate=null;SetDockTargets(false);status.text="已放置，可继续调整或退出";owner.RefreshSettings();return;
        }
#if UNITY_EDITOR
        if(DeltaForChecks>0)dt=DeltaForChecks;
#endif
        holdTime+=Mathf.Clamp(dt,0,0.05f);if(holdTime<0.15f)return;SetDockTargets(true);UpdateHandZone();
        candidate=zone!=null&&zone.gameObject.activeInHierarchy?zone:null;
        HighlightDock(candidate);
        var target=EditedWindow;
        if(candidate!=null)
        {
            target.SetPositionAndRotation(candidate.transform.position,candidate.transform.rotation);
            target.localScale=Vector3.one*(candidate.Anchor==AeroWindowAnchor.Hand?0.00055f:editingSky?0.0026f:0.0015f);
            status.text="松手放置："+(candidate.Anchor==AeroWindowAnchor.Hand?"跟随左手":candidate.Anchor==AeroWindowAnchor.View?"跟随视野":"固定在环境");
        }
        else
        {
            target.SetPositionAndRotation(ray.GetPoint(holdDistance)+grabOffset,before.Pose.rotation);
            target.localScale=before.Scale;status.text="松手固定在此处";
        }
        Physics.SyncTransforms();
    }
    void Commit(AeroWindowAnchor anchor)
    {
        var local=new Pose(eye.transform.InverseTransformPoint(EditedWindow.position),Quaternion.Inverse(eye.transform.rotation)*EditedWindow.rotation);
        if(editingSky){SkyAnchor=anchor;skyViewLocal=local;HasSkyPlacement=true;}
        else{MainAnchor=anchor;mainViewLocal=local;owner.Follow.SetMode(HudSoftFollow.FollowMode.Fixed);}
        PrepareFrame();Physics.SyncTransforms();
    }
    public void CancelDrag(string reason)
    {
        if(!Dragging)return;
        dragOwner=null;candidate=null;Restore(before);SetDockTargets(false);status.text="本次拖动已取消，原位置保留";
    }
    public void Undo()
    {
        if(Dragging)CancelDrag("CANCELLED");
        if(!hasUndo){status.text="暂无可撤销的放置";return;}
        Restore(undo);hasUndo=false;status.text="已撤销最近一次放置";owner.RefreshSettings();
    }
    public void ResetAll()
    {
        if(Dragging)CancelDrag("CANCELLED");
        MainAnchor=AeroWindowAnchor.Follow;SkyAnchor=AeroWindowAnchor.World;HasSkyPlacement=false;card.localScale=Vector3.one*0.0015f;
        SetHandRendering(true);owner.Follow.ResetOptions();hasUndo=false;
        if(Editing){PlaceZones();status.text="主卡布局已复位";}Physics.SyncTransforms();
    }
    Canvas cardCanvas;Collider[] cardColliders;bool? handRendering;
    void SetHandRendering(bool visible)
    {
        if(handRendering==visible)return;handRendering=visible;
        if(cardCanvas==null){cardCanvas=card.GetComponent<Canvas>();cardColliders=card.GetComponentsInChildren<Collider>(true);}
        cardCanvas.enabled=visible;foreach(var collider in cardColliders)collider.enabled=visible;
    }
    public void PrepareFrame()
    {
        if(owner==null)return;
        UpdateHandZone();
        if(Dragging)return;
        if(MainAnchor==AeroWindowAnchor.Hand)
        {
            bool tracked=owner.HandEntry.AnchorTracked;
            if(tracked!=handAvailable){handAvailable=tracked;if(card.gameObject.activeSelf){owner.ClearPresses();owner.HandEntry.RequireRelease();}}
            SetHandRendering(tracked);
            if(tracked){var p=HandPose();card.SetPositionAndRotation(p.position,p.rotation);}
        }
        if(MainAnchor==AeroWindowAnchor.View)ApplyView(card,mainViewLocal);
        if(SkyAnchor==AeroWindowAnchor.View)ApplyView(sky,skyViewLocal);
    }
    void ApplyView(Transform t,Pose local) => t.SetPositionAndRotation(eye.transform.TransformPoint(local.position),eye.transform.rotation*local.rotation);
    public void ReleaseToFollow()
    {
        if(MainAnchor==AeroWindowAnchor.Hand||MainAnchor==AeroWindowAnchor.View)owner.Follow.SetMode(HudSoftFollow.FollowMode.Soft);
        MainAnchor=AeroWindowAnchor.Follow;card.localScale=Vector3.one*0.0015f;SetHandRendering(true);
    }
    public void SetAnchorFromFollowMode() { MainAnchor=owner.Follow.Mode==HudSoftFollow.FollowMode.Fixed?AeroWindowAnchor.World:AeroWindowAnchor.Follow; }
    public void PlaceOpenedCard()
    {
        if(MainAnchor==AeroWindowAnchor.Follow)owner.Follow.SnapToView();else PrepareFrame();
    }
    public void RecalledSky() { HasSkyPlacement=true;if(SkyAnchor==AeroWindowAnchor.View)skyViewLocal=new Pose(eye.transform.InverseTransformPoint(sky.position),Quaternion.Inverse(eye.transform.rotation)*sky.rotation); }
    public bool ToolbarButton(UnityEngine.UI.Button button) => button!=null&&button.transform.IsChildOf(toolbar.transform);
    public bool Owns(Transform t) => t.IsChildOf(toolbar.transform);
    UnityEngine.UI.Button hoverLeft,hoverRight;bool hoverSet;
    public void SetHover(UnityEngine.UI.Button left,UnityEngine.UI.Button right)
    {
        if(hoverSet&&left==hoverLeft&&right==hoverRight)return;hoverSet=true;hoverLeft=left;hoverRight=right;
        foreach(var b in buttons)b.GetComponent<UnityEngine.UI.Image>().color=b==left||b==right?AeroUiTokens.ButtonHover:AeroUiTokens.ButtonFill;
    }
    void OnDestroy()
    {
        foreach(var go in new[]{toolbar,WorldZone!=null?WorldZone.gameObject:null,ViewZone!=null?ViewZone.gameObject:null,HandZone!=null?HandZone.gameObject:null})
            if(go!=null){if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
    }
}
