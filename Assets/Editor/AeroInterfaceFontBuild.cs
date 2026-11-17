using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class AeroInterfaceFontBuild
{
    public static void Build()
    {
        const string path="Assets/Resources/AeroUI/Fonts/AeroPulseSC_SDF.asset";
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Source/AeroPulseSC-Regular.otf");
        if(source==null)throw new InvalidOperationException("Interface source font not imported");
        var font=TMP_FontAsset.CreateFontAsset(source,48,5,GlyphRenderMode.SDF16,2048,2048,AtlasPopulationMode.Dynamic,false);
        string chars=File.ReadAllText("Assets/Fonts/InterfaceCharacters.txt");
        if(!font.TryAddCharacters(chars,out string missing))throw new InvalidOperationException("Interface font missing: "+missing);
        font.name="AeroPulseSC_SDF";font.atlasPopulationMode=AtlasPopulationMode.Static;
        if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)!=null)AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(font,path);
        font.material.name="AeroPulseSC_SDF Material";AssetDatabase.AddObjectToAsset(font.material,font);
        foreach(var atlas in font.atlasTextures){atlas.name="AeroPulseSC_SDF Atlas";atlas.filterMode=FilterMode.Bilinear;AssetDatabase.AddObjectToAsset(atlas,font);}
        AeroKeyboardSetup.ConfigureFontFallback(font);
        EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
        Debug.Log("[AeroPulse Font] BAKED "+font.characterTable.Count+" static glyphs; atlas="+font.atlasTexture.width+"x"+font.atlasTexture.height);
    }
}
