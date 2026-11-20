using UnityEngine;

// Design tokens live in DESIGN.md; keep these values in sync with it.
public static class AeroUiTokens
{
    public const float PanelWidth = 760f;
    public const float PanelHeight = 430f;
    public const float PanelRadius = 36f;

    public static readonly Color Ink = Hex("F5FAF9");
    public static readonly Color Secondary = Hex("B2C4C3");
    public static readonly Color Tertiary = Hex("A4B8B8");
    public static readonly Color Mint = Hex("7BE8C4");
    public static readonly Color Amber = Hex("FFB45C");

    public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.12f);
    public static readonly Color ChipFill = new Color(0.035f, 0.05f, 0.06f, 0.62f);
    public static readonly Color ChipStroke = new Color(1f, 1f, 1f, 0.18f);
    public static readonly Color ButtonFill = new Color(0.07f, 0.11f, 0.12f, 0.9f);
    public static readonly Color ButtonHover = new Color(0.48f, 0.91f, 0.77f, 0.22f);
    public static readonly Color ToolPanelFill = new Color(0.04f, 0.065f, 0.07f, 0.78f);
    public static readonly Color ToolSectionFill = new Color(0.04f, 0.065f, 0.07f, 0.68f);

    static Color Hex(string value)
    {
        return ColorUtility.TryParseHtmlString("#" + value, out var color) ? color : Color.white;
    }
}
