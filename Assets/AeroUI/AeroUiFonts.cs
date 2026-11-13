using TMPro;
using UnityEngine;

public static class AeroUiFonts
{
    static TMP_FontAsset regular;
    static TMP_FontAsset medium;
    static TMP_FontAsset semibold;

    public static TMP_FontAsset Regular
    {
        get { return regular != null ? regular : regular = Load("Inter-Regular_SDF"); }
    }

    public static TMP_FontAsset Medium
    {
        get { return medium != null ? medium : medium = Load("Inter-Medium_SDF"); }
    }

    public static TMP_FontAsset SemiBold
    {
        get { return semibold != null ? semibold : semibold = Load("Inter-SemiBold_SDF"); }
    }

    static TMP_FontAsset Load(string name)
    {
        var asset = Resources.Load<TMP_FontAsset>("AeroUI/Fonts/" + name);
        if (asset == null)
        {
            Debug.LogWarning("[AeroUI] Missing font asset " + name + "; using TMP default.");
            return TMP_Settings.defaultFontAsset;
        }
        return asset;
    }
}
