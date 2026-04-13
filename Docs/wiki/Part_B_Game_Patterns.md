# Part B — Game Patterns (Code-only)

> Prefab 없이 게임 오브젝트를 생성/관리하는 패턴.
> GameRenderer.cs 사용법 + 일반적 게임 아키텍처.

---

## §1 게임 렌더링 기본

### SpriteRenderer Code-only 생성

```csharp
// 프로시저럴 스프라이트 (런타임 생성, 에셋 불필요)
Sprite circleSprite = GameRenderer.CreateCircleSprite(64);
Sprite squareSprite = GameRenderer.CreateSquareSprite(4);

// 오브젝트 생성
var orb = GameRenderer.CreateSprite(
    parent: gridTransform,
    name: "Orb_0_0",
    sprite: circleSprite,
    color: Color.red,
    position: new Vector3(x, y, 0),
    scale: 0.8f,
    sortingOrder: 5
);
```

### 카메라 설정

```csharp
var cam = GameRenderer.Setup2DCamera(
    orthoSize: 6f,
    bgColor: new Color(0.04f, 0.04f, 0.06f)
);
```

### 라인 렌더러 (스와이프, 체인)

```csharp
var line = GameRenderer.CreateLine(
    parent: transform,
    name: "SwipeLine",
    color: new Color(1, 1, 1, 0.5f),
    width: 0.08f
);

// 업데이트
line.positionCount = points.Count;
line.SetPositions(points.ToArray());
```

---

## §2 오브젝트 풀링

### 풀 생성

```csharp
// 팩토리: 오브 1개 만드는 함수
var orbPool = new GameRenderer.Pool(
    factory: () => {
        var go = new GameObject("Orb");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circleSprite;
        return go;
    },
    parent: poolParent,
    preWarm: 30  // 미리 30개 생성
);
```

### 사용

```csharp
// 풀에서 가져오기
var orb = orbPool.Get();
orb.transform.position = new Vector3(x, y, 0);
orb.GetComponent<SpriteRenderer>().color = orbColor;

// 반납
orbPool.Return(orb);

// 전체 반납
orbPool.ReturnAll(activeOrbs);
```

### 풀링 대상

| 오브젝트 | 풀 여부 | 이유 |
|---------|---------|------|
| 오브 (그리드 셀) | ✅ | 매칭/드롭 시 빈번한 생성/파괴 |
| 파티클 | ✅ | 매 매칭마다 다수 생성 |
| UI 리스트 셀 | ✅ | ListView 가상화 |
| 배경/벽 | ❌ | 한 번 생성, 스테이지 동안 유지 |
| HUD 텍스트 | ❌ | 고정 요소 |

---

## §3 파티클 시스템

### Code-only ParticleSystem

```csharp
var ps = GameRenderer.CreateParticleSystem(
    parent: effectsParent,
    name: "PopEffect",
    startColor: Color.yellow,
    startSize: 0.3f,
    startLifetime: 0.8f,
    maxParticles: 50,
    gravityModifier: 0.5f
);
```

### 버스트 발사

```csharp
GameRenderer.EmitBurst(ps, worldPosition, count: 12, 
    color: orbColor, speed: 3f);
```

### 이펙트 타입별 설정 예시

```csharp
// 매칭 폭발 (화려, 퍼짐)
var popPS = GameRenderer.CreateParticleSystem(parent, "Pop",
    startColor: Color.white, startSize: 0.4f, startLifetime: 0.6f,
    maxParticles: 100, gravityModifier: 0.8f);

// 착지 먼지 (작고 빠르게 사라짐)
var dustPS = GameRenderer.CreateParticleSystem(parent, "Dust",
    startColor: new Color(0.8f, 0.8f, 0.7f, 0.5f),
    startSize: 0.15f, startLifetime: 0.3f,
    maxParticles: 20, gravityModifier: 0.2f);

// 보상 반짝임 (위로 올라감)
var sparkPS = GameRenderer.CreateParticleSystem(parent, "Spark",
    startColor: Color.yellow, startSize: 0.2f, startLifetime: 1.2f,
    maxParticles: 30, gravityModifier: -0.3f);
```

---

## §4 게임 아키텍처 패턴

### 레이어 분리

```
GameManager         ← 상태 머신, 턴 관리, 승리/패배 판정
  ↓ (데이터)
GridLogic           ← 그리드 데이터, 매칭 알고리즘, 캐스케이드
  ↓ (이벤트)
GridRenderer        ← SpriteRenderer 배치, 드롭 애니메이션
  ↓ (이벤트)
ParticleController  ← 이펙트 발사
SoundController     ← 사운드 재생
```

### 콜백 패턴 (레이어 간 통신)

```csharp
// GameManager → UI (콜백)
public Action<int> onScoreChanged;
public Action<GameState> onStateChanged;

// UI → GameManager (공개 메서드)
public void StartStage(int stageId) { ... }
public void Pause() { ... }
public void UseTurn() { ... }
```

### MonoBehaviour vs 순수 C# 클래스

| 용도 | 선택 | 이유 |
|------|------|------|
| 게임 루프 (Update) | MonoBehaviour | Time.deltaTime 필요 |
| 그리드 데이터 | 순수 C# | 유닛 테스트 용이 |
| 매칭 로직 | 순수 C# | 유닛 테스트 용이 |
| 렌더러 | MonoBehaviour | Transform 조작 |
| 스테이지 데이터 | 순수 C# + JSON | 데이터 드리븐 |
| 설정 | 순수 C# + PlayerPrefs | 영속성 |

### 상태 머신 패턴

```csharp
public enum GameState
{
    Ready,         // 스테이지 로드됨, 시작 전
    Playing,       // 활성 게임플레이
    BonusRound,    // 보너스 턴 소화 중
    StageComplete, // 골 달성
    GameOver,      // 패배
}

// 전이 규칙 (이것만 허용)
// Ready → Playing
// Playing → StageComplete | GameOver | BonusRound
// BonusRound → StageComplete
// StageComplete → Ready (다음 스테이지)
// GameOver → Ready (재시작)
```

---

## §5 오디오 패턴 (Code-only)

### AudioSource 생성

```csharp
public class SoundManager : MonoBehaviour
{
    private AudioSource _bgmSource;
    private AudioSource _sfxSource;

    void Awake()
    {
        // BGM (루프)
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.volume = 0.2f;
        _bgmSource.playOnAwake = false;

        // SFX (원샷)
        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;
    }

    public void PlaySFX(string path, float volume = 1f)
    {
        var clip = Resources.Load<AudioClip>(path);
        if (clip != null) _sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayBGM(string path)
    {
        var clip = Resources.Load<AudioClip>(path);
        if (clip == null) return;
        _bgmSource.clip = clip;
        _bgmSource.Play();
    }

    public void StopBGM() => _bgmSource.Stop();
}
```

### 볼륨 계층 (게임에서 일반적)

| 계층 | 볼륨 범위 | 원칙 |
|------|-----------|------|
| BGM | 0.15~0.25 | SFX를 덮지 않음 |
| 앰비언스 | 0.05~0.10 | 무의식적 인지 |
| UI | 0.20~0.35 | 가볍고 선명 |
| 게임 이펙트 | 0.30~0.70 | 핵심 피드백 |
| 보스/클리어 | 0.55~0.75 | 최대 임팩트 |

---

## §6 입력 처리 패턴

### 터치 입력 (모바일)

```csharp
void Update()
{
    if (Input.touchCount == 0) return;

    Touch touch = Input.GetTouch(0);
    Vector3 worldPos = Camera.main.ScreenToWorldPoint(touch.position);
    worldPos.z = 0;

    switch (touch.phase)
    {
        case TouchPhase.Began:
            OnDragStart(worldPos);
            break;
        case TouchPhase.Moved:
            OnDragUpdate(worldPos);
            break;
        case TouchPhase.Ended:
        case TouchPhase.Canceled:
            OnDragEnd();
            break;
    }
}

// 에디터 마우스 호환
#if UNITY_EDITOR
void Update()
{
    Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
    worldPos.z = 0;

    if (Input.GetMouseButtonDown(0)) OnDragStart(worldPos);
    else if (Input.GetMouseButton(0)) OnDragUpdate(worldPos);
    else if (Input.GetMouseButtonUp(0)) OnDragEnd();
}
#endif
```

### 그리드 좌표 변환

```csharp
// 월드 좌표 → 그리드 셀 (col, row)
public (int col, int row) WorldToGrid(Vector3 worldPos)
{
    float localX = worldPos.x - gridOrigin.x;
    float localY = worldPos.y - gridOrigin.y;

    int col = Mathf.FloorToInt(localX / cellSize);
    int row = Mathf.FloorToInt(localY / cellSize);

    return (Mathf.Clamp(col, 0, cols - 1),
            Mathf.Clamp(row, 0, rows - 1));
}
```
