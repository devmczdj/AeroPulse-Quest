using TMPro;
using UnityEngine;

public class AeroHudSettingsView : MonoBehaviour
{
    PinchHudExperiment owner;
    public UnityEngine.UI.Button ModeButton, DistanceButton, HeightButton, RecenterButton, ResetButton;
    UnityEngine.UI.Button[] buttons;
    public static AeroHudSettingsView Build(Transform parent)
    {
        var root = AeroHudView.NewRect("SettingsContent", parent, Vector2.zero, new Vector2(760,430));
        var view = root.gameObject.AddComponent<AeroHudSettingsView>();
        view.ModeButton = view.Row("FollowMode", "FOLLOW MODE", "SOFT FOLLOW", 88);
        view.DistanceButton = view.Row("ViewDistance", "PANEL DISTANCE", "1.8 M", 28);
        view.HeightButton = view.Row("PadHeight", "HAND PAD HEIGHT", "NORMAL / 35 MM", -32);
        view.RecenterButton = AeroHudView.ActionButton(root,"Recenter","RECENTER",new Vector2(-172,-98),new Vector2(328,48),new Vector3(328,52,8));
        view.ResetButton = AeroHudView.ActionButton(root,"ResetView","RESET DEFAULTS",new Vector2(172,-98),new Vector2(328,48),new Vector3(328,52,8));
        view.buttons = new[] {view.ModeButton,view.DistanceButton,view.HeightButton,view.RecenterButton,view.ResetButton};
        view.ModeButton.onClick.AddListener(() => { if(view.owner != null) {view.owner.Follow.CycleMode();view.Refresh();} });
        view.DistanceButton.onClick.AddListener(() => { if(view.owner != null) {view.owner.Follow.CycleDistance();view.owner.Recenter();view.Refresh();} });
        view.HeightButton.onClick.AddListener(() => { if(view.owner != null) {view.owner.HandEntry.CycleHeight();view.Refresh();} });
        view.RecenterButton.onClick.AddListener(() => { if(view.owner != null) view.owner.Recenter(); });
        view.ResetButton.onClick.AddListener(() => { if(view.owner != null) {view.owner.ResetViewOptions();view.Refresh();} });
        return view;
    }
    UnityEngine.UI.Button Row(string name,string label,string value,float y)
    {
        AeroHudView.Text(transform,name+"Heading",label,new Vector2(-344,y),new Vector2(240,36),18,
            AeroUiFonts.SemiBold,AeroUiTokens.Secondary,TextAlignmentOptions.Left,0,0);
        return AeroHudView.ActionButton(transform,name,value,new Vector2(170,y),new Vector2(344,48),new Vector3(352,52,8));
    }
    public void Bind(PinchHudExperiment hud) { owner = hud; Refresh(); }
    public void Refresh()
    {
        if(owner == null) return;
        ModeButton.GetComponentInChildren<TextMeshProUGUI>().text = owner.Follow.ModeLabel;
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
