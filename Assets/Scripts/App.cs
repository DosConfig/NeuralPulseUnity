using UnityEngine;

/// <summary>
/// 앱 진입점. 프레임워크 초기화 + 첫 화면 표시.
/// 빈 씬의 GameObject에 부착하거나, RuntimeInitializeOnLoadMethod로 자동 실행.
/// </summary>
public class App : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBoot()
    {
        if (FindObjectOfType<App>() != null) return;

        var go = new GameObject("App");
        DontDestroyOnLoad(go);
        go.AddComponent<App>();
    }

    void Start()
    {
        // 1. UI 시스템 초기화
        var canvas = UIHelper.Initialize();

        // 2. 테마 적용
        UITheme.Apply(UITheme.Palettes.CyberNeon);

        // 3. UIManager 초기화
        UIManager.Initialize(canvas.transform);

        // 4. 사운드 매니저
        var soundGo = new GameObject("SoundManager");
        DontDestroyOnLoad(soundGo);
        soundGo.AddComponent<SoundManager>();

        // 5. 카메라 설정
        GameRenderer.Setup2DCamera(5.5f, new Color(0.04f, 0.04f, 0.06f));

        // 6. 첫 화면
        UIManager.SwitchTo(SplashScreen.Create);
    }
}
