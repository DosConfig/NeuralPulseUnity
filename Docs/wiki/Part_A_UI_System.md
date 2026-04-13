# Part A — UI System

> JSON 선언적 UI + Code-only 렌더링. SDFKit 의존성 없음.
> 이 문서는 Claude Code가 스크린을 만들거나 수정할 때 참조하는 레퍼런스.

---

## §1 스크린 생성 패턴

모든 스크린은 동일한 5단계 패턴을 따른다.

### 파일 구성

```
Assets/Resources/UI/Screens/xxx_screen.json   ← 시각 구조 선언
Assets/Scripts/UI/Screens/XxxScreen.cs         ← 데이터 바인딩 + 콜백
```

파일명 규칙: JSON은 snake_case, C#은 PascalCase. 스크린명이 일치해야 함.

### C# 패턴 (모든 스크린 동일)

```csharp
public class XxxScreen : MonoBehaviour
{
    private UIScreenBuilder _builder;

    public static GameObject Create(Transform parent)
    {
        // 1. JSON 로드
        var builder = UIScreenBuilder.Load("UI/Screens/xxx_screen", parent);

        // 2. 바인딩 (Build 전)
        builder.SetBinding("playerName", "Hero");
        builder.SetBinding("showVip", false);

        // 3. 액션 등록 (Build 전)
        XxxScreen comp = null;
        builder.RegisterAction("onClose", () => comp?.Close());
        builder.RegisterFloatAction("onVolume", (v) => comp?.SetVolume(v));

        // 4. 빌드
        var screen = builder.Build();

        // 5. MonoBehaviour 부착 + 참조 캐시
        comp = screen.AddComponent<XxxScreen>();
        comp._builder = builder;

        comp.InitUI();
        return screen;
    }

    private void InitUI()
    {
        // 빌드 후 추가 설정 (애니메이션, 직접 컴포넌트 접근 등)
    }

    private void OnDestroy()
    {
        _builder?.Cleanup();
    }
}
```

### 순서 규칙 (절대)

1. `Load` → 2. `SetBinding` → 3. `RegisterAction` → 4. `Build` → 5. `AddComponent`

Build 전에 바인딩/액션을 등록해야 JSON 내 `$key`와 `action` 이름이 빌드 시 연결됨.
Build 후 SetBinding 호출은 reactive 갱신 (text/visible만).

---

## §2 JSON 구조

### 스크린 JSON 최상위

```json
{
  "screen": "ScreenName",
  "convention": "standard",
  "bg": "Bg.Dark",
  "elements": [ ... ]
}
```

| 필드 | 필수 | 설명 |
|------|------|------|
| screen | O | 스크린 이름 (GameObject 이름) |
| convention | - | "standard" 권장 |
| bg | - | 배경색 토큰 |
| elements | O | 최상위 요소 배열 |

### 요소(Element) 공통 속성

```json
{
  "id": "myElement",
  "type": "text",
  "x": "cu:10",
  "y": "cu:-20",
  "w": "sw:0.9",
  "h": "sh:0.05",
  "anchor": "TC",
  "visible": "$isVisible",
  "margin": "cu:5"
}
```

| 속성 | 설명 | 기본값 |
|------|------|--------|
| id | 요소 식별자. GetElement(id)로 조회 | (없음) |
| type | 요소 타입 (§3 참조) | **필수** |
| x, y | 위치. Column/Row 안에서는 무시됨 | 0 |
| w, h | 크기. 표기법은 §4 참조 | 타입별 기본값 |
| anchor | 앵커 프리셋: TL/TC/TR/ML/MC/MR/BL/BC/BR | (부모 따라감) |
| visible | 가시성. "$key" 또는 "key=value" | true |
| margin | 외부 마진 | 0 |

---

## §3 요소 타입

### 지원 타입 (10종)

| 타입 | 용도 | 주요 속성 |
|------|------|----------|
| **text** | 텍스트 표시 | text, font, color, align, fontStyle |
| **button** | 클릭 버튼 | label, action, buttonType |
| **panel** | 배경 컨테이너 | bg, children |
| **image** | 이미지 표시 | sprite, fit |
| **column** | 세로 스택 | gap, padding, children |
| **row** | 가로 스택 | gap, padding, children, weights |
| **scroll** | 스크롤 영역 | direction, children |
| **slider** | 값 슬라이더 | action (float 콜백) |
| **progressbar** | 진행 바 | (C#에서 Fill anchorMax 직접 조절) |
| **foreach** | 데이터 반복 | dataSource, template, layout, cols |

### text

```json
{
  "type": "text",
  "text": "$score",
  "font": "Headline.Large",
  "color": "Text.Primary",
  "align": "center",
  "fontStyle": "bold"
}
```

| 속성 | 값 | 기본값 |
|------|-----|--------|
| text | 문자열 또는 "$바인딩키" | "" |
| font | 타이포 토큰 (§7) 또는 소수 ("0.026") | Body.Medium |
| color | 컬러 토큰 (§6) 또는 "#RRGGBB" | Text.Primary |
| align | "left", "center", "right" | center |
| fontStyle | "bold", "italic", "bolditalic" | normal |
| outlineColor | 외곽선 색 | (없음) |
| outlineWidth | 외곽선 두께 | (없음) |

**바인딩**: `"text": "$score"` → `builder.SetBinding("score", "1,234")` → 빌드 시 및 이후 reactive 갱신.

### button

```json
{
  "type": "button",
  "label": "Play",
  "action": "onPlay",
  "buttonType": "confirm"
}
```

| 속성 | 값 |
|------|-----|
| label | 버튼 텍스트 |
| action | RegisterAction 이름 |
| buttonType | confirm, dismiss, danger, nav, ghost, disabled |

buttonType → 색상 자동 매핑:
- confirm/primary → BtnConfirm (초록)
- dismiss/close/ghost → BtnGhost (반투명)
- danger → BtnDanger (빨강)
- nav/secondary → BtnNav (어두운 회색)
- disabled → BtnDisabled (더 어두운 회색)

### panel

```json
{
  "type": "panel",
  "bg": "Bg.Panel",
  "w": "sw:0.9",
  "h": "sh:0.3",
  "children": [ ... ]
}
```

배경 컨테이너. bg가 없으면 투명. children 안의 요소는 자유 배치 (Column/Row와 다름).

### image

```json
{
  "type": "image",
  "sprite": "Sprites/UI/logo",
  "w": "sw:0.4",
  "h": "sw:0.4",
  "fit": "contain"
}
```

| fit | 동작 |
|-----|------|
| (기본) | preserveAspect=true |
| stretch | 부모 크기에 맞춰 늘림 |
| contain | AspectRatioFitter.FitInParent |
| cover | AspectRatioFitter.EnvelopeParent |

sprite 경로는 Resources 상대경로 (확장자 제외).

### column

```json
{
  "type": "column",
  "w": "sw:0.95",
  "h": "expand",
  "gap": "Gap.md",
  "padding": "Padding.lg 0",
  "children": [
    { "type": "text", "h": "auto", "text": "Title" },
    { "type": "panel", "h": "expand" },
    { "type": "button", "h": "sh:0.04", "label": "OK" }
  ]
}
```

**핵심 규칙:**
- 자식의 y 좌표는 **무시됨** — Column이 위에서 아래로 배치
- `h: "expand"` 자식은 남은 공간을 차지. **컨테이너당 1개만**
- gap: 자식 사이 간격 (cu 또는 토큰)
- padding: "top right bottom left" 또는 단일 값

### row

```json
{
  "type": "row",
  "h": "sh:0.04",
  "gap": "Gap.sm",
  "children": [
    { "type": "button", "w": "rw:2", "label": "Confirm" },
    { "type": "button", "w": "rw:1", "label": "Cancel" }
  ]
}
```

**핵심 규칙:**
- 자식의 x 좌표는 **무시됨** — Row가 왼쪽에서 오른쪽으로 배치
- 너비는 weight 기반: `"w": "rw:2"` = weight 2
- 총 weight 3이면: Confirm = 2/3 너비, Cancel = 1/3 너비
- visible=false인 자식은 weight 계산에서 제외됨
- `weights` 배열도 가능: `"weights": [2, 1, 1]`

### scroll

```json
{
  "type": "scroll",
  "h": "expand",
  "direction": "vertical",
  "gap": "Gap.md",
  "children": [ ... ]
}
```

- direction: "vertical" (기본) 또는 "horizontal"
- children은 Content 안에 자동 배치
- 콘텐츠 사이즈는 자식 합산으로 자동 계산
- **주의**: scroll 자식에 `h: "expand"` 쓰면 content=viewport → 스크롤 안 됨

### slider

```json
{
  "type": "slider",
  "w": "sw:0.6",
  "h": "sh:0.03",
  "action": "onVolumeChange"
}
```

C#: `builder.RegisterFloatAction("onVolumeChange", (v) => SetVolume(v));`

### progressbar

```json
{
  "type": "progressbar",
  "id": "timerBar",
  "w": "sw:0.8",
  "h": "cu:8"
}
```

fill 조절은 C#에서 직접:
```csharp
var bar = builder.GetElement("timerBar");
var fill = bar?.transform.Find("Fill")?.GetComponent<RectTransform>();
if (fill != null) fill.anchorMax = new Vector2(ratio, 1f); // 0~1
```

### foreach

```json
{
  "type": "foreach",
  "dataSource": "$items",
  "layout": "list",
  "cellH": "sh:0.08",
  "gap": "Gap.sm",
  "template": [
    { "type": "text", "text": "$item.name", "font": "Body.Large" },
    { "type": "text", "text": "$item.price", "color": "Status.Gold" }
  ],
  "onItemClick": "onSelectItem"
}
```

C# 데이터:
```csharp
var items = new List<Dictionary<string, object>> {
    new() { ["name"] = "Sword", ["price"] = "500 Gold" },
    new() { ["name"] = "Shield", ["price"] = "300 Gold" },
};
builder.SetBinding("items", items);
builder.RegisterIndexedAction("onSelectItem", (idx) => Select(idx));
```

| layout | 동작 |
|--------|------|
| list | 세로 1열 |
| grid | 세로 N열 (cols 필수) |
| row | 가로 1행 |

template 안에서 `$item.속성명`으로 각 아이템 데이터 접근.

---

## §4 사이즈 표기법 (ValueResolver)

| 표기 | 예시 | 의미 |
|------|------|------|
| `sw:F` | `sw:0.9` | SafeWidth(1032cu) × F |
| `sh:F` | `sh:0.05` | CanvasHeight(디바이스별) × F |
| `cu:N` | `cu:12` | 리터럴 N cu |
| `pw:F` | `pw:0.8` | 부모 너비 × F |
| `ph:F` | `ph:1.0` | 부모 높이 × F |
| `cw:F` | `cw:0.5` | foreach 셀 너비 × F |
| `ch:F` | `ch:0.5` | foreach 셀 높이 × F |
| `rw:F` | `rw:1.5` | Row weight (너비 비율) |
| `font:F` | `font:0.026` | Font(F) = ScreenW × F |
| `halfH` | `halfH` | CanvasH / 2 (화면 상단) |
| `halfW` | `halfW` | 540 (화면 우측) |
| `safew` | `safew` | SafeWidth (1032cu) |
| `expand` | `expand` | 남은 공간 (Column 안에서만) |
| `auto` | `auto` | 콘텐츠 기반 |
| 수식 | `halfH - sh:0.08` | A ± B (단일 연산자) |
| 토큰 | `Gap.md` | Spacing 토큰 (§8) |
| 토큰 | `Layout.TopBarH` | Layout 토큰 → SH(값) |
| 숫자 | `0.9` | float 리터럴 |

### 절대 금지

```
❌ 400              ← 픽셀 리터럴 (sw:/sh:/cu: 접두사 필수)
❌ Screen.width      ← C# 코드에서도 금지
❌ new Vector2(300, 95)  ← C# 코드에서도 금지
```

---

## §5 데이터 바인딩

### 텍스트 바인딩

```json
{ "type": "text", "text": "$playerName" }
```
```csharp
builder.SetBinding("playerName", "Hero");       // Build 전: 초기값
builder.SetBinding("playerName", "Hero Lv.5");  // Build 후: reactive 갱신
```

**reactive 갱신 가능: text, visible만.**

### 가시성 바인딩

```json
{ "type": "panel", "visible": "$isVip" }
```
```csharp
builder.SetBinding("isVip", true);   // 보임
builder.SetBinding("isVip", false);  // 숨김
```

동등 비교도 가능:
```json
{ "type": "panel", "visible": "mode=edit" }
```
```csharp
builder.SetBinding("mode", "edit");  // 보임
builder.SetBinding("mode", "view");  // 숨김
```

### SetBinding 한계 — 이것은 안 됨

| 속성 | SetBinding으로 갱신 | 우회 방법 |
|------|-------------------|----------|
| text | ✅ | — |
| visible | ✅ | — |
| **color** | ❌ | `GetElement("id")?.GetComponent<Text>().color = c` |
| **w, h** | ❌ | `GetElement("id")?.GetComponent<RectTransform>().sizeDelta = v` |
| **x, y** | ❌ | `GetElement("id")?.GetComponent<RectTransform>().anchoredPosition = v` |
| **sprite** | ❌ | `GetElement("id")?.GetComponent<Image>().sprite = s` |
| **font** | ❌ | `GetElement("id")?.GetComponent<Text>().fontSize = n` |

이것은 버그가 아닌 설계 한계. color/size/position 동적 변경이 필요하면 C#에서 직접 컴포넌트 접근.

### 액션 바인딩

```json
{ "type": "button", "action": "onPlay" }
```
```csharp
builder.RegisterAction("onPlay", () => StartGame());
```

| 메서드 | 용도 | JSON 속성 |
|--------|------|----------|
| RegisterAction(name, Action) | 버튼 클릭 | action |
| RegisterIndexedAction(name, Action\<int\>) | foreach 아이템 클릭 | onItemClick |
| RegisterFloatAction(name, Action\<float\>) | 슬라이더 값 변경 | action |

---

## §6 컬러 토큰

토큰 문자열 → Color 변환. `UITheme.ResolveColor("Text.Primary")`.

### 팔레트 시스템

4개 빌트인 팔레트. 런타임 교체 가능:
```csharp
UITheme.Apply(UITheme.Palettes.OceanBreeze);
```

| 팔레트 | 분위기 |
|--------|--------|
| CyberNeon | 다크 사이버펑크 (기본) |
| OceanBreeze | 차분한 해양 블루 |
| SunsetWarm | 따뜻한 석양 오렌지 |
| CleanLight | 라이트 모드 |

### 토큰 목록

**배경 (Bg.*)**
| 토큰 | CyberNeon | 용도 |
|------|-----------|------|
| Bg.Darkest | #0A0A0F | 최하위 배경 |
| Bg.Dark | #12121A | 일반 배경 |
| Bg.Panel | #1A1A2E | 패널 배경 |
| Bg.PanelDark | #141425 | 어두운 패널 |
| Bg.PanelLight | #222240 | 밝은 패널 |
| Bg.ScrollTint | #0F0F1A80 | 스크롤 배경 (반투명) |

**텍스트 (Text.*)**
| 토큰 | CyberNeon | 용도 |
|------|-----------|------|
| Text.Primary | #E8E8F0 | 주 텍스트 |
| Text.Secondary | #A0A0B8 | 보조 텍스트 |
| Text.Muted | #606078 | 비활성 텍스트 |
| Text.Disabled | #404050 | 불가능 상태 |
| Text.Warm | #FFD4A0 | 따뜻한 강조 |
| Text.Info | #80C8FF | 정보성 |

**상태 (Status.*)**
| 토큰 | CyberNeon | 용도 |
|------|-----------|------|
| Status.Success | #00E676 | 성공/완료 |
| Status.Warning | #FFD740 | 경고 |
| Status.Danger | #FF5252 | 위험/오류 |
| Status.Info | #448AFF | 정보 |
| Status.Gold | #FFD700 | 골드/보상 |

**버튼 (Btn.*)**
| 토큰 | CyberNeon | 용도 |
|------|-----------|------|
| Btn.Confirm | #00C853 | 확인/주요 액션 |
| Btn.Close | #424260 | 닫기/취소 |
| Btn.Danger | #D32F2F | 위험한 액션 |
| Btn.Nav | #303050 | 네비게이션 |
| Btn.Disabled | #2A2A3A | 비활성 |
| Btn.Ghost | rgba(255,255,255,0.08) | 투명 버튼 |

**악센트 (Accent.*)**
| 토큰 | CyberNeon |
|------|-----------|
| Accent.Primary | #00E5FF |
| Accent.Light | #80F0FF |

**오버레이 (Overlay.*)**
| 토큰 | alpha | 용도 |
|------|-------|------|
| Overlay.Dim | 0.85 | 일반 모달 |
| Overlay.Dense | 0.96 | 강한 블로킹 |
| Overlay.Light | 0.60 | 가벼운 오버레이 |

**HEX 직접 사용도 가능:**
```json
{ "color": "#FF6644" }
```

---

## §7 타이포그래피 토큰

ScreenWidth(1080cu) 대비 비율. `UIHelper.Font(비율)`.

| 토큰 | 비율 | 대략 크기 |
|------|------|----------|
| Display.Large | 0.074 | 80cu |
| Display.Medium | 0.060 | 65cu |
| Display.Small | 0.050 | 54cu |
| Headline.Large | 0.044 | 48cu |
| Headline.Medium | 0.037 | 40cu |
| Headline.Small | 0.033 | 36cu |
| Title.Large | 0.033 | 36cu |
| Title.Medium | 0.028 | 30cu |
| Title.Small | 0.024 | 26cu |
| Body.Large | 0.026 | 28cu |
| Body.Medium | 0.022 | 24cu |
| Body.Small | 0.019 | 21cu |
| Label.Large | 0.022 | 24cu |
| Label.Medium | 0.019 | 21cu |
| Label.Small | 0.017 | 18cu (최소 20cu로 클램프) |
| Caption.Large | 0.019 | 21cu |
| Caption.Medium | 0.017 | 18cu |
| Caption.Small | 0.015 | 16cu |

**Font 최소값**: 20cu. `Font(0.01)`은 20cu로 클램프됨.

---

## §8 스페이싱 토큰

### Gap (자식 간 간격)

| 토큰 | cu |
|------|-----|
| Gap.xs | 6 |
| Gap.sm | 8 |
| Gap.md | 10 |
| Gap.lg | 12 |
| Gap.xl | 16 |

### Padding (내부 여백)

| 토큰 | cu |
|------|-----|
| Padding.sm | 10 |
| Padding.md | 16 |
| Padding.lg | 24 |

### Layout (레이아웃 상수)

| 토큰 | SH 비율 | 용도 |
|------|---------|------|
| Layout.TopBarH | 0.052 | 상단 바 높이 |
| Layout.BottomBarH | 0.068 | 하단 바 높이 |
| Layout.MinButtonH | 0.038 | 최소 버튼 높이 |
| Layout.DialogW | 0.90 (SW) | 다이얼로그 너비 |

---

## §9 Canvas Unit (cu) 시스템

### 좌표계

```
중앙 = (0, 0)
X축: -540 ~ +540 cu (고정, 모든 디바이스)
Y축: -HalfH ~ +HalfH cu (디바이스별 다름)
```

### 핵심 함수 (C#)

```csharp
UIHelper.SW(0.9f)    // SafeWidth × 0.9 = 928.8cu
UIHelper.SH(0.05f)   // CanvasHeight × 0.05 (디바이스별)
UIHelper.Font(0.026f) // ScreenWidth × 0.026 = 28.08cu (최소 20cu)
UIHelper.HalfH       // CanvasH / 2 (화면 상단 Y)
UIHelper.HalfW       // 540 (화면 우측 X)
```

### 상수

| 이름 | 값 | 비고 |
|------|-----|------|
| ScreenW | 1080cu | 가로 고정 |
| SafeW | 1032cu | ScreenW - 24×2 |
| CanvasH | 디바이스별 | 가로 1080 기준 세로 비율 계산 |
| HalfH | CanvasH / 2 | 동적 |
| HalfW | 540 | 고정 |

### SafeArea

노치/홈 인디케이터 자동 처리. `UIHelper.Initialize()`에서 계산.
`UIHelper.SafeAreaInset` → Rect(left, top, right, bottom) in cu.

Screen Space - Overlay Canvas + CanvasScaler(matchWidthOrHeight=0)로
모든 디바이스에서 가로 1080cu 고정, 세로만 비율에 따라 변동.

---

## §10 알려진 제약사항

이것들은 버그가 아닌 현재 설계의 한계. 우회 방법과 함께 숙지.

| # | 제약 | 우회 |
|---|------|------|
| 1 | mainAxisAlignment 미구현 | padding + expand + 빈 panel |
| 2 | crossAxisAlignment 미구현 | 수동 w 지정 |
| 3 | Multiple expand: 1개만 | 나머지는 sh: 고정 |
| 4 | Column/Row 안 절대 좌표 무시 | panel로 감싸고 anchor |
| 5 | SetBinding: text/visible만 | C# GetComponent 직접 |
| 6 | visible 조건: = 만 | C#에서 bool 사전 계산 |
| 7 | Row expand 없음 | rw: weight 사용 |
| 8 | Wrap 없음 | row + visible 토글 |
| 9 | Stack 없음 | Unity 다중 Canvas 또는 panel + anchor |
| 10 | Transform 없음 | C# RectTransform 직접 |
| 11 | scroll 안 expand 자식 | h 생략 (auto) |
| 12 | foreach가 자체 scroll 생성 | foreach를 최외곽에 |

---

## §11 앵커 프리셋

```
TL ---- TC ---- TR
|                |
ML ---- MC ---- MR
|                |
BL ---- BC ---- BR
```

```json
{ "type": "button", "anchor": "TR", "x": "cu:-16", "y": "cu:-16" }
```

anchor 설정 시 anchorMin/anchorMax/pivot이 프리셋으로 설정됨.
x/y는 앵커 기준 오프셋.

Column/Row 자식에는 anchor가 무시됨 (컨테이너가 강제 배치).
최상위 요소나 Panel 직계 자식에서만 유효.

---

## §12 애니메이션 (SDFTween)

JSON에 애니메이션 선언 없음. 모든 애니메이션은 C#에서 코루틴으로.

### 사용 가능한 트윈

```csharp
// 스케일
StartCoroutine(SDFTween.Scale(rt, from, to, 0.3f, Easing.OutBack));

// 이동
StartCoroutine(SDFTween.Move(rt, from, to, 0.3f, Easing.OutQuad));

// 회전
StartCoroutine(SDFTween.RotateZ(rt, 0, 360, 0.5f, Easing.Linear));

// 페이드 (CanvasGroup)
StartCoroutine(SDFTween.FadeCanvasGroup(cg, 0, 1, 0.3f));

// 페이드 (Graphic)
StartCoroutine(SDFTween.FadeGraphic(img, 0, 1, 0.3f));

// Interval 페이드 (전체 시간의 일부 구간에서만)
StartCoroutine(SDFTween.IntervalFade(cg, 0, 1, 0.3f, 0f, 0.5f));

// PopIn (다이얼로그 입장: scale 0.85→1 + fade)
StartCoroutine(SDFTween.PopIn(rt, 0.85f, 0.3f, Easing.OutBack));

// PopOut (다이얼로그 퇴장)
StartCoroutine(SDFTween.PopOut(rt, 0.85f, 0.2f));

// 스프링 버튼 (누름 + 해제)
StartCoroutine(SDFTween.SpringPress(rt));
StartCoroutine(SDFTween.SpringRelease(rt));

// 지연 후 콜백
StartCoroutine(SDFTween.Delay(0.5f, () => DoSomething()));
```

### 이징 함수

| 함수 | 특성 |
|------|------|
| Linear | 일정 속도 |
| InQuad / OutQuad / InOutQuad | 가속 / 감속 / 가감속 |
| InCubic / OutCubic / InOutCubic | 더 강한 가감속 |
| InBack / OutBack | 오버슈트 (빠졌다 돌아옴) |
| OutElastic | 탄성 바운스 |
| OutBounce | 공 튀기듯 |
| SpringBack | 눌렀다 스프링 복귀 (버튼용) |
| SpringDamping | 감쇠 스프링 (슬라이드업용) |

---

## §13 UILayerManager

화면 겹침 관리. sortingOrder로 레이어 구분.

```
Layer 0: Screen        (메인 스크린)
Layer 1: ScreenOverlay (정보 패널)
Layer 2: Popup         (모달, 확인)
Layer 3: Toast         (알림)
Layer 4: Transition    (화면 전환 오버레이)
Layer 5: SystemAlert   (시스템 에러)
Layer 10: GameHUD      (게임 보드 위 HUD)
Layer 11: GameOverlay  (일시정지, 게임오버)
```

게임 씬 구조:
```
[Game Camera] → 2D SpriteRenderer 게임 보드
[HUD Canvas]  → Layer 10, JSON-driven HUD
[Overlay Canvas] → Layer 11, 다이얼로그/오버레이
```

이 구조로 Flutter의 Stack+Positioned를 대체.
