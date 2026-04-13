#!/bin/bash
# Unity AI GameDev Framework — Build & Safety Check
# 사용법: ./check_errors.sh [Unity 경로] [프로젝트 경로]
#
# Unity 경로 자동 탐색 순서:
# 1. 첫 번째 인자
# 2. UNITY_PATH 환경변수
# 3. /Applications/Unity/Hub/Editor/*/Unity.app 자동 탐색

set -e

# --- Unity 경로 결정 ---
if [ -n "$1" ]; then
    UNITY="$1"
elif [ -n "$UNITY_PATH" ]; then
    UNITY="$UNITY_PATH"
else
    # macOS Hub 자동 탐색 (최신 버전 우선)
    UNITY=$(find /Applications/Unity/Hub/Editor -name "Unity" -path "*/MacOS/Unity" 2>/dev/null | sort -rV | head -1)
    if [ -z "$UNITY" ]; then
        echo "❌ Unity를 찾을 수 없습니다. UNITY_PATH를 설정하거나 첫 번째 인자로 전달하세요."
        exit 1
    fi
fi

PROJECT="${2:-$(pwd)}"
LOG="/tmp/unity_compile_check.log"
FAIL=0

echo "Unity: $UNITY"
echo "Project: $PROJECT"
echo ""

# === Phase 1: Unity Headless Compile ===
echo "=== Phase 1: Unity Headless Compile ==="
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
    -logFile "$LOG" -quit 2>/dev/null

if grep -q "error CS" "$LOG"; then
    echo "❌ COMPILE FAILED"
    grep "error CS" "$LOG" | head -10
    FAIL=1
else
    echo "✅ Compile PASS"
fi

# === Phase 2: Safety Pattern Check ===
echo ""
echo "=== Phase 2: Safety Pattern Check ==="

# NP001: GetComponent 체이닝
HITS=$(grep -rn '\.GetComponent<[^>]*>()\.' "$PROJECT/Assets/Scripts/" --include="*.cs" 2>/dev/null | grep -v '//' | head -5)
if [ -n "$HITS" ]; then
    echo "❌ NP001: GetComponent chaining"
    echo "$HITS"
    FAIL=1
fi

# NP002: Singleton 딥 체이닝 (Instance.A.B 이상)
HITS=$(grep -rn 'Instance\.[A-Z][a-z]*\.[A-Z]' "$PROJECT/Assets/Scripts/" --include="*.cs" 2>/dev/null | grep -v '//' | head -5)
if [ -n "$HITS" ]; then
    echo "❌ NP002: Singleton deep chaining"
    echo "$HITS"
    FAIL=1
fi

# NP003: 픽셀 리터럴 in UI (3자리 이상 숫자)
HITS=$(grep -rn 'new Vector2([0-9]\{3,\}' "$PROJECT/Assets/Scripts/UI/" --include="*.cs" 2>/dev/null | grep -v '//' | head -5)
if [ -n "$HITS" ]; then
    echo "❌ NP003: Pixel literal in UI code (use SW/SH/cu)"
    echo "$HITS"
    FAIL=1
fi

# NP004: Screen.width/height
HITS=$(grep -rn 'Screen\.\(width\|height\)' "$PROJECT/Assets/Scripts/" --include="*.cs" 2>/dev/null | grep -v '//' | head -5)
if [ -n "$HITS" ]; then
    echo "❌ NP004: Screen.width/height (use CanvasH/SW/SH)"
    echo "$HITS"
    FAIL=1
fi

# NP005: 직접 캐스팅
HITS=$(grep -rn '([A-Z][a-zA-Z]*)\s*[a-z]' "$PROJECT/Assets/Scripts/" --include="*.cs" 2>/dev/null | grep -E '\((Transform|RectTransform|Image|Text|Button)\)' | grep -v '//' | head -5)
if [ -n "$HITS" ]; then
    echo "❌ NP005: Direct casting (use 'as' + null check)"
    echo "$HITS"
    FAIL=1
fi

if [ $FAIL -eq 0 ]; then
    echo "✅ Safety Check PASS"
fi

# === Phase 3: JSON Validation ===
echo ""
echo "=== Phase 3: JSON Validation ==="

JSON_FAIL=0
for dir in "UI/Screens" "UI/Overlays"; do
    FULL="$PROJECT/Assets/Resources/$dir"
    if [ -d "$FULL" ]; then
        for f in "$FULL"/*.json; do
            if [ -f "$f" ]; then
                python3 -c "import json; json.load(open('$f'))" 2>/dev/null
                if [ $? -ne 0 ]; then
                    echo "❌ Invalid JSON: $f"
                    JSON_FAIL=1
                    FAIL=1
                fi
            fi
        done
    fi
done

if [ $JSON_FAIL -eq 0 ]; then
    echo "✅ JSON Validation PASS"
fi

# === Result ===
echo ""
if [ $FAIL -eq 0 ]; then
    echo "========================================="
    echo "  ALL CHECKS PASSED ✅"
    echo "========================================="
    # dirty flag 제거
    rm -f /tmp/.np_cs_dirty
    exit 0
else
    echo "========================================="
    echo "  CHECKS FAILED ❌"
    echo "========================================="
    exit 1
fi
