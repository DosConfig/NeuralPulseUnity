using UnityEngine;

/// <summary>
/// 이징 함수 모음. SDFTween에서 사용.
/// 모든 함수는 t(0~1) → result(0~1, overshoot 가능) 시그니처.
/// </summary>
public static class Easing
{
    public delegate float EaseFunc(float t);

    public static float Linear(float t) => t;

    // Quad
    public static float InQuad(float t) => t * t;
    public static float OutQuad(float t) => t * (2f - t);
    public static float InOutQuad(float t) =>
        t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

    // Cubic
    public static float InCubic(float t) => t * t * t;
    public static float OutCubic(float t) { t--; return t * t * t + 1f; }
    public static float InOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : (t - 1f) * (2f * t - 2f) * (2f * t - 2f) + 1f;

    // Back (overshoot)
    public static float InBack(float t)
    {
        const float s = 1.70158f;
        return t * t * ((s + 1f) * t - s);
    }
    public static float OutBack(float t)
    {
        const float s = 1.70158f;
        t--;
        return t * t * ((s + 1f) * t + s) + 1f;
    }

    // Elastic
    public static float OutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f;
    }

    // Bounce
    public static float OutBounce(float t)
    {
        if (t < 1f / 2.75f) return 7.5625f * t * t;
        if (t < 2f / 2.75f) { t -= 1.5f / 2.75f; return 7.5625f * t * t + 0.75f; }
        if (t < 2.5f / 2.75f) { t -= 2.25f / 2.75f; return 7.5625f * t * t + 0.9375f; }
        t -= 2.625f / 2.75f;
        return 7.5625f * t * t + 0.984375f;
    }

    // Spring (Neural Pulse 호환 — 오버슈트 후 안착)
    public static float SpringBack(float t)
    {
        float inv = 1f - t;
        return 1f - inv * inv * ((2.5f * t - 1.5f) * t + 1f);
    }

    // Spring Damping (Neural Pulse SpringSlideUp 호환)
    public static float SpringDamping(float t)
    {
        float p = t * t;
        float raw = p * p * (-3f * p + 8f * t * t * t - 6f * p + 4f * t) + (1f - (1f - t) * (1f - t)) * 0.5f + t * 0.5f;
        return Mathf.Clamp01(raw);
    }
}
