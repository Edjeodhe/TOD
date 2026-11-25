// 23차-boss7: 칠죄종 보스 7종 (partial Combat). 데이터는 BossData.cs (db.bosses), 그리기는 ArenaRenderer.Boss.cs.
// - 보스가 나오면 층 죄종의 BossDef 로 몸집, 체력, 피해, 패턴을 정한다 (BossDef 가 없으면 예전 공용 보스 행동).
// - 패턴은 바닥 예고(BossTelegraph: 원, 고리, 부채꼴, 직선)를 깔고 windup 초 뒤에 터진다. 대쉬나 예고 밖으로 피하면 안 맞는다.
// - 1페이즈 체력이 0이 되면 죽지 않고 경직 → 포효(주변 넉백, 충격파) → 체력바가 다시 차오르며 2페이즈 (moves2). 전환 중 무적.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOD
{
    public enum TeleShape { Circle, Ring, Cone, Line }

    // 바닥 예고 한 개. time 이 0 이 되면 터지고(resolved), flash 동안 번쩍인 뒤 사라진다.
    public class BossTelegraph
    {
        public TeleShape shape;
        public float x, y, angle, radius, inner, width;   // angle = 라디안 (전투장 좌표), Cone 의 width = 각도(라디안)
        public float time, max, flash, damage, knock;     // damage = 실제 피해 (0 = 표시만)
        public float delay;                                // 이 시간이 지나야 보이기 시작 (Rain 엇갈림)
        public bool resolved;
        public Enemy owner;
        public Color color;
        public float Progress => max > 0 ? Mathf.Clamp01(1 - time / max) : 1;
    }

    // 보스 하나의 머리 (Enemy.brain)
    public class BossBrain
    {
        public BossDef def;
        public int phase = 1;
        public float transition = -1;          // 2페이즈 전환 경과 시간 (−1 = 전환 중 아님)
        public bool roared;
        public float hpTarget;                 // 2페이즈 체력 (전환 중 여기까지 차오름)
        public BossMove move;                  // 지금 쓰는 패턴 (null = 걸어 다님)
        public int last = -1, step;
        public float timer, cooldown, gap;   // gap = 반복 사이 숨 고르기
        public float chargeTime;               // 돌진 남은 시간
        public bool chargeHit;                 // 이번 돌진에서 이미 맞혔는지
        public Vector2 leapFrom, leapTo;       // 뛰어오르기 출발, 착지
        public float leapTime, leapMax, lift;  // lift = 그리기용 높이 (px)
        public float baseSpeed;
        public bool Shielded => transition >= 0;
        public BossMove[] Moves => phase >= 2 && def.moves2 != null && def.moves2.Length > 0 ? def.moves2 : def.moves;
        public float Tempo => phase >= 2 && def.phase2Tempo > 0 ? def.phase2Tempo : 1;
    }

    public partial class Combat
    {
        BossConfig BC => db.bosses;

        public BossDef BossDefFor(Element element) => BC?.Def(element);

        // SpawnEnemy 끝에서: 보스면 층 죄종의 보스로 만든다
        void SetupBoss(RunState r, Enemy e)
        {
            e.brain = null;
            if (!e.IsBoss) return;
            BossDef def = BossDefFor(r.floorElement);
            if (def == null || def.moves == null || def.moves.Length == 0) return;
            float floor = r.floor - 1;
            ConceptDef c = r.concept;
            float hp = Round(def.hp * (1 + E.hpPerFloor * floor) * (c?.hpScale ?? 1));
            e.hp = e.maxHp = Mathf.Max(1, hp);
            if (def.damage > 0) e.damage = def.damage * (1 + E.damagePerFloor * floor);
            if (def.speed > 0) e.speed = def.speed * (1 + E.speedPerFloor * floor) * (c?.speedScale ?? 1);
            if (def.radius > 0) e.radius = def.radius;
            e.brain = new BossBrain { def = def, cooldown = def.idle * .5f, baseSpeed = e.speed };
            e.height = EnemyHeight(e);
        }

        float BossHeight(Enemy e) => e.brain != null && e.brain.def.height > 0 ? e.brain.def.height : e.type.height;

        // Kill 맨 앞에서: 1페이즈 보스는 죽지 않고 2페이즈 전환을 시작한다 (true = 처치하지 않음)
        bool BossPhaseGate(RunState r, Enemy e)
        {
            BossBrain b = e.brain;
            if (b == null) return false;
            if (b.Shielded) { e.hp = Mathf.Max(e.hp, .01f); return true; }
            if (b.phase >= 2 || b.def.moves2 == null || b.def.moves2.Length == 0) return false;
            b.transition = 0;
            b.roared = false;
            b.move = null;
            b.chargeTime = 0;
            b.lift = 0; b.leapTime = 0;
            b.hpTarget = Mathf.Max(1, Round(e.maxHp * (b.def.phase2Hp > 0 ? b.def.phase2Hp : 1)));
            e.maxHp = b.hpTarget;
            e.hp = .01f;   // 0 이면 Retire 된다: 체력바는 0 으로 보이게 아주 조금만
            e.action = EnemyAction.Rest;
            e.timer = 99;
            e.yank = e.push = null;
            e.sleepTime = 0;
            r.telegraphs.RemoveAll(t => t.owner == e);
            r.bullets.RemoveAll(bl => bl.boss == e);
            r.freeze = Mathf.Max(r.freeze, .12f);
            r.shake = Mathf.Max(r.shake, F.shakeBase);
            Event(r, "bossstagger");
            return true;
        }

        // UpdateEnemy 에서 (잠보다 먼저): 전환 중이면 true (다른 행동 없음)
        bool UpdateBossTransition(RunState r, Enemy e, float dt)
        {
            BossBrain b = e.brain;
            if (b == null || !b.Shielded) return false;
            BossConfig c = BC;
            b.transition += dt;
            float t = b.transition;
            e.sleepTime = 0;
            if (t < c.staggerTime)
            {
                // 경직: 몸이 떨리고 깜빡인다
                e.action = EnemyAction.Rest;
                e.flash = Mathf.Max(e.flash, .08f);
                float side = Mathf.Sin(t * 70) * c.staggerJolt;
                e.jolt = new Timed { x = side, y = 0, time = .12f, max = .12f };
                return true;
            }
            if (!b.roared)
            {
                b.roared = true;
                Roar(r, e);
            }
            // 포효 자세, 체력바가 다시 차오른다
            e.action = t < c.staggerTime + c.roarTime ? EnemyAction.WindupShot : EnemyAction.Rest;
            float k = Mathf.Clamp01((t - c.staggerTime) / Mathf.Max(.05f, c.refillTime));
            e.hp = Mathf.Max(.01f, b.hpTarget * (k * k * (3 - 2 * k)));
            if (t >= c.staggerTime + Mathf.Max(c.roarTime, c.refillTime))
            {
                b.transition = -1;
                b.phase = 2;
                e.hp = e.maxHp = b.hpTarget;
                e.action = EnemyAction.Move;
                e.speed = b.baseSpeed * (b.def.phase2Speed > 0 ? b.def.phase2Speed : 1);
                b.cooldown = b.def.idle * .4f;
                b.last = -1;
                e.threshold = 0;   // 2페이즈에도 체력 문턱 졸개
                if (b.def.phase2Summons != null)
                    for (int i = 0; i < b.def.phase2Summons.Length; i++)
                    {
                        if (db.Enemy(b.def.phase2Summons[i]) == null) continue;
                        float a = i * Mathf.PI * 2 / b.def.phase2Summons.Length;
                        SpawnEnemy(r, b.def.phase2Summons[i], new Point(Mathf.Clamp(e.x + Mathf.Cos(a) * 140, A.enemyMarginX, A.width - A.enemyMarginX),
                            Mathf.Clamp(e.y + Mathf.Sin(a) * 100, A.enemyMarginY, A.height - A.enemyMarginY)));
                    }
            }
            return true;
        }

        void Roar(RunState r, Enemy e)
        {
            BossConfig c = BC;
            Player p = r.player;
            r.shake = Mathf.Max(r.shake, c.roarShake);
            r.flash = Mathf.Max(r.flash, c.roarFlash);
            r.flashTint = c.roarFlashTint;
            Event(r, "bossroar", string.IsNullOrEmpty(e.brain.def.roarLine) ? null : e.brain.def.roarLine);
            // 주인공 넉백 (반경 안이면 멀리 있을수록 덜)
            float gap = Distance(p, e);
            if (gap < c.roarRadius)
            {
                Vector2 away = gap < 1 ? Vector2.down : Unit(p.x - e.x, p.y - e.y);
                float push = c.roarKnock * (1 - .5f * gap / c.roarRadius);
                p.push = new Timed { x = away.x * push, y = away.y * push, time = c.roarKnockTime, max = c.roarKnockTime };
                p.dash = 0;
            }
            // 주변 적도 밀려난다
            foreach (Enemy o in r.enemies)
            {
                if (o == e || o.hp <= 0) continue;
                float d = Distance(o, e);
                if (d >= c.roarRadius) continue;
                Vector2 away = d < 1 ? Vector2.up : Unit(o.x - e.x, o.y - e.y);
                float push = c.roarKnock * (1 - .5f * d / c.roarRadius);
                o.push = new Timed { x = away.x * push, y = away.y * push, time = c.roarKnockTime, max = c.roarKnockTime };
                o.action = EnemyAction.Rest; o.timer = Mathf.Max(o.timer, c.roarKnockTime);
            }
            Fx burst = Spawn(r, FxKind.Burst, e.x, e.y, X.killBurst);
            burst.color = c.roarColor;
            burst.size *= 3;
        }

        // ---------------------------------------------------------------- 패턴

        void UpdateBoss(RunState r, Enemy e, float dt, float gap, Vector2 near)
        {
            BossBrain b = e.brain;
            BossDef d = b.def;
            if (gap < 1) near = new Vector2(e.facing >= 0 ? 1 : -1, 0);   // 뛰어 내려앉아 주인공과 딱 겹치면 방향이 0 → 비켜설 쪽을 정해 준다
            if (b.move == null)
            {
                // 걸어 다니며 거리 맞추기
                e.action = EnemyAction.Move;
                e.facing = near.x < 0 ? -1 : 1;
                float preferred = d.preferredDistance > 0 ? d.preferredDistance : e.type.preferredDistance;
                float speed = gap > preferred + E.approachSlack ? e.speed : gap < preferred - E.retreatSlack ? -e.speed * E.retreatSpeed : 0;
                e.x += near.x * speed * dt;
                e.y += near.y * speed * dt;
                b.cooldown -= dt;
                if (b.cooldown <= 0 && e.charmTime <= 0) StartMove(r, e);
            }
            else RunMove(r, e, dt, near);
            e.x = Mathf.Clamp(e.x, A.enemyMarginX, A.width - A.enemyMarginX);
            e.y = Mathf.Clamp(e.y, A.enemyMarginY, A.height - A.enemyMarginY);
        }

        void StartMove(RunState r, Enemy e)
        {
            BossBrain b = e.brain;
            BossMove[] list = b.Moves;
            int pick = Mathf.Min(list.Length - 1, (int)(r.random() * list.Length));
            if (list.Length > 1 && pick == b.last) pick = (pick + 1 + (int)(r.random() * (list.Length - 1))) % list.Length;
            b.last = pick;
            b.move = list[pick];
            b.step = 0;
            BeginStep(r, e);
        }

        static int Count(BossMove m) => Mathf.Max(1, m.count);
        Point Target(RunState r, BossMove m)
        {
            Player p = r.player;
            return new Point(Mathf.Clamp(p.x + p.vx * m.lead, A.wallLeft, A.wallRight), Mathf.Clamp(p.y + p.vy * m.lead, A.wallTop, A.wallBottom));
        }

        BossTelegraph Tele(RunState r, Enemy e, TeleShape shape, float x, float y, float windup, BossMove m, float damageScale = 1)
        {
            var t = new BossTelegraph
            {
                shape = shape, x = x, y = y, radius = m.radius, inner = m.inner, width = m.width,
                time = windup, max = windup, damage = e.damage * m.damage * damageScale, knock = m.knock, owner = e, color = e.brain.def.telegraph,
            };
            r.telegraphs.Add(t);
            return t;
        }

        // 한 번(step)의 예고를 깐다. 예고가 끝나면 RunMove 가 다음 step 으로.
        void BeginStep(RunState r, Enemy e)
        {
            BossBrain b = e.brain;
            BossMove m = b.move;
            Player p = r.player;
            float windup = m.windup * b.Tempo;
            Vector2 to = Unit(p.x - e.x, p.y - e.y);
            float aim = Mathf.Atan2(to.y, to.x);
            e.aim = to;
            e.facing = to.x < 0 ? -1 : 1;
            b.timer = windup;
            switch (m.kind)
            {
                case BossMoveKind.Slam:
                {
                    Point at = Target(r, m);
                    Tele(r, e, TeleShape.Circle, at.x, at.y, windup, m);
                    e.action = EnemyAction.WindupMelee;
                    break;
                }
                case BossMoveKind.Cone:
                {
                    BossTelegraph t = Tele(r, e, TeleShape.Cone, e.x, e.y, windup, m);
                    t.angle = aim + b.step * m.spread * Mathf.Deg2Rad;   // spread 0 = 매번 주인공 쪽으로 다시 겨눔
                    t.width = m.width * Mathf.Deg2Rad;
                    e.action = EnemyAction.WindupMelee;
                    break;
                }
                case BossMoveKind.Charge:
                {
                    BossTelegraph t = Tele(r, e, TeleShape.Line, e.x, e.y, windup, m, 0);   // 피해는 몸 닿음
                    t.angle = aim;
                    t.radius = m.radius;
                    e.action = EnemyAction.WindupDash;
                    break;
                }
                case BossMoveKind.Nova:
                    Tele(r, e, m.inner > 0 ? TeleShape.Ring : TeleShape.Circle, e.x, e.y, windup, m);
                    e.action = EnemyAction.WindupShot;
                    break;
                case BossMoveKind.Rain:
                {
                    int n = Count(m);
                    for (int i = 0; i < n; i++)
                    {
                        Point at = i == 0 ? Target(r, m) : new Point(p.x, p.y);
                        if (i > 0)
                        {
                            float a = r.random() * Mathf.PI * 2, d = Mathf.Sqrt(r.random()) * m.spread;
                            at = new Point(Mathf.Clamp(p.x + Mathf.Cos(a) * d, A.wallLeft, A.wallRight), Mathf.Clamp(p.y + Mathf.Sin(a) * d, A.wallTop, A.wallBottom));
                        }
                        BossTelegraph t = Tele(r, e, TeleShape.Circle, at.x, at.y, windup, m);
                        t.delay = i * m.interval * b.Tempo;
                    }
                    b.timer = windup + (n - 1) * m.interval * b.Tempo;
                    e.action = EnemyAction.WindupShot;
                    break;
                }
                case BossMoveKind.Lines:
                {
                    int n = Count(m);
                    float gapDeg = m.spread >= 360 ? 360f / n : m.spread;
                    float twist = 0;
                    for (int i = 0; i < n; i++)
                    {
                        BossTelegraph t = Tele(r, e, TeleShape.Line, e.x, e.y, windup, m);
                        t.angle = aim + twist + (m.spread >= 360 ? i * gapDeg : (i - (n - 1) / 2f) * gapDeg) * Mathf.Deg2Rad;
                    }
                    e.action = EnemyAction.WindupShot;
                    break;
                }
                case BossMoveKind.Volley:
                {
                    int n = Count(m);
                    for (int i = 0; i < n; i++)
                    {
                        float a = m.spread >= 360 ? aim + i * Mathf.PI * 2 / n : aim + (n > 1 ? (i / (n - 1f) - .5f) * m.spread * Mathf.Deg2Rad : 0);
                        BossTelegraph t = Tele(r, e, TeleShape.Line, e.x, e.y, windup, m, 0);   // 탄 길 표시만
                        t.angle = a; t.width = Mathf.Max(6, m.width);
                    }
                    e.action = EnemyAction.WindupShot;
                    break;
                }
                case BossMoveKind.Leap:
                {
                    Point at = Target(r, m);
                    Tele(r, e, TeleShape.Circle, at.x, at.y, windup, m);
                    b.leapFrom = new Vector2(e.x, e.y);
                    b.leapTo = new Vector2(at.x, at.y);
                    b.leapMax = windup;
                    b.leapTime = 0;
                    e.action = EnemyAction.WindupDash;
                    break;
                }
                case BossMoveKind.Pull:
                {
                    Tele(r, e, m.inner > 0 ? TeleShape.Ring : TeleShape.Circle, e.x, e.y, windup, m);
                    float gap = Distance(p, e);
                    float pull = Mathf.Min(m.speed, Mathf.Max(0, gap - e.radius - 30));
                    if (pull > 0) p.push = new Timed { x = -to.x * pull, y = -to.y * pull, time = windup * .6f, max = windup * .6f };
                    e.action = EnemyAction.WindupShot;
                    break;
                }
            }
            Event(r, "telegraph");
        }

        void RunMove(RunState r, Enemy e, float dt, Vector2 near)
        {
            BossBrain b = e.brain;
            BossMove m = b.move;
            Player p = r.player;
            if (b.chargeTime > 0)
            {
                // 돌진 중: 몸에 닿으면 피해 (한 번)
                b.chargeTime -= dt;
                e.action = EnemyAction.Charge;
                e.x += e.aim.x * m.speed * dt;
                e.y += e.aim.y * m.speed * dt;
                if (!b.chargeHit && Distance(p, e) < e.radius + BC.chargePad && HitPlayer(r, e.damage * m.damage, e))
                {
                    b.chargeHit = true;
                    Knock(p, e.x, e.y, m.knock);
                }
                if (b.chargeTime <= 0) NextStep(r, e);
                return;
            }
            if (b.gap > 0)
            {
                b.gap -= dt;
                if (b.gap <= 0) BeginStep(r, e);
                return;
            }
            if (m.kind == BossMoveKind.Leap && b.leapMax > 0 && b.leapTime < b.leapMax)
            {
                // 뛰어오르기: 예고의 뒤쪽 60% 동안 포물선으로 날아가 원 가운데에 내려앉는다
                b.leapTime += dt;
                float k = Mathf.Clamp01((b.leapTime / b.leapMax - .4f) / .6f);
                Vector2 at = Vector2.Lerp(b.leapFrom, b.leapTo, k * k * (3 - 2 * k));
                e.x = at.x; e.y = at.y;
                b.lift = Mathf.Sin(Mathf.PI * k) * BC.leapHeight;
            }
            if (b.timer > 0)
            {
                b.timer -= dt;
                if (b.timer > 0) return;
                // 예고 끝
                b.lift = 0;
                switch (m.kind)
                {
                    case BossMoveKind.Charge:
                        b.chargeTime = m.radius / Mathf.Max(1, m.speed);
                        b.chargeHit = false;
                        e.action = EnemyAction.Charge;
                        Event(r, "bossslam");
                        return;
                    case BossMoveKind.Volley:
                        FireVolley(r, e, m);
                        break;
                }
                e.action = EnemyAction.Rest;   // 마무리 동작 (strike, fire, brake)
                NextStep(r, e);
            }
        }

        void NextStep(RunState r, Enemy e)
        {
            BossBrain b = e.brain;
            BossMove m = b.move;
            b.chargeTime = 0;
            b.step++;
            // 연타 패턴만 count 번 되풀이 (Rain, Lines, Volley, Nova, Pull 의 count 는 개수)
            bool repeats = m.kind is BossMoveKind.Slam or BossMoveKind.Cone or BossMoveKind.Charge or BossMoveKind.Leap;
            bool again = repeats && b.step < Count(m);
            if (again)
            {
                b.timer = 0;
                b.leapMax = 0;
                // 간격 동안은 Rest 로 숨을 고르고 다음 예고
                b.gap = m.interval * b.Tempo;
                if (b.gap <= 0) BeginStep(r, e);
                return;
            }
            b.move = null;
            b.leapMax = 0;
            e.action = EnemyAction.Rest;
            b.cooldown = (m.rest + b.def.idle) * b.Tempo;
        }

        void FireVolley(RunState r, Enemy e, BossMove m)
        {
            foreach (BossTelegraph t in r.telegraphs.Where(t => t.owner == e && !t.resolved && t.damage <= 0 && t.shape == TeleShape.Line).ToList())
            {
                r.bullets.Add(new Bullet { x = e.x, y = e.y, vx = Mathf.Cos(t.angle) * m.speed, vy = Mathf.Sin(t.angle) * m.speed, hit = e.damage * m.damage, boss = e, element = e.element });
                t.time = 0; t.resolved = true; t.flash = 0;
            }
            Event(r, "enemyShot");
        }

        static void Knock(Player p, float fromX, float fromY, float distance)
        {
            if (distance <= 0) return;
            Vector2 away = Unit(p.x - fromX, p.y - fromY);
            if (away == Vector2.zero) away = Vector2.down;
            p.push = new Timed { x = away.x * distance, y = away.y * distance, time = .28f, max = .28f };
        }

        // 예고가 다 차면 터진다: 그 안에 있으면 피해 (대쉬 중이면 회피)
        void UpdateTelegraphs(RunState r, float dt)
        {
            if (r.telegraphs.Count == 0) return;
            Player p = r.player;
            r.telegraphs.RemoveAll(t =>
            {
                if (t.owner == null || t.owner.killed || t.owner.hp <= 0 || !r.enemies.Contains(t.owner) || t.owner.brain == null || t.owner.brain.Shielded) return true;
                if (t.delay > 0) { t.delay -= dt; return false; }
                if (t.resolved) return (t.flash -= dt) <= 0;
                t.time -= dt;
                if (t.time > 0) return false;
                t.resolved = true;
                t.flash = BC.flashTime;
                if (t.damage <= 0) return t.flash <= 0;
                if (Inside(t, p.x, p.y) && r.outcome == Outcome.None)
                {
                    if (HitPlayer(r, t.damage, t.owner)) Knock(p, t.x, t.y, t.knock);
                }
                r.shake = Mathf.Max(r.shake, BC.hitShake);
                Fx burst = Spawn(r, FxKind.Burst, t.x, t.y, X.killBurst);
                burst.color = t.color;
                Event(r, "bossslam");
                return false;
            });
        }

        // 바닥 도형 안인지 (주인공 발밑 점)
        public static bool Inside(BossTelegraph t, float px, float py)
        {
            float dx = px - t.x, dy = py - t.y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            switch (t.shape)
            {
                case TeleShape.Circle: return d <= t.radius;
                case TeleShape.Ring: return d <= t.radius && d >= t.inner;
                case TeleShape.Cone:
                    if (d > t.radius) return false;
                    if (d < 1) return true;
                    return Mathf.Abs(Mathf.DeltaAngle(t.angle * Mathf.Rad2Deg, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg)) <= t.width * Mathf.Rad2Deg / 2;
                case TeleShape.Line:
                {
                    float ux = Mathf.Cos(t.angle), uy = Mathf.Sin(t.angle);
                    float along = dx * ux + dy * uy, side = -dx * uy + dy * ux;
                    return along >= 0 && along <= t.radius && Mathf.Abs(side) <= t.width / 2;
                }
            }
            return false;
        }

        // 개발자 모드: 보스를 바로 2페이즈로 (전환 연출 그대로)
        public bool DevBossPhase(RunState r)
        {
            Enemy boss = r.enemies.FirstOrDefault(e => e.IsBoss && e.hp > 0 && e.brain != null);
            if (boss == null || boss.brain.phase >= 2 || boss.brain.Shielded) return false;
            boss.hp = 0;
            Kill(r, boss, new List<Strand>());
            return true;
        }
    }
}
