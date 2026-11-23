// 게임 데이터 컨테이너. 값은 코드에 두지 않고 씬의 GameDatabase 오브젝트에 MCP(update_component)로 넣는다.
// 필드 이름과 단위는 웹판(web/src/game/*.js)의 상수를 그대로 따른다. 거리는 전투장 px, 시간은 초.
using System;
using UnityEngine;

namespace TOD
{
    public enum Element { Plain, Chaos, Despair, Frustration, Dodge }
    public enum StatKind { Damage, Speed, Health, Recharge, Purity, Yarn }
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
        public float merchantChance;
        public int artifactChoices;
        public float stairsRevealTime;       // 계단 등장 연출 시간
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
    }

    [Serializable] public class HeroConfig
    {
        public float baseHp, baseDamage;
        public float strandBonus, strandBonusMax;   // 당길 때 실 한 가닥이 더 걸릴 때마다 피해 +strandBonus (최대 strandBonusMax)
        public int baseYarn, minYarn;
        public float moveSpeed, encumbrance, minSpeedFactor, frenzySpeed, initialAngle;
        public float shotInterval, aimDeadZone;
        public float rechargeSeconds, idleRechargeMultiplier, frenzyRechargeMultiplier, idleMotionThreshold;
        public int knotInterval, earlyKnotInterval;
        public float hurtTime, hurtFlash, dryTime, hurtShake;
        public float strideBase, strideFactor, motionLerp;
        public float shootHold, pullHold, hurtHold, dashHold;
        public float pickupRadius, magnetRadius, magnetSpeed, bulletHitRadius;
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
        public float shakeBase, shakePerHit, shakeMax, shakeEmpty;
        public float kickBase, kickPerHit, kickMax, kickTime;
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
        public Color healColor;
        [Header("떨어지는 경험치 구·골드")]
        public int maxXpOrbs, maxCoins;       // 처치 한 번에 나오는 최대 개수 (값은 나눠 담는다)
        public float dropPopSpeed, dropPopTime, dropHop;          // 튀어나오는 속도·시간·높이(캔버스 px)
        public float dropVacuumSpeed, dropVacuumAccel;            // 그 뒤 몸으로 빨려 드는 속도·가속
        public float orbSize, coinSize, dropGlow;                 // 그리기 크기(캔버스 px), 빛 배율
        public Color xpColor, goldColor;                          // 흡수 반짝임 색
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
    }

    [Serializable] public class SetDef
    {
        public string id, name, bonus;
        public Color color;
        public int[] thresholds;
        public StatKind stat;
        public float perTier;
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
    }

    [Serializable] public class GearConfig
    {
        public SetDef[] sets;
        public PieceDef[] pieces;
        public string[] rankNames;            // 코스트 1~4 → 일반·레어·유니크·레전더리
        public int baseColumns, baseRows, baseCostLimit, costLimitStep;
        public float rowBonus;
        public int[] startingKinds;
        public Color[] rankColors;            // 코스트 1~4 → 일반 회색·레어 하늘색·유니크 노란색·레전더리 붉은색 (0번 미사용)
        public float[] rankShine;             // 등급별 번쩍임 세기 (0 = 없음)
        public string saveKey;
    }

    [Serializable] public class UpgradeDef
    {
        public string key, name, text;
        public Sprite icon;
        public int price, priceStep, max;     // max < 0 → 보드 높이 기준(가로 줄 강화)
    }

    [Serializable] public class WareDef
    {
        public string id, name, text;
        public Sprite icon;
        public int basePrice;
        public float amount;
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
        [Header("피해 숫자 그림 글꼴")]
        public GlyphFont damageFont;
        public Color levelUpColor;
        public float dodgeScale, dodgeLaunchEnd, dodgeAirEnd, dodgeHop;
        public float strandSegment, frustrationSegment;
        public Color dodgeColor;
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
    }

    // 전투 중 배너·알림 문구. {0}, {1} 자리에 숫자/이름이 들어간다.
    [Serializable] public class MessageConfig
    {
        public string awaken, frenzy;
        public string roundBanner;            // "{0}층 · {1}"
        public string bossRoundName, roundName;   // "층 보스", "{0}라운드"
        public string loot;                   // "{0} 획득"
        public string dodge, emptyYarn, pullEmpty;
        public string pullHits, pullTransfers, pullNone;   // "실 명중 {0}회", " · 피해 전달 {0}체", "실을 거뒀습니다"
        public string bossSummon, combo;      // "{0} HIT"
    }

    // 화면 문구 중 숫자·이름이 바뀌는 것 (고정 문구는 UXML 에 있다).
    [Serializable] public class UiTextConfig
    {
        public string level, ratio, threadCount, knotReady, desireValue, frenzySuffix, awakenSuffix, floorLabel, bestRecord, bestNone;
        public string routeDefault, routeShop, routeCleared, routeBoss, routeTimer, routeRemaining;
        public string summaryClear, summaryDead, summaryAbsorbed, summaryRetreat, summaryBody;
        public string augmentTitle, augmentRule, augmentStat;
        public string desireEyebrow, desirePenalty;
        public string saleActive, saleOver, saleSold, price, soldOut, nextFloor;
        public string restEyebrow, restBodyBoss, restBodyNext, restButtonBoss, restButtonNext;
        public string upgradeBody, upgradeCost, upgradeMax, upgradeDone;
        public string inventoryHeader, setLine, setOff, setOn, equipped, invalidPlacement, pieceTooltip;
        public string soundOn, soundOff;
        public string timer, cleared, promptStairs, promptAltar, promptMerchant, augmentHint;
        public string restRoundName, bossRoundLabel, survivalLabel, purified, artifactEyebrow, artifactTaken;
        public string dashValue, expText, expLevel, augmentLevel, pieceEffect, pieceShape;
        public string reroll, rerollUsed, threadBonus, inventoryHint, inventoryInRun;
        public string[] statNames;            // StatKind 순서
        public string[] roundNames;           // 첫 번째 라운드, 두 번째 라운드, 층 보스
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
        public NeedleVisual needleVisual;
        public VisualConfig visual;
        public RenderPalette palette;
        public MessageConfig messages;
        public UiTextConfig ui;
        public SynthSound[] sounds;

        public EnemyType Enemy(string id) => Array.Find(enemies.types, t => t.id == id);
        public AugmentDef Augment(string id) => Array.Find(augments.augments, a => a.id == id);
        public SetDef Set(string id) => Array.Find(gear.sets, s => s.id == id);

        public ElementTone Tone(Element element)
        {
            ElementTone tone = Array.Find(visual.tones, t => t.element == element);
            return tone ?? Array.Find(visual.tones, t => t.element == Element.Plain);
        }
    }
}
