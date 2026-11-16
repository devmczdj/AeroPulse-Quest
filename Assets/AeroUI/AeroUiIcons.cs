using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Local IconMCP assets. Semantic names remain on GameObjects for help and checks.
public static class AeroUiIcons
{
    static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
    public static Sprite Get(string name)
    {
        if(cache.TryGetValue(name,out var sprite)&&sprite!=null)return sprite;
        sprite=Resources.Load<Sprite>("AeroIcons/"+name);
        if(sprite==null){var texture=Resources.Load<Texture2D>("AeroIcons/"+name);if(texture!=null)sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);}
        if(sprite==null)Debug.LogError("Missing HUD icon: "+name);cache[name]=sprite;return sprite;
    }
    public static void Apply(Button button,string name,float size=28)
    {
        foreach(var text in button.GetComponentsInChildren<TextMeshProUGUI>(true))text.enabled=false;
        var t=button.transform.Find("ActionIcon");Image icon;
        if(t==null)icon=AeroHudView.AddPanelImage(button.transform,"ActionIcon",Get(name),Vector2.zero,new Vector2(size,size),AeroUiTokens.Ink);
        else{icon=t.GetComponent<Image>();icon.sprite=Get(name);((RectTransform)t).sizeDelta=new Vector2(size,size);}
        icon.preserveAspect=true;icon.raycastTarget=false;
    }
    public static void Label(Button button,string name,string text,float size=28)
    {
        Apply(button,name,size);float width=((RectTransform)button.transform).rect.width;
        ((RectTransform)button.transform.Find("ActionIcon")).anchoredPosition=new Vector2(-width/2+26,0);
        var label=button.GetComponentInChildren<TextMeshProUGUI>(true);label.enabled=true;label.text=text;
        label.font=AeroUiFonts.Interface;label.fontSize=22;label.characterSpacing=0;label.alignment=TextAlignmentOptions.Left;
        label.rectTransform.anchoredPosition=new Vector2(20,0);label.rectTransform.sizeDelta=new Vector2(width-58,44);
    }
    public static void Set(Button button,string name)
    {var t=button.transform.Find("ActionIcon");if(t==null)Apply(button,name);else t.GetComponent<Image>().sprite=Get(name);}
}
