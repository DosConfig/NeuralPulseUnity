using UnityEngine;

/// <summary>
/// 스크린 전환 관리. 현재 스크린 파괴 → 새 스크린 생성.
/// 전체 앱에서 하나만 존재.
/// </summary>
public static class UIManager
{
    private static GameObject _currentScreen;
    private static Transform _canvasTransform;

    public static void Initialize(Transform canvasTransform)
    {
        _canvasTransform = canvasTransform;
    }

    /// <summary>현재 스크린 파괴 후 새 스크린 생성.</summary>
    public static void SwitchTo(System.Func<Transform, GameObject> createScreen)
    {
        if (_currentScreen != null)
            Object.Destroy(_currentScreen);

        _currentScreen = createScreen(_canvasTransform);
    }

    /// <summary>현재 스크린 위에 오버레이 추가. 반환값으로 직접 파괴.</summary>
    public static GameObject ShowOverlay(System.Func<Transform, GameObject> createOverlay)
    {
        return createOverlay(_canvasTransform);
    }

    /// <summary>현재 활성 스크린.</summary>
    public static GameObject CurrentScreen => _currentScreen;
}
