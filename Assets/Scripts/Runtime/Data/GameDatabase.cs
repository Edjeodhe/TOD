// 게임 데이터 컨테이너. 값은 코드에 두지 않고 씬의 GameDatabase 오브젝트에 MCP(update_component)로 넣는다.
// 필드 이름과 단위는 웹판(web/src/game/*.js)의 상수를 그대로 따른다. 거리는 전투장 px, 시간은 초.
using System;
using UnityEngine;

namespace TOD
{
    // 실의 속성 = 재봉 상자 세트. 직렬화 값(정수)이 데이터에 저장되므로 새 값은 끝에만 붙인다.
    // Plain = 세트가 없을 때의 기본 실, Dodge = 대쉬 흔적. (12차: 혼돈 → 화염, 절망 → 얼음, 좌절 → 번개로 이름 변경, 빛·어둠·피·바람·독 추가)
    // 22차: 8속성 → 칠죄종. 정수 값(직렬화)은 그대로 두고 이름만 바꿈 (Fire→Wrath, Ice→Sloth, Lightning→Unused3, Light→Pride, Dark→Greed, Blood→Gluttony, Wind→Lust, Poison→Envy)
    public enum Element { Plain, Wrath, Sloth, Unused3, Dodge, Pride, Greed, Gluttony, Lust, Envy }
    // 재봉 조각 고유 효과 (유니크·레전더리). value·param 의 뜻은 GearConfig.effectTexts 설명 참고.
    public enum ItemEffect { None, Brink, QuickBind, Spite, HeavyKnot, Leap, Greed, Rewind, Giant,
        // 22차 칠죄종 조각과 신화 도안: 금실 물레(금화 param 개마다 +value, 최대 = 금화 상한까지) · 만찬(처치마다 param 초 동안 +value, 겹침 최대 5)
        // 삼키기(처치하면 체력 +value) · 얽힌 실(결박한 적에게 +value) · 높은 굽(체력이 가득하면 +value) · 왕좌(치명타 확률 +value) · 재단(당기기 피해 +value)
        GoldThread, Feast, Devour, Embrace, Crown, Pedestal, Tailor }
    // 재봉 조각 능력치. 직렬화 값(정수)이 조각 데이터에 저장되므로 새 값은 끝에만 붙인다.
    // Multishot = 한 번 쏠 때 더 나가는 바늘 수(좌우로 부채꼴), ThreadWidth = 실 굵기 증가 비율(0.6 = +60%)
    public enum StatKind { Damage, Speed, Health, Recharge, Purity, Yarn, Multishot, ThreadWidth }
    public enum WaveShape { Sine, Triangle, Sawtooth, Square }
    public enum RoundKind { Survival, Rest, Boss }
    // 증강이 바꾸는 등반 능력치
    public enum RunStat { FireInterval, Reach, NeedleSpeed, Damage, YarnMax, Recharge, Encumbrance, DashCooldown, MoveSpeed, Desire, MaxHealth }
    public enum StatOp { Add, Multiply }

    [Serializable] public class ArenaConfig
    {
        public float width, height;          // 맵 논리 크기 (라운드마다 새로 만드는 경계 있는 맵)
        public float projectionY;            // 바닥 세로 압축 (.67)
        public float attackY;                // 공격 판정 세로 비율 (.74)
        public float screenTop;              // 캔버스 좌표에서 바닥 y=0 의 위치
        public float canvasHeight;           // 카메라가 세로로 보여 주는 캔버스 px (590)
        public float pixelsPerUnit;          // 캔버스 px → Unity 단위
        public float wallLeft, wallRight, wallTop, wallBottom;   // 플레이어·바늘이 닿는 경계
        public float enemyMarginX, enemyMarginY;
        public int finalFloor;
    }

    [Serializable] public class MapConfig
    {
        public float chunkSize;              // 바닥 청크 한 변 (전투장 단위, 바닥 타일 크기의 배수)
        public int lampsPerChunk;
        public float lampInset;              // 청크 가장자리에서 등불까지 최소 거리
        public float cameraOverscan;         // 맵 밖으로 카메라가 더 보여 줄 수 있는 거리 (캔버스 px)
        public float wallHeight;             // 맵 위쪽 벽 장식 높이 (캔버스 px)
        public float borderInset, borderInnerInset;
        public Color wallColor, wallEdge, borderColor, borderInnerColor;
        public float archHeight, archSpacing;    // 위쪽 벽을 따라 반복하는 아치 (캔버스 px)
    }

    // 층 = 생존 R1 → 생존 R2 → 쉬어가기 → 보스. 생존 라운드는 시간을 버티면 끝난다.
    [Serializable] public class RoundConfig
    {
        public RoundKind[] sequence;
        public float[] survivalSeconds;      // 생존 라운드 순서대로
        public Vector2 stairsOffset;         // 라운드가 끝났을 때 플레이어 기준 계단 위치
        public Vector2 altarOffset, merchantOffset, restStairsOffset, bossOffset;
        public float interactRadius;
        public float stairsRadius;            // 계단 E 판정 반경 (계단 그림이 커서 따로 넓게)
        [Header("탑 등장 (쉬어가기 · 보스 처치 뒤): 바닥 솔기 꿰매기 → 솟아오름 → 착지·창문 불")]
        public float towerStitchTime;         // 바닥에 원형 솔기가 한 땀씩 꿰매지는 시간
        public float towerRiseTime;           // 등장 전체 시간 (이 뒤에 E 가능)
        public float towerSettleTime;         // 마지막 착지 출렁임 시간
        public int towerStitches;             // 솔기 땀 수
        public Vector2 bossTowerOffset;       // 보스 처치 뒤 탑 자리 (주인공 기준, 계단과 겹치지 않게)
        public Vector2 stairsHitOffset;       // 계단 판정 중심 = 계단 발밑 + 이만큼 (y 음수 = 계단 위쪽 단 방향)
        public float merchantChance;
        public int artifactChoices;
        public float stairsRevealTime;       // 계단 등장 연출 시간 (마지막 단이 자리 잡을 때까지, 그 전에는 E 가 안 된다)
        public int stairPieces;              // 계단 조각 수 (꼭대기 문 + 단들) — 조각마다 '탁' 소리 이벤트
        public float stairStepDelay, stairSettle;   // 조각이 차례로 나타나는 간격, 한 조각이 자리 잡는 시간
        public float clearFadeTime;          // 시간 종료 때 남은 적이 흩어지는 연출 시간
    }

    // 유형별 스폰: interval 초마다 count 마리 (무작위 흔들림 포함). 컨셉의 typeWeights 와 층 배율이 곱해진다.
    [Serializable] public class SpawnEntry
    {
        public string type;
        public float interval, intervalJitter, startDelay;
        public int count, countJitter;
        public float eliteChance;
    }

    [Serializable] public class SpawnConfig
    {
        public SpawnEntry[] table;
        public int maxEnemies;
        public float spawnDistanceMin, spawnDistanceMax;   // 플레이어 기준 화면 밖 거리
        public float floorRateStep;          // 층마다 스폰 간격이 줄어드는 비율
        public float minInterval;
    }

    [Serializable] public class TypeWeight { public string type; public float weight; }

    // 임시 층 컨셉. 층마다 10개 중 3개를 뽑아 R1·R2·보스 라운드에 쓴다.
    [Serializable] public class ConceptDef
    {
        public string id, name;
        public Color lampTint, floorFill;    // floorFill.a = 바닥 덮기 양
        public float spawnRate, hpScale, speedScale, eliteScale;
        public TypeWeight[] typeWeights;
    }

    [Serializable] public class PurifyConfig
    {
        public float duration, zoom, zoomIn, zoomOut;
        public Color ringColor, glowColor, floorFill;
        public float ringWidth, glowWidth, floorBright, floorSat, vignetteCut, edgeSoftness;
        public float edgeNoise, petalRate, centerGlow;   // 경계를 꽃잎처럼 흐트러뜨리는 양(px)·꽃잎 입자 수·가운데 빛 크기
    }

    // 보스 처치 연출: 실이 몸을 감아 조이고(wrapTime) → 종이처럼 잘려(cutTime) → 조각이 떨어져 흩어진 뒤(fallTime) 정화가 시작된다.
    [Serializable] public class BossDeathConfig
    {
        public float wrapTime, cutTime, fallTime;
        public int loops, cuts, flakes;
        public float threadWidth, shake, flakeSpeed;
        public Color threadColor, cutColor, flakeColor;
        public float Total => wrapTime + cutTime + fallTime;
    }

    [Serializable] public class NeedleVisual
    {
        public float flightLift, diveTime, stickAngle, stickTilt;
        public float ringRadius, ringWidth, ringGlow, ringPulse, ringPulseSpeed, ringAlpha;
        public float pullSnapDelay, pullSnapPower;
        [Header("실 움직임: 아크 · 스쿼시 앤 스트레치 · 팔로우 스루 (꽂혀 있을 때는 일직선)")]
        public float flightBend;              // 날아가는 동안 실이 뒤로 처져 휘는 정도 (실 길이 비율, 빠를수록)
        public float settleDamp, settleFreq;  // 꽂힌 뒤 반대편으로 출렁이다 펴지는 감쇠·진동수
        public float stickSquash;             // 바늘이 꽂히는 순간 짧아지는 정도 (0~1)
        public float pullBend;                // 보통 당기기: 감겨 드는 동안 휘는 정도 (실 길이 비율)
    }

    [Serializable] public class HeroConfig
    {
        public float baseHp, baseDamage;
        public float strandBonus, strandBonusMax;   // 당길 때 실 한 가닥이 더 걸릴 때마다 피해 +strandBonus (최대 strandBonusMax)
        public float critChance, critMultiplier;    // 치명타: 당기기로 적 하나에 들어가는 실 피해마다 확률 critChance 로 × critMultiplier (속성 피해는 제외)
        public int baseYarn, minYarn;
        public float moveSpeed, encumbrance, minSpeedFactor, frenzySpeed, initialAngle;
        public float shotInterval, aimDeadZone;
        public float rechargeSeconds, idleRechargeMultiplier, frenzyRechargeMultiplier, idleMotionThreshold;
        public int knotInterval, earlyKnotInterval;
        public float hurtTime, hurtFlash, dryTime, hurtShake;
        public float strideBase, strideFactor, motionLerp;
        public float shootHold, pullHold, hurtHold, dashHold;
        public float pickupRadius, magnetRadius, magnetSpeed, bulletHitRadius;
        [Header("재봉 조각: 여러 갈래 바늘 · 굵은 실")]
        public float multishotSpread;         // 갈래 바늘 사이 각도 (라디안, 화면 기준)
        public int multishotMax;              // 한 번에 더 나가는 바늘 수 상한 (2 = 3갈래)
        public float threadWidthMax;          // 실 굵기 배율 상한 (그리기 두께에 곱한다)
        public float threadHitWidth;          // 굵기 +100% 마다 당김 판정 반경이 넓어지는 양 (전투장 px)
    }

    [Serializable] public class NeedleConfig
    {
        public float reach, launch, settle, kick, start, curve;
        public float groundShake;
    }

    [Serializable] public class FeelConfig
    {
        [Header("Throw recoil")] public float recoilHero, recoilTime, recoilCamera, recoilCameraTime;
        [Header("Needle bite")] public float stickFreeze, stickPush, stickTime, stickBossPush, stickShake, stickFlash, stickJolt;
        [Header("Pull cut")] public float cutTime, cutSplit, cutFlash;
        // dashCooldown = 대쉬 1칸이 다시 차는 시간, dashMinCooldown = 연달아 쓸 때 최소 간격
        [Header("Dash")] public float dashDuration, dashGrace, dashDistance, dashEase, dashSlow, dashGhostInterval, dashGhostLife, dashCooldown, dashMinCooldown;
        public int dashCharges;               // 대쉬 칸 수 (2)
        public float dashTrailTime, dashTrailPad, dashFxOffset;
        [Header("Yank")] public float yankReach, yankBoss, yankTime, yankMinGap, stretchTime;
        [Header("Pull impact")] public float pullHitPad, pullEnemyFlash;
        public float freezeBase, freezePerHit, freezeMax;
        [Header("24차 프레임 스톱 (당기기, 보스 마지막 베기): 맞는 자세까지 흐른 뒤 화면 전체가 멈춘다. 흔들림만 계속")]
        public float hitStopLead;             // 당긴 뒤 이만큼(초)은 베기 섬광, 숫자, 주인공 자세가 흘러 맞는 순간에 닿는다
        public float hitStopScale;            // 당기기 멈춤 시간 배율 (freezeBase + 타수 × freezePerHit, 상한 freezeMax 에 곱한다. 0 = 1)
        public float shakeBase, shakePerHit, shakeMax, shakeEmpty;
        public float kickBase, kickPerHit, kickMax, kickTime;
        [Header("24차 줌 펀치 (당기기로 맞힌 순간 카메라가 확 다가갔다가 돌아옴). 값 = 직교 크기를 줄이는 비율 (.02 = 2%)")]
        public float zoomPunchBase;           // 한 타 맞힘
        public float zoomPunchPerHit;         // 타수 하나 늘 때마다 (2타부터)
        public float zoomPunchMax;            // 타수 몫의 상한
        public float zoomPunchBind;           // 결박(같은 적에 바늘 여러 개)이면 더함
        public float zoomPunchCrit;           // 치명타가 하나라도 있으면 더함
        public float zoomPunchCap;            // 전체 상한
        public float zoomPunchIn;             // 다가가는 시간(초). 프레임 스톱 동안은 다 다가간 채로 멈춰 있음
        public float zoomPunchOut;            // 돌아오는 시간(초)
        public float flashBase, flashPerHit, flashMax;
        public int maxPopCount;
    }

    [Serializable] public class EnemyType
    {
        public string id;
        public float hp, speed, damage, radius, wait, gold, height, preferredDistance;
        public Sprite sprite;
    }

    [Serializable] public class EnemyConfig
    {
        public EnemyType[] types;             // stalker, charger, shooter, boss
        public float hpPerFloor, damagePerFloor, speedPerFloor;
        public float eliteHp, eliteDamage, eliteHeight, eliteGold;
        public float waitJitter;
        public float approachSlack, retreatSlack, retreatSpeed;
        public float meleeTrigger, meleeRange, bossShotChance;
        public float windupMelee, windupShot, windupDash, enragedWindupDash;
        public float chargeTime, chargeSpeed, bossChargeSpeed, enragedChargeSpeed, chargeHitPad;
        public float restAfterCharge, enragedRestAfterCharge, restAfterMelee, restAfterShot, enragedWaitMultiplier;
        public float enrageAt;
        public float[] bossThresholds;
        public string[] bossSummons;
        public Vector2 summonOffset;
        public int circleShots, bigFanShots, smallFanShots, bigFanFromFloor;
        public float fanSpread, circleSpeed, fanSpeed, bulletLife;
    }

    [Serializable] public class RewardConfig
    {
        public int xpNormal, xpElite, xpBoss;
        public int shardElite, shardBoss, shardFloorClear;
        public float eliteItemChance;
        public bool bossDropsItem;            // 아이템은 쉬어가는 라운드에서만 얻으므로 보통 false
        public int xpBase, xpStep;
        [Header("체력 회복 하트 (낮은 확률로 떨어짐)")]
        public float healChance, healEliteChance;   // 처치 한 번에 하트가 떨어질 확률 (일반·정예)
        public float healAmount;              // 최대 체력 대비 회복량 (0.15 = 15%)
        public float heartSize, healTime;     // 하트 그리기 크기(캔버스 px), 몸에 피가 차오르는 연출 시간
        public Color healColor;               // 회복 연출(몸이 차오르는 색·거품·+N 숫자) — 연두
        public Color heartGlowColor;          // 바닥에 떨어진 하트가 두르는 빛·빨려 들 때 꼬리 — 빨강
        [Header("떨어지는 경험치 구·골드")]
        public int maxXpOrbs, maxCoins;       // 처치 한 번에 나오는 최대 개수 (값은 나눠 담는다)
        public float dropPopSpeed, dropPopTime, dropHop;          // 튀어나오는 속도·시간·높이(캔버스 px)
        public float dropVacuumSpeed, dropVacuumAccel;            // 그 뒤 몸으로 빨려 드는 속도·가속
        public float orbSize, coinSize, dropGlow;                 // 그리기 크기(캔버스 px), 빛 배율
        public Color xpColor, goldColor;                          // 흡수 반짝임 색
        [Header("23차 보스 처치 기억의 파편 (shardBoss + shardFloorClear 를 나눠 담아 바닥에 흩뿌린다)")]
        public int bossShardPieces;           // 떨어지는 알 수
        public float bossShardSpread;         // 흩어지는 속도 (캔버스 px/초, 끌림으로 멈춤)
        public float bossShardFall, bossShardFallHeight;          // 떨어지는 시간(초)과 높이(캔버스 px)
        public float bossShardHold, bossShardStagger;             // 바닥에 머무는 시간(초), 알마다 엇갈림(초)
        public float bossShardAccel;          // 빨려 드는 가속 (시작 속도는 dropVacuumSpeed)
        public float shardSize;               // 그리기 크기(캔버스 px)
        public Color shardColor;              // 빛, 꼬리, 몸에서 반짝임 색
        public string shardSound;             // 흡수할 때 효과음 (SoundBank 이름)
    }

    [Serializable] public class AugmentEffect { public RunStat stat; public StatOp op; public float value; }

    [Serializable] public class AugmentDef
    {
        public string id, name;
        [TextArea] public string text;
        public bool rule;                     // 규칙형(현재 데이터에서 빠짐, 코드만 남음)
        public Sprite icon;
        public Color color;                   // 퍼즐 카드 색
        public AugmentEffect[] effects;       // 능력치형 효과
    }

    [Serializable] public class AugmentConfig
    {
        public AugmentDef[] augments;
        public int choices, ruleWeight, statWeight;
        public float echoWindow;
        public float minFireInterval;
    }

    [Serializable] public class DesireDef
    {
        public string id, name, text, penalty;
    }

    [Serializable] public class DesireConfig
    {
        public DesireDef[] desires;           // obsession, rage, hunger
        public DesireDef refine;              // 세 욕망을 모두 흡수한 뒤의 대체 제안
        public float refineDamage;
        public float perFloor, absorb, maxBase, capacityStep;
        public float awakenChance, awakenReset, frenzyReset, awakenDamage;
        public float highFraction, priceMarkup;
        public int obsessionYarn, rageKnots;
        public float rageMultiplier, rageTaken, hungerHeal, hungerPotion;
        [Header("욕망이 높을 때 화면이 붉게 일렁임")]
        public float warpFrom;                // 욕망 비율 (.8)
        public Color warpTint;                // a = 최대 섞는 양
        public float warpAmplitude, warpFrequency, warpSpeed;   // 일렁임 진폭(캔버스 px)·주파수·속도
        [Header("23차: 보스 욕망을 고른 뒤 화면에 잠깐 뜨는 한 줄 (GameUI.DesireEcho.cs)")]
        public string swallowLine;            // 삼킴: "달다. 그래서 더 무섭다."
        public string refuseLine;             // 안 삼킴: "손을 거두자, 욕망이 식어 간다."
        public float echoDelay;               // 고른 뒤 쪽지가 뜨기까지 (초)
        public float echoCharTime;            // 글자 하나가 번지는 시간 (초)
        public float echoHold;                // 다 쓰인 뒤 머무는 시간 (초)
        public float echoFade;                // 사라지는 시간 (초)
        [Header("23차: 욕망을 고른 뒤 그 층에서 어두워지며 다음 층으로 (GameController.FloorFade.cs)")]
        public Color swallowTint;             // 삼킴: 고르자마자 화면 전체에 번지는 붉은 기 (a = 섞는 양)
        public float tintIn;                  // 붉은 기가 다 번지기까지 (초)
        public Color swallowDark;             // 삼킴: 다 어두워졌을 때 색 (검붉은, a = 진하기)
        public Color refuseDark;              // 고개를 돌린다: 다 어두워졌을 때 색 (검정, a = 진하기)
        public float darkOutTime;             // 어두워지는 시간 (초). 문구가 사라지는 때에 맞춰 다 어두워진다
        public float darkHold;                // 다 어두워진 채 머무는 시간 (초). 이때 다음 층이 열린다
        public float darkInTime;              // 다음 층에서 밝아지는 시간 (초)
    }

    // 세트 = 속성. 같은 색 조각을 맞대 단계(thresholds)가 켜지면 실이 그 속성 모습이 되고, 당길 때 한 타마다 속성 피해가 더해지며 부가 효과가 붙는다.
    // 22차 칠죄종. 부가 효과(effectPerTier × 단계 = v)의 뜻은 죄종마다 다르다:
    //   분노 = 화상 초당 v, 내 체력이 낮을수록 피해 +(1 - 체력 비율) × extra × 단계
    //   나태 = 둔화 비율 v(effectCap 상한), 맞을 때마다 1겹 · extra 겹이면 extraTime 초 잠듦(보스는 bossSleepScale 배)
    //   시기 = 독 한 겹당 초당 v(effectCap 겹까지), 독 걸린 적이 죽으면 effectRange 안 extra 마리에게 겹을 옮김
    //   식탐 = 준 피해 중 v 비율 회복, 처치하면 털실 +extra
    //   색욕 = 매혹 effectTime 초(공격 안 함) + effectRange 안 주변 적도 끌려옴, 끄는 거리 +v 비율
    //   탐욕 = 저당(받는 피해 +v 비율, effectTime 초), 저당 잡힌 적을 처치하면 금화 +extra × 단계
    //   교만 = 정예·보스·체력이 가득한 적에게 피해 +v 비율
    [Serializable] public class SetDef
    {
        public string id, name, bonus;        // bonus = 속성 피해 이름 (예: "화염 피해")
        public Color color;
        public Element element;
        public int[] thresholds;
        public float damagePerTier;           // 당기기 한 타마다 더하는 속성 피해 (단계당)
        public float effectPerTier;           // 부가 효과 세기 (단계당)
        public float effectTime;              // 부가 효과 지속 (초)
        public float effectRange;             // 번개 튐·바람 끌림 반경 (전투장 px)
        public float effectCap;               // 얼음 둔화 상한 · 번개 튀는 마리 수 · 독 최대 겹
        public string effectText;             // 부가 효과 문구. {0} = 이 단계의 값, {1} = extra × 단계 (예: "화상 초당 {0}")
        public string brief;                  // 22차 세트 패널 목록의 짧은 효과 (effectText 와 같은 {0}, {1}). 긴 문구는 마우스 올림 창에서
        public float weight = 1;              // 24차 세트 가중치 (2단 룰렛의 2번: 같은 등급 안에서 이 세트 조각이 뽑힐 비중). 0 이면 무작위로는 안 나옴
        [TextArea] public string description; // 22차 세트 마우스 올림 창의 설명 한두 줄
        [Header("22차: 죄종 두 번째 효과 (위 설명 참고)")]
        public float extra;
        public float extraTime;
        public float bossSleepScale;          // 나태: 보스가 잠드는 시간 배율
        [Header("16차: 부가 세트 주기 발동 (메인이 아닌 켜진 세트)")]
        public int procCharge;                // 바늘 명중·당기기 타격 몇 번에 한 번 터지는지
        public float procDamage, procDamagePerTier;   // 발동 피해 = procDamage + procDamagePerTier × 단계 (상성 적용)
        public float procEffectScale;         // 발동 때 부가 효과 세기 = effectPerTier × 단계 × 이 값
    }

    [Serializable] public class PieceDef
    {
        public string name;
        public string set;
        public int cost;
        public StatKind stat;
        public float value;
        public Vector2Int[] shape;
        public Sprite icon;
        [TextArea] public string flavor;      // 카드 아래 짧은 설명
        public ItemEffect effect;             // 고유 효과 (유니크·레전더리·신화)
        public float effectValue, effectParam;
        public float desireStart;             // 22차 붉게 물든 도안: 들고 가면 이 욕망을 안고 등반을 시작한다
    }

    // 고유 효과 이름·설명. text 의 {0} = effectValue, {1} = effectParam
    [Serializable] public class ItemEffectText
    {
        public ItemEffect effect;
        public string name, text;
    }

    [Serializable] public class GearConfig
    {
        public SetDef[] sets;
        public SetUnlock[] setUnlockFloor;    // 15차: 세트 해금 표 (profile.best 가 floor 이상이면 열림, 표에 없으면 처음부터)
        public string unlockSound;            // 15차: 해금 알림 효과음 id (sounds)
        public string starterPlaced;          // (17차부터 안 씀) 15차 시작 조각 자동 배치 문구
        public int starterSlotsBase, starterSlotsStep;   // 17차: 들고 갈 시작 조각 수 = base + '시작 조각 선택' 단계 × step
        public string starterFull;            // 17차: 판이 다 찼을 때 ("시작 조각은 {0}개까지 들고 갈 수 있습니다")
        public PieceDef[] pieces;
        public string[] rankNames;            // 코스트 1~5 → 일반·레어·유니크·레전더리·신화(22차 아버지의 도안)
        public int baseColumns, baseRows, baseCostLimit, costLimitStep;
        public float rowBonus;
        public int[] startingKinds;
        public Color[] rankColors;            // 코스트 1~4 → 일반 회색·레어 하늘색·유니크 노란색·레전더리 붉은색 (0번 미사용)
        public float[] rankShine;             // 등급별 번쩍임 세기 (0 = 없음)
        public float fateSetBonus;            // 24차 예언한 죄종 세트의 가중치 보너스 (2 = +200% → ×3), 다음 등반 한 번
        public float[] rankWeights;           // 23차 레어도 가중치: 코스트(등급)별 뽑힐 비중 (rankColors 처럼 코스트가 번호, 0번 미사용). 칸이 없으면 1, 0 이하면 무작위로는 안 나옴
        public CardEntrance cardEntrance;     // 22차-ui-fixes: 레전더리, 신화 카드가 고르기 창에 나타날 때 번쩍임
        public string saveKey;
        public ItemEffectText[] effectTexts;  // 고유 효과 이름·설명
        public float subSetScale;             // (16차부터 쓰지 않음) 예전 보조 세트 배율 — 부가 세트는 SetDef.procCharge 주기 발동으로 바뀜
    }

    [Serializable] public class UpgradeDef
    {
        public string key, name, text;
        public Sprite icon;
        public int price, priceStep, max;     // max < 0 → 판 크기 기준(가로 줄 = 높이 / 2, 세로 줄 = 폭 / 2)
    }

    [Serializable] public class WareDef
    {
        public string id, name, text;
        public Sprite icon;
        public int basePrice;
        public float amount;
        public string potion;                 // 23차: 포션 id 면 사서 포션 칸에 넣는다 (바로 쓰지 않음)
    }

    [Serializable] public class ShopConfig
    {
        public WareDef[] wares;
        public float saleSeconds, saleDiscount;
    }

    [Serializable] public class ElementTone
    {
        public Element element;
        public Color color, glow, core;
        public BindStyle bindStyle;           // 실 리본 모습 (세트가 켜진 실 · 결박 당기기)
        public Sprite hit;                    // 속성 피해가 들어갈 때 적 자리에 터지는 그림 (Codex fx_*), 색으로 물들인다
        public float hitSize;                 // 그 그림 크기 (px)
        public GlyphFont damageFont;          // 이 속성 피해 숫자 그림 글꼴 (tools/bake_element_fonts.py, 흰색 그대로 그린다)
    }

    // 실 리본·결박 폭발의 모습 (Plain = 금실 = 빛)
    // 22차: 칠죄종을 끝에 붙임 (옛 값 0~7 유지, 씬에 저장된 정수가 바뀌지 않게)
    public enum BindStyle { Plain, Fire, Lightning, Frost, Dark, Blood, Wind, Poison, Wrath, Sloth, Pride, Greed, Gluttony, Lust, Envy }

    [Serializable] public class BindLook
    {
        public BindStyle style;
        public Color glow, color, core;       // 바깥 빛 · 몸통 · 가운데 심 (가산)
        [Range(0, 1)] public float cover = 1; // 실 덮개 세기 배율 (밝은 색은 다섯 가닥이 겹치면 하얗게 날아가므로 낮춘다)
        public string text;                // 적 머리 위 글자 (23차: 라틴어 죄종 이름, 예 "IRA". Cinzel 로마 비문체)
        public Color titleFill, titleGlow;    // 23차 그 글자의 금속 면 색 · 뒤 후광 색 (죄종 색)
    }

    // 23차: 결박 당기기 때 적 머리 위에 찍히는 라틴어 죄종 글자 (GameUI.Inscription.cs, ArenaRenderer DrawInscription)
    [Serializable] public class InscriptionConfig
    {
        public FxSpec fx;                     // time = 보이는 시간 (초), lift = 적 점 위로 띄우는 높이 (캔버스 px)
        public float size;                    // 글자 크기 (캔버스 px)
        public float spacing;                 // 자간 (글자 크기 비율)
        public float stamp, stampTime;        // 처음 크기 배율, 찍히는 데 걸리는 때 (0~1, 가속하며 쾅)
        public float spread;                  // 찍히기 전 자간 배율 (벌어졌다 모인다)
        public float bounce, bounceTime;      // 찍힌 뒤 눌렸다 돌아오는 정도, 길이 (0~1)
        public float settle, rise;            // 다 찍힌 뒤 천천히 커지는 배율, 떠오르는 거리 (px)
        public float fadeFrom;                // 이때부터 사라진다 (0~1)
        public float flashTime;               // 찍힌 순간 하얗게 달궈졌다 식는 길이 (0~1)
        public float shake;                   // 찍힌 순간 좌우 떨림 (px)
        public float sheenFrom, sheenTo, sheenWidth, sheenPower;   // 빛이 글자를 훑고 지나가는 구간 (0~1), 폭 (글자 수 비율), 세기
        public float edgePx, glowPx, glowBlur, shadePx;   // 금속 테두리 · 후광 외곽 굵기 · 후광 번짐 · 아래 두께(어두운 단) (캔버스 px)
        public Color edge;                    // 금속 테두리 색 (어두운 쇠)
        [Range(0, 1)] public float shadeDark; // 아래 두께 색 = 면 색 × 이 값
        public float rule, ruleThick;         // 양옆 가로줄 길이 (글자 크기 배율), 굵기 (px)
        [Range(0, 1)] public float hiLift;    // 금속 두 톤: 글자 위쪽 절반을 흰색 쪽으로 이만큼 밝게
        public float aura, auraFlash;         // 뒤 빛무리(soft_disc, 후광 색) 세기, 찍힌 순간 더 세지는 배율
    }

    // 결박 당기기: 같은 적 하나에 실이 strands 가닥 이상 걸린 채 당기면 그 적에게 추가 피해 + 속성 연출.
    [Serializable] public class BindConfig
    {
        public int strands;                   // 같은 적에게 걸린 가닥 수 (0 = 끔)
        public float damageBonus;             // 그 적에게 주는 피해 추가 배율 (1 = +100%)
        public float freezeBonus, shakeBonus, flashBonus, kickBonus;   // 히트스톱·흔들림·번쩍임·반동 추가
        public float strandWidth;             // 속성 실 굵기 배율 (보통 당기기 실 대비)
        public FxSpec burst;                  // 적 자리 폭발 (size = 폭발 반지름 px, time = 길이)
        public int bolts, tongues, spikes;    // 번개 줄기 · 불길 혀 · 얼음 가시 수
        public float particleRate;            // 속성 실을 따라 뿜는 입자 (초당, 점 하나당)
        [Header("리본 (Blender: tools/render_bind_ribbons.py)")]
        public float ribbonWidth;             // 실 몸통 리본 굵기 (캔버스 px)
        public float ribbonFps, flowSpeed;    // 리본 프레임 넘김 속도, 손 쪽으로 흐르는 속도 (타일/초)
        public float trailWidth, trailTime;   // 궤적 꼬리 굵기 (px), 꼬리 길이 = 머리 속도 × trailTime
        public float headSize;                // 머리 광구 지름 (px)
        [Header("실 덮개: 리본 위에 속성 빛·장식을 굵게 덮어 실이 보이지 않게 (0 = 끔)")]
        public float coverWidth;              // 덮개 빛 굵기 (캔버스 px, 실 리본보다 넓게)
        public float coverAlpha;              // 덮개 세기 (0~1)
        public float coverStep;               // 불길 혀·전류·얼음 결정을 놓는 간격 (px)
        [Header("세트가 켜진 실: 평소에도 속성 리본으로 (단계마다 굵어짐)")]
        public float setRibbonWidth;          // 1단계 리본 굵기 (px)
        public float setRibbonGrowth;         // 단계가 하나 오를 때마다 굵기 배율 증가
        public float setCoverScale;           // 덮개 굵기 = 리본 굵기 × 이 값
        [Header("가감속: 반동(뒤로 감김) → 가속 → 손 앞에서 감속")]
        public float pullTime;                // 결박 실이 감겨 드는 시간 (보통 당기기 fx.pullStrand.time 대신)
        public float windup, windupTime;      // 처음 windupTime(0~1) 동안 적 쪽으로 windup(실 길이 비율)만큼 뒤로 감긴다
        public float accelSplit;              // 가속 구간 끝(0~1, 그 뒤는 감속)
        public float arriveAt;                // 머리가 손에 닿는 때 (당기기 진행 0~1). 그 뒤는 팔로우 스루
        [Header("아크 · 스쿼시 앤 스트레치 · 팔로우 스루")]
        public float arcHeight;               // 호가 휘는 높이 (적~손 거리 비율). 가닥마다 fx.side(-1~1)를 곱한다
        public float[] arcSides;              // 같은 적에 걸린 n번째 가닥의 휘는 쪽·정도
        public float stretchAmount, stretchSpeed;   // 머리 속도가 stretchSpeed(px/초)일 때 진행 방향으로 stretchAmount 배
        public float squashAmount;            // 손에 닿는 순간 찌그러짐 (0~1)
        public float followLength, followCurl; // 팔로우 스루 채찍 길이(px)·휘감기는 각도(라디안)
        [Header("끌려옴 강조")]
        public float yankScale, yankTime;     // 적이 끌려오는 거리 배율·시간 (가감속)
        public float stretchPower;            // 끌려올 때 몸이 늘어나는 정도 배율
        public int speedLines;                // 적 둘레 속도선 수
        public BindLook[] looks;
        public InscriptionConfig title;       // 23차 라틴어 죄종 글자
    }

    // 맵 장식 (12차): 상호작용 없는 코스메틱. 라운드마다 새 맵에 흩어 두고, 캐릭터·몬스터·공격 예고보다 항상 뒤(Ground 층 맨 앞 순서)에 그린다.
    [Serializable] public class DecorDef
    {
        public Sprite sprite;
        public float height;                  // 그리는 높이 (캔버스 px)
        public float weight = 1;              // 뽑힐 비중
        public bool flat;                     // 바닥에 납작하게 붙는 데칼 (그림자 없음, 가운데 기준)
    }

    [Serializable] public class DecorConfig
    {
        public DecorDef[] props;
        public float density;                 // 맵 100만 px² 당 개수
        public float spacing;                 // 장식끼리 최소 거리 (전투장 px)
        public float clearRadius;             // 라운드 시작 자리(주인공) 둘레는 비워 둔다
        public float margin;                  // 맵 벽에서 떨어뜨리는 거리
        public Vector2 scaleRange;            // 크기 무작위 배율 (최소, 최대)
        public float bright, sat;             // 캐릭터보다 눈에 덜 띄게 (1 = 그대로)
        public float shadowAlpha;             // 서 있는 장식 아래 그림자 진하기
    }

    // 이펙트 한 종류의 크기·수명 (웹판 effects.push 의 size/time).
    [Serializable] public struct FxSpec
    {
        public float size, time, lift, alpha;
    }

    [Serializable] public class FxConfig
    {
        public FxSpec awaken, frenzy, dodgeText, dodgeShock, hurtNumber, hurtBurst;
        public FxSpec muzzle, puncture, thunk, pullStrand, damageNumber, slash, snip, hitFlash, heroFlash, combo, transfer;
        public FxSpec bossSummon, meleeSlash, dashTrail, dashBurst, dust, dustEnd, killBurst, sliced, absorb, augment, upgrade, pickup;
        public float muzzleForward, slashGrowth, snipGrowth, heroFlashGrowth, bossScale;
        public int maxGrowthHits, maxFlashHits;
        public Color hurtNumberColor, hurtBurstColor, dodgeTextColor, damageColor, transferColor, comboColor, rageComboColor;
        public Color killBurstColor, bossBurstColor, meleeSlashColor, pullFlashColor;
    }

    // 그림 글꼴: 아틀라스(Assets/Art/Generated/damage_font.png)에서 잘라 낸 글자 스프라이트. chars[i] ↔ glyphs[i].
    // 글자는 흰색 + 검은 외곽선이라 색을 곱해 물들인다.
    [Serializable] public class GlyphFont
    {
        public string chars;
        public Sprite[] glyphs;
        public float height = 24;             // 기본 글자 높이 (캔버스 px)
        public float spacing;                 // 글자 사이 (글자 높이 대비, 음수면 겹침)
    }

    [Serializable] public class VisualConfig
    {
        public ElementTone[] tones;
        public Color[] floorPalette;
        public float heroScaleBase, heroScaleNear, heroScaleFar;   // .104 × (.94 + .12 × y/H)
        public float heroSize = 1;            // 주인공 전체 크기 배율 (그림자·회피 통그림 포함)
        [Header("레벨업: 몸에 초록 기운")]
        public float levelUpTime, levelUpFill, levelUpGlow, levelUpRate;
        public float levelUpBeam, levelUpFlash;   // 발밑에서 솟는 빛 기둥 높이(캔버스 px), 처음 번쩍이는 시간
        public int levelUpChevrons;               // 위로 솟는 화살표(▲) 수
        public int levelUpIcons;                  // 몸 둘레 여기저기 튀어나오는 레벨업 아이콘 수
        public float levelUpIconSize, levelUpIconSpread;   // 아이콘 크기(캔버스 px), 몸 둘레 퍼짐(캔버스 px)
        [Header("21차 증강 성장: 금빛 실이 몸을 감아 오르고 아래부터 디졸브 (tools/render_augment_growth.py)")]
        public FlipClip growthBack, growthFront;  // 몸 뒤 / 앞 층 (TOD/Setup/16. Element Art 가 연결, fps 0 = 효과 시간에 한 번)
        public float growthSize;                  // 한 칸 높이 (캔버스 px). 발은 칸 가운데에서 growthFoot × 크기 아래
        public float growthFoot;                  // .3 (Blender 바닥점 y = -1.2 / 4)
        public float growthTextAt;                // 능력치 글자가 나오기 시작하는 때 (효과 시간 0~1)
        public float growthTextLift, growthTextRise, growthTextSize;   // 발에서 글자까지 높이, 떠오르는 거리, 글자 크기 (캔버스 px)
        public Color growthTextColor;
        [Header("피해 숫자 그림 글꼴")]
        public GlyphFont damageFont;
        [Header("치명타 숫자: 노란 그림 글꼴 + 왼쪽 위 뾰족한 아이콘 (세피리아 참고)")]
        public GlyphFont critFont;
        public Sprite critIcon;
        public float critScale;               // 숫자 크기 배율
        public float critIconSize;            // 아이콘 크기 (숫자 높이 대비)
        public Vector2 critIconOffset;        // 숫자 왼쪽 위 모서리에서 아이콘 가운데까지 (숫자 높이 대비, +x 오른쪽 +y 아래)
        public Color levelUpColor;
        public float dodgeScale, dodgeLaunchEnd, dodgeAirEnd, dodgeHop;
        public float dodgeHopLand;            // 24차: 대시 떠오름은 대시 이동 시간(feel.dashDuration) + 이만큼(초) 안에 내려앉는다 (멈춘 뒤 공중에 떠 있지 않게)
        public float strandSegment, frustrationSegment;
        public Color dodgeColor;
        [Header("세트 속성 (12차)")]
        public float elementNumberLift;       // 속성 피해 숫자를 실 피해 숫자 위로 띄우는 높이 (px)
        public float elementHitTime;          // 속성 그림(ElementTone.hit)이 터지는 시간
        public float chainTime;               // 번개 튐 줄기가 남는 시간
        public float dotInterval;             // 화상·중독 숫자를 모아 띄우는 간격 (초)
        public float statusRate;              // 상태 이상 적 둘레 입자 (초당)
    }

    // 전투장 그리기 색 (웹판 render.js 의 Canvas fillStyle/strokeStyle).
    [Serializable] public class RenderPalette
    {
        public Color clear, floorBase, floorTint;
        public Color windupLane, windupDash, windupRing;
        public Color knotRing, echoLine, bullet, bulletSpent, bulletGlow;
        public Color shadow, eliteRing, bossRing, hpBack, hpFill, pinCount;
        public Color aimRing, aimDot, dust, sliceGlow, textStroke;
        public Color lampBase, heroHurt, transferGlow, transferCore;
        public float lampGlowAlpha, lampFlameAlpha, moteAlphaBase, moteAlphaPulse;
        public float heroHurtAmount = .55f;   // 피격 때 붉게 덮는 양 (채도는 그대로)
        [Header("계단·제단·상인")]
        public Color stairsGlow, promptActive, promptIdle;
        public float propLabelPx;
        public float altarHeight, merchantHeight;   // 쉬어가기 탑·상인을 그릴 높이 (캔버스 px, 0 이면 그림 크기)
        public float stairScale, stairRise, stairDrop;   // 계단 조각 크기 배율(그림 px → 캔버스 px), 한 단 높이(캔버스 px), 자리 잡기 전 위에서 내려오는 거리
    }

    // 전투 중 배너·알림 문구. {0}, {1} 자리에 숫자/이름이 들어간다.
    [Serializable] public class MessageConfig
    {
        public string awaken, frenzy;
        public string roundBanner;            // "{0}층  {1}" (게임 글자에 가운뎃점은 쓰지 않는다)
        public string bossRoundName, roundName;   // "층 보스", "{0}라운드"
        public string loot;                   // "{0} 획득"
        public string dodge, emptyYarn, pullEmpty;
        public string pullHits, pullTransfers, pullNone;   // "실 명중 {0}회", ", 피해 전달 {0}체", (23차부터 pullNone 은 화면에 안 씀)
        public string bossSummon, combo;      // "{0} HIT"
        public string spite;                  // 고유 효과 '분풀이' 발동 ("분풀이! 털실 회수")
    }

    // 화면 문구 중 숫자·이름이 바뀌는 것 (고정 문구는 UXML 에 있다).
    [Serializable] public class UiTextConfig
    {
        public string level, ratio, threadCount, knotReady, desireValue, frenzySuffix, awakenSuffix, floorLabel, bestRecord, bestNone;
        public string routeDefault, routeShop, routeCleared, routeBoss, routeTimer, routeRemaining;
        public string summaryClear, summaryDead, summaryAbsorbed, summaryRetreat, summaryBody;
        public string augmentTitle, augmentRule, augmentStat;
        public string desireEyebrow, desirePenalty;
        // 22차 보스 욕망 창 (속삭임 말투). desireTitle {0} = 죄종 이름, desireNumbers {0} = 흡수로 얻는 욕망, {1} = 욕망 이름, {2} = 효과, {3} = 대가
        public string desireTitle, absorbButton, refuseButton, desireNumbers;
        [TextArea] public string absorbFeel, refuseFeel;
        public string saleActive, saleOver, saleSold, price, soldOut, nextFloor;
        public string restEyebrow, restBodyBoss, restBodyNext, restButtonBoss, restButtonNext;
        public string upgradeBody, upgradeCost, upgradeMax, upgradeDone;
        public string inventoryHeader, setLine, setOff, setOn, equipped, invalidPlacement, pieceTooltip;
        public string soundOn, soundOff;
        public string timer, cleared, promptStairs, promptAltar, promptMerchant, augmentHint;
        public string restRoundName, bossRoundLabel, survivalLabel, purified, artifactEyebrow, artifactTaken;
        public string dashValue, expText, expLevel, augmentLevel, pieceEffect, pieceShape;
        public string reroll, rerollUsed, threadBonus, inventoryHint, inventoryInRun;
        public string inventoryOutgame;       // 17차: 아웃게임 재봉 상자 안내 ({0} 판에 놓은 시작 조각 수, {1} 들고 갈 수)
        public string starterSlotsLine;       // 17차: 실궤 '시작 조각 선택' 카드 아랫줄 ({0} 지금 들고 갈 수)
        public string pieceCells, tipSet, setProgress, setProgressMax, ownedAll;   // 재봉 상자 정보 창·세트 패널·상점
        public string setMain, setSub;        // 세트 패널: 메인 속성 / 부가 속성 (16차: setSub 의 {0} = 발동까지 타격 수)
        public string setListCount, setListNeed, setMainTag, setSubTag, setTierLine;   // 22차 세트 목록 "(3)" / 못 미친 "(1/2)" / 메인, 부가 꼬리표 / 창의 단계 숫자 "({0})"
        public string[] statNames;            // StatKind 순서
        public string[] roundNames;           // 첫 번째 라운드, 두 번째 라운드, 층 보스
        public string hudFloorRound, hudFloorRest, hudFloorBoss;   // 23차 타이머 오른쪽 "{0}층 {1}라운드" ({0} 층, {1} 라운드 번호). 쉬어가기, 보스는 따로(비면 hudFloorRound)
    }

    [Serializable] public class SynthSound
    {
        public string id;
        public float from, to, duration, volume;
        public WaveShape wave;
        public AudioClip sample;               // 실제 샘플이 생기면 여기에 넣으면 합성음 대신 재생
    }

    public class GameDatabase : MonoBehaviour
    {
        public ArenaConfig arena;
        public MapConfig map;
        public RoundConfig rounds;
        public ConceptDef[] concepts;
        public SpawnConfig spawn;
        public HeroConfig hero;
        public NeedleConfig needle;
        public FeelConfig feel;
        public EnemyConfig enemies;
        public RewardConfig rewards;
        public AugmentConfig augments;
        public DesireConfig desire;
        public GearConfig gear;
        public UpgradeDef[] upgrades;
        public ShopConfig shop;
        public FxConfig fx;
        public PurifyConfig purify;
        public BossDeathConfig bossDeath;
        public BindConfig bind;
        public NeedleVisual needleVisual;
        public VisualConfig visual;
        public RenderPalette palette;
        public DecorConfig decor;
        public ElementConfig elements;        // 16차: 층 속성 · 상성 · 속성 몬스터 · 부가 발동 · 보스 속성 공격 (ElementData.cs)
        public DiaryConfig diary;             // 15차: 아버지의 일기 (DiaryData.cs)
        public HubConfig hub;                 // 15차: 재봉실 허브 (HubData.cs)
        public MerchantConfig merchant;       // 16차: 상인 단추 자세와 말풍선 (MerchantData.cs)
        public MessageConfig messages;
        public UiTextConfig ui;
        public SynthSound[] sounds;
        public PotionConfig potions;          // 23차: 포션 칸 (PotionData.cs)
        public BossConfig bosses;             // 23차-boss7: 칠죄종 보스 7종, 패턴, 2페이즈 (BossData.cs)
        public NpcConfig npcs;                // 23차: 쉬어가기 NPC 4종, 재봉실 NPC (NpcData.cs)
        public SettingsConfig settings;       // 24차: 환경설정 기본값과 기본 키 (SettingsData.cs)

        public EnemyType Enemy(string id) => Array.Find(enemies.types, t => t.id == id);
        public AugmentDef Augment(string id) => Array.Find(augments.augments, a => a.id == id);
        public SetDef Set(string id) => Array.Find(gear.sets, s => s.id == id);

        public BindLook Look(BindStyle style) =>
            bind.looks == null ? null : Array.Find(bind.looks, l => l.style == style) ?? Array.Find(bind.looks, l => l.style == BindStyle.Plain);

        public ElementTone Tone(Element element)
        {
            ElementTone tone = Array.Find(visual.tones, t => t.element == element);
            return tone ?? Array.Find(visual.tones, t => t.element == Element.Plain);
        }
    }
}
