using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Passive geography painted into the sky, independent from the nearby interactive HUD.
// Authored schematic only: no GPS or terrain registration.
public class AeroSkyMap : MonoBehaviour
{
    public AeroRouteDemo Route { get; private set; }
    public bool Visible => gameObject.activeSelf;
    // Kept for older Editor-source compatibility. The environment layer creates no controls.
    public UnityEngine.UI.Button DetailButton, AdvanceButton, RecallButton, HideButton;
    public RectTransform CurrentMarker, NextMarker;
    public TextMeshProUGUI LocationLabel;
    public AeroSkyGeometry Geometry { get; private set; }
    public float Heading => heading;
    public bool Paused => paused;
    Camera eye;
    Canvas canvas;
    float heading, outsideTime;
    bool paused, recentering;

    public static AeroSkyMap Build(Camera camera,AeroRouteDemo route,Action detail=null,Action recall=null,Action hide=null)
    {
        var go=new GameObject("SkyMapLayer",typeof(RectTransform),typeof(Canvas));
        var rect=(RectTransform)go.transform;
        rect.sizeDelta=new Vector2(1120,620);
        rect.localScale=Vector3.one*AeroSkyGeometry.CanvasScale;
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
        var view=go.AddComponent<AeroSkyMap>();view.eye=camera;view.canvas=canvas;view.Route=route;
        view.Create();route.Changed+=view.Refresh;view.Refresh();view.Recall();go.SetActive(false);return view;
    }
    void OnDestroy() { if(Route!=null)Route.Changed-=Refresh; }
    public void Show() { gameObject.SetActive(true);canvas.enabled=!paused;Recall(); }
    public void Hide() { gameObject.SetActive(false);outsideTime=0;recentering=false; }
    public void SetPaused(bool value)
    {
        paused=value;canvas.enabled=!value;outsideTime=0;recentering=false;
        if(!value&&Visible)Tick(0);
    }
    public void Recall()
    {
        if(eye==null)return;
        heading=ViewHeading();outsideTime=0;recentering=false;
        ApplyPose();
    }
    float ViewHeading()
    {
        Vector3 forward=Vector3.ProjectOnPlane(eye.transform.forward,Vector3.up);
        return forward.sqrMagnitude>0.01f?Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg:heading;
    }
    void ApplyPose()
    {
        transform.SetPositionAndRotation(eye.transform.position,Quaternion.Euler(0,heading,0));
    }
    // Owner calls once in LateUpdate. Translation follows the viewer, while rotation can be looked away from.
    public void Tick(float dt)
    {
        if(paused||!Visible||eye==null)return;
        dt=Mathf.Clamp(dt,0,0.05f);
        float target=ViewHeading();float error=Mathf.Abs(Mathf.DeltaAngle(heading,target));
        outsideTime=error>70f?outsideTime+dt:0;
        if(outsideTime>=0.7f)recentering=true;
        if(recentering)
        {
            heading=Mathf.MoveTowardsAngle(heading,target,32f*dt);
            if(Mathf.Abs(Mathf.DeltaAngle(heading,target))<1f){heading=target;recentering=false;outsideTime=0;}
        }
        ApplyPose();
    }
    public static Vector2 MapPoint(Vector2 point) => point*2.25f+new Vector2(-35,-20);
    public static Vector3 SkyPoint(Vector2 point) => AeroSkyGeometry.Project(MapPoint(point));
    void Create()
    {
        var geo=new GameObject("RegionalGeography",typeof(RectTransform),typeof(CanvasRenderer),typeof(AeroSkyGeometry));
        geo.transform.SetParent(transform,false);
        var rect=(RectTransform)geo.transform;rect.sizeDelta=new Vector2(1120,620);
        Geometry=geo.GetComponent<AeroSkyGeometry>();Geometry.raycastTarget=false;
        // Nested, irregular ridges retain a shared silhouette rather than concentric decorative circles.
        for(int hill=0;hill<3;hill++)
        {
            Vector2 center=hill==0?new Vector2(-260,80):hill==1?new Vector2(240,30):new Vector2(-40,-165);
            float rx=hill==2?155:215,ry=hill==2?78:125;
            float angle=(-22+hill*31)*Mathf.Deg2Rad;
            for(int ring=0;ring<4;ring++)
            {
                var points=new List<Vector2>(65);
                float scale=1-ring*0.18f;
                for(int i=0;i<=64;i++)
                {
                    float a=i*Mathf.PI*2/64;
                    float ripple=1+0.13f*Mathf.Sin(3*a+hill)+0.075f*Mathf.Sin(5*a+hill*0.7f)+0.035f*Mathf.Cos(8*a);
                    var q=new Vector2(Mathf.Cos(a)*rx*scale*ripple,Mathf.Sin(a)*ry*scale*ripple);
                    points.Add(center+new Vector2(q.x*Mathf.Cos(angle)-q.y*Mathf.Sin(angle),q.x*Mathf.Sin(angle)+q.y*Mathf.Cos(angle)));
                }
                Geometry.AddStroke(points,1.2f,new Color(0.76f,0.86f,0.83f,0.28f));
            }
        }
        var creek=new List<Vector2>(73);
        for(int i=0;i<=72;i++){float y=-285+i*(570f/72);creek.Add(new Vector2(RiverX(y),y));}
        Geometry.AddStroke(creek,2f,new Color(0.64f,0.81f,0.85f,0.48f));
        for(int branch=0;branch<6;branch++)
        {
            float y=-220+branch*81;var start=new Vector2(RiverX(y),y);float side=branch%2==0?-1:1;
            var tributary=new List<Vector2>(13){start};
            for(int i=1;i<=12;i++)tributary.Add(start+new Vector2(side*i*17,Mathf.Sin(i*0.42f+branch)*12+i*6));
            Geometry.AddStroke(tributary,1.1f,new Color(0.70f,0.82f,0.83f,0.28f));
        }
        var route=new List<Vector2>(Route.Count);
        for(int i=0;i<Route.Count;i++)route.Add(MapPoint(Route.StopAt(i).Position));
        Geometry.AddStroke(route,3f,new Color(0.62f,0.90f,0.77f,0.85f));
        for(int i=0;i<Route.Count;i++)
        {
            var p=MapPoint(Route.StopAt(i).Position);
            var stop=AeroHudView.AddPanelImage(transform,"SkyStop"+i,AeroUiSprites.Dot,Vector2.zero,new Vector2(7,7),new Color(0.82f,0.90f,0.86f,0.85f));
            Place((RectTransform)stop.transform,p);
            Label("SkyPlace"+i,Route.StopAt(i).Name,p+new Vector2(0,i%2==0?-25:25),new Vector2(180,36),18,new Color(0.85f,0.92f,0.88f,0.9f));
        }
        CurrentMarker=(RectTransform)AeroHudView.AddPanelImage(transform,"SkyCurrent",AeroUiSprites.Dot,Vector2.zero,new Vector2(24,24),AeroUiTokens.Mint).transform;
        AeroHudView.AddPanelImage(CurrentMarker,"CurrentCore",AeroUiSprites.Dot,Vector2.zero,new Vector2(11,11),new Color(0.03f,0.14f,0.13f,1));
        NextMarker=(RectTransform)AeroHudView.AddPanelImage(transform,"SkyNext",AeroUiSprites.White,Vector2.zero,new Vector2(13,13),new Color(0.84f,0.79f,0.60f,0.95f)).transform;
        LocationLabel=Label("SkyDisclosure","SCHEMATIC / SIMULATED",new Vector2(-375,232),new Vector2(300,32),16,new Color(0.75f,0.85f,0.81f,0.7f));
        Label("North","N",new Vector2(435,222),new Vector2(60,32),16,new Color(0.77f,0.86f,0.82f,0.8f));
    }
    static float RiverX(float y) => -45+0.45f*y+30*Mathf.Sin(y*0.015f)+11*Mathf.Sin(y*0.042f);
    static void Place(RectTransform rect,Vector2 point)
    {
        Vector3 local=AeroSkyGeometry.Project(point);
        rect.anchoredPosition3D=local;
        rect.localRotation=Quaternion.LookRotation(local.normalized,Vector3.up);
    }
    void Refresh()
    {
        Place(CurrentMarker,MapPoint(Route.Current.Position));
        NextMarker.gameObject.SetActive(Route.NextIndex>=0);
        if(Route.NextIndex>=0)
        {
            Place(NextMarker,MapPoint(Route.StopAt(Route.NextIndex).Position));
            NextMarker.localRotation*=Quaternion.Euler(0,0,45);
        }
    }
    // There is deliberately no hover UI on an environmental sky layer.
    public void SetHover(UnityEngine.UI.Button left,UnityEngine.UI.Button right) { }
    TextMeshProUGUI Label(string name,string value,Vector2 point,Vector2 size,float fontSize,Color tint)
    {
        var text=AeroHudView.Text(transform,name,value,Vector2.zero,size,fontSize,AeroUiFonts.SemiBold,tint,TextAlignmentOptions.Center,0,0,true);
        Place((RectTransform)text.transform,point);return text;
    }
}
