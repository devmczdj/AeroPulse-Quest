using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Placement owns the reference frame; a manipulation only edits its pose/size.
public class AeroWindowLayout : MonoBehaviour
{
    public bool Editing {get;private set;}
    public bool Dragging=>dragOwner!=null;
    public bool DragActive=>Dragging&&holdTime>=.15f;
    public string DragOwner=>dragOwner;
    public bool SelectedCompact=>selected==small;
    public AeroWindowAnchor MainAnchor=>main.Anchor;
    public AeroWindowAnchor CompactAnchor=>small.Anchor;
    public bool HasSkyPlacement=>false;
    public AeroWindowAnchor SkyAnchor=>AeroWindowAnchor.World;
    // Old Editor helpers remain compilable; these zones never enter the new UI.
    public AeroDockZone WorldZone,ViewZone,HandZone;
    public AeroDragHandle CardHandle,SkyHandle,ResizeHandle,CompactHandle,CompactResizeHandle;
    public Button DoneButton,UndoButton,ResetButton,PinButton,EnvironmentButton,HandButton,ViewButton,SelectMainButton,SelectCompactButton,SpeedButton;
    public string MainLabel=>main.Anchor==AeroWindowAnchor.Hand?"HAND DOCK":main.Anchor==AeroWindowAnchor.View?"VIEW DOCK":main.Follow.ModeLabel;
    public float MainScale=>main.Follow.UserScale;
    public float CompactScale=>small.Follow.UserScale;
    public bool NeedsRecall=>((owner.IsCardOpen&&NeedsRecallFor(main))||(owner.Compact.gameObject.activeSelf&&NeedsRecallFor(small)));
#if UNITY_EDITOR
    public float DeltaForChecks;
#endif
    sealed class Panel
    {
        public Transform Root;public HudSoftFollow Follow;public AeroWindowAnchor Anchor;
        public Pose Local;public bool Pinned,Tracked=true;public HudSoftFollow.FollowMode Speed=HudSoftFollow.FollowMode.Soft;
        public Pose[] Saved=new Pose[4];public bool[] HasSaved=new bool[4];
        public Canvas Canvas;public Collider[] Colliders;public AeroDragHandle Move,Resize;
        public Snapshot Undo;public bool HasUndo;
    }
    sealed class Snapshot
    {
        public Pose World,Local,Offset;public Vector3 Scale;public AeroWindowAnchor Anchor;
        public float Distance,Size;public HudSoftFollow.FollowMode Mode,Speed;public bool Pinned;
        public Pose[] Saved;public bool[] HasSaved;
    }
    PinchHudExperiment owner;Camera eye;Panel main,small,selected;Panel[] panels;
    RectTransform toolbar;TextMeshProUGUI nameLabel,sizeLabel;
    readonly List<Button> buttons=new List<Button>();
    string dragOwner;float holdTime,holdDistance,startProjection;
    bool resizing;Vector3 grabOffset,pivot,diagonal;Plane resizePlane;Snapshot before;
    readonly RaycastHit[] hits=new RaycastHit[64];
    Button hoverLeft,hoverRight;
    bool NeedsRecallFor(Panel p)=>p.Anchor==AeroWindowAnchor.Hand&&!owner.HandEntry.AnchorTracked||Vector3.Angle(eye.transform.forward,p.Root.position-eye.transform.position)>70;
    public void Initialize(PinchHudExperiment hud,Camera camera,Transform detail,Transform sky)
    {
        owner=hud;eye=camera;
        main=MakePanel(detail,owner.Follow,AeroWindowAnchor.Follow,false);
        var compact=owner.Compact.transform;compact.SetParent(null,true);
        var f=compact.gameObject.AddComponent<HudSoftFollow>();f.Initialize(eye.transform);f.ConfigureScale(.0009f/1.05f,1.05f);
        small=MakePanel(compact,f,AeroWindowAnchor.View,true);small.Local=new Pose(new Vector3(-.42f,-.14f,1.05f),Quaternion.identity);
        panels=new[]{main,small};owner.Compact.PlacementChanged=CompactPreset;selected=main;CardHandle=main.Move;ResizeHandle=main.Resize;CompactHandle=small.Move;CompactResizeHandle=small.Resize;
        WorldZone=LegacyZone(AeroWindowAnchor.World);ViewZone=LegacyZone(AeroWindowAnchor.View);HandZone=LegacyZone(AeroWindowAnchor.Hand);
        toolbar=AeroHudView.NewRect("LayoutToolbar",detail,Vector2.zero,new Vector2(456,132));
        AeroHudView.AddPanelImage(toolbar,"Backing",AeroUiSprites.Pill,Vector2.zero,new Vector2(456,132),new Color(.04f,.07f,.08f,.96f));
        SelectMainButton=Button("SelectMain","主卡","browser",-180,34,()=>SelectPanel(false));
        SelectCompactButton=Button("SelectCompact","小型读数","picture-in-picture",-108,34,()=>SelectPanel(true));
        nameLabel=AeroHudView.Text(toolbar,"EditedObject","主卡",new Vector2(-56,34),new Vector2(174,40),22,AeroUiFonts.Interface,AeroUiTokens.Ink,TextAlignmentOptions.Left,0,0);
        SpeedButton=Button("FollowSpeed","跟随速度","arrow-right",180,34,ToggleSpeed);
        EnvironmentButton=Button("PlaceEnvironment","环境","cube",-180,-34,()=>SetPlacement(AeroWindowAnchor.Follow));
        HandButton=Button("PlaceHand","手部","hand-open",-108,-34,()=>SetPlacement(AeroWindowAnchor.Hand));
        ViewButton=Button("PlaceView","面前","vr-headset",-36,-34,()=>SetPlacement(AeroWindowAnchor.View));
        UndoButton=Button("LayoutUndo","撤销","undo",36,-34,Undo);
        ResetButton=Button("LayoutReset","复位","rotate-ccw",108,-34,ResetSelected);
        DoneButton=Button("LayoutDone","完成","check",180,-34,EndEdit);
        PinButton=AeroHudView.ActionButton(detail,"EnvironmentPin","",Vector2.zero,new Vector2(56,56),new Vector3(64,64,8));AeroUiIcons.Apply(PinButton,"pushpin-off",36);PinButton.onClick.AddListener(TogglePin);buttons.Add(PinButton);
        sizeLabel=AeroHudView.Text(detail,"ResizeValue","",Vector2.zero,new Vector2(140,40),24,AeroUiFonts.Interface,AeroUiTokens.Ink,TextAlignmentOptions.Right,0,0);
        main.Follow.SnapToView();ApplyAttached(small);SetEditUI(false);
    }
    Panel MakePanel(Transform t,HudSoftFollow f,AeroWindowAnchor anchor,bool compact)
    {
        var p=new Panel{Root=t,Follow=f,Anchor=anchor,Canvas=t.GetComponent<Canvas>()};
        var rect=(RectTransform)t;var size=rect.rect.size;
        p.Move=Handle(t,"MoveHandle",new Vector2(0,size.y/2+34),new Vector2(Mathf.Min(200,size.x-100),52),"grip-vertical",compact,false);
        p.Resize=Handle(t,"ResizeHandle",new Vector2(size.x/2+32,-size.y/2-32),new Vector2(56,56),"move-diagonal",compact,true);
        return p;
    }
    AeroDragHandle Handle(Transform parent,string name,Vector2 position,Vector2 size,string icon,bool compact,bool resize)
    {
        var image=AeroHudView.AddPanelImage(parent,name,AeroUiSprites.Pill,position,size,AeroUiTokens.ButtonFill);
        image.gameObject.AddComponent<BoxCollider>().size=new Vector3(size.x+8,size.y+8,8);
        AeroHudView.AddPanelImage(image.transform,"Icon",AeroUiIcons.Get(icon),Vector2.zero,new Vector2(36,36),AeroUiTokens.Ink);
        var h=image.gameObject.AddComponent<AeroDragHandle>();h.Compact=compact;h.Resize=resize;h.gameObject.SetActive(false);return h;
    }
    AeroDockZone LegacyZone(AeroWindowAnchor anchor){var go=new GameObject("Legacy"+anchor);var z=go.AddComponent<AeroDockZone>();z.Anchor=anchor;go.SetActive(false);return z;}
    Button Button(string name,string label,string icon,float x,float y,UnityEngine.Events.UnityAction action)
    {
        var b=AeroHudView.ActionButton(toolbar,name,label,new Vector2(x,y),new Vector2(56,52),new Vector3(64,60,8));AeroUiIcons.Apply(b,icon,36);b.onClick.AddListener(action);buttons.Add(b);return b;
    }
    public void BeginEdit(bool isSky){if(isSky){owner.RecenterSky();return;}if(Dragging)CancelDrag("");owner.ShowDetailForLayout();selected=main;Editing=true;SetEditUI(true);owner.ClearPresses();}
    public void EndEdit(){if(Dragging)CancelDrag("");Editing=false;SetEditUI(false);owner.ClearPresses();owner.RefreshSettings();}
    public void SelectPanel(bool compact)
    {
        if(Dragging||compact&&!owner.Compact.gameObject.activeSelf)return;
        selected=compact?small:main;SetEditUI(Editing);owner.ClearPresses();
    }
    void SetEditUI(bool visible)
    {
        foreach(var p in panels){p.Canvas.sortingOrder=visible&&p==selected?10:0;p.Move.gameObject.SetActive(visible&&p==selected);p.Resize.gameObject.SetActive(visible&&p==selected);}
        toolbar.SetParent(selected.Root,false);toolbar.anchoredPosition=new Vector2(0,-((RectTransform)selected.Root).rect.height/2-120);
        PinButton.transform.SetParent(selected.Root,false);((RectTransform)PinButton.transform).anchoredPosition=new Vector2(((RectTransform)selected.Root).rect.width/2-28,((RectTransform)selected.Root).rect.height/2+34);
        sizeLabel.transform.SetParent(selected.Root,false);sizeLabel.rectTransform.anchoredPosition=new Vector2(((RectTransform)selected.Root).rect.width/2-140,-((RectTransform)selected.Root).rect.height/2-32);
        toolbar.gameObject.SetActive(visible);PinButton.gameObject.SetActive(visible&&IsEnvironment(selected));sizeLabel.gameObject.SetActive(false);
        main.Root.GetComponent<AeroHudView>().SetLayoutEditing(visible);RefreshEditor();Physics.SyncTransforms();
    }
    static bool IsEnvironment(Panel p)=>p.Anchor==AeroWindowAnchor.Follow||p.Anchor==AeroWindowAnchor.World;
    void RefreshEditor()
    {
        if(toolbar==null)return;nameLabel.text=SelectedCompact?"小型读数":"主卡";
        HandButton.interactable=!Dragging&&owner.HandEntry.AnchorTracked;
        SelectCompactButton.interactable=!Dragging&&owner.Compact.gameObject.activeSelf;
        foreach(var b in buttons)if(b!=HandButton&&b!=SelectCompactButton)b.interactable=!Dragging;
        UndoButton.interactable=!Dragging&&selected.HasUndo;
        SpeedButton.gameObject.SetActive(Editing&&selected.Anchor==AeroWindowAnchor.Follow);AeroUiIcons.Set(SpeedButton,selected.Speed==HudSoftFollow.FollowMode.Quick?"fast-forward":"arrow-right");
        PinButton.gameObject.SetActive(Editing&&IsEnvironment(selected));AeroUiIcons.Set(PinButton,selected.Anchor==AeroWindowAnchor.World?"pushpin":"pushpin-off");
        foreach(var b in buttons){bool active=b==EnvironmentButton&&IsEnvironment(selected)||b==HandButton&&selected.Anchor==AeroWindowAnchor.Hand||b==ViewButton&&selected.Anchor==AeroWindowAnchor.View||b==PinButton&&selected.Anchor==AeroWindowAnchor.World||b==SelectMainButton&&!SelectedCompact||b==SelectCompactButton&&SelectedCompact;
            b.GetComponent<Image>().color=!b.interactable?new Color(.08f,.11f,.12f,.5f):b==hoverLeft||b==hoverRight?AeroUiTokens.ButtonHover:active?new Color(.2f,.31f,.28f,1):AeroUiTokens.ButtonFill;}
    }
    Pose Reference(Panel p)=>p.Anchor==AeroWindowAnchor.Hand?owner.HandEntry.ContactSurface:new Pose(eye.transform.position,eye.transform.rotation);
    static Pose ToLocal(Pose basis,Transform t)=>new Pose(Quaternion.Inverse(basis.rotation)*(t.position-basis.position),Quaternion.Inverse(basis.rotation)*t.rotation);
    void ApplyAttached(Panel p){var basis=Reference(p);p.Root.SetPositionAndRotation(basis.position+basis.rotation*p.Local.position,basis.rotation*p.Local.rotation);}
    void Remember(Panel p){if(p.Anchor==AeroWindowAnchor.Hand||p.Anchor==AeroWindowAnchor.View)p.Local=ToLocal(Reference(p),p.Root);else if(p.Anchor==AeroWindowAnchor.Follow)p.Follow.CaptureOffset();p.Saved[(int)p.Anchor]=p.Anchor==AeroWindowAnchor.World?new Pose(p.Root.position,p.Root.rotation):p.Anchor==AeroWindowAnchor.Follow?p.Follow.Offset:p.Local;p.HasSaved[(int)p.Anchor]=true;}
    Snapshot Capture(Panel p)=>new Snapshot{World=new Pose(p.Root.position,p.Root.rotation),Local=p.Local,Offset=p.Follow.Offset,Scale=p.Root.localScale,Anchor=p.Anchor,Distance=p.Follow.Distance,Size=p.Follow.UserScale,Mode=p.Follow.Mode,Speed=p.Speed,Pinned=p.Pinned,Saved=(Pose[])p.Saved.Clone(),HasSaved=(bool[])p.HasSaved.Clone()};
    void Restore(Panel p,Snapshot s){p.Anchor=s.Anchor;p.Local=s.Local;p.Pinned=s.Pinned;p.Speed=s.Speed;p.Saved=(Pose[])s.Saved.Clone();p.HasSaved=(bool[])s.HasSaved.Clone();p.Follow.RestoreOptions(s.Distance,s.Size,s.Offset,s.Mode);p.Root.SetPositionAndRotation(s.World.position,s.World.rotation);p.Root.localScale=s.Scale;p.Follow.SyncView();SetRendering(p,p.Anchor!=AeroWindowAnchor.Hand||owner.HandEntry.AnchorTracked);}
    void SaveUndo(Panel p){p.Undo=Capture(p);p.HasUndo=true;}
    public void SetPlacement(AeroWindowAnchor mode)
    {
        if(Dragging||AeroValueSlider.Active!=null)return;
        if(mode==AeroWindowAnchor.Hand&&!owner.HandEntry.AnchorTracked)return;
        if(mode==AeroWindowAnchor.Follow)mode=selected.Pinned?AeroWindowAnchor.World:AeroWindowAnchor.Follow;
        if(selected.Anchor==mode)return;SaveUndo(selected);Remember(selected);selected.Anchor=mode;
        selected.Follow.SetMode(mode==AeroWindowAnchor.World?HudSoftFollow.FollowMode.Fixed:selected.Speed);
        if(mode==AeroWindowAnchor.Hand||mode==AeroWindowAnchor.View)
        {
            selected.Local=selected.HasSaved[(int)mode]?selected.Saved[(int)mode]:mode==AeroWindowAnchor.Hand?new Pose(new Vector3(0,.18f,-.12f),Quaternion.identity):ToLocal(Reference(selected),selected.Root);
            ApplyAttached(selected);
        }
        else if(selected.HasSaved[(int)mode])
        {
            var saved=selected.Saved[(int)mode];
            if(mode==AeroWindowAnchor.World)selected.Root.SetPositionAndRotation(saved.position,saved.rotation);
            else{selected.Follow.RestoreOptions(selected.Follow.Distance,selected.Follow.UserScale,saved,selected.Speed);selected.Follow.ApplyOffset();}
        }
        else if(mode==AeroWindowAnchor.Follow)selected.Follow.CaptureOffset();
        SetRendering(selected,true);owner.ClearPresses();RefreshEditor();owner.RefreshSettings();Physics.SyncTransforms();
    }
    public void TogglePin()
    {
        if(!IsEnvironment(selected)||Dragging)return;SaveUndo(selected);Remember(selected);
        selected.Pinned=selected.Anchor!=AeroWindowAnchor.World;selected.Anchor=selected.Pinned?AeroWindowAnchor.World:AeroWindowAnchor.Follow;
        selected.Follow.SetMode(selected.Pinned?HudSoftFollow.FollowMode.Fixed:selected.Speed);
        if(!selected.Pinned)selected.Follow.CaptureOffset();owner.ClearPresses();RefreshEditor();owner.RefreshSettings();
    }
    public void ToggleSpeed(){if(Dragging||selected.Anchor!=AeroWindowAnchor.Follow)return;SaveUndo(selected);selected.Speed=selected.Speed==HudSoftFollow.FollowMode.Soft?HudSoftFollow.FollowMode.Quick:HudSoftFollow.FollowMode.Soft;selected.Follow.SetMode(selected.Speed);owner.ClearPresses();RefreshEditor();owner.RefreshSettings();}
    public void StartDrag(string hand,AeroDragHandle handle,Ray ray,float distance)
    {
        if(!Editing||Dragging||AeroValueSlider.Active!=null||handle==null||handle.Sky||handle.Compact!=SelectedCompact)return;
        if(selected.Anchor==AeroWindowAnchor.Hand&&!owner.HandEntry.AnchorTracked)return;
        before=Capture(selected);resizing=handle.Resize;dragOwner=hand;holdTime=0;holdDistance=Mathf.Clamp(distance,.05f,4.5f);grabOffset=selected.Root.position-ray.GetPoint(holdDistance);
        var rect=(RectTransform)selected.Root;Vector3 corner=selected.Root.TransformPoint(new Vector3(rect.rect.xMax,rect.rect.yMin,0));
        pivot=selected.Root.TransformPoint(new Vector3(rect.rect.xMin,rect.rect.yMax,0));diagonal=(corner-pivot).normalized;resizePlane=new Plane(selected.Root.forward,selected.Root.position);
        if(resizing){if(!resizePlane.Raycast(ray,out float d)){dragOwner=null;return;}startProjection=Vector3.Dot(ray.GetPoint(d)-pivot,diagonal);if(startProjection<=.001f){dragOwner=null;return;}}
        RefreshEditor();
    }
    public void StepDrag(bool valid,bool pressed,Ray ray,AeroDockZone unused,float dt)
    {
        if(!Dragging)return;
        if(!valid||selected.Anchor==AeroWindowAnchor.Hand&&!owner.HandEntry.AnchorTracked){CancelDrag("");return;}
        if(!pressed)
        {
            if(holdTime<.15f){CancelDrag("");return;}
            selected.Undo=before;selected.HasUndo=true;Remember(selected);dragOwner=null;sizeLabel.gameObject.SetActive(false);RefreshEditor();owner.ClearPresses();owner.RefreshSettings();return;
        }
#if UNITY_EDITOR
        if(DeltaForChecks>0)dt=DeltaForChecks;
#endif
        holdTime+=Mathf.Clamp(dt,0,.05f);if(holdTime<.15f)return;
        if(resizing)
        {
            if(!resizePlane.Raycast(ray,out float d)||d<0||d>5)return;
            float ratio=Vector3.Dot(ray.GetPoint(d)-pivot,diagonal)/startProjection;
            float size=Mathf.Clamp(before.Size*ratio,HudSoftFollow.MinScale,HudSoftFollow.MaxScale);
            selected.Follow.SetUserScale(size);selected.Root.localScale=before.Scale*(size/before.Size);
            var rect=(RectTransform)selected.Root;selected.Root.position+=pivot-selected.Root.TransformPoint(new Vector3(rect.rect.xMin,rect.rect.yMax,0));
            sizeLabel.gameObject.SetActive(true);sizeLabel.text=Mathf.RoundToInt(size*100)+"%";
        }
        else selected.Root.SetPositionAndRotation(ray.GetPoint(holdDistance)+grabOffset,before.World.rotation);
        Physics.SyncTransforms();
    }
    public void CancelDrag(string reason){if(!Dragging)return;dragOwner=null;Restore(selected,before);sizeLabel.gameObject.SetActive(false);RefreshEditor();}
    public void Undo(){if(Dragging)CancelDrag("");if(!selected.HasUndo)return;Restore(selected,selected.Undo);selected.HasUndo=false;owner.ClearPresses();RefreshEditor();owner.RefreshSettings();Physics.SyncTransforms();}
    public void ResetSelected(){if(Dragging)CancelDrag("");SaveUndo(selected);ResetPanel(selected);SetEditUI(Editing);owner.ClearPresses();owner.RefreshSettings();}
    void ResetPanel(Panel p){p.Pinned=false;p.Speed=HudSoftFollow.FollowMode.Soft;p.HasSaved=new bool[4];p.Follow.SetUserScale(1);if(p==main){p.Anchor=AeroWindowAnchor.Follow;p.Follow.ResetOptions();}else{p.Anchor=AeroWindowAnchor.View;p.Follow.ConfigureScale(.0009f/1.05f,1.05f);p.Follow.SetUserScale(1);p.Local=new Pose(new Vector3(owner.Compact.Side==AeroCompactHud.Placement.Right?.42f:-.42f,-.14f,1.05f),Quaternion.identity);ApplyAttached(p);}SetRendering(p,true);}
    public void ResetAll(){if(Dragging)CancelDrag("");ResetPanel(main);ResetPanel(small);main.HasUndo=small.HasUndo=false;if(Editing)SetEditUI(true);Physics.SyncTransforms();}
    void SetRendering(Panel p,bool show)
    {
        p.Canvas.enabled=show;p.Colliders=p.Root.GetComponentsInChildren<Collider>(true);
        foreach(var c in p.Colliders)if(c!=null)c.enabled=show;
    }
    public void PrepareFrame()
    {
        if(owner==null)return;
        foreach(var p in panels)
        {
            if(p.Anchor==AeroWindowAnchor.Hand)
            {
                bool tracked=owner.HandEntry.AnchorTracked;
                if(p.Tracked!=tracked){p.Tracked=tracked;SetRendering(p,tracked);owner.ClearPresses();if(Dragging&&p==selected)CancelDrag("");}
            }
            if((Dragging&&p==selected)||AeroValueSlider.Active!=null){p.Follow.SyncView();continue;}
            if((p.Anchor==AeroWindowAnchor.Hand&&owner.HandEntry.AnchorTracked)||p.Anchor==AeroWindowAnchor.View)ApplyAttached(p);
        }
        if(Editing)RefreshEditor();
    }
    public void Tick(float dt,bool interacting)
    {foreach(var p in panels)if(p.Anchor==AeroWindowAnchor.Follow){if(Editing||Dragging||AeroValueSlider.Active!=null)p.Follow.SyncView();else p.Follow.Tick(dt,interacting);}}
    public bool Raycast(Ray ray,float distance,out RaycastHit hit)
    {
        if(!Editing&&!Dragging)return Physics.Raycast(ray,out hit,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        int count=Physics.RaycastNonAlloc(ray,hits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);hit=default;float nearest=distance+1;bool found=false;
        for(int i=0;i<count;i++)
        {
            var t=hits[i].transform;
            if(Dragging&&t.IsChildOf(selected.Root))continue;
            if(!Dragging)
            {
                var handle=t.GetComponent<AeroDragHandle>();var button=t.GetComponent<Button>();
                bool control=(handle!=null&&handle.Compact==SelectedCompact)||(button!=null&&button.IsInteractable()&&(ToolbarButton(button)||button==owner.Compact.OpenButton));
                if(!control)continue;
            }
            if(hits[i].distance<nearest){nearest=hits[i].distance;hit=hits[i];found=true;}
        }
        return found;
    }
    public void ReleaseToFollow(){main.Anchor=AeroWindowAnchor.Follow;main.Pinned=false;main.Follow.SetMode(main.Speed);main.Follow.CaptureOffset();main.HasUndo=false;SetRendering(main,true);}
    public void SetAnchorFromFollowMode(){main.Anchor=main.Follow.Mode==HudSoftFollow.FollowMode.Fixed?AeroWindowAnchor.World:AeroWindowAnchor.Follow;main.Pinned=main.Anchor==AeroWindowAnchor.World;if(!main.Pinned)main.Speed=main.Follow.Mode;main.Follow.CaptureOffset();main.HasUndo=false;SetRendering(main,true);}
    public void PlaceOpenedCard(){PrepareFrame();main.Follow.SyncView();}
    public void RecalledSky(){}
    public void InvalidateMainUndo(){main.HasUndo=false;}
    public void CompactPreset(AeroCompactHud.Placement side){if(side==AeroCompactHud.Placement.Hidden)return;small.HasUndo=false;small.Anchor=AeroWindowAnchor.View;small.Local=new Pose(new Vector3(side==AeroCompactHud.Placement.Right?.42f:-.42f,-.14f,1.05f),Quaternion.identity);ApplyAttached(small);SetRendering(small,true);}
    public void Recall(){if(Editing)EndEdit();if(Dragging)CancelDrag("");ReleaseToFollow();main.Follow.SnapToView();if(NeedsRecallFor(small)){small.Anchor=AeroWindowAnchor.View;small.Local=new Pose(new Vector3(-.42f,-.14f,1.05f),Quaternion.identity);small.HasUndo=false;ApplyAttached(small);SetRendering(small,true);}}
    public bool ToolbarButton(Button b)=>b!=null&&(b.transform.IsChildOf(toolbar)||b==PinButton);
    public bool Owns(Transform t)=>t.IsChildOf(main.Root)||t.IsChildOf(small.Root);
    public void SetHover(Button left,Button right){if(hoverLeft==left&&hoverRight==right)return;hoverLeft=left;hoverRight=right;if(Editing)RefreshEditor();}
    void OnDestroy(){foreach(var z in new[]{WorldZone,ViewZone,HandZone})if(z!=null)Destroy(z.gameObject);}
}
