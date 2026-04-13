# Neural Pulse Unity

Match-3 퍼즐 게임. 모바일(iOS/Android), 세로 모드.
Flutter/Flame 원본에서 Unity로 포팅. Code-only UI (JSON + C#, No Prefabs).

---

## 새 대화 프로토콜

1. `Docs/DEVELOPMENT_ROADMAP.md` → 현재 Phase 확인
2. `Docs/BACKLOG.md` → 오늘 할 작업 확인
3. 이 파일 → 규칙 숙지
4. 관련 위키 읽기 (라우팅 테이블 참조)

---

## 아키텍처

```
Game (로직) ←→ Rendering (비주얼) ←→ UI (스크린/HUD)
                                         ↑
                                   Services (Firebase)
```

### 의존성 방향
- UI → Game: 공개 메서드만
- Game → UI: 콜백만
- Rendering → Game: 데이터 읽기만

### 폴더 구조
```
Assets/Scripts/
├── Game/Components/   # OrbGrid, SwipeHandler, CellData
├── Game/Managers/     # GameManager, SoundManager
├── Game/Stage/        # StageConfig, zone 데이터
├── Game/Config/       # 상수, 밸런스
├── UI/Core/           # UIHelper, UIScreenBuilder 등 (프레임워크)
├── UI/Screens/        # 각 스크린 C#
├── Utils/             # SDFTween, Easing
├── Rendering/         # GridRenderer, ParticleController
├── Services/          # Firebase, Storage
└── Editor/            # BootstrapEditor

Assets/Resources/
├── UI/Screens/        # 스크린 JSON
├── UI/Overlays/       # 오버레이 JSON
└── StageData/         # 스테이지 JSON
```

---

## 코드 컨벤션

### UI 사이징 (절대 규칙)
- ❌ `new Vector2(400f, 95f)` (픽셀 리터럴)
- ❌ `Screen.width` / `Screen.height`
- ✅ `SW()`, `SH()`, `Font()`, `HalfH`, `HalfW`
- ✅ JSON: `sw:`, `sh:`, `cu:`, `pw:`, `ph:`

### 안전 패턴
- ❌ `go.GetComponent<T>().property` (체이닝)
- ❌ `Instance.A.B.C` (딥 체이닝)
- ✅ 로컬 변수 + null 체크

### 스크린 패턴
1. `UIScreenBuilder.Load()` → 2. `SetBinding()` → 3. `RegisterAction()` → 4. `Build()` → 5. `AddComponent`

---

## 검증

C# 수정 후:
```bash
./check_errors.sh
```
PASS 없이 "완료" 보고 금지.

---

## 금지 사항

1. ❌ 요청 범위 밖 수정
2. ❌ Prefab 생성
3. ❌ 하드코딩 좌표/색상
4. ❌ check_errors.sh 없이 "완료"
5. ❌ "사용자가 확인해주세요" (직접 해결)

---

## 위키 라우팅

| 작업 | 참조 |
|------|------|
| 스크린 추가/수정 | wiki/Part_A_UI_System.md |
| 게임 오브젝트/렌더링 | wiki/Part_B_Game_Patterns.md |

---

## Unity 경로
```
/Applications/Unity/Hub/Editor/6000.3.7f1/Unity.app/Contents/MacOS/Unity
```
