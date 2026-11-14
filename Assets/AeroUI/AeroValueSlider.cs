using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One captured pointer previews a value. Release commits; loss/hide cancels.
public sealed class AeroValueSlider : MonoBehaviour
{
    public static AeroValueSlider Active { get; private set; }
    public Button Button { get; private set; }
    public float Value { get; private set; }
    public bool Interacting => InteractionOwner!=null;
    public string InteractionOwner { get; private set; }
    float minimum,maximum,step,startValue,width;
    Matrix4x4 capturedInverse; Plane capturedPlane;
    RectTransform thumb,fill;TextMeshProUGUI valueLabel;Func<float,string> formatter;Action<float> changed;
    public static AeroValueSlider Build(Transform parent,string name,Vector2 position,Vector2 size,float min,float max,float initial,float increment,Func<float,string> format,Action<float> onChange)
    {
        var root=AeroHudView.NewRect(name,parent,position,size);var slider=root.gameObject.AddComponent<AeroValueSlider>();
        slider.minimum=min;slider.maximum=max;slider.step=increment;slider.width=size.x-20;slider.formatter=format;slider.changed=onChange;
        slider.valueLabel=AeroHudView.Text(root,"Value", "",new Vector2(size.x/2-150,22),new Vector2(150,28),22,AeroUiFonts.Interface,AeroUiTokens.Ink,TextAlignmentOptions.Right,0,0);
        var track=AeroHudView.AddPanelImage(root,"TrackHit",AeroUiSprites.White,new Vector2(0,-6),new Vector2(size.x,40),new Color(0,0,0,0));
        slider.Button=track.gameObject.AddComponent<Button>();slider.Button.targetGraphic=track;slider.Button.transition=Selectable.Transition.None;
        track.gameObject.AddComponent<BoxCollider>().size=new Vector3(size.x,40,8);
        AeroHudView.AddPanelImage(track.transform,"Rail",AeroUiSprites.White,Vector2.zero,new Vector2(slider.width,4),new Color(.3f,.38f,.38f,1));
        slider.fill=(RectTransform)AeroHudView.AddPanelImage(track.transform,"Fill",AeroUiSprites.White,Vector2.zero,new Vector2(1,4),AeroUiTokens.Mint).transform;
        slider.thumb=(RectTransform)AeroHudView.AddPanelImage(track.transform,"Thumb",AeroUiSprites.Dot,Vector2.zero,new Vector2(24,24),AeroUiTokens.Ink).transform;
        slider.SetValueWithoutNotify(initial);return slider;
    }
    public void SetValueWithoutNotify(float value){if(Interacting)return;Value=Quantize(value);Paint();}
    public void SetInteractable(bool enabled){Button.interactable=enabled;if(!enabled)CancelInteraction();valueLabel.color=enabled?AeroUiTokens.Ink:AeroUiTokens.Tertiary;thumb.GetComponent<Image>().color=enabled?AeroUiTokens.Ink:AeroUiTokens.Tertiary;}
    float Quantize(float value)=>Mathf.Clamp(Mathf.Round((value-minimum)/step)*step+minimum,minimum,maximum);
    void Paint(){float t=Mathf.InverseLerp(minimum,maximum,Value);thumb.anchoredPosition=new Vector2((t-.5f)*width,0);fill.sizeDelta=new Vector2(Mathf.Max(1,t*width),4);fill.anchoredPosition=new Vector2((t-1)*width*.5f,0);valueLabel.text=formatter(Value);}
    public bool BeginInteraction(string owner)
    {
        if(!isActiveAndEnabled||!Button.IsInteractable()||Active!=null)return false;
        Active=this;InteractionOwner=owner;startValue=Value;capturedInverse=Button.transform.worldToLocalMatrix;capturedPlane=new Plane(Button.transform.forward,Button.transform.position);return true;
    }
    public void SetFromWorldPoint(Vector3 point){if(!Interacting)return;Value=Quantize(Mathf.Lerp(minimum,maximum,Mathf.Clamp01(capturedInverse.MultiplyPoint3x4(point).x/width+.5f)));Paint();}
    public void SetFromRay(Ray ray){if(Interacting&&capturedPlane.Raycast(ray,out float distance)&&distance>=0&&distance<5)SetFromWorldPoint(ray.GetPoint(distance));}
    public void EndInteraction(string owner){if(InteractionOwner!=owner)return;float value=Value;InteractionOwner=null;if(Active==this)Active=null;if(!Mathf.Approximately(value,startValue))changed?.Invoke(value);}
    public void CancelInteraction(){if(!Interacting)return;InteractionOwner=null;if(Active==this)Active=null;Value=startValue;Paint();}
    void OnDisable()=>CancelInteraction();
    void OnDestroy(){if(Active==this)Active=null;}
}
