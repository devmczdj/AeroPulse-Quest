using System;
using TMPro;
using UnityEngine;

// Transparent, invented regional geography. No geolocation or terrain registration.
public class AeroSkyMap : MonoBehaviour
{
    public AeroRouteDemo Route { get; private set; }
    public bool Visible => gameObject.activeSelf;
    public UnityEngine.UI.Button DetailButton, AdvanceButton, RecallButton, HideButton;
    public RectTransform CurrentMarker, NextMarker;
    public TextMeshProUGUI LocationLabel;
    UnityEngine.UI.Button[] buttons;
    Camera eye;

    public static AeroSkyMap Build(Camera camera,AeroRouteDemo route,Action detail,Action recall,Action hide)
    {
        var go=new GameObject("SkyMapLayer",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
        var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(1120,620);rect.localScale=Vector3.one*0.0026f;
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
        var view=go.AddComponent<AeroSkyMap>();view.eye=camera;view.Route=route;view.Create();
        view.DetailButton.onClick.AddListener(()=>detail());
        view.AdvanceButton.onClick.AddListener(route.Advance);
        view.RecallButton.onClick.AddListener(()=>recall());view.HideButton.onClick.AddListener(()=>hide());
        route.Changed+=view.Refresh;view.Refresh();go.SetActive(false);return view;
    }
    void OnDestroy() { if(Route!=null) Route.Changed-=Refresh; }
    public void Show() { gameObject.SetActive(true);Recall(); }
    public void Recall()
    {
        var forward=Vector3.ProjectOnPlane(eye.transform.forward,Vector3.up).normalized;
        if(forward.sqrMagnitude<0.01f)forward=Vector3.forward;
        var right=Vector3.Cross(Vector3.up,forward);
        var center=eye.transform.position+forward*3.6f-right*0.45f+Vector3.up*1.55f;
        transform.SetPositionAndRotation(center,Quaternion.LookRotation(center-eye.transform.position,Vector3.up));
        Physics.SyncTransforms();
    }
    public void Hide() => gameObject.SetActive(false);
    public static Vector2 MapPoint(Vector2 point) => point*2.25f+new Vector2(-35,-20);
    void Create()
    {
        // Topographic contours and watercourses are authored schematics, not copied map data.
        for(int hill=0;hill<2;hill++)
            for(int ring=0;ring<4;ring++)
            {
                Vector2 center=hill==0?new Vector2(-270,35):new Vector2(255,60);
                float rx=210-ring*35,ry=140-ring*23;
                Vector2 previous=Vector2.zero;
                for(int i=0;i<=28;i++)
                {
                    float a=i*Mathf.PI*2/28;
                    float ripple=1+0.11f*Mathf.Sin(a*3+hill)+0.05f*Mathf.Sin(a*7+ring);
                    Vector2 p=center+new Vector2(Mathf.Cos(a)*rx*ripple,Mathf.Sin(a)*ry*ripple);
                    if(i>0)Line("Contour"+hill+"-"+ring+"-"+i,previous,p,2.2f,new Color(0.79f,0.89f,0.87f,0.2f));
                    previous=p;
                }
            }
        Vector2 last=Vector2.zero;
        for(int i=0;i<=30;i++)
        {
            float y=-205+i*14;
            Vector2 p=new Vector2(25+65*Mathf.Sin(y*0.012f)+25*Mathf.Sin(y*0.026f),y);
            if(i>0)Line("Creek"+i,last,p,3f,new Color(0.63f,0.84f,0.91f,0.45f));
            last=p;
        }
        // Minor tributaries give the contour layer a regional drainage structure.
        for(int branch=0;branch<6;branch++)
        {
            float y=-145+branch*58;
            var start=new Vector2(25+65*Mathf.Sin(y*0.012f)+25*Mathf.Sin(y*0.026f),y);
            Vector2 previous=start;
            float side=branch%2==0?-1:1;
            for(int j=1;j<=7;j++)
            {
                var point=start+new Vector2(side*j*34,Mathf.Sin(j*0.65f+branch)*17+j*10);
                Line("Tributary"+branch+"-"+j,previous,point,2.2f,new Color(0.67f,0.84f,0.88f,0.22f));
                previous=point;
            }
        }
        for(int i=1;i<Route.Count;i++)Line("Trail"+i,MapPoint(Route.StopAt(i-1).Position),MapPoint(Route.StopAt(i).Position),4.5f,new Color(0.64f,0.96f,0.82f,0.9f));
        for(int i=0;i<Route.Count;i++)
        {
            var p=MapPoint(Route.StopAt(i).Position);
            AeroHudView.AddPanelImage(transform,"SkyStop"+i,AeroUiSprites.Dot,p,new Vector2(10,10),AeroUiTokens.Ink);
            Label("SkyPlace"+i,Route.StopAt(i).Name,p+new Vector2(0,i==0?33:i%2==0?-31:33),new Vector2(190,40),20,AeroUiTokens.Ink,true);
        }
        CurrentMarker=(RectTransform)AeroHudView.AddPanelImage(transform,"SkyCurrent",AeroUiSprites.Dot,Vector2.zero,new Vector2(27,27),AeroUiTokens.Mint).transform;
        AeroHudView.AddPanelImage(CurrentMarker,"CurrentCore",AeroUiSprites.Dot,Vector2.zero,new Vector2(12,12),new Color(0.03f,0.14f,0.13f,1));
        NextMarker=(RectTransform)AeroHudView.AddPanelImage(transform,"SkyNext",AeroUiSprites.White,Vector2.zero,new Vector2(17,17),AeroUiTokens.Amber).transform;
        NextMarker.localRotation=Quaternion.Euler(0,0,45);
        Label("SkyTitle","AEROPULSE TRAIL",new Vector2(-520,252),new Vector2(650,48),28,AeroUiTokens.Ink);
        Label("SkyDisclosure","SCHEMATIC REGION / SIMULATED POSITION",new Vector2(-520,218),new Vector2(800,34),16,AeroUiTokens.Secondary);
        Label("North","N UP",new Vector2(430,235),new Vector2(100,36),18,AeroUiTokens.Ink);
        LocationLabel=Label("SkyLocation","",new Vector2(-520,-211),new Vector2(1040,36),20,AeroUiTokens.Ink);
        DetailButton=AeroHudView.ActionButton(transform,"SkyDetail","DETAIL",new Vector2(-330,-270),new Vector2(200,56),new Vector3(210,72,8));
        AdvanceButton=AeroHudView.ActionButton(transform,"SkyAdvance","ADVANCE",new Vector2(-100,-270),new Vector2(200,56),new Vector3(210,72,8));
        RecallButton=AeroHudView.ActionButton(transform,"SkyRecall","RECALL",new Vector2(130,-270),new Vector2(200,56),new Vector3(210,72,8));
        HideButton=AeroHudView.ActionButton(transform,"SkyHide","HIDE",new Vector2(360,-270),new Vector2(180,56),new Vector3(200,72,8));
        buttons=new[]{DetailButton,AdvanceButton,RecallButton,HideButton};
    }
    void Refresh()
    {
        CurrentMarker.anchoredPosition=MapPoint(Route.Current.Position);
        NextMarker.gameObject.SetActive(Route.NextIndex>=0);
        if(Route.NextIndex>=0)NextMarker.anchoredPosition=MapPoint(Route.StopAt(Route.NextIndex).Position);
        LocationLabel.text="YOU  "+Route.Current.Name+"     /     NEXT  "+Route.NextName+"     /     DEMO "+(Route.CurrentIndex+1)+" / "+Route.Count;
        AdvanceButton.GetComponentInChildren<TextMeshProUGUI>().text=Route.NextIndex<0?"RESET":"ADVANCE";
    }
    public void SetHover(UnityEngine.UI.Button left,UnityEngine.UI.Button right)
    {
        foreach(var b in buttons)
        {
            bool hover=b==left||b==right;
            b.GetComponent<UnityEngine.UI.Image>().color=hover?AeroUiTokens.ButtonHover:AeroUiTokens.ButtonFill;
            b.GetComponentInChildren<TextMeshProUGUI>().color=hover?AeroUiTokens.Mint:AeroUiTokens.Ink;
        }
    }
    void Line(string name,Vector2 a,Vector2 b,float width,Color color)
    {
        var line=AeroHudView.AddPanelImage(transform,name,AeroUiSprites.Pill,(a+b)*0.5f,new Vector2((b-a).magnitude,width),color);
        line.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
    }
    TextMeshProUGUI Label(string name,string value,Vector2 pos,Vector2 size,float fontSize,Color color,bool centered=false)
        =>AeroHudView.Text(transform,name,value,pos,size,fontSize,AeroUiFonts.SemiBold,color,centered?TextAlignmentOptions.Center:TextAlignmentOptions.Left,0,0,centered);
}
