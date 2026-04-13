using UnityEngine;

public class ResultScreen : MonoBehaviour
{
    private UIScreenBuilder _builder;

    public static GameObject Create(Transform parent, int score, int stage, bool cleared)
    {
        var builder = UIScreenBuilder.Load("UI/Screens/result_screen", parent);

        builder.SetBinding("resultTitle", cleared ? "STAGE CLEAR!" : "GAME OVER");
        builder.SetBinding("stageText", $"Stage {stage}");
        builder.SetBinding("scoreText", $"{score:N0}");
        builder.SetBinding("titleColor", cleared ? "Status.Success" : "Status.Danger");

        ResultScreen comp = null;
        builder.RegisterAction("onContinue", () =>
        {
            if (cleared)
                UIManager.SwitchTo((p) => GameScreen.Create(p)); // 다음 스테이지
            else
                UIManager.SwitchTo(HomeScreen.Create);
        });
        builder.RegisterAction("onHome", () => UIManager.SwitchTo(HomeScreen.Create));

        var screen = builder.Build();
        comp = screen.AddComponent<ResultScreen>();
        comp._builder = builder;

        return screen;
    }

    private void OnDestroy()
    {
        _builder?.Cleanup();
    }
}
