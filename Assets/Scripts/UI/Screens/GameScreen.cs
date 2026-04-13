using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 게임 스크린. 게임 보드(2D) + HUD(Canvas) 통합.
/// </summary>
public class GameScreen : MonoBehaviour
{
    // 게임 로직
    private GameManager _gameManager;
    private OrbGrid _grid;
    private SwipeHandler _swipe;

    // 렌더링
    private GridRenderer _gridRenderer;
    private ParticleController _particles;

    // HUD
    private UIScreenBuilder _hudBuilder;
    private CanvasGroup _gradeCG;
    private float _gradeTimer;

    // 스테이지
    private StageConfig _stageConfig;

    public static GameObject Create(Transform parent, StageConfig config = null)
    {
        var go = new GameObject("GameScreen");
        go.transform.SetParent(parent, false);

        var comp = go.AddComponent<GameScreen>();
        comp._stageConfig = config ?? CreateDefaultStage();
        comp.Setup();

        return go;
    }

    private void Setup()
    {
        // ---- 게임 로직 ----
        _gameManager = new GameManager();
        _grid = new OrbGrid(_stageConfig.cols, _stageConfig.rows, _stageConfig.colorCount);
        _grid.ApplyStageConfig(_stageConfig);
        _grid.Fill();
        _swipe = new SwipeHandler(_grid);

        _gameManager.LoadStage(_stageConfig);

        // ---- 렌더링 ----
        var boardGo = new GameObject("Board");
        boardGo.transform.SetParent(transform, false);
        boardGo.transform.localPosition = new Vector3(0, -0.5f, 0); // 약간 아래

        _gridRenderer = boardGo.AddComponent<GridRenderer>();
        _gridRenderer.Initialize(_grid);

        _particles = boardGo.AddComponent<ParticleController>();
        _particles.Initialize();

        // ---- HUD (Canvas 위) ----
        SetupHUD();

        // ---- 이벤트 연결 ----
        _grid.OnOrbsPopped = OnOrbsPopped;
        _grid.OnCascade = (depth) => { };
        _grid.OnObstacleDamaged = OnObstacleDamaged;

        _gameManager.OnScoreChanged = (score) => _hudBuilder?.SetBinding("score", $"{score:N0}");
        _gameManager.OnTurnsChanged = (turns) => _hudBuilder?.SetBinding("turns", turns.ToString());
        _gameManager.OnChainResolved = OnChainGrade;
        _gameManager.OnGoalsUpdated = UpdateGoalDisplay;
        _gameManager.OnStateChanged = OnStateChanged;

        // 시작
        _gameManager.StartPlaying();
        UpdateGoalDisplay();
    }

    private void SetupHUD()
    {
        var canvas = UIHelper.RootCanvas;
        if (canvas == null) return;

        _hudBuilder = UIScreenBuilder.Load("UI/Screens/game_hud", canvas.transform);
        _hudBuilder.SetBinding("score", "0");
        _hudBuilder.SetBinding("turns", _stageConfig.turns.ToString());
        _hudBuilder.SetBinding("showGrade", false);
        _hudBuilder.SetBinding("gradeText", "");

        // 골 바인딩
        for (int i = 0; i < 3; i++)
        {
            _hudBuilder.SetBinding($"g{i}vis", i < _gameManager.Goals.Count);
            _hudBuilder.SetBinding($"g{i}text", "");
        }

        _hudBuilder.RegisterAction("onPause", OnPause);

        var hud = _hudBuilder.Build();
        hud.name = "GameHUD";

        // 등급 팝업 CanvasGroup
        var gradeGo = _hudBuilder.GetElement("gradePopup");
        if (gradeGo != null)
        {
            _gradeCG = gradeGo.GetComponent<CanvasGroup>();
            if (_gradeCG == null) _gradeCG = gradeGo.AddComponent<CanvasGroup>();
        }
    }

    // ====== 입력 ======

    void Update()
    {
        // 타이머
        _gameManager.TickTimer(Time.deltaTime);

        // 등급 팝업 페이드
        if (_gradeTimer > 0)
        {
            _gradeTimer -= Time.deltaTime;
            if (_gradeCG != null)
                _gradeCG.alpha = Mathf.Clamp01(_gradeTimer * 2f);
            if (_gradeTimer <= 0)
                _hudBuilder?.SetBinding("showGrade", false);
        }

        // 터치/마우스 입력
        if (_gameManager.State != GameManager.GameState.Playing) return;
        HandleInput();
    }

    private void HandleInput()
    {
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) HandleDragStart(Input.mousePosition);
        else if (Input.GetMouseButton(0)) HandleDragUpdate(Input.mousePosition);
        else if (Input.GetMouseButtonUp(0)) HandleDragEnd();
#else
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began: HandleDragStart(touch.position); break;
                case TouchPhase.Moved: HandleDragUpdate(touch.position); break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled: HandleDragEnd(); break;
            }
        }
#endif
    }

    private void HandleDragStart(Vector2 screenPos)
    {
        var worldPos = Camera.main.ScreenToWorldPoint(screenPos);
        var (col, row) = _gridRenderer.WorldToGrid(worldPos);
        _swipe.OnDragStart(col, row);

        if (_swipe.IsSwiping)
            _gridRenderer.ShowChainLine(_swipe.Chain);
    }

    private void HandleDragUpdate(Vector2 screenPos)
    {
        if (!_swipe.IsSwiping) return;
        var worldPos = Camera.main.ScreenToWorldPoint(screenPos);
        var (col, row) = _gridRenderer.WorldToGrid(worldPos);
        _swipe.OnDragUpdate(col, row);
        _gridRenderer.ShowChainLine(_swipe.Chain);

        // 체인 사운드
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayChainSelect(_swipe.Chain.Count);
    }

    private void HandleDragEnd()
    {
        _gridRenderer.HideChainLine();
        var result = _swipe.OnDragEnd();

        if (result.IsValid)
        {
            _grid.ProcessChain(result.Chain);
            _gameManager.UseTurn();
        }
    }

    // ====== 이벤트 핸들러 ======

    private void OnOrbsPopped(List<OrbData> orbs)
    {
        _gameManager.ResolveChain(orbs, false);

        foreach (var orb in orbs)
        {
            var pos = _gridRenderer.GridToWorld(orb.Col, orb.Row) + _gridRenderer.transform.position;
            _particles.PlayPop(pos, OrbData.ToUnityColor(orb.Color));
        }

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayPop(orbs.Count);
    }

    private void OnObstacleDamaged(int col, int row, bool destroyed)
    {
        if (destroyed)
        {
            var pos = _gridRenderer.GridToWorld(col, row) + _gridRenderer.transform.position;
            _particles.PlaySpark(pos);
        }
    }

    private void OnChainGrade(int count, GameManager.ChainGrade grade)
    {
        if (grade == GameManager.ChainGrade.None) return;

        _hudBuilder?.SetBinding("gradeText", grade.ToString().ToUpper());
        _hudBuilder?.SetBinding("showGrade", true);
        _gradeTimer = 1.2f;
        if (_gradeCG != null) _gradeCG.alpha = 1f;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayGrade(grade);
    }

    private void UpdateGoalDisplay()
    {
        for (int i = 0; i < 3; i++)
        {
            if (i < _gameManager.Goals.Count)
            {
                var g = _gameManager.Goals[i];
                string label = g.Color.HasValue ? g.Color.Value.ToString().Substring(0, 3) : g.Type.ToString().Substring(0, 5);
                _hudBuilder?.SetBinding($"g{i}text", $"{label} {g.Current}/{g.Target}");
            }
        }
    }

    private void OnStateChanged(GameManager.GameState state)
    {
        switch (state)
        {
            case GameManager.GameState.StageComplete:
                StartCoroutine(ShowResultDelayed(true));
                break;
            case GameManager.GameState.GameOver:
                StartCoroutine(ShowResultDelayed(false));
                break;
        }
    }

    private IEnumerator ShowResultDelayed(bool cleared)
    {
        yield return new WaitForSeconds(1f);

        // HUD 정리
        var hudGo = _hudBuilder?.GetElement("topHUD")?.transform.root.gameObject;
        // 결과 화면으로
        UIManager.SwitchTo((parent) => ResultScreen.Create(parent,
            _gameManager.TotalScore, _stageConfig.stage, cleared));
    }

    private void OnPause()
    {
        Debug.Log("[Game] Pause");
        // 추후: 일시정지 오버레이
    }

    private void OnDestroy()
    {
        _hudBuilder?.Cleanup();
        // HUD GameObject 정리
        var canvas = UIHelper.RootCanvas;
        if (canvas != null)
        {
            var hud = canvas.transform.Find("GameHUD");
            if (hud != null) Destroy(hud.gameObject);
        }
    }

    // ====== 기본 스테이지 ======

    private static StageConfig CreateDefaultStage()
    {
        return new StageConfig
        {
            stage = 1,
            cols = 5,
            rows = 6,
            turns = 20,
            colorCount = 5,
            goals = new List<StageConfig.GoalDef>
            {
                new StageConfig.GoalDef { type = "clearColor", color = "red", target = 15 },
                new StageConfig.GoalDef { type = "clearColor", color = "blue", target = 15 },
            }
        };
    }
}
