using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 디자인 토큰 시스템. 팔레트 교체로 전체 테마 변경 가능.
/// 사용법: UITheme.Apply(UITheme.Palettes.CyberNeon) 또는 커스텀 팔레트 생성.
/// </summary>
public static class UITheme
{
    // ====== Active Palette ======
    private static ColorPalette _palette = Palettes.CyberNeon;
    public static ColorPalette Colors => _palette;

    public static void Apply(ColorPalette palette)
    {
        _palette = palette;
    }

    // ====== Color Palette Definition ======
    public class ColorPalette
    {
        public string Name;

        // Background
        public Color BgDarkest;
        public Color BgDark;
        public Color BgPanel;
        public Color BgPanelDark;
        public Color BgPanelLight;
        public Color BgScrollTint;

        // Text
        public Color TextPrimary;
        public Color TextSecondary;
        public Color TextMuted;
        public Color TextDisabled;
        public Color TextWarm;
        public Color TextInfo;

        // Status
        public Color StatusSuccess;
        public Color StatusWarning;
        public Color StatusDanger;
        public Color StatusInfo;
        public Color StatusGold;

        // Button
        public Color BtnConfirm;
        public Color BtnClose;
        public Color BtnDanger;
        public Color BtnNav;
        public Color BtnDisabled;
        public Color BtnGhost;

        // Accent
        public Color AccentPrimary;
        public Color AccentLight;

        // Overlay
        public Color OverlayDim;    // alpha 0.85
        public Color OverlayDense;  // alpha 0.96
        public Color OverlayLight;  // alpha 0.60

        // Card
        public Color CardBg;
        public Color CardDefault;
        public Color CardEquipped;
    }

    // ====== Token Resolver ======
    private static Dictionary<string, System.Func<Color>> _tokenMap;

    static UITheme()
    {
        RebuildTokenMap();
    }

    private static void RebuildTokenMap()
    {
        _tokenMap = new Dictionary<string, System.Func<Color>>
        {
            // Bg
            ["Bg.Darkest"]    = () => _palette.BgDarkest,
            ["Bg.Dark"]       = () => _palette.BgDark,
            ["Bg.Panel"]      = () => _palette.BgPanel,
            ["Bg.PanelDark"]  = () => _palette.BgPanelDark,
            ["Bg.PanelLight"] = () => _palette.BgPanelLight,
            ["Bg.ScrollTint"] = () => _palette.BgScrollTint,
            // Text
            ["Text.Primary"]   = () => _palette.TextPrimary,
            ["Text.Secondary"] = () => _palette.TextSecondary,
            ["Text.Muted"]     = () => _palette.TextMuted,
            ["Text.Disabled"]  = () => _palette.TextDisabled,
            ["Text.Warm"]      = () => _palette.TextWarm,
            ["Text.Info"]      = () => _palette.TextInfo,
            // Status
            ["Status.Success"] = () => _palette.StatusSuccess,
            ["Status.Warning"] = () => _palette.StatusWarning,
            ["Status.Danger"]  = () => _palette.StatusDanger,
            ["Status.Info"]    = () => _palette.StatusInfo,
            ["Status.Gold"]    = () => _palette.StatusGold,
            // Button
            ["Btn.Confirm"]  = () => _palette.BtnConfirm,
            ["Btn.Close"]    = () => _palette.BtnClose,
            ["Btn.Danger"]   = () => _palette.BtnDanger,
            ["Btn.Nav"]      = () => _palette.BtnNav,
            ["Btn.Disabled"] = () => _palette.BtnDisabled,
            ["Btn.Ghost"]    = () => _palette.BtnGhost,
            // Accent
            ["Accent.Primary"] = () => _palette.AccentPrimary,
            ["Accent.Light"]   = () => _palette.AccentLight,
            // Overlay
            ["Overlay.Dim"]   = () => _palette.OverlayDim,
            ["Overlay.Dense"] = () => _palette.OverlayDense,
            ["Overlay.Light"] = () => _palette.OverlayLight,
            // Card
            ["Card.Bg"]       = () => _palette.CardBg,
            ["Card.Default"]  = () => _palette.CardDefault,
            ["Card.Equipped"] = () => _palette.CardEquipped,
        };
    }

    /// <summary>토큰 문자열 → Color. 없으면 magenta 반환.</summary>
    public static Color ResolveColor(string token)
    {
        if (string.IsNullOrEmpty(token)) return Color.white;

        // HEX (#RRGGBB or #RRGGBBAA)
        if (token.StartsWith("#"))
        {
            if (ColorUtility.TryParseHtmlString(token, out var c)) return c;
            return Color.magenta;
        }

        // Token
        if (_tokenMap.TryGetValue(token, out var getter)) return getter();

        Debug.LogWarning($"[UITheme] Unknown color token: {token}");
        return Color.magenta;
    }

    // ====== Typography ======
    public static class Typography
    {
        // 모든 값은 ScreenWidth(1080cu) 대비 비율
        public static class Display  { public const float Large = 0.074f; public const float Medium = 0.060f; public const float Small = 0.050f; }
        public static class Headline { public const float Large = 0.044f; public const float Medium = 0.037f; public const float Small = 0.033f; }
        public static class Title    { public const float Large = 0.033f; public const float Medium = 0.028f; public const float Small = 0.024f; }
        public static class Body     { public const float Large = 0.026f; public const float Medium = 0.022f; public const float Small = 0.019f; }
        public static class Label    { public const float Large = 0.022f; public const float Medium = 0.019f; public const float Small = 0.017f; }
        public static class Caption  { public const float Large = 0.019f; public const float Medium = 0.017f; public const float Small = 0.015f; }

        private static readonly Dictionary<string, float> _fontTokens = new Dictionary<string, float>
        {
            ["Display.Large"]  = Display.Large,  ["Display.Medium"]  = Display.Medium,  ["Display.Small"]  = Display.Small,
            ["Headline.Large"] = Headline.Large, ["Headline.Medium"] = Headline.Medium, ["Headline.Small"] = Headline.Small,
            ["Title.Large"]    = Title.Large,    ["Title.Medium"]    = Title.Medium,    ["Title.Small"]    = Title.Small,
            ["Body.Large"]     = Body.Large,     ["Body.Medium"]     = Body.Medium,     ["Body.Small"]     = Body.Small,
            ["Label.Large"]    = Label.Large,    ["Label.Medium"]    = Label.Medium,    ["Label.Small"]    = Label.Small,
            ["Caption.Large"]  = Caption.Large,  ["Caption.Medium"]  = Caption.Medium,  ["Caption.Small"]  = Caption.Small,
        };

        public static float Resolve(string token)
        {
            if (_fontTokens.TryGetValue(token, out var f)) return f;
            if (float.TryParse(token, out var raw)) return raw;
            return Body.Medium;
        }
    }

    // ====== Layout Constants ======
    public static class Layout
    {
        // sh: 비율
        public const float TopBarH = 0.052f;
        public const float BottomBarH = 0.068f;
        public const float MinButtonH = 0.038f;
        // sw: 비율
        public const float DialogW = 0.90f;

        private static readonly Dictionary<string, float> _layoutTokens = new Dictionary<string, float>
        {
            ["Layout.TopBarH"]    = TopBarH,
            ["Layout.BottomBarH"] = BottomBarH,
            ["Layout.MinButtonH"] = MinButtonH,
            ["Layout.DialogW"]    = DialogW,
        };

        public static bool TryResolve(string token, out float value)
        {
            return _layoutTokens.TryGetValue(token, out value);
        }
    }

    // ====== Spacing Tokens ======
    public static class Spacing
    {
        // cu 값 (Canvas Unit)
        public const float GapXs = 6f;
        public const float GapSm = 8f;
        public const float GapMd = 10f;
        public const float GapLg = 12f;
        public const float GapXl = 16f;

        public const float PaddingSm = 10f;
        public const float PaddingMd = 16f;
        public const float PaddingLg = 24f;

        private static readonly Dictionary<string, float> _spacingTokens = new Dictionary<string, float>
        {
            ["Gap.xs"] = GapXs, ["Gap.sm"] = GapSm, ["Gap.md"] = GapMd, ["Gap.lg"] = GapLg, ["Gap.xl"] = GapXl,
            ["Padding.sm"] = PaddingSm, ["Padding.md"] = PaddingMd, ["Padding.lg"] = PaddingLg,
        };

        public static bool TryResolve(string token, out float value)
        {
            return _spacingTokens.TryGetValue(token, out value);
        }
    }

    // ====== Built-in Palettes ======
    public static class Palettes
    {
        public static readonly ColorPalette CyberNeon = new ColorPalette
        {
            Name = "Cyber Neon",
            // Background
            BgDarkest    = Hex("#0A0A0F"),
            BgDark       = Hex("#12121A"),
            BgPanel      = Hex("#1A1A2E"),
            BgPanelDark  = Hex("#141425"),
            BgPanelLight = Hex("#222240"),
            BgScrollTint = Hex("#0F0F1A80"),
            // Text
            TextPrimary   = Hex("#E8E8F0"),
            TextSecondary = Hex("#A0A0B8"),
            TextMuted     = Hex("#606078"),
            TextDisabled  = Hex("#404050"),
            TextWarm      = Hex("#FFD4A0"),
            TextInfo      = Hex("#80C8FF"),
            // Status
            StatusSuccess = Hex("#00E676"),
            StatusWarning = Hex("#FFD740"),
            StatusDanger  = Hex("#FF5252"),
            StatusInfo    = Hex("#448AFF"),
            StatusGold    = Hex("#FFD700"),
            // Button
            BtnConfirm  = Hex("#00C853"),
            BtnClose    = Hex("#424260"),
            BtnDanger   = Hex("#D32F2F"),
            BtnNav      = Hex("#303050"),
            BtnDisabled = Hex("#2A2A3A"),
            BtnGhost    = new Color(1, 1, 1, 0.08f),
            // Accent
            AccentPrimary = Hex("#00E5FF"),
            AccentLight   = Hex("#80F0FF"),
            // Overlay
            OverlayDim   = new Color(0, 0, 0, 0.85f),
            OverlayDense = new Color(0, 0, 0, 0.96f),
            OverlayLight = new Color(0, 0, 0, 0.60f),
            // Card
            CardBg       = Hex("#1A1A2E"),
            CardDefault  = Hex("#222240"),
            CardEquipped = Hex("#1A3A2A"),
        };

        public static readonly ColorPalette OceanBreeze = new ColorPalette
        {
            Name = "Ocean Breeze",
            BgDarkest    = Hex("#0B1622"),
            BgDark       = Hex("#0F2030"),
            BgPanel      = Hex("#162D44"),
            BgPanelDark  = Hex("#0F2030"),
            BgPanelLight = Hex("#1E3A55"),
            BgScrollTint = Hex("#0B162280"),
            TextPrimary   = Hex("#E0F0FF"),
            TextSecondary = Hex("#90B8D8"),
            TextMuted     = Hex("#506880"),
            TextDisabled  = Hex("#304050"),
            TextWarm      = Hex("#FFDAB0"),
            TextInfo      = Hex("#64B5F6"),
            StatusSuccess = Hex("#26A69A"),
            StatusWarning = Hex("#FFA726"),
            StatusDanger  = Hex("#EF5350"),
            StatusInfo    = Hex("#42A5F5"),
            StatusGold    = Hex("#FFD54F"),
            BtnConfirm  = Hex("#00897B"),
            BtnClose    = Hex("#37474F"),
            BtnDanger   = Hex("#C62828"),
            BtnNav      = Hex("#263238"),
            BtnDisabled = Hex("#1C2830"),
            BtnGhost    = new Color(1, 1, 1, 0.06f),
            AccentPrimary = Hex("#26C6DA"),
            AccentLight   = Hex("#80DEEA"),
            OverlayDim   = new Color(0.02f, 0.06f, 0.1f, 0.85f),
            OverlayDense = new Color(0.02f, 0.06f, 0.1f, 0.96f),
            OverlayLight = new Color(0.02f, 0.06f, 0.1f, 0.60f),
            CardBg       = Hex("#162D44"),
            CardDefault  = Hex("#1E3A55"),
            CardEquipped = Hex("#1A4040"),
        };

        public static readonly ColorPalette SunsetWarm = new ColorPalette
        {
            Name = "Sunset Warm",
            BgDarkest    = Hex("#1A0F0A"),
            BgDark       = Hex("#251812"),
            BgPanel      = Hex("#332218"),
            BgPanelDark  = Hex("#2A1A10"),
            BgPanelLight = Hex("#3D2A1E"),
            BgScrollTint = Hex("#1A0F0A80"),
            TextPrimary   = Hex("#FFF0E0"),
            TextSecondary = Hex("#D4A880"),
            TextMuted     = Hex("#8A6848"),
            TextDisabled  = Hex("#5A4030"),
            TextWarm      = Hex("#FFE0B0"),
            TextInfo      = Hex("#FFB060"),
            StatusSuccess = Hex("#66BB6A"),
            StatusWarning = Hex("#FFCA28"),
            StatusDanger  = Hex("#FF7043"),
            StatusInfo    = Hex("#FFA726"),
            StatusGold    = Hex("#FFD740"),
            BtnConfirm  = Hex("#E65100"),
            BtnClose    = Hex("#4E342E"),
            BtnDanger   = Hex("#BF360C"),
            BtnNav      = Hex("#3E2723"),
            BtnDisabled = Hex("#33221A"),
            BtnGhost    = new Color(1, 0.9f, 0.8f, 0.08f),
            AccentPrimary = Hex("#FF6D00"),
            AccentLight   = Hex("#FFAB40"),
            OverlayDim   = new Color(0.08f, 0.04f, 0.02f, 0.85f),
            OverlayDense = new Color(0.08f, 0.04f, 0.02f, 0.96f),
            OverlayLight = new Color(0.08f, 0.04f, 0.02f, 0.60f),
            CardBg       = Hex("#332218"),
            CardDefault  = Hex("#3D2A1E"),
            CardEquipped = Hex("#3D3A18"),
        };

        public static readonly ColorPalette CleanLight = new ColorPalette
        {
            Name = "Clean Light",
            BgDarkest    = Hex("#FFFFFF"),
            BgDark       = Hex("#F5F5F8"),
            BgPanel      = Hex("#EBEBF0"),
            BgPanelDark  = Hex("#E0E0E8"),
            BgPanelLight = Hex("#F0F0F5"),
            BgScrollTint = Hex("#F5F5F880"),
            TextPrimary   = Hex("#1A1A2E"),
            TextSecondary = Hex("#555570"),
            TextMuted     = Hex("#9090A0"),
            TextDisabled  = Hex("#C0C0CC"),
            TextWarm      = Hex("#8B5E3C"),
            TextInfo      = Hex("#1976D2"),
            StatusSuccess = Hex("#2E7D32"),
            StatusWarning = Hex("#F57F17"),
            StatusDanger  = Hex("#C62828"),
            StatusInfo    = Hex("#1565C0"),
            StatusGold    = Hex("#F9A825"),
            BtnConfirm  = Hex("#2E7D32"),
            BtnClose    = Hex("#BDBDBD"),
            BtnDanger   = Hex("#C62828"),
            BtnNav      = Hex("#E0E0E0"),
            BtnDisabled = Hex("#EEEEEE"),
            BtnGhost    = new Color(0, 0, 0, 0.05f),
            AccentPrimary = Hex("#1976D2"),
            AccentLight   = Hex("#64B5F6"),
            OverlayDim   = new Color(0, 0, 0, 0.50f),
            OverlayDense = new Color(0, 0, 0, 0.70f),
            OverlayLight = new Color(0, 0, 0, 0.30f),
            CardBg       = Hex("#FFFFFF"),
            CardDefault  = Hex("#F5F5F8"),
            CardEquipped = Hex("#E8F5E9"),
        };

        /// <summary>이름으로 팔레트 선택</summary>
        public static ColorPalette GetByName(string name)
        {
            switch (name?.ToLower())
            {
                case "cyberneon":
                case "cyber neon":    return CyberNeon;
                case "oceanbreeze":
                case "ocean breeze":  return OceanBreeze;
                case "sunsetwarm":
                case "sunset warm":   return SunsetWarm;
                case "cleanlight":
                case "clean light":   return CleanLight;
                default:              return CyberNeon;
            }
        }

        /// <summary>모든 팔레트 목록</summary>
        public static ColorPalette[] All => new[] { CyberNeon, OceanBreeze, SunsetWarm, CleanLight };
    }

    // ====== Utility ======
    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
