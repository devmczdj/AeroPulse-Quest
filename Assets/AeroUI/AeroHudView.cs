using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AeroUI is a small local component layer: tokens + primitives, not a new input stack.
public class AeroHudView : MonoBehaviour
{
    public Button CloseButton { get; private set; }

    public Button MapButton { get; private set; }
    public AeroRouteDemo Route { get; private set; }
    public AeroMapView MapView { get; private set; }
    public Button SkyButton { get; private set; }
    public Button SettingsButton { get; private set; }
    public AeroHudSettingsView SettingsView { get; private set; }
    public bool IsSettingsOpen => SettingsView != null && SettingsView.gameObject.activeSelf;
    public bool IsMapOpen => !IsSettingsOpen && mapMode;
    bool mapMode;
    TextMeshProUGUI settingsLabel;
    RectTransform dataBody;
    TextMeshProUGUI sourceLabel, subline, mapButtonLabel;

    Image closeFill;
    TextMeshProUGUI closeLabel;
    HudReveal reveal;

    public static AeroHudView Build(GameObject card)
    {
        var existing = card.GetComponent<AeroHudView>();
        if (existing != null) return existing;
        var view = card.AddComponent<AeroHudView>();
        view.Create(card);
        return view;
    }

    public void SetCloseHover(bool hover)
    {
        if (closeFill == null) return;
        closeFill.color = hover ? AeroUiTokens.ButtonHover : AeroUiTokens.ButtonFill;
        closeLabel.color = hover ? AeroUiTokens.Mint : AeroUiTokens.Ink;
    }

    public void SetHover(Button left, Button right)
    {
        SetCloseHover(left == CloseButton || right == CloseButton);
        bool hover = left == MapButton || right == MapButton;
        MapButton.GetComponent<Image>().color = hover ? AeroUiTokens.ButtonHover : AeroUiTokens.ButtonFill;
        mapButtonLabel.color = hover ? AeroUiTokens.Mint : AeroUiTokens.Ink;
        MapView.SetHover(left == MapView.AdvanceButton || right == MapView.AdvanceButton);
        bool settingsHover = left == SettingsButton || right == SettingsButton;
        SettingsButton.GetComponent<Image>().color = settingsHover ? AeroUiTokens.ButtonHover : AeroUiTokens.ButtonFill;
        settingsLabel.color = settingsHover ? AeroUiTokens.Mint : AeroUiTokens.Ink;
        SettingsView.SetHover(left,right);
        SkyButton.GetComponent<Image>().color = left == SkyButton || right == SkyButton ? AeroUiTokens.ButtonHover : AeroUiTokens.ButtonFill;
    }

    public void BindControls(PinchHudExperiment hud)
    {
        SettingsView.Bind(hud);
        SkyButton.onClick.AddListener(hud.ShowSkyMap);
    }
    public void ToggleSettings()
    {
        if (IsSettingsOpen) { ShowMap(mapMode); return; }
        dataBody.gameObject.SetActive(false); MapView.gameObject.SetActive(false);
        SettingsView.gameObject.SetActive(true); SettingsView.Refresh();
        sourceLabel.text = "DEMO SETTINGS"; subline.text = "VIEW OPTIONS"; settingsLabel.text = "DONE";
        Physics.SyncTransforms();
    }

    public void ShowMap(bool show)
    {
        mapMode = show;
        SettingsView.gameObject.SetActive(false);
        settingsLabel.text = "SETTINGS";
        dataBody.gameObject.SetActive(!show);
        MapView.gameObject.SetActive(show);
        sourceLabel.text = show ? "SIMULATED MAP" : "SIMULATED DATA";
        subline.text = show ? "ROUTE OVERVIEW" : "RUN OVERVIEW";
        mapButtonLabel.text = show ? "DATA" : "MAP";
        Physics.SyncTransforms();
    }

    public void PlayReveal()
    {
        if (reveal == null) reveal = GetComponent<HudReveal>();
        if (reveal != null) reveal.Play();
    }

    void Create(GameObject card)
    {
        var rect = (RectTransform)card.transform;
        rect.sizeDelta = new Vector2(AeroUiTokens.PanelWidth, AeroUiTokens.PanelHeight);
        var group = card.GetComponent<CanvasGroup>();
        if (group == null) group = card.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        reveal = card.AddComponent<HudReveal>();

        AddPanelImage(card.transform, "Shadow", AeroUiSprites.Shadow, new Vector2(0f, -8f),
                      new Vector2(840f, 510f), new Color(1f, 1f, 1f, 0.78f));
        var glass = AddPanelImage(card.transform, "GlassBase", LoadGlassSprite(), Vector2.zero,
                                  new Vector2(AeroUiTokens.PanelWidth, AeroUiTokens.PanelHeight), new Color(1f, 1f, 1f, 0.85f));
        glass.type = Image.Type.Sliced;
        glass.pixelsPerUnitMultiplier = 4f;
        glass.material = Resources.Load<Material>("AeroGlassBackground");
        AddPanelImage(card.transform, "Tint", AeroUiSprites.Tint, Vector2.zero,
                      new Vector2(AeroUiTokens.PanelWidth, AeroUiTokens.PanelHeight), new Color(1f, 1f, 1f, 0.5f));
        AddPanelImage(card.transform, "Sheen", AeroUiSprites.Sheen, Vector2.zero,
                      new Vector2(AeroUiTokens.PanelWidth, AeroUiTokens.PanelHeight), Color.white);
        AddPanelImage(card.transform, "Stroke", AeroUiSprites.Stroke, Vector2.zero,
                      new Vector2(AeroUiTokens.PanelWidth, AeroUiTokens.PanelHeight), Color.white);
        var content = NewRect("Content", card.transform, Vector2.zero,
                              new Vector2(AeroUiTokens.PanelWidth, AeroUiTokens.PanelHeight));
        content.SetAsLastSibling();

        AddPanelImage(content, "Logo", AeroUiSprites.Logo, new Vector2(-344f, 168f),
                      new Vector2(24f, 24f), Color.white);
        Text(content, "Wordmark", "AEROPULSE", new Vector2(-310f, 169f), new Vector2(300f, 32f),
             22f, AeroUiFonts.SemiBold, AeroUiTokens.Ink, TextAlignmentOptions.Left, 12f, 0f);
        subline = Text(content, "Subline", "RUN OVERVIEW", new Vector2(-310f, 145f), new Vector2(300f, 22f),
             16f, AeroUiFonts.Medium, AeroUiTokens.Secondary,
             TextAlignmentOptions.Left, 4f, 0f);

        AddPanelImage(content, "ChipBorder", AeroUiSprites.PillStroke, new Vector2(244f, 169f),
                      new Vector2(222f, 44f), AeroUiTokens.ChipStroke);
        AddPill(content, "ChipFill", new Vector2(244f, 169f), new Vector2(218f, 40f),
                AeroUiTokens.ChipFill);
        AddPanelImage(content, "ChipDot", AeroUiSprites.Dot, new Vector2(150f, 169f),
                      new Vector2(7f, 7f), AeroUiTokens.Mint);
        sourceLabel = Text(card.transform, "DataSource", "SIMULATED DATA", new Vector2(258f, 169f),
             new Vector2(190f, 30f), 16f, AeroUiFonts.Medium,
             new Color(0.75f, 0.92f, 0.87f, 0.96f), TextAlignmentOptions.Center, 2f, 0f, true);

        SkyButton = ActionButton(card.transform,"OpenSkyMap","SKY MAP",new Vector2(18,155),new Vector2(156,44),new Vector3(160,52,8));

        Hairline(content, "HeaderDivider", new Vector2(0f, 122f), 688f);

        dataBody = NewRect("DataContent", content, Vector2.zero,
                           new Vector2(AeroUiTokens.PanelWidth, AeroUiTokens.PanelHeight));
        Text(dataBody, "HeartLabel", "HEART RATE", new Vector2(-344f, 88f), new Vector2(260f, 20f),
             18f, AeroUiFonts.SemiBold, AeroUiTokens.Secondary, TextAlignmentOptions.Left, 13f, 0f);
        Text(dataBody, "HeartRate", "142", new Vector2(-344f, 15f), new Vector2(300f, 130f),
             95f, AeroUiFonts.SemiBold, AeroUiTokens.Ink, TextAlignmentOptions.Left, -2f, 0f);
        Text(dataBody, "HeartUnit", "BPM", new Vector2(-160f, -7f), new Vector2(80f, 30f),
             18f, AeroUiFonts.Medium, new Color(0.76f, 0.82f, 0.82f, 0.94f),
             TextAlignmentOptions.Left, 10f, 0f);

        Text(dataBody, "EffortLabel", "EFFORT", new Vector2(-344f, -62f), new Vector2(80f, 20f),
             16f, AeroUiFonts.Medium, AeroUiTokens.Tertiary, TextAlignmentOptions.Left, 12f, 0f);
        for (int i = 0; i < 5; i++)
        {
            AddPanelImage(dataBody, "Effort" + i, AeroUiSprites.Pill,
                          new Vector2(-242f + i * 20f, -62f), new Vector2(14f, 6f),
                          i < 3 ? AeroUiTokens.Mint : new Color(1f, 1f, 1f, 0.14f));
        }
        Text(dataBody, "EffortValue", "STEADY", new Vector2(-125f, -62f), new Vector2(100f, 20f),
             16f, AeroUiFonts.Medium, new Color(0.84f, 0.89f, 0.89f, 0.96f),
             TextAlignmentOptions.Left, 6f, 0f);

        Hairline(dataBody, "ColumnDivider", new Vector2(44f, 3f), 2.5f, 194f);
        Text(dataBody, "PaceLabel", "PACE", new Vector2(96f, 76f), new Vector2(140f, 20f),
             18f, AeroUiFonts.SemiBold, AeroUiTokens.Secondary, TextAlignmentOptions.Left, 11f, 0f);
        Text(dataBody, "Pace", "5:20", new Vector2(96f, 20f), new Vector2(140f, 60f),
             42f, AeroUiFonts.SemiBold, AeroUiTokens.Ink, TextAlignmentOptions.Left, -2f, 0f);
        Text(dataBody, "PaceUnit", "/KM", new Vector2(208f, 11f), new Vector2(60f, 24f),
             16f, AeroUiFonts.Medium, AeroUiTokens.Secondary, TextAlignmentOptions.Left, 10f, 0f);
        Hairline(dataBody, "RowDivider", new Vector2(220f, -16f), 248f);
        Text(dataBody, "DistanceLabel", "DISTANCE", new Vector2(96f, -44f), new Vector2(180f, 20f),
             18f, AeroUiFonts.SemiBold, AeroUiTokens.Secondary, TextAlignmentOptions.Left, 11f, 0f);
        Text(dataBody, "Distance", "3.20", new Vector2(96f, -100f), new Vector2(150f, 60f),
             42f, AeroUiFonts.SemiBold, AeroUiTokens.Ink, TextAlignmentOptions.Left, -2f, 0f);
        Text(dataBody, "DistanceUnit", "KM", new Vector2(208f, -109f), new Vector2(60f, 24f),
             16f, AeroUiFonts.Medium, AeroUiTokens.Secondary, TextAlignmentOptions.Left, 10f, 0f);

        Hairline(content, "FooterDivider", new Vector2(0f, -134f), 688f);
        AddPanelImage(card.transform, "CloseBorder", AeroUiSprites.PillStroke, new Vector2(266f, -172f),
                      new Vector2(180f, 68f), new Color(1f, 1f, 1f, 0.16f));

        var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BoxCollider));
        close.transform.SetParent(card.transform, false);
        var closeRect = (RectTransform)close.transform;
        closeRect.sizeDelta = new Vector2(176f, 64f);
        closeRect.anchoredPosition = new Vector2(266f, -172f);
        closeFill = close.GetComponent<Image>();
        closeFill.sprite = AeroUiSprites.Pill;
        closeFill.type = Image.Type.Simple;
        closeFill.color = AeroUiTokens.ButtonFill;
        closeFill.raycastTarget = true;
        var collider = close.GetComponent<BoxCollider>();
        collider.size = new Vector3(220f, 90f, 8f);
        CloseButton = close.GetComponent<Button>();
        CloseButton.targetGraphic = closeFill;
        CloseButton.transition = Selectable.Transition.None;

        closeLabel = Text(close.transform, "CloseLabel", "HIDE", Vector2.zero,
                          new Vector2(152f, 48f), 24f, AeroUiFonts.SemiBold, AeroUiTokens.Ink,
                          TextAlignmentOptions.Center, 2f, 0f, true);
        MapButton = ActionButton(card.transform, "ToggleMap", "MAP", new Vector2(66, -172),
                                 new Vector2(156, 64), new Vector3(164, 82, 8));
        mapButtonLabel = MapButton.GetComponentInChildren<TextMeshProUGUI>();
        Route = new AeroRouteDemo();
        MapView = AeroMapView.Build(content, Route);
        SettingsView = AeroHudSettingsView.Build(content);
        SettingsButton = ActionButton(card.transform,"ToggleSettings","SETTINGS",new Vector2(-232,-172),
                                      new Vector2(224,64),new Vector3(224,82,8));
        settingsLabel = SettingsButton.GetComponentInChildren<TextMeshProUGUI>();
        SettingsButton.onClick.AddListener(ToggleSettings);
        MapButton.onClick.AddListener(() => ShowMap(!mapMode));
        ShowMap(false);
    }

    internal static Button ActionButton(Transform parent, string name, string label, Vector2 position,
                                         Vector2 size, Vector3 hitSize)
    {
        var image = AddPanelImage(parent, name, AeroUiSprites.Pill, position, size, AeroUiTokens.ButtonFill);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        image.gameObject.AddComponent<BoxCollider>().size = hitSize;
        Text(image.transform, name + "Label", label, Vector2.zero, size - new Vector2(12, 8), 22,
             AeroUiFonts.SemiBold, AeroUiTokens.Ink, TextAlignmentOptions.Center, 2, 0, true);
        return button;
    }

    static Sprite LoadGlassSprite()
    {
        var sprite = Resources.Load<Sprite>("JetStyle/RoundedRect");
        if (sprite == null) Debug.LogWarning("[AeroUI] JetStyle/RoundedRect missing");
        return sprite;
    }

    internal static RectTransform NewRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    internal static Image AddPanelImage(Transform parent, string name, Sprite sprite, Vector2 position,
                               Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        if (sprite == null) image.enabled = false;
        return image;
    }

    static Image AddPill(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var image = AddPanelImage(parent, name, AeroUiSprites.Pill, position, size, color);
        image.type = Image.Type.Simple;
        return image;
    }

    static void Hairline(Transform parent, string name, Vector2 position, float width, float height = 2.5f)
    {
        var image = AddPanelImage(parent, name, AeroUiSprites.White, position, new Vector2(width, height), AeroUiTokens.Hairline);
        image.color = AeroUiTokens.Hairline;
    }

    internal static TextMeshProUGUI Text(Transform parent, string name, string content, Vector2 position,
                                Vector2 size, float fontSize, TMP_FontAsset font, Color color,
                                TextAlignmentOptions alignment, float tracking, float verticalOffset,
                                bool centered = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
        rect.anchoredPosition = position + new Vector2(0f, verticalOffset);
        rect.sizeDelta = new Vector2(size.x, Mathf.Max(size.y, fontSize * 1.6f));
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.characterSpacing = tracking;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
        text.extraPadding = true;
        text.UpdateMeshPadding();
        return text;
    }
}
