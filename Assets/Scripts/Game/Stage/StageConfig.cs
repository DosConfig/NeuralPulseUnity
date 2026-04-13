using System.Collections.Generic;

/// <summary>
/// 스테이지 정의. JSON에서 로드하거나 C#으로 직접 생성.
/// </summary>
[System.Serializable]
public class StageConfig
{
    public int stage;
    public int cols = 5;
    public int rows = 6;
    public int turns = 20;
    public int timeLimit = 0;   // 0=턴제, >0=시간제(초)
    public int colorCount = 5;  // 사용할 오브 색상 수 (3~7)

    public List<GoalDef> goals = new List<GoalDef>();

    // 장애물 배치
    public List<Placement> bricks = new List<Placement>();
    public List<Placement> glass = new List<Placement>();
    public List<Placement> ice = new List<Placement>();
    public List<IntPair> steels = new List<IntPair>();
    public List<IntPair> darkZones = new List<IntPair>();
    public List<IntPair> bombDots = new List<IntPair>();
    public List<IntPair> crystals = new List<IntPair>();
    public List<IntPair> butterflies = new List<IntPair>();
    public List<IntPair> mask = new List<IntPair>(); // 비활성 셀

    [System.Serializable]
    public class GoalDef
    {
        public string type;     // clearColor, breakGlass, destroyBricks, breakIce, dropButterfly, destroyCrystal
        public string color;    // clearColor용
        public int target;
    }

    [System.Serializable]
    public class Placement
    {
        public int col, row, layers;
    }

    [System.Serializable]
    public class IntPair
    {
        public int col, row;
    }
}

/// <summary>런타임 골 상태 추적.</summary>
public class StageGoal
{
    public GoalType Type;
    public OrbData.OrbColor? Color;  // clearColor용
    public int Target;
    public int Current;

    public bool IsComplete => Current >= Target;

    public enum GoalType
    {
        ClearColor,
        BreakGlass,
        DestroyBricks,
        BreakIce,
        DropButterfly,
        DestroyCrystal,
    }

    public static GoalType ParseType(string s)
    {
        switch (s?.ToLower())
        {
            case "clearcolor":      return GoalType.ClearColor;
            case "breakglass":      return GoalType.BreakGlass;
            case "destroybricks":   return GoalType.DestroyBricks;
            case "breakice":        return GoalType.BreakIce;
            case "dropbutterfly":   return GoalType.DropButterfly;
            case "destroycrystal":  return GoalType.DestroyCrystal;
            default:                return GoalType.ClearColor;
        }
    }

    public static OrbData.OrbColor ParseColor(string s)
    {
        switch (s?.ToLower())
        {
            case "red":    return OrbData.OrbColor.Red;
            case "blue":   return OrbData.OrbColor.Blue;
            case "green":  return OrbData.OrbColor.Green;
            case "yellow": return OrbData.OrbColor.Yellow;
            case "purple": return OrbData.OrbColor.Purple;
            case "orange": return OrbData.OrbColor.Orange;
            case "cyan":   return OrbData.OrbColor.Cyan;
            default:       return OrbData.OrbColor.Red;
        }
    }
}
