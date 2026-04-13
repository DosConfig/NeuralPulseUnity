using UnityEngine;
using System.Collections;

public class SplashScreen : MonoBehaviour
{
    private UIScreenBuilder _builder;

    public static GameObject Create(Transform parent)
    {
        var builder = UIScreenBuilder.Load("UI/Screens/splash_screen", parent);

        builder.SetBinding("loadingStatus", "Initializing...");
        builder.SetBinding("version", $"v0.1.0");

        SplashScreen comp = null;
        builder.RegisterAction("onTap", () => comp?.GoToHome());

        var screen = builder.Build();
        comp = screen.AddComponent<SplashScreen>();
        comp._builder = builder;

        comp.StartCoroutine(comp.LoadSequence());
        return screen;
    }

    private IEnumerator LoadSequence()
    {
        yield return new WaitForSeconds(0.3f);
        _builder.SetBinding("loadingStatus", "Loading audio...");

        yield return new WaitForSeconds(0.3f);
        _builder.SetBinding("loadingStatus", "Ready!");

        yield return new WaitForSeconds(0.5f);
        _builder.SetBinding("loadingStatus", "Tap to start");
    }

    private void GoToHome()
    {
        // 추후: HomeScreen으로 전환
        Debug.Log("[SplashScreen] GoToHome");
    }

    private void OnDestroy()
    {
        _builder?.Cleanup();
    }
}
