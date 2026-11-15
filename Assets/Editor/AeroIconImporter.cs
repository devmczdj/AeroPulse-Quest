using UnityEditor;
using UnityEngine;
public class AeroIconImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/AeroIcons/"))return;
        var i=(TextureImporter)assetImporter;i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;
        i.alphaIsTransparency=true;i.mipmapEnabled=true;i.filterMode=FilterMode.Bilinear;i.maxTextureSize=256;
        i.textureCompression=TextureImporterCompression.Uncompressed;i.spritePixelsPerUnit=100;
    }
}
