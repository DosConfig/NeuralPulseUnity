using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 터치 입력 → 체인 감지. 8방향 연결, 같은 색만.
/// </summary>
public class SwipeHandler
{
    private OrbGrid _grid;
    private List<Vector2Int> _chain = new List<Vector2Int>();
    private bool _swiping;

    public List<Vector2Int> Chain => _chain;
    public bool IsSwiping => _swiping;

    public SwipeHandler(OrbGrid grid)
    {
        _grid = grid;
    }

    /// <summary>드래그 시작. 그리드 좌표(col, row).</summary>
    public void OnDragStart(int col, int row)
    {
        _chain.Clear();
        _swiping = false;

        var orb = _grid.GetOrb(col, row);
        if (orb == null) return;

        _swiping = true;
        _chain.Add(new Vector2Int(col, row));
        orb.Selected = true;
    }

    /// <summary>드래그 이동. 새 셀이 체인에 추가될 수 있으면 추가.</summary>
    public void OnDragUpdate(int col, int row)
    {
        if (!_swiping || _chain.Count == 0) return;

        var pos = new Vector2Int(col, row);

        // 이미 체인에 있으면 — 되돌아가기 체크
        int existIdx = _chain.IndexOf(pos);
        if (existIdx >= 0 && existIdx == _chain.Count - 2)
        {
            // 한 칸 뒤로 (언두)
            var removed = _chain[_chain.Count - 1];
            var removedOrb = _grid.GetOrb(removed.x, removed.y);
            if (removedOrb != null) removedOrb.Selected = false;
            _chain.RemoveAt(_chain.Count - 1);
            return;
        }
        if (existIdx >= 0) return; // 이미 체인에 있는 다른 위치

        // 마지막 셀과 인접한지 확인 (8방향)
        var last = _chain[_chain.Count - 1];
        if (!IsAdjacent(last, pos)) return;

        // 벽 체크
        if (HasWallBetween(last.x, last.y, col, row)) return;

        // 같은 색인지 확인
        var firstOrb = _grid.GetOrb(_chain[0].x, _chain[0].y);
        var newOrb = _grid.GetOrb(col, row);
        if (firstOrb == null || newOrb == null) return;
        if (newOrb.Color != firstOrb.Color) return;

        _chain.Add(pos);
        newOrb.Selected = true;
    }

    /// <summary>드래그 종료. 체인 길이 2+ 이면 유효.</summary>
    public SwipeResult OnDragEnd()
    {
        // 선택 해제
        foreach (var pos in _chain)
        {
            var orb = _grid.GetOrb(pos.x, pos.y);
            if (orb != null) orb.Selected = false;
        }

        var result = new SwipeResult
        {
            Chain = new List<Vector2Int>(_chain),
            IsValid = _chain.Count >= 2,
            IsLoop = DetectLoop(),
        };

        _swiping = false;
        _chain.Clear();
        return result;
    }

    /// <summary>체인 취소 (터치 영역 이탈 등).</summary>
    public void Cancel()
    {
        foreach (var pos in _chain)
        {
            var orb = _grid.GetOrb(pos.x, pos.y);
            if (orb != null) orb.Selected = false;
        }
        _chain.Clear();
        _swiping = false;
    }

    // ====== 내부 ======

    private bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx <= 1 && dy <= 1 && (dx + dy) > 0;
    }

    private bool HasWallBetween(int c1, int r1, int c2, int r2)
    {
        var cell = _grid.Cells[c1, r1];
        int dx = c2 - c1; // -1, 0, +1
        int dy = r2 - r1;

        int dir = DirectionIndex(dx, dy);
        if (dir < 0) return false;

        return cell.HasWall(dir);
    }

    /// <summary>dx, dy → 8방향 인덱스. N=0, NE=1, E=2, SE=3, S=4, SW=5, W=6, NW=7.</summary>
    private int DirectionIndex(int dx, int dy)
    {
        if (dx == 0 && dy == 1) return 0;  // N
        if (dx == 1 && dy == 1) return 1;  // NE
        if (dx == 1 && dy == 0) return 2;  // E
        if (dx == 1 && dy == -1) return 3; // SE
        if (dx == 0 && dy == -1) return 4; // S
        if (dx == -1 && dy == -1) return 5;// SW
        if (dx == -1 && dy == 0) return 6; // W
        if (dx == -1 && dy == 1) return 7; // NW
        return -1;
    }

    private bool DetectLoop()
    {
        if (_chain.Count < 3) return false;
        // 마지막 셀이 첫 셀과 인접하면 루프
        return IsAdjacent(_chain[_chain.Count - 1], _chain[0]);
    }
}

public class SwipeResult
{
    public List<Vector2Int> Chain;
    public bool IsValid;
    public bool IsLoop;
}
