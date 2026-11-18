using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// One instance per metric, created before direct-touch input caches its buttons.
public sealed class AeroWidgetCatalog : MonoBehaviour
{
    public sealed class Item
    {
        public string Id;
        public string Title;
        public Transform Root;
        public UnityEngine.UI.Button EntryButton;
        internal TextMeshProUGUI Status;
    }

    public IReadOnlyList<Item> Items => items;
    public Transform CatalogRoot { get; private set; }
    public UnityEngine.UI.Button DoneButton { get; private set; }
    public UnityEngine.UI.Button ComponentsTab { get; private set; }
    public UnityEngine.UI.Button ProfilesTab { get; private set; }
    public RectTransform ComponentsPage { get; private set; }
    public RectTransform ProfilesPage { get; private set; }
    public bool ShowingProfiles { get; private set; }
    public bool KeepOpen { get; private set; }
    public AeroEditLayer EditingLayer { get; private set; }
    public bool IsPreviewing { get; private set; }
    public UnityEngine.UI.Button[] LayerButtons { get; private set; }
    public UnityEngine.UI.Button PreviewButton { get; private set; }
    public UnityEngine.UI.Button KeepOpenButton { get; private set; }
    public Transform LayerControls { get; private set; }
    public Action<AeroEditLayer> LayerRequested;
    public Action PreviewRequested;
    public Action PageChanged;
    bool modal,handAvailable;
    TextMeshProUGUI catalogHint;
    public bool IsOpen => CatalogRoot != null && CatalogRoot.gameObject.activeInHierarchy && catalogCanvas!=null && catalogCanvas.enabled;
    Canvas catalogCanvas;
    public Action<string> Requested;
    public Action Finished;

    readonly List<Item> items = new List<Item>();
    Camera viewCamera;
    AeroRouteDemo route;
    TextMeshProUGUI nextValue, timerValue;
    float sessionStart;
    int lastElapsedSecond = -1;

    public static AeroWidgetCatalog Build(Camera camera, AeroRouteDemo route)
    {
        var catalog = new GameObject("WidgetCatalogController").AddComponent<AeroWidgetCatalog>();
        catalog.viewCamera = camera;
        catalog.route = route;
        catalog.sessionStart = Time.unscaledTime;
        catalog.Create();
        if (route != null) route.Changed += catalog.Refresh;
        catalog.Refresh();
        catalog.UpdateTimer();
        catalog.SetOpen(false);
        return catalog;
    }

    void Create()
    {
        CatalogRoot = CreateCanvas("WidgetCatalog", new Vector2(400, 560), .00075f);
        catalogCanvas=CatalogRoot.GetComponent<Canvas>();catalogCanvas.sortingOrder=100;
        CatalogRoot.gameObject.AddComponent<BoxCollider>().size=new Vector3(400,560,3);
        Backing(CatalogRoot, new Vector2(400, 560),true);
        CreateLayerControls();
        ComponentsTab = Tab("ComponentsTab", "组件", -90, false);
        ProfilesTab = Tab("ProfilesTab", "档案", 90, true);
        ComponentsPage = AeroHudView.NewRect("ComponentsPage", CatalogRoot, Vector2.zero, new Vector2(400, 410));
        ProfilesPage = AeroHudView.NewRect("ProfilesPage", CatalogRoot, Vector2.zero, new Vector2(400, 410));
        ProfilesPage.gameObject.SetActive(false);
        catalogHint=Label(ComponentsPage, "CatalogHint", "点按添加，捏合拖出", new Vector2(-172, 200), new Vector2(344, 34), 21, AeroUiTokens.Secondary);

        AddItem("heart", "心率", "heart-pulse", "142", "BPM · 模拟数据");
        AddItem("pace", "配速", "fast-forward", "5:20", "/km · 模拟数据");
        AddItem("distance", "距离", "map", "3.20", "km · 模拟数据");
        nextValue = AddItem("next", "下一站", "arrow-right", "", "示意路线");
        nextValue.fontSize = 30;
        timerValue = AddItem("timer", "会话计时", "rotate-ccw", "00:00", "应用运行时长");

        DoneButton = AeroHudView.ActionButton(CatalogRoot, "CatalogDone", "完成编辑",
            new Vector2(0, -238), new Vector2(352, 52), new Vector3(352, 52, 8));
        var doneLabel = DoneButton.GetComponentInChildren<TextMeshProUGUI>(true);
        doneLabel.font = AeroUiFonts.Interface;
        doneLabel.characterSpacing = 0;
        DoneButton.onClick.AddListener(() => Finished?.Invoke());
        RefreshTabs(null, null);
    }

    void CreateLayerControls()
    {
        LayerControls = AeroHudView.NewRect("EditorToolsHeader", CatalogRoot, new Vector2(0, 350), new Vector2(400, 124));
        Backing(LayerControls, new Vector2(400, 124),true);
        LayerControls.gameObject.AddComponent<BoxCollider>().size=new Vector3(400,124,3);
        LayerButtons = new UnityEngine.UI.Button[3];
        string[] names = { "环境", "手部", "面前" }, icons = { "cube", "hand-open", "vr-headset" };
        for (int i = 0; i < LayerButtons.Length; i++)
        {
            var layer = (AeroEditLayer)i;
            LayerButtons[i] = ToolButton("EditLayer" + layer, names[i], new Vector2((i - 1) * 120, 28), new Vector2(112, 44),
                () => { if (!modal) LayerRequested?.Invoke(layer); });
            AeroUiIcons.Label(LayerButtons[i], icons[i], names[i], 26);
        }
        PreviewButton = ToolButton("PreviewAllLayers", "全部预览", new Vector2(-90, -28), new Vector2(172, 44),
            () => { if (!modal) PreviewRequested?.Invoke(); });
        KeepOpenButton = ToolButton("KeepToolsOpen", "保持打开", new Vector2(90, -28), new Vector2(172, 44),
            () => { if (!modal) SetKeepOpen(!KeepOpen); });
        AeroUiIcons.Label(KeepOpenButton, "pushpin-off", "保持打开", 26);
    }

    UnityEngine.UI.Button ToolButton(string name, string title, Vector2 position, Vector2 size, Action action)
    {
        var button = AeroHudView.ActionButton(LayerControls, name, title, position, size, new Vector3(size.x, size.y + 4, 8));
        var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        text.font = AeroUiFonts.Interface; text.characterSpacing = 0; text.fontSize = 22;
        button.onClick.AddListener(() => action());
        return button;
    }

    public void SetEditingView(AeroEditLayer layer, bool preview)
    {
        EditingLayer = layer; IsPreviewing = preview;
        RefreshHint();RefreshTabs(null, null);
    }

    public void SetHandAvailable(bool value)
    {if(handAvailable==value)return;handAvailable=value;RefreshHint();}
    void RefreshHint()
    {if(catalogHint!=null)catalogHint.text=!IsPreviewing&&EditingLayer==AeroEditLayer.Hand&&!handAvailable?"手部未追踪，请抬起左手":"点按添加，捏合拖出";}

    public void SetKeepOpen(bool value)
    {
        KeepOpen = value;
        AeroUiIcons.Set(KeepOpenButton, value ? "pushpin" : "pushpin-off");
        RefreshTabs(null, null);
    }

    UnityEngine.UI.Button Tab(string name, string title, float x, bool profiles)
    {
        var button = AeroHudView.ActionButton(CatalogRoot, name, title, new Vector2(x, 242), new Vector2(172, 48), new Vector3(172, 48, 8));
        var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        text.font = AeroUiFonts.Interface; text.characterSpacing = 0;
        button.onClick.AddListener(() => SetPage(profiles));
        return button;
    }

    public void SetPage(bool profiles)
    {
        if (modal) return;
        ShowingProfiles = profiles;
        ComponentsPage.gameObject.SetActive(!profiles);
        ProfilesPage.gameObject.SetActive(profiles);
        RefreshTabs(null, null);
        PageChanged?.Invoke();
        Physics.SyncTransforms();
    }

    public void SetModal(bool value)
    {
        modal = value;
        ComponentsTab.interactable = ProfilesTab.interactable = DoneButton.interactable = !value;
        ComponentsTab.gameObject.SetActive(!value);ProfilesTab.gameObject.SetActive(!value);
        DoneButton.gameObject.SetActive(!value);
        LayerControls.gameObject.SetActive(!value);
        foreach (var button in LayerButtons) button.interactable = !value;
        PreviewButton.interactable = KeepOpenButton.interactable = !value;
        RefreshTabs(null, null);
    }

    public void RefreshTabs(UnityEngine.UI.Button left, UnityEngine.UI.Button right)
    {
        PaintButton(ComponentsTab, !ShowingProfiles, left, right);
        PaintButton(ProfilesTab, ShowingProfiles, left, right);
        PaintButton(DoneButton, false, left, right);
        if (LayerButtons == null) return;
        for (int i = 0; i < LayerButtons.Length; i++)
            PaintButton(LayerButtons[i], !IsPreviewing && (int)EditingLayer == i, left, right);
        PaintButton(PreviewButton, IsPreviewing, left, right);
        PaintButton(KeepOpenButton, KeepOpen, left, right);
    }

    static void PaintButton(UnityEngine.UI.Button button, bool selected, UnityEngine.UI.Button left, UnityEngine.UI.Button right)
    {
        if (button == null) return;
        button.GetComponent<UnityEngine.UI.Image>().color = !button.interactable ? new Color(.06f, .08f, .08f, .6f)
            : button == left || button == right ? AeroUiTokens.ButtonHover
            : selected ? new Color(.2f, .31f, .28f, 1) : AeroUiTokens.ButtonFill;
        button.GetComponentInChildren<TextMeshProUGUI>(true).color = button.interactable ? AeroUiTokens.Ink : AeroUiTokens.Tertiary;
    }

    TextMeshProUGUI AddItem(string id, string title, string iconName, string value, string disclosure)
    {
        var root = CreateCanvas("Widget_" + id, new Vector2(280, 140), .00075f);
        if (viewCamera != null)
        {
            root.position = viewCamera.transform.TransformPoint(new Vector3(0, 0, .8f));
            root.rotation = viewCamera.transform.rotation;
        }
        Backing(root, new Vector2(280, 140));
        Label(root, "Title", title, new Vector2(-120, 44), new Vector2(240, 34), 22, AeroUiTokens.Secondary);
        var valueLabel = AeroHudView.Text(root, "Value", value, new Vector2(-120, 0), new Vector2(240, 58),
            40, AeroUiFonts.SemiBold, AeroUiTokens.Ink, TextAlignmentOptions.Left, 0, 0);
        Label(root, "Disclosure", disclosure, new Vector2(-120, -46), new Vector2(240, 30), 18, AeroUiTokens.Secondary);

        var entry = AeroHudView.ActionButton(ComponentsPage, "CatalogEntry_" + id, title,
            new Vector2(0, 152 - items.Count * 64), new Vector2(352, 56), new Vector3(352, 56, 8));
        var titleLabel = entry.GetComponentInChildren<TextMeshProUGUI>(true);
        titleLabel.font = AeroUiFonts.Interface;
        titleLabel.fontSize = 24;
        titleLabel.characterSpacing = 0;
        titleLabel.alignment = TextAlignmentOptions.Left;
        titleLabel.rectTransform.pivot = new Vector2(0, .5f);
        titleLabel.rectTransform.anchoredPosition = new Vector2(-114, 0);
        titleLabel.rectTransform.sizeDelta = new Vector2(174, 44);
        var icon = AeroHudView.AddPanelImage(entry.transform, "MetricIcon", AeroUiIcons.Get(iconName),
            new Vector2(-146, 0), new Vector2(30, 30), AeroUiTokens.Ink);
        icon.preserveAspect = true;
        var status = AeroHudView.Text(entry.transform, "EntryStatus", "添加", new Vector2(120, 0), new Vector2(72, 38),
            21, AeroUiFonts.Interface, AeroUiTokens.Mint, TextAlignmentOptions.Center, 0, 0, true);
        entry.gameObject.AddComponent<AeroDragHandle>().CatalogId = id;
        entry.onClick.AddListener(() => Requested?.Invoke(id));

        items.Add(new Item { Id = id, Title = title, Root = root, EntryButton = entry, Status = status });
        root.gameObject.SetActive(false);
        return valueLabel;
    }

    RectTransform CreateCanvas(string name, Vector2 size, float scale)
    {
        var rect = AeroHudView.NewRect(name, null, Vector2.zero, size);
        rect.localScale = Vector3.one * scale;
        var canvas = rect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = viewCamera;
        rect.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return rect;
    }

    static void Backing(Transform parent, Vector2 size,bool opaque=false)
    {
        var backing = AeroHudView.AddPanelImage(parent, "Backing", opaque?AeroUiSprites.SolidPanel:Resources.Load<Sprite>("JetStyle/RoundedRect"),
            Vector2.zero, size, new Color(.04f, .065f, .07f, opaque?1f:.94f));
        backing.type = UnityEngine.UI.Image.Type.Sliced;
        backing.pixelsPerUnitMultiplier = 4;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        return AeroHudView.Text(parent, name, text, position, size, fontSize, AeroUiFonts.Interface,
            color, TextAlignmentOptions.Left, 0, 0);
    }

    public void SetOpen(bool open, bool reposition = true)
    {
        if (CatalogRoot == null) return;
        if (open)
        {
            if (reposition) PlaceCatalog();
            Refresh();
            PageChanged?.Invoke();
        }
        CatalogRoot.gameObject.SetActive(open);
        Physics.SyncTransforms();
    }

    public void Refresh()
    {
        foreach (var item in items)
        {
            bool visible = item.Root != null && item.Root.gameObject.activeSelf;
            item.Status.text = visible ? "编辑" : "添加";
            item.Status.color = visible ? AeroUiTokens.Secondary : AeroUiTokens.Mint;
            // Existing items stay clickable so a request selects their current instance.
            item.EntryButton.interactable = true;
        }
        if (nextValue != null) nextValue.text = route != null ? route.NextName : "—";
    }

    public void SetItemVisible(string id, bool visible)
    {
        foreach (var item in items)
        {
            if (item.Id != id) continue;
            item.Root.gameObject.SetActive(visible);
            Refresh();
            Physics.SyncTransforms();
            return;
        }
    }

    public void ResetWidgets()
    {
        foreach (var item in items)
            if (item.Root != null) item.Root.gameObject.SetActive(false);
        Refresh();
        Physics.SyncTransforms();
    }

    void Update() => UpdateTimer();

    void UpdateTimer()
    {
        int seconds = Mathf.FloorToInt(Mathf.Max(0, Time.unscaledTime - sessionStart));
        if (timerValue == null || seconds == lastElapsedSecond) return;
        lastElapsedSecond = seconds;
        timerValue.text = seconds >= 3600
            ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}"
            : $"{seconds / 60:00}:{seconds % 60:00}";
    }

    void PlaceCatalog()
    {
        if (viewCamera == null || CatalogRoot == null) return;
        CatalogRoot.position = viewCamera.transform.TransformPoint(new Vector3(0, -.05f, .55f));
        CatalogRoot.rotation = viewCamera.transform.rotation;
    }

    void OnDestroy()
    {
        if (route != null) route.Changed -= Refresh;
        foreach (var item in items) DestroyRoot(item.Root);
        DestroyRoot(CatalogRoot);
    }

    static void DestroyRoot(Transform root)
    {
        if (root == null) return;
        if (Application.isPlaying) Destroy(root.gameObject);
        else DestroyImmediate(root.gameObject);
    }
}
