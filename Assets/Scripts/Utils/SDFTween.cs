using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 코루틴 기반 트윈 라이브러리. DOTween 의존성 없음.
/// 사용법: StartCoroutine(SDFTween.Scale(rt, from, to, 0.3f, Easing.OutBack));
/// </summary>
public static class SDFTween
{
    // ====== Transform Tweens ======

    public static IEnumerator Scale(RectTransform rt, Vector3 from, Vector3 to,
                                     float duration, Easing.EaseFunc ease = null)
    {
        if (rt == null) yield break;
        ease = ease ?? Easing.OutQuad;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = ease(Mathf.Clamp01(elapsed / duration));
            rt.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }
        rt.localScale = to;
    }

    public static IEnumerator Move(RectTransform rt, Vector2 from, Vector2 to,
                                    float duration, Easing.EaseFunc ease = null)
    {
        if (rt == null) yield break;
        ease = ease ?? Easing.OutQuad;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = ease(Mathf.Clamp01(elapsed / duration));
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    public static IEnumerator RotateZ(RectTransform rt, float fromDeg, float toDeg,
                                       float duration, Easing.EaseFunc ease = null)
    {
        if (rt == null) yield break;
        ease = ease ?? Easing.OutQuad;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = ease(Mathf.Clamp01(elapsed / duration));
            float deg = Mathf.LerpUnclamped(fromDeg, toDeg, t);
            rt.localRotation = Quaternion.Euler(0, 0, deg);
            yield return null;
        }
        rt.localRotation = Quaternion.Euler(0, 0, toDeg);
    }

    // ====== Fade ======

    public static IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to,
                                               float duration, Easing.EaseFunc ease = null)
    {
        if (cg == null) yield break;
        ease = ease ?? Easing.Linear;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = ease(Mathf.Clamp01(elapsed / duration));
            cg.alpha = Mathf.LerpUnclamped(from, to, t);
            yield return null;
        }
        cg.alpha = to;
    }

    /// <summary>
    /// Interval 페이드 — 전체 duration 중 intervalStart~intervalEnd 구간에서만 애니메이션.
    /// Flutter의 Interval(0.0, 0.5) 대응.
    /// </summary>
    public static IEnumerator IntervalFade(CanvasGroup cg, float from, float to,
                                            float totalDuration,
                                            float intervalStart, float intervalEnd,
                                            Easing.EaseFunc ease = null)
    {
        if (cg == null) yield break;
        ease = ease ?? Easing.Linear;
        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / totalDuration);
            float mapped = Mathf.InverseLerp(intervalStart, intervalEnd, t);
            mapped = Mathf.Clamp01(ease(Mathf.Clamp01(mapped)));
            cg.alpha = Mathf.Lerp(from, to, mapped);
            yield return null;
        }
        cg.alpha = to;
    }

    public static IEnumerator FadeGraphic(Graphic graphic, float from, float to,
                                           float duration, Easing.EaseFunc ease = null)
    {
        if (graphic == null) yield break;
        ease = ease ?? Easing.Linear;
        float elapsed = 0f;
        Color c = graphic.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = ease(Mathf.Clamp01(elapsed / duration));
            c.a = Mathf.LerpUnclamped(from, to, t);
            graphic.color = c;
            yield return null;
        }
        c.a = to;
        graphic.color = c;
    }

    // ====== Composite ======

    /// <summary>PopIn: scale 0→1 + fade 0→1. 다이얼로그 입장용.</summary>
    public static IEnumerator PopIn(RectTransform rt, float startScale = 0.85f,
                                     float duration = 0.3f, Easing.EaseFunc ease = null)
    {
        if (rt == null) yield break;
        ease = ease ?? Easing.OutBack;

        var cg = rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();

        rt.localScale = Vector3.one * startScale;
        cg.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = ease(t);

            rt.localScale = Vector3.LerpUnclamped(Vector3.one * startScale, Vector3.one, eased);
            // 페이드는 앞쪽 50%에서 완료
            cg.alpha = Mathf.Clamp01(t / 0.5f);
            yield return null;
        }
        rt.localScale = Vector3.one;
        cg.alpha = 1f;
    }

    /// <summary>PopOut: scale 1→0 + fade 1→0. 다이얼로그 퇴장용.</summary>
    public static IEnumerator PopOut(RectTransform rt, float endScale = 0.85f,
                                      float duration = 0.2f, Easing.EaseFunc ease = null)
    {
        if (rt == null) yield break;
        ease = ease ?? Easing.InQuad;

        var cg = rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();

        float elapsed = 0f;
        Vector3 start = rt.localScale;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = ease(Mathf.Clamp01(elapsed / duration));
            rt.localScale = Vector3.LerpUnclamped(start, Vector3.one * endScale, t);
            cg.alpha = 1f - t;
            yield return null;
        }
        rt.localScale = Vector3.one * endScale;
        cg.alpha = 0f;
    }

    /// <summary>SpringPress + SpringRelease — 버튼 탭 피드백.</summary>
    public static IEnumerator SpringPress(RectTransform rt, float scaleDown = 0.92f,
                                           float duration = 0.12f)
    {
        yield return Scale(rt, rt.localScale, Vector3.one * scaleDown, duration, Easing.InOutQuad);
    }

    public static IEnumerator SpringRelease(RectTransform rt, float duration = 0.3f)
    {
        yield return Scale(rt, rt.localScale, Vector3.one, duration, Easing.SpringBack);
    }

    // ====== Utility ======

    /// <summary>지연 후 콜백.</summary>
    public static IEnumerator Delay(float seconds, System.Action callback)
    {
        yield return new WaitForSeconds(seconds);
        callback?.Invoke();
    }

    /// <summary>순차 실행.</summary>
    public static IEnumerator Sequence(MonoBehaviour host, params IEnumerator[] tweens)
    {
        foreach (var tween in tweens)
        {
            yield return host.StartCoroutine(tween);
        }
    }
}
