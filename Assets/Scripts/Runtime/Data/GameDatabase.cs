// 게임 데이터 컨테이너. 값은 코드에 두지 않고 씬의 GameDatabase 오브젝트에 MCP(update_component)로 넣는다.
// 필드 이름과 단위는 웹판(web/src/game/*.js)의 상수를 그대로 따른다. 거리는 전투장 px, 시간은 초.
using System;
using UnityEngine;

namespace TOD
{
    public enum Element { Plain, Chaos, Despair, Frustration, Dodge }
    public enum StatKind { Damage, Speed, Health, Recharge, Purity, Yarn }
    public enum WaveShape { Sine, Triangle, Sawtooth, Square }

    [Serializable] public class ArenaConfig
    {
        public float width, height;          // 전투장 논리 크기 (1000×700)
        public float projectionY;            // 바닥 세로 압축 (.67)
        public float attackY;                // 공격 판정 세로 비율 (.74)
        public float screenTop;              // 캔버스에서 바닥이 시작하는 y (83)
        public float canvasHeight;           // 웹 캔버스 높이 (590)
        public float pixelsPerUnit;          // 캔버스 px → Unity 단위
        public float wallLeft, wallRight, wallTop, wallBottom;
        public float enemyMarginX, enemyMarginY;
        public int finalFloor;
        public float[] roundDurations;       // 인덱스 = 라운드 (0은 미사용, 보스 라운드는 0)
        public Vector2 bossSpawn;
    }

    [Serializable] public class SpawnConfig
    {
        public float startDelay;
        public int maxEnemies;
        public float avoidPlayerRadius;
        public float edgeInset, edgeInsetY, sideSpan, topSpan;
        public string firstRoundType;          // 1라운드 초반 고정 유형 (stalker)
        public string[] rotation;              // 이후 (spawned + floor) % n 순환
        public int firstRoundStalkers;
        public int eliteEveryFirstRound, eliteEveryLater;
        public float intervalFirstRound, intervalLater, intervalFloorStep, minInterval;
    }

    [Serializable] public class HeroConfig
    {
        public float baseHp, baseDamage;
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
        [Header("Dash")] public float dashDuration, dashGrace, dashDistance, dashEase, dashSlow, dashGhostInterval, dashGhostLife, dashCooldown, dashMinCooldown;
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
        public int xpBase, xpStep;
    }

    [Serializable] public class AugmentDef
    {
        public string id, name;
        [TextArea] public string text;
        public bool rule;
        public Sprite icon;
    }

    [Serializable] public class AugmentConfig
    {
        public AugmentDef[] augments;
        public int choices, ruleWeight, statWeight;
        public float fireRate, damage, supplyYarn, supplyRecharge, lightEncumbrance, lightDash, calm, echoWindow;
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
    }

    [Serializable] public class GearConfig
    {
        public SetDef[] sets;
        public PieceDef[] pieces;
        public string[] rankNames;            // 코스트 1~4 → 일반·레어·유니크·레전더리
        public int baseColumns, baseRows, baseCostLimit, costLimitStep;
        public float rowBonus;
        public int[] startingKinds;
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
        public FxSpec bossSummon, meleeSlash, dashTrail, dashBurst, dust, dustEnd, killBurst, sliced, absorb, augment, upgrade;
        public float muzzleForward, slashGrowth, snipGrowth, heroFlashGrowth, bossScale;
        public int maxGrowthHits, maxFlashHits;
        public Color hurtNumberColor, hurtBurstColor, dodgeTextColor, damageColor, transferColor, comboColor, rageComboColor;
        public Color killBurstColor, bossBurstColor, meleeSlashColor, pullFlashColor;
    }

    [Serializable] public class VisualConfig
    {
        public ElementTone[] tones;
        public Color[] floorPalette;
        public float heroScaleBase, heroScaleNear, heroScaleFar;   // .104 × (.94 + .12 × y/H)
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
