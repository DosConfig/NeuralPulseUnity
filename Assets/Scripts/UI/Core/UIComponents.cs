using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 레이아웃 컨테이너: Column, Row, Scroll, Panel.
/// UIScreenBuilder에서 호출. 직접 사용도 가능.
/// </summary>
public static class UIComponents
{
    // ====== Column (Vertical Stack) ======

    /// <summary>
    /// 자식을 위에서 아래로 쌓는 컨테이너.
    /// expand 자식이 있으면 남은 공간을 할당.
    /// </summary>
    public static GameObject BuildColumn(Transform parent, float w, float h,
                                          float gap, float[] padding,
                                          List<ColumnChild> children,
                                          Color bgColor = default)
    {
        var go = new GameObject("Column", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        rt.pivot = new Vector2(0.5f, 1f);       // top-center
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);

        // 배경
        if (bgColor.a > 0.001f)
        {
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;
        }

        // 패딩: [top, right, bottom, left]
        float padTop = padding != null && padding.Length > 0 ? padding[0] : 0;
        float padRight = padding != null && padding.Length > 1 ? padding[1] : 0;
        float padBottom = padding != null && padding.Length > 2 ? padding[2] : 0;
        float padLeft = padding != null && padding.Length > 3 ? padding[3] : 0;

        float innerW = w - padLeft - padRight;
        float innerH = h - padTop - padBottom;

        // 1차: 고정 자식 높이 합산, expand 자식 찾기
        float fixedSum = 0;
        int visibleCount = 0;
        int expandIdx = -1;
        for (int i = 0; i < children.Count; i++)
        {
            if (!children[i].Visible) continue;
            visibleCount++;
            if (children[i].IsExpand)
            {
                if (expandIdx < 0) expandIdx = i;
            }
            else
            {
                fixedSum += children[i].Height;
            }
        }

        float totalGap = visibleCount > 1 ? gap * (visibleCount - 1) : 0;
        float expandH = expandIdx >= 0 ? Mathf.Max(0, innerH - fixedSum - totalGap) : 0;

        // 2차: 배치
        float cursor = -padTop; // top-down (Y 감소)
        float centerX = (padLeft - padRight) / 2f;
        int visIdx = 0;

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.Visible) continue;

            float childH = child.IsExpand ? expandH : child.Height;
            float childY = cursor - childH / 2f;

            if (child.GameObject != null)
            {
                var childRt = child.GameObject.GetComponent<RectTransform>();
                if (childRt != null)
                {
                    childRt.anchorMin = new Vector2(0.5f, 1f);
                    childRt.anchorMax = new Vector2(0.5f, 1f);
                    childRt.pivot = new Vector2(0.5f, 0.5f);
                    childRt.anchoredPosition = new Vector2(centerX, childY);

                    // 너비: 자식이 지정하지 않았으면 innerW
                    if (child.UseParentWidth)
                        childRt.sizeDelta = new Vector2(innerW, childH);
                    else
                        childRt.sizeDelta = new Vector2(childRt.sizeDelta.x, childH);
                }
            }

            cursor -= childH;
            visIdx++;
            if (visIdx < visibleCount) cursor -= gap;
        }

        return go;
    }

    public class ColumnChild
    {
        public GameObject GameObject;
        public float Height;
        public bool IsExpand;
        public bool Visible = true;
        public bool UseParentWidth = true;
    }

    // ====== Row (Horizontal Stack) ======

    /// <summary>
    /// 자식을 왼쪽에서 오른쪽으로 배치. weight 기반 너비 분배.
    /// </summary>
    public static GameObject BuildRow(Transform parent, float w, float h,
                                       float gap, float[] padding,
                                       List<RowChild> children,
                                       Color bgColor = default)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);

        if (bgColor.a > 0.001f)
        {
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;
        }

        float padTop = padding != null && padding.Length > 0 ? padding[0] : 0;
        float padRight = padding != null && padding.Length > 1 ? padding[1] : 0;
        float padBottom = padding != null && padding.Length > 2 ? padding[2] : 0;
        float padLeft = padding != null && padding.Length > 3 ? padding[3] : 0;

        float innerW = w - padLeft - padRight;
        float innerH = h - padTop - padBottom;

        // visible 자식의 weight 합산
        float totalWeight = 0;
        int visibleCount = 0;
        for (int i = 0; i < children.Count; i++)
        {
            if (!children[i].Visible) continue;
            visibleCount++;
            totalWeight += children[i].Weight;
        }

        float totalGap = visibleCount > 1 ? gap * (visibleCount - 1) : 0;
        float usableW = innerW - totalGap;

        // 배치
        float cursor = -innerW / 2f + padLeft;
        int visIdx = 0;

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.Visible) continue;

            float itemW = totalWeight > 0 ? usableW * (child.Weight / totalWeight) : usableW / visibleCount;
            float childX = cursor + itemW / 2f;
            float childY = (padBottom - padTop) / 2f;

            if (child.GameObject != null)
            {
                var childRt = child.GameObject.GetComponent<RectTransform>();
                if (childRt != null)
                {
                    childRt.anchorMin = new Vector2(0.5f, 0.5f);
                    childRt.anchorMax = new Vector2(0.5f, 0.5f);
                    childRt.pivot = new Vector2(0.5f, 0.5f);
                    childRt.anchoredPosition = new Vector2(childX, childY);
                    childRt.sizeDelta = new Vector2(itemW, child.UseParentHeight ? innerH : childRt.sizeDelta.y);
                }
            }

            cursor += itemW;
            visIdx++;
            if (visIdx < visibleCount) cursor += gap;
        }

        return go;
    }

    public class RowChild
    {
        public GameObject GameObject;
        public float Weight = 1f;
        public bool Visible = true;
        public bool UseParentHeight = true;
    }

    // ====== Scroll ======

    /// <summary>
    /// ScrollRect 래퍼. direction: "vertical" (기본), "horizontal".
    /// 반환된 go의 Content transform에 자식을 추가.
    /// </summary>
    public static ScrollContainer BuildScroll(Transform parent, float w, float h,
                                               string direction = "vertical",
                                               Color bgColor = default)
    {
        // Viewport
        var go = new GameObject("Scroll", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);

        if (bgColor.a > 0.001f)
        {
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;
        }

        // Mask
        var mask = go.AddComponent<Mask>();
        mask.showMaskGraphic = bgColor.a > 0.001f;
        if (go.GetComponent<Image>() == null)
        {
            var img = go.AddComponent<Image>();
            img.color = Color.clear;
        }

        // Content
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(go.transform, false);
        var contentRt = contentGo.GetComponent<RectTransform>();

        bool isVertical = direction != "horizontal";

        if (isVertical)
        {
            contentRt.anchorMin = new Vector2(0.5f, 1f);
            contentRt.anchorMax = new Vector2(0.5f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(w, h); // 나중에 콘텐츠 높이로 갱신
        }
        else
        {
            contentRt.anchorMin = new Vector2(0f, 0.5f);
            contentRt.anchorMax = new Vector2(0f, 0.5f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.sizeDelta = new Vector2(w, h);
        }

        // ScrollRect
        var sr = go.AddComponent<ScrollRect>();
        sr.content = contentRt;
        sr.viewport = rt;
        sr.horizontal = !isVertical;
        sr.vertical = isVertical;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.elasticity = 0.1f;
        sr.decelerationRate = 0.135f;

        return new ScrollContainer
        {
            Root = go,
            Content = contentGo,
            ContentRT = contentRt,
            ScrollRect = sr,
            IsVertical = isVertical,
            ViewportW = w,
            ViewportH = h,
        };
    }

    public class ScrollContainer
    {
        public GameObject Root;
        public GameObject Content;
        public RectTransform ContentRT;
        public ScrollRect ScrollRect;
        public bool IsVertical;
        public float ViewportW;
        public float ViewportH;

        /// <summary>콘텐츠 사이즈 확정 후 호출</summary>
        public void SetContentSize(float contentW, float contentH)
        {
            ContentRT.sizeDelta = new Vector2(contentW, contentH);
            ContentRT.anchoredPosition = Vector2.zero;
        }
    }
}
