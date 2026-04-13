using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// JSON → GameObject 빌더. SDFKit 의존성 없음.
///
/// 사용법:
///   var builder = UIScreenBuilder.Load("UI/Screens/splash_screen", parent);
///   builder.SetBinding("score", "0");
///   builder.RegisterAction("onPlay", () => StartGame());
///   var screen = builder.Build();
/// </summary>
public class UIScreenBuilder
{
    // ====== Element Definition (JSON 파싱 결과) ======
    public class ElementDef
    {
        public string id;
        public string type;       // text, button, panel, image, column, row, scroll, slider, progressbar, foreach, listview, include
        public string x, y, w, h;
        public string text;
        public string font;
        public string fontStyle;   // bold, italic, bolditalic
        public string color;
        public string bg;
        public string align;       // left, center, right
        public string anchor;      // TL, TC, TR, ML, MC, MR, BL, BC, BR
        public string visible;     // $key or "key=value"
        public string action;      // 버튼 클릭 액션 이름
        public string buttonType;  // confirm, dismiss, danger, nav, ghost, disabled
        public string label;       // 버튼 라벨
        public string sprite;      // 이미지 스프라이트 경로
        public string fit;         // stretch, contain, cover
        public string gap;
        public string padding;     // "top right bottom left" or single
        public string direction;   // scroll: vertical, horizontal
        public string margin;      // 개별 요소 외부 마진
        public string dataSource;  // foreach/listview: $바인딩키
        public string layout;      // foreach: list, grid, row
        public string cellW, cellH;
        public string cols;        // foreach grid 열 수
        public string onItemClick; // foreach/listview 아이템 클릭
        public float[] weights;    // row weights
        public string outlineColor;
        public string outlineWidth;
        public string bestFit;
        public List<ElementDef> children;
        public List<ElementDef> template;  // foreach 템플릿

        // 런타임
        internal ElementDef _runtimeParent;
    }

    // ====== Screen Definition ======
    private class ScreenDef
    {
        public string screen;
        public string convention;
        public string archetype;
        public string bg;
        public List<ElementDef> elements;
    }

    // ====== State ======
    private ScreenDef _screenDef;
    private Transform _parent;
    private Dictionary<string, object> _bindings = new Dictionary<string, object>();
    private Dictionary<string, Action> _actions = new Dictionary<string, Action>();
    private Dictionary<string, Action<int>> _indexedActions = new Dictionary<string, Action<int>>();
    private Dictionary<string, Action<float>> _floatActions = new Dictionary<string, Action<float>>();

    // Reactive targets
    private struct BindingTarget
    {
        public GameObject go;
        public Text textComp;
        public string field; // "text" or "visible"
        public string bindingKey;
    }
    private Dictionary<string, List<BindingTarget>> _reactiveTargets = new Dictionary<string, List<BindingTarget>>();

    // Element lookup
    private Dictionary<string, GameObject> _elements = new Dictionary<string, GameObject>();

    // Foreach context
    private Dictionary<string, object> _currentForeachItem;
    private int _currentForeachIndex;

    // ====== Public API ======

    /// <summary>JSON 리소스 로드. path는 Resources 상대경로 (확장자 제외).</summary>
    public static UIScreenBuilder Load(string resourcePath, Transform parent)
    {
        var builder = new UIScreenBuilder();
        builder._parent = parent;

        var textAsset = Resources.Load<TextAsset>(resourcePath);
        if (textAsset == null)
        {
            Debug.LogError($"[UIScreenBuilder] JSON not found: {resourcePath}");
            return builder;
        }

        builder._screenDef = ParseScreen(textAsset.text);
        return builder;
    }

    /// <summary>JSON 문자열에서 직접 로드.</summary>
    public static UIScreenBuilder LoadFromJson(string json, Transform parent)
    {
        var builder = new UIScreenBuilder();
        builder._parent = parent;
        builder._screenDef = ParseScreen(json);
        return builder;
    }

    /// <summary>데이터 바인딩. Build 전후 모두 가능. Build 후 호출 시 reactive 갱신.</summary>
    public void SetBinding(string key, object value)
    {
        _bindings[key] = value;

        // Reactive update
        if (_reactiveTargets.TryGetValue(key, out var targets))
        {
            foreach (var t in targets)
            {
                if (t.go == null) continue;
                switch (t.field)
                {
                    case "text":
                        if (t.textComp != null)
                            t.textComp.text = value?.ToString() ?? "";
                        break;
                    case "visible":
                        t.go.SetActive(ResolveBool(value));
                        break;
                }
            }
        }
    }

    /// <summary>버튼 액션 등록.</summary>
    public void RegisterAction(string name, Action callback)
    {
        _actions[name] = callback;
    }

    /// <summary>인덱스 기반 액션 (foreach 아이템 클릭).</summary>
    public void RegisterIndexedAction(string name, Action<int> callback)
    {
        _indexedActions[name] = callback;
    }

    /// <summary>float 액션 (슬라이더).</summary>
    public void RegisterFloatAction(string name, Action<float> callback)
    {
        _floatActions[name] = callback;
    }

    /// <summary>JSON → GameObject 트리 빌드.</summary>
    public GameObject Build()
    {
        if (_screenDef == null)
        {
            Debug.LogError("[UIScreenBuilder] No screen definition loaded");
            return new GameObject("ErrorScreen");
        }

        // Root
        var root = new GameObject(_screenDef.screen ?? "Screen", typeof(RectTransform));
        root.transform.SetParent(_parent, false);

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;
        rootRt.anchoredPosition = Vector2.zero;

        // 배경
        if (!string.IsNullOrEmpty(_screenDef.bg))
        {
            var bgImg = root.AddComponent<Image>();
            bgImg.color = UITheme.ResolveColor(_screenDef.bg);
            bgImg.raycastTarget = true;
        }

        // 요소 빌드
        if (_screenDef.elements != null)
        {
            float rootW = UIHelper.ScreenW;
            float rootH = UIHelper.CanvasH;

            foreach (var el in _screenDef.elements)
            {
                try
                {
                    var go = BuildElement(el, root.transform, rootW, rootH);
                    if (go != null && !string.IsNullOrEmpty(el.id))
                        _elements[el.id] = go;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UIScreenBuilder] Error building element '{el.id ?? el.type}': {ex.Message}");
                    CreateErrorPlaceholder(root.transform, el, ex.Message);
                }
            }
        }

        return root;
    }

    /// <summary>ID로 빌드된 요소 조회.</summary>
    public GameObject GetElement(string id)
    {
        _elements.TryGetValue(id, out var go);
        return go;
    }

    /// <summary>바인딩/액션 정리.</summary>
    public void Cleanup()
    {
        _bindings.Clear();
        _actions.Clear();
        _indexedActions.Clear();
        _floatActions.Clear();
        _reactiveTargets.Clear();
        _elements.Clear();
    }

    // ====== Element Builder ======

    private GameObject BuildElement(ElementDef el, Transform parent, float parentW, float parentH)
    {
        if (el == null) return null;

        // Visibility
        if (!string.IsNullOrEmpty(el.visible))
        {
            bool vis = ResolveVisibility(el.visible);
            // 바인딩 키면 나중에 토글 가능하도록 빌드는 하되 SetActive
            var built = BuildElementInner(el, parent, parentW, parentH);
            if (built != null)
            {
                built.SetActive(vis);
                RegisterVisibilityBinding(el.visible, built);
            }
            return built;
        }

        return BuildElementInner(el, parent, parentW, parentH);
    }

    private GameObject BuildElementInner(ElementDef el, Transform parent, float parentW, float parentH)
    {
        switch (el.type)
        {
            case "text":        return BuildText(el, parent, parentW, parentH);
            case "button":      return BuildButton(el, parent, parentW, parentH);
            case "panel":       return BuildPanel(el, parent, parentW, parentH);
            case "image":       return BuildImage(el, parent, parentW, parentH);
            case "column":      return BuildColumnElement(el, parent, parentW, parentH);
            case "row":         return BuildRowElement(el, parent, parentW, parentH);
            case "scroll":      return BuildScrollElement(el, parent, parentW, parentH);
            case "slider":      return BuildSlider(el, parent, parentW, parentH);
            case "progressbar": return BuildProgressBar(el, parent, parentW, parentH);
            case "foreach":     return BuildForeach(el, parent, parentW, parentH);
            default:
                Debug.LogWarning($"[UIScreenBuilder] Unknown type: {el.type}");
                return null;
        }
    }

    // ---- Text ----
    private GameObject BuildText(ElementDef el, Transform parent, float parentW, float parentH)
    {
        float w = ResolveSize(el.w, parentW, parentH, parentW, 0);
        float h = ResolveSize(el.h, parentW, parentH, 0, parentH);
        if (w <= 0) w = parentW * 0.9f;
        if (h <= 0) h = UIHelper.SH(0.04f);

        float fontSize = UIHelper.Font(UITheme.Typography.Resolve(el.font ?? "Body.Medium"));
        Color color = UITheme.ResolveColor(el.color ?? "Text.Primary");

        TextAnchor alignment = TextAnchor.MiddleCenter;
        if (el.align == "left") alignment = TextAnchor.MiddleLeft;
        else if (el.align == "right") alignment = TextAnchor.MiddleRight;

        FontStyle fs = FontStyle.Normal;
        if (el.fontStyle == "bold") fs = FontStyle.Bold;
        else if (el.fontStyle == "italic") fs = FontStyle.Italic;
        else if (el.fontStyle == "bolditalic") fs = FontStyle.BoldAndItalic;

        string content = ResolveText(el.text);

        var go = UIHelper.CreateText(parent, content, fontSize, color,
                                      ResolvePosition(el, parentW, parentH),
                                      new Vector2(w, h), alignment, fs);
        go.name = el.id ?? "Text";

        // Anchor preset
        if (!string.IsNullOrEmpty(el.anchor))
            UIHelper.ApplyAnchorPreset(go.GetComponent<RectTransform>(), el.anchor);

        // Reactive text binding
        RegisterTextBinding(el.text, go);

        return go;
    }

    // ---- Button ----
    private GameObject BuildButton(ElementDef el, Transform parent, float parentW, float parentH)
    {
        float w = ResolveSize(el.w, parentW, parentH, parentW, 0);
        float h = ResolveSize(el.h, parentW, parentH, 0, parentH);
        if (w <= 0) w = UIHelper.SW(0.4f);
        if (h <= 0) h = UIHelper.SH(UITheme.Layout.MinButtonH);

        string label = el.label ?? el.text ?? "";
        Color bg = UIHelper.ResolveButtonColor(el.buttonType ?? "confirm");

        Action onClick = null;
        if (!string.IsNullOrEmpty(el.action) && _actions.TryGetValue(el.action, out var act))
            onClick = act;

        var go = UIHelper.CreateButton(parent, ResolveText(label),
                                        ResolvePosition(el, parentW, parentH),
                                        new Vector2(w, h), bg, onClick);
        go.name = el.id ?? "Button";

        if (!string.IsNullOrEmpty(el.anchor))
            UIHelper.ApplyAnchorPreset(go.GetComponent<RectTransform>(), el.anchor);

        return go;
    }

    // ---- Panel ----
    private GameObject BuildPanel(ElementDef el, Transform parent, float parentW, float parentH)
    {
        float w = ResolveSize(el.w, parentW, parentH, parentW, 0);
        float h = ResolveSize(el.h, parentW, parentH, 0, parentH);
        if (w <= 0) w = parentW;
        bool isExpand = el.h == "expand";

        Color bgColor = string.IsNullOrEmpty(el.bg) ? Color.clear : UITheme.ResolveColor(el.bg);

        var go = UIHelper.CreatePanel(parent, ResolvePosition(el, parentW, parentH),
                                       new Vector2(w, isExpand ? 0 : h), bgColor);
        go.name = el.id ?? "Panel";

        if (!string.IsNullOrEmpty(el.anchor))
            UIHelper.ApplyAnchorPreset(go.GetComponent<RectTransform>(), el.anchor);

        // Children
        if (el.children != null)
        {
            foreach (var child in el.children)
            {
                var childGo = BuildElement(child, go.transform, w, h);
                if (childGo != null && !string.IsNullOrEmpty(child.id))
                    _elements[child.id] = childGo;
            }
        }

        return go;
    }

    // ---- Image ----
    private GameObject BuildImage(ElementDef el, Transform parent, float parentW, float parentH)
    {
        float w = ResolveSize(el.w, parentW, parentH, parentW, 0);
        float h = ResolveSize(el.h, parentW, parentH, 0, parentH);
        if (w <= 0) w = UIHelper.SW(0.2f);
        if (h <= 0) h = w;

        Sprite sprite = null;
        string spritePath = ResolveText(el.sprite ?? "");
        if (!string.IsNullOrEmpty(spritePath))
            sprite = Resources.Load<Sprite>(spritePath);

        var go = UIHelper.CreateImage(parent, sprite,
                                       ResolvePosition(el, parentW, parentH),
                                       new Vector2(w, h));
        go.name = el.id ?? "Image";

        // Fit mode
        if (el.fit == "contain" || el.fit == "cover")
        {
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = el.fit == "contain"
                ? AspectRatioFitter.AspectMode.FitInParent
                : AspectRatioFitter.AspectMode.EnvelopeParent;
            if (sprite != null && sprite.rect.height > 0)
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        if (!string.IsNullOrEmpty(el.anchor))
            UIHelper.ApplyAnchorPreset(go.GetComponent<RectTransform>(), el.anchor);

        return go;
    }

    // ---- Column ----
    private GameObject BuildColumnElement(ElementDef el, Transform parent, float parentW, float parentH)
    {
        float w = ResolveSize(el.w, parentW, parentH, parentW, 0);
        float h = ResolveSize(el.h, parentW, parentH, 0, parentH);
        if (w <= 0) w = parentW;
        bool isExpand = el.h == "expand";
        if (h <= 0 && !isExpand) h = parentH;

        float gap = ResolveSize(el.gap, parentW, parentH, 0, 0);
        float[] padding = ResolvePadding(el.padding, parentW, parentH);
        Color bgColor = string.IsNullOrEmpty(el.bg) ? default : UITheme.ResolveColor(el.bg);

        var columnChildren = new List<UIComponents.ColumnChild>();

        if (el.children != null)
        {
            float innerW = w - (padding != null ? padding[1] + padding[3] : 0);
            float innerH = h - (padding != null ? padding[0] + padding[2] : 0);

            foreach (var childDef in el.children)
            {
                float childH = ResolveSize(childDef.h, innerW, innerH, 0, innerH);
                bool childExpand = childDef.h == "expand";
                bool childVisible = string.IsNullOrEmpty(childDef.visible) || ResolveVisibility(childDef.visible);

                var childGo = BuildElement(childDef, null, innerW, innerH); // parent=null, 나중에 Column이 배치
                if (childGo != null && !string.IsNullOrEmpty(childDef.id))
                    _elements[childDef.id] = childGo;

                columnChildren.Add(new UIComponents.ColumnChild
                {
                    GameObject = childGo,
                    Height = childExpand ? 0 : (childH > 0 ? childH : UIHelper.SH(0.05f)),
                    IsExpand = childExpand,
                    Visible = childVisible,
                });
            }
        }

        var go = UIComponents.BuildColumn(parent, w, h, gap, padding, columnChildren, bgColor);
        go.name = el.id ?? "Column";

        // Reparent children
        foreach (var cc in columnChildren)
        {
            if (cc.GameObject != null)
                cc.GameObject.transform.SetParent(go.transform, false);
        }
        // Rebuild positions (BuildColumn already did this but children were orphaned)
        // Re-run column layout
        ReapplyColumnLayout(go, w, h, gap, padding, columnChildren);

        if (!string.IsNullOrEmpty(el.anchor))
            UIHelper.ApplyAnchorPreset(go.GetComponent<RectTransform>(), el.anchor);

        return go;
    }

    private void ReapplyColumnLayout(GameObject columnGo, float w, float h,
                                      float gap, float[] padding,
                                      List<UIComponents.ColumnChild> children)
    {
        float padTop = padding != null && padding.Length > 0 ? padding[0] : 0;
        float padRight = padding != null && padding.Length > 1 ? padding[1] : 0;
        float padBottom = padding != null && padding.Length > 2 ? padding[2] : 0;
        float padLeft = padding != null && padding.Length > 3 ? padding[3] : 0;

        float innerW = w - padLeft - padRight;
        float innerH = h - padTop - padBottom;

        float fixedSum = 0;
        int visCount = 0;
        int expandIdx = -1;
        for (int i = 0; i < children.Count; i++)
        {
            if (!children[i].Visible) continue;
            visCount++;
            if (children[i].IsExpand && expandIdx < 0) expandIdx = i;
            else fixedSum += children[i].Height;
        }
        float totalGap = visCount > 1 ? gap * (visCount - 1) : 0;
        float expandH = expandIdx >= 0 ? Mathf.Max(0, innerH - fixedSum - totalGap) : 0;

        float cursor = -padTop;
        float centerX = (padLeft - padRight) / 2f;
        int visIdx = 0;

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.Visible || child.GameObject == null) continue;

            float childH = child.IsExpand ? expandH : child.Height;
            float childY = cursor - childH / 2f;

            var crt = child.GameObject.GetComponent<RectTransform>();
            if (crt != null)
            {
                crt.anchorMin = new Vector2(0.5f, 1f);
                crt.anchorMax = new Vector2(0.5f, 1f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = new Vector2(centerX, childY);
                crt.sizeDelta = new Vector2(
                    child.UseParentWidth ? innerW : crt.sizeDelta.x,
                    childH);
            }

            cursor -= childH;
            visIdx++;
            if (visIdx < visCount) cursor -= gap;
        }
    }

    // ---- Row ----
    private GameObject BuildRowElement(ElementDef el, Transform parent, float parentW, float parentH)
    {
        float w = ResolveSize(el.w, parentW, parentH, parentW, 0);
        float h = ResolveSize(el.h, parentW, parentH, 0, parentH);
        if (w <= 0) w = parentW;
        if (h <= 0) h = UIHelper.SH(0.05f);

        float gap = ResolveSize(el.gap, parentW, parentH, 0, 0);
        float[] padding = ResolvePadding(el.padding, parentW, parentH);
        Color bgColor = string.IsNullOrEmpty(el.bg) ? default : UITheme.ResolveColor(el.bg);

        var rowChildren = new List<UIComponents.RowChild>();

        if (el.children != null)
        {
            float innerW = w - (padding != null ? padding[1] + padding[3] : 0);
            float innerH = h - (padding != null ? padding[0] + padding[2] : 0);

            for (int i = 0; i < el.children.Count; i++)
            {
                var childDef = el.children[i];
                float weight = 1f;

                // rw: prefix 또는 weights 배열
                if (ValueResolver.IsRowWeight(childDef.w))
                    weight = ValueResolver.ParseRowWeight(childDef.w);
                else if (el.weights != null && i < el.weights.Length)
                    weight = el.weights[i];

                bool childVisible = string.IsNullOrEmpty(childDef.visible) || ResolveVisibility(childDef.visible);

                var childGo = BuildElement(childDef, null, innerW, innerH);
                if (childGo != null && !string.IsNullOrEmpty(childDef.id))
                    _elements[childDef.id] = childGo;

                rowChildren.Add(new UIComponents.RowChild
                {
                    GameObject = childGo,
                    Weight = weight,
                    Visible = childVisible,
                });
            }
        }

        var go = UIComponents.BuildRow(parent, w, h, gap, padding, rowChildren, bgColor);
        go.name = el.id ?? "Row";

        // Reparent
        foreach (var rc in rowChildren)
        {
            if (rc.GameObject != null)
                rc.GameObject.transform.SetParent(go.transform, false);
        }

        // Reapply row layout
        ReapplyRowLayout(go, w, h, gap, padding, rowChildren);

        if (!string.IsNullOrEmpty(el.anchor))
            UIHelper.ApplyAnchorPreset(go.GetComponent<RectTransform>(), el.anchor);

        return go;
    }

    private void ReapplyRowLayout(GameObject rowGo, float w, float h,
                                   float gap, float[] padding,
                                   List<UIComponents.RowChild> children)
    {
        float padTop = padding != null && padding.Length > 0 ? padding[0] : 0;
        float padRight = padding != null && padding.Length > 1 ? padding[1] : 0;
        float padBottom = padding != null && padding.Length > 2 ? padding[2] : 0;
        float padLeft = padding != null && padding.Length > 3 ? padding[3] : 0;

        float innerW = w - padLeft - padRight;
        float innerH = h - padTop - padBottom;

        float totalWeight = 0;
        int visCount = 0;
        foreach (var c in children) { if (c.Visible) { totalWeight += c.Weight; visCount++; } }

        float totalGap = visCount > 1 ? gap * (visCount - 1) : 0;
        float usableW = innerW - totalGap;
        float cursor = -innerW / 2f + padLeft;
        int visIdx = 0;

        foreach (var child in children)
        {
            if (!child.Visible || child.GameObject == null) continue;

            float itemW = totalWeight > 0 ? usableW * (child.Weight / totalWeight) : usableW / visCount;
            float childX = cursor + itemW / 2f;

            var crt = child.GameObject.GetComponent<RectTransform>();
            if (crt != null)
            {
                crt.anchorMin = new Vector2(0.5f, 0.5f);
                crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = new Vector2(childX, (padBottom - padTop) / 2f);
                crt.sizeDelta = new Vector2(itemW, child.UseParentHeight ? innerH : crt.sizeDelta.y);
            }

            cursor += itemW;
            visIdx++;
            if (visIdx < visCount) cursor += gap;
        }
    }

    // ---- Scroll ----
    private GameObject BuildScrollElement(ElementDef el, Transform parent, float parentW, float parentH)
    {
        float w = ResolveSize(el.w, parentW, parentH, parentW, 0);
        float h = ResolveSize(el.h, parentW, parentH, 0, parentH);
        if (w <= 0) w = UIHelper.SW(0.9f);
        if (h <= 0) h = UIHelper.SH(0.5f);

        string dir = el.direction ?? "vertical";
        Color bgColor = string.IsNullOrEmpty(el.bg) ? default : UITheme.ResolveColor(el.bg);

        var scroll = UIComponents.BuildScroll(parent, w, h, dir, bgColor);
        scroll.Root.name = el.id ?? "Scroll";

        // Children → Content
        float contentCursor = 0;
        float gap = ResolveSize(el.gap, w, h, 0, 0);

        if (el.children != null)
        {
            bool isVert = dir != "horizontal";
            int childCount = 0;

            foreach (var childDef in el.children)
            {
                var childGo = BuildElement(childDef, scroll.Content.transform, w, h);
                if (childGo == null) continue;
                if (!string.IsNullOrEmpty(childDef.id))
                    _elements[childDef.id] = childGo;

                var crt = childGo.GetComponent<RectTransform>();
                if (crt == null) continue;

                if (isVert)
                {
                    float childH = crt.sizeDelta.y;
                    crt.anchorMin = new Vector2(0.5f, 1f);
                    crt.anchorMax = new Vector2(0.5f, 1f);
                    crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.anchoredPosition = new Vector2(0, -(contentCursor + childH / 2f));
                    contentCursor += childH + gap;
                }
                else
                {
                    float childW = crt.sizeDelta.x;
                    crt.anchorMin = new Vector2(0f, 0.5f);
                    crt.anchorMax = new Vector2(0f, 0.5f);
                    crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.anchoredPosition = new Vector2(contentCursor + childW / 2f, 0);
                    contentCursor += childW + gap;
                }
                childCount++;
            }

            // 마지막 gap 제거
            if (childCount > 0) contentCursor -= gap;

            // Content 사이즈 설정
            if (dir != "horizontal")
                scroll.SetContentSize(w, contentCursor);
            else
                scroll.SetContentSize(contentCursor, h);
        }

        if (!string.IsNullOrEmpty(el.anchor))
            UIHelper.ApplyAnchorPreset(scroll.Root.GetComponent<RectTransform>(), el.anchor);

        return scroll.Root;
    }

    // ---- Slider ----
    private GameObject BuildSlider(ElementDef el, Transform parent, float parentW, float parentH)
    {
        float w = ResolveSize(el.w, parentW, parentH, parentW, 0);
        float h = ResolveSize(el.h, parentW, parentH, 0, parentH);
        if (w <= 0) w = UIHelper.SW(0.6f);
        if (h <= 0) h = UIHelper.SH(0.03f);

        var go = new GameObject(el.id ?? "Slider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = ResolvePosition(el, parentW, parentH);
        rt.sizeDelta = new Vector2(w, h);

        // Track background
        var bgGo = UIHelper.CreatePanel(go.transform, Vector2.zero, new Vector2(w, h * 0.4f),
                                         UITheme.ResolveColor("Bg.PanelDark"));
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0, 0.5f);
        bgRt.anchorMax = new Vector2(1, 0.5f);
        bgRt.sizeDelta = new Vector2(0, h * 0.4f);
        bgRt.anchoredPosition = Vector2.zero;

        // Fill
        var fillGo = UIHelper.CreatePanel(go.transform, Vector2.zero, Vector2.zero,
                                           UITheme.ResolveColor("Accent.Primary"));
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0, 0.3f);
        fillRt.anchorMax = new Vector2(0.5f, 0.7f);
        fillRt.sizeDelta = Vector2.zero;
        fillRt.anchoredPosition = Vector2.zero;

        // Handle
        var handleGo = UIHelper.CreatePanel(go.transform, Vector2.zero, new Vector2(h, h),
                                             UITheme.ResolveColor("Text.Primary"), h / 2f);
        var handleRt = handleGo.GetComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0, 0.5f);
        handleRt.anchorMax = new Vector2(0, 0.5f);

        // Slider component
        var slider = go.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.5f;

        if (!string.IsNullOrEmpty(el.action) && _floatActions.TryGetValue(el.action, out var floatAct))
            slider.onValueChanged.AddListener((v) => floatAct(v));

        if (!string.IsNullOrEmpty(el.anchor))
            UIHelper.ApplyAnchorPreset(rt, el.anchor);

        return go;
    }

    // ---- ProgressBar ----
    private GameObject BuildProgressBar(ElementDef el, Transform parent, float parentW, float parentH)
    {
        float w = ResolveSize(el.w, parentW, parentH, parentW, 0);
        float h = ResolveSize(el.h, parentW, parentH, 0, parentH);
        if (w <= 0) w = UIHelper.SW(0.6f);
        if (h <= 0) h = 8f;

        var go = UIHelper.CreatePanel(parent, ResolvePosition(el, parentW, parentH),
                                       new Vector2(w, h), UITheme.ResolveColor("Bg.PanelDark"));
        go.name = el.id ?? "ProgressBar";

        // Fill
        var fill = UIHelper.CreatePanel(go.transform, Vector2.zero, Vector2.zero,
                                         UITheme.ResolveColor("Accent.Primary"));
        fill.name = "Fill";
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0.5f, 1f); // 50% 기본
        fillRt.sizeDelta = Vector2.zero;
        fillRt.anchoredPosition = Vector2.zero;

        if (!string.IsNullOrEmpty(el.anchor))
            UIHelper.ApplyAnchorPreset(go.GetComponent<RectTransform>(), el.anchor);

        return go;
    }

    // ---- Foreach ----
    private GameObject BuildForeach(ElementDef el, Transform parent, float parentW, float parentH)
    {
        string dataKey = el.dataSource?.TrimStart('$') ?? "";
        if (!_bindings.TryGetValue(dataKey, out var dataObj) || !(dataObj is List<Dictionary<string, object>> items))
        {
            Debug.LogWarning($"[UIScreenBuilder] Foreach dataSource '{dataKey}' not found or wrong type");
            return new GameObject(el.id ?? "Foreach_Empty");
        }

        string layout = el.layout ?? "list";
        int cols = 1;
        if (!string.IsNullOrEmpty(el.cols)) int.TryParse(el.cols, out cols);
        if (cols < 1) cols = 1;

        float cellW = ResolveSize(el.cellW, parentW, parentH, parentW, 0);
        float cellH = ResolveSize(el.cellH, parentW, parentH, 0, parentH);
        float gap = ResolveSize(el.gap, parentW, parentH, 0, 0);

        if (cellW <= 0) cellW = parentW / cols;
        if (cellH <= 0) cellH = UIHelper.SH(0.08f);

        // Container (scroll)
        float totalH = layout == "row"
            ? cellH
            : Mathf.Ceil((float)items.Count / cols) * (cellH + gap) - gap;

        var scroll = UIComponents.BuildScroll(parent, parentW, Mathf.Min(totalH, parentH),
                                               layout == "row" ? "horizontal" : "vertical");
        scroll.Root.name = el.id ?? "Foreach";

        // Items
        var savedItem = _currentForeachItem;
        int savedIndex = _currentForeachIndex;

        for (int i = 0; i < items.Count; i++)
        {
            _currentForeachItem = items[i];
            _currentForeachIndex = i;

            // Position
            float x, y;
            if (layout == "row")
            {
                x = i * (cellW + gap) + cellW / 2f;
                y = 0;
            }
            else
            {
                int col = i % cols;
                int row = i / cols;
                x = (col - (cols - 1) / 2f) * (cellW + gap);
                y = -(row * (cellH + gap) + cellH / 2f);
            }

            // Cell container
            var cellGo = new GameObject($"Item_{i}", typeof(RectTransform));
            cellGo.transform.SetParent(scroll.Content.transform, false);
            var cellRt = cellGo.GetComponent<RectTransform>();
            cellRt.sizeDelta = new Vector2(cellW, cellH);

            if (layout == "row")
            {
                cellRt.anchorMin = new Vector2(0, 0.5f);
                cellRt.anchorMax = new Vector2(0, 0.5f);
                cellRt.pivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                cellRt.anchorMin = new Vector2(0.5f, 1f);
                cellRt.anchorMax = new Vector2(0.5f, 1f);
                cellRt.pivot = new Vector2(0.5f, 0.5f);
            }
            cellRt.anchoredPosition = new Vector2(x, y);

            // Template elements
            if (el.template != null)
            {
                foreach (var tmpl in el.template)
                {
                    BuildElement(tmpl, cellGo.transform, cellW, cellH);
                }
            }

            // Item click
            if (!string.IsNullOrEmpty(el.onItemClick))
            {
                int capturedIdx = i;
                var btn = cellGo.AddComponent<Button>();
                var img = cellGo.AddComponent<Image>();
                img.color = Color.clear;
                if (_indexedActions.TryGetValue(el.onItemClick, out var idxAct))
                    btn.onClick.AddListener(() => idxAct(capturedIdx));
            }
        }

        _currentForeachItem = savedItem;
        _currentForeachIndex = savedIndex;

        // Content size
        if (layout == "row")
            scroll.SetContentSize(items.Count * (cellW + gap) - gap, cellH);
        else
            scroll.SetContentSize(parentW, totalH);

        if (!string.IsNullOrEmpty(el.anchor))
            UIHelper.ApplyAnchorPreset(scroll.Root.GetComponent<RectTransform>(), el.anchor);

        return scroll.Root;
    }

    // ====== Resolve Helpers ======

    private float ResolveSize(string expr, float parentW, float parentH, float defaultW, float defaultH)
    {
        if (string.IsNullOrEmpty(expr)) return 0;
        if (expr == "expand") return -2f; // sentinel
        return ValueResolver.Resolve(expr, parentW, parentH);
    }

    private Vector2 ResolvePosition(ElementDef el, float parentW, float parentH)
    {
        float x = !string.IsNullOrEmpty(el.x) ? ValueResolver.Resolve(el.x, parentW, parentH) : 0;
        float y = !string.IsNullOrEmpty(el.y) ? ValueResolver.Resolve(el.y, parentW, parentH) : 0;
        return new Vector2(x, y);
    }

    private float[] ResolvePadding(string paddingExpr, float parentW, float parentH)
    {
        if (string.IsNullOrEmpty(paddingExpr)) return new float[] { 0, 0, 0, 0 };

        string[] parts = paddingExpr.Split(' ');
        if (parts.Length == 1)
        {
            float v = ValueResolver.Resolve(parts[0], parentW, parentH);
            return new float[] { v, v, v, v };
        }
        if (parts.Length == 2)
        {
            float vertical = ValueResolver.Resolve(parts[0], parentW, parentH);
            float horizontal = ValueResolver.Resolve(parts[1], parentW, parentH);
            return new float[] { vertical, horizontal, vertical, horizontal };
        }
        if (parts.Length == 4)
        {
            return new float[]
            {
                ValueResolver.Resolve(parts[0], parentW, parentH),
                ValueResolver.Resolve(parts[1], parentW, parentH),
                ValueResolver.Resolve(parts[2], parentW, parentH),
                ValueResolver.Resolve(parts[3], parentW, parentH),
            };
        }
        return new float[] { 0, 0, 0, 0 };
    }

    private string ResolveText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // $item.property (foreach context)
        if (text.StartsWith("$item.") && _currentForeachItem != null)
        {
            string prop = text.Substring(6);
            if (_currentForeachItem.TryGetValue(prop, out var val))
                return val?.ToString() ?? "";
            return "";
        }

        // $key (binding)
        if (text.StartsWith("$"))
        {
            string key = text.Substring(1);
            if (_bindings.TryGetValue(key, out var val))
                return val?.ToString() ?? "";
            return text; // 아직 바인딩 안 됐으면 원본 유지
        }

        return text;
    }

    private bool ResolveVisibility(string visExpr)
    {
        if (string.IsNullOrEmpty(visExpr)) return true;

        // $item.property (foreach)
        if (visExpr.StartsWith("$item.") && _currentForeachItem != null)
        {
            string prop = visExpr.Substring(6);
            // "=$item.prop=value" 형태
            int eqIdx = prop.IndexOf('=');
            if (eqIdx > 0)
            {
                string key = prop.Substring(0, eqIdx);
                string expected = prop.Substring(eqIdx + 1);
                if (_currentForeachItem.TryGetValue(key, out var val))
                    return val?.ToString() == expected;
                return false;
            }
            if (_currentForeachItem.TryGetValue(prop, out var boolVal))
                return ResolveBool(boolVal);
            return false;
        }

        // "key=value" 동등 비교
        int eqIdx2 = visExpr.IndexOf('=');
        if (eqIdx2 > 0 && !visExpr.StartsWith("$"))
        {
            string key = visExpr.Substring(0, eqIdx2);
            string expected = visExpr.Substring(eqIdx2 + 1);
            if (_bindings.TryGetValue(key, out var val))
                return val?.ToString() == expected;
            return false;
        }

        // $boolKey
        string bindKey = visExpr.TrimStart('$');
        if (_bindings.TryGetValue(bindKey, out var binding))
            return ResolveBool(binding);

        return true;
    }

    private static bool ResolveBool(object val)
    {
        if (val is bool b) return b;
        if (val is string s) return s != "" && s != "0" && s.ToLower() != "false";
        if (val is int i) return i != 0;
        if (val is float f) return f != 0f;
        return val != null;
    }

    // ====== Reactive Binding Registration ======

    private void RegisterTextBinding(string textExpr, GameObject go)
    {
        if (string.IsNullOrEmpty(textExpr) || !textExpr.StartsWith("$") || textExpr.StartsWith("$item."))
            return;

        string key = textExpr.Substring(1);
        var text = go.GetComponent<Text>();
        if (text == null) return;

        if (!_reactiveTargets.ContainsKey(key))
            _reactiveTargets[key] = new List<BindingTarget>();

        _reactiveTargets[key].Add(new BindingTarget
        {
            go = go,
            textComp = text,
            field = "text",
            bindingKey = key,
        });
    }

    private void RegisterVisibilityBinding(string visExpr, GameObject go)
    {
        if (string.IsNullOrEmpty(visExpr)) return;
        string key = visExpr.TrimStart('$');
        if (key.StartsWith("item.") || key.Contains("=")) return;

        if (!_reactiveTargets.ContainsKey(key))
            _reactiveTargets[key] = new List<BindingTarget>();

        _reactiveTargets[key].Add(new BindingTarget
        {
            go = go,
            field = "visible",
            bindingKey = key,
        });
    }

    // ====== Error Placeholder ======

    private void CreateErrorPlaceholder(Transform parent, ElementDef el, string error)
    {
        var go = UIHelper.CreatePanel(parent, Vector2.zero,
                                       new Vector2(UIHelper.SW(0.8f), UIHelper.SH(0.03f)),
                                       new Color(0.8f, 0.1f, 0.1f, 0.5f));
        go.name = $"ERROR_{el.id ?? el.type}";
        UIHelper.CreateText(go.transform, $"[{el.type}] {error}",
                            UIHelper.Font(0.015f), Color.white,
                            Vector2.zero, new Vector2(UIHelper.SW(0.75f), UIHelper.SH(0.025f)));
    }

    // ====== JSON Parser (Minimal, no external dependency) ======

    private static ScreenDef ParseScreen(string json)
    {
        var dict = MiniJson.Deserialize(json) as Dictionary<string, object>;
        if (dict == null) return null;

        var screen = new ScreenDef();
        screen.screen = GetStr(dict, "screen");
        screen.convention = GetStr(dict, "convention");
        screen.archetype = GetStr(dict, "archetype");
        screen.bg = GetStr(dict, "bg");

        if (dict.TryGetValue("elements", out var elObj) && elObj is List<object> elList)
            screen.elements = ParseElements(elList);

        return screen;
    }

    private static List<ElementDef> ParseElements(List<object> list)
    {
        var result = new List<ElementDef>();
        foreach (var item in list)
        {
            if (item is Dictionary<string, object> d)
                result.Add(ParseElementDef(d));
        }
        return result;
    }

    private static ElementDef ParseElementDef(Dictionary<string, object> d)
    {
        var el = new ElementDef();
        el.id = GetStr(d, "id");
        el.type = GetStr(d, "type");
        el.x = GetStr(d, "x");
        el.y = GetStr(d, "y");
        el.w = GetStr(d, "w");
        el.h = GetStr(d, "h");
        el.text = GetStr(d, "text");
        el.font = GetStr(d, "font");
        el.fontStyle = GetStr(d, "fontStyle");
        el.color = GetStr(d, "color");
        el.bg = GetStr(d, "bg");
        el.align = GetStr(d, "align");
        el.anchor = GetStr(d, "anchor");
        el.visible = GetStr(d, "visible");
        el.action = GetStr(d, "action");
        el.buttonType = GetStr(d, "buttonType");
        el.label = GetStr(d, "label");
        el.sprite = GetStr(d, "sprite");
        el.fit = GetStr(d, "fit");
        el.gap = GetStr(d, "gap");
        el.padding = GetStr(d, "padding");
        el.direction = GetStr(d, "direction");
        el.margin = GetStr(d, "margin");
        el.dataSource = GetStr(d, "dataSource");
        el.layout = GetStr(d, "layout");
        el.cellW = GetStr(d, "cellW");
        el.cellH = GetStr(d, "cellH");
        el.cols = GetStr(d, "cols");
        el.onItemClick = GetStr(d, "onItemClick");
        el.bestFit = GetStr(d, "bestFit");
        el.outlineColor = GetStr(d, "outlineColor");
        el.outlineWidth = GetStr(d, "outlineWidth");

        if (d.TryGetValue("children", out var ch) && ch is List<object> chList)
            el.children = ParseElements(chList);
        if (d.TryGetValue("template", out var tmpl) && tmpl is List<object> tmplList)
            el.template = ParseElements(tmplList);
        if (d.TryGetValue("weights", out var wObj) && wObj is List<object> wList)
        {
            el.weights = new float[wList.Count];
            for (int i = 0; i < wList.Count; i++)
            {
                if (wList[i] is double dv) el.weights[i] = (float)dv;
                else if (wList[i] is long lv) el.weights[i] = lv;
                else float.TryParse(wList[i]?.ToString() ?? "1", out el.weights[i]);
            }
        }

        return el;
    }

    private static string GetStr(Dictionary<string, object> d, string key)
    {
        if (d.TryGetValue(key, out var v) && v != null) return v.ToString();
        return null;
    }
}
