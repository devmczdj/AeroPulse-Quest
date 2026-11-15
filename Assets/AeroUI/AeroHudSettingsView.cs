using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Left-side categories navigate only; the content region owns settings and actions.
public class AeroHudSettingsView : MonoBehaviour
{
    public enum SettingsTab { Display, Input, Layout }
    public SettingsTab CurrentTab { get; private set; }
    public bool LayoutPage => CurrentTab == SettingsTab.Layout;
    public bool ResetConfirmationOpen { get; private set; }
    public UnityEngine.UI.Button[] TabButtons, SkyOptions, CompactOptions, EntryOptions,
        HeightOptions, ModeOptions, DistanceOptions, SoundOptions;
    public AeroValueSlider HeightSlider { get; private set; }
    public AeroValueSlider DistanceSlider { get; private set; }
    public UnityEngine.UI.Button HelpButton, ConfirmResetButton, CancelResetButton;
    public TextMeshProUGUI CurrentModeLabel { get; private set; }
    // Aliases keep older Editor helpers compilable; the height/distance controls are sliders.
    public UnityEngine.UI.Button CompactButton, EntryButton, EditCardButton, EditSkyButton,
        ModeButton, DistanceButton, HeightButton, RecenterButton, ResetButton;

    PinchHudExperiment owner;
    RectTransform[] pages;
    RectTransform resetConfirmation;
    readonly List<Control> controls = new List<Control>();
    UnityEngine.UI.Button hoverLeft, hoverRight;

    sealed class Control
    {
        public UnityEngine.UI.Button Button;
        public UnityEngine.UI.Image Fill;
        public TextMeshProUGUI Label;
        public bool Selected, Navigation;
    }

    public static AeroHudSettingsView Build(Transform parent)
    {
        var root = AeroHudView.NewRect("SettingsContent", parent, Vector2.zero, new Vector2(760,430));
        var view = root.gameObject.AddComponent<AeroHudSettingsView>();
        view.pages = new[] {
            AeroHudView.NewRect("DisplaySettings",root,Vector2.zero,new Vector2(760,430)),
            AeroHudView.NewRect("InputSettings",root,Vector2.zero,new Vector2(760,430)),
            AeroHudView.NewRect("LayoutSettings",root,Vector2.zero,new Vector2(760,430))
        };
        // Navigation lives in its own left column. Plain text and a quiet neutral selection
        // distinguish category changes from the value controls on the right.
        var navigation=AeroHudView.NewRect("SettingsNavigation",root,new Vector2(-270,0),new Vector2(148,224));
        view.TabButtons = new UnityEngine.UI.Button[3];
        string[] tabs = { "显示", "交互", "布局" };
        for(int i=0;i<tabs.Length;i++)
        {
            int index=i;
            var button=view.Button(navigation,"SettingsTab"+((SettingsTab)i),tabs[i],
                new Vector2(0,84-i*76),new Vector2(148,56),false,24,true);
            button.GetComponent<UnityEngine.UI.Image>().sprite=null;
            view.TabButtons[i]=button;
            button.onClick.AddListener(()=>view.SelectTab((SettingsTab)index));
        }

        // Move each complete content page without changing its internal control spacing.
        foreach(var page in view.pages)page.anchoredPosition=new Vector2(212,0);

        var display=view.pages[0];
        view.Heading(display,"SkyHeading","天空地图",84);
        view.SkyOptions=view.Options(display,"SkyVisible",new[]{"关闭","开启"},84);
        view.SkyOptions[0].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetSkyVisible(false);view.Refresh();});
        view.SkyOptions[1].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetSkyVisible(true);view.Refresh();});
        view.Heading(display,"CompactHeading","视野小读数",8);
        view.CompactOptions=view.Options(display,"CompactPlacement",new[]{"隐藏","左侧","右侧"},8);
        for(int i=0;i<3;i++)
        {
            int index=i;
            view.CompactOptions[i].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetCompactPlacement((AeroCompactHud.Placement)index);view.Refresh();});
        }
        view.Heading(display,"SkyRecallHeading","天空位置",-68);
        view.EditSkyButton=view.Button(display,"RecallSky","召回天空",new Vector2(-16,-68),new Vector2(296,44));
        view.EditSkyButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.RecenterSky();});

        var input=view.pages[1];
        view.Heading(input,"EntryHeading","打开方式",84);
        view.EntryOptions=view.Options(input,"HandEntryMode",new[]{"手背","快捷","前方"},84);
        for(int i=0;i<3;i++)
        {
            int index=i;
            view.EntryOptions[i].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetHandEntryMode(index);view.Refresh();});
        }
        view.Heading(input,"HeightHeading","手背高度",18);
        view.HeightSlider=AeroValueSlider.Build(input,"HandHeightSlider",new Vector2(-106,-6),new Vector2(476,64),
            .025f,.045f,.035f,.001f,value=>Mathf.RoundToInt(value*1000)+" mm",
            value=>{if(view.owner!=null)view.owner.SetHandHeight(value);view.Refresh();});
        view.HelpButton=view.Button(input,"InteractionHelp","操作帮助",new Vector2(-252,-88),new Vector2(184,42));
        view.HelpButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.ShowHelp();});
        view.Text(input,"SoundHeading","提示音",new Vector2(-136,-88),new Vector2(88,30),22,AeroUiTokens.Secondary);
        view.SoundOptions=new[] {
            view.Button(input,"FeedbackSoundOff","关",new Vector2(-12,-88),new Vector2(72,42),true),
            view.Button(input,"FeedbackSoundOn","开",new Vector2(80,-88),new Vector2(72,42),true)
        };
        view.SoundOptions[0].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetFeedbackSound(false);view.Refresh();});
        view.SoundOptions[1].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetFeedbackSound(true);view.Refresh();});

        var layout=view.pages[2];
        view.Heading(layout,"FollowHeading","跟随方式",84);
        view.ModeOptions=view.Options(layout,"FollowMode",new[]{"柔和","快速","固定"},84);
        for(int i=0;i<3;i++)
        {
            int index=i;
            view.ModeOptions[i].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetFollowMode((HudSoftFollow.FollowMode)index);view.Refresh();});
        }
        view.Heading(layout,"DistanceHeading","面前距离",18);
        view.DistanceSlider=AeroValueSlider.Build(layout,"PanelDistanceSlider",new Vector2(-106,-6),new Vector2(476,64),
            0.55f,2.2f,1.8f,.01f,value=>value.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m",
            value=>{if(view.owner!=null)view.owner.SetPanelDistance(value);view.Refresh();});
        view.CurrentModeLabel=view.Text(layout,"CurrentDockState","",new Vector2(-344,-48),new Vector2(476,28),18,AeroUiTokens.Secondary);
        view.RecenterButton=view.Button(layout,"RecenterHud","移到面前",new Vector2(-228,-96),new Vector2(232,40));
        view.RecenterButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.Recenter();view.Refresh();});
        view.EditCardButton=view.Button(layout,"EditCard","编辑位置",new Vector2(16,-96),new Vector2(232,40));
        view.EditCardButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.Layout.BeginEdit(false);});

        // The parent reserves this footer slot by hiding its route button in settings.
        view.ResetButton=view.Button(root,"ResetHudDefaults","恢复默认",new Vector2(66,-172),new Vector2(156,56));
        view.ResetButton.GetComponent<BoxCollider>().size=new Vector3(164,70,8);
        view.ResetButton.onClick.AddListener(view.RequestReset);
        view.resetConfirmation=AeroHudView.NewRect("ResetHudConfirmation",root,new Vector2(106,0),new Vector2(476,236));
        view.Text(view.resetConfirmation,"ResetTitle","恢复HUD默认设置？",new Vector2(-226,58),new Vector2(452,36),26,AeroUiTokens.Ink);
        view.Text(view.resetConfirmation,"ResetScope","入口、读数、高度、提示音、主卡布局将复位。\n天空与路线进度不变。",new Vector2(-226,0),new Vector2(452,72),18,AeroUiTokens.Secondary);
        view.CancelResetButton=view.Button(view.resetConfirmation,"CancelHudReset","取消",new Vector2(-120,-90),new Vector2(216,44));
        view.ConfirmResetButton=view.Button(view.resetConfirmation,"ConfirmHudReset","恢复默认",new Vector2(120,-90),new Vector2(216,44));
        view.CancelResetButton.onClick.AddListener(view.CancelReset);
        view.ConfirmResetButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.ResetViewOptions();view.CancelReset();});

        view.CompactButton=view.CompactOptions[0];view.EntryButton=view.EntryOptions[0];
        view.ModeButton=view.ModeOptions[0];view.DistanceButton=view.DistanceSlider.Button;view.HeightButton=view.HeightSlider.Button;
        view.HeightOptions=new[]{view.HeightButton};view.DistanceOptions=new[]{view.DistanceButton};
        view.SelectTab(SettingsTab.Display);
        return view;
    }

    UnityEngine.UI.Button Button(Transform parent,string name,string label,Vector2 position,Vector2 size,bool choice=false,float fontSize=22,bool navigation=false)
    {
        var button=AeroHudView.ActionButton(parent,name,label,position,size,new Vector3(size.x+8,size.y+8,8));
        var caption=button.GetComponentInChildren<TextMeshProUGUI>();
        caption.font=AeroUiFonts.Interface;caption.fontSize=fontSize;caption.characterSpacing=0;
        controls.Add(new Control {Button=button,Fill=button.GetComponent<UnityEngine.UI.Image>(),Label=caption,Navigation=navigation});
        return button;
    }
    UnityEngine.UI.Button[] Options(Transform parent,string name,string[] labels,float y)
    {
        var result=new UnityEngine.UI.Button[labels.Length];
        float width=labels.Length==2?140:88;
        for(int i=0;i<labels.Length;i++)
        {
            float x=labels.Length==2?-94+i*156:-120+i*104;
            result[i]=Button(parent,name+i,labels[i],new Vector2(x,y),new Vector2(width,44),true);
        }
        return result;
    }
    TextMeshProUGUI Text(Transform parent,string name,string label,Vector2 position,Vector2 size,float fontSize,Color tint)
        =>AeroHudView.Text(parent,name,label,position,size,fontSize,AeroUiFonts.Interface,tint,TextAlignmentOptions.Left,0,0);
    void Heading(Transform parent,string name,string label,float y)
        =>Text(parent,name,label,new Vector2(-344,y),new Vector2(156,32),22,AeroUiTokens.Secondary);

    public void Bind(PinchHudExperiment hud) { owner=hud;Refresh(); }
    public void SelectTab(SettingsTab tab)
    {
        CurrentTab=tab;ResetConfirmationOpen=false;ApplyVisibility();Refresh();
        if(owner!=null)owner.ClearPresses();Physics.SyncTransforms();
    }
    public void TogglePage() => SelectTab(LayoutPage?SettingsTab.Display:SettingsTab.Layout);
    public void RequestReset()
    {
        if(owner==null)return;
        ResetConfirmationOpen=true;ApplyVisibility();owner.ClearPresses();Physics.SyncTransforms();
    }
    public void CancelReset()
    {
        ResetConfirmationOpen=false;ApplyVisibility();Refresh();
        if(owner!=null)owner.ClearPresses();Physics.SyncTransforms();
    }
    void ApplyVisibility()
    {
        if(pages==null)return;
        for(int i=0;i<pages.Length;i++)pages[i].gameObject.SetActive(!ResetConfirmationOpen&&i==(int)CurrentTab);
        if(resetConfirmation!=null)resetConfirmation.gameObject.SetActive(ResetConfirmationOpen);
        if(ResetButton!=null)ResetButton.gameObject.SetActive(!ResetConfirmationOpen);
    }
    void OnEnable() { if(pages!=null){ApplyVisibility();Refresh();} }
    void OnDisable() { ResetConfirmationOpen=false; }
    void Select(UnityEngine.UI.Button[] options,int index)
    {
        for(int i=0;i<options.Length;i++)
            foreach(var control in controls)
                if(control.Button==options[i]){control.Selected=i==index;break;}
    }
    public void Refresh()
    {
        if(TabButtons==null)return;
        Select(TabButtons,(int)CurrentTab);
        if(owner!=null)
        {
            Select(SkyOptions,owner.Sky.Visible?1:0);
            Select(CompactOptions,(int)owner.Compact.Side);
            Select(EntryOptions,!owner.HandEntry.EnabledForEntry?2:owner.HandEntry.ShortcutsEnabled?1:0);
            HeightSlider.SetValueWithoutNotify(owner.HandEntry.HeightOffset);
            Select(SoundOptions,owner.FeedbackSoundEnabled?1:0);
            bool attached=owner.Layout.MainAnchor==AeroWindowAnchor.Hand||owner.Layout.MainAnchor==AeroWindowAnchor.View;
            Select(ModeOptions,attached?-1:(int)owner.Follow.Mode);
            DistanceSlider.SetValueWithoutNotify(owner.Follow.Distance);
            DistanceSlider.SetInteractable(!attached);
            CurrentModeLabel.text=owner.Layout.MainAnchor==AeroWindowAnchor.Hand?"随左手：移到面前后可调距离。":
                owner.Layout.MainAnchor==AeroWindowAnchor.View?"随视野：移到面前后可调距离。":"";
        }
        Paint();
    }
    public void SetHover(UnityEngine.UI.Button left,UnityEngine.UI.Button right)
    {
        if(hoverLeft==left&&hoverRight==right)return;
        hoverLeft=left;hoverRight=right;Paint();
    }
    void Paint()
    {
        foreach(var control in controls)
        {
            bool enabled=control.Button.IsInteractable();
            bool hover=enabled&&(control.Button==hoverLeft||control.Button==hoverRight);
            if(control.Navigation)
            {
                control.Fill.color=control.Selected?new Color(1f,1f,1f,.09f):hover?new Color(1f,1f,1f,.045f):Color.clear;
                control.Label.color=control.Selected?AeroUiTokens.Ink:AeroUiTokens.Secondary;
                control.Label.fontStyle=control.Selected?FontStyles.Bold:FontStyles.Normal;
            }
            else
            {
                control.Fill.color=!enabled?new Color(.08f,.11f,.12f,.45f):hover?new Color(.20f,.28f,.27f,1f):
                    control.Selected?new Color(.20f,.31f,.28f,1f):AeroUiTokens.ButtonFill;
                control.Label.color=!enabled?new Color(.55f,.64f,.62f,.65f):control.Selected?AeroUiTokens.Mint:AeroUiTokens.Ink;
            }
        }
    }
}
