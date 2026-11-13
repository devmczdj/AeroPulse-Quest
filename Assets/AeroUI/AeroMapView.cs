using TMPro;
using UnityEngine;

public class AeroMapView : MonoBehaviour
{
    public UnityEngine.UI.Button AdvanceButton { get; private set; }
    public RectTransform CurrentMarker { get; private set; }
    public RectTransform NextMarker { get; private set; }
    public TextMeshProUGUI CurrentLabel { get; private set; }
    public TextMeshProUGUI NextLabel { get; private set; }
    TextMeshProUGUI stepLabel;
    TextMeshProUGUI advanceLabel;
    AeroRouteDemo route;

    public static AeroMapView Build(Transform parent, AeroRouteDemo route)
    {
        var root = AeroHudView.NewRect("MapContent", parent, Vector2.zero, new Vector2(760, 430));
        var view = root.gameObject.AddComponent<AeroMapView>();
        view.route = route;
        view.Create();
        route.Changed += view.Refresh;
        view.Refresh();
        return view;
    }

    void OnDestroy() { if (route != null) route.Changed -= Refresh; }

    void Create()
    {
        var board = AeroHudView.NewRect("RouteBoard", transform, new Vector2(-128, -6), new Vector2(432, 232));
        AeroHudView.AddPanelImage(board, "MapBacking", AeroUiSprites.Pill, Vector2.zero,
                                 new Vector2(432, 232), new Color(0.03f, 0.07f, 0.08f, 0.62f));
        // Few, broad lines rather than a fine repeating grid in the headset.
        for (int i = -1; i <= 1; i++)
        {
            Line(board, "GridX" + i, new Vector2(i * 100, -102), new Vector2(i * 100, 102), 2.5f,
                 new Color(1, 1, 1, 0.055f));
            Line(board, "GridY" + i, new Vector2(-202, i * 65), new Vector2(202, i * 65), 2.5f,
                 new Color(1, 1, 1, 0.055f));
        }
        for (int i = 1; i < route.Count; i++)
            Line(board, "RouteSegment" + i, route.StopAt(i - 1).Position, route.StopAt(i).Position,
                 5f, new Color(0.48f, 0.91f, 0.77f, 0.85f));
        for (int i = 0; i < route.Count; i++)
        {
            var stop = route.StopAt(i);
            AeroHudView.AddPanelImage(board, "Stop" + i, AeroUiSprites.Dot, stop.Position,
                                     new Vector2(12, 12), AeroUiTokens.Ink);
            Label(board, "Place" + i, stop.Name, stop.Position + new Vector2(0, i % 2 == 0 ? -21 : 23),
                  new Vector2(116, 26), 16, AeroUiTokens.Ink, true);
        }
        CurrentMarker = (RectTransform)AeroHudView.AddPanelImage(board, "CurrentMarker", AeroUiSprites.Dot,
            Vector2.zero, new Vector2(30, 30), AeroUiTokens.Mint).transform;
        AeroHudView.AddPanelImage(CurrentMarker, "CurrentCore", AeroUiSprites.Dot, Vector2.zero,
            new Vector2(13, 13), new Color(0.03f, 0.09f, 0.09f, 1));
        NextMarker = (RectTransform)AeroHudView.AddPanelImage(board, "NextMarker", AeroUiSprites.White,
            Vector2.zero, new Vector2(17, 17), AeroUiTokens.Amber).transform;
        NextMarker.localRotation = Quaternion.Euler(0, 0, 45);
        Label(transform, "MapOrientation", "SCHEMATIC / N UP", new Vector2(-336, 104),
              new Vector2(220, 26), 16, AeroUiTokens.Secondary);
        Label(transform, "CurrentHeading", "YOU ARE HERE", new Vector2(112, 88),
              new Vector2(240, 28), 16, AeroUiTokens.Mint);
        CurrentLabel = Label(transform, "MapCurrent", "", new Vector2(112, 55),
                             new Vector2(240, 36), 22, AeroUiTokens.Ink);
        Label(transform, "NextHeading", "NEXT STOP", new Vector2(112, 12),
              new Vector2(240, 28), 16, AeroUiTokens.Amber);
        NextLabel = Label(transform, "MapNext", "", new Vector2(112, -20),
                          new Vector2(240, 36), 22, AeroUiTokens.Ink);
        stepLabel = Label(transform, "DemoPosition", "", new Vector2(112, -55),
                          new Vector2(240, 28), 16, AeroUiTokens.Secondary);
        AdvanceButton = AeroHudView.ActionButton(transform, "AdvanceDemo", "ADVANCE",
            new Vector2(244, -94), new Vector2(208, 52), new Vector3(216, 56, 8));
        advanceLabel = AdvanceButton.GetComponentInChildren<TextMeshProUGUI>();
        AdvanceButton.onClick.AddListener(route.Advance);
    }

    void Refresh()
    {
        CurrentMarker.anchoredPosition = route.Current.Position;
        NextMarker.gameObject.SetActive(route.NextIndex >= 0);
        if (route.NextIndex >= 0) NextMarker.anchoredPosition = route.StopAt(route.NextIndex).Position;
        CurrentLabel.text = route.Current.Name;
        NextLabel.text = route.NextName;
        stepLabel.text = "DEMO " + (route.CurrentIndex + 1) + " / " + route.Count;
        advanceLabel.text = route.NextIndex < 0 ? "RESET" : "ADVANCE";
    }

    public void SetHover(bool hover)
    {
        AdvanceButton.GetComponent<UnityEngine.UI.Image>().color = hover ? AeroUiTokens.ButtonHover : AeroUiTokens.ButtonFill;
        advanceLabel.color = hover ? AeroUiTokens.Mint : AeroUiTokens.Ink;
    }

    static void Line(Transform parent, string name, Vector2 a, Vector2 b, float width, Color color)
    {
        var image = AeroHudView.AddPanelImage(parent, name, AeroUiSprites.Pill, (a + b) * 0.5f,
                                              new Vector2((b - a).magnitude, width), color);
        image.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position,
                                 Vector2 size, float fontSize, Color color, bool centered = false)
        => AeroHudView.Text(parent, name, text, position, size, fontSize, AeroUiFonts.SemiBold, color,
                            centered ? TextAlignmentOptions.Center : TextAlignmentOptions.Left, 0, 0, centered);
}
