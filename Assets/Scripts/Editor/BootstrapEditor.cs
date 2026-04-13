#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Unity Editor 메뉴에서 프로젝트 초기 설정을 실행.
/// Menu: Tools > AI Framework > Bootstrap Project
/// </summary>
public static class BootstrapEditor
{
    [MenuItem("Tools/AI Framework/Bootstrap Project")]
    public static void BootstrapProject()
    {
        // 1. 폴더 구조 생성
        CreateFolders();

        // 2. 빈 씬 설정
        SetupScene();

        // 3. Project Settings
        SetProjectSettings();

        Debug.Log("[Bootstrap] Project setup complete. See console for details.");
    }

    [MenuItem("Tools/AI Framework/Setup Scene (Canvas + EventSystem)")]
    public static void SetupScene()
    {
        // Camera
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            Debug.Log("[Bootstrap] Created Main Camera");
        }

        // Canvas
        var existingCanvas = Object.FindObjectOfType<Canvas>();
        if (existingCanvas == null)
        {
            var canvasGo = new GameObject("UICanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;

            canvasGo.AddComponent<GraphicRaycaster>();
            Debug.Log("[Bootstrap] Created UICanvas (1080cu, width-match)");
        }

        // EventSystem
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
            Debug.Log("[Bootstrap] Created EventSystem");
        }

        EditorUtility.DisplayDialog("Scene Setup", "Canvas + EventSystem 생성 완료.\n\nUIHelper.Initialize()를 앱 시작 코드에서 호출하세요.", "OK");
    }

    [MenuItem("Tools/AI Framework/Set Project Settings (Mobile Portrait)")]
    public static void SetProjectSettings()
    {
        // Portrait only
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        // Resolution
        PlayerSettings.resizableWindow = false;

        Debug.Log("[Bootstrap] Project Settings: Portrait mode, no landscape");
    }

    private static void CreateFolders()
    {
        string[] folders = new[]
        {
            "Assets/Scripts",
            "Assets/Scripts/Game",
            "Assets/Scripts/Game/Components",
            "Assets/Scripts/Game/Managers",
            "Assets/Scripts/Game/Stage",
            "Assets/Scripts/Game/Config",
            "Assets/Scripts/UI",
            "Assets/Scripts/UI/Core",
            "Assets/Scripts/UI/Screens",
            "Assets/Scripts/Utils",
            "Assets/Scripts/Rendering",
            "Assets/Scripts/Services",
            "Assets/Resources/UI/Screens",
            "Assets/Resources/UI/Overlays",
            "Assets/Resources/StageData",
            "Assets/Audio/BGM",
            "Assets/Audio/SFX",
            "Assets/Sprites",
            "Assets/Tests",
            "Docs",
            "Docs/wiki",
        };

        foreach (var path in folders)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("[Bootstrap] Created folder structure");
    }
}
#endif
