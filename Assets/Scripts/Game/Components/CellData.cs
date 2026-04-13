/// <summary>
/// 그리드 셀 하나의 데이터. 오브 + 장애물 + 기믹.
/// </summary>
public class CellData
{
    public OrbData Orb;     // null이면 빈 셀

    // 장애물
    public int BrickLayers; // 0~3 (0=없음)
    public int GlassLayers; // 0~2
    public bool Steel;      // 파괴 불가

    // 기믹
    public int IceLayers;   // 0~3 (얼음)
    public bool Crystal;    // 주변 8칸 클리어로 파괴
    public bool Butterfly;  // 하단으로 떨어뜨려야 함
    public int TimerBox;    // N턴 후 자동 파괴 (0=없음)

    // 특수
    public bool DarkZone;   // 색상 숨김
    public bool BombDot;    // 매칭 시 8방향 폭발
    public bool Chameleon;  // 와일드카드 색상

    // 벽 (8방향 비트마스크)
    public byte Walls;      // bit 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW

    // 포탈
    public int PortalId;    // 0=없음, 같은 ID끼리 연결
    public int PortalTargetCol, PortalTargetRow;

    public bool IsEmpty => Orb == null && !Steel;
    public bool HasObstacle => BrickLayers > 0 || GlassLayers > 0 || Steel || IceLayers > 0;

    /// <summary>장애물에 대미지. true면 파괴됨.</summary>
    public bool DamageObstacle()
    {
        if (BrickLayers > 0) { BrickLayers--; return BrickLayers == 0; }
        if (GlassLayers > 0) { GlassLayers--; return GlassLayers == 0; }
        if (IceLayers > 0) { IceLayers--; return IceLayers == 0; }
        return false;
    }

    public bool HasWall(int direction)
    {
        return (Walls & (1 << direction)) != 0;
    }
}
