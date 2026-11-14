using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Fixed categories navigate only; every option sets an explicit value.
public class AeroHudSettingsView : MonoBehaviour
{
    public enum SettingsTab { Display, Input, Layout }
    public SettingsTab CurrentTab { get; private set; }
    public bool LayoutPage => CurrentTab == SettingsTab.Layout;
    public bool ResetConfirmationOpen { get; private set; }
    public UnityEngine.UI.Button[] TabButtons, SkyOptions, CompactOptions, EntryOptions,
        HeightOptions, ModeOptions, DistanceOptions, SoundOptions;
    public UnityEngine.UI.Button HelpButton, ConfirmResetButton, CancelResetButton;
    public TextMeshProUGUI CurrentModeLabel { get; private set; }
    // Former single-button fields remain aliases for Editor source compatibility, not cycling controls.
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
        public UnityEngine.UI.Image Fill, SelectedOutline;
        public TextMeshProUGUI Label;
        public bool Selected;
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
        view.TabButtons = new UnityEngine.UI.Button[3];
        string[] tabs = { "显示", "交互", "布局" };
        for(int i=0;i<tabs.Length;i++)
        {
            int index=i;
            view.TabButtons[i]=view.Button(root,"SettingsTab"+((SettingsTab)i),tabs[i],new Vector2((i-1)*234,98),new Vector2(216,42),true,22);
            view.TabButtons[i].onClick.AddListener(()=>view.SelectTab((SettingsTab)index));
        }

        var display=view.pages[0];
        view.Heading(display,"SkyHeading","天空地图",40);
        view.SkyOptions=view.Options(display,"SkyVisible",new[]{"关闭","开启"},40);
        view.SkyOptions[0].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetSkyVisible(false);view.Refresh();});
        view.SkyOptions[1].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetSkyVisible(true);view.Refresh();});
        view.Heading(display,"CompactHeading","视野小读数",-18);
        view.CompactOptions=view.Options(display,"CompactPlacement",new[]{"隐藏","左侧","右侧"},-18);
        for(int i=0;i<3;i++)
        {
            int index=i;
            view.CompactOptions[i].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetCompactPlacement((AeroCompactHud.Placement)index);view.Refresh();});
        }
        view.Heading(display,"SkyRecallHeading","天空定位",-76);
        view.EditSkyButton=view.Button(display,"RecallSky","召回天空",new Vector2(180,-76),new Vector2(312,42));
        view.EditSkyButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.RecenterSky();});
        view.Hint(display,"SkyIndependence","天空独立显示，收起HUD不会关闭。");

        var input=view.pages[1];
        view.Heading(input,"EntryHeading","打开方式",40);
        view.EntryOptions=view.Options(input,"HandEntryMode",new[]{"手背","快捷","前方"},40);
        for(int i=0;i<3;i++)
        {
            int index=i;
            view.EntryOptions[i].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetHandEntryMode(index);view.Refresh();});
        }
        view.Heading(input,"HeightHeading","手背高度",-18);
        view.HeightOptions=view.Options(input,"HandHeight",new[]{"25 mm","35 mm","45 mm"},-18);
        float[] heights={.025f,.035f,.045f};
        for(int i=0;i<3;i++)
        {
            float value=heights[i];
            view.HeightOptions[i].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetHandHeight(value);view.Refresh();});
        }
        view.HelpButton=view.Button(input,"InteractionHelp","操作帮助",new Vector2(-242,-76),new Vector2(204,42));
        view.HelpButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.ShowHelp();});
        view.Text(input,"SoundHeading","提示音",new Vector2(-110,-76),new Vector2(160,36),20,AeroUiTokens.Secondary);
        view.SoundOptions=new[] {
            view.Button(input,"FeedbackSoundOff","关",new Vector2(160,-76),new Vector2(104,42),true),
            view.Button(input,"FeedbackSoundOn","开",new Vector2(280,-76),new Vector2(104,42),true)
        };
        view.SoundOptions[0].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetFeedbackSound(false);view.Refresh();});
        view.SoundOptions[1].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetFeedbackSound(true);view.Refresh();});
        view.Hint(input,"HandCalibrationHint","手背高度只抬高显示片，触碰位置不变。");

        var layout=view.pages[2];
        view.Heading(layout,"FollowHeading","主卡跟随",40);
        view.ModeOptions=view.Options(layout,"FollowMode",new[]{"柔和","快速","固定"},40);
        for(int i=0;i<3;i++)
        {
            int index=i;
            view.ModeOptions[i].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetFollowMode((HudSoftFollow.FollowMode)index);view.Refresh();});
        }
        view.Heading(layout,"DistanceHeading","面前距离",-18);
        view.DistanceOptions=view.Options(layout,"PanelDistance",new[]{"1.4 m","1.8 m","2.2 m"},-18);
        float[] distances={1.4f,1.8f,2.2f};
        for(int i=0;i<3;i++)
        {
            float value=distances[i];
            view.DistanceOptions[i].onClick.AddListener(()=>{if(view.owner!=null)view.owner.SetPanelDistance(value);view.Refresh();});
        }
        view.RecenterButton=view.Button(layout,"RecenterHud","移到面前",new Vector2(-172,-76),new Vector2(328,42));
        view.RecenterButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.Recenter();view.Refresh();});
        view.EditCardButton=view.Button(layout,"EditCard","编辑位置",new Vector2(172,-76),new Vector2(328,42));
        view.EditCardButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.Layout.BeginEdit(false);});
        view.CurrentModeLabel=view.Hint(layout,"CurrentDockState","");

        // The parent hides its MAP button while settings are open, reserving this footer slot.
        view.ResetButton=view.Button(root,"ResetHudDefaults","恢复默认",new Vector2(66,-172),new Vector2(156,56));
        view.ResetButton.GetComponent<BoxCollider>().size=new Vector3(164,70,8);
        view.ResetButton.onClick.AddListener(view.RequestReset);
        view.resetConfirmation=AeroHudView.NewRect("ResetHudConfirmation",root,Vector2.zero,new Vector2(688,236));
        view.Text(view.resetConfirmation,"ResetTitle","恢复HUD默认设置？",new Vector2(-328,40),new Vector2(656,44),24,AeroUiTokens.Ink);
        view.Text(view.resetConfirmation,"ResetScope","入口、小读数、高度、提示音和主卡布局将复位。\n天空地图与路线进度保持不变。",new Vector2(-328,-10),new Vector2(656,70),18,AeroUiTokens.Secondary);
        view.CancelResetButton=view.Button(view.resetConfirmation,"CancelHudReset","取消",new Vector2(-172,-86),new Vector2(288,44));
        view.ConfirmResetButton=view.Button(view.resetConfirmation,"ConfirmHudReset","恢复默认",new Vector2(172,-86),new Vector2(288,44));
        view.CancelResetButton.onClick.AddListener(view.CancelReset);
        view.ConfirmResetButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.ResetViewOptions();view.CancelReset();});

        view.CompactButton=view.CompactOptions[0];view.EntryButton=view.EntryOptions[0];
        view.ModeButton=view.ModeOptions[0];view.DistanceButton=view.DistanceOptions[0];view.HeightButton=view.HeightOptions[0];
        view.SelectTab(SettingsTab.Display);
        return view;
    }

    UnityEngine.UI.Button Button(Transform parent,string name,string label,Vector2 position,Vector2 size,bool choice=false,float fontSize=20)
    {
        var button=AeroHudView.ActionButton(parent,name,label,position,size,new Vector3(size.x+8,size.y+8,8));
        var caption=button.GetComponentInChildren<TextMeshProUGUI>();
        caption.font=AeroUiFonts.Interface;caption.fontSize=fontSize;caption.characterSpacing=0;
        var state=new Control {Button=button,Fill=button.GetComponent<UnityEngine.UI.Image>(),Label=caption};
        if(choice)
        {
            state.SelectedOutline=AeroHudView.AddPanelImage(button.transform,"SelectedOutline",AeroUiSprites.PillStroke,Vector2.zero,size+new Vector2(2,2),AeroUiTokens.Mint);
            state.SelectedOutline.raycastTarget=false;state.SelectedOutline.gameObject.SetActive(false);
        }
        controls.Add(state);return button;
    }
    UnityEngine.UI.Button[] Options(Transform parent,string name,string[] labels,float y)
    {
        var result=new UnityEngine.UI.Button[labels.Length];
        float width=labels.Length==2?202:128;
        for(int i=0;i<labels.Length;i++)
        {
            float x=labels.Length==2?4+i*224:-28+i*144;
            result[i]=Button(parent,name+i,labels[i],new Vector2(x,y),new Vector2(width,42),true);
        }
        return result;
    }
    TextMeshProUGUI Text(Transform parent,string name,string label,Vector2 position,Vector2 size,float fontSize,Color tint)
        =>AeroHudView.Text(parent,name,label,position,size,fontSize,AeroUiFonts.Interface,tint,TextAlignmentOptions.Left,0,0);
    void Heading(Transform parent,string name,string label,float y)
        =>Text(parent,name,label,new Vector2(-344,y),new Vector2(220,36),20,AeroUiTokens.Secondary);
    TextMeshProUGUI Hint(Transform parent,string name,string label)
        =>Text(parent,name,label,new Vector2(-344,-116),new Vector2(688,28),16,AeroUiTokens.Secondary);

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
            Select(HeightOptions,owner.HandEntry.HeightOffset<.03f?0:owner.HandEntry.HeightOffset<.04f?1:2);
            Select(SoundOptions,owner.FeedbackSoundEnabled?1:0);
            bool attached=owner.Layout.MainAnchor==AeroWindowAnchor.Hand||owner.Layout.MainAnchor==AeroWindowAnchor.View;
            Select(ModeOptions,attached?-1:(int)owner.Follow.Mode);
            Select(DistanceOptions,owner.Follow.Distance<1.6f?0:owner.Follow.Distance<2f?1:2);
            foreach(var button in DistanceOptions)button.interactable=!attached;
            CurrentModeLabel.text=owner.Layout.MainAnchor==AeroWindowAnchor.Hand?"当前：随左手；移到面前后可调距离。":
                owner.Layout.MainAnchor==AeroWindowAnchor.View?"当前：随视野；移到面前后可调距离。":
                owner.Follow.Mode==HudSoftFollow.FollowMode.Fixed?"当前：固定在环境中。":
                owner.Follow.Mode==HudSoftFollow.FollowMode.Quick?"当前：快速跟随。":"当前：柔和跟随。";
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
            control.Fill.color=!enabled?new Color(.07f,.11f,.12f,.45f):hover?AeroUiTokens.ButtonHover:
                control.Selected?new Color(.18f,.40f,.34f,.92f):AeroUiTokens.ButtonFill;
            control.Label.color=!enabled?new Color(.55f,.64f,.62f,.65f):control.Selected||hover?AeroUiTokens.Mint:AeroUiTokens.Ink;
            if(control.SelectedOutline!=null)control.SelectedOutline.gameObject.SetActive(control.Selected&&enabled);
        }
    }
}
