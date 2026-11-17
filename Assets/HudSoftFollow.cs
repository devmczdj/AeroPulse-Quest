using UnityEngine;

// A delayed reference frame carries a user-selected offset, rather than chasing
// the panel's centre back to the middle of the view.
public class HudSoftFollow : MonoBehaviour
{
    public enum FollowMode { Soft, Quick, Fixed }
    public FollowMode Mode { get; private set; }
    public float Distance { get; private set; } = .65f;
    public float UserScale { get; private set; } = 1f;
    public Pose Offset { get; private set; } = new Pose(new Vector3(0,0,.65f),Quaternion.identity);
    public string ModeLabel => Mode==FollowMode.Soft?"SOFT FOLLOW":Mode==FollowMode.Quick?"QUICK FOLLOW":"FIXED HERE";
    public const float MinScale=.8f,MaxScale=1.5f;
    Transform view;
    Vector3 previousViewPosition;
    Quaternion referenceRotation;
    float outsideTime,pressFreezeTime,scalePerMeter=.0015f/1.8f,defaultScalePerMeter=.0015f/1.8f;
    bool moving;
    public void Initialize(Transform camera) { view=camera;referenceRotation=ViewRotation();previousViewPosition=view.position; }
    public void ConfigureScale(float perMeter,float distance){scalePerMeter=defaultScalePerMeter=perMeter;Distance=distance;Offset=new Pose(new Vector3(0,0,distance),Quaternion.identity);}
    public void SetMode(FollowMode value){Mode=value;outsideTime=pressFreezeTime=0;moving=false;SyncView();}
    public void CycleMode()=>SetMode((FollowMode)(((int)Mode+1)%3));
    public void SetDistance(float value){float next=Mathf.Clamp(value,.55f,2.2f);float radius=Mathf.Max(.01f,Offset.position.magnitude);float physical=transform.localScale.x;Offset=new Pose(Offset.position*(next/radius),Offset.rotation);Distance=next;scalePerMeter=physical/(Distance*UserScale);ApplyScale();}
    public void CycleDistance()=>SetDistance(Distance<1.6f?1.8f:Distance<2f?2.2f:1.4f);
    public void SetUserScale(float value){UserScale=Mathf.Clamp(value,MinScale,MaxScale);ApplyScale();}
    public void ApplyScale()=>transform.localScale=Vector3.one*(scalePerMeter*Distance*UserScale);
    public void SyncView(){if(view!=null)previousViewPosition=view.position;}
    Quaternion ViewRotation()
    {
        if(view==null)return Quaternion.identity;
        var f=view.forward;float pitch=Mathf.Clamp(Mathf.Asin(Mathf.Clamp(f.y,-1,1))*Mathf.Rad2Deg,-20,20);
        return Quaternion.Euler(-pitch,Mathf.Atan2(f.x,f.z)*Mathf.Rad2Deg,0);
    }
    public void CaptureOffset()
    {
        referenceRotation=ViewRotation();
        Offset=new Pose(Quaternion.Inverse(referenceRotation)*(transform.position-view.position),Quaternion.identity);
        Distance=Mathf.Max(.01f,Offset.position.magnitude);scalePerMeter=transform.localScale.x/(Distance*UserScale);
        outsideTime=pressFreezeTime=0;moving=false;SyncView();
    }
    public void RestoreOptions(float distance,float size,Pose offset,FollowMode mode,float physicalScale=-1)
    {Distance=distance;UserScale=size;if(physicalScale>0)scalePerMeter=physicalScale/(distance*size);Offset=offset;referenceRotation=ViewRotation();SetMode(mode);ApplyScale();}
    public void ResetOptions(){scalePerMeter=defaultScalePerMeter;UserScale=1;Distance=.65f;Mode=FollowMode.Soft;SnapToView();}
    public void SnapToView()
    {
        referenceRotation=ViewRotation();Offset=new Pose(new Vector3(0,0,Distance),Quaternion.identity);
        ApplyOffset();outsideTime=pressFreezeTime=0;moving=false;SyncView();ApplyScale();
    }
    public void ApplyOffset(){transform.SetPositionAndRotation(view.position+referenceRotation*Offset.position,view.rotation);SyncView();}
    public void Tick(float delta,bool interacting)
    {
        if(view==null)return;
        float elapsed=Mathf.Clamp(delta,0,.25f);delta=Mathf.Clamp(delta,0,.05f);
        var translation=view.position-previousViewPosition;SyncView();transform.rotation=view.rotation;if(Mode==FollowMode.Fixed)return;
        transform.position+=translation;
        // Hidden following panels still travel with the viewer. Reopening must not
        // discard movement accumulated while their Canvas was inactive.
        if(!gameObject.activeInHierarchy)return;
        pressFreezeTime=interacting?pressFreezeTime+elapsed:0;
        if(interacting&&pressFreezeTime<.6f){outsideTime=0;moving=false;return;}
        var desired=ViewRotation();float angle=Quaternion.Angle(referenceRotation,desired);
        float threshold=Mode==FollowMode.Quick?10:18;
        bool outside=angle>threshold;
        outsideTime=(outside||(outsideTime>0&&angle>threshold-4))?outsideTime+elapsed:0;
        if(outsideTime>=(Mode==FollowMode.Quick?.12f:.32f))moving=true;
        if(!moving)return;
        referenceRotation=Quaternion.RotateTowards(referenceRotation,desired,(Mode==FollowMode.Quick?90:55)*delta);
        var target=view.position+referenceRotation*Offset.position;
        transform.position=Vector3.MoveTowards(transform.position,target,(Mode==FollowMode.Quick?2:1.2f)*delta);
        transform.rotation=view.rotation;
        if(Vector3.Distance(transform.position,target)<.01f&&Quaternion.Angle(referenceRotation,desired)<1)
        {moving=false;outsideTime=0;}
    }
}
