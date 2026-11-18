using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Placement owns the reference frame; a manipulation only edits its pose/size.
public class AeroWindowLayout : MonoBehaviour
{
    public bool Editing {get;private set;}
    public int LayerMask {get;private set;} = 7;
    bool LayerEnabled(AeroEditLayer layer)=>(LayerMask&(1<<(int)layer))!=0;
    bool toolsRequested,toolsBeforeDrag;
    public bool Dragging=>dragOwner!=null;
    public bool NearDragging=>Dragging&&nearDragging;
    public bool DragActive=>Dragging&&holdTime>=.15f;
    public string DragOwner=>dragOwner;
    public bool SelectedCompact=>selected!=main;
    public string SelectedId=>selected.Key;
    public bool HasVisibleWidgets { get {foreach(var p in panels)if(p.Key!="main"&&p.Key!="legacy"&&p.Root.gameObject.activeSelf&&p.Canvas.enabled)return true;return false;} }
    public bool SurfacesAvailable {get{if(owner==null||owner.Widgets==null||owner.Widgets.CatalogRoot==null||panels==null)return false;foreach(var p in panels)if(p.Root==null||p.Canvas==null)return false;return true;}}
    // Scene teardown may destroy independent canvases before the owner is disabled.
    public void AbandonForTeardown(){dragOwner=null;nearDragging=false;creating=false;Editing=false;}
    public Transform WidgetRoot(string key)=>FindPanel(key)?.Root;
    public Button WidgetClose(string key)=>FindPanel(key)?.Close;
    public AeroWindowAnchor MainAnchor=>main.Anchor;
    public AeroWindowAnchor CompactAnchor=>small.Anchor;
    public bool HasSkyPlacement=>false;
    public AeroWindowAnchor SkyAnchor=>AeroWindowAnchor.World;
    // Old Editor helpers remain compilable; these zones never enter the new UI.
    public AeroDockZone WorldZone,ViewZone,HandZone;
    public AeroDragHandle CardHandle,SkyHandle,ResizeHandle,CompactHandle,CompactResizeHandle,DepthHandle,CompactDepthHandle;
    public Button DoneButton,UndoButton,ResetButton,PinButton,EnvironmentButton,HandButton,ViewButton,SelectMainButton,SelectCompactButton,SpeedButton;
    public string MainLabel=>main.Anchor==AeroWindowAnchor.Hand?"HAND DOCK":main.Anchor==AeroWindowAnchor.View?"VIEW DOCK":main.Follow.ModeLabel;
    public float MainScale=>main.Follow.UserScale;
    public float CompactScale=>small.Follow.UserScale;
    public bool NeedsRecall {get{if(!Editing)return main.Root.gameObject.activeSelf&&NeedsRecallFor(main);foreach(var p in panels)if(ActiveInScope(p)&&NeedsRecallFor(p))return true;return false;}}
#if UNITY_EDITOR
    public float DeltaForChecks;
#endif
    sealed class Panel
    {
        public string Key,Title;public Button Close,Body;public bool Placed;public Transform Root;public HudSoftFollow Follow;public AeroWindowAnchor Anchor;
        public Pose Local;public bool Pinned,Tracked=true;public HudSoftFollow.FollowMode Speed=HudSoftFollow.FollowMode.Soft;
        public Pose[] Saved=new Pose[4];public bool[] HasSaved=new bool[4];
        public Canvas Canvas;public Collider[] Colliders;public AeroDragHandle Move,Resize,Depth;
        public Snapshot Undo;public bool HasUndo;
    }
    sealed class Snapshot
    {
        public Pose World,Local,Offset;public Vector3 Scale;public AeroWindowAnchor Anchor;
        public float Distance,Size;public HudSoftFollow.FollowMode Mode,Speed;public bool Pinned,Placed;
        public Pose[] Saved;public bool[] HasSaved;
    }
    PinchHudExperiment owner;Camera eye;Panel main,small,selected;Panel[] panels;
    RectTransform toolbar;TextMeshProUGUI sizeLabel;
    Collider[] toolbarColliders;
    readonly List<Button> buttons=new List<Button>();
    string dragOwner;float holdTime,startProjection,startRadius,minRadius,maxRadius;
    bool resizing,depthMoving,handPlane,creating,wasVisible,paused,catalogMoving;Pose catalogMoveBefore;Vector3 catalogMoveHit;Plane catalogMovePlane;Snapshot catalogBefore;Pose catalogSpawn;Panel creatingPanel;float handHeight;Vector3 handNormal;Vector3 pivot,diagonal,orbitCenter,radial,startOrigin,orbitHit;Plane resizePlane,orbitPlane;Snapshot before;
    readonly RaycastHit[] hits=new RaycastHit[64];
    Button hoverLeft,hoverRight;
    bool nearDragging,suppressChanges;
    Vector3 nearDirection;
    void Changed(){if(!suppressChanges&&owner.Profiles!=null)owner.Profiles.MarkChanged();}
    bool NeedsRecallFor(Panel p)=>p.Anchor==AeroWindowAnchor.Hand&&!owner.HandEntry.AnchorTracked||Vector3.Angle(eye.transform.forward,p.Root.position-eye.transform.position)>70;
    public void Initialize(PinchHudExperiment hud,Camera camera,Transform detail,Transform sky)
    {
        owner=hud;eye=camera;
        main=MakePanel(detail,owner.Follow,AeroWindowAnchor.Follow,false);
        var compact=owner.Compact.transform;compact.SetParent(null,true);
        var f=compact.gameObject.AddComponent<HudSoftFollow>();f.Initialize(eye.transform);f.ConfigureScale(.0009f/1.05f,1.05f);
        small=MakePanel(compact,f,AeroWindowAnchor.View,true);small.Local=new Pose(new Vector3(-.42f,-.14f,1.05f),Quaternion.identity);
        main.Key="main";main.Title="主面板";main.Placed=true;small.Key="legacy";small.Title="旧小窗";
        var all=new List<Panel>{main,small};
        foreach(var item in owner.Widgets.Items)
        {
            var follow=item.Root.gameObject.AddComponent<HudSoftFollow>();follow.Initialize(eye.transform);follow.ConfigureScale(.00075f/.85f,.85f);
            var panel=MakePanel(item.Root,follow,AeroWindowAnchor.Follow,false);panel.Key=item.Id;panel.Title=item.Title;
            panel.Close=AeroHudView.ActionButton(item.Root,"CloseWidget_"+item.Id,"关闭",new Vector2(116,46),new Vector2(40,40),new Vector3(48,48,8));AeroUiIcons.Apply(panel.Close,"close",28);
            string key=item.Id;panel.Close.onClick.AddListener(()=>CloseWidget(key));all.Add(panel);
        }
        panels=all.ToArray();
        foreach(var panel in panels){panel.Move.PanelId=panel.Depth.PanelId=panel.Resize.PanelId=panel.Key;}
        main.Close=main.Root.GetComponent<AeroHudView>().CloseButton;
        var mainClose=(RectTransform)main.Close.transform;mainClose.anchoredPosition=new Vector2(348,184);mainClose.sizeDelta=new Vector2(44,44);main.Close.GetComponent<BoxCollider>().size=new Vector3(52,52,8);AeroUiIcons.Apply(main.Close,"close",28);
        owner.Widgets.Requested=AddWidget;owner.Widgets.Finished=EndEdit;
        owner.Widgets.Hidden=HideTools;
        owner.Widgets.LayerRequested=ToggleLayer;owner.Widgets.RecallRequested=RecallCatalog;
owner.Compact.PlacementChanged=CompactPreset;selected=main;CardHandle=main.Move;ResizeHandle=main.Resize;CompactHandle=small.Move;CompactResizeHandle=small.Resize;DepthHandle=main.Depth;CompactDepthHandle=small.Depth;
        WorldZone=LegacyZone(AeroWindowAnchor.World);ViewZone=LegacyZone(AeroWindowAnchor.View);HandZone=LegacyZone(AeroWindowAnchor.Hand);
        toolbar=AeroHudView.NewRect("PlacementChoices",detail,Vector2.zero,new Vector2(432,72));
        Backing(toolbar,new Vector2(432,72),Vector2.zero);
        EnvironmentButton=Button(toolbar,"PlaceEnvironment","环境","cube",-144,0,136,()=>SetPlacement(AeroWindowAnchor.Follow));
        HandButton=Button(toolbar,"PlaceHand","手部","hand-open",0,0,136,()=>SetPlacement(AeroWindowAnchor.Hand));
        ViewButton=Button(toolbar,"PlaceView","面前","vr-headset",144,0,136,()=>SetPlacement(AeroWindowAnchor.View));
        toolbarColliders=toolbar.GetComponentsInChildren<Collider>(true);
        DoneButton=owner.Widgets.DoneButton;
        // Retired sidebar controls remain absent; Done belongs to the shared catalog.
        sizeLabel=AeroHudView.Text(detail,"ResizeValue","",Vector2.zero,new Vector2(140,40),24,AeroUiFonts.Interface,AeroUiTokens.Ink,TextAlignmentOptions.Right,0,0);
        main.Follow.SnapToView();ApplyAttached(small);SetEditUI(false);
    }
    Panel MakePanel(Transform t,HudSoftFollow f,AeroWindowAnchor anchor,bool compact)
    {
        var p=new Panel{Root=t,Follow=f,Anchor=anchor,Canvas=t.GetComponent<Canvas>()};
        var rect=(RectTransform)t;var size=rect.rect.size;
        p.Body=t.gameObject.AddComponent<Button>();p.Body.transition=Selectable.Transition.None;
        var bodyCollider=t.GetComponent<BoxCollider>();if(bodyCollider==null)bodyCollider=t.gameObject.AddComponent<BoxCollider>();
        bodyCollider.size=new Vector3(size.x,size.y,3);p.Body.onClick.AddListener(()=>SelectWidget(p.Key));
        p.Move=Handle(t,"OrbitHandle",new Vector2(-82,size.y/2+36),new Vector2(156,56),"grip-vertical",compact,false);
        HandleLabel(p.Move,"环绕");
        p.Depth=Handle(t,"DepthHandle",new Vector2(82,size.y/2+36),new Vector2(156,56),"crosshair",compact,false);p.Depth.Depth=true;
        HandleLabel(p.Depth,"远近");
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
    void Backing(Transform parent,Vector2 size,Vector2 position)
    {
        var image=AeroHudView.AddPanelImage(parent,"Backing",Resources.Load<Sprite>("JetStyle/RoundedRect"),position,size,new Color(.04f,.07f,.08f,.96f));
        image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=8;
    }
    void HandleLabel(AeroDragHandle handle,string label)
    {
        ((RectTransform)handle.transform.Find("Icon")).anchoredPosition=new Vector2(-50,0);
        AeroHudView.Text(handle.transform,"Caption",label,new Vector2(18,0),new Vector2(98,44),22,AeroUiFonts.Interface,AeroUiTokens.Ink,TextAlignmentOptions.Left,0,0,true);
    }
    Button Button(Transform parent,string name,string label,string icon,float x,float y,float width,UnityEngine.Events.UnityAction action)
    {
        var b=AeroHudView.ActionButton(parent,name,label,new Vector2(x,y),new Vector2(width,48),new Vector3(width+4,52,8));AeroUiIcons.Label(b,icon,label);b.onClick.AddListener(action);buttons.Add(b);return b;
    }
    void Caption(Button button,string value){var text=button.GetComponentInChildren<TextMeshProUGUI>(true);if(text.text!=value)text.text=value;}
    static AeroEditLayer LayerOf(Panel p)=>p.Anchor==AeroWindowAnchor.Hand?AeroEditLayer.Hand:p.Anchor==AeroWindowAnchor.View?AeroEditLayer.View:AeroEditLayer.Environment;
    bool ActiveInScope(Panel p)=>p.Root.gameObject.activeSelf&&(!Editing||p==main||p==small||LayerEnabled(LayerOf(p)));
    void EnsureSelection()
    {
        if(selected!=null&&ActiveInScope(selected))return;
        foreach(var p in panels)if(p!=small&&ActiveInScope(p)){selected=p;return;}
        selected=main;
    }
    void RefreshTools(bool reposition=false)=>owner.Widgets.SetOpen(Editing&&toolsRequested&&!paused&&!Dragging,reposition);
    void HideTools(){if(!Editing||Dragging)return;toolsRequested=false;RefreshTools();owner.ClearPresses();}
    public void ToggleTools(){if(!Editing)return;if(owner.Profiles!=null&&owner.Profiles.ModalOpen||Dragging)return;if(owner.Widgets.IsOpen){toolsRequested=false;RefreshTools();owner.ClearPresses();}else OpenTools();}
    public void OpenTools()
    {
        if(!Editing||owner.Profiles!=null&&owner.Profiles.ModalOpen||Dragging)return;
        toolsRequested=true;RefreshTools(true);SetEditUI(true);
        if(owner.Compact!=null)owner.Compact.SetFallbackVisible(false);
        owner.ClearPresses();
    }
    public void BeginEdit(bool isSky)
    {
        if(isSky){owner.RecenterSky();return;}if(Dragging)CancelDrag("");
        if(!Editing&&owner.IsCardOpen){owner.ShowDetailForLayout();selected=main;}
        bool entered=!Editing;Editing=true;toolsRequested=false;EnsureSelection();SetEditUI(true);RefreshTools(true);owner.ClearPresses();
        if(owner.Widgets!=null)owner.Widgets.SetOpen(false);
        if(entered&&owner.Widgets!=null)owner.Widgets.SetKeepOpen(false);
        if(entered&&owner.Feedback!=null)owner.Feedback.Notify("组件工具已收起\n左手手背轻触设置，右手抬起后打开",6f);
    }
    public void EndEdit(){if(Dragging)CancelDrag("");Editing=false;toolsRequested=false;if(owner.Profiles!=null)owner.Profiles.EndEditing();RefreshTools();SetEditUI(false);owner.ClearPresses();owner.RefreshSettings();}
    void ToggleLayer(AeroEditLayer layer){if(!Editing)return;SetLayerMask(LayerMask^(1<<(int)layer));}
    public void SetLayerMask(int mask)
    {
        if(!Editing||owner.Profiles!=null&&owner.Profiles.ModalOpen)return;
        if(Dragging)CancelDrag("");LayerMask=Mathf.Clamp(mask,0,7);Panel keep=selected;if(keep!=null&&keep!=main&&keep!=small&&!LayerEnabled(LayerOf(keep)))keep=null;if(keep==null)EnsureSelection();else selected=keep;SetEditUI(Editing);owner.Widgets.SetEditingView(LayerMask);foreach(var p in panels)SetRendering(p,!paused&&(p.Anchor!=AeroWindowAnchor.Hand||owner.HandEntry.AnchorTracked));owner.ClearPresses();
    }
    void RecallCatalog(){if(owner.Widgets!=null)owner.Widgets.RecallPlacement();}
    public void ToolActionCompleted()
    {if(!Editing||owner.Profiles!=null&&owner.Profiles.ModalOpen)return;if(!owner.Widgets.KeepOpen)toolsRequested=false;RefreshTools();owner.ClearPresses();}
    Panel FindPanel(string key){foreach(var p in panels)if(p.Key==key)return p;return null;}
    public void SelectPanel(bool compact){if(compact)AddWidget("heart");else SelectWidget("main");}
    public void SelectWidget(string key)
    {if(!Editing||Dragging)return;var p=FindPanel(key);if(p==null||!p.Root.gameObject.activeSelf)return;selected=p;SetEditUI(Editing);owner.ClearPresses();}
    public void AddWidget(string key)=>AddWidget(key,false);
    bool AddWidget(string key,bool fromDrag)
    {
        if(!Editing||Dragging)return false;var p=FindPanel(key);if(p==null||p==main||p==small)return false;
        bool newlyShown=!p.Root.gameObject.activeSelf;
        if(newlyShown&&LayerMask==0){owner.Feedback.Notify("先打开一层再添加组件");return false;}
        var source=selected!=null&&selected.Root.gameObject.activeSelf&&LayerEnabled(LayerOf(selected))?LayerOf(selected):
            LayerEnabled(AeroEditLayer.Environment)?AeroEditLayer.Environment:LayerEnabled(AeroEditLayer.Hand)?AeroEditLayer.Hand:AeroEditLayer.View;
        var target=newlyShown?(source==AeroEditLayer.Hand?AeroWindowAnchor.Hand:source==AeroEditLayer.View?AeroWindowAnchor.View:AeroWindowAnchor.Follow):p.Anchor;
        if(target==AeroWindowAnchor.Hand&&!owner.HandEntry.AnchorTracked){owner.Feedback.Notify("先抬起左手");return false;}
        bool changed=newlyShown||!p.Placed||NeedsRecallFor(p);
        bool movedLayer=p.Anchor!=target;p.Anchor=target;
        if(newlyShown){p.Pinned=target==AeroWindowAnchor.World;p.Follow.SetMode(p.Pinned?HudSoftFollow.FollowMode.Fixed:p.Speed);}
        if(!p.Placed||NeedsRecallFor(p)||movedLayer)
        {
            int i=System.Array.IndexOf(panels,p)-2;
            if(target==AeroWindowAnchor.Hand){p.Local=new Pose(new Vector3((i%2)*.23f,.18f-(i/2)*.14f,-.12f),Quaternion.identity);ApplyAttached(p);}
            else {var slot=new Vector3(i%2==0?.30f:.62f,.24f-(i/2)*.25f,.9f);p.Root.SetPositionAndRotation(eye.transform.TransformPoint(slot),eye.transform.rotation);}
            Remember(p);p.Placed=true;
        }
        owner.Widgets.SetItemVisible(key,true);selected=p;SetEditUI(true);owner.ClearPresses();
        if(changed&&!fromDrag)Changed();if(newlyShown&&!fromDrag)ToolActionCompleted();else RefreshTools();return true;
    }
    public void CloseWidget(string key)
    {
        if(!Editing)return;
        var p=FindPanel(key);if(p==null)return;
        if(Dragging&&selected==p)CancelDrag("");
        if(p==main){main.Close.onClick.Invoke();return;}
        owner.Widgets.SetItemVisible(key,false);
        if(selected==p){EnsureSelection();SetEditUI(Editing);}
        owner.ClearPresses();owner.Widgets.Refresh();Changed();
    }
    public void MainHidden()
    {if(!Editing)return;if(Dragging&&selected==main)CancelDrag("");EnsureSelection();SetEditUI(true);}
    public void SetPaused(bool value)
    {paused=value;RefreshTools();foreach(var p in panels)SetRendering(p,!value&&(p.Anchor!=AeroWindowAnchor.Hand||owner.HandEntry.AnchorTracked));}
    void SetEditUI(bool visible)
    {
        foreach(var p in panels)
        {
            SetRendering(p,!paused&&(p.Anchor!=AeroWindowAnchor.Hand||owner.HandEntry.AnchorTracked));
            bool edit=visible&&p==selected&&ActiveInScope(p)&&p.Key!="legacy";
            p.Canvas.sortingOrder=edit?10:0;
            p.Move.gameObject.SetActive(edit);p.Body.interactable=visible&&p.Key!="legacy";
            p.Depth.gameObject.SetActive(edit);p.Resize.gameObject.SetActive(edit);
            p.Move.GetComponentInChildren<TextMeshProUGUI>().text=p.Anchor==AeroWindowAnchor.Hand?"平移":"环绕";
            p.Depth.GetComponentInChildren<TextMeshProUGUI>().text=p.Anchor==AeroWindowAnchor.Hand?"离手":"远近";
        }
        toolbar.SetParent(selected.Root,false);toolbar.anchoredPosition=new Vector2(0,-((RectTransform)selected.Root).rect.height/2-112);
        sizeLabel.transform.SetParent(selected.Root,false);sizeLabel.rectTransform.anchoredPosition=new Vector2(((RectTransform)selected.Root).rect.width/2-140,-((RectTransform)selected.Root).rect.height/2-32);
        toolbar.gameObject.SetActive(visible&&ActiveInScope(selected));SetToolbarRendering(!paused&&selected.Canvas.enabled);sizeLabel.gameObject.SetActive(false);
        main.Root.GetComponent<AeroHudView>().SetLayoutEditing(visible);var group=main.Close.GetComponent<CanvasGroup>();if(group!=null)group.alpha=1;
        foreach (var p in panels)
        {
            if (p.Close != null)
            {
                bool showClose = p == main || visible;
                p.Close.interactable = showClose;
                p.Close.gameObject.SetActive(showClose);
            }
            SetRendering(p,!paused&&(p.Anchor!=AeroWindowAnchor.Hand||owner.HandEntry.AnchorTracked));
        }
        owner.Widgets.SetEditingView(LayerMask);if(owner.ToolsEntry!=null)owner.ToolsEntry.RefreshAvailability();RefreshEditor();Physics.SyncTransforms();
    }
    static bool IsEnvironment(Panel p)=>p.Anchor==AeroWindowAnchor.Follow||p.Anchor==AeroWindowAnchor.World;
    void RefreshEditor()
    {
        if(toolbar==null)return;HandButton.interactable=Editing&&!Dragging&&owner.HandEntry.AnchorTracked;
        EnvironmentButton.interactable=ViewButton.interactable=Editing&&!Dragging;
        foreach(var b in buttons)
        {
            bool active=b==EnvironmentButton&&IsEnvironment(selected)||b==HandButton&&selected.Anchor==AeroWindowAnchor.Hand||b==ViewButton&&selected.Anchor==AeroWindowAnchor.View;
            b.GetComponent<Image>().color=!b.interactable?new Color(.08f,.11f,.12f,.5f):b==hoverLeft||b==hoverRight?AeroUiTokens.ButtonHover:active?new Color(.2f,.31f,.28f,1):AeroUiTokens.ButtonFill;
        }
    }
    Quaternion Facing(Panel p)
    {
        if(p.Anchor!=AeroWindowAnchor.Hand)return eye.transform.rotation;
        var n=Dragging&&p==selected?handNormal:owner.HandEntry.ContactSurface.rotation*Vector3.forward;
        var up=Vector3.ProjectOnPlane(eye.transform.up,n);if(up.sqrMagnitude<.0001f)up=Vector3.ProjectOnPlane(owner.HandEntry.ContactSurface.rotation*Vector3.up,n);
        return Quaternion.LookRotation(n,up.normalized);
    }
    void Face(Panel p){p.Root.rotation=Facing(p);}
    Pose Reference(Panel p)=>p.Anchor==AeroWindowAnchor.Hand?owner.HandEntry.ContactSurface:new Pose(eye.transform.position,eye.transform.rotation);
    static Pose ToLocal(Pose basis,Transform t)=>new Pose(Quaternion.Inverse(basis.rotation)*(t.position-basis.position),Quaternion.Inverse(basis.rotation)*t.rotation);
    void ApplyAttached(Panel p){var basis=Reference(p);p.Root.SetPositionAndRotation(basis.position+basis.rotation*p.Local.position,Facing(p));}
    void Remember(Panel p){p.Follow.CaptureOffset();if(p.Anchor==AeroWindowAnchor.Hand||p.Anchor==AeroWindowAnchor.View)p.Local=ToLocal(Reference(p),p.Root);p.Saved[(int)p.Anchor]=p.Anchor==AeroWindowAnchor.World?new Pose(p.Root.position,p.Root.rotation):p.Anchor==AeroWindowAnchor.Follow?p.Follow.Offset:p.Local;p.HasSaved[(int)p.Anchor]=true;}
    Snapshot Capture(Panel p)=>new Snapshot{World=new Pose(p.Root.position,p.Root.rotation),Local=p.Local,Offset=p.Follow.Offset,Scale=p.Root.localScale,Anchor=p.Anchor,Distance=p.Follow.Distance,Size=p.Follow.UserScale,Mode=p.Follow.Mode,Speed=p.Speed,Pinned=p.Pinned,Placed=p.Placed,Saved=(Pose[])p.Saved.Clone(),HasSaved=(bool[])p.HasSaved.Clone()};
    void Restore(Panel p,Snapshot s){p.Anchor=s.Anchor;p.Local=s.Local;p.Pinned=s.Pinned;p.Placed=s.Placed;p.Speed=s.Speed;p.Saved=(Pose[])s.Saved.Clone();p.HasSaved=(bool[])s.HasSaved.Clone();p.Follow.RestoreOptions(s.Distance,s.Size,s.Offset,s.Mode,s.Scale.x);p.Root.SetPositionAndRotation(s.World.position,s.World.rotation);p.Root.localScale=s.Scale;p.Follow.SyncView();SetRendering(p,p.Anchor!=AeroWindowAnchor.Hand||owner.HandEntry.AnchorTracked);Face(p);}
    void SaveUndo(Panel p){p.Undo=Capture(p);p.HasUndo=true;}
    public void SetPlacement(AeroWindowAnchor mode)
    {
        if(!Editing||Dragging||!ActiveInScope(selected)||AeroValueSlider.Active!=null)return;
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
        selected.Follow.CaptureOffset();SetRendering(selected,true);Face(selected);SetEditUI(Editing);owner.ClearPresses();RefreshEditor();owner.RefreshSettings();Physics.SyncTransforms();Changed();
    }
    public void TogglePin()
    {
        if(!Editing||!IsEnvironment(selected)||Dragging)return;SaveUndo(selected);Remember(selected);
        selected.Pinned=selected.Anchor!=AeroWindowAnchor.World;selected.Anchor=selected.Pinned?AeroWindowAnchor.World:AeroWindowAnchor.Follow;
        selected.Follow.SetMode(selected.Pinned?HudSoftFollow.FollowMode.Fixed:selected.Speed);
        if(!selected.Pinned)selected.Follow.CaptureOffset();owner.ClearPresses();RefreshEditor();owner.RefreshSettings();Changed();
    }
    public void ToggleSpeed(){if(!Editing||Dragging||selected.Anchor!=AeroWindowAnchor.Follow)return;SaveUndo(selected);selected.Speed=selected.Speed==HudSoftFollow.FollowMode.Soft?HudSoftFollow.FollowMode.Quick:HudSoftFollow.FollowMode.Soft;selected.Follow.SetMode(selected.Speed);owner.ClearPresses();RefreshEditor();owner.RefreshSettings();Changed();}
    // Near joints project onto the same frozen manipulation plane as far rays.
    // The virtual projection is internal geometry; it never redirects the visible ray.
    public bool StartNearDrag(string hand,AeroDragHandle handle,Vector3 point)
    {
        if(Dragging||handle==null||!handle.isActiveAndEnabled||!string.IsNullOrEmpty(handle.CatalogId))return false;
        var p=FindPanel(handle.PanelId);if(p==null)return false;
        var direction=handle.Resize?p.Root.forward:p.Anchor==AeroWindowAnchor.Hand
            ?owner.HandEntry.ContactSurface.rotation*Vector3.forward:(p.Root.position-eye.transform.position).normalized;
        if(direction.sqrMagnitude<.5f)return false;
        StartDrag(hand,handle,new Ray(point-direction,direction),1);
        if(!Dragging||dragOwner!=hand)return false;
        nearDirection=direction;nearDragging=true;holdTime=.15f;return true;
    }
    public void StepNearDrag(bool valid,bool pressed,Vector3 point,float dt)
    {if(NearDragging)StepDrag(valid,pressed,new Ray(point-nearDirection,nearDirection),null,dt);}
    public void StartDrag(string hand,AeroDragHandle handle,Ray ray,float distance)
    {
        if(!Editing||Dragging||AeroValueSlider.Active!=null||handle==null||handle.Sky||owner.Profiles!=null&&owner.Profiles.ModalOpen)return;
        nearDragging=false;toolsBeforeDrag=toolsRequested;
        if(!string.IsNullOrEmpty(handle.CatalogId))
        {
            var target=FindPanel(handle.CatalogId);if(target==null)return;
            catalogBefore=Capture(target);wasVisible=target.Root.gameObject.activeSelf;creatingPanel=target;
            suppressChanges=true;bool added;try{added=AddWidget(handle.CatalogId,true);}finally{suppressChanges=false;}if(!added)return;catalogSpawn=new Pose(selected.Root.position,selected.Root.rotation);if(!catalogBefore.Placed&&selected.Anchor!=AeroWindowAnchor.Hand)selected.Root.SetPositionAndRotation(ray.GetPoint(distance),eye.transform.rotation);
            owner.Widgets.SetOpen(false);creating=true;handle=selected.Move;
        }
        else if(handle.PanelId!=selected.Key)
        {var p=FindPanel(handle.PanelId);if(p==null||!p.Root.gameObject.activeSelf)return;selected=p;SetEditUI(true);}
        if(selected.Anchor==AeroWindowAnchor.Hand&&!owner.HandEntry.AnchorTracked)return;
        before=Capture(selected);resizing=handle.Resize;depthMoving=handle.Depth;dragOwner=hand;holdTime=0;
        orbitCenter=eye.transform.position;startRadius=Vector3.Distance(selected.Root.position,orbitCenter);
        if(startRadius<.01f){CancelDrag("Invalid drag radius");return;}
        radial=(selected.Root.position-orbitCenter)/startRadius;startOrigin=ray.origin;
        minRadius=Mathf.Min(.55f,startRadius);maxRadius=Mathf.Max(2.2f,startRadius);
        handPlane=selected.Anchor==AeroWindowAnchor.Hand;handNormal=owner.HandEntry.ContactSurface.rotation*Vector3.forward;
        handHeight=Vector3.Dot(selected.Root.position-owner.HandEntry.ContactSurface.position,-handNormal);
        orbitPlane=new Plane(handPlane?handNormal:radial,selected.Root.position);
        if(!resizing&&!depthMoving){if(!orbitPlane.Raycast(ray,out float hit)||hit<=0||hit>5){CancelDrag("Invalid drag plane");return;}orbitHit=ray.GetPoint(hit);}
        var rect=(RectTransform)selected.Root;Vector3 corner=selected.Root.TransformPoint(new Vector3(rect.rect.xMax,rect.rect.yMin,0));
        pivot=selected.Root.TransformPoint(new Vector3(rect.rect.xMin,rect.rect.yMax,0));diagonal=(corner-pivot).normalized;resizePlane=new Plane(selected.Root.forward,selected.Root.position);
        if(resizing){if(!resizePlane.Raycast(ray,out float d)){CancelDrag("Invalid resize plane");return;}startProjection=Vector3.Dot(ray.GetPoint(d)-pivot,diagonal);if(startProjection<=.001f){CancelDrag("Invalid resize projection");return;}}
        if(creating)holdTime=.15f;owner.Widgets.SetOpen(false);RefreshEditor();
    }
    public void StepDrag(bool valid,bool pressed,Ray ray,AeroDockZone unused,float dt)
    {
        if(!Dragging)return;
        if(catalogMoving)
        {
            if(!valid){CancelDrag("");return;}
            if(!pressed){catalogMoving=false;dragOwner=null;owner.Widgets.FixPlacement();owner.Widgets.SetOpen(true,false);sizeLabel.gameObject.SetActive(false);RefreshEditor();owner.ClearPresses();return;}
            if(catalogMovePlane.Raycast(ray,out float hit)&&hit>0)owner.Widgets.CatalogRoot.position=catalogMoveBefore.position+ray.GetPoint(hit)-catalogMoveHit;
            return;
        }
        if(!valid||selected.Anchor==AeroWindowAnchor.Hand&&!owner.HandEntry.AnchorTracked){CancelDrag("");return;}
        if(!pressed)
        {
            if(holdTime<.15f){CancelDrag("");return;}
            if(creating&&!catalogBefore.Placed&&Vector3.Distance(selected.Root.position,before.World.position)<.02f)selected.Root.SetPositionAndRotation(catalogSpawn.position,catalogSpawn.rotation);
            if(catalogMoving){catalogMoving=false;owner.Widgets.FixPlacement();owner.Widgets.SetOpen(true,false);dragOwner=null;sizeLabel.gameObject.SetActive(false);RefreshEditor();owner.ClearPresses();return;}
            bool addedNew=creating&&!wasVisible;selected.Undo=creating?catalogBefore:before;selected.HasUndo=true;Remember(selected);dragOwner=null;nearDragging=false;creating=false;Face(selected);toolsRequested=toolsBeforeDrag;sizeLabel.gameObject.SetActive(false);RefreshEditor();owner.ClearPresses();owner.RefreshSettings();Changed();if(addedNew)ToolActionCompleted();else RefreshTools();return;
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
        else if(depthMoving)
        {
            float radius=Mathf.Clamp(startRadius+Vector3.Dot(ray.origin-startOrigin,radial),minRadius,maxRadius);
            if(handPlane)
            {
                float height=Mathf.Clamp(handHeight+Vector3.Dot(ray.origin-startOrigin,-handNormal),Mathf.Min(.03f,handHeight),Mathf.Max(.35f,handHeight));
                selected.Root.SetPositionAndRotation(before.World.position-handNormal*(height-handHeight),Facing(selected));radius=height;
            }
            else selected.Root.SetPositionAndRotation(orbitCenter+radial*radius,Facing(selected));
            sizeLabel.gameObject.SetActive(true);sizeLabel.text=radius.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m";
        }
        else
        {
            if(!orbitPlane.Raycast(ray,out float distance)||distance<=0||distance>5)return;
            var candidate=before.World.position+ray.GetPoint(distance)-orbitHit;var direction=(candidate-orbitCenter).normalized;
            if(direction.sqrMagnitude<.5f)return;
            if(handPlane)selected.Root.SetPositionAndRotation(before.World.position+Vector3.ProjectOnPlane(ray.GetPoint(distance)-orbitHit,handNormal),Facing(selected));
            else selected.Root.SetPositionAndRotation(orbitCenter+direction*startRadius,Facing(selected));
        }
        Physics.SyncTransforms();
    }
    public void CancelDrag(string reason){if(!Dragging)return;if(catalogMoving){catalogMoving=false;dragOwner=null;owner.Widgets.SetOpen(true,false);return;}if(!SurfacesAvailable){AbandonForTeardown();return;}dragOwner=null;nearDragging=false;Restore(selected,creating?catalogBefore:before);if(creating&&!wasVisible)owner.Widgets.SetItemVisible(creatingPanel.Key,false);creating=false;toolsRequested=toolsBeforeDrag;RefreshTools();sizeLabel.gameObject.SetActive(false);SetEditUI(Editing);}
    public void Undo(){if(!Editing)return;if(Dragging)CancelDrag("");if(!selected.HasUndo)return;Restore(selected,selected.Undo);selected.HasUndo=false;owner.ClearPresses();RefreshEditor();owner.RefreshSettings();Physics.SyncTransforms();Changed();}
    public void ResetSelected(){if(!Editing)return;if(Dragging)CancelDrag("");SaveUndo(selected);ResetPanel(selected);SetEditUI(Editing);owner.ClearPresses();owner.RefreshSettings();Changed();}
    void ResetPanel(Panel p){p.Pinned=false;p.Speed=HudSoftFollow.FollowMode.Soft;p.HasSaved=new bool[4];p.Follow.SetUserScale(1);if(p==main){p.Anchor=AeroWindowAnchor.Follow;p.Follow.ResetOptions();}else if(p==small){p.Anchor=AeroWindowAnchor.View;p.Follow.ConfigureScale(.0009f/1.05f,1.05f);p.Follow.SetUserScale(1);p.Local=new Pose(new Vector3(owner.Compact.Side==AeroCompactHud.Placement.Right?.42f:-.42f,-.14f,1.05f),Quaternion.identity);ApplyAttached(p);}else{p.Anchor=AeroWindowAnchor.Follow;p.Follow.ConfigureScale(.00075f/.85f,.85f);p.Follow.SetUserScale(1);p.Follow.SetMode(HudSoftFollow.FollowMode.Soft);p.Follow.SnapToView();}SetRendering(p,true);}
    public void ResetAll(){if(Dragging)CancelDrag("");LayerMask=7;selected=main;ResetPanel(main);ResetPanel(small);foreach(var p in panels){p.HasUndo=false;if(p!=main&&p!=small){ResetPanel(p);p.Placed=false;}}owner.Widgets.ResetWidgets();if(Editing)SetEditUI(true);Physics.SyncTransforms();Changed();}
    void SetRendering(Panel p,bool show)
    {
        show&=!Editing||LayerEnabled(LayerOf(p))||p==main;p.Canvas.enabled=show;
        if(p.Colliders==null)p.Colliders=p.Root.GetComponentsInChildren<Collider>(true);
        foreach(var c in p.Colliders)if(c!=null&&!c.transform.IsChildOf(toolbar))c.enabled=show&&(Editing||p==main||p==small);
        if(p==selected)SetToolbarRendering(show);
    }
    void SetToolbarRendering(bool enabled)
    {if(toolbarColliders!=null)foreach(var collider in toolbarColliders)collider.enabled=enabled&&Editing;}
    public void PrepareFrame()
    {
        if(owner==null||paused)return;
        owner.Widgets.SetHandAvailable(owner.HandEntry.AnchorTracked);
        foreach(var p in panels)
        {
            if(p.Anchor==AeroWindowAnchor.Hand)
            {
                bool tracked=owner.HandEntry.AnchorTracked;
                if(p.Tracked!=tracked)
                {
                    p.Tracked=tracked;SetRendering(p,tracked);
                    // A hand anchor only invalidates input belonging to that visible panel.
                    bool relevant=ActiveInScope(p)&&
                        (Dragging?p==selected:AeroValueSlider.Active!=null?AeroValueSlider.Active.transform.IsChildOf(p.Root):true);
                    if(relevant){owner.ClearPresses();if(Dragging&&p==selected)CancelDrag("");if(!tracked&&owner.Feedback!=null&&(owner.Profiles==null||!owner.Profiles.ModalOpen))owner.Feedback.Notify("请抬起左手，恢复手部组件");}
                }
            }
            if((Dragging&&p==selected)||AeroValueSlider.Active!=null){p.Follow.SyncView();if(Dragging&&p==selected&&!resizing)Face(p);continue;}
            if((p.Anchor==AeroWindowAnchor.Hand&&owner.HandEntry.AnchorTracked)||p.Anchor==AeroWindowAnchor.View)ApplyAttached(p);
            Face(p);
        }
        if(Editing)RefreshEditor();
    }
    public void Tick(float dt,bool interacting)
    {foreach(var p in panels)if(p.Anchor==AeroWindowAnchor.Follow){if(Editing||Dragging||AeroValueSlider.Active!=null)p.Follow.SyncView();else p.Follow.Tick(dt,interacting);}}
    public bool Raycast(Ray ray,float distance,out RaycastHit hit,Transform ignoreRoot=null)
    {
        if(!Editing&&!Dragging&&ignoreRoot==null&&(owner.ToolsEntry==null||!owner.ToolsEntry.Visible))return Physics.Raycast(ray,out hit,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        int count=Physics.RaycastNonAlloc(ray,hits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);hit=default;float nearest=distance+1;bool found=false;int bestPriority=-1;
        for(int i=0;i<count;i++)
        {
            var t=hits[i].transform;
            if(ignoreRoot!=null&&t.IsChildOf(ignoreRoot)||!CanInteract(t))continue;
            if(Dragging&&t.IsChildOf(selected.Root))continue;
            bool tools=owner.Widgets.IsOpen&&t.IsChildOf(owner.Widgets.CatalogRoot);
            bool entry=owner.ToolsEntry!=null&&owner.ToolsEntry.Visible&&t.IsChildOf(owner.ToolsEntry.Root);
            if(Editing&&!Dragging)
            {
                var handle=t.GetComponent<AeroDragHandle>();var button=t.GetComponent<Button>();
                bool backdrop=tools&&(t==owner.Widgets.CatalogRoot||t==owner.Widgets.LayerControls);
                if(owner.Profiles!=null&&owner.Profiles.ModalOpen&&!backdrop&&(button==null||!owner.Profiles.AllowButton(button)))continue;
                bool control=backdrop||handle!=null&&(handle.CatalogMove||!string.IsNullOrEmpty(handle.CatalogId)||FindPanel(handle.PanelId)!=null)||
                    (button!=null&&button.IsInteractable()&&(ToolbarButton(button)||button==owner.Compact.OpenButton));
                if(!control)continue;
            }
            // Match visual overlay ordering while retaining the real ray/collider hit.
            int priority=entry?2:tools?1:0;
            if(priority>bestPriority||priority==bestPriority&&hits[i].distance<nearest)
            {bestPriority=priority;nearest=hits[i].distance;hit=hits[i];found=true;}
        }
        return found;
    }
    public bool CoveredByTools(Transform target,Vector3 point)
    {
        var catalog=owner.Widgets;
        if(!catalog.IsOpen||target.IsChildOf(catalog.CatalogRoot)||!Owns(target)||
            owner.ToolsEntry!=null&&target.IsChildOf(owner.ToolsEntry.Root))return false;
        var direction=point-eye.transform.position;
        if(direction.sqrMagnitude<.000001f||Vector3.Dot(eye.transform.forward,direction)<=0)return false;
        var ray=new Ray(eye.transform.position,direction.normalized);var plane=new Plane(catalog.CatalogRoot.forward,catalog.CatalogRoot.position);
        if(!plane.Raycast(ray,out float distance)||distance<=0)return false;
        Vector3 projected=ray.GetPoint(distance);var body=(RectTransform)catalog.CatalogRoot;
        return body.rect.Contains((Vector2)body.InverseTransformPoint(projected))||
            catalog.LayerControls.gameObject.activeInHierarchy&&((RectTransform)catalog.LayerControls).rect.Contains((Vector2)catalog.LayerControls.InverseTransformPoint(projected));
    }
    public void ReleaseToFollow(){main.Anchor=AeroWindowAnchor.Follow;main.Pinned=false;main.Follow.SetMode(main.Speed);main.Follow.CaptureOffset();main.HasUndo=false;SetRendering(main,true);}
    public void SetAnchorFromFollowMode(){main.Anchor=main.Follow.Mode==HudSoftFollow.FollowMode.Fixed?AeroWindowAnchor.World:AeroWindowAnchor.Follow;main.Pinned=main.Anchor==AeroWindowAnchor.World;if(!main.Pinned)main.Speed=main.Follow.Mode;main.Follow.CaptureOffset();main.HasUndo=false;SetRendering(main,true);}
    public void PlaceOpenedCard(){PrepareFrame();main.Follow.SyncView();}
    public void RecalledSky(){}
    public void InvalidateMainUndo(){main.HasUndo=false;}
    public void CompactPreset(AeroCompactHud.Placement side){if(side==AeroCompactHud.Placement.Hidden)return;small.HasUndo=false;small.Anchor=AeroWindowAnchor.View;small.Local=new Pose(new Vector3(side==AeroCompactHud.Placement.Right?.42f:-.42f,-.14f,1.05f),Quaternion.identity);ApplyAttached(small);SetRendering(small,true);}
    public void Recall()
    {
        bool editWidgets=Editing;if(Editing)EndEdit();ReleaseToFollow();main.Follow.SetDistance(Mathf.Clamp(main.Follow.Distance,.65f,2.2f));main.Follow.SnapToView();
        foreach(var p in panels)if(editWidgets&&p!=main&&p!=small&&p.Root.gameObject.activeSelf&&NeedsRecallFor(p))
        {p.Anchor=AeroWindowAnchor.Follow;p.Pinned=false;p.Follow.SetMode(p.Speed);int i=System.Array.IndexOf(panels,p)-2;p.Root.position=eye.transform.TransformPoint(new Vector3(.30f+(i%2)*.3f,.24f-(i/2)*.25f,.9f));Remember(p);Face(p);p.HasUndo=false;SetRendering(p,true);}Changed();
    }
    public AeroLayoutState CaptureLayout()
    {
        var states=new List<AeroPanelState>(6);
        foreach(var p in panels)
        {
            if(p==small)continue;
            Vector3 position=p.Anchor==AeroWindowAnchor.Follow?p.Follow.Offset.position
                :p.Anchor==AeroWindowAnchor.World?eye.transform.InverseTransformPoint(p.Root.position):p.Local.position;
            states.Add(new AeroPanelState{id=p.Key,visible=p==main?owner.PreferredMainVisibility:p.Root.gameObject.activeSelf,placed=p.Placed,
                anchor=(int)p.Anchor,position=position,physicalScale=p.Root.localScale.x,userScale=p.Follow.UserScale,
                followMode=(int)p.Follow.Mode,speed=(int)p.Speed,pinned=p.Pinned});
        }
        return new AeroLayoutState{panels=states.ToArray()};
    }
    public bool ApplyLayout(AeroLayoutState state)
    {
        if(!AeroLayoutProfileStore.ValidateState(state,out _))return false;
        if(Dragging)CancelDrag("");owner.ClearPresses();
        suppressChanges=true;
        try
        {
            foreach(var saved in state.panels)
            {
                var p=FindPanel(saved.id);p.Anchor=(AeroWindowAnchor)saved.anchor;p.Pinned=saved.pinned;
                p.Speed=(HudSoftFollow.FollowMode)saved.speed;p.Placed=saved.placed;p.HasUndo=false;
                p.HasSaved=new bool[4];p.Saved=new Pose[4];
                p.Local=new Pose(saved.position,Quaternion.identity);
                var offset=new Pose(saved.position,Quaternion.identity);
                p.Follow.RestoreOptions(Mathf.Max(.01f,saved.position.magnitude),saved.userScale,offset,
                    (HudSoftFollow.FollowMode)saved.followMode,saved.physicalScale);
                if(p.Anchor==AeroWindowAnchor.Follow)p.Follow.ApplyOffset();
                else if(p.Anchor==AeroWindowAnchor.World)
                {
                    p.Root.SetPositionAndRotation(eye.transform.TransformPoint(saved.position),eye.transform.rotation);
                    p.Follow.CaptureOffset();
                }
                else ApplyAttached(p);
                p.Root.localScale=Vector3.one*saved.physicalScale;
                if (p == main)
                {
                    owner.MainVisibilityPreference = saved.visible;
                    owner.RestoreMainVisibility(saved.visible);
                }
                else owner.Widgets.SetItemVisible(p.Key,saved.visible);
                p.Tracked=p.Anchor!=AeroWindowAnchor.Hand||owner.HandEntry.AnchorTracked;
                SetRendering(p,!paused&&p.Tracked);Face(p);
                p.Saved[(int)p.Anchor]=p.Anchor==AeroWindowAnchor.World?new Pose(p.Root.position,p.Root.rotation)
                    :p.Anchor==AeroWindowAnchor.Follow?p.Follow.Offset:p.Local;p.HasSaved[(int)p.Anchor]=true;
            }
            selected=main;foreach(var p in panels)if(p!=small&&p.Root.gameObject.activeSelf){selected=p;break;}
            EnsureSelection();SetEditUI(Editing);owner.Widgets.Refresh();owner.RefreshSettings();Physics.SyncTransforms();return true;
        }
        finally{suppressChanges=false;}
    }
    public bool CanInteract(Transform t)
    {
        if(owner.ToolsEntry!=null&&t.IsChildOf(owner.ToolsEntry.Root))return Editing;
        if(t.IsChildOf(owner.Widgets.CatalogRoot))return Editing;
        foreach(var p in panels)if(t.IsChildOf(p.Root))return p.Canvas.enabled;
        return true;
    }
    public bool IsOwnPanel(Transform t)
    {
        if(t==null)return false;
        foreach(var p in panels)if(t.IsChildOf(p.Root))return true;
        return false;
    }
    public bool ToolbarButton(Button b)
    {
        if(b==null)return false;
        if(owner.ToolsEntry!=null&&b==owner.ToolsEntry.Button)return true;
        if(b.transform.IsChildOf(owner.Widgets.CatalogRoot))return true;
        if(b.transform.IsChildOf(owner.Widgets.LayerControls))return true;
        if(b.transform.IsChildOf(toolbar))return true;
        foreach(var p in panels)if(p.Canvas.enabled&&(b==p.Close||b==p.Body))return true;return false;
    }
    public bool Owns(Transform t)
    {if(owner.ToolsEntry!=null&&t.IsChildOf(owner.ToolsEntry.Root)||t.IsChildOf(owner.Widgets.CatalogRoot))return true;foreach(var p in panels)if(t.IsChildOf(p.Root))return true;return false;}
    public void SetHover(Button left,Button right)
    {
        if(hoverLeft==left&&hoverRight==right)return;hoverLeft=left;hoverRight=right;if(Editing)RefreshEditor();
        foreach(var p in panels)if(p.Close!=null)p.Close.GetComponent<Image>().color=p.Close==left||p.Close==right?AeroUiTokens.ButtonHover:AeroUiTokens.ButtonFill;
        foreach(var item in owner.Widgets.Items)item.EntryButton.GetComponent<Image>().color=item.EntryButton==left||item.EntryButton==right?AeroUiTokens.ButtonHover:item.Id==selected.Key?new Color(.2f,.31f,.28f,1):AeroUiTokens.ButtonFill;
    }
    void OnDestroy(){foreach(var z in new[]{WorldZone,ViewZone,HandZone})if(z!=null)Destroy(z.gameObject);}
}
