// 한 번의 등반 상태 (웹판 createRun 이 만드는 객체). 좌표는 전투장 논리 좌표.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    // Combat: 생존·보스 전투 중 / Purifying: 보스 처치 후 정화 연출 / Cleared: 라운드 종료(계단·제단·상인과 상호작용) / Victory
    public enum Phase { Combat, Purifying, Cleared, Victory, BossDying }
    public enum Interactable { None, Stairs, Altar, Merchant }
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
        public bool IsBoss => type.id == "boss";
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

    public class Bullet : Point { public float vx, vy, hit, age; public bool spent; }

    // 처치 때 튀어나오는 보상. 잠깐 튀어 오른 뒤(age < popTime) 몸으로 빨려 든다.
    public class Drop : Point { public int xp, gold, shard; public bool item; public float heal, vx, vy, age, speed; }

    public class Trail { public Point a, b; public float time; public HashSet<Enemy> used = new(); }

    public class Echo { public float until; public List<Strand> strands; }

    public enum FxKind { Sprite, Flip, Number, Combo, Burst, Slash, Dust, Pull, Cut, Sliced, Transfer, Afterimage, Bind }
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
        public Point follow;                  // 이 점을 따라 그린다 (결박 폭발 → 끌려가는 적)
        public string text;
        public int pop;
        public EnemyType type;
        public float Fade => max > 0 ? time / max : 0;
    }

    public struct GameEvent { public string type, message; }

    public class RunState
    {
        public Profile profile;
        public Func<float> random;
        public GearStats gearStats;
        public int floor = 1, round = 1, level = 1, xp, gold, kills, spawned, shotCount, nextKnot, augmentQueue, combo;
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
        public float stairsAge;
        public List<int> artifactOffer = new();
        public bool artifactTaken, desirePending;
        public Point purifyCenter;
        public float purifyTime = -1;
        public bool purified;
        public readonly Stack<Enemy> enemyPool = new();
        public float winding, dry, freeze, shake, flash, snapFlash, hurtFlash, deathClock;
        public Color flashTint;
        public Element element;
        public Mood mood;
        public Player player = new();
        public List<Knot> knots = new();
        public List<Enemy> enemies = new();
        public List<Strand> strands = new();
        public List<Bullet> bullets = new();
        public List<Drop> drops = new();
        public List<Trail> trails = new();
        public List<Fx> effects = new();
        public HashSet<string> rules = new(), desires = new();
        public Dictionary<string, int> augmentsTaken = new();
        public bool artifactRerolled;        // 쉬어가기 탑: 층마다 한 번 다시 굴리기
        public BossDeath bossDeath;          // 보스 처치 연출 (BossDying 단계)   // 증강 id → 고른 횟수 (카드에 Lv 로 표시)
        public Timed kick;
        public Echo echo;
        public Outcome outcome;
        public Phase phase = Phase.Combat;
        public DesireDef offer;
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
