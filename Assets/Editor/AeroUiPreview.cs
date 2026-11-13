using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AeroUiPreview
{
    const int Width = 1600;
    const int Height = 1000;

    public static void Check()
    {
        var camera = MakeCamera();
        var card = MakeCard(camera, true);
        try
        {
            Canvas.ForceUpdateCanvases();
            Require(card.transform.Find("DataSource") != null, "DataSource must remain a direct child");
            Require(GameObject.Find("HeartRate") != null, "HeartRate missing");
            Require(GameObject.Find("Pace") != null, "Pace missing");
            Require(GameObject.Find("Distance") != null, "Distance missing");
            Require(GameObject.Find("Close") != null, "Close missing");
            Require(AeroUiSprites.Shadow != null && AeroUiSprites.Sheen != null &&
                    AeroUiSprites.Stroke != null && AeroUiSprites.Pulse != null &&
                    AeroUiSprites.Logo != null && AeroUiSprites.Dot != null &&
                    AeroUiSprites.Glow != null && AeroUiSprites.Pill != null,
                    "AeroUI texture sprite missing");
            Require(Resources.Load<Material>("AeroGlassBackground") != null, "Glass material missing");
            foreach (var image in card.GetComponentsInChildren<Image>(true))
            {
                var sprite = image.sprite;
                Debug.Log("[AeroUI] IMAGE " + image.name + " sprite=" +
                          (sprite != null ? sprite.name : "null") + " tex=" +
                          (sprite != null && sprite.texture != null ? sprite.texture.name : "null") +
                          " size=" + ((RectTransform)image.transform).sizeDelta +
                          " pos=" + ((RectTransform)image.transform).anchoredPosition +
                          " color=" + image.color + " enabled=" + image.enabled);
            }
            foreach (var text in card.GetComponentsInChildren<TextMeshProUGUI>())
            {
                text.ForceMeshUpdate();
                Require(!text.isTextOverflowing, "HUD text overflows: " + text.name);
                foreach (char c in text.GetParsedText())
                    Require(char.IsWhiteSpace(c) || text.font.HasCharacter(c, true),
                            "Missing font glyph '" + c + "' in " + text.name);
            }
            var close = card.transform.Find("Close/CloseLabel").GetComponent<TextMeshProUGUI>();
            Require(close.fontSize >= 24f && close.extraPadding, "HIDE clarity settings missing");
            foreach (var text in card.GetComponentsInChildren<TextMeshProUGUI>())
                Require(text.fontSize >= 16f && text.extraPadding, "Small/unpadded text: " + text.name);
            foreach (var path in Directory.GetFiles("Assets/Resources/AeroUI/Textures", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Require(importer.mipmapEnabled && importer.filterMode == FilterMode.Trilinear,
                        "UI texture minification not filtered: " + path);
            }
            var mobile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(
                "Assets/Settings/Mobile_RPAsset.asset");
            Require(mobile.msaaSampleCount == 4 && Mathf.Approximately(mobile.renderScale, 1f),
                    "Mobile rendering clarity settings missing");
            Debug.Log("[AeroUI] PASS: names, fonts, text fit, HIDE >=24, labels >=16, padding, mipmaps, 4x MSAA and 1.0 render scale.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(card);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    public static void Capture(bool map = false, bool settings = false)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("Main Camera missing");
        camera.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
        camera.fieldOfView = 60f;
        camera.clearFlags = CameraClearFlags.Skybox;

        // The live app hides the entry orb while the card is open; mirror that in the still.
        foreach (var hotspot in UnityEngine.Object.FindObjectsByType<GazeHotspot>(FindObjectsSortMode.None))
            hotspot.gameObject.SetActive(false);
        foreach (var label in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (label.name == "HudEntryLabel") label.SetActive(false);

        var card = MakeCard(camera, true);
        card.GetComponent<AeroHudView>().ShowMap(map);
        if (settings) card.GetComponent<AeroHudView>().ToggleSettings();
        card.transform.position = camera.transform.position + camera.transform.forward * 1.8f;
        card.transform.rotation = camera.transform.rotation;
        Canvas.ForceUpdateCanvases();

        var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            string path = Path.Combine(Path.GetTempPath(), "aeropulse_v091_" + (settings ? "settings" : map ? "map" : "data") + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Debug.Log("[AeroUI] Preview written to " + path);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(card);
        }
    }

    static Camera MakeCamera()
    {
        var go = new GameObject("AeroUiCheckCamera", typeof(Camera));
        var camera = go.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.21f, 0.22f, 1f);
        camera.fieldOfView = 60f;
        return camera;
    }

    static GameObject MakeCard(Camera camera, bool activate = false)
    {
        var card = new GameObject("PinchHudCard", typeof(RectTransform), typeof(Canvas),
                                  typeof(GraphicRaycaster), typeof(BoxCollider));
        var canvas = card.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        var rect = (RectTransform)card.transform;
        rect.sizeDelta = new Vector2(AeroUiTokens.PanelWidth, AeroUiTokens.PanelHeight);
        rect.localScale = Vector3.one * 0.0015f;
        card.GetComponent<BoxCollider>().size =
            new Vector3(AeroUiTokens.PanelWidth, AeroUiTokens.PanelHeight, 3f);
        AeroHudView.Build(card);
        var group = card.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1f;
        card.SetActive(activate);
        return card;
    }

    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
