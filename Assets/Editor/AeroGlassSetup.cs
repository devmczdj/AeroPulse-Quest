using System;
using UnityEditor;
using UnityEngine;

public static class AeroGlassSetup
{
    public static void Configure()
    {
        const string cubePath = "Assets/Resources/AeroGlassPanorama.asset";
        var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(cubePath);
        if (cube == null)
        {
            // Bake only the static panorama; no camera readback or runtime blur pass is needed.
            var source = RenderSettings.skybox.GetTexture("_MainTex");
            if (source == null) throw new InvalidOperationException("Panorama texture missing");
            var target = RenderTexture.GetTemporary(128, 64, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var small = new Texture2D(128, 64, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                small.ReadPixels(new Rect(0, 0, 128, 64), 0, 0);
                small.Apply();
                small.wrapMode = TextureWrapMode.Clamp;
                cube = new Cubemap(128, TextureFormat.RGBA32, true);
                float rotation = RenderSettings.skybox.GetFloat("_Rotation");
                for (int face = 0; face < 6; face++)
                {
                    var pixels = new Color[128 * 128];
                    for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 128; x++)
                    {
                        float u = (x + 0.5f) / 64f - 1f, v = (y + 0.5f) / 64f - 1f;
                        Vector3 d = face == 0 ? new Vector3(1, v, -u) : face == 1 ? new Vector3(-1, v, u) :
                            face == 2 ? new Vector3(u, 1, -v) : face == 3 ? new Vector3(u, -1, v) :
                            face == 4 ? new Vector3(u, v, 1) : new Vector3(-u, v, -1);
                        d = Quaternion.AngleAxis(rotation, Vector3.up) * d.normalized;
                        float longitude = Mathf.Repeat(0.5f - Mathf.Atan2(d.z, d.x) / (2f * Mathf.PI), 1f);
                        float latitude = 1f - Mathf.Acos(Mathf.Clamp(d.y, -1f, 1f)) / Mathf.PI;
                        pixels[y * 128 + x] = small.GetPixelBilinear(longitude, latitude);
                    }
                    cube.SetPixels(pixels, (CubemapFace)face);
                }
                cube.Apply(true, false);
                cube.filterMode = FilterMode.Trilinear;
                AssetDatabase.CreateAsset(cube, cubePath);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(small);
            }
        }
        var shader = Shader.Find("AeroPulse/JetStyleGlass");
        if (shader == null || !shader.isSupported) throw new InvalidOperationException("Glass shader unsupported");
        const string path = "Assets/Resources/AeroGlassBackground.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.SetTexture("_Background", cube);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
    }
}
