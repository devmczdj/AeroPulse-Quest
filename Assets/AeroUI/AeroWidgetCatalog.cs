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
    public int LayerMask { get; private set; } = 7;
    public UnityEngine.UI.Button[] LayerButtons { get; private set; }
    public UnityEngine.UI.Button KeepOpenButton { get; private set; }
    public UnityEngine.UI.Button RecallButton { get; private set; }
    public bool PlacementFixed { get; private set; }
    public Transform LayerControls { get; private set; }
    public UnityEngine.UI.Button CloseButton { get; private set; }
    public UnityEngine.UI.Button CloseSmallButton { get; private set; }
    public UnityEngine.UI.Button RecallSmallButton { get; private set; }
    public AeroDragHandle CatalogMoveHandle { get; private set; }
    public AeroDragHandle CatalogDepthHandle { get; private set; }
    public AeroDragHandle CatalogScaleHandle { get; private set; }
    public Action<AeroEditLayer> LayerRequested;
    public Action RecallRequested;
    public Action PageChanged;
    bool modal,handAvailable;
    TextMeshProUGUI catalogHint;
    public bool IsOpen => CatalogRoot != null && CatalogRoot.gameObject.activeInHierarchy && catalogCanvas!=null && catalogCanvas.enabled;
    Canvas catalogCanvas;
    public Action<string> Requested;
    public Action Finished;
    public Action Hidden;
    public Action CloseSmallRequested;
    public Action RecallSmallRequested;

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
        CatalogRoot = CreateCanvas("WidgetCatalog", new Vector2(480, 620), .00075f);
        catalogCanvas=CatalogRoot.GetComponent<Canvas>();catalogCanvas.sortingOrder=100;
        CatalogRoot.gameObject.AddComponent<BoxCollider>().size=new Vector3(480,620,3);
        Backing(CatalogRoot, new Vector2(480, 620),true);
        CloseButton = AeroHudView.ActionButton(CatalogRoot, "CatalogClose", "关闭", new Vector2(218, 286),
            new Vector2(36, 36), new Vector3(44, 44, 8));
        AeroUiIcons.Apply(CloseButton, "close", 28);
        CloseButton.onClick.AddListener(() => { if (!modal) Hidden?.Invoke(); });
        CreateLayerControls();
        CatalogMoveHandle = ToolHandle("CatalogMove", new Vector2(-112, 342), new Vector2(174, 44), "grip-vertical", "环绕", false, false);
        CatalogDepthHandle = ToolHandle("CatalogDepth", new Vector2(112, 342), new Vector2(174, 44), "crosshair", "远近", true, false);
        ToolHandle("CatalogScale", new Vector2(272, -342), new Vector2(56, 56), "move-diagonal", "", false, true);
        ComponentsTab = Tab("ComponentsTab", "组件", -105, false);
        ProfilesTab = Tab("ProfilesTab", "档案", 105, true);
        ComponentsPage = AeroHudView.NewRect("ComponentsPage", CatalogRoot, new Vector2(0,-70), new Vector2(460, 330));
        ProfilesPage = AeroHudView.NewRect("ProfilesPage", CatalogRoot, new Vector2(0,-70), new Vector2(460, 330));
        ProfilesPage.gameObject.SetActive(false);
        catalogHint=Label(ComponentsPage, "CatalogHint", "点按添加，捏合拖出", new Vector2(-202, 136), new Vector2(400, 24), 18, AeroUiTokens.Secondary);

        AddItem("heart", "心率", "heart-pulse", "142", "BPM · 模拟数据");
        AddItem("pace", "配速", "fast-forward", "5:20", "/km · 模拟数据");
        AddItem("distance", "距离", "map", "3.20", "km · 模拟数据");
        nextValue = AddItem("next", "下一站", "arrow-right", "", "示意路线");
        nextValue.fontSize = 30;
        timerValue = AddItem("timer", "会话计时", "rotate-ccw", "00:00", "应用运行时长");

        CloseSmallButton = FooterButton("CatalogCloseSmall", "关闭", "close", new Vector2(-174, -276), new Vector2(112, 46),
            () => CloseSmallRequested?.Invoke());
        RecallSmallButton = FooterButton("CatalogRecallSmall", "召回", "rotate-ccw", new Vector2(-52, -276), new Vector2(112, 46),
            () => RecallSmallRequested?.Invoke());
        DoneButton = FooterButton("CatalogDone", "完成编辑", "check", new Vector2(116, -276), new Vector2(208, 46),
            () => Finished?.Invoke());
        RefreshTabs(null, null);
    }

    void CreateLayerControls()
    {
        LayerControls = AeroHudView.NewRect("EditorToolsHeader", CatalogRoot, Vector2.zero, new Vector2(440, 112));
        Backing(LayerControls, new Vector2(440, 112),true);
        LayerControls.gameObject.AddComponent<BoxCollider>().size=new Vector3(440,112,3);
        LayerButtons = new UnityEngine.UI.Button[3];
        string[] names = { "环境", "手部", "面前" }, icons = { "cube", "hand-open", "vr-headset" };
        for (int i = 0; i < LayerButtons.Length; i++)
        {
            var layer = (AeroEditLayer)i;
            LayerButtons[i] = ToolButton("EditLayer" + layer, names[i], new Vector2((i - 1) * 144, 24), new Vector2(128, 40),
                () => { if (!modal) LayerRequested?.Invoke(layer); });
            AeroUiIcons.Label(LayerButtons[i], icons[i], names[i], 22);
        }
        RecallButton = ToolButton("RecallCatalog", "回到面前", new Vector2(-108, -28), new Vector2(204, 40),
            () => { if (!modal) RecallRequested?.Invoke(); });
        AeroUiIcons.Label(RecallButton, "crosshair", "回到面前", 22);
        KeepOpenButton = ToolButton("KeepToolsOpen", "保持打开", new Vector2(108, -28), new Vector2(204, 40),
            () => { if (!modal) SetKeepOpen(!KeepOpen); });
        AeroUiIcons.Label(KeepOpenButton, "pushpin-off", "保持打开", 22);
    }

    UnityEngine.UI.Button ToolButton(string name, string title, Vector2 position, Vector2 size, Action action)
    {
        var button = AeroHudView.ActionButton(LayerControls, name, title, position, size, new Vector3(size.x, size.y + 4, 8));
        var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        text.font = AeroUiFonts.Interface; text.characterSpacing = 0; text.fontSize = 22;
        button.onClick.AddListener(() => action());
        return button;
    }

    UnityEngine.UI.Button FooterButton(string name, string label, string icon, Vector2 position, Vector2 size, Action action)
    {
        var button = AeroHudView.ActionButton(CatalogRoot, name, label, position, size, new Vector3(size.x, size.y + 6, 8));
        AeroUiIcons.Label(button, icon, label, 22);
        button.onClick.AddListener(() => action());
        return button;
    }

    AeroDragHandle ToolHandle(string name, Vector2 position, Vector2 size, string icon, string caption, bool depth, bool resize)
    {
        var image = AeroHudView.AddPanelImage(CatalogRoot, name, AeroUiSprites.Pill, position, size, AeroUiTokens.ButtonFill);
        image.gameObject.AddComponent<BoxCollider>().size = new Vector3(size.x + 8, size.y + 8, 8);
        AeroHudView.AddPanelImage(image.transform, "Icon", AeroUiIcons.Get(icon),
            new Vector2(resize ? 0 : -50, 0), new Vector2(resize ? 24 : 22, resize ? 24 : 22), AeroUiTokens.Ink);
        if (!resize)
            AeroHudView.Text(image.transform, "Caption", caption, new Vector2(18, 0), new Vector2(110, 36),
                20, AeroUiFonts.Interface, AeroUiTokens.Ink, TextAlignmentOptions.Left, 0, 0, true);
        var handle = image.gameObject.AddComponent<AeroDragHandle>();
        handle.CatalogMove = true; handle.Depth = depth; handle.Resize = resize;
        if (resize) CatalogScaleHandle = handle;
        return handle;
    }

    public void SetEditingView(int mask)
    {
        LayerMask = mask;
        RefreshHint();RefreshTabs(null, null);
    }

    public void SetHandAvailable(bool value)
    {if(handAvailable==value)return;handAvailable=value;RefreshHint();}
    void RefreshHint()
    {if(catalogHint!=null)catalogHint.text=(LayerMask&4)!=0&&!handAvailable?"手部未追踪，请抬起左手":"点按添加，捏合拖出";}

    public void SetKeepOpen(bool value)
    {
        KeepOpen = value;
        AeroUiIcons.Set(KeepOpenButton, value ? "pushpin" : "pushpin-off");
        RefreshTabs(null, null);
    }

    UnityEngine.UI.Button Tab(string name, string title, float x, bool profiles)
    {
        var button = AeroHudView.ActionButton(CatalogRoot, name, title, new Vector2(x, 118), new Vector2(190, 44), new Vector3(190, 48, 8));
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
        CloseSmallButton.interactable = RecallSmallButton.interactable = !value;
        CloseSmallButton.gameObject.SetActive(!value);RecallSmallButton.gameObject.SetActive(!value);
        LayerControls.gameObject.SetActive(!value);
        foreach (var button in LayerButtons) button.interactable = !value;
        RecallButton.interactable = KeepOpenButton.interactable = !value;
        RefreshTabs(null, null);
    }

    public void RefreshTabs(UnityEngine.UI.Button left, UnityEngine.UI.Button right)
    {
        PaintButton(ComponentsTab, !ShowingProfiles, left, right);
        PaintButton(ProfilesTab, ShowingProfiles, left, right);
        PaintButton(DoneButton, false, left, right);
        PaintButton(CloseSmallButton, false, left, right);
        PaintButton(RecallSmallButton, false, left, right);
        if (LayerButtons == null) return;
        for (int i = 0; i < LayerButtons.Length; i++)
            PaintButton(LayerButtons[i], (LayerMask & (1 << i)) != 0, left, right);
        PaintButton(RecallButton, false, left, right);
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
            new Vector2(0, 92 - items.Count * 58), new Vector2(420, 50), new Vector3(420, 56, 8));
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
        var backing = AeroHudView.AddPanelImage(parent, "Backing", Resources.Load<Sprite>("JetStyle/RoundedRect"),
            Vector2.zero, size, opaque?AeroUiTokens.ToolPanelFill:AeroUiTokens.ToolSectionFill);
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
            if (reposition && !PlacementFixed) PlaceCatalog();
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

    void Update()
    {
        UpdateTimer();
        if (!IsOpen || viewCamera == null || CatalogRoot == null) return;
        CatalogRoot.rotation = viewCamera.transform.rotation;
    }

    void UpdateTimer()
    {
        int seconds = Mathf.FloorToInt(Mathf.Max(0, Time.unscaledTime - sessionStart));
        if (timerValue == null || seconds == lastElapsedSecond) return;
        lastElapsedSecond = seconds;
        timerValue.text = seconds >= 3600
            ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}"
            : $"{seconds / 60:00}:{seconds % 60:00}";
    }

    public void RecallPlacement(){PlacementFixed=false;PlaceCatalog();}
    public void FixPlacement(){PlacementFixed=true;}
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
