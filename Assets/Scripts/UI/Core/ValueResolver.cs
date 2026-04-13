using UnityEngine;

/// <summary>
/// JSON 사이즈 표기법 → float 변환기.
/// "sw:0.9" → SafeWidth * 0.9, "sh:0.05" → CanvasH * 0.05, "cu:12" → 12f
/// </summary>
public static class ValueResolver
{
    /// <summary>
    /// 사이즈 표현식을 cu 값으로 변환.
    /// parentW/parentH: pw:/ph: 해석용. cellW/cellH: cw:/ch: 해석용.
    /// </summary>
    public static float Resolve(string expr, float parentW = 0, float parentH = 0,
                                 float cellW = 0, float cellH = 0)
    {
        if (string.IsNullOrEmpty(expr)) return 0f;

        // "auto" sentinel
        if (expr == "auto") return -1f;

        // "expand" sentinel
        if (expr == "expand") return -2f;

        // 수식 (A + B 또는 A - B)
        int opIdx = FindOperator(expr);
        if (opIdx > 0)
        {
            string left = expr.Substring(0, opIdx).Trim();
            char op = expr[opIdx];
            string right = expr.Substring(opIdx + 1).Trim();
            float lv = Resolve(left, parentW, parentH, cellW, cellH);
            float rv = Resolve(right, parentW, parentH, cellW, cellH);
            return op == '+' ? lv + rv : lv - rv;
        }

        // 접두사 기반 해석
        if (expr.StartsWith("sw:"))  return UIHelper.SW(ParseFloat(expr, 3));
        if (expr.StartsWith("sh:"))  return UIHelper.SH(ParseFloat(expr, 3));
        if (expr.StartsWith("cu:"))  return ParseFloat(expr, 3);
        if (expr.StartsWith("pw:"))  return parentW * ParseFloat(expr, 3);
        if (expr.StartsWith("ph:"))  return parentH * ParseFloat(expr, 3);
        if (expr.StartsWith("cw:"))  return cellW * ParseFloat(expr, 3);
        if (expr.StartsWith("ch:"))  return cellH * ParseFloat(expr, 3);
        if (expr.StartsWith("rw:"))  return ParseFloat(expr, 3); // row weight — 호출자가 해석
        if (expr.StartsWith("font:")) return UIHelper.Font(ParseFloat(expr, 5));

        // 특수 키워드
        if (expr == "halfH") return UIHelper.HalfH;
        if (expr == "halfW") return UIHelper.HalfW;
        if (expr == "safew") return UIHelper.SafeW;

        // 토큰 (Layout.TopBarH 등)
        if (UITheme.Layout.TryResolve(expr, out float layoutVal))
            return UIHelper.SH(layoutVal);
        if (UITheme.Spacing.TryResolve(expr, out float spacingVal))
            return spacingVal;

        // 숫자 리터럴
        if (float.TryParse(expr, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out float num))
            return num;

        Debug.LogWarning($"[ValueResolver] Cannot resolve: \"{expr}\"");
        return 0f;
    }

    /// <summary>rw: 접두사인지 확인 (Row weight)</summary>
    public static bool IsRowWeight(string expr)
    {
        return !string.IsNullOrEmpty(expr) && expr.StartsWith("rw:");
    }

    /// <summary>rw:1.5 → 1.5f</summary>
    public static float ParseRowWeight(string expr)
    {
        if (IsRowWeight(expr)) return ParseFloat(expr, 3);
        return 1f;
    }

    private static float ParseFloat(string s, int startIndex)
    {
        if (startIndex >= s.Length) return 0f;
        if (float.TryParse(s.Substring(startIndex),
                           System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out float v))
            return v;
        return 0f;
    }

    /// <summary>수식에서 +/- 연산자 위치 찾기 (첫 번째 토큰 뒤의 연산자)</summary>
    private static int FindOperator(string expr)
    {
        // "halfH - sh:0.08" → 6번째 인덱스의 '-'
        // 첫 토큰(접두사:값 또는 키워드) 이후의 +/- 찾기
        bool passedFirst = false;
        for (int i = 1; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c == ' ') { passedFirst = true; continue; }
            if (passedFirst && (c == '+' || c == '-'))
                return i;
        }
        return -1;
    }
}
