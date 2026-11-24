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
- **실에 붙는 이펙트는 실을 완전히 덮게 만듭니다.** 이펙트가 도는 동안 맨 실(가는 선·땋은 결)이 보이면 안 됩니다. 실보다 넓은 속성 빛과 장식으로 감싸 화려함을 강조합니다. 밝은 색은 여러 가닥이 겹치면 하얗게 날아가므로 세기를 낮춥니다. (2026-11-23 사용자 요청, 11차 참고)
- **이미지 도구:** 실·이펙트처럼 움직이는 VFX는 **Blender 5.3** CLI 스크립트로 만듭니다(`C:/Program Files/Blender Foundation/Blender 5.3/blender.exe -b -P tools/render_*.py -- C:/Project/TOD`). 아이콘·조각 같은 그림 한 장짜리는 **Codex CLI**로 만듭니다(`%LOCALAPPDATA%\OpenAI\Codex\bin\<해시>\codex.exe exec -s workspace-write --skip-git-repo-check -i <참고 그림> - < tools/codex_prompts/<이름>.txt`). Unity↔Blender 실시간 연결은 MCP `blender_bridge`입니다(아래 "Blender 연결").
- 작업 전 이 규칙을 먼저 확인합니다. 판단이 애매하면 사용자에게 질문한 뒤 진행합니다.

## Unity 포팅 결정 사항 (2026-11-23)

| 항목 | 결정 |
|---|---|
| 뷰 | 2D 직교 + Y정렬. 웹판의 1000×700 전투장 좌표와 `PROJECTION_Y .67`을 그대로 씁니다. |
| 렌더 파이프라인 | URP 2D 렌더러, Linear 색공간 |
| 주인공 리그 | **(2026-11-23 변경)** Unity 뼈대 계층(`Arena/Hero/Rig`) + C# 동작(`HeroRig`). 웹 `rig.html`·rig json 은 더 이상 Unity 에서 읽지 않습니다. |
| 웹판 | 동결하고 `web/`로 옮깁니다. Unity Assets에는 런타임 아트만 두고, 원본 시트와 .blend는 `art_src/`로 옮깁니다. |
| UI | UI Toolkit, **주아(Jua) 폰트** (14차에 갈무리에서 바꿈. 갈무리는 주아에 없는 기호 전용 대체 글꼴로만 남음) |
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

## 19차 요청 (2026-11-24) — 다락의 예견가 모이라, 강화 위치 고르기, 시작 조각 챙기기(1회용), 아웃게임 재봉 상자 제거 — 완료

- **사용자 결정:**
  - 다락 방의 실궤 대신 **예견가 모이라**(거미, 탁자 앞에 앉아 점성구를 읽음)를 둡니다. 쥐로 먼저 뽑았다가 사용자 요청으로 거미로 바꿨습니다(쥐 그림은 `art_src/raw/moira_rat.png`로 남김).
  - 모이라에게 말을 걸면 강화 4종(인벤토리 확장, 가로 줄 강화, 세로 줄 강화, 욕망 최대치)을 합니다. 확장과 줄 강화는 재봉 상자 판이 열리고, 마우스를 올린 자리가 반투명하게 보이며 눌러서 고릅니다.
    - 확장은 판 네 가장자리 중 한 곳에 줄 하나를 붙입니다(판은 직사각형 유지).
    - 가로/세로 줄 강화는 한 번에 한 줄입니다.
  - '시작 조각 선택'(들고 갈 수) 강화는 상인 단추에게 넘깁니다(이름 '들고 갈 수 늘리기').
  - **조각은 영구로 사지 않습니다.** 단추의 '시작 조각 챙기기'에서 진열 조각을 파편으로 사서 판에 놓고, 다음 등반 한 번만 들고 갑니다. 진열은 무작위 4개(등반 끝마다 새로) + 풀린 아버지의 도안입니다.
  - 옛 저장의 조각(처음 지급 4개와 산 것)은 돌려주는 파편 없이 지웁니다.
  - 아웃게임 재봉 상자(재봉실 시설, 결과 화면 버튼)를 없앱니다. 재봉 상자는 등반 중(I)과 챙기기 판, 모이라의 고르기 판에서만 열립니다.
- **구현:**
  - `Profile` version 3: `extraCols/extraRows`(확장으로 붙인 줄), `rowLines/colLines`(강화한 줄 번호), `expansion` = 확장 횟수(등급 한도 = 기본 + 횟수 × `costLimitStep`). 옛 `rows/cols` n → 위(왼쪽)부터 2n 줄, 옛 `expansion` e → 오른쪽과 아래에 e 줄씩, 횟수 2e. 새 프로필은 조각 없이 시작합니다(`gear.startingKinds`는 이제 안 씀).
  - `Sim/Moira.cs`: 단계, 가격, 최대(가로/세로 줄은 판 줄 수가 한도), `CanPick`, `Apply`, `Expand`(위/왼쪽이면 놓인 조각과 줄 번호가 한 칸 밀림).
  - `Sim/HubShop.cs`: `Offers`, `Pack`(진열 조각을 판에 놓으면 삼, 결과 `PackResult`), `Unpack`(빼면 파편 돌려줌), `ConsumePacked`(`StartRun`에서 등반 가방을 만든 뒤 영구 프로필의 조각과 배치를 비움).
  - 판: `GameController.RenderInventory`가 `BoardMode`(pick = 고르는 강화, offers = 진열 가짜 조각 id `OfferId + 종류`)를 넘깁니다. 진열 줄은 끌거나 눌러 판에 놓으면 삽니다. `UI/GameUI.Board.cs`가 제목과 안내, 강화한 줄 금빛 띠, 올린 줄 반투명 띠(이미 강화한 줄이면 붉게), 확장 가장자리 띠(`Slots.uxml` `pick-band`, `pick-strip`)를 그립니다. 확인하면 판에 남아 바뀐 줄을 보여 주고, Esc나 돌아가기로 모이라 창에 돌아갑니다.
  - 모이라 창 = 예전 실궤 창(`UiScreen.Attic`): 왼쪽 그림(`attic-portrait`), 말을 걸 때마다 `hub.moiraLines`에서 한 줄, 카드 4장. 확인 창(`dialog-upgrade`)의 머리 문구는 모이라와 단추가 따로 씁니다.
  - 단추 창(`dialog-hubshop`) = 카드 2장(챙기기, 들고 갈 수 늘리기).
  - `HubStation.Chest` → `Moira`(같은 번호). `SewingBox` 값은 직렬화 번호 때문에 남겼고 시설 데이터에서는 뺐습니다.
- **그림:** `tools/codex_prompts/moira.txt`(참고 `merchant_idle.png`) → `art_src/raw/moira.png` → `python tools/bake_attic_art.py` → `Assets/sprites/hub_attic_moira.png` → `TOD/Setup/1. Texture Imports`.
- **MCP 데이터(임시값, 씬 저장):**
  - 강화: 확장 6+4(최대 4, 한 번에 줄 하나, 등급 한도 +1, `gear.costLimitStep` 2 → 1), 가로/세로 줄 4+3(최대 = 판 줄 수), 욕망 최대치 그대로, starter 이름 '들고 갈 수 늘리기'.
  - 시설: 재봉실의 재봉 상자를 뺐고, 다락 Moira 시설은 이름 '예견가 모이라', 문구 '모이라에게 말 걸기', 높이 210, 막는 타원 (95, 30).
  - `hub.shopStock` 4. 단추 메뉴, 챙기기 판, 모이라 문구(`hub.pack*`, `slot*`, `moira*`, `pick*`, `confirm*`, `sideNames`, `lineTaken`)를 넣었고, `gear.starterFull` 문구도 바꿨습니다.
- **확인:**
  - EditMode 45개 통과. 새 테스트는 `MoiraUpgradesChosenLineAndSide`, `PackedPiecesLastOneRun`, `PackingSpendsShardsAndRefunds`, `OldSaveDropsPiecesAndKeepsUpgrades`입니다.
  - 플레이 캡처(`Temp/Shots/`):
    - `moira19_room`: 다락 방의 모이라
    - `moira19_talk`: 모이라 창
    - `moira19_pick_rows`, `moira19_row_done-1`: 줄 위 반투명 띠, 강화한 줄 금빛
    - `moira19_pick_expand`, `moira19_expand_top-1`: 위에 붙이자 판이 7×6이 되고 강화한 줄이 3 → 4번째로 밀림
    - `moira19_merchant_menu`: 단추 메뉴
    - `moira19_packing`, `moira19_packing_prices-3`: 챙기기와 가격 꼬리표
    - `moira19_packing_prices`: 재봉실에 재봉 상자가 없음
  - 빼면 파편이 돌아오고, 등반을 시작하면 챙긴 조각이 등반 가방에만 남는 것도 확인했습니다. 실제 프로필은 `Temp/profile_backup_19.json`(v2)에서 되돌렸으므로, 다음 실행 때 v3로 옮겨지며 조각 5개가 사라집니다.
- **남은 것:**
  - 사람이 마우스로 직접 고르는 느낌은 확인하지 않았습니다. 확장 띠는 Linear 색공간이라 생각보다 밝게 보여 알파를 0.07로 낮췄지만, 낮춘 값은 캡처로 확인하지 못했습니다.
  - 단추 메뉴에서 챙기기 판을 열면 대화 상자가 닫히며 단추의 작별 대사가 나옵니다.
  - 모이라 그림이 주인공보다 꽤 큽니다(높이 210).
  - 문구와 가격은 임시입니다.

## 18차 요청 (2026-11-24) — 일기가 뒤로 갈수록 피에 젖고 욕망에 사로잡힌 말투 — 완료

- **문구 (`GameDatabase.diary.entries`, MCP):**
  - 1장은 그대로입니다(차분하고 아이를 걱정함).
  - 2장부터 집착이 점점 커집니다. 2장은 실이 피에 물들어도 "곱다"고 하고, 아이 이름을 적는 걸 잊습니다. 3장은 외투를 "갖고 싶었다", 닦아도 안 지워지는 바늘, 줄 그은 "집에 돌아가"가 나옵니다. 4장은 "갖고 싶다"(굵게), "좋다"를 반복하고, 줄 그은 "위층으로 오지 마라" 뒤에 "위층은 내 것이다"를 씁니다. 5장은 "더. 더. 더 많이.", 자기 피로 실을 꿰고, 줄 그은 당부 끝에 "네 이름이 생각나지 않는다"로 끝납니다.
  - 흡수 줄(주인공 글씨)도 갈수록 욕망을 닮아 가고(5장 "더, 더."), 거절 줄은 끝까지 단단하게 버팁니다.
  - 줄 긋기와 굵게는 리치 텍스트 `<s>`, `<b>`로 씁니다.
- **핏자국:**
  - 쪽지마다 `blood`(0, .2, .45, .7, 1)만큼 핏자국을 흩뿌립니다(`DiaryBoard.Stain`). 자리는 층 번호로 정해지는 시드라서 매번 같습니다. 게시판 쪽지는 크게 읽기의 절반 크기와 수입니다.
  - 본문 잉크(`ink`)는 1장 기본색에서 5장 #5a1111로 갈수록 핏빛이 됩니다.
  - 그림은 Codex `tools/codex_prompts/blood_stains.txt` → `python tools/bake_blood_stains.py` → `Assets/UI/Paper/blood_1~6.png`(튄 자국, 문지른 자국, 생쥐 발자국, 흘러내림, 골무 고리, 점 뿌림)입니다. 값은 `diary.bloodStains`, `bloodMaxStains` 7, `bloodSize` 100~240, `bloodAlpha` .7~1, `bloodTint` #e6d6ce입니다.
- 확인: EditMode 43개 통과. 캡처 `Temp/Shots/diary18_board2.png`(1장 깨끗 → 5장 흠뻑), `diary18_page5b.png`(5장 크게 읽기), `diary18_strike.png`(줄 그은 글씨 확대). 프로필은 백업에서 되돌렸습니다.
- 남은 것: 문구는 초안이니 사람이 다듬어야 합니다. 줄 긋기 선이 가늘어 작은 쪽지에서는 잘 안 보입니다.

## 17차 요청 (2026-11-24) — 등반 조각 초기화, 시작 조각, 걸어 다니는 다락, 모이라의 실궤, 일기 보상, Esc

> 작업이 끊기면 여기서 이어 갑니다. 단계를 마칠 때마다 체크합니다.

- **사용자 결정:**
  - Esc로 열린 창(재봉 상자, 다락 창, 일기, 상점, 확인 창 등)을 닫습니다.
  - 다락 창 종이의 오른쪽 위에 기억의 파편 수를 표시합니다.
  - **등반 조각과 능력치는 등반 전용입니다.** 귀환하면 그 등반에서 얻은 조각, 배치, 증강이 모두 사라집니다. 재봉 상자는 아웃게임에서도 열 수 있지만, 거기서는 시작 조각만 다룹니다.
  - **시작 조각만 영구입니다.** 기억의 파편으로 사서 영구히 가지며, 등반 전에 재봉 상자 판에 골라 놓으면 들고 갑니다. 가격(임시)은 일반 3, 레어 4, 유니크 5, 레전더리 6입니다. 판매처는 재봉실 상인 단추입니다.
  - 들고 갈 시작 조각 수는 '시작 조각 선택' 강화 단계마다 1개씩 늘어납니다. 1단계 3개, 2단계 4개, …
  - **예견의 다락은 걸어 다니는 방입니다.** 재봉실의 다락 사다리에서 E를 누르면 다락 방으로 올라갑니다. 방 안 상자 **'모이라의 실궤'**(E)에서 강화 5종을 합니다: 인벤토리 확장, 가로 줄 강화, 세로 줄 강화(새로 추가), 욕망 최대치, 시작 조각 선택(들고 갈 칸 수). 뚜껑문(E)으로 내려갑니다.
  - **아버지의 일기 보상:** ① 새 일기 조각을 처음 읽으면 기억의 파편을 줍니다. ② 조각마다 '아버지의 도안'(전용 시작 조각)이 하나씩 풀려 상인 단추에게서 살 수 있습니다. 도안은 탑 보상과 상점 무작위 진열에는 나오지 않습니다.
- **구현 결정:**
  - `Profile`은 영구 저장만 맡습니다: 파편, 강화, 일기, 시작 조각(`items`), 등반 전 배치(`placements`). 등반은 `RunState.bag`(`Gear.RunBag`가 만든 임시 Profile: 강화 단계는 같고, 조각은 판에 놓인 시작 조각만)을 씁니다. 탑 보상, 다시 굴리기, 엘리트 드롭, 등반 중 재봉 상자는 모두 `bag`을 씁니다.
  - 옛 저장(`version` 1)의 조각은 예전 규칙으로 얻은 등반 조각이라서, 불러올 때 `gear.startingKinds`만 남기고 배치를 비웁니다(`version` 2). 파편, 강화, 일기는 그대로 둡니다.
  - 15차의 '시작 조각 1개 고르기'(`starterKind`, 등급 합 0)는 없앱니다. 시작 조각도 등급 합 한도에 들어갑니다.
  - 들고 갈 수 = `gear.starterSlotsBase + 단계 × gear.starterSlotsStep`(값은 데이터, 0단계 2개).
  - 세로 줄 강화 = 왼쪽부터 두 칸 단위 세로 줄 위 조각 능력치 +`rowBonus`. 가로 줄과 겹치는 칸은 둘 다 받습니다.

### 단계 — 17차 완료
- [x] 1. 코드: 프로필 분리(`Gear.RunBag`, `RunState.bag`, v1 → v2 이전), 시작 조각 칸 수(`Unlocks.StarterSlots/StarterFull`), 세로 줄 강화(`Profile.cols`), 상인 = 시작 조각(`HubShop.Display`가 풀린 도안을 붙임), 일기 보상(`Diary.MarkRead`가 파편을 주고 true를 돌려줌, `Diary.PatternOpen/OpenPatterns`, 도안은 `Gear.IsPattern`이라 `Unowned`에서 빠짐)
  - 15차 시작 조각 1개 고르기(`PlaceStarter`, `dialog-starter`, `attic-starter` 줄)는 지웠습니다. 재봉 상자는 `GameController.Bag`(등반 중 = 등반 가방, 아웃게임 = 영구 프로필)을 다룹니다. 아웃게임 재봉 상자에는 "등반 준비: 판에 놓은 시작 조각 N / M개" 안내가 뜹니다.
- [x] 2. 코드: 다락 방(`HubRoom`, `HubConfig.attic/atticReturn/roomFade/roomSound`, `HubStation.Chest/AtticExit`, `HubView.SetRoom/Room/InAttic`, `GameController.MoveRoom/GoToAttic`), 실궤 창(`UiScreen.Attic`을 다락 방 위에 `.over-room`으로 띄움, 종이 오른쪽 위 `attic-shards`), 방 전환 어둠(`room-fade`, `GameUI.FadeRoom`), 위 띠 방 이름(`GameUI.SetHubRoom`), Esc(`GameController.Escape`: 열린 창 → 재봉 상자, 실궤 창 → 등반 중이면 일시 정지. 일기는 DiaryBoard가 스스로, 욕망 제안은 못 닫음)
- [x] 3. 그림: `sh tools/codex_prompts/run_17.sh` → `python tools/bake_attic_art.py` → `Assets/sprites/attic_room.png`, `hub_attic_chest.png`, `hub_attic_exit.png`, `item_pattern_1~5.png` → `TOD/Setup/1. Texture Imports`. 도안 아이콘 1번 첫판에 디즈니 쥐 귀 같은 그림이 있어 프롬프트를 고쳐 다시 뽑았습니다.
- [x] 4. MCP 데이터(임시값, 씬 저장):
  - 가격 `hub.piecePrices` 0, 3, 4, 5, 6. `gear.starterSlotsBase` 2, `starterSlotsStep` 1(0단계 2개, 1단계 3개, 2단계 4개).
  - 실궤 강화 순서: 인벤토리 확장(8+5, 최대 2), 가로 줄 강화(6+5), 세로 줄 강화(새로 추가, 6+5, 최대 = 폭 / 2), 욕망 최대치(7+5, 최대 10), 시작 조각 선택(6+6, 최대 4).
  - 아버지의 도안 5개(조각 34~38): 아버지의 재단 도안(화염 레어 피해 4), 탑 문장 골무(얼음 레어 체력 16), 낡은 바늘집(번개 유니크 충전 .2), 금 간 안경(빛 유니크 정화 5), 미완성 자수틀(어둠 레전더리 피해 7). 일기 1~5장에 하나씩 연결했고, 처음 읽으면 파편을 층 × 3개 줍니다.
  - 다락 방: 배경 `attic_room`(0~1000 × 0~667), 걷는 영역 x 90~910 / y 330~890, 시작 (290, 650), 실궤 (500, 360) 높이 150, 뚜껑문 (175, 640) 높이 115. 재봉실로 내려오면 (225, 275)에 섭니다. 어둠 .22초, 효과음 `stairstep`.
- [x] 5. EditMode 43개 통과(새 테스트 6개: `StarterSlotsGrowPerLevel`, `RunPiecesStayInTheRunBag`, `ColumnUpgradeBoostsLeftColumns`, `OldSaveDropsRunPieces`, `DiaryReadGivesShardsAndPattern`, `StarterPricesByRank`). 플레이 캡처 `Temp/Shots/attic17_room.png`(다락 방, 실궤), `attic17_chest.png`(실궤 창, 파편 41), `attic17_inventory.png`(시작 조각 3/3, 4번째는 "3개까지" 안내), `attic17_shop.png`(상인 진열 + 아버지의 도안), `attic17_diary2.png`(쪽지 보상 줄). 등반 시작 → 가방 3개 → 조각 추가 → 귀환 뒤 영구 프로필 4개 그대로인 것을 확인했습니다. Esc로 실궤 창과 재봉 상자가 닫히는 것도 확인했습니다. 실제 프로필은 `Temp/profile_backup_17.json`에서 되돌렸습니다(다음에 실행하면 v2로 바뀌어 조각이 처음 4개로 돌아갑니다).
- **남은 것:**
  - 사람이 직접 걸어 본 느낌, 방 전환 어둠의 속도. 캡처는 정지 화면뿐입니다.
  - 이미 읽은 일기 조각(지금 프로필의 1장)은 파편을 다시 주지 않습니다. 도안은 읽은 기록으로 풀리므로 바로 진열됩니다.
  - 다락 방에도 "Enter 등반 시작" 버튼이 보입니다(Enter 바로 등반은 그대로 둠).
  - 모이라 캐릭터는 방에 없습니다(실궤만 있음).

## 예약 작업: 15차 — 재봉실 허브 + 이야기 해금 (2026-11-24 00:30 자동 시작) — 15차 완료

> 사용자는 자는 중입니다. **질문 없이 임의로 판단해 끝까지 진행**하고, 결정한 내용은 이 절에 적습니다. 끝나면 "15차 완료"로 바꿉니다.

- **목표:** 지금 메뉴뿐인 로비를 탑 아래 '재봉실' 방으로 바꿉니다. 주인공이 걸어 다니며 시설과 상호작용합니다.
- **재봉실 허브:**
  - 로비 화면에 재봉실 배경(방 바닥, 벽)을 깔고, 주인공이 WASD로 걸어 다닙니다. 전투장과 같은 렌더러와 주인공 리그를 쓰고, 적과 전투는 없습니다.
  - 시설(E로 상호작용): 재봉 상자(재봉 상자 화면), 예견의 다락(다락 화면), 상인 단추(조각 판매 상점: 파편으로 아직 없는 조각 구매), 탑 입구(등반 시작, 기존 Enter도 유지), 일기 게시판(이야기).
  - 시설 그림은 Codex CLI로 뽑습니다(초록 키 시트 → `bake_stage2_art.py`). 위치·문구·가격은 GameDatabase 데이터로 넣습니다.
- **등반과 연결:**
  - 처음 돌파한 층(`profile.best`)에 따라 세트가 열립니다. 예: 1층부터 화염·얼음·번개, 2층 돌파 시 빛·바람, 3층 돌파 시 어둠·피, 4층 돌파 시 독.
  - 잠긴 세트 조각은 탑 보상·상점·지급에서 빠집니다. 해금 표는 데이터 `gear.setUnlockFloor`입니다.
  - 해금될 때 재봉실에서 알림과 연출을 보여 줍니다.
- **이야기 해금:**
  - 층을 처음 돌파할 때마다 아버지의 일기 조각(층당 1개, 5개)이 풀려 재봉실 게시판에 붙습니다. 내용은 기획서 톤으로 쓴 임시 문구입니다(수인 재봉사 생쥐가 아버지를 찾아 욕망의 탑을 오름).
  - 보스 욕망을 흡수했는지(`profile`에 기록)에 따라 일기 마지막 줄이 달라집니다.
  - 게시판 UI는 UXML 템플릿 복제로 만들고, 저장은 `Profile`에 필드를 추가합니다.
- **반복 동기:** 다락 강화에 '시작 조각 선택'(등반 시작 때 가진 조각 중 1개가 판에 미리 놓임) 또는 '시작 세트 1단계' 같은 강화를 하나 추가합니다.
- **지킬 것:** 이 문서 맨 위 작업 규칙, 게임 문구에 가운뎃점 금지, 사용자에게 보이는 글은 한국어, 아이콘·그림은 Codex CLI, 움직이는 이펙트는 Blender 5.3, 단계마다 이 절 갱신, 커밋하지 않음, EditMode 테스트 통과, 플레이 캡처로 확인.

### 15차-outgame-unlock (세트 해금, 다락 강화 시작 조각) — 완료

> 이 세션이 맡는 일: 세트 해금, 해금 알림, 다락 강화 '시작 조각'. 허브 본체는 15차 허브 세션, 일기는 `diary-story` 세션.

- **결정:**
  - 해금 표는 `gear.setUnlockFloor`(`SetUnlock { set, floor }` 배열)입니다. `floor` = 필요한 `profile.best`(처음 돌파한 가장 높은 층). 화염, 얼음, 번개 0 / 빛, 바람 2 / 어둠, 피 3 / 독 4. 표에 없는 세트는 처음부터 열림.
  - 잠긴 세트 조각은 `Gear.Unowned`(탑 보상, 다시 굴리기, 상점 조각, 무작위 `AddItem`)에서 빠집니다. 이미 가진 잠긴 세트 조각은 그대로 둡니다(판에도 놓을 수 있음).
  - 개발자 모드: `Unlocks.ignoreLocks`(정적, 저장 안 함)를 "잠금 무시" 토글로 켜고 끕니다. 꺼져 있으면 세트 +1, 모든 조각 지급도 잠긴 세트를 건너뜁니다.
  - 해금 알림: `Profile.seenUnlocks`(세트 id 목록). 처음부터 열린 세트는 알림 없이 본 것으로 칩니다. 로비(허브)로 돌아올 때 새로 열린 세트가 있으면 `GameUI.ShowUnlockNotice`로 하나씩 띄웁니다.
  - 다락 강화 '시작 조각'(`upgrades` key `starter`, 단계는 데이터 `max`): 다락에서 가진 조각 중 1개를 작은 창으로 고릅니다(`Profile.starterKind`). 등반을 시작할 때 그 조각이 판에 없으면 같은 세트와 가장 많이 맞닿는 빈자리에 놓입니다. 시작 조각은 등급 합 한도에 들어가지 않습니다(판이 저장되므로 자리 확보만으로는 의미가 약해서).
- **진행:**
  - [x] 코드: `Sim/Unlocks.cs`(해금 표 계산, 알림 기록, 시작 조각 자리 찾기와 비용 0), `Gear.Unowned`가 잠긴 세트를 뺌, `Profile.seenUnlocks/starter/starterKind`, `GearConfig.setUnlockFloor/unlockSound/starterPlaced`.
  - [x] 다른 세션과 겹치지 않게 `GameController`, `GameUI`를 partial 로 바꾸고 새 코드는 `GameController.Outgame.cs`, `GameUI.Outgame.cs`에 둠. 원래 파일에는 연결 줄만(ShowScreen 로비 → `ShowUnlocks`, StartRun → `PlaceStarterPiece`, 다락 Level/Confirm 에 `starter`, Esc, Init → `InitOutgame`, 판 머리글 비용).
  - [x] UI: `Main.uxml`에 `unlock-notice`(띠, 빛줄기, 별 8개), 다락 `attic-starter` 줄, `dialog-starter` 창, 개발자 패널 "해금 (15차)" 줄(잠금 무시, 최고 층 +1, 초기화, 알림 보기). 스타일은 새 `Assets/UI/Outgame.uss`.
  - [x] MCP 데이터 입력(씬 저장함): `gear.setUnlockFloor` 8줄, `gear.unlockSound` "unlock", `gear.starterPlaced`, `sounds`에 `unlock`(사인 700→1760Hz, .55초, 음량 .12), `upgrades`에 `starter`(시작 조각, 파편 12, 1단계, 아이콘 `icon_needle`). 컴파일 오류 없음.
  - [x] 테스트 `Tests/OutgameTests.cs` 4개(해금 표, 잠긴 세트 제외(탑 보상 포함, 잠금 무시), 알림 한 번만, 시작 조각 배치와 비용 0). `CombatTests.ItemsAreUnique`는 `best = 99`로 모두 열고 검사하게 고침.
  - [x] EditMode 34개 전부 통과(05:32, 새 4개 포함).
  - [x] 플레이 캡처: `Temp/Shots/unlock_notice_2.png`(허브에서 "빛" 해금 띠, 1단계 효과 "빛 피해 +2, 결박, 정예 +20%"). 처음에는 띠가 화면 위(top 150)에서 일기 알림과 겹쳐서(`unlock_notice_1.png`) 주인공 아래(top 560)로 옮기고, 효과 수치를 세트 패널과 같은 `Amount` 표기로 바꿈.
  - [x] 다락 캡처: `starter_attic_none.png`(시작 조각 카드 "최대 단계"와 아래 줄 "아직 고르지 않았습니다"), `starter_picker.png`(고르는 창, 재봉 상자 줄 모양). 등반 시작: 시작 조각(피 묻은 가위, 등급 3)을 판에서 뺀 뒤 `StartRun` → 다시 놓이고 판 등급 합은 7 그대로(비용 0), 알림 문구 확인. 문구 조사가 어색해서 `gear.starterPlaced`를 "시작 조각을 판에 먼저 놓았습니다: {0}"로 바꿈(씬 저장).
  - [x] 실제 프로필을 백업(`Temp/outgame_profile_backup.json`)에서 되돌림(같은 JSON 확인). EditMode 34개 다시 통과.
- **남은 것:**
  - 전투 화면 캡처에는 HUD와 알림 글자가 빠집니다(UI 층이 캡처에 안 잡힘, 이 작업과 무관한 캡처 경로 문제). 등반 시작 알림은 코드로만 확인했습니다.
  - 해금 띠 움직임(튀어나옴, 빛줄기, 별)은 정지 캡처라 실제 속도로 사람 눈 확인이 필요합니다. 빛(상아색) 세트는 띠가 밝아 세트 색이 약하게 보입니다.
  - 허브 진입점이 로비 화면과 달라지면 그 자리에서 `ShowUnlocks()`를 불러야 합니다(지금은 `ShowScreen(UiScreen.Lobby)` 끝).
  - 시작 조각 강화는 1단계(데이터 `max` 1, 파편 12)만 있습니다. 값 다듬기와, 고른 조각을 재봉 상자 목록에서 표시하는 것은 하지 않았습니다.

### 15차-outgame-hub (재봉실 허브 본체) 완료
> 이 세션이 맡는 일: 방, 걷기, 카메라, 시설 5곳, 상인 단추 조각 상점, 시설 그림. 해금 알림은 `outgame-unlock`(`ShowUnlocks`가 로비 진입 때 이미 불림), 일기는 `diary-story`(`ui.ShowDiary()`).
- **결정:**
  - 허브 = 로비 화면. `UiScreen.Lobby`일 때 씬 `Hub` 루트(MCP)가 켜지고 `Arena`는 꺼집니다. 로비 UI 층은 투명해지고(배경 그림, 큰 메뉴 제거) 위쪽 띠(파편, 소리)와 아래 안내만 남습니다. `start/open-inventory/open-attic` 버튼은 Bind 때문에 남기되 작게(Enter 등반 버튼만 보임).
  - 좌표: 허브도 전투장과 같은 바닥 좌표(x, y 아래로 +, `Space.Ground`로 투영)를 씁니다. 주인공은 `Arena/Hero`를 **런타임에 한 번 복제**(템플릿 복제 예외)해 `Hub` 아래에 두고 `HeroView`로 그립니다(같은 리그와 동작). 걷기는 전투의 이동 계산을 흉내 낸 간단한 코드(`HubView`)이고 대쉬, 공격은 없습니다.
  - 데이터 = `GameDatabase.hub`(`Runtime/Data/HubData.cs`의 `HubConfig`): 배경 그림과 캔버스 영역, 걷는 영역, 시작/귀환 자리, 걷기 속도, 상호작용 거리, 카메라 줌, 시설 5개(`HubFacility`: 종류, 이름, 안내 문구, 바닥 위치, 그림, 높이, 막는 타원), 상점 진열 수와 등급별 파편 가격, 문구.
  - 시설: E = 가장 가까운 시설(거리 안). 재봉 상자 → 재봉 상자 화면, 예견의 다락 → 다락 화면, 상인 단추 → 조각 상점 창(`dialog-hubshop`), 탑 입구 → 등반 시작(Enter도 그대로), 일기 게시판 → `ui.ShowDiary()`. 가까우면 시설 위에 "E 문구" 꼬리표, 시설 그림이 살짝 밝아짐.
  - 상인 단추 상점: 파편으로 **아직 없는 조각**(`Gear.Unowned`, 잠긴 세트 제외)을 삽니다. 진열 = `Profile.hubStock`(조각 종류 목록, 기본 3개), 등반이 끝날 때마다 새로 진열하고 비어 있으면 채움. 산 조각은 "팔림". 가격 = `hub.piecePrices[등급 코스트]`. 로직은 `Sim/HubShop.cs`(테스트 대상).
  - 상인 그림은 `merchant_idle.png`(시설 `Merchant`의 sprite). `merchant` 세션이 자세를 바꿔 끼울 수 있게 `HubView.FacilityRenderer(HubStation)`을 둡니다.
  - 새 코드는 `GameController.Hub.cs`, `GameUI.Hub.cs`, `View/HubView.cs`에 두고 공유 파일에는 연결 줄만 넣습니다.
- **진행:**
  - [x] Codex 그림: `tools/codex_prompts/hub_room.txt`(재봉실 배경), `hub_facilities.txt`(초록 키 2×2) → `sh tools/codex_prompts/run_hub.sh` → 굽기 `python tools/bake_hub_art.py`. 시설 4장 `Assets/sprites/hub_sewing_box/attic/tower_door/diary_board.png`(피벗 아래 가운데).
  - [x] 코드: `Data/HubData.cs`(HubConfig, HubFacility, HubStation), `Sim/HubShop.cs`, `View/HubView.cs`, `Game/GameController.Hub.cs`, `UI/GameUI.Hub.cs`, 테스트 `Tests/HubTests.cs`(3개). 공유 파일 연결 줄: GameController(Start `InitHub`, ShowScreen `SyncHub`, Finish `HubAfterRun`, Interact `Run == null → HubInteract`, Esc `hubshop`, Update `TickHub`), GameUI(Init `InitHub`, 탑 아이템 카드를 `PieceOfferCard`로 뽑아 상점과 같이 씀), GameDatabase(`hub`), Profile(`hubStock`).
  - [x] UI: 로비 층을 허브 HUD로(위 띠 "재봉실", 꼬리표 `hub-prompt`, 아래 안내 `hub-hint`, 오른쪽 아래 등반 버튼), `dialog-hubshop`, 결과 화면 버튼 "재봉실로". 스타일 `Assets/UI/Hub.uss`, 가격 띠 템플릿 `Assets/UI/HubSlots.uxml`.
  - [x] 그림 완료: 배경 `Assets/sprites/hub_room.png`(1536×1024, 벽 아래 끝이 위에서 31%), 시설 4장. 상인 `merchant_idle~farewell` 6장을 Sprite로 임포트(피벗 아래 가운데 (0.5, 8/475)).
  - [x] MCP: 씬 `Hub`(HubView) > `Backdrop`(Background 층) + `Facilities/FacilityTemplate`(Actors 층, 피벗 기준 Y 정렬, 꺼 둠). 주인공은 런타임 복제라 씬에 없음. `GameController.hub/hubRoot`, `GameUI.hubSlots` 연결. `diary.showLobbyButton` = false.
  - [x] 데이터(임시, `GameDatabase.hub`, 플레이로 한 번 고침): 배경 캔버스 0~1000 × 0~667(방 전체가 거의 한 화면, 줌 0.95), 걷는 영역 x 60~940 / y 205~850, 시작 (500, 560) 깔개 위, 귀환 (500, 290) 탑 문 앞, 걷기 220, 거리 110. 시설(바닥 좌표, 높이 px): 탑 입구 (500, 197) 175 / 다락 (225, 207) 180 / 게시판 (775, 207) 140 / 재봉 상자 (175, 580) 110 / 상인 (825, 580) 105. 진열 3, 가격 일반 4, 레어 7, 유니크 11, 레전더리 16 파편. 꼬리표는 위 띠에 가리지 않게 화면 위 150px 아래로 제한.
  - [x] 씬 저장, EditMode 34개 통과(허브 3개 포함), 프로필은 테스트 전 값으로 되돌림.
  - [x] 플레이 캡처 `Temp/Shots/hub_*.png`: 시작(`hub_start-2`), 걷기(`hub_walk`), 꼬리표(`hub_prompt_sewing/merchant/diary/tower-2`), 재봉 상자(`hub_sewing_box-1`), 상인 상점(`hub_shop`, 산 뒤 `hub_shop_bought`: 팔림과 파편 부족 흐림), 일기(`hub_diary`), 다락(`hub_attic`), 탑 입구 E → 등반(`hub_run_start`), 귀환 → 탑 문 앞, 새 진열(`hub_return`).
  - 남은 것(다음 세션): 상인 자세 교체와 말풍선은 `merchant` 세션(`HubView.FacilityRenderer(HubStation.Merchant)`로 그림 교체, `UseFacility(Merchant)`가 상점을 엶). 사람이 직접 걸어 본 느낌, 시설 앞 발자국이나 먼지 같은 작은 연출 없음. 허브 효과음은 상점 열 때 `loot` 하나뿐.
- 허브 본체 완료 (2026-11-24 05:37)

### 15차-diary-story (아버지의 일기) 완료
> 이 세션은 이야기 해금만 맡습니다. 허브 본체는 15차 허브 세션, 세트 해금은 `outgame-unlock`.
- **결정:**
  - 일기 조각 = 층당 1개, 5개. `profile.best`(처음 돌파한 최고 층) 이하의 조각이 풀립니다. 조각 문구는 데이터 `GameDatabase.diary`(`Runtime/Data/DiaryData.cs`의 `DiaryConfig`, 조각마다 제목, 본문 여러 줄, 흡수 줄, 거절 줄).
  - 흡수 기록 = `Profile.floorChoices`(층 번호 - 1 자리, 0 아직, 1 흡수, 2 거절). 같은 층을 다시 깨면 **가장 최근 선택**으로 덮습니다(일기가 지금의 재봉사를 비추는 느낌). 잔향(이미 셋 다 흡수)도 흡수로 칩니다. 아직 고르지 않은 층은 마지막 줄 대신 "번져서 읽을 수 없다" 줄(데이터).
  - 읽음 = `Profile.diaryRead`(읽은 층 번호 목록). 풀렸는데 안 읽은 조각에 반짝 표시. 알림 = `Profile.diaryNoticed`(알린 조각 수). 등반이 끝나 `Run == null`이 되면(결과 화면, 허브) 새 조각이 있으면 한 번 알립니다.
  - 공유 파일 충돌을 줄이려고 게시판은 **따로 만든 UIDocument `Game/Diary`**(`Assets/UI/Diary.uxml/uss`, 쪽지 템플릿 `DiaryNote.uxml`)와 컴포넌트 `DiaryBoard`가 그립니다. `GameUI`는 `partial`로 바꾸고 `GameUI.Diary.cs`에 `ShowDiary()`/`HideDiary()`만 둡니다(열린 동안 `Opened == "diary"`라 Enter 시작과 E가 막힘). 판정 로직은 `Sim/Diary.cs`(Unity 객체 무관, 테스트 대상).
  - 허브가 아직이면 로비에 임시 "아버지의 일기" 버튼(Diary 문서 안, 로비가 보일 때만)으로 엽니다. 허브의 게시판 E가 `ShowDiary()`를 부르면 임시 버튼은 데이터 `diary.showLobbyButton`으로 끕니다.
- **진행:**
  - [x] 데이터/Profile/Diary 로직: `Data/DiaryData.cs`(DiaryEntry, DiaryConfig), `GameDatabase.diary`, `Profile.floorChoices/diaryRead/diaryNoticed`, `Sim/Diary.cs`, `Combat.ChooseDesire`에서 `Diary.RecordChoice` + 저장.
  - [x] 게시판 UI 코드: `UI/DiaryBoard.cs`, `UI/GameUI.Diary.cs`(GameUI는 outgame-unlock이 이미 partial로 바꿈), `Assets/UI/Diary.uxml/uss`, `DiaryNote.uxml`, 찢긴 쪽지 `Assets/UI/Paper/note_torn.png`(`tools/bake_diary_paper.py`). 알림도 DiaryBoard가 `Run == null`일 때 띄움(GameController 안 건드림).
  - [x] 알림 (DiaryBoard.Update)
  - [x] MCP: `Game/Diary` 오브젝트(UIDocument Diary.uxml, PanelSettings 공용, sortingOrder 5, DiaryBoard 참조), `GameUI.diary` 연결, `GameDatabase.diary` 임시 문구 5조각 입력, 씬 저장.
  - [x] 테스트: `Tests/DiaryTests.cs` 3개 통과(조각 5개와 두 결말과 가운뎃점 없음, 1층 돌파 → 해금과 알림 한 번과 읽음, 흡수/거절 → 마지막 줄과 최근 선택으로 덮기와 저장 후 불러오기). 전체 EditMode 34개 중 실패 5개는 다른 세션 것(element-combat 데이터 미입력 3개, HubTests 2개)이라 손대지 않음.
  - [x] 플레이 캡처 `Temp/Shots/`: `diary_notice.png`(로비 알림과 임시 버튼), `diary_board2.png`(게시판: 읽은 조각, 새 조각 반짝임과 "새 조각" 띠, 찢긴 빈 쪽지), `diary_read_refused.png`(거절 줄 청록 잉크), `diary_read_absorbed.png`(흡수 줄 포도주 잉크), `diary_notice_summary-2.png`(결과 화면 알림). 번진 줄(미선택)은 코드로 확인. 테스트와 캡처 뒤 실제 프로필을 백업본으로 되돌림.
- **데이터 위치:** `Game/GameDatabase` 인스펙터 `diary`(조각 5개 제목, 본문 4줄, 흡수 줄, 거절 줄, 기울기, 게시판 문구, 알림 4.5초, 잉크 색). 마지막 줄은 주인공이 여백에 적은 글("(내 글씨) ...")이라 아버지 글과 잉크 색이 다릅니다.
- **남은 것:**
  - 허브 게시판 시설의 E에서 `ui.ShowDiary()` 호출(허브 세션). 붙으면 `diary.showLobbyButton = false`로 임시 버튼을 끕니다. 허브 걷기 입력은 `ui.Opened != ""`일 때 막아야 합니다.
  - 문구는 임시입니다(기획서 톤으로 쓴 초안). 사람이 다듬을 것.
  - 쪽지 열 때 종이 바스락 효과음은 없고 기존 `loot`/`upgrade` 소리를 씁니다.

## 16차 — 메인/부가 세트, 층 속성 상성, 속성 몬스터, 상인 대사 (2026-11-24 자동 작업) 완료

> 사용자 지시: **질문 없이 임의로 판단해 인게임에 구현**합니다. 분할 세션으로 나눠 진행하고, 세션마다 이 절 아래에 자기 절(`### 16차-<세션 이름>`)을 붙여 진행을 적습니다. 커밋하지 않습니다. 끝나면 제목 줄 끝에 "완료"를 붙입니다.

### 결정 (조율 세션이 정함)
- **메인 세트 = 판에 놓인 조각 수가 가장 많은 켜진 세트**입니다(동률이면 단계, 그다음 연결 조각 수, 그다음 세트 순서). 켜진 세트가 없으면 무속성입니다. 13차의 "단계 높은 쪽이 주 속성, 나머지 50%"를 대신합니다.
- **메인 속성:** 바늘 공격, 당기기, 결박 폭발의 피해와 이펙트가 모두 메인 속성입니다. 메인 세트가 있으면 그 타격의 피해 전체가 메인 속성 피해로 계산됩니다.
- **부가 세트 = 스태틱식 주기 발동:** 메인이 아닌 켜진 세트는 각자 충전 게이지를 가집니다. 바늘 명중과 당기기 타격마다 충전되고, 다 차면 다음 타격에 그 속성이 터집니다(피해 + 부가 효과 + 전용 이펙트). 상시 50% 보조는 없앱니다. 충전 수, 발동 피해는 데이터(`gear`/`SetDef`)입니다.
- **층 속성:** 층마다 8속성 중 하나를 무작위로 고릅니다(한 등반 안에서 겹치지 않게). 그 층의 몬스터는 모두 그 속성입니다. 라운드 컨셉은 그대로 두되, 색조는 층 속성이 우선합니다.
- **상성 고리:** 화염 → 얼음 → 바람 → 번개 → 독 → 피 → 빛 → 어둠 → 화염 (화살표 앞이 뒤에 강함). 화염이 얼음을 녹이고, 얼음이 바람을 얼리고, 바람이 번개 구름을 흩고, 번개가 독을 태우고, 독이 피를 썩히고, 피가 빛을 더럽히고, 빛이 어둠을 몰아내고, 어둠이 불을 삼킵니다.
  - 공격 속성이 대상 속성에 강하면 ×1.5(약점), 대상 속성이 공격 속성에 강하면 ×0.5(저항), 나머지와 무속성은 ×1입니다. 값은 데이터입니다.
  - 상성은 **무기 속성 → 몬스터** 방향에만 씁니다. 플레이어가 받는 피해에는 상성이 없습니다.
  - 피해 숫자 옆에 "약점"/"저항" 작은 글자를 띄웁니다.
- **층 암시:** 라운드가 시작할 때마다 화면 가운데에 층 속성 문구를 띄웁니다(예: "화염의 기운이 가득한 것 같다..."). 문구는 데이터입니다.
- **층 맵:** 바닥, 등불, 안개 색조가 층 속성을 따릅니다. 속성 장식은 정적 소품(Codex)과 움직이는 장식(Blender: 불꽃 일렁임, 눈발, 전기 불꽃, 빛 알갱이, 어둠 연기, 핏방울 웅덩이, 바람 먼지, 독 거품)입니다.
- **속성 몬스터:** 스토커, 차저, 슈터, 보스 4종 × 8속성의 파츠 그림(Codex)으로 리그 그림만 바꿔 끼웁니다(뼈대와 동작은 그대로).
- **보스 속성 공격:** 일반 몬스터는 공격 이펙트를 바꾸지 않습니다. 보스만 공격에 속성 이펙트(Blender)와 속성 효과가 붙습니다. 화염 = 화상 도트, 얼음 = 둔화, 번개 = 감전 연쇄 피해, 빛 = 눈부심(잠깐 시야 밝게 번짐), 어둠 = 시야 좁힘, 피 = 준 피해만큼 보스 회복, 바람 = 밀쳐냄, 독 = 중독 도트.
- **상인:** Codex로 다시 뽑은 통그림 자세(기본, 인사, 말하기, 구매 기쁨, 돈 부족 거절, 작별)를 바꿔 끼우고, 코드 트윈(통통 튐, 눌림, 기울기)으로 움직입니다. 리그는 만들지 않습니다. 말 걸기, 구매, 돈 부족, 다시 굴리기, 떠나기마다 말풍선 대사가 뜹니다(대사는 데이터, 상황마다 여러 줄 중 무작위).

### 세션 나누기
| 세션 | 맡는 일 | 시작 |
|---|---|---|
| 조율(이 세션) | 그림 생성(Codex CLI, 한도가 차면 ChatGPT 웹): 상인 자세, 속성 몬스터 파츠, 속성 정적 장식. 세션 시작과 확인 | 바로 |
| `element-combat` | 메인/부가 세트, 층 속성, 상성, 층 암시, 층 맵 색조, 보스 속성 공격, 그림·이펙트 연결 (Unity) | 바로 |
| `element-vfx` | Blender 이펙트: 보스 속성 공격 8종, 부가 발동 8종, 움직이는 맵 장식 8종, 결박 폭발 4종 (파일만) | 바로 |
| `merchant` | 상인 자세 연결, 말풍선 대사, 상호작용 동작 (Unity). **허브 본체 세션(`15차: 재봉실 허브 본체`)이 이어서 맡음** (05:40) | 허브 본체 완료 뒤 |

- **Unity 잠금:** 에디터는 하나라서 플레이 모드, 테스트 실행, 씬 저장 전에 `Temp/claude_unity.lock`을 확인합니다. 없거나 20분 넘게 갱신되지 않았으면 자기 세션 이름과 시각을 써서 잡고, 끝나면 지웁니다. 다른 세션이 잡고 있으면 코드 작업을 하며 기다립니다.
- **MCP 끊김:** Unity가 백그라운드에서 멈칫하면 MCP가 끊깁니다(instances 0, Editor.log에 "WebSocket is not initialised"). 00:47에 한 번 있었고, Unity 창을 앞으로 가져오니 20초 안에 다시 붙었습니다. PowerShell에서 user32 `ShowWindow(h,9)` + `SetForegroundWindow(h)`를 `(Get-Process Unity).MainWindowHandle`에 호출하면 됩니다.
- **같은 파일 편집:** `GameDatabase.cs`, `Combat.cs`, `GameUI.cs`는 15차와 겹칠 수 있습니다. 고치기 직전에 다시 읽고, 작은 단위로 고칩니다. 다른 세션이 만든 컴파일 오류는 고치지 말고 잠시 기다립니다.
- **그림 경로:** 속성 몬스터 `art_src/raw/enemy_<종류>_parts_<속성>.png` → `Assets/sprites/enemy_<종류>_<파츠>_<속성>.png`. 정적 장식 `art_src/raw/decor_<속성>.png` → `Assets/sprites/decor_<속성>_<이름>.png`. 상인 `art_src/raw/merchant_poses.png` → `Assets/sprites/merchant_<자세>.png`. 진행 상황은 아래 "16차-그림"에 적습니다.

### 16차-element-combat (게임플레이) 완료
- **설계(이 세션이 정함):**
  - 데이터는 새 파일 `Runtime/Data/ElementData.cs`의 `ElementConfig`(GameDatabase `elements` 한 필드)에 모읍니다. 상성 고리, 층 암시 문구, 층 색조, 속성 장식, 몬스터 파츠 교체, 부가 발동 이펙트, 보스 속성 공격 값이 속성마다 `ElementDef` 한 줄입니다. 충전 수와 발동 값은 결정대로 `SetDef`(`procCharge`, `procDamage`, `procDamagePerTier`, `procEffectScale`)에 둡니다.
  - 메인 세트 정렬: 판에 놓인 조각 수 → 단계 → 연결 조각 수 → 세트 순서(`gear.sets` 순). `GearStats.sets[0]`이 메인입니다.
  - 메인 속성: 당기기 한 타의 피해 = 실 피해 + 메인 세트 `damagePerTier × 단계`를 **한 숫자**로 합쳐 메인 속성 글꼴로 띄웁니다(치명타는 합친 피해 전체에 걸리고 치명타 글꼴). 메인 세트의 부가 효과(화상, 둔화 등)는 예전처럼 당기기마다 100%로 겁니다.
  - 부가 세트: 바늘이 적에 꽂힐 때 +1, 당기기에 맞은 적 하나마다 +1 충전합니다. `procCharge`만큼 차면 "준비" 상태가 되고, **다음 타격**의 적에게 터진 뒤 0으로 돌아갑니다. 발동 = 피해(`procDamage + procDamagePerTier × 단계`, 상성 적용) + 그 속성 효과 한 번(`effectPerTier × 단계 × procEffectScale`) + 발동 이펙트(`ElementDef.procFx`, 없으면 `ElementTone.hit`을 크게).
  - 상성은 무기 → 몬스터에만 씁니다. 실 피해, 부가 발동, 번개 튐, 화상과 중독 도트 모두 그 피해의 속성으로 배율을 겁니다. 무속성은 ×1입니다.
  - 층 속성: `BeginFloor`에서 `elements.defs` 중 이번 등반에 안 나온 것을 뽑습니다(8층을 넘으면 다시 처음부터). 그 층 몬스터 전원 `Enemy.element`.
  - 층 암시, 부가 충전 게이지는 `Main.uxml`과 겹치지 않게 따로 만든 UIDocument `ElementHud`(`Assets/UI/ElementHud.uxml/uss`, 게이지 템플릿 `ProcGauge.uxml`)가 그립니다.
  - 보스 속성 공격은 보스의 돌진 접촉과 보스 탄이 실제로 피해를 줬을 때만 겁니다(회피 중이면 없음). 화상, 중독, 감전 피해는 피격 무적과 경직 없이 체력만 깎습니다. 상태는 라운드가 바뀌면 풀립니다.
- **코드:** `Sim/CombatElements.cs`(partial `Combat`: 메인/부가, 발동, 상성, 층 속성, 보스 속성 공격, 주인공 상태), `View/ArenaRenderer.Elements.cs`(partial: 상성 글자, 발동, 보스 피격, 주인공 상태와 눈부심/어둠, 속성 장식, 보스 탄, 결박 폭발 시트), `UI/ElementHud.cs`, `Editor/ElementArtSetup.cs`. 기존 파일은 고리만 걸었습니다(`Combat.Pull/Attach/DamageEnemy/EnemyShots`, `BackgroundView` 색조, `EnemyView.Skin`, `GameUI` 세트 패널 문구와 순서, `DevTools` 층 속성 버튼, `Main.uxml` 개발자 줄 하나).
- **그림 연결 메뉴 `TOD/Setup/16. Element Art`** (그림이 들어올 때마다 MCP `execute_menu_item`으로 다시 실행):
  - `Assets/Art/VFX/16/*.png`를 정사각 칸으로 자르고 피벗을 `art_src/vfx/16/sheets.json`에서 읽습니다. `proc_<속성>` → `procFx`, `boss_<속성>_slam/bullet/pool` → `boss.fx/bullet/pool`, `decor_<속성>_loop` → `animated`, `bind_<속성>_burst` → `bindBurst`. `_glow` 짝은 가산 빛으로 같은 클립에 들어갑니다.
  - `Assets/sprites/enemy_<종류>_<파츠>_<속성>.png`는 원래 파츠와 같은 가져오기 설정(피벗, PPU는 그림 높이 비율로)으로 맞추고 `skins`에 넣습니다. **속성 파츠는 원래 파츠와 같은 구도(같은 캔버스 비율, 같은 피벗 자리)여야 리그에 맞습니다.**
  - `Assets/sprites/decor_<속성>_<이름>.png` → `props`. 크기와 세기 같은 수치는 건드리지 않고(fps만 시트 메타), 씬도 저장하지 않습니다.
- 어둠 시야 마스크: `python tools/make_vision_mask.py` → `Assets/sprites/fx_vision_mask.png`.
- **씬 (MCP):** `Game/ElementHud`(UIDocument `ElementHud.uxml`, 정렬 -1 = Main 아래, `ElementHud` 컴포넌트)를 만들었습니다. 게이지는 상태 패널 바로 아래(`top: 452px`)입니다(아래쪽은 조작 안내와 경험치 줄이 덮음).
- **데이터 (MCP `execute_code`, 모두 임시값):**
  - 상성: 고리는 결정 그대로, 약점 ×1.5 / 저항 ×.5, 글자 "약점"(노랑) "저항"(회청), 15px.
  - 부가 발동: 번개와 바람 5타, 나머지 6타. 피해 8 + 단계당 4, 효과 세기 ×2. 발동 이펙트 크기 190, 길이 .55초. 게이지 문구 "{0}/{1}", 다 차면 "준비!".
  - 세트 패널: `ui.setMain` "메인 속성 (모든 공격)", `ui.setSub` "부가 속성 ({0}타마다 발동)". 세트 목록은 메인, 부가, 꺼진 세트 순서입니다.
  - 층 암시(라운드 시작 .5초 뒤, .5초 나타나 2.2초 유지, .9초 사라짐): 화염 "화염의 기운이 가득한 것 같다...", 얼음 "살을 에는 냉기가 감도는 것 같다...", 번개 "공기가 찌릿찌릿 떨리는 것 같다...", 빛 "눈부신 빛이 스며든 것 같다...", 어둠 "짙은 어둠이 깔린 것 같다...", 피 "비릿한 피 냄새가 나는 것 같다...", 바람 "거센 바람이 휘도는 것 같다...", 독 "매캐한 독기가 서린 것 같다...".
  - 층 색조: 속성마다 바닥 덮개(.24~.40), 등불 색, 안개(.55), 그림 없는 파츠에 곱하는 색(.45). 속성 소품 밀도 2.2, 움직이는 장식 3(100만 px²당), 크기 130.
  - 보스 공격: 화염 화상 초당 6, 3초 / 얼음 둔화 35%, 2.5초 / 번개 감전 5 × 3번, .35초 간격 / 빛 눈부심 1.6초, 세기 .5 / 어둠 시야 반지름 170, 3.5초 / 피 준 피해 ×1 회복 / 바람 160 밀침, .35초 / 독 겹당 초당 3, 5초, 최대 4겹. 맞은 이펙트 200, .67초, 보스 탄 48.
- **그림 연결 현황 (05:30):** Blender 시트 76장 모두 연결(발동 8, 보스 슬램 8, 보스 탄 8, 화염과 독 웅덩이, 움직이는 장식 8, 결박 폭발 4). 몬스터 파츠는 화염, 얼음, 번개, 독(각 23장)만 들어와 연결했고 빛, 어둠, 피, 바람은 색만 입힙니다. 속성 정적 소품(`decor_<속성>_*.png`)은 아직 0장입니다.
- **주의:** `TOD/Setup/1. Texture Imports`가 `Assets/Art/VFX/16`을 Single로 되돌리지 않게 건너뛰도록 고쳤습니다. 플레이 중에 에셋을 새로 고치면 Main.uxml이 다시 불려 `GameUI` 참조가 끊깁니다(화면이 로비로 보임). 캡처 전에는 플레이를 다시 시작하세요.
- **확인:** EditMode 34개 통과(새 테스트 `MainSetHasMostPieces`, `SubSetProcFiresAfterCharge`, `AffinityRingMultipliers`, `FloorElementsDoNotRepeat`, `BossAttacksCarryElement`, 옛 `SecondSetIsHalf`는 지움). 플레이 캡처 `Temp/Shots/`: `e16_hint_poison`(독 층 문구, 색조, 독 장식), `e16_ice_floor`(얼음 층, 얼음 스토커 파츠, 번개 게이지), `e16_proc_weak2`(화염 메인 115 + "약점", 번개 발동 12, 게이지 1/5), `e16_boss_dark3`(어둠 시야 좁힘), `e16_boss_fire`(화염 층 파츠, 화상 웅덩이), `e16_boss_light_blind`(눈부심 최고점). 개발자 모드(F1) "층 속성" 줄로 층 속성을 바로 바꿀 수 있습니다.
- **남은 것:** 수치 균형(모두 임시값), 실제 속도로 보는 발동과 보스 공격 손맛, 빛, 어둠, 피, 바람 몬스터 파츠와 속성 정적 소품이 들어오면 `TOD/Setup/16. Element Art` 다시 실행(크기가 0인 새 클립은 위 크기로 맞춰 줄 것), 발동과 보스 속성 공격 효과음(`proc`, `bosselement` 이벤트에 소리 없음), 결박 폭발 시트를 쓰는 4속성은 기존 속도선을 그리지 않습니다.
- **그림 전부 연결 확인 (06:20):**
  - 조율 세션이 8속성 몬스터 파츠(속성마다 23장)와 정적 소품(속성마다 4개)을 연결해 둔 상태를 확인했습니다. 모든 속성이 "파츠 23, 소품 4"입니다. 움직이는 장식 클립 중 크기 0인 것은 없습니다.
  - 개발자 모드 층 속성 줄과 같은 방법(`DevSetFloorElement` + `arena.BeginRound`)으로 8속성을 하나씩 바꾸고, 스토커, 차저, 슈터, 보스를 같은 자리에 세워 찍었습니다(`Temp/Shots/e16_all_<속성>.png`). 8속성 모두 파츠가 리그에 맞게 붙었고 관절 어긋남이나 크기 차이는 없습니다. 원본 해상도로 확대한 `e16_joints_poison.png`, `e16_joints_wind.png`과 걷는 자세 `e16_all_poison_move.png`로도 확인했습니다.
  - 데이터 수정 2가지: 속성 소품 기본 높이 `elements.propHeight` 110 → 64(기존 장식 높이 40~72에 맞춤), 어둠 바닥 덮개 `floorFill.a` .40 → .30(어두운 파츠가 바닥에 묻혀 보였음). 씬을 저장했습니다.
  - EditMode 38개 통과(다른 세션 테스트 포함).

### 16차-그림 (조율 세션) 완료
- 흐름: `python tools/make_element_enemy_prompts.py`, `python tools/make_element_decor_prompts.py`(프롬프트 생성) → `sh tools/codex_prompts/run_16.sh`(상인 + 몬스터 32장), `sh tools/codex_prompts/run_16b.sh`(장식 8장). 둘 다 이미 있는 그림은 건너뛰고, Codex 한도에 걸리면 `QUOTA`를 찍고 멈춥니다. 진행 기록은 `tools/codex_prompts/run_16.out`. 한도가 차면 브라우저 패널의 ChatGPT 웹으로 같은 프롬프트를 넣어 뽑고 같은 경로에 저장합니다.
- 굽기: `python tools/bake_16_art.py`. 속성 몬스터 파츠는 **기존 파츠와 같은 픽셀 크기**로 맞춰 저장하므로 리그 관절과 피벗은 그대로 두고 스프라이트만 바꿔 끼우면 됩니다. 상인 자세 6장은 같은 캔버스 크기, 발이 캔버스 아래(아래 가운데 피벗). 장식은 긴 변 320px.
- [x] 상인 자세 6장 (`Assets/sprites/merchant_idle/greet/talk/happy/refuse/farewell.png`, 487×475, 발이 아래 끝에서 약 8px 위). 단추는 두더지 수인 떠돌이 상인: 찢긴 보라 외투, 분홍 리본 모자, 단추 안대, 실패와 가위가 든 큰 배낭. 미리보기 `Temp/Shots/merchant_poses_baked.png`. 기존 임시 `Assets/Art/Generated/merchant.png`를 대신합니다.
- [x] 속성 몬스터 파츠 32장 — **8속성 모두 구움, `TOD/Setup/16. Element Art` 실행해 속성마다 파츠 23장 연결, Main 씬 저장(06:2x)**. 확인 그림 `Temp/Shots/enemy_<속성>_check.png`. 이전 기록: 화염, 얼음, 번개, 독 16장 먼저 구움(`Assets/sprites/enemy_<종류>_<파츠>_<속성>.png`, 확인 `Temp/Shots/enemy_fire_check*.png`, `enemy_fire_boss.png`, `enemy_elements_heads.png`). 05:13쯤 Codex 한도에 걸려 바람부터 멈췄고, 05:46에 `run_16.sh` → `run_16b.sh`가 다시 돌도록 걸어 둠(기록 `tools/codex_prompts/run_16c.out`).
- [x] 속성 정적 장식 8장 → 32개 (`Assets/sprites/decor_<속성>_<이름>.png`, 미리보기 `Temp/Shots/decor_elements.png`). `TOD/Setup/1. Texture Imports`(스프라이트로 임포트) → `TOD/Setup/16. Element Art` 순서로 돌려 속성마다 소품 4개 연결, Main 씬 저장. **주의:** Codex가 칸 순서를 프롬프트와 다르게 놓은 시트가 있어 파일 이름과 그림이 어긋난 것이 있습니다(예: 얼음 `snow_yarn`이 실제로는 촛대). 소품은 속성별로 무작위 배치라 게임에는 영향 없고, 이름이 필요하면 미리보기로 확인하세요.
- **16차-그림 완료** (06:5x). Codex 한도는 05:13에 한 번 걸렸고 05:45 초기화 뒤 이어서 끝냄(ChatGPT 웹은 쓰지 않음).

### 15차 세션 나누기 (사용자 요청, 2026-11-24)
- 허브 본체(방, 걷기, 시설, 상인 단추 상점, 시설 그림) = 기존 15차 세션. 세트 해금과 다락 '시작 조각 선택' = `outgame-unlock`. 아버지의 일기 = `diary-story`. 각자 15차 절 아래 `### 15차-<이름>` 절에 진행을 적습니다.
- 토큰이 다 떨어져 멈추면 05:30 예약 작업(`tod-resume-0530`)이 멈춘 세션들을 다시 깨웁니다.

### 16차-element-vfx (Blender 이펙트 시트) 완료
> 스크립트 `tools/render_element_vfx.py`(Blender 5.3), 미리보기 `tools/preview_element_vfx.py`. 출력 `Assets/Art/VFX/16/` 38시트 × 2장 = PNG 76장. Unity는 건드리지 않았습니다(임포트, 자르기, 연결은 element-combat).
> 다시 굽기: `"C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b -P tools/render_element_vfx.py -- C:/Project/TOD [boss|proc|decor|bind|시트 이름...]` (전부 약 5분, 그룹별로 4개 동시에 돌리면 빠름) → `python tools/preview_element_vfx.py` → `Temp/Shots/vfx16_boss/proc/decor/bind.png`.
- **진행:** [x] 공용 뼈대 [x] 보스 내려찍기 8 + 탄 8 + 장판 2 [x] 부가 발동 8 [x] 맵 장식 8 [x] 결박 폭발 4 [x] 미리보기로 확인하고 다듬기(바닥 웅덩이를 둥글게, 빛 장판의 탁한 바닥 판 제거, 김과 독기와 먼지는 외곽 없는 번짐, 피 발동 할퀴기를 크게, 어둠 결박이 끝에 사라지게, 번개 발동 굵게).
- **결정 (형식):**
  - 시트마다 두 장: `<이름>.png` 본체(속성 색을 칠한 종이 컷아웃 + 거친 검은 잉크 외곽 + 옅은 종이 얼룩, 칸마다 외곽과 모양이 들썩임) = **보통 알파 합성**(SrcAlpha, OneMinusSrcAlpha), 틴트 흰색. `<이름>_glow.png` 빛(속성 색 번짐만) = **가산 합성**(SrcAlpha, One), 본체보다 **먼저(아래에)** 같은 칸을 그립니다.
  - 기존 플립북의 core/rim 회색 틴트 방식이 아니라 **색이 구워져 있습니다**(어둠과 피는 가산으로는 표현이 안 돼서). 속성 색은 `bind.looks` 색조를 채도만 낮춘 것입니다.
  - 빛 세기: 빛 알파 상한 .6, 실제 가산 최댓값 .09(바람 장식)~.54(화염 내려찍기). 한 장으로는 하얗게 날아가지 않습니다. 같은 자리에 여러 개 겹치면 빛 시트 색에 .6~.8을 곱해 쓰세요.
  - **시트 형식:** 가로 1줄 스트립, 정사각 칸 N장, 왼쪽이 0번. 칸 수 = 폭 / 높이 → 기존 `ProjectSetup.SliceFlipbook`이 그대로 맞습니다(지금은 `Assets/vfx/flip`만 자르니 `Assets/Art/VFX/16`을 더하면 `<이름>_0..N-1`). 밉맵 끔, Clamp, 필터 Bilinear 권장(플립북과 같음).
  - **좌표:** 한 칸 = 월드 4×4(x, y ∈ [-2, 2]). **바닥 피벗 (0.5, 0.2)** = 바닥 접점(그림자 자리), 바닥 원과 고리는 PROJECTION_Y .67로 납작하게 구워 둠(회전 금지). **가운데 피벗 (0.5, 0.5)** = 적 몸 가운데. 장판은 (0.5, 0.35)가 웅덩이 가운데.
  - **재생:** 터지는 시트는 0번 → 마지막 칸을 한 번 재생하고 끝(마지막 칸은 거의 비어 있음, 페이드 없이 칸 단위, `DrawFlip`과 같음). 반복 시트는 마지막 칸 → 0번이 이어지므로 `(시간 × fps) mod N`. 장식 여러 개를 놓을 때는 시작 칸을 무작위로 어긋나게.
  - 메타는 `art_src/vfx/16/sheets.json`(그룹, 속성, 칸 수, 칸 px, fps, 길이, 반복, 피벗)에도 있습니다.

  | 시트 (`Assets/Art/VFX/16/`) | 칸 | 칸 px | fps (길이) | 반복 | 피벗 | 내용 / 연결 메모 |
  |---|---|---|---|---|---|---|
  | `boss_<속성>_slam` ×8 | 16 | 256 | 24 (0.67초) | 아니오 | (.5, .2) | 보스 근접 내려찍기 자리에 덮음. 바닥 고리 반지름이 칸 폭의 약 45% → **그리기 폭 ≈ 내려찍기 반경 × 2.2**. 화염 불기둥과 불씨, 얼음 가시와 서리 파편, 번개 낙뢰와 바닥 전기, 빛 십자 섬광과 빛 기둥, 어둠 그림자 웅덩이와 연기 촉수, 피 핏물 파도 둘과 핏방울, 바람 회오리와 잎, 독 가시 튐과 독방울과 거품. 번개는 위로 칸 끝까지 줄기가 뻗음. |
  | `boss_<속성>_bullet` ×8 | 8 | 128 | 16 (0.5초) | 예 | (.5, .5) | 보스 탄에 덮음. **+x = 진행 방향**(탄 각도만큼 회전). 몸통 반지름 ≈ 칸 폭의 14% → **그리기 폭 ≈ 탄 반지름 × 7**. 꼬리가 −x로 칸 끝까지. 탄막이 많으면 빛 시트는 생략해도 됨. |
  | `boss_fire_pool`, `boss_poison_pool` | 12 | 256 | 12 (1.0초) | 예 | (.5, .35) | 바닥 장판 지속 효과(화상, 중독 장판). 웅덩이 반지름 ≈ 칸 폭의 44% → **그리기 폭 ≈ 장판 반경 × 2.3**. 불꽃과 독기는 웅덩이 위로 솟음. 사라질 때는 알파를 줄여 페이드. |
  | `proc_<속성>` ×8 | 12 | 256 | 24 (0.5초) | 아니오 | (.5, .5) | 부가 세트 발동(스태틱식). 적 몸 가운데. 주 무늬 반지름 ≈ 칸 폭의 40% → **그리기 폭 ≈ 140~200 캔버스 px**(적 크기 × 2.5 정도). 메인 타격(Codex `fx_*` 그림)과 구분되게 날 선 별 모양과 강한 빛: 화염 불꽃 가시 별, 얼음 눈꽃 결정이 펼쳐졌다 산산조각, 번개 지그재그 가지와 전기 고리, 빛 여덟 갈래 성광과 무늬 고리, 어둠 가시가 빨려 들었다 검은 구멍과 가시 고리로 터짐, 피 할퀴기 세 줄과 핏방울, 바람 초승달 칼날 셋 회전, 독 가시 독 덩이 폭발과 꼬리 독방울. |
  | `decor_<속성>_loop` ×8 | 16 | 256 | 10 (1.6초) | 예 | (.5, .2) | 움직이는 맵 장식(바닥 정렬, Y정렬 대상). 내용 폭 ≈ 칸 폭의 60~80% → **그리기 폭 ≈ 180~240 캔버스 px**. 화염 촛불 다섯 자루(촛농 웅덩이 포함), 얼음 언 웅덩이와 서리 결정과 눈발과 김, 번개 철판과 구리 막대 잔해에서 전기 불꽃(4칸 중 1칸 꺼짐), 빛 옅은 빛 웅덩이(빛 시트만)와 떠오르는 빛 알갱이, 어둠 바닥을 굴러가는 연기와 가는 연기, 피 웅덩이에 핏방울이 떨어져 튀고 물결, 바람 작은 회오리와 먼지와 잎, 독 끓는 거품 웅덩이와 독기. 눈발과 핏방울, 빛 알갱이는 칸 위쪽까지 씀. |
  | `bind_<dark/blood/wind/poison>_burst` ×4 | 14 | 256 | 30 (0.47초) | 아니오 | (.5, .2) | 결박 폭발(`DrawBind`)의 남은 4종. 적 발밑(ground)에 바닥 피벗, 적 몸 가운데는 피벗 위 칸 높이의 25%. 바닥 고리 반지름 ≈ 칸 폭의 45% → **그리기 폭 ≈ R × 2.2**(R 기본 90이면 약 200 px). 기존 화염/번개/얼음/빛처럼 `Fx.follow`를 따라가면 됨. 어둠 촉수 여섯이 나선으로 휘감아 조이고 보랏빛 가시, 피 가시 왕관과 핏줄기와 핏방울, 바람 회오리 고리가 감겨 조이고 엇갈린 칼바람과 잎, 독 독액 기둥이 솟아 꼭대기에서 왕관처럼 터짐. 코드의 바닥 고리와 속도선은 그대로 두고 그 위에 얹어도 어울림. |

- **남은 것:** 실제 게임 속도와 크기로 본 느낌(Unity 연결 뒤 element-combat가 확인). 탄 8칸은 128px이라 아주 크게 그리면 흐릴 수 있음(그때는 `size` 256으로 다시 굽기, 시트 표 한 줄). 맵 장식 화염의 초는 Codex 정적 장식과 겹치면 불꽃만 쓰는 판이 필요할 수 있음.

### 16차-merchant (상인 단추 자세와 말풍선) 완료
> 허브 세션(15차-outgame-hub)이 이어 맡음. 허브 상인과 쉬어가기 라운드 상인이 같은 구조를 씁니다.
- **결정:**
  - 데이터 = `GameDatabase.merchant`(`Runtime/Data/MerchantData.cs`의 `MerchantConfig`): 자세 6장(`MerchantMood` Idle, Greet, Talk, Happy, Refuse, Farewell → 스프라이트), 트윈 값(숨쉬기, 바꿀 때 튐과 눌림, 기울기, 깡충, 좌우 흔들기, 자세 유지 시간), 상황별 대사(`MerchantCue` Approach, Greet, Buy, NoMoney, SoldOut, Farewell → 3~5줄과 자세), 말풍선(글자 속도, 유지 시간, 높이), 효과음 id.
  - 상태와 트윈 계산 = `Sim/MerchantTalk.cs`(Unity 그리기 무관, 테스트 대상): 지금 자세, 자세 시작 시각, 지금 대사, 다가옴 혼잣말 쿨다운. `Pose(...)`가 스프라이트 대신 자세, 가로세로 배율, 기울기, 뜀 높이를 돌려줍니다. GameController가 하나를 갖고 허브와 쉬어가기 라운드가 같이 씁니다.
  - 그리기: 허브는 `HubView`가 상인 시설 렌더러에 자세 그림과 트윈을 입힘. 쉬어가기 라운드는 `ArenaRenderer.Merchant.cs`의 `DrawMerchant`(기존 `DrawProp(merchantSprite…)` 한 줄을 바꿈). 상인 그림은 피벗이 발밑이라 기울기와 눌림이 발을 축으로 일어납니다.
  - 말풍선: 상점 창이 닫혀 있으면 상인 머리 위 월드 말풍선(`merchant-world` 층), 상점 창(`dialog-shop`, `dialog-hubshop`)이 열려 있으면 창 오른쪽 위에 상인 얼굴(지금 자세 그림)과 말풍선(`merchant-peek`). 둘 다 템플릿 `Assets/UI/MerchantBubble.uxml`, 스타일 `Assets/UI/Merchant.uss`(양피지 `panel_light`). 글자는 한 자씩 찍히고 두 자마다 짧은 소리(`merchantBlip`), 새 대사마다 `merchantTalk`.
  - 계기: 다가옴(상호작용 거리 안에 처음 들어옴, 쿨다운), 말 걸기(E로 상점 열기), 구매, 돈 부족, 이미 팔림(산 카드를 누름), 작별(상점 창 닫기, `GameUI.DialogClosed` 이벤트). 돈 부족과 팔림을 누를 수 있게 상점 카드는 누를 수 있게 두고 흐리게만 합니다.
- **진행:**
  - [x] 코드: `Data/MerchantData.cs`, `Sim/MerchantTalk.cs`, `Game/GameController.Merchant.cs`, `UI/GameUI.Merchant.cs`, `View/ArenaRenderer.Merchant.cs`, HubView `AnimateMerchant/MerchantHead`, 테스트 `Tests/MerchantTests.cs`(4개). 공유 파일 연결 줄: GameController(Interact 상인 → 인사, Buy 의 팔림/부족/구매 대사, Update `TickMerchant`), GameUI(`CloseDialog`가 `DialogClosed` 이벤트, 쉬어가기 상점 카드는 누를 수 있게 두고 `ware-dim`으로 흐리게), ArenaRenderer(`DrawProp(merchantSprite…)` → `DrawMerchant`), GameDatabase(`merchant`).
  - [x] UI: 템플릿 `Assets/UI/MerchantBubble.uxml`(월드 말풍선, 상점 창 얼굴+말풍선), `Assets/UI/Merchant.uss`, Main.uxml에 `merchant-world` 층과 두 상점 창의 `merchant-peek-slot`.
  - [x] MCP 데이터(`GameDatabase.merchant`, 임시): 자세 6장, 자세 유지 1.6초, 숨쉬기 0.022 × 0.55회/초, 바꿀 때 0.32초 눌림 0.14 튐 9px, 인사/작별 기울기 9도 0.7초, 기쁨 깡충 16px 2번 0.75초, 거절 흔들기 8도 3번 0.6초, 말할 때 까딱 1.6도. 대사 6상황 × 4~5줄(능청스럽고 수상한 두더지 말투). 글자 24자/초, 다 찍힌 뒤 2.2초, 머리 위 58px, 혼잣말 쿨다운 12초. 효과음 `merchantTalk`(삼각파 330→470Hz 0.09초), `merchantBlip`(사각파 560→500Hz 0.035초, 두 자마다). 쉬어가기 상인 그림 `ArenaRenderer.merchantSprite` = `merchant_idle`, `palette.merchantHeight` = 105.
  - [x] EditMode 38개 통과(상인 4개 포함). 프로필은 테스트 전 값으로 되돌림.
  - [x] 플레이 캡처 `Temp/Shots/merchant_*.png`: 다가옴 혼잣말(`merchant_approach`, 말하기 자세), 상점 열기 인사(`merchant_greet_shop`), 파편 부족 거절(`merchant_refuse`), 구매 기쁨(`merchant_happy`, 팔림 흐림), 작별(`merchant_farewell`, 손 흔들기), 글자 찍히는 중(`merchant_typing-1`), 쉬어가기 라운드 상인 인사(`merchant_rest_round`), 쉬어가기 상점과 구매(`merchant_rest_shop-2`, `merchant_rest_buy`), 넓힌 상점 말풍선(`merchant_shop_peek-1`).
  - 남은 것: 트윈 움직임(튐, 흔들기)은 정지 화면으로만 봤습니다. 효과음 청취는 못 함. MCP 캡처가 가끔 UI 없이 찍혀 같은 장면을 두 번 찍은 것이 있음(게임 문제 아님).

## 14차 요청 (2026-11-23) — 게임 글꼴 주아, 속성별·치명타 피해 숫자, 치명타

- [x] **게임 글꼴 전체 주아:**
  - 사용자가 검토한 뒤 "폰트가 별로"라며 바꾸기로 했습니다.
  - 글꼴은 Google Fonts에서 받은 `Assets/Fonts/Jua/Jua-Regular.ttf`(우아한형제들 BM JUA)이고, 라이선스는 OFL(`OFL.txt`)이라 게임에 넣어도 됩니다.
  - UI 글꼴 에셋: `Assets/Fonts/Jua/Jua_SDF.asset`(TextCore SDF, 동적, 2048 아틀라스 여러 장). `Temp/galmuri_charset.txt`의 게임 문구와 KS X 1001 한글 2350자를 미리 구워 넣었습니다(2450자).
  - 주아에 없는 기호(×, ★, ✧, ◇, ↑, 「」 등 45개)는 대체 글꼴 `Galmuri14-15px`에서 가져옵니다.
  - `Main.uss`와 `DevTools.uss`의 `-unity-font-definition`을 모두 `Jua_SDF.asset`으로 바꿨습니다. SDF라서 **글자 크기 배수 규칙(4차의 10/12/15 배수)은 이제 필요 없습니다.**
- [x] **피해 숫자 그림 글꼴 (주아):**
  - `python tools/bake_damage_font_jua.py`가 기본 아틀라스(흰 글자 + 검은 외곽선 9px, 크기 116)와 JSON을 주아로 다시 굽습니다. 옛 Codex 손글씨는 `bake_damage_font.py`에 남아 있습니다.
  - `python tools/bake_element_fonts.py`가 속성 8종과 치명타 아틀라스 `damage_font_<이름>.png`를 굽습니다. 위→아래 3색 그라디언트, 위쪽 광택 띠, 속성 색 잉크 외곽선, 바깥 빛으로 구성됩니다.
  - `TOD/Setup/1`은 `damage_font*.png`를 모두 같은 JSON으로 자릅니다.
  - 데이터: `visual.tones[].damageFont`는 속성마다, `visual.critFont`은 치명타입니다.
- [x] **속성 피해 숫자:** 세트 속성 피해는 속성마다 숫자를 따로 띄웁니다. 그 속성 글꼴을 흰색 그대로 쓰고, 실 피해 숫자 위로 `elementNumberLift`(34) 간격으로 쌓습니다. 화상·중독·번개 튐(`DamageEnemy(r, e, amount, Element)`)과 흡혈 회복 숫자도 속성 글꼴입니다.
- [x] **치명타 (새 기능):**
  - 당기기 실 피해마다 `hero.critChance`(.1) 확률로 × `critMultiplier`(1.5)가 됩니다. 속성 피해는 제외하고, 이벤트 `crit`이 납니다.
  - 숫자는 치명타 글꼴(흰빛 → 노랑, 붉은 외곽선)로 `critScale` 1.3배입니다.
  - 숫자 왼쪽 위에 세피리아식 뾰족 폭발 아이콘을 붙입니다(`Assets/sprites/crit_icon.png`, Codex `tools/codex_prompts/crit_icon.txt`). 아이콘은 톡 튀어나와 살짝 흔들리고, 크기는 숫자 높이 × `critIconSize` .8입니다.
- 확인: EditMode 20개 통과. 플레이 캡처(`Temp/Shots/jua_numbers_crop.png`)에서 치명타 274와 아이콘, 화염 12(붉은 글꼴), 번개 6(노란 글꼴)이 쌓인 모습, 주아 UI(`jua_numbers.png`)를 확인했습니다. 검토 이미지는 `art_src/fonts/jua_review.png`입니다.
- 남은 것: 치명타 효과음(`crit` 이벤트에 소리 없음). 맞은 적과 이펙트 위에서 숫자가 여전히 겹쳐 보일 수 있습니다. 전체 화면을 주아로 사람 눈으로 훑어보는 확인(글자 넘침).

## 13차 요청 (2026-11-23) — 세트 이름 표시, 주/보조 속성, 계단 판정, 가운뎃점 제거, 맵 장식, 보스 뒤 탑과 등장 연출

- [x] **세트 이름 꼬리표:** 판 위 조각의 맨 윗줄 가운데에 세트 이름(예: 화염)을 세트 색 글씨로 띄웁니다. 템플릿은 `Slots.uxml board-set-tag`이고, 끌기·바느질 때 조각과 함께 움직입니다. 목록 줄에서는 퍼즐 모양 위에(`PieceButton.uxml piece-shape-col`), 정보 창에서는 이름 위(`tip-set-name`)에 둡니다. 탑 카드 띠는 "등급  세트"이며, 세트 이름이 세트 색 리치 텍스트로 나옵니다.
- [x] **주 속성 + 보조:** 단계가 가장 높은 세트(동률이면 연결 조각 수)가 주 속성입니다. 주 속성은 실 모습과 피해·효과를 100%로 내고, 나머지 세트는 `gear.subSetScale`(.5)만큼 냅니다(`Combat.SetScale`). 세트 패널 이름 아래 한 줄에 `ui.setMain / setSub`를 표시합니다.
- [x] **계단 판정:** 계단만 반경을 따로 둡니다(`rounds.stairsRadius` 150, 중심 `stairsHitOffset` (0, −60) = 발밑보다 위쪽 단). 상호작용은 "반경 대비 가장 가까운 것"으로 고릅니다(`Combat.Nearest`).
- [x] **가운뎃점 제거:** 게임에 나오는 글자의 `·`를 모두 뺐습니다. GameDatabase 문자열 28개는 목록이면 쉼표로, 머리글이면 띄어쓰기·괄호·쌍점으로 바꿨습니다. 코드 문자열(세트 단계 효과, 정보 창, 개발자 모드)도 고쳤습니다. **앞으로 게임 문구에 가운뎃점을 쓰지 않습니다.** 주석과 인스펙터 머리글은 그대로입니다.
- [x] **공격 예고:** 돌진 길과 근접·탄막 고리는 Ground 정렬 층이라 캐릭터·몬스터(Actors 층)보다 항상 뒤에 그려집니다(확인함, 주석 추가).
- [x] **맵 장식 8종 (Codex CLI):** 마네킹, 실타래·단추 더미, 재봉틀 잔해, 버려진 인형, 거울 조각, 촛대, 바닥 얼룩 데칼, 가위·바늘 무더기입니다.
  - 그림 흐름: `tools/codex_prompts/decor_props.txt`(참고 그림은 바닥 타일) → `art_src/raw/decor_props.png` → `bake_stage2_art.py`(4×2 칸으로 자르기) → `Assets/sprites/decor_*.png`.
  - 배치: `ArenaRenderer.ScatterDecor`가 라운드마다 무작위로 흩습니다. 데이터는 `db.decor`: 100만 px²당 4개, 간격 220, 시작 자리 반경 260은 비움, 크기 .85~1.15, 밝기 .8, 채도 .75. 라운드 컨셉 바닥 색을 입힙니다.
  - 그리는 순서: Ground 층 맨 앞입니다. 그래서 장식 < 공격 예고·드롭·계단·탑 < 캐릭터·몬스터 순입니다.
- [x] **보스 처치 뒤 탑:** 정화가 끝나면 계단과 함께 아티팩트 탑이 솟습니다(`rounds.bossTowerOffset` (−230, −10)). 아티팩트 3택이고, 다시 굴리기는 한 번입니다(`Combat.RaiseTower`).
- [x] **탑 등장 연출 (쉬어가기·보스 뒤 공통, `ArenaRenderer.DrawTowerRise`):**
  1. 바닥에 원형 솔기가 바늘땀으로 한 땀씩 꿰매지며 빛이 샙니다(`towerStitchTime` .45, `towerStitches` 16).
  2. 탑이 솔기 안에서 바닥을 뚫고 솟아오릅니다. 바닥 선 아래는 절단면으로 잘리고, 떨림·흙먼지·실 불티가 납니다.
  3. 조금 넘쳤다가 착지하며 눌렸다 펴지고, 화면이 흔들리고, 창문에 불이 켜집니다(`towerSettleTime` .35). 전체 시간은 `towerRiseTime` 1.5초이며, 그 전에는 E가 안 됩니다(`Combat.TowerReady`). 솟기 시작과 착지 때 효과음 `stairstep`이 납니다.
- 확인: EditMode 20개 통과(`SecondSetIsHalf`, `TowerRisesAfterBoss` 추가). 캡처: 장식과 예고 고리 겹침(`Temp/Shots/decor_ingame.png`), 탑 등장 단계별(`tower_rise_sheet.png`), 재봉 상자 꼬리표(`inv_setname3-1.png`).
- 남은 것: 실제 속도로 보는 탑 연출. 장식 밀도·밝기는 사람 눈으로 다듬어야 합니다. 원소 반응(화염+얼음 등)은 하지 않았습니다(주/보조만).

## 개발자 모드 (2026-11-23)

- **F1**로 여닫습니다. 에디터와 개발 빌드(`Debug.isDebugBuild`)에서만 켜지고, 릴리스 빌드에서는 컴포넌트가 스스로 꺼집니다.
- 구성:
  - UI: `Main.uxml`의 `dev-panel`, 스타일 `Assets/UI/DevTools.uss`, 세트 버튼 템플릿 `Assets/UI/DevSetButton.uxml`.
  - 코드: `Runtime/Debug/DevTools.cs`. `Game/GameController` 오브젝트에 MCP로 붙였습니다. 지급량은 인스펙터 값입니다: 골드 100, 파편 50, 욕망 ±10.
  - 전투 쪽 함수: `Combat.DevSpawn / DevKillAll / DevEndRound / DevLevelUp / DevBind`, `RunState.devGod / devFreeze`, `GameController.DevGearChanged`.
- 버튼:
  - 주인공: 무적(토글), 체력 회복, 레벨 +1, 털실 채우기, 골드 +, 파편 +, 욕망 ±.
  - 전투: 적 4종 소환(주인공 둘레), 결박 실 걸기(가장 가까운 적에게 결박 가닥 수만큼 → 우클릭으로 바로 결박 당기기), 적 전부 처치, 라운드 끝내기(생존은 시간 0, 보스는 처치), 적 멈춤(토글, 화상·중독은 계속).
  - 재봉 상자: 세트마다 "+1"을 누르면 그 세트 조각 하나를 지급하고, 같은 세트와 가장 많이 맞닿는 자리에 바로 놓습니다(세트 단계 확인용). 모든 조각 지급, 판 비우기, 조각 전부 버리기도 있습니다.
- 위쪽 줄에 층·체력·욕망·적 수, 켜진 세트와 단계, 조각 수가 실시간으로 보입니다.
- **주의:** 재봉 상자 버튼은 실제 프로필(`tower-of-desire.profile.v1`)에 저장됩니다. 원래 상태로 되돌리려면 `TOD/Debug/Reset Profile`을 씁니다.
- 확인: 버튼을 눌러 화염·번개 세트 켜기, 무적, 적 멈춤, 소환, 결박 실 5가닥까지 동작을 확인했습니다(`Temp/Shots/devtools.png`). EditMode 18개 통과.

## 12차 요청 (2026-11-23) — 세트 = 속성 8종 · 조각 고유 효과 8종 · 새 조각 20개

- [x] **세트 이름과 속성:** 혼돈 → **화염**, 절망 → **얼음**, 좌절 → **번개**로 바꾸고 **빛·어둠·피·바람·독**을 더해 8세트가 되었습니다. 세트 id도 `fire/ice/lightning/light/dark/blood/wind/poison`로 바꿨고 조각 데이터를 함께 고쳤습니다. 코드 열거형은 `Element { Plain, Fire, Ice, Lightning, Dodge, Light, Dark, Blood, Wind, Poison }`이고 정수 값은 그대로 유지합니다.
- [x] **세트 효과 = 속성 (능력치 보너스 없앰):** `SetDef`에서 `stat/perTier`를 빼고 `element, damagePerTier, effectPerTier, effectTime, effectRange, effectCap, effectText`를 넣었습니다. 켜진 세트(`GearStats.sets`, 단계 높은 순)마다 당기기 한 타에 속성 피해가 붙고(`Combat.Pull`), 속성 색 숫자가 실 피해 숫자 위에 뜹니다. 부가 효과는 `Combat.ApplySetEffects`가 겁니다.
  | 세트 | 단계 | 단계당 피해 | 부가 효과(단계당) |
  |---|---|---|---|
  | 화염 | 2·4·6·8·10 | 2 | 화상 초당 2 (3초) |
  | 얼음 | 3·6·9 | 3 | 둔화 15% (2초, 최대 60%) — 이동·행동 대기가 함께 느려짐 |
  | 번개 | 1·3·5 | 2 | 반경 220 안 옆 적 1마리에게 6 튐 (`FxKind.Chain`) |
  | 빛 | 2·4 | 2 | 결박·정예·보스에게 속성 피해 +20% |
  | 어둠 | 2·4 | 2 | 저주 4초: 받는 모든 피해 +10% |
  | 피 | 1·3 | 1 | 흡혈: 준 피해의 4% 회복 |
  | 바람 | 1·3 | 1 | 끌어오는 거리 +30%, 맞은 적 반경 160 안 적도 끌림 |
  | 독 | 2·4 | 1 | 중독: 맞은 타수만큼 겹(최대 6), 한 겹당 초당 1 (4초) |
  - 화상·중독은 `visual.dotInterval`(0.5초)마다 모아서 숫자로 띄웁니다. 적 둘레에는 상태 입자(불씨·서리·연기·거품, `visual.statusRate`)가 생깁니다. 상태 값은 `Enemy.ClearStatus`로 풀에서 꺼낼 때 지웁니다.
- [x] **세트가 켜진 실:** 평소에도 그 속성의 Blender 리본과 덮개로 그립니다(`ArenaRenderer.DrawSetStrand`). 굵기는 `bind.setRibbonWidth` 14 × (1 + `setRibbonGrowth` .2 × (단계−1))이고, 덮개는 × `setCoverScale` 1.3입니다. 속성 피해가 들어가면 적 자리에 Codex 속성 그림(`ElementTone.hit`)이 터집니다: 화염·피 = `fx_chaos`, 얼음·독 = `fx_despair`, 번개 = `fx_frustration`, 빛 = `fx_sigil`, 어둠 = `fx_snip`, 바람 = `fx_yank`. 전에는 쓰지 않던 그림입니다.
- [x] **새 리본 4종 (Blender 5.3):** `tools/render_bind_ribbons.py`에 `dark`(휘감는 연기), `blood`(맥박 치는 핏줄기 + 떨어지는 방울), `wind`(나선 세 가닥 + 바람 결), `poison`(출렁이는 독액 + 거품)을 추가했습니다. 결박 글자, 덮개 장식, 입자도 4종을 넣었습니다(`bind.looks[].cover`: 어둠 .75, 피 .8, 바람 .45, 독 .6). 결박 폭발(`DrawBind`)은 새 4종이 아직 기본(빛살) 모양입니다.
- [x] **조각 고유 효과 8종** (`PieceDef.effect/effectValue/effectParam`, 문구는 `gear.effectTexts`, 정보 창·탑 카드에 ★ 줄):
  | 조각 | 효과 |
  |---|---|
  | 광기의 실패 (레전더리) | 탐욕의 실: 욕망 50% 이상일 때 당기기 피해 +25% |
  | 피 묻은 가위 (유니크) | 벼랑 끝: 체력 30% 이하일 때 당기기 피해 +60% |
  | 얼어붙은 핀쿠션 (유니크) | 무거운 매듭: 깔린 매듭 하나마다 당기기 피해 +10% |
  | 깨진 회중시계 (유니크) | 도약 일격: 대쉬 후 1초 안 첫 당기기 피해 +50% |
  | 잠든 인형 눈 (레전더리) | 거인 사냥: 보스·정예에게 주는 피해 +30% |
  | 번개 맞은 재봉틀 (레전더리) | 분풀이: 하나도 못 맞히면 털실을 전부 돌려받고 다음 당기기 +60% |
  | 세 갈래 골무 (레전더리) | 성급한 결박: 결박 가닥 −1 (`Combat.Bound(r, n)`) |
  | 굵은 밧줄 실패 (유니크) | 실 되감기: 당기기로 처치할 때마다 털실 +1 |
- [x] **새 조각 20개 (Codex CLI):** 빛·어둠·피·바람·독 세트마다 4개씩입니다(일반 2, 레어 1, 유니크 1). 아이콘은 `tools/codex_prompts/item_icons_v3.txt`(빛·어둠·피, 초록 키)와 `item_icons_v4.txt`(바람·독, 그림에 초록이 있어 자홍 키) → `art_src/raw/item_icons_v3/v4.png` → `python tools/bake_stage2_art.py` → `Assets/sprites/item_*.png`입니다. 조각 번호는 14~33입니다.
- [x] **재봉 상자:** 세트 패널은 스크롤(`ScrollView set-list`, 최대 640px)되고, 켜진 단계 + 다음 단계만 보입니다. 판에 없는 세트는 이름 줄만 보입니다.
- 확인: EditMode 18개 통과(`SetAddsElementalDamageAndStatus`, `ItemEffectsApply` 추가). 플레이에서 속성 7종의 평소 실과 맞는 순간을 캡처했습니다(`Temp/Shots/sets_sheet.png`). 번개 튐, 어둠 저주, 피 회복, 독 2겹이 값으로 들어가는 것을 확인했습니다. 얼음은 3개가 있어야 켜져서 캡처하지 않았습니다. 재봉 상자는 `Temp/Shots/inv_12b.png`입니다. 테스트 중 저장된 프로필은 지웠습니다.
- 남은 것: 수치 균형(전부 임시값). 새 4종의 결박 폭발 모양. 빛 세트 종이(상아색) 위 일반 등급 회색 이름이 흐림. 사람이 직접 한 플레이.

## 11차 요청 (2026-11-23) — 재봉 상자 목록 색·이름 등급 색·실을 덮는 결박 이펙트

- [x] **목록 줄 = 세트 색:** 재봉 상자 오른쪽 목록에서 줄 바탕 종이를 세트 색으로 칠합니다(`GameUI.SetPaperTint`: 세트 색을 20% 어둡게). 퍼즐 모양도 세트 색(25% 밝게)이고, 바탕과 같은 색이어도 보이도록 바깥에 2px 잉크 테두리를 넣었습니다(`MiniShape`, 정보 창 모양에도 적용). 정보 창의 퍼즐 모양도 세트 색입니다.
- [x] **판 위 조각 = 세트 색:** `BuildPiece`의 종이 색을 세트 색으로 바꿨습니다. 맞닿은 조각의 명암 구분(tone)은 같은 등급이 아니라 같은 세트끼리 합니다. 등급은 왼쪽 위 매듭(이제 등급 색), 테두리, 바늘땀 실 색, 장식, 레전더리 반짝임으로 보입니다.
- [x] **이름 = 등급 색:** 목록 이름과 정보 창 이름을 등급 색으로 바꾸고, USS에 잉크 외곽선 2px(`.piece-name`, `.tip-name`)을 넣었습니다. 어두운 진한 색(레전더리 `#B83338`)은 같은 색조로 밝혀 씁니다(`GameUI.NameColor`: 채도 > .3이고 명도 < .85이면 명도를 .97로). 회색은 그대로입니다. 선택한 줄의 이름을 포도주색으로 바꾸던 USS 규칙은 지웠습니다.
- [x] **결박 실 덮개:** `ArenaRenderer.DrawBindCover`가 결박 실 몸통과 팔로우 스루 채찍 위에 덮개를 그립니다.
  - 바탕은 같은 Blender 리본을 더 넓게 한 번 더 그린 것입니다(가산, 심 없음). 몸통처럼 팽팽할 때 가늘어지지(taut) 않습니다.
  - 그 위에 `coverStep` 간격으로 속성 장식을 얹습니다. 화염은 불길 혀, 번개는 실을 가로지르는 전류(45%만), 서리는 얼음 결정과 양옆 가시, 금실은 반짝임입니다.
  - 결박일 때는 `DrawCut`의 가는 두 선을 그리지 않습니다.
  - 값(MCP 입력): `bind.coverWidth` 44(리본 30보다 넓게), `coverAlpha` .85, `coverStep` 16, 속성별 `looks[].cover`는 화염 1, 번개 .45, 서리 .45, 금실 .55입니다. 밝은 색은 5가닥이 겹치면 하얗게 날아가서 낮췄습니다.
- 확인: EditMode 16개 통과. 재봉 상자는 임시 아이템을 넣어 등급 4종과 세트 3종을 캡처했습니다(`Temp/Shots/inv_after3*.png`). 결박은 속성 4종을 당기기 진행 25·55·70% 시점에 캡처했습니다(`Temp/Shots/bind_cover_final.png`). 테스트 중 저장된 프로필은 지워서 원래 상태(저장 없음)로 되돌렸습니다.
- 남은 것: 실제 속도로 보는 느낌. 머리 뒤 궤적 꼬리(가늘어지는 혜성 꼬리)는 덮지 않았습니다. 판 캡처는 `Temp/Shots/board_set-1.png`입니다.

## 10차 요청 (2026-11-23) — 결박 당기기(같은 적에 실 5가닥)·속성 리본·실 움직임 원칙

- [x] **결박 당기기:** 같은 적 하나에 바늘이 `bind.strands`(5)개 꽂힌 채 당기면 그 적에게 피해가 `bind.damageBonus`(+100%)만큼 더 들어갑니다(`Combat.Bound`, 테스트 `BindingFiveStrandsHitsHarder`). 히트스톱·흔들림·번쩍임·반동이 더해지고, 화면은 속성 빛으로 번쩍이며 효과음 `bind`가 납니다.
  - 적 자리 폭발(`FxKind.Bind`)이 끌려가는 적을 따라갑니다(`Fx.follow`). 종류는 화염 기둥, 낙뢰, 얼음 가시, 금빛 빛살이고, 머리 위에 "화염 결박!" 같은 글자가 뜹니다. 적 둘레에는 주인공 쪽으로 흐르는 속도선이 생깁니다.
  - 적은 `bind.yankScale`(2.4배)만큼, `yankTime` 0.3초 동안 가감속(`Timed.smooth`)으로 끌려옵니다. 몸은 `stretchPower`만큼 늘어납니다.
- [x] **속성 연결(데이터):** `visual.tones[].bindStyle`로 정합니다. 혼돈 → 화염, 좌절 → 번개, 절망 → 서리, 기본·회피 → 금실입니다. 색과 글자는 `bind.looks[]`(glow·color·core·text)에 있습니다. 불·번개 세트가 따로 없어서 기존 세트 색과 입자에 맞췄고, 인스펙터에서 바꿀 수 있습니다.
- [x] **Blender 리본:** `tools/render_bind_ribbons.py`(Blender 5.3, 6초)가 속성 4종을 굽습니다. 결과물은 실 몸통(가로 타일, 세로 8프레임 아틀라스), 궤적 꼬리(8프레임), 머리 광구이고, 각각 core(흰색)와 rim(회색) 두 장입니다. 출력 위치는 `Assets/vfx/bind`입니다.
  - 미리보기는 `python tools/preview_bind_ribbons.py`로 만들고, `art_src/vfx/bind/preview.png`에 저장됩니다.
  - 임포트는 메뉴 `TOD/Setup/1`에서 합니다. `bind_*_thread`는 Repeat로 설정됩니다. 그림 연결은 `ArenaRenderer.bindArt[]`(MCP)에 있습니다.
  - 그리기는 `Painter.Ribbon`이 맡습니다. 가산 선에 `_MainTex`·`_MainTex_ST` 프로퍼티 블록으로 프레임을 고르고 손 쪽으로 흐르게 합니다. 일반 `Line`은 블록을 비웁니다.
- [x] **실 움직임 (꽂혀 있을 때는 이전처럼 일직선):**
  - **가감속:** `BindCurve`는 뒤로 감김(`windup`·`windupTime`) → 가속 → 감속(`accelSplit`) 순서입니다. `arriveAt`(0.72)에 손에 닿고, 결박 실은 `bind.pullTime` 0.7초 동안 감깁니다.
  - **아크:** 머리가 2차 베지어 호를 따라옵니다. 가닥마다 `arcSides`(1, −1, .55, −.55, .25)로 부채꼴을 그립니다. 호 높이는 `arcHeight × sin(π·진행)`이라 붙어 있을 때와 손에 닿을 때는 일직선입니다. 실 몸통과 꼬리도 같은 호를 따릅니다. 일반 당기기는 `needleVisual.pullBend`, 날아가는 실은 `flightBend`(속도 비례, 바늘 뒤로 처짐)로 휩니다.
  - **스쿼시 앤 스트레치:** 머리 광구는 속도(`stretchSpeed`)에 따라 진행 방향으로 `stretchAmount`배 늘어나고, 부피는 유지됩니다. 실 몸통은 빠르거나 뒤로 감길수록 가늘어집니다. 손에 닿으면 `squashAmount`만큼 찌그러졌다가 출렁입니다. 바늘은 바닥에 꽂힐 때 `stickSquash`만큼 짧아졌다 돌아옵니다.
  - **팔로우 스루:** 머리가 멈춘 뒤 꼬리가 손을 지나 채찍처럼 휘감기고(`followLength` 140, `followCurl`), 꼬리가 줄어들며 따라 들어옵니다. 손에서는 고리 두 겹이 퍼집니다. 꽂힌 실은 반대편으로 한 번 출렁인 뒤(`settleDamp`·`settleFreq`) 펴집니다.
- 확인: EditMode 16개 통과. 플레이에서 속성 3종과 시간 순 장면을 캡처했습니다(`Temp/Shots/v_pull_sheet.png`, `arc_fire_sheet.png`, `arc_other_sheet.png`, `v_flight_sheet.png`). 에디터가 포커스를 잃으면 화면 캡처가 늦게 찍혀서, 같은 호출 안에서 `GameController.Update` → `Camera.Render`로 찍었습니다.
- 남은 것: 실제 속도로 보는 손맛과 소리 확인. `followLength`·`flightBend` 상향 이후 모습은 캡처하지 않았습니다. 같은 적에 5가닥이 겹치면 가산 합성이라 가운데가 많이 밝습니다.

## 9차 요청 (2026-11-23) — 재봉 상자 끌어 놓기·맞닿은 경계 바느질

- [x] **꾹 눌러 끌기:** 판 위 조각이나 목록 줄을 0.28초 누르고 있거나 14px 넘게 끌면 집어 듭니다(`GameUI.HoldTime/DragSlop`).
  - 집은 동안 조각 전체가 반투명(0.62)하게 손끝을 따라옵니다. 손끝은 회전 중심 칸의 가운데입니다. 조각은 Main.uxml `drag-piece`(인벤토리 맨 위 층)에 `BuildPiece`로 한 벌 그리고, 조각·회전·칸 크기가 바뀔 때만 다시 만듭니다. 끄는 중에도 R로 회전할 수 있습니다.
  - 판 위에서는 놓일 칸을 상아색 영역으로 칠하고(`board-ghost.drag`, 못 놓으면 붉은색), 원래 자리는 0.3로 흐려지며 그 조각의 경계 이음은 풀립니다.
  - 떼면 그 칸에 들어갑니다. 손끝 자리에서 칸 자리로 미끄러져 들어가며(`pendingDrop` → `dropFrom`, 0.16초) 반투명이 풀립니다. 못 놓는 자리면 안내를 띄우고, 판 밖이면 취소합니다(`GameController.BeginDrag/DropDrag`).
- [x] **맞닿은 경계 바느질:** 서로 다른 조각이 맞닿은 칸 경계마다 어두운 홈과 건너 잇는 땀 두 개(`join_h/v.png`, `tools/bake_puzzle_tiles.py`)를 그립니다. 등급이 같은 조각끼리 맞닿으면 종이 명암을 달리해 구분합니다.
  - 장착할 때 이웃과 맞닿은 경계가 있으면 바늘이 **그 경계만** 맨 위·왼쪽부터 이어서 꿰맵니다. 경계 한 칸에 두 땀이고, 바늘이 경계를 가로질러 들락날락합니다. 이음(`piece-join` 창 + `join-art` 그림)은 바늘을 따라 한 칸씩 드러나고, 마지막 땀 자리에서 매듭 불티가 튑니다. 바깥 둘레는 이미 꿰매진 채로 둡니다.
  - 이웃이 없으면 지금처럼 둘레를 한 바퀴 꿰맵니다.
- 확인: EditMode 15개 통과. 플레이에서는 리플렉션으로 끌기·놓기를 재현했습니다. 끄는 중 화면 캡처는 `Temp/Shots/drag_mid2_crop.png`입니다. 이음이 순서대로 드러나는 것은 값으로 확인했습니다(t=.25 → 1칸, .4 → 2칸, .6 → 3칸 모두). 못 놓는 자리에 놓으면 배치가 그대로인 것도 확인했습니다. 테스트 후 프로필 배치는 원래대로 되돌렸습니다.
- 남은 것: 실제 마우스로 해 보는 손맛 확인. 에디터가 포커스를 잃으면 프레임이 늦게 찍혀, 바느질 중간 장면은 캡처로 정확히 잡지 못했습니다.

## 7차 요청 (2026-11-23) — 계단이 한 단씩 놓이는 연출·아이템은 탑에서만

- [x] **계단 연출:** 라운드가 끝나면 계단이 꼭대기 문(0번)부터 맨 아래 단까지 한 조각씩 '다라라락' 놓입니다. 조각은 제자리 바로 위(`palette.stairDrop` 18px)에서 내려앉으며 살짝 눌렸다 펴지고, 닿을 때 먼지·빛 부스러기가 일고 효과음 `stairstep`이 납니다(소리 이벤트는 `Combat`이 조각마다 냄). 다 놓이기 전(`rounds.stairsRevealTime` 0.7초)에는 E가 안 됩니다.
  - 값: `rounds.stairPieces` 6, `stairStepDelay` .09초, `stairSettle` .16초 / `palette.stairScale` .3(그림 px → 캔버스 px), `stairRise` 26(한 단 높이), 계단 위치 `stairsOffset` (170, −30).
  - 조각에 라운드 컨셉의 바닥 덮개(정화된 층은 정화 밝기)를 입혀 바닥과 같은 색감이 되고, 바닥 빛은 부드러운 스프라이트로 바꿔 "단단한 원판처럼 보임" 문제를 고쳤습니다. 다 놓인 뒤 문 안쪽 빛이 숨 쉬듯 밝아집니다.
  - 코드: `ArenaRenderer.DrawStairCascade`(`stairPieces`가 비어 있으면 옛 통그림 `stairsSprite`로 그림).
  - 그림: `tools/codex_prompts/stairs_steps_v2.txt`(바닥 타일을 참고 이미지로 줌, 초록 키) → `art_src/raw/stairs_steps_v2.png` → `python tools/bake_stage2_art.py` → `Assets/sprites/stair_0~5.png`. v1(`stairs_steps.png`)은 검은 외곽선·분홍 바늘땀이라 바닥과 따로 놀아 버렸습니다(굽기는 v2가 없을 때만 v1을 씀).
- [x] **아이템은 쉬어가기 탑에서만:** `rewards.eliteItemChance` 0, 상인 품목에서 조각(`piece`) 제거(2차 결정 반영). 테스트 `ItemsOnlyComeFromTheTower`.
- 확인: EditMode 12개 통과. 플레이에서 R1 시간 종료 → 계단이 놓이는 중간·완료 화면 캡처(`Temp/Shots/stairs_v2_*.png`).
- 남은 것: 실제 속도로 본 '다라라락' 느낌과 `stairstep` 소리는 사람 눈·귀 확인 필요. 정화된 층·다른 컨셉 색에서의 계단 색감은 보지 못했습니다.

## 6차 요청 (2026-11-23) — 레벨업 아이콘·피해 숫자 그림 글꼴

- [x] **레벨업 아이콘:** 레벨업 때 몸 둘레 여기저기에서 레벨업 아이콘(Codex `levelup_icon` → `Assets/sprites/levelup_icon.png`)이 차례로 톡 튀어나와 떠오르며 사라집니다. 수·크기·퍼짐은 `visual.levelUpIcons / levelUpIconSize / levelUpIconSpread`.
- [x] **피해 숫자 그림 글꼴:** 숫자를 Codex 로 그린 글자(흰색 + 굵은 검은 외곽선, `0~9 + - !`)로 바꿨습니다.
  - 굽기: `art_src/raw/damage_digits.png` → `python tools/bake_damage_font.py` → 아틀라스 `Assets/Art/Generated/damage_font.png` + 글자 위치 `art_src/fonts/damage_font.json` → 메뉴 `TOD/Setup/1. Texture Imports` 가 JSON 대로 글자 스프라이트(`damage_font_<글자 코드>`)를 잘라 냄(스프라이트 ID 유지).
  - 쓰기: `visual.damageFont` (`chars` ↔ `glyphs`, 높이·간격). `ArenaRenderer.DrawGlyphs` 가 한 줄을 가운데 정렬해 그리고 색은 곱해 물들입니다(피해·전달·피격·회복 색 그대로). 그림에 없는 글자가 섞인 문구(예: 회피!)는 UI 글꼴로 씁니다.

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
- **서버가 안 켜질 때 (2026-11-23):** Start Server는 `uvx`로 서버를 띄웁니다. `uvx.exe`는 `%APPDATA%\Python\Python313\Scripts`에 있고, 이 폴더를 사용자 PATH에 추가했습니다. 에디터는 Unity Hub의 환경 변수를 물려받으므로 **Hub까지 완전히 껐다 켜야** 새 PATH가 적용됩니다. 서버를 밖에서 띄운 경우(`uvx --from "mcpforunityserver==10.3.0" mcp-for-unity --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools`)에는 Start Server(이때는 "Stop Server"로 보임)가 아니라 **Connect** 버튼으로 붙습니다. 실패 기록은 `Library/MCPForUnity/Logs/server-launch-8080.log`에 남습니다.
- **Blender 연결 (2026-11-23):**
  - Blender 5.3에 BlenderMCP 애드온("MCP for Blender" 2.0)을 넣었습니다. 원본은 `C:\Users\MinSung\blender-mcp`(github.com/ahujasid/blender-mcp, 커밋 a5a8acf, 검토 완료)입니다. 이 애드온은 localhost에서만 듣고, 원격 수집과 외부 연동(Poly Haven·Sketchfab 등)은 기본으로 꺼져 있습니다.
  - Unity 쪽 설정(EditorPrefs `BlenderBridgePrefs`): 원본 경로 = 위 폴더, 애드온 폴더 = `%APPDATA%\Blender Foundation\Blender\5.3\scripts\addons`.
  - 쓰는 법: Blender를 켜면 애드온이 127.0.0.1:9876 서버를 자동으로 엽니다. 그다음 `blender_bridge status / run_python / screenshot / import_model`을 씁니다. 애드온을 새로 받으면 원본 폴더에서 `git pull`을 한 뒤 `blender_bridge sync_addon`을 실행합니다.
  - **주의:** Claude 데스크톱 앱은 MSIX 패키지라서, 그 셸이 `%APPDATA%`·`%LOCALAPPDATA%`에 쓴 파일은 앱 전용 사본으로 빠집니다. Unity와 평소 켜는 Blender에서는 보이지 않습니다. 사용자 폴더의 다른 위치나 프로젝트에 두거나, Unity(`execute_code`, `sync_addon`)를 통해 씁니다. 같은 이유로, 위의 uvx PATH 설정이 Claude 셸에서 한 것이었다면 실제 환경에는 없을 수 있습니다.
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
