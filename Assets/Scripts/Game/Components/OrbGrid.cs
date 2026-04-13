using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 보드 로직. 매칭, 캐스케이드, 드롭.
/// 렌더링 무관 — 순수 데이터 + 알고리즘.
/// </summary>
public class OrbGrid
{
    public int Cols { get; private set; }
    public int Rows { get; private set; }
    public CellData[,] Cells { get; private set; }

    private int _colorCount;
    private System.Random _rng;

    // 이벤트
    public System.Action<List<OrbData>> OnOrbsPopped;
    public System.Action<int, int, bool> OnObstacleDamaged; // col, row, destroyed
    public System.Action OnDropComplete;
    public System.Action<int> OnCascade; // cascade depth

    public OrbGrid(int cols, int rows, int colorCount, int seed = 0)
    {
        Cols = cols;
        Rows = rows;
        _colorCount = Mathf.Clamp(colorCount, 3, 7);
        _rng = seed > 0 ? new System.Random(seed) : new System.Random();
        Cells = new CellData[cols, rows];

        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                Cells[c, r] = new CellData();
    }

    /// <summary>그리드를 랜덤 오브로 채움 (초기 매칭 없이).</summary>
    public void Fill()
    {
        for (int c = 0; c < Cols; c++)
        {
            for (int r = 0; r < Rows; r++)
            {
                if (Cells[c, r].Steel) continue;

                var color = RandomColor();
                // 초기 3연속 방지
                int attempts = 0;
                while (attempts < 20 && WouldMatch3(c, r, color))
                {
                    color = RandomColor();
                    attempts++;
                }

                Cells[c, r].Orb = new OrbData(color, c, r);
            }
        }
    }

    /// <summary>스테이지 설정 적용 (장애물, 기믹 배치).</summary>
    public void ApplyStageConfig(StageConfig config)
    {
        foreach (var b in config.bricks)
            if (InBounds(b.col, b.row)) Cells[b.col, b.row].BrickLayers = b.layers;
        foreach (var g in config.glass)
            if (InBounds(g.col, g.row)) Cells[g.col, g.row].GlassLayers = g.layers;
        foreach (var i in config.ice)
            if (InBounds(i.col, i.row)) Cells[i.col, i.row].IceLayers = i.layers;
        foreach (var s in config.steels)
            if (InBounds(s.col, s.row)) Cells[s.col, s.row].Steel = true;
        foreach (var d in config.darkZones)
            if (InBounds(d.col, d.row)) Cells[d.col, d.row].DarkZone = true;
        foreach (var bd in config.bombDots)
            if (InBounds(bd.col, bd.row)) Cells[bd.col, bd.row].BombDot = true;
        foreach (var cr in config.crystals)
            if (InBounds(cr.col, cr.row)) Cells[cr.col, cr.row].Crystal = true;
        foreach (var bf in config.butterflies)
            if (InBounds(bf.col, bf.row)) Cells[bf.col, bf.row].Butterfly = true;
    }

    // ====== 매칭 ======

    /// <summary>
    /// 체인(연결된 오브 목록) 처리. 매칭 → 대미지 → 제거 → 드롭 → 캐스케이드.
    /// 반환: 총 제거된 오브 수.
    /// </summary>
    public int ProcessChain(List<Vector2Int> chain)
    {
        if (chain == null || chain.Count < 2) return 0;

        int totalPopped = 0;
        int cascadeDepth = 0;

        // 체인 오브 제거
        var popped = new List<OrbData>();
        foreach (var pos in chain)
        {
            var cell = Cells[pos.x, pos.y];
            if (cell.Orb != null)
            {
                popped.Add(cell.Orb);
                cell.Orb = null;
            }
            // 인접 장애물 대미지
            DamageAdjacentObstacles(pos.x, pos.y);
        }

        if (popped.Count > 0)
        {
            OnOrbsPopped?.Invoke(popped);
            totalPopped += popped.Count;
        }

        // 캐스케이드 루프
        const int maxCascade = 12;
        while (cascadeDepth < maxCascade)
        {
            // 1. 중력 (빈 칸 채우기)
            ApplyGravity();

            // 2. 새 오브로 상단 채우기
            FillTop();

            // 3. 새로운 매칭 찾기
            var newMatches = FindAllMatches();
            if (newMatches.Count == 0) break;

            cascadeDepth++;
            OnCascade?.Invoke(cascadeDepth);

            // 매칭 제거
            var cascadePopped = new List<OrbData>();
            foreach (var pos in newMatches)
            {
                var cell = Cells[pos.x, pos.y];
                if (cell.Orb != null)
                {
                    cascadePopped.Add(cell.Orb);
                    cell.Orb = null;
                }
                DamageAdjacentObstacles(pos.x, pos.y);
            }

            if (cascadePopped.Count > 0)
            {
                OnOrbsPopped?.Invoke(cascadePopped);
                totalPopped += cascadePopped.Count;
            }
        }

        OnDropComplete?.Invoke();
        return totalPopped;
    }

    /// <summary>3개 이상 연속된 모든 매칭 찾기 (가로 + 세로).</summary>
    public List<Vector2Int> FindAllMatches()
    {
        var matched = new HashSet<Vector2Int>();

        // 가로 스캔
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols - 2; c++)
            {
                var color = GetOrbColor(c, r);
                if (color == null) continue;

                int len = 1;
                while (c + len < Cols && GetOrbColor(c + len, r) == color)
                    len++;

                if (len >= 3)
                {
                    for (int i = 0; i < len; i++)
                        matched.Add(new Vector2Int(c + i, r));
                }
                c += len - 1;
            }
        }

        // 세로 스캔
        for (int c = 0; c < Cols; c++)
        {
            for (int r = 0; r < Rows - 2; r++)
            {
                var color = GetOrbColor(c, r);
                if (color == null) continue;

                int len = 1;
                while (r + len < Rows && GetOrbColor(c, r + len) == color)
                    len++;

                if (len >= 3)
                {
                    for (int i = 0; i < len; i++)
                        matched.Add(new Vector2Int(c, r + i));
                }
                r += len - 1;
            }
        }

        return new List<Vector2Int>(matched);
    }

    // ====== 중력 + 채우기 ======

    private void ApplyGravity()
    {
        for (int c = 0; c < Cols; c++)
        {
            int writeRow = 0; // 아래부터 채움
            for (int r = 0; r < Rows; r++)
            {
                if (Cells[c, r].Steel) { writeRow = r + 1; continue; }
                if (Cells[c, r].Orb != null)
                {
                    if (r != writeRow)
                    {
                        var orb = Cells[c, r].Orb;
                        orb.Row = writeRow;
                        orb.TargetY = writeRow;
                        orb.Dropping = true;
                        orb.VelocityY = 0;

                        Cells[c, writeRow].Orb = orb;
                        Cells[c, r].Orb = null;
                    }
                    writeRow++;
                }
            }
        }
    }

    private void FillTop()
    {
        for (int c = 0; c < Cols; c++)
        {
            for (int r = 0; r < Rows; r++)
            {
                if (Cells[c, r].Steel) continue;
                if (Cells[c, r].Orb == null)
                {
                    var color = RandomColor();
                    var orb = new OrbData(color, c, r);
                    orb.CurrentY = Rows + 1; // 위에서 떨어짐
                    orb.TargetY = r;
                    orb.Dropping = true;
                    Cells[c, r].Orb = orb;
                }
            }
        }
    }

    // ====== 장애물 ======

    private void DamageAdjacentObstacles(int col, int row)
    {
        int[] dx = { 0, 1, 0, -1 };
        int[] dy = { 1, 0, -1, 0 };

        for (int d = 0; d < 4; d++)
        {
            int nc = col + dx[d];
            int nr = row + dy[d];
            if (!InBounds(nc, nr)) continue;

            var cell = Cells[nc, nr];
            if (cell.HasObstacle)
            {
                bool destroyed = cell.DamageObstacle();
                OnObstacleDamaged?.Invoke(nc, nr, destroyed);
            }
        }
    }

    // ====== 유틸리티 ======

    public OrbData GetOrb(int col, int row)
    {
        if (!InBounds(col, row)) return null;
        return Cells[col, row].Orb;
    }

    public OrbData.OrbColor? GetOrbColor(int col, int row)
    {
        if (!InBounds(col, row)) return null;
        return Cells[col, row].Orb?.Color;
    }

    public bool InBounds(int col, int row)
    {
        return col >= 0 && col < Cols && row >= 0 && row < Rows;
    }

    private OrbData.OrbColor RandomColor()
    {
        return (OrbData.OrbColor)_rng.Next(_colorCount);
    }

    private bool WouldMatch3(int c, int r, OrbData.OrbColor color)
    {
        // 가로 체크
        if (c >= 2 && GetOrbColor(c - 1, r) == color && GetOrbColor(c - 2, r) == color)
            return true;
        // 세로 체크
        if (r >= 2 && GetOrbColor(c, r - 1) == color && GetOrbColor(c, r - 2) == color)
            return true;
        return false;
    }
}
