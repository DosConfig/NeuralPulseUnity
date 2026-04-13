using UnityEngine;

public class SettingsScreen : MonoBehaviour
{
    private UIScreenBuilder _builder;
    private bool _hapticEnabled = true;

    public static GameObject Create(Transform parent)
    {
        var builder = UIScreenBuilder.Load("UI/Screens/settings_screen", parent);

        SettingsScreen comp = null;

        builder.SetBinding("hapticLabel", "ON");

        builder.RegisterAction("onBack", () => UIManager.SwitchTo(HomeScreen.Create));
        builder.RegisterFloatAction("onVolumeChange", (v) => comp?.OnVolumeChange(v));
        builder.RegisterAction("onHapticToggle", () => comp?.OnHapticToggle());
        builder.RegisterAction("onThemeCyber", () => comp?.ApplyTheme("CyberNeon"));
        builder.RegisterAction("onThemeOcean", () => comp?.ApplyTheme("OceanBreeze"));
        builder.RegisterAction("onThemeSunset", () => comp?.ApplyTheme("SunsetWarm"));
        builder.RegisterAction("onThemeLight", () => comp?.ApplyTheme("CleanLight"));

        var screen = builder.Build();
        comp = screen.AddComponent<SettingsScreen>();
        comp._builder = builder;

        return screen;
    }

    private void OnVolumeChange(float volume)
    {
        AudioListener.volume = volume;
        Debug.Log($"[Settings] Volume: {volume:F2}");
    }

    private void OnHapticToggle()
    {
        _hapticEnabled = !_hapticEnabled;
        _builder.SetBinding("hapticLabel", _hapticEnabled ? "ON" : "OFF");
        Debug.Log($"[Settings] Haptic: {_hapticEnabled}");
    }

    private void ApplyTheme(string paletteName)
    {
        var palette = UITheme.Palettes.GetByName(paletteName);
        UITheme.Apply(palette);
        Debug.Log($"[Settings] Theme: {palette.Name}");

        // 스크린 재빌드 (테마 변경 반영)
        UIManager.SwitchTo(SettingsScreen.Create);
    }

    private void OnDestroy()
    {
        _builder?.Cleanup();
    }
}
