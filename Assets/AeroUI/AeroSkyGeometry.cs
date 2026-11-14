using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One passive mesh for the authored regional linework. Coordinates are angular sky positions.
public sealed class AeroSkyGeometry : Graphic
{
    public const float WorldRadius = 20f;
    public const float CanvasScale = 0.018f;
    public const float AngularWidth = 54f;
    public const float AngularHeight = 30f;
    public const float Elevation = 27f;
    struct Stroke
    {
        public Vector2 A, B;
        public float Width;
        public Color Color;
    }
    readonly List<Stroke> strokes = new List<Stroke>(800);
    public int SegmentCount => strokes.Count;
    public void AddStroke(IList<Vector2> points, float width, Color tint)
    {
        for(int i=1;i<points.Count;i++)
            if((points[i]-points[i-1]).sqrMagnitude>0.0001f)
                strokes.Add(new Stroke { A=points[i-1], B=points[i], Width=width, Color=tint });
        SetVerticesDirty();
    }
    public static Vector3 Project(Vector2 p)
    {
        float azimuth=p.x/1120f*AngularWidth*Mathf.Deg2Rad;
        float elevation=(Elevation+p.y/620f*AngularHeight)*Mathf.Deg2Rad;
        float radius=WorldRadius/CanvasScale;
        return new Vector3(Mathf.Sin(azimuth)*Mathf.Cos(elevation),Mathf.Sin(elevation),Mathf.Cos(azimuth)*Mathf.Cos(elevation))*radius;
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        foreach(var line in strokes)
        {
            Vector2 direction=(line.B-line.A).normalized;
            Vector2 normal=new Vector2(-direction.y,direction.x)*(line.Width*0.5f);
            int start=mesh.currentVertCount;
            Color tint=line.Color*color;
            mesh.AddVert(Project(line.A-normal),tint,Vector2.zero);
            mesh.AddVert(Project(line.A+normal),tint,Vector2.zero);
            mesh.AddVert(Project(line.B+normal),tint,Vector2.zero);
            mesh.AddVert(Project(line.B-normal),tint,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);
            mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
