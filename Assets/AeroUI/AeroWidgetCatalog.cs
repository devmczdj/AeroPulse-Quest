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
    public bool IsOpen => CatalogRoot != null && CatalogRoot.gameObject.activeSelf;
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
        CatalogRoot = CreateCanvas("WidgetCatalog", new Vector2(400, 460), .001f);
        Backing(CatalogRoot, new Vector2(400, 460));
        Label(CatalogRoot, "CatalogTitle", "组件", new Vector2(-172, 194), new Vector2(344, 44), 28, AeroUiTokens.Ink);

        AddItem("heart", "心率", "heart-pulse", "142", "BPM · 模拟数据");
        AddItem("pace", "配速", "fast-forward", "5:20", "/km · 模拟数据");
        AddItem("distance", "距离", "map", "3.20", "km · 模拟数据");
        nextValue = AddItem("next", "下一站", "arrow-right", "", "示意路线");
        nextValue.fontSize = 30;
        timerValue = AddItem("timer", "会话计时", "rotate-ccw", "00:00", "应用运行时长");

        DoneButton = AeroHudView.ActionButton(CatalogRoot, "CatalogDone", "完成",
            new Vector2(0, -198), new Vector2(352, 52), new Vector3(352, 52, 8));
        var doneLabel = DoneButton.GetComponentInChildren<TextMeshProUGUI>();
        doneLabel.font = AeroUiFonts.Interface;
        doneLabel.characterSpacing = 0;
        DoneButton.onClick.AddListener(() => Finished?.Invoke());
        PlaceCatalog();
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

        var entry = AeroHudView.ActionButton(CatalogRoot, "CatalogEntry_" + id, title,
            new Vector2(0, 126 - items.Count * 64), new Vector2(352, 56), new Vector3(352, 56, 8));
        var titleLabel = entry.GetComponentInChildren<TextMeshProUGUI>();
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

    static void Backing(Transform parent, Vector2 size)
    {
        var backing = AeroHudView.AddPanelImage(parent, "Backing", Resources.Load<Sprite>("JetStyle/RoundedRect"),
            Vector2.zero, size, new Color(.04f, .065f, .07f, .94f));
        backing.type = UnityEngine.UI.Image.Type.Sliced;
        backing.pixelsPerUnitMultiplier = 4;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        return AeroHudView.Text(parent, name, text, position, size, fontSize, AeroUiFonts.Interface,
            color, TextAlignmentOptions.Left, 0, 0);
    }

    public void SetOpen(bool open)
    {
        if (CatalogRoot == null) return;
        if (open)
        {
            PlaceCatalog();
            Refresh();
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

    void LateUpdate()
    {
        if (IsOpen) PlaceCatalog();
    }

    void PlaceCatalog()
    {
        if (viewCamera == null || CatalogRoot == null) return;
        CatalogRoot.position = viewCamera.transform.TransformPoint(new Vector3(-.48f, 0, 1f));
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
