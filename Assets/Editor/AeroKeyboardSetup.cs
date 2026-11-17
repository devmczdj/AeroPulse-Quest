using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

public static class AeroKeyboardSetup
{
    const string Source="Assets/Fonts/Source/NotoSansCJKsc-Regular.otf";
    const string Asset="Assets/Resources/AeroUI/Fonts/AeroPulseCJKFallback_SDF.asset";
    public static void Configure()
    {
        var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        var quest=settings.GetFeature<MetaQuestFeature>();
        if(quest==null||!quest.enabled)throw new InvalidOperationException("Meta Quest OpenXR feature must be enabled");
        quest.enableSystemKeyboard=true;EditorUtility.SetDirty(quest);
        ConfigureFontFallback(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/AeroUI/Fonts/AeroPulseSC_SDF.asset"));
        AssetDatabase.SaveAssets();
    }
    public static void ConfigureFontFallback(TMP_FontAsset primary)
    {
        if(primary==null)throw new InvalidOperationException("Primary interface font missing");
        var source=AssetDatabase.LoadAssetAtPath<Font>(Source);
        if(source==null)throw new InvalidOperationException("Full OFL Noto CJK font missing");
        var importer=AssetImporter.GetAtPath(Source) as TrueTypeFontImporter;
        if(importer!=null&&!importer.includeFontData){importer.includeFontData=true;importer.SaveAndReimport();}
        var fallback=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Asset);
        if(fallback==null)
        {
            fallback=TMP_FontAsset.CreateFontAsset(source,48,5,GlyphRenderMode.SDF16,1024,1024,AtlasPopulationMode.Dynamic,false);
            fallback.name="AeroPulseCJKFallback_SDF";fallback.isMultiAtlasTexturesEnabled=true;
            AssetDatabase.CreateAsset(fallback,Asset);
            fallback.material.name="AeroPulseCJKFallback Material";AssetDatabase.AddObjectToAsset(fallback.material,fallback);
            foreach(var atlas in fallback.atlasTextures){atlas.name="AeroPulseCJKFallback Atlas";atlas.filterMode=FilterMode.Bilinear;AssetDatabase.AddObjectToAsset(atlas,fallback);}
        }
        primary.fallbackFontAssetTable=new List<TMP_FontAsset>{fallback};
        EditorUtility.SetDirty(primary);EditorUtility.SetDirty(fallback);
        Debug.Log("[AeroPulse Keyboard] OpenXR system keyboard and dynamic CJK fallback configured");
    }
}
