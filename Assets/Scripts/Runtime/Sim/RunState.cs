// 한 번의 등반 상태 (웹판 createRun 이 만드는 객체). 좌표는 전투장 논리 좌표.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    // Combat: 생존·보스 전투 중 / Purifying: 보스 처치 후 정화 연출 / Cleared: 라운드 종료(계단·제단·상인과 상호작용) / Victory
    public enum Phase { Combat, Purifying, Cleared, Victory, BossDying }
    public enum Interactable { None, Stairs, Altar, Merchant, Npc }   // 23차 Npc = 쉬어가기 NPC
    public enum Outcome { None, Dead, Absorbed, Clear, Retreat }
    public enum Mood { Normal, Frenzy, Awakened }
    public enum HeroAction { Idle, Run, Shoot, Pull, Dash, Hurt, Die }
    public enum EnemyAction { Move, WindupMelee, WindupShot, WindupDash, Charge, Rest }
    public enum StrandState { Flight, Stuck, Ground }

    public class Point { public float x, y; public Point() { } public Point(float x, float y) { this.x = x; this.y = y; } }

    public class Knot : Point { public int index; }

    public class Timed { public float x, y, time, max, done, power = 1; public bool smooth; }   // smooth: 가감속(천천히 → 빠르게 → 천천히), power: 늘어남 배율

    public class PoseSnapshot { public HeroAction action; public float actionTime, strideTime, angle, motion; }

    public class Player : Point
    {
        public float facing = 1, vx, vy, dash, dashGrace, dashCooldown, ghostIn, hurt, shotCooldown, angle, motion, strideTime, actionTime;
        public HeroAction action = HeroAction.Idle;
        public PoseSnapshot previousPose;
        public Timed recoil;
        public float dx, dy, dashDone, dashFacing = 1;
        public int dashCharges;              // 남은 대쉬 칸
        public float levelUp;                // 레벨업 초록 기운 남은 시간
        public float heal;                   // 하트를 먹고 몸에 피가 차오르는 연출 남은 시간
        public float dashCharge;             // 다음 칸이 차오른 정도 (0~1)
        public Point dashFrom;
        public bool dodged;
        public float sinceDash = 99;         // 마지막 대쉬 끝난 뒤 지난 시간 (고유 효과 '도약 일격')
        public bool leapUsed;                // 그 대쉬 뒤 첫 당기기에 이미 썼는지
        public bool spiteReady;              // 고유 효과 '분풀이': 다음 당기기 강화 대기
        // 16차 보스 속성 공격이 건 상태: 화상·중독 = 초당 피해, 둔화 = 이동 감소 비율, 감전 = 남은 번개 수, 눈부심·어둠 = 남은 시간
        public float burnDps, burnTime, slow, slowTime, poisonDps, poisonTime, dotTick, dotPending;
        public int poisonStacks, shockLeft;
        public float shockDamage, shockInterval, shockTimer;
        public float blind, blindMax, blindPower, dark, darkMax, darkRadius;
        public Timed push;                   // 바람: 밀쳐짐 (전투장 px)
        public void ClearStatus()
        {
            burnDps = burnTime = slow = slowTime = poisonDps = poisonTime = dotTick = dotPending = 0;
            poisonStacks = shockLeft = 0; shockDamage = shockInterval = shockTimer = 0;
            blind = blindMax = blindPower = dark = darkMax = darkRadius = 0; push = null;
        }
    }

    public class Cut { public float angle, time, max, side; }

    public class Enemy : Point
    {
        public EnemyType type;
        public bool elite;
        public float hp, maxHp, radius, wait, speed, damage, timer, facing = 1, flash, height;
        public int threshold;
        public EnemyAction action;
        public Vector2 aim = Vector2.right;
        public Timed stretch, jolt, push, yank;
        public Cut cut;
        // 속성 상태 (세트 부가 효과): 화상·중독 = 초당 피해, 둔화 = 이동 감소 비율, 저주 = 받는 피해 증가 비율
        public float burnDps, burnTime, slow, slowTime, curse, curseTime, poisonDps, poisonTime, dotTick, dotPending;
        public int poisonStacks;
        // 22차 칠죄종: 나태 겹(다 차면 잠듦) · 잠 남은 시간, 색욕 매혹 남은 시간(공격 안 함), 탐욕 저당(저주가 탐욕에서 왔는지 = 처치 때 금화)
        public int slothStacks;
        public float sleepTime, charmTime;
        public bool pawned;
        public Element element;              // 16차: 층 속성 (그 층 몬스터는 모두 같은 속성)
        public bool IsBoss => type.id == "boss";
        public BossBrain brain;              // 23차-boss7: 칠죄종 보스의 패턴, 2페이즈 (CombatBoss.cs). 공용 보스, 일반 몬스터는 null
        public bool killed;                  // 처치 처리를 이미 했는지 (풀에서 꺼낼 때 ClearStatus 가 지운다)
        public void ClearStatus() { burnDps = burnTime = slow = slowTime = curse = curseTime = poisonDps = poisonTime = dotTick = dotPending = sleepTime = charmTime = 0; poisonStacks = slothStacks = 0; pawned = false; killed = false; }
    }

    public class Strand : Point
    {
        public float vx, vy, speed, range, maxRange, birth, landed;
        public Point anchor;
        public Enemy target;
        public StrandState state;
        public bool bounce;
        public bool extra;                    // 여러 갈래 바늘(Multishot)로 덤으로 나간 바늘 — 털실을 쓰지 않았으므로 당길 때 돌려받지 않는다
        public Element? element;              // 대쉬 흔적이 만든 실은 속성이 없다 → 등반의 속성을 쓴다
        public Vector2 bite;
        public Strand Copy() => (Strand)MemberwiseClone();
    }

    // 보스가 실에 묶여 종이처럼 잘리는 연출 상태. 뷰가 time 으로 단계를 나눠 그린다.
    public class BossDeath : Point { public EnemyType type; public bool elite; public float facing = 1, time, height, cutAngle; }

    public class Bullet : Point { public float vx, vy, hit, age; public bool spent; public Enemy boss; public Element element; }   // boss = 보스가 쏜 속성 탄

    // 처치 때 튀어나오는 보상. 잠깐 튀어 오른 뒤(age < popTime) 몸으로 빨려 든다.
    // 23차: shower = 보스 처치 기억의 파편 (위에서 떨어져 hold 초 머문 뒤 빨려 든다)
    public class Drop : Point { public int xp, gold, shard; public bool item, shower; public string potion; public float heal, vx, vy, age, speed, hold; }

    public class Trail { public Point a, b; public float time; public HashSet<Enemy> used = new(); }

    public class Echo { public float until; public List<Strand> strands; }

    // ElementHit = 속성 피해 그림(ElementTone.hit), Chain = 번개가 옆 적에게 튀는 줄기 (x,y → toX,toY)
    public enum FxKind { Sprite, Flip, Number, Combo, Burst, Slash, Dust, Pull, Cut, Sliced, Transfer, Afterimage, Bind, ElementHit, Chain, Proc, BossHit, Growth, Sew }   // Proc = 부가 세트 발동, BossHit = 보스 속성 공격이 주인공에 맞음, Growth = 21차 증강 성장 (주인공을 따라감, text = 오른 능력치), Sew = 24차 바느질 타격 파티클 (pop = 타수, bound = 결박)
    public enum FxSprite { None, Pull, Dash, Boss, Needle, Stitch, Streak, Launch, Dodge, Shock }
    public enum FlipSheet { Puncture, Slash, Snip, Flash, Muzzle, Thunk }

    // 화면 효과 한 개. 시뮬레이션은 수명만 줄이고, 그리기는 뷰가 맡는다.
    public class Fx
    {
        public FxKind kind;
        public float x, y, toX, toY, time, max, size, angle = float.NaN, lift = float.NaN, alpha = 1, seed, progress, facing = 1, side = 1;
        public FxSprite sprite;
        public FlipSheet sheet;
        public Element element;
        public Color? color;                  // tint 를 색으로 지정할 때
        public bool squash, mirror, burst, rage, elite, useElement, followHero;
        public bool bound;                    // 결박 당기기의 실 (속성 모습으로 그린다)
        public bool crit;                     // 치명타 피해 숫자 (치명타 글꼴 + 아이콘)
        public Point follow;                  // 이 점을 따라 그린다 (결박 폭발 → 끌려가는 적)
        public string text;
        public int pop;
        public EnemyType type;
        public float Fade => max > 0 ? time / max : 0;
    }

    public struct GameEvent { public string type, message; }

    public class RunState
    {
        public Profile profile;   // 영구 저장 (파편, 최고 층, 일기)
        public Profile bag;       // 17차 등반 가방: 시작 조각 + 이번 등반에서 얻은 조각과 배치 (Gear.RunBag, 귀환하면 버림)
        public Func<float> random;
        public GearStats gearStats;
        public int floor = 1, round = 1, level = 1, xp, gold, kills, spawned, shotCount, nextKnot, augmentQueue, combo;
        public int feastStacks;              // 22차 고유 효과 만찬: 지금 겹 수 (feastTime 이 끝나면 0)
        public float feastTime;
        public float time, elapsed, desire, maxDesire, hp, maxHp, yarn, yarnBonus, recharge, damage, dashRate = 1, encumbrance;
        // 바늘·이동 능력치 (증강으로 오른다)
        public float fireInterval, reach, needleSpeed = 1, moveScale = 1;
        // 라운드
        public int roundIndex;
        public RoundKind roundKind;
        public ConceptDef[] floorConcepts = new ConceptDef[0];
        public ConceptDef concept;
        public float roundLength;
        public readonly Dictionary<SpawnEntry, float> spawnTimers = new();
        public Point stairs, altar, merchant;
        // 23차 쉬어가기 NPC (CombatNpc.cs): 자리(없으면 null), 누구, 단계(0 아직, 1 도박 제안 중, 2 받음), 이번 라운드의 말과 그 시각
        public Point npc;
        public NpcKind npcKind;
        public int npcStage;
        public bool npcGreeted;
        public float stairsAge;
        public float altarAge;               // 탑이 나타난 뒤 지난 시간 (등장 연출)
        public List<int> artifactOffer = new();
        public bool artifactTaken, desirePending;
        public Point purifyCenter;
        public float purifyTime = -1;
        public bool purified;
        public readonly Stack<Enemy> enemyPool = new();
        public float winding, dry, freeze, shake, flash, snapFlash, hurtFlash, deathClock;
        // 24차 프레임 스톱: freeze 동안 이펙트, 주인공 자세까지 멈춘다 (frameLead 초는 먼저 흐름). 바늘 박힘 같은 짧은 멈춤은 false
        public bool frameStop;
        public float frameLead;
        public bool FrameHeld => frameStop && freeze > 0 && frameLead <= 0;
        public Color flashTint;
        public Element element;
        public Mood mood;
        public Player player = new();
        public List<Knot> knots = new();
        public List<Enemy> enemies = new();
        public List<Strand> strands = new();
        public List<Bullet> bullets = new();
        public List<BossTelegraph> telegraphs = new();   // 23차-boss7: 보스 패턴 바닥 예고
        public List<Drop> drops = new();
        public PotionSlot[] potions = new PotionSlot[0];   // 23차 포션 칸 (Potions.NewSlots)
        public float potionReady;            // 이 time 이 지나야 다음 포션을 마신다
        public List<Trail> trails = new();
        public List<Fx> effects = new();
        public HashSet<string> rules = new(), desires = new();
        public bool devGod, devFreeze;        // 개발자 모드: 무적 · 적 멈춤
        public Dictionary<string, int> augmentsTaken = new();
        public bool artifactRerolled;        // 쉬어가기 탑: 층마다 한 번 다시 굴리기
        public BossDeath bossDeath;          // 보스 처치 연출 (BossDying 단계)   // 증강 id → 고른 횟수 (카드에 Lv 로 표시)
        public Timed kick;
        public Timed zoomPunch;              // 24차 줌 펀치: x = 세기(직교 크기 줄이는 비율), time = 남은 시간, max = 다가감 + 돌아옴, done = 다가감 시간
        public Echo echo;
        public Outcome outcome;
        public Phase phase = Phase.Combat;
        public DesireDef offer;
        // 16차 층 속성 · 부가 세트 충전
        public Element floorElement;
        public readonly List<Element> floorElements = new();   // 이번 등반에서 이미 나온 층 속성 (겹치지 않게)
        public int fate;                                        // 24차 예언한 죄종 (Element + 1, 0 = 없음): 이번 등반 동안 그 세트 조각 가중치가 오른다
        // 24차 층 날씨: 탐욕의 소수 몫, 식탐 허기 모음
        public float xpCarry, goldCarry, hungerPending, hungerTick;
        public int hintSerial;               // 층 암시 문구를 다시 띄울 때마다 +1 (라운드 시작 · 개발자 모드)
        public readonly Dictionary<SetDef, int> procCharges = new();   // 부가 세트 → 충전 수 (procCharge 이상 = 준비)
        public readonly List<GameEvent> events = new();

        public float Gear(StatKind kind) => gearStats[kind];
    }

    public struct CombatInput
    {
        public float x, y;
        public bool shoot, dash, pull;
        public Vector2 aim;                   // 전투장 좌표
    }
}
