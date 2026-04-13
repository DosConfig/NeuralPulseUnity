using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// UI 요소 팩토리 + Canvas Unit(cu) 시스템.
/// SDFKit 의존성 없음 — 표준 UGUI만 사용.
///
/// 초기화: UIHelper.Initialize(canvas) 를 앱 시작 시 호출.
/// </summary>
public static class UIHelper
{
    // ====== Canvas Unit System ======
    public const float ScreenW = 1080f;
    public const float SafePadding = 24f;
    public static float SafeW => ScreenW - SafePadding * 2f; // 1032
    public static float HalfW => ScreenW / 2f;               // 540

    private static float _canvasH = 1920f; // 기본값. Initialize에서 갱신.
    public static float CanvasH => _canvasH;
    public static float HalfH => _canvasH / 2f;

    // SafeArea (notch/home indicator)
    public static Rect SafeAreaInset { get; private set; }

    /// <summary>SafeWidth의 fraction (0~1). 예: SW(0.9) = 928.8cu</summary>
    public static float SW(float fraction) => SafeW * fraction;

    /// <summary>CanvasHeight의 fraction (0~1). 예: SH(0.05) = 디바이스별 다름</summary>
    public static float SH(float fraction) => _canvasH * fraction;

    /// <summary>ScreenWidth의 fraction → 폰트 크기. 최소 20cu.</summary>
    public static float Font(float fraction) => Mathf.Max(20f, ScreenW * fraction);

    // ====== Initialization ======

    private static Canvas _rootCanvas;
    private static CanvasScaler _scaler;

    /// <summary>앱 시작 시 호출. Canvas를 생성하거나 기존 것을 등록.</summary>
    public static Canvas Initialize(Canvas existingCanvas = null)
    {
        if (existingCanvas != null)
        {
            _rootCanvas = existingCanvas;
        }
        else
        {
            var go = new GameObject("UICanvas");
            _rootCanvas = go.AddComponent<Canvas>();
            _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _rootCanvas.sortingOrder = 0;
        }

        // CanvasScaler — 가로 1080 기준 스케일
        _scaler = _rootCanvas.GetComponent<CanvasScaler>();
        if (_scaler == null) _scaler = _rootCanvas.gameObject.AddComponent<CanvasScaler>();
        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _scaler.referenceResolution = new Vector2(ScreenW, 1920f);
        _scaler.matchWidthOrHeight = 0f; // 가로 기준
        _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        // Raycaster
        if (_rootCanvas.GetComponent<GraphicRaycaster>() == null)
            _rootCanvas.gameObject.AddComponent<GraphicRaycaster>();

        // EventSystem
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        // CanvasH 계산
        RecalculateCanvasH();

        // SafeArea
        CalculateSafeArea();

        return _rootCanvas;
    }

    public static Canvas RootCanvas => _rootCanvas;

    /// <summary>화면 비율 변경 시 호출 (orientation change 등)</summary>
    public static void RecalculateCanvasH()
    {
        if (_scaler == null) return;
        float aspect = (float)Screen.height / Screen.width;
        _canvasH = ScreenW * aspect;
    }

    private static void CalculateSafeArea()
    {
        var sa = Screen.safeArea;
        float scaleX = ScreenW / Screen.width;
        float scaleY = _canvasH / Screen.height;

        SafeAreaInset = new Rect(
            sa.x * scaleX,                         // left
            (Screen.height - sa.yMax) * scaleY,    // top (notch)
            (Screen.width - sa.xMax) * scaleX,     // right
            sa.y * scaleY                          // bottom (home indicator)
        );
    }

    // ====== Factories ======

    /// <summary>텍스트 생성</summary>
    public static GameObject CreateText(Transform parent, string content,
                                         float fontSize, Color color,
                                         Vector2 position, Vector2 size,
                                         TextAnchor alignment = TextAnchor.MiddleCenter,
                                         FontStyle fontStyle = FontStyle.Normal)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        var text = go.AddComponent<Text>();
        text.text = content;
        text.fontSize = Mathf.RoundToInt(fontSize);
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = fontStyle;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        return go;
    }

    /// <summary>패널 생성 (배경 이미지 + 라운드 코너 옵션)</summary>
    public static GameObject CreatePanel(Transform parent, Vector2 position, Vector2 size,
                                          Color bgColor, float cornerRadius = 0f)
    {
        var go = new GameObject("Panel", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        // 라운드 코너: Unity 2021.2+의 pixelsPerUnitMultiplier 트릭
        // 또는 9-slice sprite 사용
        if (cornerRadius > 0f)
        {
            // 기본 Knob 스프라이트를 사용해 라운드 느낌
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Mathf.Max(1f, 20f / cornerRadius);
        }

        return go;
    }

    /// <summary>버튼 생성</summary>
    public static GameObject CreateButton(Transform parent, string label,
                                           Vector2 position, Vector2 size,
                                           Color bgColor, System.Action onClick,
                                           float fontSize = 0f)
    {
        var go = CreatePanel(parent, position, size, bgColor, 8f);
        go.name = "Button";

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        btn.colors = colors;

        if (onClick != null)
            btn.onClick.AddListener(() => onClick());

        // 라벨
        if (!string.IsNullOrEmpty(label))
        {
            float fs = fontSize > 0 ? fontSize : Font(UITheme.Typography.Body.Large);
            CreateText(go.transform, label, fs, UITheme.Colors.TextPrimary,
                       Vector2.zero, size, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        return go;
    }

    /// <summary>ButtonType 기반 버튼 생성</summary>
    public static GameObject CreateButton(Transform parent, string label,
                                           Vector2 position, Vector2 size,
                                           string buttonType, System.Action onClick)
    {
        Color bg = ResolveButtonColor(buttonType);
        return CreateButton(parent, label, position, size, bg, onClick);
    }

    /// <summary>이미지 생성</summary>
    public static GameObject CreateImage(Transform parent, Sprite sprite,
                                          Vector2 position, Vector2 size)
    {
        var go = new GameObject("Image", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        return go;
    }

    /// <summary>전체 화면 오버레이</summary>
    public static GameObject CreateFullScreenOverlay(Transform parent, Color color)
    {
        var go = new GameObject("Overlay", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;

        return go;
    }

    // ====== Button Color Resolver ======

    public static Color ResolveButtonColor(string buttonType)
    {
        if (string.IsNullOrEmpty(buttonType)) return UITheme.Colors.BtnConfirm;

        switch (buttonType.ToLower())
        {
            case "confirm":
            case "primary":   return UITheme.Colors.BtnConfirm;
            case "dismiss":
            case "close":
            case "ghost":     return UITheme.Colors.BtnGhost;
            case "danger":    return UITheme.Colors.BtnDanger;
            case "nav":
            case "secondary": return UITheme.Colors.BtnNav;
            case "disabled":  return UITheme.Colors.BtnDisabled;
            default:          return UITheme.Colors.BtnConfirm;
        }
    }

    // ====== Anchor Presets ======

    /// <summary>앵커 프리셋 적용. "TC"=TopCenter, "BL"=BottomLeft, "MC"=MiddleCenter 등.</summary>
    public static void ApplyAnchorPreset(RectTransform rt, string preset)
    {
        if (rt == null || string.IsNullOrEmpty(preset)) return;

        switch (preset.ToUpper())
        {
            case "TL": SetAnchor(rt, 0, 1, 0, 1); break;
            case "TC": SetAnchor(rt, 0.5f, 1, 0.5f, 1); break;
            case "TR": SetAnchor(rt, 1, 1, 1, 1); break;
            case "ML": SetAnchor(rt, 0, 0.5f, 0, 0.5f); break;
            case "MC": SetAnchor(rt, 0.5f, 0.5f, 0.5f, 0.5f); break;
            case "MR": SetAnchor(rt, 1, 0.5f, 1, 0.5f); break;
            case "BL": SetAnchor(rt, 0, 0, 0, 0); break;
            case "BC": SetAnchor(rt, 0.5f, 0, 0.5f, 0); break;
            case "BR": SetAnchor(rt, 1, 0, 1, 0); break;
        }
    }

    private static void SetAnchor(RectTransform rt, float ax, float ay, float px, float py)
    {
        rt.anchorMin = new Vector2(ax, ay);
        rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(px, py);
    }
}
