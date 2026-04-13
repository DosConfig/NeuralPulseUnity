using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// OrbGrid 시각화. SpriteRenderer 기반, Code-only.
/// </summary>
public class GridRenderer : MonoBehaviour
{
    private OrbGrid _grid;
    private float _cellSize = 0.9f;
    private Vector3 _origin;

    // 스프라이트
    private Sprite _orbSprite;
    private Sprite _squareSprite;

    // 오브 렌더러 풀
    private SpriteRenderer[,] _orbRenderers;
    private SpriteRenderer[,] _bgRenderers;

    // 체인 라인
    private LineRenderer _chainLine;

    public float CellSize => _cellSize;
    public Vector3 Origin => _origin;

    public void Initialize(OrbGrid grid, float cellSize = 0.9f)
    {
        _grid = grid;
        _cellSize = cellSize;

        // 그리드 중앙 맞추기
        float totalW = grid.Cols * cellSize;
        float totalH = grid.Rows * cellSize;
        _origin = new Vector3(-totalW / 2f + cellSize / 2f, -totalH / 2f + cellSize / 2f, 0);

        // 프로시저럴 스프라이트
        _orbSprite = GameRenderer.CreateCircleSprite(64);
        _squareSprite = GameRenderer.CreateSquareSprite(4);

        // 배경 셀
        _bgRenderers = new SpriteRenderer[grid.Cols, grid.Rows];
        for (int c = 0; c < grid.Cols; c++)
        {
            for (int r = 0; r < grid.Rows; r++)
            {
                var pos = GridToWorld(c, r);
                var bgGo = new GameObject($"BG_{c}_{r}");
                bgGo.transform.SetParent(transform, false);
                bgGo.transform.localPosition = pos;
                bgGo.transform.localScale = Vector3.one * (cellSize * 0.95f);

                var sr = bgGo.AddComponent<SpriteRenderer>();
                sr.sprite = _squareSprite;
                sr.color = new Color(1, 1, 1, 0.04f);
                sr.sortingOrder = 0;
                _bgRenderers[c, r] = sr;
            }
        }

        // 오브 렌더러
        _orbRenderers = new SpriteRenderer[grid.Cols, grid.Rows];
        for (int c = 0; c < grid.Cols; c++)
        {
            for (int r = 0; r < grid.Rows; r++)
            {
                var go = new GameObject($"Orb_{c}_{r}");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * (cellSize * 0.8f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _orbSprite;
                sr.sortingOrder = 5;
                _orbRenderers[c, r] = sr;
            }
        }

        // 체인 라인
        _chainLine = GameRenderer.CreateLine(transform, "ChainLine",
            new Color(1, 1, 1, 0.4f), cellSize * 0.08f, 10);
    }

    void Update()
    {
        if (_grid == null) return;

        for (int c = 0; c < _grid.Cols; c++)
        {
            for (int r = 0; r < _grid.Rows; r++)
            {
                var orb = _grid.GetOrb(c, r);
                var sr = _orbRenderers[c, r];
                var cell = _grid.Cells[c, r];

                if (orb == null || cell.Steel)
                {
                    sr.enabled = false;

                    // 장애물 표시
                    if (cell.BrickLayers > 0)
                    {
                        _bgRenderers[c, r].color = new Color(0.6f, 0.4f, 0.2f, 0.3f + cell.BrickLayers * 0.15f);
                    }
                    else if (cell.GlassLayers > 0)
                    {
                        _bgRenderers[c, r].color = new Color(0.4f, 0.7f, 1f, 0.15f + cell.GlassLayers * 0.1f);
                    }
                    else if (cell.Steel)
                    {
                        _bgRenderers[c, r].color = new Color(0.5f, 0.5f, 0.55f, 0.4f);
                    }
                    continue;
                }

                sr.enabled = true;
                sr.color = OrbData.ToUnityColor(orb.Color);

                // 위치 (드롭 애니메이션)
                var pos = GridToWorld(c, Mathf.Max(orb.CurrentY, 0));
                sr.transform.localPosition = pos;

                // 선택 하이라이트
                if (orb.Selected)
                {
                    sr.transform.localScale = Vector3.one * (_cellSize * 0.9f);
                    var col = sr.color;
                    col.a = 1f;
                    sr.color = col;
                }
                else
                {
                    sr.transform.localScale = Vector3.one * (_cellSize * 0.8f * orb.Squash);
                }

                // 드롭 물리
                orb.UpdateDrop(Time.deltaTime);
                orb.UpdateSquash(Time.deltaTime);
            }
        }
    }

    /// <summary>체인 라인 표시.</summary>
    public void ShowChainLine(List<Vector2Int> chain)
    {
        if (chain == null || chain.Count < 2)
        {
            _chainLine.positionCount = 0;
            return;
        }

        _chainLine.positionCount = chain.Count;
        for (int i = 0; i < chain.Count; i++)
        {
            _chainLine.SetPosition(i, GridToWorld(chain[i].x, chain[i].y) + transform.position);
        }

        // 체인 색
        var orb = _grid.GetOrb(chain[0].x, chain[0].y);
        if (orb != null)
        {
            var col = OrbData.ToUnityColor(orb.Color);
            col.a = 0.6f;
            _chainLine.startColor = col;
            _chainLine.endColor = col;
        }
    }

    public void HideChainLine()
    {
        _chainLine.positionCount = 0;
    }

    /// <summary>그리드 좌표 → 월드 로컬 좌표.</summary>
    public Vector3 GridToWorld(int col, float row)
    {
        return _origin + new Vector3(col * _cellSize, row * _cellSize, 0);
    }

    /// <summary>월드 좌표 → 그리드 좌표.</summary>
    public (int col, int row) WorldToGrid(Vector3 worldPos)
    {
        Vector3 local = worldPos - transform.position - _origin;
        int col = Mathf.RoundToInt(local.x / _cellSize);
        int row = Mathf.RoundToInt(local.y / _cellSize);
        return (Mathf.Clamp(col, 0, _grid.Cols - 1),
                Mathf.Clamp(row, 0, _grid.Rows - 1));
    }
}
