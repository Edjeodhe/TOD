# 욕망의 탑 (가제) — 핸드오프

> 원본: `욕망의 탑 (가제) 기획서2.pptx` (27p, 2026.11.18 초안)
> 이 문서는 기획서의 메카닉스를 구현 가능한 규칙/수치로 정리하고, 현재 웹 프로토타입의 구조와 남은 결정 사항을 넘기기 위한 문서입니다.
> **(임시)** 표시는 기획서 원문에서도 임시값이거나, 원문이 비어 있어 구현 쪽에서 해석해 채운 값입니다.

## ⚠ 작업 규칙 (모든 작업에 적용, 2026-11-23~)

- **Unity 객체의 생성과 수정은 MCP(`UnityMCP`, MCP for Unity)로 에디터에서 직접 합니다.** 씬, GameObject, 컴포넌트와 그 값, 프리팹, 머티리얼 등이 대상이며, 코드에 하드코딩해 만들거나 고치지 않습니다.
- **예외는 템플릿 슬롯 복제입니다.** MCP로 미리 만든 템플릿(프리팹, 풀 슬롯, UI 템플릿)을 런타임에 복제하는 일은 코드로 합니다.
- **게임 데이터도 코드에 두지 않습니다.** 적 스탯, 증강, 퍼즐 조각, 상점 품목, 타격감 수치가 대상입니다. 코드에는 빈 필드만 선언하고, 실제 값은 데이터 프리팹이나 씬 컴포넌트에 MCP `update_component`로 넣습니다.
- **UI는 UXML/USS 에셋 파일로 작성합니다.** UIDocument GameObject는 MCP로 만듭니다. 목록형 UI는 UXML 템플릿을 복제합니다.
- **MCP 도구로 할 수 없는 작업**(파이프라인 에셋, Input Actions, Project Settings, 에셋 이동)은 먼저 `execute_menu_item`으로 시도합니다. 안 되면 설정 파일이나 에셋 파일을 직접 편집하고, 그 내용을 보고합니다.
- 작업 전 이 규칙을 먼저 확인합니다. 판단이 애매하면 사용자에게 질문한 뒤 진행합니다.

## Unity 포팅 결정 사항 (2026-11-23)

| 항목 | 결정 |
|---|---|
| 뷰 | 2D 직교 + Y정렬. 웹판의 1000×700 전투장 좌표와 `PROJECTION_Y .67`을 그대로 씁니다. |
| 렌더 파이프라인 | URP 2D 렌더러, Linear 색공간 |
| 주인공 리그 | **(2026-11-23 변경)** Unity 뼈대 계층(`Arena/Hero/Rig`) + C# 동작(`HeroRig`). 웹 `rig.html`·rig json 은 더 이상 Unity 에서 읽지 않습니다. |
| 웹판 | 동결하고 `web/`로 옮깁니다. Unity Assets에는 런타임 아트만 두고, 원본 시트와 .blend는 `art_src/`로 옮깁니다. |
| UI | UI Toolkit, 갈무리(Galmuri) 폰트 |
| 플랫폼·입력 | PC 키보드·마우스, New Input System |
| 사운드 | 웹판 오실레이터 효과음을 C#에서 AudioClip으로 생성합니다. |
| 그리기 | GameObject 기반: SpriteRenderer, LineRenderer, ParticleSystem과 커스텀 셰이더 |
| 화면 배치 | 게임식 전체화면: 타이틀 → 로비(등반/재봉 상자/다락) → 전투(모서리 HUD) |
| 진행 | 전투, 아웃게임, VFX, 후처리를 한 번에 옮깁니다. |

## 진행 중 작업 — 2차 개편 (2026-11-23 시작)

> 작업이 끊기면 여기서 이어 갑니다. 단계를 마칠 때마다 체크합니다.

### 사용자 결정
- **층 구성:** 생존 R1(90초) → 생존 R2(120초) → 쉬어가기 → 보스. 생존 라운드는 시간을 버티면 클리어됩니다.
- **컨셉:** 임시 10개 중 층마다 3개를 무작위로 뽑아 R1·R2·보스 라운드에 하나씩 씁니다. 컨셉은 바닥·등불 색조, 몬스터 구성 비율, 스폰 속도와 난이도 배율을 바꿉니다.
- **맵:** 경계가 있는 큰 맵(4000×2800)이고 카메라가 캐릭터를 따라갑니다. 바닥은 카메라 주변만 청크 단위로 생성·재활용합니다. 라운드마다 새 맵입니다.
- **라운드 종료:**
  - 시간이 끝나면 스폰이 멈추고 남은 적은 사라지며, 드롭은 자동 회수됩니다.
  - 화면 가운데(캐릭터 근처)에 이펙트와 함께 계단이 나타나고 "위로 올라가기"가 뜹니다. 계단 가까이에서 **E**를 누르면 올라갑니다.
- **증강:**
  - 전투 중에는 고르지 않습니다. 라운드가 끝난 뒤 아무 데서나 E를 누르면 쌓인 수만큼 3택 1을 고릅니다.
  - 증강은 능력치 전용입니다. 규칙형 4개는 데이터에서 빼고 코드만 남깁니다.
- **쉬어가는 라운드:**
  - 아티팩트(퍼즐 조각) 3개 중 1개를 고릅니다. 상인은 50% 확률로 나오며 조각은 팔지 않습니다.
  - 아이템은 층마다 이 1개뿐입니다. 정예·보스의 조각 드롭은 없앱니다.
- **바늘:**
  - 발사 간격 0.55초, 사거리 340, 속도는 기존의 약 60%입니다. 셋 다 능력치이고 증강으로 올립니다.
  - 손 높이로 곧게 날다가 끝에서 바닥에 수직으로 박힙니다.
  - 박힌 자리의 원을 더 잘 보이게 합니다.
- **버그:** 우클릭으로 당긴 뒤 실이 맵에 잠깐 남는 현상. 원인 후보는 당기기 연출이 당긴 순간의 플레이어 위치로 감겨 들어가는 것입니다.
- **스폰:** 무작위 요소를 넣고, 유형별로 간격과 마릿수를 차등합니다(예: 근접 1초에 3마리, 원거리 3초에 1마리). 몬스터는 오브젝트 풀링을 씁니다.
- **보스 정화:**
  - 캐릭터에서 상아·청록 빛의 고리가 바닥을 따라 4초에 걸쳐 퍼지고, 카메라가 줌아웃됩니다.
  - 고리가 지나간 바닥은 채도와 밝기가 회복되고, 비네트와 욕망 테두리가 약해집니다. 정화된 상태는 그 층을 떠날 때까지 유지됩니다.
  - 욕망 흡수 제안은 정화 후 계단에서 E를 눌렀을 때 뜹니다.
- **욕망 80% 이상:** 화면이 살짝 붉어지며 일렁입니다(후처리).

### 단계
- [x] 1. 데이터 구조: 맵·라운드·컨셉·스폰 표·증강 효과·정화·일렁임·바늘 연출 값 (`GameDatabase.cs`. 값 입력은 6단계)
- [x] 2. 시뮬레이션: 라운드 흐름, 스폰 표, 적 풀, 계단·제단·상인, 증강 대기, 아티팩트, 바늘 능력치 (`Combat.cs`, `RunState.cs`)
  - Phase는 Combat / Purifying / Cleared / Victory입니다. `Combat.Ascend`, `Nearest`, `TakeArtifact`, `PurifyRadius`, `TimeLeft`를 추가했습니다.
  - 당기기 잔상 수정: Pull·Cut 이펙트에 `followHero`를 넣었고, 뷰가 현재 손 위치로 감기게 합니다.
- [x] 3. 뷰: 카메라 추적·줌, 바닥 청크, 맵 경계, 바늘 비행·박힘, 박힘 원, 당기기 잔상 수정, 계단·제단·상인, 정화
  - `CameraRig`(추적·줌), `BackgroundView`+`ChunkView`(청크), `ArenaRenderer`의 카메라·맵 벽/테두리·계단/제단/상인·정화 고리·정화 셰이더 전역값(`_TOD_PurifyCircle/Look/Fill`). 데이터 필드 추가: `MapConfig` 벽·테두리 색과 아치, `RenderPalette` 계단 빛·안내 문구 색.
  - 바늘(`NeedleVisual`): 나는 동안 바닥에서 `flightLift`(캔버스 px)만큼 떠서 곧게 날고, 떨어지면 `diveTime`초 동안 수직으로 떨어져 박힌다. 박힌 각도는 `stickAngle`(도, 90 = 수직), 날아온 쪽으로 `stickTilt`(도)만큼 기운다. 박힘 원은 `ringRadius/Width/Glow(가산 빛 두께)/Pulse(반지름 흔들림 px)/PulseSpeed/Alpha`.
  - 당기기 잔상: `Fx.followHero`이면 `DrawPullSnap`·`DrawCut`의 시작점이 그 프레임의 손 위치(`handNow`). 감기는 타이밍은 `pullSnapDelay`(0~1, 기존 .16)·`pullSnapPower`(기존 2.4).
  - SpriteFX `_Purify`: 고리 안쪽 바닥의 컨셉 덮개를 걷고 `floorBright/floorSat`로 되살림, `floorFill`로 덮음, `edgeSoftness` px로 경계를 부드럽게. 바닥 청크는 `BackgroundView.Paint`가 프로퍼티 블록으로 켬.
  - 상인 임시 그림: `tools/make_unity_helper_art.py` → `Assets/Art/Generated/merchant.png`.
- **MCP 교체 (2026-11-23):** gamelovers `mcp-unity`를 CoplayDev **MCP for Unity v10.3.0**(Window > MCP for Unity)으로 바꿨습니다. `.mcp.json`은 `UnityMCP`(HTTP `localhost:8080/mcp`)입니다. 아래 "MCP 관련 주의"의 `[TOD patch]` 기능은 더 이상 없습니다.
- [x] 4. 후처리: 정화·일렁임 (`PostFX.shader`: `_TOD_Purified`만큼 비네트 약화, `_TOD_Warp`로 화면이 물결치고 `_TOD_WarpTint`로 붉어짐. `_TOD_WarpParams` = 폭(캔버스 px), 물결 수, 속도)
- [x] 5. UI: 타이머, E 안내, 아티팩트 대화 상자, 라운드 목록(4칸 + 컨셉 이름)
- [x] 6. MCP: 씬 정리(배경 → 청크 템플릿), 새 템플릿, 데이터 입력
  - `Arena/Background`: 고정 화면용 `FloorTint/Dusk/Frame`과 등불 6개를 지우고 `Chunks/ChunkTemplate`(ChunkView: Floor + Lamps/Lamp0~2)으로 바꿈. `Fill`은 맵 전체를 덮게 키움. `Shade`는 `BackgroundView`가 매 프레임 카메라 중심으로 옮김. 예전 바닥 덮개 역할은 컨셉 `floorFill`이 맡음(정화되면 걷힘).
  - `ArenaRenderer`의 `stairs/altar/merchant/archSprite` = `Assets/Art/Generated/*.png`(TOD/Setup/1로 스프라이트 임포트). `CameraRig.baseSize` 2.95.
  - `GameDatabase` 입력값(임시): 맵 4000×2800(벽 25~3975 × 24~2775), 청크 481.28(바닥 타일 1장)·청크당 등불 1, 라운드 순서 생존·생존·쉬어가기·보스(90초/120초), 계단은 플레이어 위쪽 120, 상인 확률 .5, 아티팩트 3택. 컨셉 10개(잿빛 회랑·핏빛 연회·얼어붙은 성가대·녹슨 태엽·곰팡이 정원·거울 무도회·젖은 다락·그을린 재봉실·속삭이는 서고·굶주린 식탁). 스폰 표: 스토커 2.2초마다 3±1마리, 차저 6초마다 1±1(12초부터), 슈터 3초마다 1(18초부터), 최대 36마리, 화면 밖 600~800.
  - 바늘: `hero.shotInterval` .55, `needle.reach` 340, `launch/settle` 1860/336(기존 60%). `needleVisual.flightLift` 20(사격 중 손 높이 실측 약 18px).
  - 증강: 규칙형 4개(bounce·dashThread·earlyKnot·echo)를 데이터에서 뺌. 능력치형 8개(fire·damage·supply·light·calm + 새 reach·swift·stride)에 `effects`를 넣음. `minFireInterval` .25.
  - 정화(4초, 줌 1.6), 욕망 일렁임(80%부터, 붉은 막 .12), 계단 빛·안내 문구 색과 UI 문구도 입력.
- [x] 7. 테스트 갱신, 플레이 검증, 커밋
  - `AugmentChoicesAreUniqueStatOnly`로 교체(규칙형 없음 + fire·reach 적용 확인). EditMode 5개 통과.
  - 플레이(자동 조작 + 시간 건너뛰기)로 확인: 생존 R1 타이머 → 시간 종료 → 계단·"E 위로 올라가기" → R2(새 컨셉) → 쉬어가기(제단·상인·계단, 아티팩트 대화 상자) → 보스 → 정화 고리·줌아웃·바닥 회복 → 계단에서 E → 욕망 제안 → 2층. 바늘이 손 높이로 날다 수직으로 박히고 원이 보임. 욕망 95%에서 붉은 막.
  - 남은 것: 사람이 직접 한 플레이, 일렁임 움직임(정지 화면으로는 확인 불가), 당기기 잔상 수정의 눈 확인, 값 다듬기(정화된 바닥이 조금 밝고 평평함, 계단 바닥 빛이 단단한 원판으로 보임).

## 5차 요청 (2026-11-23) — 레벨업 연출 강화·체력 회복 하트

- [x] **레벨업:** 흐려서 안 보이던 연출을 바꿨습니다. 발밑에서 초록 빛 기둥이 솟고(`visual.levelUpBeam` 200px), 충격파 고리 두 겹이 퍼지며, 굵은 ▲ 화살표 3개(`levelUpChevrons`)가 몸을 타고 머리 위로 올라갑니다. 몸은 처음 0.3초(`levelUpFlash`) 밝은 초록으로 번쩍였다가 은은한 기운이 남고, 빛 알갱이가 둘레를 돌며 올라갑니다. 글자는 넣지 않았습니다.
- [x] **체력 회복 하트:** 처치 때 일반 3%·정예 15% 확률(`rewards.healChance/healEliteChance`)로 하트가 떨어지고, 다른 드롭처럼 몸으로 빨려 들어 최대 체력의 15%(`healAmount`)를 회복합니다. 하트 그림은 Codex(`tools/codex_prompts/heart_pickup.txt` → `bake_stage2_art.py` → `Assets/sprites/heart_pickup.png`), 바닥에서는 붉은 빛을 두르고 심장처럼 두근거립니다.
- [x] **피가 차는 연출:** 먹으면 주인공 실루엣이 발끝부터 머리까지 붉게 차오르고(파츠 스냅샷 + 절단면으로 수면 아래만 칠함, `healTime` 1.1초) 차오르는 선이 빛나며 거품이 떠오릅니다. `+N` 숫자, 효과음 `heal`. HUD 체력 게이지는 새로 찰 몫이 밝게 깜빡이고, 게이지와 숫자가 그 위로 천천히 차오릅니다(15% 회복 ≈ 0.7초). 피해는 바로 줄어듭니다.
- 테스트 11개 (하트 드롭·회복 추가). 게이지가 차오르는 중간 모습은 캡처가 늦어 화면으로 잡지 못했습니다.

## 4차 요청 (2026-11-23) — 흔들림 제거·선명도·레벨업·실 보너스·등급·리롤·인벤토리·보스 처치·정화

> 사용자 선택: UI 는 DST식 찢어진 종이, 몬스터는 Codex 파츠로 뼈대 리그, 아이템 등급은 목록대로(유니크·레전더리 번쩍), 일렁임은 둘 다 제거.
> 나눠서 진행: 몬스터 리그는 세션 `monster-rig`, 종이 UI·보스 HUD 상단 중앙은 세션 `paper-ui` 가 맡습니다(각자 이 문서에 절을 추가).

- [x] **흔들림 제거:** `PostFX.shader` 의 손그림 보일링(8fps 1px 흔들림)과 욕망 80% 물결 일렁임을 뺐습니다. 욕망 80% 붉은 막은 남김.
- [x] **선명도:** 흐림 원인 두 가지를 고쳤습니다. ① 보일링이 화면을 서브픽셀로 흔들어 전체가 번짐 → 제거. ② 잉크 외곽선 Sobel 이 캔버스(590px) 단위로 샘플해 1080p 에서 두껍고 흐림 → 화면 픽셀 단위. ③ 스프라이트를 3~7배 줄여 그리는데 밉맵이 없어 뭉개짐 → `ProjectSetup` 텍스처 임포트에 밉맵(Kaiser) + 바이어스 −0.6 + Trilinear.
- [x] **레벨업:** 숫자 대신 몸에 초록 기운(파츠 단색 덮기 `visual.levelUpFill`, 발밑 고리, 빛 알갱이 `levelUpRate`, 둘레 빛 `levelUpGlow`, `levelUpTime` 1.6초) + 효과음 `levelup`.
- [x] **실 보너스:** 당길 때 걸린 실이 한 가닥 늘 때마다 한 타 피해 +12% (`hero.strandBonus`, 최대 `strandBonusMax` 60%). HUD 실 줄에 `+N%` 표시.
- [x] **스탯 증강 카드:** 등급이 없으므로 모두 같은 회색.
- [x] **아이템 등급:** 카드 색 = 등급(`gear.rankColors`): 일반 회색, 레어 하늘색, 유니크 노란색, 레전더리 어두운 빨강. 유니크는 빛 줄기가 지나가고, 레전더리는 더 굵은 빛 줄기 + 지날 때마다 테두리가 금빛으로 번쩍(`gear.rankShine`).
- [x] **다시 굴리기:** 증강 창은 선택마다 1번, 쉬어가기 탑 아이템 창은 층마다 1번. 방금 본 카드는 되도록 빼고 다시 뽑습니다.
- [x] **등반 중 재봉 상자(I):** 라운드가 끝났거나(Cleared) 쉬어가는 라운드일 때만 열립니다(전투 정지). 닫을 때(I·Esc·돌아가기) `Combat.RefreshGear` 가 장비 능력치 차이를 바로 반영합니다(피해·충전·최대 체력·실 속성). 하단 안내 `I 재봉 상자 열기`.
- [x] **보스 처치 연출:** 보스가 쓰러지면 바로 정화하지 않고 `Phase.BossDying` (`bossDeath`): 실 고리가 위에서 아래로 감겨 조이고(1.2초) → 하얀 칼빛 두 줄과 함께 섬광·멈춤·흔들림(0.35초) → 종이 조각 세 개로 잘려 밀려나며 떨어지고 종이 부스러기·실 끝이 흩어짐(1.1초) → 보스가 있던 자리에서 정화. 조각은 SpriteFX 의 두 번째 절단면 `_CutPlane2` 로 자릅니다.
- [x] **정화:** 둥근 테두리 선을 없앴습니다. 경계는 노이즈로 꽃잎처럼 흐트러지고(`purify.edgeNoise`, 넓은 `edgeSoftness`), 경계를 따라 상아·청록 꽃잎 빛이 피어오르며(`petalRate`), 가운데에서 큰 빛이 한 번 피었다 집니다(`centerGlow`). 정화된 바닥은 더 밝게(`floorBright` 1.25).
- [x] **갈무리 폰트 굽기:** 공식 배포(github.com/quiple/galmuri v2.40.4)를 받아 비교해 보니 `Assets/Fonts` 의 TTF 와 같은 파일이었습니다. 흐림은 UI Toolkit 이 TTF 를 SDF 로 그려 17px 같은 아무 크기로 늘린 탓이라, 고유 픽셀 크기의 **비트맵 폰트 에셋**을 구웠습니다(`Assets/Fonts/Baked/Galmuri9-10px`, `Galmuri11-12px`, `Galmuri11-Bold-12px`, `Galmuri14-15px`, RASTER_HINTED·점 필터·KS X 1001 한글 2350자 + 게임 문구 미리 구움, 빠진 글자는 실행 중 동적 추가).
  - `python tools/galmuri_charset.py` (글자 목록) → MCP `FontAsset.CreateFontAsset` 로 굽기 → `python tools/snap_ui_fonts.py` 가 USS 를 `-unity-font-definition` 으로 바꾸고 글자 크기를 고유 크기 배수(10 / 12·24·36·48 / 15·30·45·60)로 맞춤. **USS 에 글자 크기를 새로 넣을 때도 이 배수만 씁니다.** 다시 돌리면 이미 바꾼 규칙에 정의가 한 번 더 붙으니 새 규칙만 손으로 맞추세요.
  - 전투 글자(피해 숫자 등)는 코드가 크기를 정하므로 고유 배수가 아닐 수 있습니다(점 필터라 흐리지는 않음).
- 테스트 10개: 실 보너스, 보스 처치 → 정화, 탑 다시 굴리기 추가. 당김 예시 테스트는 보너스를 반영해 타격 수 기준으로 바꿈.

## 종이 UI (2026-11-23) — DST식 찢어진 종이 + 거친 잉크

> 사용자 선택: "DST식 찢어진 종이". 보스 HUD 는 화면 위 가운데, 크게.

- **그림:** Codex 시트 `art_src/raw/ui_kit.png`(프롬프트 `tools/codex_prompts/ui_kit.txt`, 초록 키) → `python tools/bake_ui_paper.py` → `Assets/UI/Paper/`:
  `panel_dark`(어두운 종이 판) · `panel_light`(양피지 판) · `button`(찢어진 띠) · `meter_frame`(속이 빈 잉크 틀) · `card`(바느질 카드) · `seal`(밀랍 인장) · `grain`(게이지 채움용 회색 종이 결, 반복).
  스프라이트 border(9-slice)는 임포터에 넣었고(`panel_dark` 64, `panel_light` 72, `card` 44, `button` 좌우 64·위아래 44, `meter_frame` 좌우 56·위아래 40), USS 에서도 같은 값을 `-unity-slice-*` 로 씁니다. 굵기는 `-unity-slice-scale` 로 맞춥니다.
- **USS 규칙(`Assets/UI/Main.uss`):** 색은 tint(`-unity-background-image-tint-color`)로 물들입니다. HUD 판·방·로비 = 어두운 종이, 대화 상자 = 양피지 + 잉크 글자, 버튼·꼬리표·띠·알림 = 찢어진 양피지(주 버튼은 포도주색), 일시 정지·소리 = 밀랍 인장, 게이지 = 잉크 틀(`.bar-frame` > `.bar-track` + `.bar-ink`) 안에 종이 결 채움. 글꼴은 그대로 갈무리(한글 손글씨 글꼴은 프로젝트에 없음).
- **배치:** 위 가운데 줄은 생존 타이머(top 14) 또는 **보스 HUD**(top 10: 인장 + 이름 띠 32px, 700×70 잉크 틀 게이지) → 그 아래 라운드 띠(top 146). 증강·아이템·상점 창은 1200px, 나머지 대화 상자는 900px.
- **퍼즐 카드:** 바느질 카드를 등급 색으로 살짝 물들이고(`GameUI.PaperTint`), 띠는 진하게 물든 양피지, 돌기는 등급 색 + 잉크 테두리, 홈은 양피지 색. 레전더리 번쩍임은 카드 tint·액자 테두리·돌기 색으로 냅니다(`GameUI.Tick`). 표시 코드만 바꿨고 게임 로직은 그대로입니다.
- **UXML 구조 변경:** `hp-bar`·`desire-bar`·`boss-bar`·`exp-track` 을 잉크 틀로 감쌌고, 보스 HUD 에 `boss-name` 라벨과 인장을 넣었습니다. `GameUI` 가 찾는 이름은 모두 그대로입니다.
- 확인(플레이 캡처 `Temp/Shots/after_*.png`): 로비, 전투 HUD(타이머), 보스 HUD, 증강 창, 탑 아이템 창(레전더리·유니크·레어), 상점, 욕망 제안, 일시 정지 + 알림, 재봉 상자(등반 중), 다락, 결과. EditMode 10개 통과.
- 남은 것: 다락 배경의 `room-shade`가 그림을 거의 어둡게 하지 않는 것처럼 보임(이전부터, 확인만 함). 사람 눈으로 본 마우스 호버·눌림 느낌.

## 3차 요청 (2026-11-23) — 피격·크기·대쉬·드롭·증강·아이템·경험치 바·리그

> 사용자 선택: 피격은 붉게 물듦 + 깜빡, 캐릭터 75%, 스탯 증강 8종, 아이템 12개 새로, 리그는 Unity 계층 + C# 동작.

- [x] **피격:** 흑백(채도 0) 대신 원래 색 위에 붉은 기운(`palette.heroHurt`, 양 `heroHurtAmount`)을 덮고 무적 시간 동안 깜빡입니다.
- [x] **캐릭터 크기:** `visual.heroSize` 0.75 (그림자·잔상 포함). 바늘 비행 높이 `needleVisual.flightLift` 15.
- [x] **대쉬 2칸:** `feel.dashCharges` 2, 한 칸 충전 `feel.dashCooldown` 1.6초(× 대쉬 충전 능력치), 연달아 쓸 때 최소 간격 `dashMinCooldown`. 왼쪽 위 HUD 에 칸 + 차오르는 게이지.
- [x] **경험치·골드 드롭:** 처치하면 초록 경험치 구(최대 3알)와 골드(최대 3닢)가 튀어나왔다가(`rewards.dropPop*`) 몸으로 빨려 들어 회수됩니다(`dropVacuum*`). 빛·꼬리·흡수 반짝임(`fx.pickup`)·효과음(`xp`, `gold`). 라운드가 끝날 때 남은 드롭도 몸으로 날아옵니다.
- [x] **증강:** 스탯 8종(공격력·공격속도·이동속도·최대 체력·사거리·바늘 속도·털실 최대·대쉬 충전), 아이콘은 Codex. 카드에 "지금 → 고른 뒤" 수치와 Lv(고른 횟수). Esc 로 닫고 E 로 다시 열면 같은 선택지.
- [x] **아이템:** 쉬어가기 라운드의 제단을 **탑**(`prop_tower`, 높이 `palette.altarHeight`)으로 바꿈. 상호작용하면 퍼즐 카드 창(세트 색·아이콘·이름·효과·조각 모양·설명)에서 1개를 고릅니다. 조각 12개(혼돈·절망·좌절 × 4) 새로 만듦. 0~2번은 세트 테스트 때문에 모양을 유지.
- [x] **하단 경험치 바:** 메이플식(레벨 칸 · 노란 초록 게이지 · 10% 눈금 · `EXP 현재 / 필요 [퍼센트]`).
- [x] **리그:** 웹에서 구운 rig json 을 읽던 `Rig.cs`·`MiniJson.cs`를 지우고, 원본 파츠 그림으로 씬에 뼈대 계층을 MCP 로 조립했습니다. 동작은 `HeroRig`(C#)가 매 프레임 계산합니다.
  - 반복 동작(idle·run): 뼈마다 `offset + a1·sin(2π(u+p1)) + a2·sin(4π(u+p2))`. 한 번 동작(shoot·pull·dash·hurt·die): 시작 자세 → `peakTime` 최고 자세 → `settleTime` 정리 자세 → 0.
  - 처음 값은 웹 키프레임에서 한 번 맞춘 것입니다(`tools/fit_hero_motion.py`, 원본은 `art_src/rig/hero-quarter.rig.json`, 최대 오차 약 3°). 이후 조정은 `Arena/Hero/Rig` 인스펙터에서 합니다.
  - 회피 통그림 3장 대신 대쉬 뼈 동작 + 떠오름. 대쉬 잔상은 생긴 순간의 파츠 자세를 하늘색 실루엣으로 그립니다.
- 그림(Codex CLI): `tools/codex_prompts/{stat_icons,item_icons,tower,xp_orb}.txt` → `art_src/raw/*.png` → `python tools/bake_stage2_art.py` → `Assets/sprites/{stat_*,item_*,prop_tower,xp_orb}.png`. 경험치 구는 초록이라 자홍 키로 뽑았습니다. 탑 창문(키 색)은 굽기에서 빛 색으로 채웁니다.
- 테스트 7개: 증강 적용(공격속도·사거리·최대 체력), 대쉬 2칸 충전, 드롭 흡수 추가.
- 확인 못 한 것: 사람이 직접 한 플레이, 달리기·쏘기 동작을 웹판과 나란히 비교, 효과음 청취.

## 몬스터 뼈대 리그 (2026-11-23)

> 사용자 선택: "Codex로 파츠 뽑아 뼈대 조립". 몬스터 4종이 통그림 늘이기 대신 뼈대로 걷습니다.

- **그림:** `tools/codex_prompts/enemy_{stalker,charger,shooter,boss}_parts.txt`(참고 = 기존 통그림) → `art_src/raw/enemy_*_parts.png`(초록 키) → `python tools/bake_enemy_parts.py` → `Assets/sprites/enemy_{종류}_{파츠}.png`(23장, 시트의 0.5배) → `TOD/Setup/1. Texture Imports`(이름이 `enemy_`라 아래 가운데 피벗).
  - 스토커: head·torso·uarm·farm·thigh·shin(발 포함). 차저: head·torso·uarm·farm·thigh·shin·foot. 슈터: head·jaw·torso·arm·leg. 보스: head·torso·skirt·uarm·farm(다리 없음, 치마로 미끄러짐).
- **코드:** `Runtime/Rig/BoneRig.cs`(공통: 뼈 배열·`Pose`·`Loop`·`Shot`·`ApplyPose`) ← `HeroRig`(주인공, API 그대로)와 `MonsterRig`(몬스터)가 상속합니다. 뼈는 주인공과 같은 `HeroBone`, 파형은 `Wave/LoopMotion/ShotMotion`을 그대로 씁니다.
  - 걷기 위상 = 실제로 걸은 거리 / `stride`(그림 px). 빨리 가면 발도 빨라지고, 뒷걸음질이면 거꾸로 돕니다. 위상이 1초에 도는 횟수는 `maxStepRate`로 막습니다(차저 돌진 440px/초). 당겨지거나 밀리는 이동은 걷기로 치지 않습니다.
  - 실제 속도로 idle ↔ walk를 섞습니다(`walkFull` 캔버스 px/초에서 100%, `walkFollow`로 부드럽게). 서 있는 슈터는 숨쉬기만 합니다.
  - 행동: 예고 `melee`/`shot`/`dash`(keepEnd) → 돌진 `charge`(걷기 위에 겹침) → 쉬는 동안 마무리 `strike`/`fire`/`brake`(예고의 정리 자세에서 이어 시작). 피격 `hurt`는 `flash`가 새로 켜질 때마다 위에 더합니다.
  - 접지: `feet`(발마다 뒤꿈치·발끝)의 가장 낮은 점을 리그 로컬 공간에서 바닥에 맞춥니다(`groundFollow` 1, 보스 0). 늘이기·회전 계층 아래에서도 맞습니다.
  - `EnemyView`: 종류별 템플릿(`rigs`)을 처음 쓸 때 자기 `Flip` 아래로 복제해 둡니다(뷰 하나에 종류마다 최대 1개). 배율 = `e.height / rig.height`. 늘이기·당김·정예 고리·체력바·바늘·깜빡임(파츠마다 `_Bright/_Sat`, 뒤쪽 팔다리는 `_Fill` 그늘)은 그대로이고, 리그가 있으면 옛 계단식 출렁임은 끕니다. **두 동강(e.cut) 동안만** 통그림 `type.sprite` 두 조각으로 바꿔 그립니다.
- **씬(MCP):** `Arena/Enemies/Rigs`(꺼 둠) 아래 `Stalker/Charger/Shooter/Boss`(MonsterRig + 뼈 계층 + `Art`). 파츠 정렬 순서 10~24, 그래서 `EnemyTemplate`의 체력바를 40·41, 바늘을 45·46으로 올렸습니다. 관절·동작 값은 여기 인스펙터에서 조정합니다.
  - 첫 값: `art_src/rig/enemy_rigs.json`(관절, `tools/enemy_rig_seed.py`가 동작·접지를 써 넣음). `python tools/preview_enemy_rig.py <종류> <동작> out.png [프레임]`이 같은 계산으로 그려 봅니다. **조립한 뒤에는 씬이 원본**이고 json 은 다시 읽지 않습니다.
- 확인: EditMode 10개 통과. 플레이(`StartRun` + 종류별 소환 + AutoPilot)에서 4종 모두 걷기·정렬·좌우 뒤집기·깜빡임·두 동강·정예 고리 확인(`Temp/Shots/rig_*.png`).
- 남은 것: 움직임은 정지 화면과 미리보기 스트립으로만 봤습니다(실제 속도감은 사람 눈 확인 필요). 리그 몸이 옛 통그림보다 좁아 같은 키에서 조금 작아 보입니다(`rig.height`를 줄이면 커짐). 스토커 앞팔 위팔이 가슴 위에 얹혀 관절 공이 보입니다. 두 동강 순간 리그 자세 → 통그림으로 바뀌는 튐. 보스 처치 연출(`DrawBossDeath`)은 여전히 통그림입니다.

## Unity 판 (2026-11-23 포팅 완료)

### 실행
- Unity 6.3(6000.3.19f1)로 프로젝트 폴더를 열고 `Assets/Scenes/Main.unity`를 플레이합니다. 로비 → 등반 시작(Enter) → 전투이며, 조작은 웹판 데스크톱과 같습니다(WASD, 좌클릭 발사·누르고 있기, 우클릭 당기기, Space 대쉬, Esc 일시 정지).
- 저장은 PlayerPrefs의 `tower-of-desire.profile.v1` 키(JSON)를 씁니다. 웹판 브라우저 저장과는 별개입니다. 초기화는 메뉴 `TOD/Debug/Reset Profile`입니다.

### 씬 구성 (모두 MCP로 생성·입력)
| 오브젝트 | 내용 |
|---|---|
| `Main Camera` | 직교 2.95(캔버스 590px 높이), `CameraRig`(캐릭터 추적·맵 경계·정화 줌아웃·흔들림·당기기 킥) |
| `Game/GameDatabase` | **모든 게임 데이터**: 전투장·스폰·주인공·바늘·타격감·적 4종·보상·증강 9종·욕망·세트·조각 7종·다락·상점·이펙트 크기와 수명·실 속성 색·화면 색·문구·효과음. 밸런스는 여기 인스펙터에서 조정합니다. |
| `Game/GameController` | 진행(main.js), `SoundBank`(합성 효과음, `sample`을 넣으면 샘플로 교체), `AutoPilot`(검증용 자동 조작, 평소 꺼짐) |
| `Game/UI` | UIDocument(`Assets/UI/Main.uxml`) + `GameUI` |
| `Arena` | `ArenaRenderer`(render.js). 비전투 화면에서는 꺼집니다. |
| `Arena/Background` | 맵 바탕(`Fill`), 바닥 청크 템플릿(`Chunks/ChunkTemplate`: 바닥 + 등불 3개), 화면 그늘(`Shade`), 불씨 템플릿. `BackgroundView`가 카메라 주변 청크만 복제·재활용하고 컨셉 색을 입힘. 맵 위쪽 벽·아치·테두리는 `ArenaRenderer.DrawMap`이 그림 |
| `Arena/Painter/Slots` | 실·이펙트를 그리는 SpriteRenderer/LineRenderer **템플릿 슬롯**(런타임에 복제) |
| `Arena/Enemies/EnemyTemplate` | 적 템플릿: 그림자·정예 고리·늘이기 계층(Body/Center/Rot/Squash/Unrot/Back/Flip)·두 동강용 BodyA/B·체력바·바늘 4개. `EnemyView.rigs` = 종류별 몸 리그 템플릿(`Arena/Enemies/Rigs/*`, 꺼 둠) |
| `Arena/Hero` | `Rig`(HeroRig) 아래 뼈 24개 계층(`hips/torso/...`, 각 뼈에 `HeroBone` + 그림 `Art`). 피벗·관절 위치는 뼈 Transform, 동작 값은 `HeroRig` 인스펙터. 회피 통그림은 없앴고 대쉬도 뼈 동작으로 그립니다 |
| `Arena/Particles` | 불씨·물방울·스파크·반짝임 ParticleSystem 4개 |

### 코드 (`Assets/Scripts`)
- `Runtime/Sim` — `Combat.cs`(combat.js 1:1), `Profile.cs`(gear.js), `RunState.cs`. Unity 오브젝트에 의존하지 않습니다.
- `Runtime/Rig` — `HeroRig.cs`(동작 계산: 반복 동작은 사인 2개, 한 번 동작은 시작→최고→정리 자세, 발 접지 보정), `HeroBone.cs`, `Affine.cs`
- `Runtime/View` — `ArenaRenderer`, `Painter`(Canvas 호출을 템플릿 슬롯으로), `HeroView`, `EnemyView`, `ParticleFx`, `BackgroundView`, `CameraRig`, `Space`(좌표 변환·슬롯 풀)
- `Runtime/UI/GameUI.cs`, `Runtime/Game/GameController.cs`, `Runtime/Audio/SoundBank.cs`, `Runtime/Debug/AutoPilot.cs`
- `Editor/ProjectSetup.cs` — `TOD/Setup/1~4` 메뉴(텍스처 임포트·플립북 슬라이스, URP 2D 파이프라인+후처리 패스, UI PanelSettings, Player 설정). MCP로 못 만드는 설정·에셋만 다룹니다.
- `Editor/DebugMenu.cs` — 플레이 중 화면 전환·파편 지급·프로필 초기화
- `Tests/CombatTests.cs` — EditMode 테스트 5개(당기기 피해 전달 예시, 세트 판정, 폭주→동화, 증강 3택, 5층 엔딩까지 자동 진행)
- 셰이더 `Assets/Shaders`: `SpriteFX`(밝기·채도·단색 덮기·반평면 절단·원/고리), `LineFX`, `PostFX`(shader.js + 화면 섬광·피격·욕망 테두리)

### MCP 관련 주의
- MCP 서버는 CoplayDev **MCP for Unity**(`UnityMCP`, Window > MCP for Unity에서 서버 시작)입니다. 옛 gamelovers 패키지의 `[TOD patch]`(배열 채우기, `"scene:경로"` 참조)는 없습니다.
- 컴포넌트 값·배열·오브젝트 참조는 `execute_code`에서 `SerializedObject`/`FindProperty`로 넣는 방식이 가장 확실합니다(직렬화 이름 사용, `Undo.RecordObject` 후 `ApplyModifiedProperties`, 씬 저장은 `manage_scene save`). 2026-11-23 데이터 입력과 배경 정리는 이렇게 했습니다.
- `execute_code`는 CodeDom(C# 6)으로 돕니다: `Object`는 `UnityEngine.Object`로 적고, 로컬 함수 대신 `System.Func/Action`을 씁니다. `AssetDatabase.DeleteAsset` 같은 패턴은 안전 검사에 막힙니다.
- 스크립트를 고친 뒤에는 `refresh_unity(compile=request)` → `read_console`로 오류를 확인합니다.
- `manage_camera screenshot`은 `output_folder`를 주지 않으면 `Assets/Screenshots`에 저장합니다. 검증 캡처는 `output_folder: "Temp/Shots"`로 둡니다. 플레이 중 캡처 때 MCP 쪽에서 "PlayerLoop ... called recursively" 오류가 콘솔에 찍히지만 게임 오류는 아닙니다.
- 플레이 검증 때 시간 건너뛰기는 `execute_code`로 `GameController.Run.elapsed`를 바꾸고, E는 `GameController.Interact()`로 대신합니다.

### 검증한 것 / 남은 것
- 확인한 것:
  - 자동 조작으로 1→4층 진행: 라운드, 보스, 욕망 흡수, 각성, 조각 획득 알림, 층별 등불 색
  - 증강 대화 상자, 로비, 재봉 상자(미리보기 포함), 예견의 다락 화면
  - EditMode 테스트 5개 통과. 콘솔 오류 없음
- 직접 확인하지 못한 것:
  - 사람이 직접 키보드·마우스로 한 플레이
  - 효과음 청취
  - 상점·욕망·결과 대화 상자 화면 캡처
- 웹판과 다른 점:
  - Canvas `blur`·`shadowBlur` 글로우는 가산 복사본으로 대신했습니다.
  - 실선의 그림자 글로우는 생략했습니다.
  - 물방울 입자는 정사각 빌보드입니다.
  - 모션 감소(`prefers-reduced-motion`) 대응과 통그림 섞기(cels)는 옮기지 않았습니다(웹판에서도 꺼져 있었습니다).
  - HUD 패널이 전투장 위쪽 모서리를 조금 가립니다.

## 웹 프로토타입 (동결, `web/`)

- 개발 서버와 리그 편집은 `node web/server.js` 실행 후 [게임](http://localhost:5173/) 또는 [리그 에디터](http://localhost:5173/rig.html)를 엽니다. 서버는 `/assets/*`를 Unity `Assets/`에서 먼저 찾고, 없으면 `art_src/`(원본 시트·옛 파츠·.blend)에서 찾습니다. 리그 json 은 2026-11-23부터 `art_src/rig/hero-quarter.rig.json`에 있고(서버가 `art_src/`에서 찾음), Unity 판은 더 이상 읽지 않습니다. 웹에서 리그를 고쳐 Unity 에 옮기려면 `python tools/fit_hero_motion.py`로 값을 다시 뽑아 MCP 로 `HeroRig`에 넣습니다.
- `web/index.html`을 파일로 바로 여는 오프라인 모드는 에셋이 옮겨져서 이미지가 깨집니다. 서버로 실행하세요. `node tools/build_offline_game.mjs`는 `web/src/game/offline.js`를 갱신합니다.
- 아래 1~8장과 이 절의 나머지 내용은 웹판을 기준으로 쓴 것입니다(경로의 `index.html`, `src/` 등은 이제 `web/` 아래에 있습니다).
- 프로토타입은 **5층, 층당 3라운드**입니다. 보스를 쓰러뜨려 층을 돌파하고, 마지막 보스 선택 뒤 엔딩을 확인합니다. 사망하거나 일시 정지 메뉴 또는 상점에서 귀환한 뒤 장비를 조합하고 다음 등반을 시작할 수 있습니다.
- 데스크톱은 WASD/방향키, 좌클릭 또는 길게 누르기, 우클릭, Space, Esc를 사용합니다. 터치는 전투장을 눌러 조준·발사하고 화면 아래 이동·당기기·대쉬 버튼을 사용합니다.
- 재봉 상자는 목록에서 조각 선택 → R 또는 회전 버튼 → 판의 기준 칸 선택 순서입니다. 배치된 조각을 다시 선택해 옮기거나 제거합니다. 장비 효과와 다락 강화는 다음 등반을 시작할 때 반영됩니다.
- 저장 범위는 기억의 파편, 획득한 퍼즐 조각과 배치, 영구 강화, 최고 돌파 층입니다. 브라우저의 로컬 저장소 키는 `tower-of-desire.profile.v1`입니다. 진행 중인 등반은 세션 단위이며 새로고침 시 초기화됩니다.
- 기획서 4장은 장르·뷰·주인공과 루프의 초기 정의를 담고 있습니다. 바로 아래 요약과 각 규칙의 구현 값을 이 웹 프로토타입의 1페이지 기획 기준으로 사용합니다.
- 브라우저에서 등반 시작, 실 당기기 다중 타격, 대쉬 효과, 일시 정지, 귀환, 조각 장착 및 리그 로딩을 확인했습니다. 5층 엔딩·증강·욕망 분기는 전투 로직 시뮬레이션으로 검증했습니다. 수동 플레이 밸런스 조정은 남아 있습니다.

---

## 1. 한 줄 요약

수인 재봉사(생쥐)가 사라진 아버지를 찾아 **욕망의 탑**을 오른다.
**인게임 = 뱀서라이크**(층마다 3라운드, 증강 선택), **아웃게임 = 로그라이크**(퍼즐 인벤토리, 영구 성장).
핵심 재미는 **선택 → 리스크 → 피드백**: 실을 언제 당길지, 매듭을 더 묶을지, 보스 욕망을 흡수할지.

| 항목 | 내용 |
|---|---|
| 장르 | 뱀서라이크 + 로그라이크 |
| 뷰 | 쿼터뷰 (프로토타입: 바닥을 세로 압축한 3/4 뷰 + Y 정렬 빌보드) |
| 조작 | WASD 이동 / 좌클릭 바늘 발사 / 우클릭 실 당기기 / Space 대쉬 |
| 루프 | 전투 → 재화(경험치·골드·기억의 파편·퍼즐 아이템) → 성장(증강·영구강화) |
| 차별점 | 무기 종류가 아니라 **규칙을 바꾸는** 성장, 퍼즐 인벤토리, 욕망 수치 관리 |

---

## 2. 핵심 재미 (p3)

| 선택 | 리스크 | 피드백 |
|---|---|---|
| 레벨업 증강 3택 1 | 층 오를수록 욕망 수치 누적 → 관리 실패 시 탑에 동화 | 모아둔 실을 한 번에 당기면 여러 적 동시 타격 |
| 보스 욕망 흡수 여부 | 매듭 쌓일수록 이동속도 감소 | 히트스톱, 데미지 숫자, 화면 흔들림 |
| 실을 언제 당길지 | 털실 다 쓰면 공격 불가 | |

→ 구현 시 피드백 3종(히트스톱·데미지 숫자·셰이크)은 **당기기 타격 수에 비례**해 강해지도록.

---

## 3. 인게임 (뱀서라이크)

### 3.1 층 / 라운드 (p10)
- 층마다 **3라운드**. 3라운드에 **층 보스** 등장 (임시).
- 라운드 클리어 조건 (프로토타입): 라운드 타이머(1R 24초, 2R 28초) 동안 스폰되는 적을 버티고, 남은 적 처치 시 클리어. 3R은 보스 처치 시 클리어하고 남은 하수인이 사라집니다. 라운드 끝에 남은 드롭을 자동 회수합니다.
- 경험치 드롭은 적 등급 비례: **일반 1 / 정예 3 / 보스 10** (임시).
- 층 클리어 → (보스 욕망 흡수 선택) → **상인**(층과 층 사이) → 다음 층.

### 3.2 적 (p11)
| 유형 | 행동 | 구현 메모 |
|---|---|---|
| 추적형 | 다가와서 근접 공격, **공격 전 예고 동작** | 사거리 진입 → 0.45s 웅크림(예고) → 짧은 할퀴기 |
| 돌진형 | **조준선 표시 후 직선 돌진** | 0.7s 조준선 → 0.5s 고속 돌진 → 1.2s 경직 |
| 탄막형 | 거리 유지, **3~5발 산탄** | 선호 거리 260px, 2.2s마다 부채꼴 발사 |
| 층 보스 | HP 50% 이하에서 **패턴 변경**, HP **66%·33%에 하수인 소환** | 1페이즈: 돌진+산탄 / 2페이즈: 원형 탄막+빠른 돌진 |

- 정예: **체력 x1.6, 피해 x1.2** (외곽선 금색).
- 층이 오를수록 체력·피해·이동속도 증가 (구현: 층당 HP +18%, 피해 +10%, 속도 +4%) (임시).
- 적 = 수인들의 욕망이 형상화된 존재 (이름 미정).

### 3.3 무기: 바늘과 실 (p12–14, `hi` 프로토타입 전투 레퍼런스)
```
좌클릭  0.34초 간격으로 바늘 발사. 바늘 자체는 피해를 주지 않음.
        먼저 맞은 적, 벽 또는 최대 사거리 420px 지점에 바늘이 꽂힘.
        5발째에 플레이어 자리에서 그동안 손에 들고 있던 실을 모두 매듭에 고정.
        다음 발부터 새 묶음의 실을 다시 손에 들고 이동.
우클릭  비행 중·바닥·적에게 꽂힌 모든 실을 회수. 실과 교차한 적마다 타격 합산.
        바늘이 꽂힌 적은 자신의 교차 타격에 더해 나머지 타격까지 모두 전달받음.
        피해를 입은 적을 플레이어 쪽으로 0.16초에 걸쳐 감속하며 당김. 최대 34px, 보스 최대 10px.
바늘 속도는 발사 직후 2700px/s에서 사거리 끝 820px/s로 감속합니다(남은 사거리 비율의 제곱).
Space   입력 방향으로 백스텝. 방향키를 떼면 마지막 발사 방향의 반대로 이동.
```
**당기기 피해 = (해당 적의 선분 교차·직접 타격 + 전달 타격) × 실 피해(기본 14)**

- 레퍼런스의 판정 예시. 적 A에게 바늘 한 발이 꽂혀 있고 바닥에 꽂힌 실 두 가닥이 각각 B와 C를 통과하면 총 5타입니다. A는 70, B와 C는 각 28 피해를 받습니다.
- 앞 매듭은 이후 묶음에도 같은 자리에 남습니다. 당기면 모든 매듭이 풀리고 다음 매듭까지 필요한 발사 수를 초기화합니다.

### 3.4 무기 리소스 (p14)
| 리소스 | 규칙 | 선택 |
|---|---|---|
| 털실 | 최대 **6**, 발사마다 1 소모 / 이동 중 **1.05초에 1** 충전, **멈춰 있으면 2배** / 당기면 회수한 실 절반을 올림해 돌려받음 | 멈춰서 감을지, 움직일지 |
| 매듭 무게 | 매듭 1개당 이동속도 **−14%** (최저 **30%**) / 당기면 매듭 전부 풀림 | 더 묶을지, 지금 당길지 |
| 대쉬(회피) | **0.17s** 동안 150px 이동(초반에 몰아서 가는 감속 곡선) + **0.12s** 추가 무적, 쿨 **0.95s** / 대쉬 중 발사 불가, 적과 적 탄환 속도 0.4배 | 피할지, 쏠지 |

### 3.5 욕망 시스템 (p15–18)
- 최대치 기본 **100**. 레퍼런스: 다키스트 던전 스트레스.
- **쌓이는 것**: n층 입장 시 **+5×n** (1층 +5, 5층 +25) / 보스 욕망 흡수 **+20**.
- **줄이는 것**: 욕망 감소 증강(즉시 −15), 상인 아이템(정화 포션), 퍼즐 아이템 효과.
- **버티는 양**: 영구 재화로 최대치 100 → 110 → 120 …

| 단계 (최대치 대비) | 효과 |
|---|---|
| 0~49% | 변화 없음 |
| 50~99% | 화면 가장자리 일렁임, 환청, **상인 가격 상승**(구현: +25%) |
| 100% 도달 | 판정 → **폭주**(부정, 기본) 또는 **각성**(긍정, 낮은 확률 — 구현 25%) |
| 폭주 중 100% 다시 도달 | 탑에 완전 동화 → **등반 실패** |

- 폭주 예: 이동속도 −20%, 털실 충전 느려짐(−40%). 폭주 후 수치는 70%로 리셋 (구현 해석).
- 각성 예: 욕망 수치 절반 감소, 실 데미지 +20%.

### 3.6 보스 욕망 흡수 (p16)
| 욕망 | 효과 (규칙 바뀜) | 대가 |
|---|---|---|
| 집착 | 바늘 꽂힌 적이 죽으면 실이 가장 가까운 적에게 옮겨 붙음 | 털실 최대 −2 |
| 분노 | 매듭 3개 이상일 때 당기면 데미지 ×2 | 받는 피해 +20% |
| 갈망 | 적 처치 시 체력 소량 회복(+1) | 포션 효과 −50% |

흡수 시 욕망 **+20**. 거절하면 욕망 변화 없이 다음 층. 보스 처치 시 미획득 욕망 중 하나를 무작위로 제시합니다. 세 종류를 이미 흡수한 경우 실 피해 +4를 주는 ‘실에 스민 잔향’을 제시합니다.

### 3.7 증강 (p19) — 레벨업 시 3택 1, 규칙형 비중 높게
| 수치형 | 규칙형 |
|---|---|
| 바늘 발사 속도 +15% | 벽에 맞은 바늘이 한 번 튕김 |
| 실 데미지 +4 | 대쉬한 경로에 실 한 가닥 깔림 (경로를 지나는 적에게 실이 걸림) |
| 털실 최대 +2, 충전 +20% | 매듭이 5발이 아니라 4발마다 생김 |
| 매듭 감속 −30%, 대쉬 쿨 −20% | 당긴 뒤 0.5초 동안 실이 남아 한 번 더 당기기 가능 |
| 욕망 수치 즉시 −15 | |

- 구현: 규칙형 가중치 2, 수치형 1. 규칙형은 중복 획득 불가, 수치형은 중복 가능.
- 경험치 곡선 (임시): 다음 레벨 필요 = 5 + 3×(레벨−1).

---

## 4. 아웃게임 (로그라이크)

### 4.1 퍼즐 인벤토리 '재봉 상자' (p20–22)
- 인벤토리 = 퍼즐판(기본 **6×4**, 영구강화로 확장), 아이템 = 폴리오미노 퍼즐 조각(아이템마다 모양 다름). 방어구 없음.
- 색 = 세트: **혼돈(분홍) / 절망(청록) / 좌절(노랑)**.
- **붙어있다** = 변끼리 맞닿음(4방향). 꼭짓점만 닿으면 X (임시). 같은 색 조각들의 **연결 그룹 크기 중 최대값**을 세트 수로 사용 (구현 해석).

| 세트 | 색 | 활성화 숫자 |
|---|---|---|
| 혼돈 | 분홍 `#E89BB7` | 2·4·6·8·10 |
| 절망 | 청록 `#8CC7BE` | 3·6·9 |
| 좌절 | 노랑 `#E3C46B` | 1·3·5 |

예: 혼돈 3 → 2세트, 절망 2 → 꺼짐, 좌절 1 → 1세트.
세트 효과 내용은 원문 미정 → 프로토타입 임시값: 혼돈=실 데미지 +2/단계, 절망=털실 최대 +1/단계, 좌절=이동속도 +5%/단계.

- **등급 코스트**: 일반 1 / 레어 2 / 유니크 3 / 레전더리 4. 판 위 코스트 합 한도 **30** (칸 확장 시 +2씩). 세트 개수 제한은 없음. (p22 노트: '등급 개수 최대치'를 코스트 합 한도로 해석)

### 4.2 영구 성장 '예견의 다락' (p23–24)
- 영구 재화 **기억의 파편** 소모 → 마녀 **모이라**에게 "미래를 읽는다" → 예 → 화면 번쩍 → 강화.
- 강화 3종 (비용은 임시):
  1. **칸 늘리기**: 6×4 → 7×5 → 8×6, 코스트 한도 +2/단계
  2. **가로 줄 강화**: 두 줄 단위, 그 줄 위 퍼즐 능력치 +10%
  3. **욕망 최대치**: +10/단계

### 4.3 재화 (p25)
| 구분 | 재화 | 획득 | 사용처 | 등반 종료 시 |
|---|---|---|---|---|
| 영구 | 기억의 파편 | 보스·정예 처치, 층 클리어 | 예견의 다락 | 유지 |
| 소모 | 골드 | 적 처치 | 상인 | 소멸 |
| 소모 | 경험치 | 적 처치(등급 비례) | 레벨업→증강 | 소멸 |
| 장비 | 퍼즐 아이템 | 드롭, 상인 | 재봉 상자 | **미정** → 프로토타입: 유지 |

### 4.4 상인 '단추' (p26)
- 층과 층 사이 등장. 퍼즐 아이템·포션(HP)·정화 포션(욕망 −20)·버프 판매.
- **타임 세일**: 상점 진입 시 1개 품목 50% 할인, 20초 지나면 종료 (시간/할인율 원문 미정 → 임시).
- 욕망 50% 이상이면 가격 +25%.
- 각 층 상점에서 네 품목을 각각 한 번 구매할 수 있습니다. 기본 가격은 회복 포션 14, 정화 포션 19, 퍼즐 조각 23, 실 피해 버프 28 골드입니다.

---

## 5. 비주얼 / 애니메이션 가이드 (p7–8)
- 팀 버튼 풍 고딕 + 2D 종이 질감 컷아웃 (레퍼런스: 돈스타브 투게더, 유령 신부)
- 일정 굵기의 거친 검은 외곽선, 캐릭터는 귀엽고 분위기는 음산. 낮은 채도·명도.
- 팔레트(임시): `#16131B` `#4A3D50` `#E89BB7` `#8CC7BE`
- **2.5D 쿼터뷰 SD**: 앞서 생성한 45° 생쥐 원본의 둥근 눈과 입 없는 주둥이를 씁니다. 몸통을 압축해 약 2등신으로 맞추고 가까운 팔다리와 먼 팔다리의 스케일·명암·높이를 따로 잡았습니다. 전투장 앞쪽으로 이동할수록 캐릭터와 그림자가 조금 커집니다.
- **관절과 접지**: 이미지 파츠 노드 19개와 골반 중심축 1개. 허리와 양쪽 다리가 골반에서 갈라지므로 상체를 기울일 때 발이 함께 끌려가지 않습니다. 어깨·팔꿈치·손목·무릎·발목의 겹침을 늘리고 꼬리 세 마디의 끝을 둥글게 잘라 굽힐 때 이음새를 덮습니다. `feet`는 양발 밑창과 각 발의 바닥 깊이를 기록하며 달리기와 당기기 때 접지에 사용합니다.
- **애니메이션**: 달리기, 발사, 당기기, 백스텝의 관절 키를 연속 보간합니다. 전투에서는 자세가 바뀔 때 짧게 섞고, 이동 중 발사하면 달리기 하체를 합성합니다. 대쉬(회피) 중에는 리그 대신 Codex로 만든 회피 통그림 3프레임(도약 → 공중 회피 → 착지 슬라이드)을 그리고, 잔상은 하늘색(`#6cc8ff`) 실루엣 한 가지로 통일했습니다.
- **실 속성**: 재봉 상자에서 가장 높은 단계의 세트가 실의 속성이 됩니다(동률이면 연결 크기). 혼돈 = 분홍 가시 실과 불씨, 절망 = 청록 물결 실과 물방울·파문, 좌절 = 노랑 번개 실과 스파크, 세트가 없으면 기본 분홍 실. 시각 효과만 바뀌고 규칙은 같습니다.
- **발사·당기기 연출**: 발사 시 손 앞 섬광과 바늘 문양, 비행 중 속도에 비례하는 혜성 꼬리와 바늘 늘임, 바닥에 꽂히면 떨림, 적에게 꽂히면 가위 베기 섬광. 당기면 히트스톱 동안 실이 팽팽하게 떨리다 가속하며 감겨 들어오고, 적은 당김 방향으로 늘어나며 끌려옵니다. 카메라가 주인공 쪽으로 튕기고 화면 섬광, 충격 링, `N HIT` 표시가 붙습니다.
- **VFX**: Blender — 명중 섬광, 실 당기기 파열, 하늘색 대쉬 리본·회피 돌풍, 바늘 혜성 꼬리, 발사 섬광, 당기기 충격 링, 보스 욕망 파동, 각성과 강화에 사용하는 바느질 파동. Blender에서 투명 PNG를 출력하고 Canvas에서 수명·회전·크기·잔상·가산 합성을 적용합니다. 브라우저에서 모션 감소를 켜면 화면 흔들림, 배경 입자, 효과 세기가 줄어듭니다.

---

## 6. 프로토타입 구조 (이 저장소)

```
index.html            게임 화면과 메뉴 마크업
rig.html              캐릭터 리깅 에디터 (파츠 피벗/부모/각도, 애니메이션 키, 저장)
server.js             정적 서버 + POST /api/save (리그 JSON 저장) — `node server.js` → http://localhost:5173
src/
  rig/runtime.js      리그 로딩·포즈 계산·그리기 (게임/에디터 공용)
  game/main.js        입력·화면·오디오·등반 진행 연결
  game/combat.js      전투·적 행동·라운드·증강·욕망
  game/gear.js        보드·세트·저장
  game/render.js      쿼터뷰 투영, 캐릭터, VFX
  game/style.css      화면 스타일과 모바일 조작 UI
assets/
  raw/                원본 시트 및 hero_quarter_atlas.png
  sprites/            크로마키 제거 후 자른 파츠/적/아이콘 PNG
  rig/hero-quarter.rig.json   게임과 에디터에서 쓰는 리그
  rig/hero.rig.json           초기 모델 (rig.html?legacy=1로 열기)
  vfx/tower-vfx.blend        편집 원본, 효과별 5개 씬
  vfx/*.png                   실제 게임이 로드하는 Blender 렌더 (흰색 텍스처는 실행 중 속성 색으로 칠함)
  vfx/fx_*.png                Codex 이펙트 시트에서 자른 가산 합성용 텍스처 (혼돈·절망·좌절·가위·문양·당김)
  sprites/hero_dodge_{1,2,3}.png  Codex로 만든 회피 통그림
  ref/                기획서에서 뽑은 컨셉 이미지 (UI 초상화로 재사용)
tools/
  make_quarter_rig.py   PNG 파츠와 리그 재생성 (Pillow, NumPy)
  render_vfx.py         Blender 씬과 투명 PNG 재생성
  bake_codex_art.py     Codex 시트 자르기: `vfx`(검은 배경 → 알파) / `dodge`(크로마키 → 3프레임)
  codex_prompts/        초기 이미지 생성용 원본 프롬프트
```

### 리그 데이터 포맷 (`hero.rig.json`)
```jsonc
{
  "parts": [ { "id": "torso", "img": "hero_torso.png",
               "parent": null,           // 부모 파츠 id
               "pivot": [x, y],          // 파츠 이미지 안의 할핀 위치(px)
               "attach": [x, y],         // 부모 이미지 안에서 이 파츠가 붙는 위치(px)
               "z": 0, "scale": 1, "flipX": false, "tint": null } ],
  "anims": { "idle": { "fps": 12, "loop": true, "length": 1.0,
              "keys": { "<partId>": [ [t, deg], ... ] } } }
}
```
- 각도는 부모 기준 상대 회전(도). 런타임은 `stepFps`로 t를 양자화 → 키 사이 보간 여부는 `"interp": "step" | "linear"`.
- 필요한 애니메이션: `idle`, `run`, `shoot`, `pull`, `dash`, `hurt`, `die`.

### 캐릭터 및 VFX 제작

- 최종 캐릭터 시트는 내장 이미지 생성 도구로 제작했습니다. 프롬프트의 핵심 조건은 기존 종이 질감과 재봉사 의상을 유지하고, 오른쪽으로 45° 돌아 양쪽 눈이 보이며, 둥근 호박색 눈과 입 없는 주둥이, 꼬리의 뿌리·1/3·2/3에 황동 관절, 단색 녹색 배경을 사용하는 것이었습니다.
- `python tools/make_quarter_rig.py`로 지정한 파츠를 자르고 기준 리그를 생성합니다. 리그 에디터로 수동 수정한 내용이 있으면 재생성 전에 JSON을 별도 보관합니다.
- `blender --background --python tools/render_vfx.py -- <프로젝트 절대 경로>`로 Blender 씬과 384×384 RGBA PNG 아홉 장을 생성합니다. Codex 원본은 `tools/codex_prompts/dodge.txt`, `vfx.txt` 프롬프트로 `assets/raw/`에 만든 뒤 `python tools/bake_codex_art.py vfx|dodge`로 자릅니다. 실행 에디터에서 애니메이션과 피벗을 확인할 수 있습니다.

<details>
<summary>최종 파츠 시트 이미지 생성 프롬프트</summary>

> Precise image edit of the provided game part atlas. Keep the 1536x1024 horizontal layout fitted naturally, all 11 loose paper-cut doll pieces in the same rows and positions. Change the 45-degree three-quarter view mouse face to innocent and kind: two round open honey-brown eyes, tiny highlights and gentle faint eyebrows, no stern or angled brow. **Mouthless design**: the entire pink muzzle below the round nose is completely smooth fur, no mouth opening, no smile, no mouth line, no lip line, no cheek line resembling a mouth, no teeth. Keep round pink nose, whiskers and youthful face. Keep rich dark burgundy coat, all brass pivot rivets. Tail bottom left remains a continuous long pink curved part but now has three small gold pivot rivets along its spine at root, one-third and two-thirds. All pieces sit on a perfectly flat solid chroma key green #00FF00 background (even the holes in the sewing needle). No shadows, no gradient, no text, no guides. Precise sharp black outlines and paper grain within the parts.

</details>

---

## 7. 정해야 할 것 (p27 + 구현 중 생긴 질문)
- [ ] 정식 1P GDD 확정 (이 문서 요약을 프로토타입 기준으로 사용)
- [ ] **바늘 재공격 규칙** (원문 끊김) — 현재 `hi` 레퍼런스대로 가닥마다 교차 타격 집계, 바늘이 꽂힌 적에게 전체 타격 전달
- [ ] 라운드 클리어 조건 (현재: 타이머 생존 + 잔여 적 처치)
- [ ] 욕망 누적량, 폭주/각성 효과·확률(현재 25%), 폭주 후 리셋값(현재 70%)
- [ ] 세트 종류와 세트별 효과 (현재 임시 3종), 절망·좌절 활성화 숫자
- [ ] '붙어있다' 판정 — 최대 연결 그룹 vs 같은 색 전체 개수
- [ ] 퍼즐 아이템이 등반 종료 시 유지되는지
- [ ] 타임 세일 시간/할인율
- [ ] 주인공 종족·이름, 수치 밸런스, 플랫폼, 타깃

### 임시 명칭
욕망의 탑(제목) · 예견의 다락(영구 성장 건물) · 모이라(마녀) · 재봉 상자(퍼즐 인벤토리) · 폭주/각성 · 단추(떠돌이 상인)

---

## 8. 2026-11-19 업데이트 — 타격감 · VFX · 플랫 아트 · 셰이더

### 아트 파이프라인 (ChatGPT 웹 → 크로마키 → 리그)
- `assets/raw/hero_flat_atlas.png`: 플랫·2.5등신 파츠 아틀라스 (기존 아틀라스와 같은 배치). `python tools/make_quarter_rig.py flat` 로 파츠 분할 + `assets/rig/hero-quarter.rig.json` 재생성. 옛 할핀 좌표는 조각 박스 내 상대 위치로 자동 이전, 다리 ×1.38 연장(`LEG`), 발 접지 높이는 FK로 자동 계산.
- 예전 스프라이트/리그 백업: `assets/sprites/_old_quarter/`.
- 회피 3프레임: `assets/raw/hero_flat_dodge.png` → `python tools/bake_codex_art.py dodge assets/raw/hero_flat_dodge.png`.
- 통그림(전신 키포즈) 레퍼런스: `assets/raw/hero_pose_locomotion.png`, `hero_pose_combat.png` → `assets/ref/poses/pose_*.png` (12장).

### 모션: 통그림 비교 + 통그림 섞기 (`rig.html` → "통그림 비교")
- 레퍼런스를 리그 위에 반투명으로 겹쳐 보고, **자동 맞춤**(`src/rig/posefit.js`)이 실루엣 IoU + 색 영역 IoU + 접지 제약으로 관절각을 찾는다. 결과를 보고 손으로 미세 조정 후 저장.
- **전 클립 통그림 맞춤**: idle/run/shoot/pull/hurt 키를 통그림 박자로 재작성 (run 후반은 다리 교대 + 팔 흔들림 반전).
- **통그림 섞기 (cels)**: 리그 JSON `cels` + `src/rig/editor.js`의 `CEL_PLAN`으로 특정 구간을 전신 그림으로 교체하는 기능. 현재 쓰는 구간 없음 — 기존 통그림은 리그보다 머리가 약 1.27배 커서 교체 순간 머리가 커 보였다. 리그 비율에 맞춘 통그림을 새로 뽑으면 다시 켤 수 있다. 회피(Space)는 별도의 플랫 통그림 3프레임.

### 타격감 (`src/game/combat.js` 상단 상수)
- 바늘: 40ms 급가속 후 사거리 끝에서 무겁게 감속 (`NEEDLE_SPEED`), 발사 반동 — 주인공이 뒤로 밀렸다 튕겨 돌아옴 + 카메라 킥 (`RECOIL`).
- 박힘: 35ms 히트스톱, 적이 바늘 방향으로 밀림, 바늘이 파고들었다 튕겨 나옴, 실이 퉁기듯 떨리다 팽팽해짐 (`STICK`).
- 당기기(그웬 풍): 실이 칼날 광선으로 변해 대상을 관통, 흰 임팩트 프레임(히트스톱 동안), 초승달 베기 + 가위 X 스닙 + 렌즈 섬광, 적 스프라이트가 칼선 따라 **두 동강**으로 벌어졌다 붙음, 처치 시 반쪽이 미끄러지며 떨어짐 (`CUT`).

### Blender 플립북 VFX (`tools/render_flipbooks.py`)
- `blender -b -P tools/render_flipbooks.py -- <프로젝트 경로> [이름...]` → `assets/vfx/flip/<name>_core.png`(흰 코어) / `_rim.png`(속성 색으로 곱해 쓰는 회색 외곽), 메타 `src/game/flipbooks.js`.
- puncture(박힘), slash(초승달 베기), snip(가위 X), flash(섬광), muzzle(발사), thunk(바닥 박힘). 프레임마다 파편·칼날 형태가 실제로 변하고, 런타임은 페이드 없이 프레임 단위로 재생.

### 후처리 셰이더 (`src/game/shader.js`, `?fx=0` 으로 끔)
- 유령 신부: 채도 낮춤, 청회색 그림자 / 바랜 분홍·상아 하이라이트, 떠 있는 검정.
- 굶지마: 종이 섬유결, 거친 잉크 외곽 덧칠(Sobel), 8fps 손그림 보일링, 비네트(욕망 수치가 높을수록 붉게 조여 옴), 12fps 필름 그레인·촛불 깜빡임.
- 아트는 플랫(질감 없음)으로 두고 질감·톤은 셰이더가 담당.

### 달리기 절뚝임 수정 (2026-11-19)
- 원인: ① 통그림 자동 맞춤이 박자마다 따로 맞춰져 좌우 다리·몸 높이가 어긋남, ② 다리 연장 후 뒷다리가 12% 짧아 뒷발로 디딜 때 몸이 더 꺼짐.
- `python tools/symmetric_run.py` 가 run 클립을 생성: 뒷다리 = 앞다리 곡선을 반 박자 늦춘 것 + 보정값(`FAR_OFFSET`), 뒷발 접지 깊이 `FAR_DEPTH = -23`. 뒷다리 길이는 앞다리와 같게(`make_quarter_rig.py`).
- 결과: 앞발/뒷발 접지 시간 8:8 프레임으로 같고, 두 걸음의 몸 높이 차 ≤ 약 5 리그 단위(화면 0.5px).
- run 은 에디터의 "전 클립 통그림 맞춤" 대상에서 뺐다.
- 몸 흔들림 축소: run 에서 골반 회전 0, 몸통·머리 고정 기울기(회전하면 큰 머리가 지렛대처럼 흔들림). 접지 보정은 `rig.data.groundDamp.run = 0.35` (런타임 `groundedMatrices(rig, pose, base, clip)` 가 사이클 평균 기준으로 35%만 적용). 머리 흔들림 좌우 26→2, 상하 21→7 리그 단위. 발은 최대 약 8단위(화면 0.8px) 파묻히거나 뜰 수 있음.

### 상체/하체 분리 (2026-11-19)
- 하체 파츠: `assets/raw/hero_flat_lower.png` (ChatGPT — 골반[허리띠+바지 엉덩이, 할핀 3개], 앞 코트 자락 근측/원측).
- `python tools/add_lower_body.py` → 현재 리그에 끼워 넣음(애니메이션 유지, 재실행 가능). 계층: `hips → pelvis → 허벅지…`(하체), `hips → torso → 머리·팔·앞자락`(상체). 허리 위아래가 따로 회전.
- 조정값은 스크립트 상단: `PELVIS_WIDTH`, `PELVIS_TOP`, `THIGH_WORLD`(다리 길이), `SKIRT_*`. 버클도 황동색이라 할핀 검출은 "가장 아래 두 개"를 다리 할핀으로 씀.
- 리그 전체를 다시 만들 때 순서: `make_quarter_rig.py flat` → `add_lower_body.py` → `symmetric_run.py` (달리기 대칭 보정 자동 재계산, `tools/rigfk.py` = 파이썬 FK).
- 하체 추가 전 리그 백업: `assets/sprites/_old_quarter/hero-quarter.before-lower.rig.json`.
- ChatGPT 무료 플랜 이미지 한도에 걸림(2026-11-19 기준 23시간 후 재설정).

### v3 캐릭터 — 통그림 비율로 전면 교체 (2026-11-19)
- 기준: `assets/raw/hero_pose_locomotion.png` (사용자 확정 룩). 큰 얼굴 + 속눈썹, 가는 상체, 겉옷처럼 걸친 코트, 치마, 짧은 부츠.
- 아트: `assets/raw/hero_v3_atlas.png` (Codex, 15조각 — ChatGPT 무료 한도로 Codex 사용), 회피 `assets/raw/hero_v3_dodge.png`.
- `python tools/make_v3_rig.py` (`--preview <png>` 로 조각·할핀 검출 확인) → `assets/sprites/hero_v3_*.png` + 리그. 할핀(황동 점) 자동 검출로 관절 결정. 이어서 `python tools/symmetric_run.py`.
- 코트 레이어: `coat_shoulders`(깃·어깨, 몸통 위) · `coat_front_near`/`coat_front_far`(앞자락, 어깨에서 늘어짐) · `coat`(뒷자락). 앞자락·치마는 뒷자락 흔들림을 작게 따라감(2차 동작).
- 비율 조정값: `HEAD_SCALE 1.74 / LEG_SCALE .5 / ARM_SCALE .72` → 머리 55% · 몸통 30% · 다리 15% (통그림 대기 포즈 측정). 키는 `TARGET_HEIGHT 960`.
- 모션: idle = 손으로 만든 숨쉬기, run = 대칭 생성, shoot/pull/hurt = 통그림 자동 맞춤(끝 자세는 기본 자세). 바늘은 자동 맞춤에서 ±20°로 고정(가늘어서 실루엣으로 방향을 못 읽음).
- 이전 리그 백업: `assets/sprites/_old_quarter/hero-quarter.before-v3.rig.json`.
- 통그림 섞기(cels)는 지금 비율이 통그림과 맞으므로 다시 켤 수 있음(`CEL_PLAN`) — 아직 꺼져 있음.

### v3b — 목 없는 머리 · 몸통에 씌우는 코트 · 레이어 정리 (2026-11-19)
- `assets/raw/hero_v3b_parts.png` (Codex): 목 없는 머리, 코트 몸판(앞이 뚫려 조끼가 보임, 어깨 할핀 2개), 허리 아래 밑단 근측/원측. `make_v3_rig.py` 가 있으면 자동으로 교체 사용.
- 코트 몸판은 양 어깨 할핀을 몸통 어깨 할핀에 겹치는 닮음 변환(크기·각도 자동)으로 몸통에 씌움 → 몸통이 굽으면 같이 굽는 "입은 옷".
- 머리: 턱 밑 할핀 위치만 쓰고 그림에서는 지움(`erase_pins` + `trim_chin`), 몸통의 목 토막도 잘라냄. 깃 안쪽으로 `NECK_SINK = 32` 만큼 내려 꽂음.
- 레이어(뒤→앞)는 `make_v3_rig.py` 의 `Z` 표: 꼬리 · 뒷자락 · 뒤팔 · 뒤 밑단 · 뒷다리 · 앞다리 · 치마 · 조끼 · 앞 밑단 · 코트 몸판 · 머리 · 앞팔(바늘은 앞팔과 손 사이).
- 백업: `assets/sprites/_old_quarter/hero-quarter.before-v3b.rig.json`.
