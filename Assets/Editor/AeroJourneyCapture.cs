using System.IO;
using UnityEngine;
public static class AeroJourneyCapture
{
    // Capture actual runtime canvases; injected poses are documented by the caller.
    public static void Save(string path)
    {
        var camera=Camera.main;Canvas.ForceUpdateCanvases();
        var target=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);
        var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        var prior=RenderTexture.active;var priorTarget=camera.targetTexture;
        try {camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally {camera.targetTexture=priorTarget;RenderTexture.active=prior;
            Object.DestroyImmediate(texture);target.Release();Object.DestroyImmediate(target);}
    }
}
