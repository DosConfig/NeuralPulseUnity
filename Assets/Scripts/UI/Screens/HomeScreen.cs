using UnityEngine;

public class HomeScreen : MonoBehaviour
{
    private UIScreenBuilder _builder;

    public static GameObject Create(Transform parent)
    {
        var builder = UIScreenBuilder.Load("UI/Screens/home_screen", parent);

        HomeScreen comp = null;
        builder.RegisterAction("onPlay", () => comp?.OnPlay());
        builder.RegisterAction("onDaily", () => comp?.OnDaily());
        builder.RegisterAction("onLeaderboard", () => comp?.OnLeaderboard());
        builder.RegisterAction("onSettings", () => comp?.OnSettings());
        builder.RegisterAction("onProfile", () => comp?.OnProfile());

        var screen = builder.Build();
        comp = screen.AddComponent<HomeScreen>();
        comp._builder = builder;

        return screen;
    }

    private void OnPlay()
    {
        Debug.Log("[Home] Play → WorldMap or GameScreen");
        // UIManager.SwitchTo(GameScreen.Create);
    }

    private void OnDaily()
    {
        Debug.Log("[Home] Daily Challenge");
    }

    private void OnLeaderboard()
    {
        Debug.Log("[Home] Leaderboard");
    }

    private void OnSettings()
    {
        Debug.Log("[Home] Settings");
        UIManager.SwitchTo(SettingsScreen.Create);
    }

    private void OnProfile()
    {
        Debug.Log("[Home] Profile");
    }

    private void OnDestroy()
    {
        _builder?.Cleanup();
    }
}
