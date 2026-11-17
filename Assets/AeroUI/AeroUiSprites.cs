using UnityEngine;

// The PNGs are generated from DESIGN.md's shape rules; load them as full-rect UI sprites.
public static class AeroUiSprites
{
    static Sprite shadow;
    static Sprite sheen;
    static Sprite stroke;
    static Sprite pulse;
    static Sprite logo;
    static Sprite dot;
    static Sprite glow;
    static Sprite pill;
    static Sprite solidPanel;
    static Sprite white;
    static Sprite tint;
    static Sprite pillStroke;

    public static Sprite Shadow { get { return shadow != null ? shadow : shadow = Load("panel_shadow"); } }
    public static Sprite Sheen { get { return sheen != null ? sheen : sheen = Load("panel_sheen"); } }
    public static Sprite Stroke { get { return stroke != null ? stroke : stroke = Load("panel_stroke"); } }
    public static Sprite Pulse { get { return pulse != null ? pulse : pulse = Load("pulse_line"); } }
    public static Sprite Logo { get { return logo != null ? logo : logo = Load("logo_mark"); } }
    public static Sprite Dot { get { return dot != null ? dot : dot = Load("dot"); } }
    public static Sprite Glow { get { return glow != null ? glow : glow = Load("glow"); } }
    public static Sprite Pill { get { return pill != null ? pill : pill = Load("pill"); } }
    public static Sprite SolidPanel
    {
        get
        {
            if(solidPanel!=null)return solidPanel;
            var source=Pill;if(source==null)return White;var texture=source.texture;float corner=texture.height*.5f;
            solidPanel=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect,new Vector4(corner,corner,corner,corner));
            return solidPanel;
        }
    }
    public static Sprite Tint { get { return tint != null ? tint : tint = Load("panel_tint"); } }
    public static Sprite PillStroke { get { return pillStroke != null ? pillStroke : pillStroke = Load("pill_stroke"); } }
    public static Sprite White
    {
        get
        {
            if (white == null)
                white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                                      new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            return white;
        }
    }

    static Sprite Load(string name)
    {
        var texture = Resources.Load<Texture2D>("AeroUI/Textures/" + name);
        if (texture == null)
        {
            Debug.LogWarning("[AeroUI] Missing texture " + name);
            return null;
        }
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                             new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
}
