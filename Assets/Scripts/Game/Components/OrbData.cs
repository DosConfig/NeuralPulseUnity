using UnityEngine;

/// <summary>
/// 오브 하나의 데이터. 색상, 위치, 애니메이션 상태.
/// 렌더링과 분리된 순수 데이터.
/// </summary>
public class OrbData
{
    public enum OrbColor { Red, Blue, Green, Yellow, Purple, Orange, Cyan }

    public OrbColor Color;
    public int Col, Row;

    // 드롭 애니메이션
    public float TargetY;
    public float CurrentY;
    public float VelocityY;
    public bool Dropping;

    // 시각 상태
    public bool Selected;
    public float PopTimer;    // > 0 이면 팝 애니메이션 중
    public float PopDelay;    // 스태거 딜레이

    // 특수 상태
    public bool IsBomb;
    public bool Locked;

    // 스쿼시/스트레치 (바운스)
    public float Squash;      // 1.0 = 정상, < 1.0 = 납작

    public OrbData(OrbColor color, int col, int row)
    {
        Color = color;
        Col = col;
        Row = row;
        CurrentY = row;
        TargetY = row;
        Squash = 1f;
    }

    /// <summary>드롭 물리 업데이트. true면 아직 떨어지는 중.</summary>
    public bool UpdateDrop(float dt, float gravity = 30f, float bounceDamp = 0.4f)
    {
        if (!Dropping) return false;

        VelocityY += gravity * dt;
        CurrentY -= VelocityY * dt;

        if (CurrentY <= TargetY)
        {
            CurrentY = TargetY;
            if (VelocityY > 2f)
            {
                // 바운스
                VelocityY = -VelocityY * bounceDamp;
                Squash = Mathf.Max(0.7f, 1f - VelocityY * 0.02f);
                return true;
            }
            else
            {
                VelocityY = 0;
                Dropping = false;
                Squash = 1f;
                return false;
            }
        }

        return true;
    }

    /// <summary>스쿼시 복원 (프레임마다 호출).</summary>
    public void UpdateSquash(float dt, float restoreSpeed = 8f)
    {
        if (Squash < 1f)
            Squash = Mathf.MoveTowards(Squash, 1f, restoreSpeed * dt);
    }

    public static UnityEngine.Color ToUnityColor(OrbColor c)
    {
        switch (c)
        {
            case OrbColor.Red:    return new UnityEngine.Color(0.95f, 0.25f, 0.25f);
            case OrbColor.Blue:   return new UnityEngine.Color(0.25f, 0.50f, 0.95f);
            case OrbColor.Green:  return new UnityEngine.Color(0.20f, 0.85f, 0.40f);
            case OrbColor.Yellow: return new UnityEngine.Color(0.95f, 0.85f, 0.20f);
            case OrbColor.Purple: return new UnityEngine.Color(0.70f, 0.30f, 0.90f);
            case OrbColor.Orange: return new UnityEngine.Color(0.95f, 0.55f, 0.15f);
            case OrbColor.Cyan:   return new UnityEngine.Color(0.15f, 0.90f, 0.90f);
            default:              return UnityEngine.Color.white;
        }
    }
}
