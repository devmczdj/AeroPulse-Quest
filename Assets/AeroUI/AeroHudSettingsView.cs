using TMPro;
using UnityEngine;

public class AeroHudSettingsView : MonoBehaviour
{
    PinchHudExperiment owner;
    public UnityEngine.UI.Button CompactButton, EntryButton, EditCardButton, EditSkyButton;
    public bool LayoutPage { get; private set; }
    RectTransform basic,layout;
    public void TogglePage() { LayoutPage=!LayoutPage;basic.gameObject.SetActive(!LayoutPage);layout.gameObject.SetActive(LayoutPage);Refresh();Physics.SyncTransforms(); }
    public UnityEngine.UI.Button ModeButton, DistanceButton, HeightButton, RecenterButton, ResetButton;
    UnityEngine.UI.Button[] buttons;
    public static AeroHudSettingsView Build(Transform parent)
    {
        var root = AeroHudView.NewRect("SettingsContent", parent, Vector2.zero, new Vector2(760,430));
        var view = root.gameObject.AddComponent<AeroHudSettingsView>();
        view.basic=AeroHudView.NewRect("BasicOptions",root,Vector2.zero,new Vector2(760,430));
        view.layout=AeroHudView.NewRect("LayoutOptions",root,Vector2.zero,new Vector2(760,430));
        view.ModeButton = view.Row(view.basic,"FollowMode", "FOLLOW MODE", "SOFT FOLLOW", 88);
        view.DistanceButton = view.Row(view.basic,"ViewDistance", "PANEL DISTANCE", "1.8 M", 28);
        view.HeightButton = view.Row(view.basic,"PadHeight", "HAND PAD HEIGHT", "NORMAL / 35 MM", -32);
        view.CompactButton=view.Row(view.layout,"SmallHUD","SMALL HUD","HIDDEN",88);
        view.EntryButton=view.Row(view.layout,"HandEntry","HAND ENTRY","HAND BACK",28);
        AeroHudView.Text(view.layout,"EditLayoutHeading","WINDOW LAYOUT",new Vector2(-344,-32),new Vector2(240,36),18,AeroUiFonts.SemiBold,AeroUiTokens.Secondary,TextAlignmentOptions.Left,0,0);
        view.EditCardButton=AeroHudView.ActionButton(view.layout,"EditCard","EDIT CARD",new Vector2(82,-32),new Vector2(168,48),new Vector3(168,52,8));
        view.EditSkyButton=AeroHudView.ActionButton(view.layout,"EditSky","CENTER SKY",new Vector2(258,-32),new Vector2(168,48),new Vector3(168,52,8));
        view.layout.gameObject.SetActive(false);
        view.RecenterButton = AeroHudView.ActionButton(root,"Recenter","RECENTER",new Vector2(-172,-98),new Vector2(328,48),new Vector3(328,52,8));
        view.ResetButton = AeroHudView.ActionButton(root,"ResetView","RESET DEFAULTS",new Vector2(172,-98),new Vector2(328,48),new Vector3(328,52,8));
        view.buttons = new[] {view.ModeButton,view.DistanceButton,view.HeightButton,view.RecenterButton,view.ResetButton,view.CompactButton,view.EntryButton,view.EditCardButton,view.EditSkyButton};
        view.EditCardButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.Layout.BeginEdit(false);});
        view.EditSkyButton.onClick.AddListener(()=>{if(view.owner!=null)view.owner.RecenterSky();});
        view.CompactButton.onClick.AddListener(()=>{if(view.owner!=null){view.owner.Compact.CyclePlacement();view.Refresh();}});
        view.EntryButton.onClick.AddListener(()=>{if(view.owner!=null){view.owner.ToggleHandEntry();view.Refresh();}});
        view.ModeButton.onClick.AddListener(() => { if(view.owner != null) {view.owner.CycleFollowMode();view.Refresh();} });
        view.DistanceButton.onClick.AddListener(() => { if(view.owner != null) {view.owner.Follow.CycleDistance();view.owner.Recenter();view.Refresh();} });
        view.HeightButton.onClick.AddListener(() => { if(view.owner != null) {view.owner.HandEntry.CycleHeight();view.Refresh();} });
        view.RecenterButton.onClick.AddListener(() => { if(view.owner != null) view.owner.Recenter(); });
        view.ResetButton.onClick.AddListener(() => { if(view.owner != null) {view.owner.ResetViewOptions();view.Refresh();} });
        return view;
    }
    UnityEngine.UI.Button Row(Transform parent,string name,string label,string value,float y)
    {
        AeroHudView.Text(parent,name+"Heading",label,new Vector2(-344,y),new Vector2(240,36),18,
            AeroUiFonts.SemiBold,AeroUiTokens.Secondary,TextAlignmentOptions.Left,0,0);
        return AeroHudView.ActionButton(parent,name,value,new Vector2(170,y),new Vector2(344,48),new Vector3(352,52,8));
    }
    public void Bind(PinchHudExperiment hud) { owner = hud; Refresh(); }
    public void Refresh()
    {
        if(owner == null) return;
        CompactButton.GetComponentInChildren<TextMeshProUGUI>().text=owner.Compact.Side.ToString().ToUpperInvariant();
        EntryButton.GetComponentInChildren<TextMeshProUGUI>().text=owner.HandEntry.EnabledForEntry?(owner.HandEntry.ShortcutsEnabled?"HAND SHORTCUTS":"HAND BACK"):"FRONT BUTTON";
        ModeButton.GetComponentInChildren<TextMeshProUGUI>().text = owner.Layout.MainLabel;
        DistanceButton.GetComponentInChildren<TextMeshProUGUI>().text = owner.Follow.Distance.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" M";
        float height = owner.HandEntry.HeightOffset;
        HeightButton.GetComponentInChildren<TextMeshProUGUI>().text = (height<0.03f?"LOW":height<0.04f?"NORMAL":"HIGH")+" / "+Mathf.RoundToInt(height*1000)+" MM";
    }
    public void SetHover(UnityEngine.UI.Button left,UnityEngine.UI.Button right)
    {
        foreach(var button in buttons)
        {
            bool hover = button == left || button == right;
            button.GetComponent<UnityEngine.UI.Image>().color = hover?AeroUiTokens.ButtonHover:AeroUiTokens.ButtonFill;
            button.GetComponentInChildren<TextMeshProUGUI>().color = hover?AeroUiTokens.Mint:AeroUiTokens.Ink;
        }
    }
}
