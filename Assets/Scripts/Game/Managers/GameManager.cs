using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 상태 머신 + 턴/골/콤보/스코어 관리.
/// </summary>
public class GameManager
{
    public enum GameState { Ready, Playing, BonusRound, StageComplete, GameOver }

    public enum ChainGrade { None, Nice, Great, Amazing, Incredible }

    // 상태
    public GameState State { get; private set; } = GameState.Ready;
    public int CurrentStage { get; private set; }
    public int TurnsRemaining { get; private set; }
    public float TimeRemaining { get; private set; }
    public int TotalScore { get; private set; }
    public int Combo { get; private set; }
    public List<StageGoal> Goals { get; private set; } = new List<StageGoal>();

    // 설정
    private StageConfig _config;
    private bool _isTimeBased;

    // 콜백
    public System.Action<GameState> OnStateChanged;
    public System.Action<int> OnScoreChanged;
    public System.Action<int> OnTurnsChanged;
    public System.Action<float> OnTimeChanged;
    public System.Action<int, ChainGrade> OnChainResolved; // popCount, grade
    public System.Action<int> OnComboChanged;
    public System.Action OnGoalsUpdated;

    // ====== 라이프사이클 ======

    public void LoadStage(StageConfig config)
    {
        _config = config;
        CurrentStage = config.stage;
        TurnsRemaining = config.turns;
        TimeRemaining = config.timeLimit;
        _isTimeBased = config.timeLimit > 0;
        TotalScore = 0;
        Combo = 0;

        // 골 초기화
        Goals.Clear();
        foreach (var gd in config.goals)
        {
            Goals.Add(new StageGoal
            {
                Type = StageGoal.ParseType(gd.type),
                Color = gd.type == "clearColor" ? (OrbData.OrbColor?)StageGoal.ParseColor(gd.color) : null,
                Target = gd.target,
                Current = 0,
            });
        }

        SetState(GameState.Ready);
    }

    public void StartPlaying()
    {
        SetState(GameState.Playing);
    }

    public void TickTimer(float dt)
    {
        if (State != GameState.Playing || !_isTimeBased) return;
        TimeRemaining -= dt;
        OnTimeChanged?.Invoke(TimeRemaining);
        if (TimeRemaining <= 0)
        {
            TimeRemaining = 0;
            SetState(GameState.GameOver);
        }
    }

    // ====== 턴 처리 ======

    /// <summary>체인 매칭 결과 처리. poppedOrbs = 이번에 터진 오브 목록.</summary>
    public void ResolveChain(List<OrbData> poppedOrbs, bool isLoop)
    {
        if (State != GameState.Playing && State != GameState.BonusRound) return;

        int count = poppedOrbs.Count;
        if (count == 0) return;

        // 스코어 계산 (체인 길이별 더블링)
        int baseScore = CalculateChainScore(count);
        int shapeBonus = isLoop ? CalculateShapeBonus(count) : 0;
        float comboMult = GetComboMultiplier();

        int earned = Mathf.RoundToInt((baseScore + shapeBonus) * comboMult);
        TotalScore += earned;
        OnScoreChanged?.Invoke(TotalScore);

        // 콤보
        if (count >= 4) { Combo++; OnComboChanged?.Invoke(Combo); }
        else { Combo = 0; OnComboChanged?.Invoke(0); }

        // 등급
        ChainGrade grade = GradeForChain(count);
        OnChainResolved?.Invoke(count, grade);

        // 골 업데이트
        UpdateGoals(poppedOrbs);
    }

    /// <summary>턴 소모. 매칭 후 호출.</summary>
    public void UseTurn()
    {
        if (State == GameState.BonusRound)
        {
            TurnsRemaining--;
            OnTurnsChanged?.Invoke(TurnsRemaining);
            if (TurnsRemaining <= 0)
                SetState(GameState.StageComplete);
            return;
        }

        if (State != GameState.Playing) return;

        if (!_isTimeBased)
        {
            TurnsRemaining--;
            OnTurnsChanged?.Invoke(TurnsRemaining);
        }

        // 골 체크
        if (AllGoalsComplete())
        {
            if (TurnsRemaining > 0)
                SetState(GameState.BonusRound);
            else
                SetState(GameState.StageComplete);
            return;
        }

        // 게임 오버 체크
        if (!_isTimeBased && TurnsRemaining <= 0)
        {
            SetState(GameState.GameOver);
        }
    }

    // ====== 스코어 ======

    private int CalculateChainScore(int count)
    {
        // 2=100, 3=200, 4=400, 5=800, 6+=1600
        if (count <= 1) return 0;
        if (count == 2) return 100;
        if (count == 3) return 200;
        if (count == 4) return 400;
        if (count == 5) return 800;
        return 1600;
    }

    private int CalculateShapeBonus(int loopLength)
    {
        if (loopLength == 3) return 300;   // 삼각형
        if (loopLength == 4) return 600;   // 사각형
        return 1200;                        // 오각형+
    }

    private float GetComboMultiplier()
    {
        if (Combo <= 0) return 1f;
        if (Combo == 1) return 1.5f;
        if (Combo == 2) return 2f;
        if (Combo == 3) return 2.25f;
        return 2.5f; // max
    }

    public static ChainGrade GradeForChain(int length)
    {
        if (length >= 7) return ChainGrade.Incredible;
        if (length >= 6) return ChainGrade.Amazing;
        if (length >= 5) return ChainGrade.Great;
        if (length >= 4) return ChainGrade.Nice;
        return ChainGrade.None;
    }

    // ====== 골 ======

    private void UpdateGoals(List<OrbData> popped)
    {
        foreach (var goal in Goals)
        {
            switch (goal.Type)
            {
                case StageGoal.GoalType.ClearColor:
                    foreach (var orb in popped)
                        if (orb.Color == goal.Color) goal.Current++;
                    break;
            }
        }
        OnGoalsUpdated?.Invoke();
    }

    public void NotifyObstacleDestroyed(StageGoal.GoalType type)
    {
        foreach (var goal in Goals)
        {
            if (goal.Type == type) goal.Current++;
        }
        OnGoalsUpdated?.Invoke();
    }

    private bool AllGoalsComplete()
    {
        foreach (var g in Goals)
            if (!g.IsComplete) return false;
        return Goals.Count > 0;
    }

    // ====== 상태 전이 ======

    private void SetState(GameState newState)
    {
        State = newState;
        OnStateChanged?.Invoke(newState);
    }
}
