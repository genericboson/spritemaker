# SpriteMaker

캐릭터 **앞모습 한 장**을 그리면 도트 아트 스프라이트로 바꾸고, Godot 4 의 2D 타일맵 게임에서 바로 쓸 수 있는
4방향 걷기·공격 애니메이션을 만들어 주는 WPF 프로그램.

## 흐름

1. **스케치** — 512×512 캔버스에 펜/지우개/채우기/스포이드로 앞모습을 그린다. PNG 를 불러와도 된다
   (흰 배경은 자동으로 투명 처리).
2. **도트화** — 그림을 캐릭터 높이(16/24/32/48/64px)로 줄이고 미디언 컷으로 색 수를 줄인 뒤 1px 셀아웃 외곽선을 두른다.
   선만 그려도 닫힌 영역은 채워진다. 어두운 선은 가중치를 줘서 축소해도 살아남는다.
3. **부위 나누기** — 목(가장 좁은 행)과 다리 사이 빈틈을 찾아 머리/몸통/다리를 나눈다. 슬라이더로 직접 고칠 수 있다.
4. **방향 추정**
   - 뒷모습: 머리 안쪽을 머리카락 색으로 덮어 얼굴을 가리고 좌우 반전
   - 옆모습(오른쪽): 뒤통수 머리카락 + 얼굴 오른쪽 절반(옆머리는 피부로 바꿔 얼굴 앞선을 드러냄), 몸통 가운데 조각, 한쪽 다리 두 벌(뒷다리는 어둡게)
   - 왼쪽: 오른쪽 프레임 좌우 반전
5. **애니메이션** — 부위를 픽셀 단위로 움직여 만든다.

| 이름 | 프레임 | fps | 반복 |
|---|---|---|---|
| `idle_down/up/right/left` | 1 | 1 | O |
| `walk_down/up/right/left` | 4 | 8 | O |
| `attack_down/up/right/left` | 4 (젖힘→베기→잔상→복귀) | 12 | X |

## Godot 로 내보내기

이름이 `hero` 이면 고른 폴더에 다음이 생긴다. 고닷 프로젝트 안 폴더를 고르면 된다(서로 상대 경로로 참조).

- `hero.png` — 스프라이트시트(애니메이션 하나가 한 줄, 모든 칸이 같은 정사각형)
- `hero_frames.tres` — `SpriteFrames` 리소스
- `hero.tscn` — `AnimatedSprite2D` 씬. 텍스처 필터 Nearest, **발밑이 노드 원점**이라 타일 위 위치를 그대로 쓰면 된다
- `hero.json` — 칸 크기, 발 기준점, 애니메이션 목록

Godot 4.7.2 에서 로드를 확인했다. 사용 예:

```gdscript
extends CharacterBody2D

@onready var sprite: AnimatedSprite2D = $Hero
var facing := "down"

func _physics_process(_delta):
    var dir := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
    velocity = dir * 80
    move_and_slide()
    if dir != Vector2.ZERO:
        facing = ("right" if dir.x > 0 else "left") if abs(dir.x) > abs(dir.y) else ("down" if dir.y > 0 else "up")
        sprite.play("walk_" + facing)
    else:
        sprite.play("idle_" + facing)
```

## 구조

```
src/SpriteMaker.Core   변환 로직(WPF 무관): Pixelizer, Palette, BodyAnalyzer, ViewBuilder, Animator, GodotExporter, SketchDocument
src/SpriteMaker.App    WPF 화면
tests/SpriteMaker.Tests xUnit 테스트(코드로 그린 샘플 스케치로 전체 파이프라인 검증)
```

## 빌드

.NET 10 SDK 필요.

```
dotnet build
dotnet test
dotnet run --project src/SpriteMaker.App
```

## 한계 (현재)

- 뒷모습·옆모습은 앞모습에서 규칙으로 추정한다. 팔은 따로 움직이지 않고, 모자·꼬리처럼 뒤에서만 보이는 요소는 만들 수 없다.
- 공격은 몸을 내지르며 베기 궤적을 그리는 방식이며 무기 자체를 그리지는 않는다.
