using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

public static class AeroUiSetup
{
    const string Root = "Assets/Resources/AeroUI";

    public static void Configure()
    {
        ConfigureRendering();
        ConfigureFont("Inter-Regular");
        ConfigureFont("Inter-Medium");
        ConfigureFont("Inter-SemiBold");
        EnsureFontAsset("Inter-Regular", "Inter-Regular_SDF");
        EnsureFontAsset("Inter-Medium", "Inter-Medium_SDF");
        EnsureFontAsset("Inter-SemiBold", "Inter-SemiBold_SDF");
        foreach (var path in Directory.GetFiles(Root + "/Textures", "*.png"))
            ConfigureTexture(path.Replace("\\", "/"));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AeroUI] Setup complete: Inter font assets and uncompressed UI textures.");
    }

    static void ConfigureRendering()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        if (pipeline == null) throw new InvalidOperationException("Mobile render pipeline missing");
        pipeline.msaaSampleCount = 4;
        pipeline.renderScale = 1f;
        EditorUtility.SetDirty(pipeline);
        QualitySettings.SetQualityLevel(0, true);
    }

    static void ConfigureFont(string name)
    {
        string path = Root + "/Fonts/" + name + ".ttf";
        var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
        if (importer == null) throw new InvalidOperationException("Missing font " + path);
        importer.fontTextureCase = FontTextureCase.Dynamic;
        importer.includeFontData = true;
        importer.SaveAndReimport();
    }

    static void EnsureFontAsset(string ttfName, string assetName)
    {
        string assetPath = Root + "/Fonts/" + assetName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return;
        var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/" + ttfName + ".ttf");
        if (font == null) throw new InvalidOperationException("Font import failed: " + ttfName);
        var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA,
                                                  1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (asset == null) throw new InvalidOperationException("TMP font asset failed: " + ttfName);
        asset.name = assetName;
        string characters = "";
        for (char c = ' '; c <= '~'; c++) characters += c;
        characters += "\u2304\u00b7\u00d7";
        bool added = asset.TryAddCharacters(characters, out string missing);
        Debug.Log("[AeroUI] " + ttfName + " glyphs=" + asset.glyphTable.Count +
                  " chars=" + asset.characterTable.Count + " added=" + added);
        if (!added)
            Debug.LogWarning("[AeroUI] Missing glyphs in " + ttfName + ": " + missing);
        AssetDatabase.CreateAsset(asset, assetPath);
        var atlas = asset.atlasTextures[0];
        atlas.name = assetName + " Atlas";
        AssetDatabase.AddObjectToAsset(atlas, asset);
        var material = asset.material;
        material.name = assetName + " Material";
        AssetDatabase.AddObjectToAsset(material, asset);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[AeroUI] Created " + assetPath);
    }

    static void ConfigureTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture " + path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.sRGBTexture = true;
        importer.SaveAndReimport();
    }
}

public class AeroUiTexturePostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/AeroUI/Textures/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.sRGBTexture = true;
    }
}
