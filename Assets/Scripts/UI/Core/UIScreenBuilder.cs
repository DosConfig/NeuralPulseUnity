using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// JSON → GameObject 빌더.
/// Build()는 최상위 elements를 암묵적 rootColumn으로 감싸서 위→아래 레이아웃.
/// </summary>
public class UIScreenBuilder
{
    // ====== Element Definition ======
    public class ElementDef
    {
        public string id, type;
        public string x, y, w, h;
        public string text, font, fontStyle, color, bg, align, anchor;
        public string visible, action, buttonType, label, sprite, fit;
        public string gap, padding, direction, margin;
        public string dataSource, layout, cellW, cellH, cols, onItemClick;
        public string outlineColor, outlineWidth, bestFit;
        public float[] weights;
        public List<ElementDef> children;
        public List<ElementDef> template;
    }

    private class ScreenDef
    {
        public string screen, convention, archetype, bg;
        public List<ElementDef> elements;
    }

    // State
    private ScreenDef _screenDef;
    private Transform _parent;
    private Dictionary<string, object> _bindings = new Dictionary<string, object>();
    private Dictionary<string, Action> _actions = new Dictionary<string, Action>();
    private Dictionary<string, Action<int>> _indexedActions = new Dictionary<string, Action<int>>();
    private Dictionary<string, Action<float>> _floatActions = new Dictionary<string, Action<float>>();
    private Dictionary<string, List<BindTarget>> _reactive = new Dictionary<string, List<BindTarget>>();
    private Dictionary<string, GameObject> _elements = new Dictionary<string, GameObject>();
    private Dictionary<string, object> _foreachItem;
    private int _foreachIdx;

    private struct BindTarget { public GameObject go; public Text text; public string field, key; }

    // ====== Public API ======

    public static UIScreenBuilder Load(string path, Transform parent)
    {
        var b = new UIScreenBuilder { _parent = parent };
        var ta = Resources.Load<TextAsset>(path);
        if (ta == null) { Debug.LogError($"[Builder] Not found: {path}"); return b; }
        b._screenDef = ParseScreen(ta.text);
        return b;
    }

    public void SetBinding(string key, object value)
    {
        _bindings[key] = value;
        if (_reactive.TryGetValue(key, out var targets))
            foreach (var t in targets)
            {
                if (t.go == null) continue;
                if (t.field == "text" && t.text != null) t.text.text = value?.ToString() ?? "";
                else if (t.field == "visible") t.go.SetActive(IsTrue(value));
            }
    }

    public void RegisterAction(string n, Action cb) => _actions[n] = cb;
    public void RegisterIndexedAction(string n, Action<int> cb) => _indexedActions[n] = cb;
    public void RegisterFloatAction(string n, Action<float> cb) => _floatActions[n] = cb;
    public GameObject GetElement(string id) { _elements.TryGetValue(id, out var g); return g; }

    public void Cleanup()
    {
        _bindings.Clear(); _actions.Clear(); _reactive.Clear(); _elements.Clear();
    }

    // ====== Build ======

    public GameObject Build()
    {
        if (_screenDef == null) return new GameObject("ErrorScreen");

        float screenW = UIHelper.ScreenW;
        float screenH = UIHelper.CanvasH;

        // Root (full-screen stretch)
        var root = CreateRT(_parent, _screenDef.screen ?? "Screen");
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        if (!string.IsNullOrEmpty(_screenDef.bg))
        {
            var img = root.AddComponent<Image>();
            img.color = UITheme.ResolveColor(_screenDef.bg);
            img.raycastTarget = true;
        }

        // 최상위 elements → 암묵적 rootColumn으로 레이아웃
        if (_screenDef.elements != null && _screenDef.elements.Count > 0)
        {
            LayoutColumn(root.transform, _screenDef.elements, screenW, screenH, 0, null);
        }

        return root;
    }

    // ====== Layout: Column ======

    /// <summary>부모 안에 elements를 위→아래로 배치. expand 지원.</summary>
    private void LayoutColumn(Transform parent, List<ElementDef> elements,
                               float containerW, float containerH, float gap, float[] pad)
    {
        float pT = pad != null && pad.Length > 0 ? pad[0] : 0;
        float pR = pad != null && pad.Length > 1 ? pad[1] : 0;
        float pB = pad != null && pad.Length > 2 ? pad[2] : 0;
        float pL = pad != null && pad.Length > 3 ? pad[3] : 0;
        float innerW = containerW - pL - pR;
        float innerH = containerH - pT - pB;

        // Pass 1: 자식 빌드 + 높이 측정
        var built = new List<(GameObject go, float h, bool expand, bool vis, string id)>();
        float fixedSum = 0;
        int visCount = 0;
        bool hasExpand = false;

        foreach (var el in elements)
        {
            // anchor가 있는 요소는 Column 밖에서 절대 배치
            if (!string.IsNullOrEmpty(el.anchor))
            {
                var go = BuildAny(el, parent, innerW, innerH);
                if (go != null)
                {
                    var rt = go.GetComponent<RectTransform>();
                    UIHelper.ApplyAnchorPreset(rt, el.anchor);
                    rt.anchoredPosition = ResolvePos(el, innerW, innerH);
                    Reg(el.id, go);
                }
                continue;
            }

            bool isExpand = el.h == "expand";
            float childH = isExpand ? 0 : MeasureHeight(el, innerW, innerH);
            bool vis = CheckVis(el);

            var childGo = BuildAny(el, parent, innerW, innerH);
            Reg(el.id, childGo);

            built.Add((childGo, childH, isExpand, vis, el.id));
            if (vis) { visCount++; if (isExpand) hasExpand = true; else fixedSum += childH; }
        }

        // expand 높이 계산
        float totalGap = visCount > 1 ? gap * (visCount - 1) : 0;
        float expandH = hasExpand ? Mathf.Max(0, innerH - fixedSum - totalGap) : 0;

        // Pass 2: 배치
        float cursor = -pT;
        int vi = 0;
        foreach (var (go, h, expand, vis, id) in built)
        {
            if (go == null || !vis) { if (go != null) go.SetActive(false); continue; }

            float finalH = expand ? expandH : h;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(innerW, finalH);
            rt.anchoredPosition = new Vector2((pL - pR) / 2f, cursor);

            cursor -= finalH;
            vi++;
            if (vi < visCount) cursor -= gap;
        }
    }

    // ====== Layout: Row ======

    private void LayoutRow(Transform parent, List<ElementDef> elements, ElementDef rowDef,
                            float containerW, float containerH, float gap, float[] pad)
    {
        float pT = pad != null && pad.Length > 0 ? pad[0] : 0;
        float pR = pad != null && pad.Length > 1 ? pad[1] : 0;
        float pB = pad != null && pad.Length > 2 ? pad[2] : 0;
        float pL = pad != null && pad.Length > 3 ? pad[3] : 0;
        float innerW = containerW - pL - pR;
        float innerH = containerH - pT - pB;

        // weights
        var items = new List<(GameObject go, float weight, bool vis)>();
        float totalWeight = 0;
        int visCount = 0;

        for (int i = 0; i < elements.Count; i++)
        {
            var el = elements[i];
            float w = 1f;
            if (ValueResolver.IsRowWeight(el.w)) w = ValueResolver.ParseRowWeight(el.w);
            else if (rowDef.weights != null && i < rowDef.weights.Length) w = rowDef.weights[i];

            bool vis = CheckVis(el);
            var go = BuildAny(el, parent, innerW, innerH);
            Reg(el.id, go);

            items.Add((go, w, vis));
            if (vis) { totalWeight += w; visCount++; }
        }

        float totalGap = visCount > 1 ? gap * (visCount - 1) : 0;
        float usableW = innerW - totalGap;
        float cursor = -innerW / 2f + pL;
        int vi = 0;

        foreach (var (go, weight, vis) in items)
        {
            if (go == null || !vis) { if (go != null) go.SetActive(false); continue; }

            float itemW = totalWeight > 0 ? usableW * (weight / totalWeight) : usableW / visCount;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(itemW, innerH);
            rt.anchoredPosition = new Vector2(cursor + itemW / 2f, (pB - pT) / 2f);

            cursor += itemW;
            vi++;
            if (vi < visCount) cursor += gap;
        }
    }

    // ====== BuildAny: 타입별 분기 ======

    private GameObject BuildAny(ElementDef el, Transform parent, float pw, float ph)
    {
        if (el == null) return null;
        try
        {
            switch (el.type)
            {
                case "column": return BuildColumn(el, parent, pw, ph);
                case "row":    return BuildRow(el, parent, pw, ph);
                case "text":   return BuildText(el, parent, pw, ph);
                case "button": return BuildButton(el, parent, pw, ph);
                case "panel":  return BuildPanel(el, parent, pw, ph);
                case "image":  return BuildImage(el, parent, pw, ph);
                case "scroll": return BuildScroll(el, parent, pw, ph);
                case "slider": return BuildSlider(el, parent, pw, ph);
                case "progressbar": return BuildProgressBar(el, parent, pw, ph);
                case "foreach": return BuildForeach(el, parent, pw, ph);
                default:
                    Debug.LogWarning($"[Builder] Unknown type: {el.type}");
                    return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Builder] {el.id ?? el.type}: {ex.Message}\n{ex.StackTrace}");
            return CreateErr(parent, el.type, ex.Message);
        }
    }

    // ---- Column ----
    private GameObject BuildColumn(ElementDef el, Transform parent, float pw, float ph)
    {
        float w = Res(el.w, pw, ph); if (w <= 0) w = pw;
        float h = Res(el.h, pw, ph);
        bool isExpand = el.h == "expand";
        if (h <= 0 && !isExpand) h = ph;
        float gap = Res(el.gap, pw, ph);
        float[] pad = ResPad(el.padding, pw, ph);

        var go = CreateRT(parent, el.id ?? "Column");
        if (!string.IsNullOrEmpty(el.bg))
        {
            var img = go.AddComponent<Image>();
            img.color = UITheme.ResolveColor(el.bg);
            img.raycastTarget = true;
        }

        if (el.children != null)
            LayoutColumn(go.transform, el.children, w, h, gap, pad);

        return go;
    }

    // ---- Row ----
    private GameObject BuildRow(ElementDef el, Transform parent, float pw, float ph)
    {
        float w = Res(el.w, pw, ph); if (w <= 0) w = pw;
        float h = Res(el.h, pw, ph); if (h <= 0) h = UIHelper.SH(0.05f);
        float gap = Res(el.gap, pw, ph);
        float[] pad = ResPad(el.padding, pw, ph);

        var go = CreateRT(parent, el.id ?? "Row");
        if (!string.IsNullOrEmpty(el.bg))
        {
            var img = go.AddComponent<Image>();
            img.color = UITheme.ResolveColor(el.bg);
            img.raycastTarget = true;
        }

        // sizeDelta는 LayoutRow 안에서 설정되지 않으므로 여기서 임시 설정
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);

        if (el.children != null)
            LayoutRow(go.transform, el.children, el, w, h, gap, pad);

        return go;
    }

    // ---- Text ----
    private GameObject BuildText(ElementDef el, Transform parent, float pw, float ph)
    {
        float w = Res(el.w, pw, ph); if (w <= 0) w = pw * 0.9f;
        float h = Res(el.h, pw, ph); if (h <= 0) h = UIHelper.SH(0.04f);

        float fs = UIHelper.Font(UITheme.Typography.Resolve(el.font ?? "Body.Medium"));
        Color col = UITheme.ResolveColor(el.color ?? "Text.Primary");
        TextAnchor align = el.align == "left" ? TextAnchor.MiddleLeft
                         : el.align == "right" ? TextAnchor.MiddleRight
                         : TextAnchor.MiddleCenter;
        FontStyle fst = el.fontStyle == "bold" ? FontStyle.Bold
                      : el.fontStyle == "italic" ? FontStyle.Italic
                      : FontStyle.Normal;
        string content = ResText(el.text);

        var go = CreateRT(parent, el.id ?? "Text");
        var text = go.AddComponent<Text>();
        text.text = content;
        text.fontSize = Mathf.RoundToInt(fs);
        text.color = col;
        text.alignment = align;
        text.fontStyle = fst;
        text.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        // Reactive
        if (el.text != null && el.text.StartsWith("$") && !el.text.StartsWith("$item."))
        {
            string key = el.text.Substring(1);
            AddReactive(key, go, text, "text");
        }

        return go;
    }

    // ---- Button ----
    private GameObject BuildButton(ElementDef el, Transform parent, float pw, float ph)
    {
        float w = Res(el.w, pw, ph); if (w <= 0) w = UIHelper.SW(0.4f);
        float h = Res(el.h, pw, ph); if (h <= 0) h = UIHelper.SH(0.04f);

        Color bg = UIHelper.ResolveButtonColor(el.buttonType ?? "confirm");
        string label = ResText(el.label ?? el.text ?? "");

        var go = CreateRT(parent, el.id ?? "Button");
        var img = go.AddComponent<Image>();
        img.color = bg;
        img.sprite = UIHelper.GetRoundedSpritePublic();
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        if (!string.IsNullOrEmpty(el.action) && _actions.TryGetValue(el.action, out var act))
            btn.onClick.AddListener(() => act());

        if (!string.IsNullOrEmpty(label))
        {
            var txtGo = CreateRT(go.transform, "Label");
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            var txt = txtGo.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = Mathf.RoundToInt(UIHelper.Font(UITheme.Typography.Body.Large));
            txt.color = UITheme.Colors.TextPrimary;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = FontStyle.Bold;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            txt.raycastTarget = false;
        }

        return go;
    }

    // ---- Panel ----
    private GameObject BuildPanel(ElementDef el, Transform parent, float pw, float ph)
    {
        float w = Res(el.w, pw, ph); if (w <= 0) w = pw;
        float h = Res(el.h, pw, ph);
        Color bg = string.IsNullOrEmpty(el.bg) ? Color.clear : UITheme.ResolveColor(el.bg);

        var go = CreateRT(parent, el.id ?? "Panel");
        if (bg.a > 0.001f)
        {
            var img = go.AddComponent<Image>();
            img.color = bg;
            img.raycastTarget = true;
            if (!string.IsNullOrEmpty(el.bg))
            {
                img.sprite = UIHelper.GetRoundedSpritePublic();
                img.type = Image.Type.Sliced;
            }
        }

        if (el.children != null)
        {
            float cw = w > 0 ? w : pw;
            float ch = h > 0 ? h : ph;
            LayoutColumn(go.transform, el.children, cw, ch, 0, null);
        }

        return go;
    }

    // ---- Image ----
    private GameObject BuildImage(ElementDef el, Transform parent, float pw, float ph)
    {
        float w = Res(el.w, pw, ph); if (w <= 0) w = UIHelper.SW(0.2f);
        float h = Res(el.h, pw, ph); if (h <= 0) h = w;

        Sprite spr = null;
        string path = ResText(el.sprite ?? "");
        if (!string.IsNullOrEmpty(path)) spr = Resources.Load<Sprite>(path);

        var go = CreateRT(parent, el.id ?? "Image");
        var img = go.AddComponent<Image>();
        img.sprite = spr;
        img.preserveAspect = true;
        img.raycastTarget = false;

        return go;
    }

    // ---- Scroll ----
    private GameObject BuildScroll(ElementDef el, Transform parent, float pw, float ph)
    {
        float w = Res(el.w, pw, ph); if (w <= 0) w = UIHelper.SW(0.9f);
        float h = Res(el.h, pw, ph); if (h <= 0) h = ph;
        bool isVert = el.direction != "horizontal";
        float gap = Res(el.gap, pw, ph);

        var go = CreateRT(parent, el.id ?? "Scroll");
        var goImg = go.AddComponent<Image>();
        goImg.color = Color.clear;
        go.AddComponent<Mask>().showMaskGraphic = false;

        var content = CreateRT(go.transform, "Content");
        var contentRt = content.GetComponent<RectTransform>();
        if (isVert)
        {
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
        }
        else
        {
            contentRt.anchorMin = new Vector2(0, 0);
            contentRt.anchorMax = new Vector2(0, 1);
            contentRt.pivot = new Vector2(0, 0.5f);
        }

        var sr = go.AddComponent<ScrollRect>();
        sr.content = contentRt;
        sr.horizontal = !isVert;
        sr.vertical = isVert;
        sr.movementType = ScrollRect.MovementType.Elastic;

        // 자식 배치
        if (el.children != null)
        {
            float cursor = 0;
            foreach (var childDef in el.children)
            {
                var childGo = BuildAny(childDef, content.transform, w, ph);
                if (childGo == null) continue;
                Reg(childDef.id, childGo);

                float childH = MeasureHeight(childDef, w, ph);
                var crt = childGo.GetComponent<RectTransform>();
                if (isVert)
                {
                    crt.anchorMin = new Vector2(0.5f, 1);
                    crt.anchorMax = new Vector2(0.5f, 1);
                    crt.pivot = new Vector2(0.5f, 1);
                    crt.sizeDelta = new Vector2(w, childH);
                    crt.anchoredPosition = new Vector2(0, -cursor);
                    cursor += childH + gap;
                }
                else
                {
                    float childW = Res(childDef.w, w, ph);
                    if (childW <= 0) childW = UIHelper.SW(0.3f);
                    crt.anchorMin = new Vector2(0, 0.5f);
                    crt.anchorMax = new Vector2(0, 0.5f);
                    crt.pivot = new Vector2(0, 0.5f);
                    crt.sizeDelta = new Vector2(childW, ph);
                    crt.anchoredPosition = new Vector2(cursor, 0);
                    cursor += childW + gap;
                }
            }
            if (isVert)
                contentRt.sizeDelta = new Vector2(0, cursor);
            else
                contentRt.sizeDelta = new Vector2(cursor, 0);
        }

        return go;
    }

    // ---- Slider ----
    private GameObject BuildSlider(ElementDef el, Transform parent, float pw, float ph)
    {
        float w = Res(el.w, pw, ph); if (w <= 0) w = UIHelper.SW(0.6f);
        float h = Res(el.h, pw, ph); if (h <= 0) h = UIHelper.SH(0.035f);

        var go = CreateRT(parent, el.id ?? "Slider");

        // Background
        var bgGo = CreateRT(go.transform, "Background");
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0, 0.25f);
        bgRt.anchorMax = new Vector2(1, 0.75f);
        bgRt.sizeDelta = Vector2.zero;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = UITheme.ResolveColor("Bg.PanelDark");

        // Fill area
        var fillArea = CreateRT(go.transform, "FillArea");
        var fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1, 0.75f);
        fillAreaRt.sizeDelta = Vector2.zero;
        fillAreaRt.offsetMin = Vector2.zero;
        fillAreaRt.offsetMax = Vector2.zero;

        var fillGo = CreateRT(fillArea.transform, "Fill");
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = UITheme.ResolveColor("Accent.Primary");

        // Handle
        var handleArea = CreateRT(go.transform, "HandleArea");
        var handleAreaRt = handleArea.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.sizeDelta = Vector2.zero;
        handleAreaRt.offsetMin = Vector2.zero;
        handleAreaRt.offsetMax = Vector2.zero;

        var handleGo = CreateRT(handleArea.transform, "Handle");
        var handleRt = handleGo.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(h, h);
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color = UITheme.Colors.TextPrimary;
        handleImg.sprite = UIHelper.GetRoundedSpritePublic();
        handleImg.type = Image.Type.Sliced;

        var slider = go.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.value = 0.5f;

        if (!string.IsNullOrEmpty(el.action) && _floatActions.TryGetValue(el.action, out var fa))
            slider.onValueChanged.AddListener((v) => fa(v));

        return go;
    }

    // ---- ProgressBar ----
    private GameObject BuildProgressBar(ElementDef el, Transform parent, float pw, float ph)
    {
        float w = Res(el.w, pw, ph); if (w <= 0) w = UIHelper.SW(0.6f);
        float h = Res(el.h, pw, ph); if (h <= 0) h = 8f;

        var go = CreateRT(parent, el.id ?? "ProgressBar");
        var bgImg = go.AddComponent<Image>();
        bgImg.color = UITheme.ResolveColor("Bg.PanelDark");

        var fill = CreateRT(go.transform, "Fill");
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0.5f, 1);
        fillRt.sizeDelta = Vector2.zero;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = UITheme.ResolveColor("Accent.Primary");

        return go;
    }

    // ---- Foreach ----
    private GameObject BuildForeach(ElementDef el, Transform parent, float pw, float ph)
    {
        string dk = el.dataSource?.TrimStart('$') ?? "";
        if (!_bindings.TryGetValue(dk, out var obj) || !(obj is List<Dictionary<string, object>> items))
            return CreateRT(parent, el.id ?? "Foreach_Empty");

        int cols = 1;
        if (!string.IsNullOrEmpty(el.cols)) int.TryParse(el.cols, out cols);
        float cellH = Res(el.cellH, pw, ph); if (cellH <= 0) cellH = UIHelper.SH(0.08f);
        float gap = Res(el.gap, pw, ph);
        float cellW = pw / Mathf.Max(1, cols);

        var go = CreateRT(parent, el.id ?? "Foreach");
        var saved = _foreachItem;
        int savedIdx = _foreachIdx;

        float cursor = 0;
        for (int i = 0; i < items.Count; i++)
        {
            _foreachItem = items[i];
            _foreachIdx = i;

            int col = i % cols;
            int row = i / cols;
            float x = (col - (cols - 1) / 2f) * cellW;
            float y = -(row * (cellH + gap));

            var cell = CreateRT(go.transform, $"Item_{i}");
            var crt = cell.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 1);
            crt.anchorMax = new Vector2(0.5f, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = new Vector2(cellW, cellH);
            crt.anchoredPosition = new Vector2(x, y);

            if (el.template != null)
                foreach (var tmpl in el.template)
                    BuildAny(tmpl, cell.transform, cellW, cellH);

            if (!string.IsNullOrEmpty(el.onItemClick) && _indexedActions.TryGetValue(el.onItemClick, out var ia))
            {
                int idx = i;
                cell.AddComponent<Image>().color = Color.clear;
                cell.AddComponent<Button>().onClick.AddListener(() => ia(idx));
            }

            cursor = (row + 1) * (cellH + gap);
        }

        _foreachItem = saved;
        _foreachIdx = savedIdx;
        return go;
    }

    // ====== Helpers ======

    private float Res(string expr, float pw, float ph) => ValueResolver.Resolve(expr, pw, ph);

    private float MeasureHeight(ElementDef el, float pw, float ph)
    {
        if (string.IsNullOrEmpty(el.h) || el.h == "auto") return UIHelper.SH(0.04f);
        if (el.h == "expand") return 0;
        float v = Res(el.h, pw, ph);
        return v > 0 ? v : UIHelper.SH(0.04f);
    }

    private Vector2 ResolvePos(ElementDef el, float pw, float ph)
    {
        float x = Res(el.x, pw, ph);
        float y = Res(el.y, pw, ph);
        return new Vector2(x, y);
    }

    private float[] ResPad(string expr, float pw, float ph)
    {
        if (string.IsNullOrEmpty(expr)) return null;
        string[] p = expr.Split(' ');
        if (p.Length == 1) { float v = Res(p[0], pw, ph); return new[] { v, v, v, v }; }
        if (p.Length == 2) { float vert = Res(p[0], pw, ph); float hor = Res(p[1], pw, ph); return new[] { vert, hor, vert, hor }; }
        if (p.Length >= 4) return new[] { Res(p[0], pw, ph), Res(p[1], pw, ph), Res(p[2], pw, ph), Res(p[3], pw, ph) };
        return null;
    }

    private string ResText(string t)
    {
        if (string.IsNullOrEmpty(t)) return "";
        if (t.StartsWith("$item.") && _foreachItem != null)
        {
            string k = t.Substring(6);
            return _foreachItem.TryGetValue(k, out var v) ? v?.ToString() ?? "" : "";
        }
        if (t.StartsWith("$"))
        {
            string k = t.Substring(1);
            return _bindings.TryGetValue(k, out var v) ? v?.ToString() ?? "" : t;
        }
        return t;
    }

    private bool CheckVis(ElementDef el)
    {
        if (string.IsNullOrEmpty(el.visible)) return true;
        string vis = el.visible;

        if (vis.StartsWith("$item.") && _foreachItem != null)
        {
            string k = vis.Substring(6);
            return _foreachItem.TryGetValue(k, out var v) && IsTrue(v);
        }
        string bindKey = vis.TrimStart('$');
        int eq = bindKey.IndexOf('=');
        if (eq > 0)
        {
            string k = bindKey.Substring(0, eq);
            string expected = bindKey.Substring(eq + 1);
            return _bindings.TryGetValue(k, out var v) && v?.ToString() == expected;
        }
        return _bindings.TryGetValue(bindKey, out var bv) && IsTrue(bv);
    }

    private void Reg(string id, GameObject go)
    {
        if (!string.IsNullOrEmpty(id) && go != null) _elements[id] = go;
    }

    private void AddReactive(string key, GameObject go, Text text, string field)
    {
        if (!_reactive.ContainsKey(key)) _reactive[key] = new List<BindTarget>();
        _reactive[key].Add(new BindTarget { go = go, text = text, field = field, key = key });
    }

    private static bool IsTrue(object v)
    {
        if (v is bool b) return b;
        if (v is string s) return s != "" && s != "0" && s.ToLower() != "false";
        if (v is int i) return i != 0;
        return v != null;
    }

    private static GameObject CreateRT(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateErr(Transform parent, string type, string msg)
    {
        var go = CreateRT(parent, $"ERR_{type}");
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 30);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.8f, 0.1f, 0.1f, 0.5f);
        var txt = go.AddComponent<Text>();
        txt.text = $"[{type}] {msg}";
        txt.fontSize = 16;
        txt.color = Color.white;
        txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        return go;
    }

    // ====== Visibility reactive ======
    // Build 후 visible 바인딩된 요소를 등록해야 하는데
    // LayoutColumn/Row에서 CheckVis 시점에 go가 이미 생성되어 있으므로
    // SetBinding 호출 시 _elements에서 찾아서 SetActive
    // → 간단하게: visible이 $key면 reactive 등록
    // BuildAny 호출 후 visible 체크 + reactive 등록
    // 현재는 LayoutColumn에서 CheckVis로 초기 상태만 설정
    // SetBinding("key", false) → _elements["id"].SetActive(false) 패턴으로 사용

    // ====== JSON Parser ======

    private static ScreenDef ParseScreen(string json)
    {
        var d = MiniJson.Deserialize(json) as Dictionary<string, object>;
        if (d == null) return null;
        var s = new ScreenDef();
        s.screen = GS(d, "screen"); s.convention = GS(d, "convention");
        s.archetype = GS(d, "archetype"); s.bg = GS(d, "bg");
        if (d.TryGetValue("elements", out var el) && el is List<object> list)
            s.elements = ParseEls(list);
        return s;
    }

    private static List<ElementDef> ParseEls(List<object> list)
    {
        var r = new List<ElementDef>();
        foreach (var item in list)
            if (item is Dictionary<string, object> d) r.Add(ParseEl(d));
        return r;
    }

    private static ElementDef ParseEl(Dictionary<string, object> d)
    {
        var e = new ElementDef();
        e.id = GS(d, "id"); e.type = GS(d, "type");
        e.x = GS(d, "x"); e.y = GS(d, "y"); e.w = GS(d, "w"); e.h = GS(d, "h");
        e.text = GS(d, "text"); e.font = GS(d, "font"); e.fontStyle = GS(d, "fontStyle");
        e.color = GS(d, "color"); e.bg = GS(d, "bg"); e.align = GS(d, "align");
        e.anchor = GS(d, "anchor"); e.visible = GS(d, "visible");
        e.action = GS(d, "action"); e.buttonType = GS(d, "buttonType");
        e.label = GS(d, "label"); e.sprite = GS(d, "sprite"); e.fit = GS(d, "fit");
        e.gap = GS(d, "gap"); e.padding = GS(d, "padding"); e.direction = GS(d, "direction");
        e.margin = GS(d, "margin"); e.dataSource = GS(d, "dataSource");
        e.layout = GS(d, "layout"); e.cellW = GS(d, "cellW"); e.cellH = GS(d, "cellH");
        e.cols = GS(d, "cols"); e.onItemClick = GS(d, "onItemClick");
        e.bestFit = GS(d, "bestFit"); e.outlineColor = GS(d, "outlineColor");
        e.outlineWidth = GS(d, "outlineWidth");
        if (d.TryGetValue("children", out var ch) && ch is List<object> cl) e.children = ParseEls(cl);
        if (d.TryGetValue("template", out var tm) && tm is List<object> tl) e.template = ParseEls(tl);
        if (d.TryGetValue("weights", out var wt) && wt is List<object> wl)
        {
            e.weights = new float[wl.Count];
            for (int i = 0; i < wl.Count; i++)
            {
                if (wl[i] is double dv) e.weights[i] = (float)dv;
                else if (wl[i] is long lv) e.weights[i] = lv;
                else float.TryParse(wl[i]?.ToString() ?? "1", out e.weights[i]);
            }
        }
        return e;
    }

    private static string GS(Dictionary<string, object> d, string k)
    {
        if (d.TryGetValue(k, out var v) && v != null) return v.ToString();
        return null;
    }
}
