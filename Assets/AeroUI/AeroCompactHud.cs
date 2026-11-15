using TMPro;
using UnityEngine;

// Small, opt-in view-relative readout; independent of detail and sky placement.
public class AeroCompactHud : MonoBehaviour
{
    public enum Placement { Hidden, Left, Right }
    public Placement Side { get; private set; }
    public AeroRouteDemo Route { get; private set; }
    public TextMeshProUGUI NextLabel { get; private set; }
    public UnityEngine.UI.Button OpenButton { get; private set; }
    public bool FallbackVisible => fallback != null && fallback.activeSelf;
    GameObject fallback;
    TextMeshProUGUI openLabel;
    bool paused, fallbackRequested, recallRequested;
    public System.Action<Placement> PlacementChanged;
    public static AeroCompactHud Build(Camera camera,AeroRouteDemo route,System.Action open)
    {
        var go=new GameObject("CompactViewHUD",typeof(RectTransform),typeof(Canvas));
        go.transform.SetParent(camera.transform,false);go.transform.localScale=Vector3.one*0.0009f;
        ((RectTransform)go.transform).sizeDelta=new Vector2(320,132);
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
        var widget=go.AddComponent<AeroCompactHud>();widget.Route=route;
        var backing=AeroHudView.AddPanelImage(go.transform,"ReadoutBacking",Resources.Load<Sprite>("JetStyle/RoundedRect"),Vector2.zero,new Vector2(320,132),new Color(0.025f,0.06f,0.07f,0.84f));
        backing.type=UnityEngine.UI.Image.Type.Sliced;backing.pixelsPerUnitMultiplier=4;
        AeroHudView.Text(go.transform,"CompactDisclosure","SIM DATA",new Vector2(-140,45),new Vector2(280,29),18,AeroUiFonts.SemiBold,AeroUiTokens.Secondary,TextAlignmentOptions.Left,0,0);
        AeroHudView.Text(go.transform,"CompactMetrics","142 BPM   5:20",new Vector2(-140,7),new Vector2(280,45),28,AeroUiFonts.SemiBold,AeroUiTokens.Ink,TextAlignmentOptions.Left,0,0);
        widget.NextLabel=AeroHudView.Text(go.transform,"CompactNext","",new Vector2(-140,-34),new Vector2(280,34),20,AeroUiFonts.SemiBold,AeroUiTokens.Mint,TextAlignmentOptions.Left,0,0);
        widget.fallback=new GameObject("FallbackOpenHUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
        widget.fallback.transform.SetParent(camera.transform,false);widget.fallback.transform.localPosition=new Vector3(0,-0.42f,1.1f);widget.fallback.transform.localScale=Vector3.one*0.001f;
        ((RectTransform)widget.fallback.transform).sizeDelta=new Vector2(220,80);
        var fallbackCanvas=widget.fallback.GetComponent<Canvas>();fallbackCanvas.renderMode=RenderMode.WorldSpace;fallbackCanvas.worldCamera=camera;
        widget.OpenButton=AeroHudView.ActionButton(widget.fallback.transform,"FallbackOpenButton","OPEN HUD",Vector2.zero,new Vector2(220,64),new Vector3(232,80,8));
        widget.openLabel=widget.OpenButton.GetComponentInChildren<TextMeshProUGUI>();AeroUiIcons.Apply(widget.OpenButton,"browser",40);
        widget.OpenButton.onClick.AddListener(()=>open());widget.fallback.SetActive(false);
        route.Changed+=widget.Refresh;widget.Refresh();widget.SetPlacement(Placement.Hidden);return widget;
    }
    void OnDestroy()
    {
        if(Route!=null)Route.Changed-=Refresh;
        if(fallback!=null) { if(Application.isPlaying)Destroy(fallback);else DestroyImmediate(fallback); }
    }
    void Refresh() { if(NextLabel!=null)NextLabel.text="NEXT  "+Route.NextName; }
    public void CyclePlacement() => SetPlacement((Placement)(((int)Side+1)%3));
    public void SetPlacement(Placement side)
    {
        bool changed=Side!=side;Side=side;
        if(PlacementChanged==null){transform.localPosition=new Vector3(side==Placement.Right?.42f:-.42f,-.14f,1.05f);transform.localRotation=Quaternion.identity;}
        gameObject.SetActive(side!=Placement.Hidden&&!paused);if(changed&&PlacementChanged!=null)PlacementChanged(side);
    }
    public void SetPaused(bool value) { paused=value;SetPlacement(Side);SetFallbackVisible(fallbackRequested,recallRequested); }
    public void SetFallbackVisible(bool value,bool recall=false)
    {
        fallbackRequested=value;recallRequested=recall;
        bool visible=value&&!paused;if(fallback.activeSelf!=visible)fallback.SetActive(visible);
        var label=recall?"RECALL HUD":"OPEN HUD";
        if(openLabel.text!=label){openLabel.text=label;AeroUiIcons.Set(OpenButton,recall?"crosshair":"browser");}
    }
}
