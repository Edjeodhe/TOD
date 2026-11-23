// 전투·적 행동·라운드·증강·욕망 (웹판 combat.js 를 1:1로 옮김). 수치는 모두 GameDatabase 에서 읽는다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOD
{
    public class Combat
    {
        readonly GameDatabase db;
        public Combat(GameDatabase db) { this.db = db; }

        ArenaConfig A => db.arena;
        HeroConfig H => db.hero;
        FeelConfig F => db.feel;
        EnemyConfig E => db.enemies;
        FxConfig X => db.fx;
        MessageConfig M => db.messages;
        float WorldVertical => A.attackY / A.projectionY;
        public int BossRound => A.roundDurations.Length - 1;

        // JS Math.round (0.5 은 올림)
        public static float Round(float v) => Mathf.Floor(v + .5f);
        static float Hypot(float x, float y) => Mathf.Sqrt(x * x + y * y);
        static Vector2 Unit(float x, float y) { float l = Hypot(x, y); if (l == 0) l = 1; return new Vector2(x / l, y / l); }
        public static float Distance(Point p, Point q) => Hypot(p.x - q.x, p.y - q.y);

        public static float LineDistance(float px, float py, Point a, Point b)
        {
            float dx = b.x - a.x, dy = b.y - a.y;
            float length = dx * dx + dy * dy;
            if (length == 0) length = 1;
            float t = Mathf.Clamp01(((px - a.x) * dx + (py - a.y) * dy) / length);
            return Hypot(px - a.x - dx * t, py - a.y - dy * t);
        }

        public int XpNeeded(RunState r) => db.rewards.xpBase + (r.level - 1) * db.rewards.xpStep;
        public int YarnMax(RunState r) => Mathf.Max(H.minYarn, (int)Round(H.baseYarn + r.Gear(StatKind.Yarn) + r.yarnBonus - (r.desires.Contains("obsession") ? db.desire.obsessionYarn : 0)));
        public float DesireFraction(RunState r) => r.desire / r.maxDesire;
        public int KnotInterval(RunState r) => r.rules.Contains("earlyKnot") ? H.earlyKnotInterval : H.knotInterval;
        public float EnemyHeight(Enemy e) => e.type.height * (e.elite ? E.eliteHeight : 1);
        public float RoundDuration(RunState r) => r.round < A.roundDurations.Length ? A.roundDurations[r.round] : 0;

        public static (Point start, Point end) Endpoints(Player player, Strand s) => (s.anchor ?? player, (Point)s.target ?? s);

        float NeedleSpeed(Strand s, float now)
        {
            NeedleConfig n = db.needle;
            float left = Mathf.Clamp01(s.range / n.reach);
            float cruise = n.settle + (n.launch - n.settle) * Mathf.Pow(left, n.curve);
            float kick = Mathf.Clamp01((now - s.birth) / n.kick);
            return cruise * (n.start + (1 - n.start) * (1 - Mathf.Pow(1 - kick, 3)));
        }

        float DashTravel(float u) => F.dashDistance * (1 - Mathf.Pow(1 - Mathf.Clamp01(u), F.dashEase));

        Enemy FirstImpact(Point a, Point b, List<Enemy> enemies)
        {
            float dx = b.x - a.x, dy = (b.y - a.y) / WorldVertical;
            float lengthSq = dx * dx + dy * dy;
            Enemy first = null;
            float earliest = float.PositiveInfinity;
            foreach (Enemy e in enemies)
            {
                if (e.hp <= 0) continue;
                float x = a.x - e.x, y = (a.y - e.y) / WorldVertical;
                float offset = x * x + y * y - e.radius * e.radius;
                float dot = x * dx + y * dy;
                float discriminant = dot * dot - lengthSq * offset;
                if (discriminant < 0) continue;
                float t = offset <= 0 ? 0 : (-dot - Mathf.Sqrt(discriminant)) / lengthSq;
                if (t >= 0 && t <= 1 && t < earliest) { earliest = t; first = e; }
            }
            return first;
        }

        // ---------------------------------------------------------------- run lifecycle

        public RunState CreateRun(Profile profile, Func<float> random)
        {
            GearStats gear = Gear.Stats(db, profile);
            float hp = Round(H.baseHp + gear[StatKind.Health]);
            var r = new RunState
            {
                profile = profile, random = random, gearStats = gear, spawnIn = db.spawn.startDelay,
                element = Gear.ThreadElement(gear), maxDesire = db.desire.maxBase + profile.capacity * db.desire.capacityStep,
                hp = hp, maxHp = hp, yarn = Round(H.baseYarn + gear[StatKind.Yarn]), recharge = 1 + gear[StatKind.Recharge],
                damage = H.baseDamage + gear[StatKind.Damage], encumbrance = H.encumbrance,
            };
            r.player.x = A.width / 2;
            r.player.y = A.height / 2;
            r.player.angle = H.initialAngle;
            return r;
        }

        void Event(RunState r, string type, string message = null) => r.events.Add(new GameEvent { type = type, message = message });

        Fx Spawn(RunState r, FxKind kind, float x, float y, FxSpec spec)
        {
            var fx = new Fx { kind = kind, x = x, y = y, time = spec.time, max = spec.time, size = spec.size, lift = spec.lift, alpha = spec.alpha };
            r.effects.Add(fx);
            return fx;
        }

        Fx Flip(RunState r, FlipSheet sheet, float x, float y, FxSpec spec, float angle, Element element)
        {
            Fx fx = Spawn(r, FxKind.Flip, x, y, spec);
            fx.sheet = sheet; fx.angle = angle; fx.element = element; fx.useElement = true;
            return fx;
        }

        public void AdjustDesire(RunState r, float value)
        {
            r.desire = Mathf.Clamp(r.desire + value, 0, r.maxDesire);
            if (r.desire < r.maxDesire) return;
            if (r.mood == Mood.Frenzy) { r.outcome = Outcome.Absorbed; return; }
            r.mood = r.random() < db.desire.awakenChance ? Mood.Awakened : Mood.Frenzy;
            bool awakened = r.mood == Mood.Awakened;
            r.desire = r.maxDesire * (awakened ? db.desire.awakenReset : db.desire.frenzyReset);
            Fx fx = Spawn(r, FxKind.Sprite, r.player.x, r.player.y, awakened ? X.awaken : X.frenzy);
            fx.sprite = awakened ? FxSprite.Stitch : FxSprite.Boss;
            Event(r, "desire", awakened ? M.awaken : M.frenzy);
        }

        public void BeginFloor(RunState r)
        {
            r.round = 1;
            r.player.x = A.width / 2;
            r.player.y = A.height / 2;
            AdjustDesire(r, Mathf.Max(0, Round(db.desire.perFloor * r.floor - r.Gear(StatKind.Purity))));
            if (r.outcome == Outcome.None) BeginRound(r);
        }

        public void BeginRound(RunState r)
        {
            r.phase = Phase.Combat;
            r.elapsed = 0; r.spawned = 0; r.spawnIn = db.spawn.startDelay;
            r.bullets.Clear(); r.strands.Clear(); r.knots.Clear(); r.trails.Clear();
            r.echo = null; r.shotCount = 0; r.winding = 0; r.dry = 0; r.nextKnot = 0;
            r.yarn = YarnMax(r);
            if (r.round == BossRound) SpawnEnemy(r, "boss", new Point(A.bossSpawn.x, A.bossSpawn.y));
            string name = r.round == BossRound ? M.bossRoundName : string.Format(M.roundName, r.round);
            Event(r, "round", string.Format(M.roundBanner, r.floor, name));
        }

        public List<AugmentDef> AugmentChoices(RunState r)
        {
            AugmentConfig c = db.augments;
            List<AugmentDef> pool = c.augments.Where(a => !a.rule || !r.rules.Contains(a.id)).ToList();
            var choices = new List<AugmentDef>();
            while (choices.Count < c.choices && pool.Count > 0)
            {
                float pick = r.random() * pool.Sum(a => a.rule ? c.ruleWeight : c.statWeight);
                int index = pool.FindIndex(a => (pick -= a.rule ? c.ruleWeight : c.statWeight) < 0);
                if (index < 0) index = pool.Count - 1;
                choices.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return choices;
        }

        public void ApplyAugment(RunState r, string id)
        {
            AugmentConfig c = db.augments;
            if (db.Augment(id)?.rule == true) r.rules.Add(id);
            switch (id)
            {
                case "fire": r.fireRate *= c.fireRate; break;
                case "damage": r.damage += c.damage; break;
                case "supply": r.yarnBonus += c.supplyYarn; r.recharge *= c.supplyRecharge; r.yarn = Mathf.Min(YarnMax(r), r.yarn + c.supplyYarn); break;
                case "light": r.encumbrance *= c.lightEncumbrance; r.dashRate *= c.lightDash; break;
                case "calm": AdjustDesire(r, -c.calm); break;
            }
            r.augmentQueue--;
        }

        public void ChooseDesire(RunState r, string id)
        {
            if (id != null)
            {
                if (id == db.desire.refine.id) r.damage += db.desire.refineDamage;
                else r.desires.Add(id);
                r.yarn = Mathf.Min(r.yarn, YarnMax(r));
                AdjustDesire(r, db.desire.absorb);
            }
            if (r.outcome == Outcome.None) r.phase = r.floor == A.finalFloor ? Phase.Victory : Phase.Shop;
        }

        // ---------------------------------------------------------------- enemies, rewards, damage

        Enemy SpawnEnemy(RunState r, string typeId, Point location, bool elite = false)
        {
            EnemyType def = db.Enemy(typeId);
            SpawnConfig s = db.spawn;
            if (location == null)
            {
                int edge = Mathf.FloorToInt(r.random() * 4);
                location = edge < 2
                    ? new Point(edge == 1 ? A.width - s.edgeInset : s.edgeInset, s.edgeInsetY + r.random() * (A.height - 2 * s.edgeInsetY))
                    : new Point(s.sideSpan + r.random() * (A.width - 2 * s.sideSpan), edge == 2 ? s.topSpan : A.height - s.topSpan);
                if (Distance(location, r.player) < s.avoidPlayerRadius) location.x = A.width - location.x;
            }
            float hp = Round(def.hp * (1 + E.hpPerFloor * (r.floor - 1)) * (elite ? E.eliteHp : 1));
            var enemy = new Enemy
            {
                x = location.x, y = location.y, type = def, elite = elite, hp = hp, maxHp = hp, radius = def.radius,
                wait = def.wait * (1 - E.waitJitter / 2 + r.random() * E.waitJitter),
                speed = def.speed * (1 + E.speedPerFloor * (r.floor - 1)),
                damage = def.damage * (1 + E.damagePerFloor * (r.floor - 1)) * (elite ? E.eliteDamage : 1),
                action = EnemyAction.Move,
            };
            enemy.height = EnemyHeight(enemy);
            r.enemies.Add(enemy);
            return enemy;
        }

        void GainXp(RunState r, int amount)
        {
            r.xp += amount;
            while (r.xp >= XpNeeded(r)) { r.xp -= XpNeeded(r); r.level++; r.augmentQueue++; }
        }

        void Collect(RunState r, Drop drop)
        {
            GainXp(r, drop.xp);
            r.gold += drop.gold;
            if (drop.shard > 0) r.profile.shards += drop.shard;
            if (drop.item) Event(r, "loot", string.Format(M.loot, Gear.AddItem(db, r.profile).name));
            if (drop.shard > 0 || drop.item) Gear.Save(db, r.profile);
        }

        void Kill(RunState r, Enemy e, List<Strand> stuck)
        {
            RewardConfig rw = db.rewards;
            r.kills++;
            bool boss = e.IsBoss;
            r.drops.Add(new Drop
            {
                x = e.x, y = e.y, xp = boss ? rw.xpBoss : e.elite ? rw.xpElite : rw.xpNormal,
                gold = (int)(e.type.gold * (e.elite ? E.eliteGold : 1)), shard = boss ? rw.shardBoss : e.elite ? rw.shardElite : 0,
                item = boss || (e.elite && r.random() < rw.eliteItemChance),
            });
            if (r.desires.Contains("hunger")) r.hp = Mathf.Min(r.maxHp, r.hp + db.desire.hungerHeal);
            if (stuck.Count > 0 && r.desires.Contains("obsession"))
            {
                Enemy nearby = r.enemies.Where(o => o.hp > 0 && o != e).OrderBy(o => Distance(o, e)).FirstOrDefault();
                if (nearby != null) r.strands.AddRange(stuck.Select(s => { Strand copy = s.Copy(); copy.target = nearby; return copy; }));
            }
            if (e.cut != null)
            {
                Fx fx = Spawn(r, FxKind.Sliced, e.x, e.y, X.sliced);
                fx.type = e.type; fx.elite = e.elite; fx.facing = e.facing; fx.angle = e.cut.angle; fx.side = e.cut.side;
            }
            else Spawn(r, FxKind.Burst, e.x, e.y, X.killBurst).color = boss ? X.bossBurstColor : X.killBurstColor;
        }

        bool DamagePlayer(RunState r, float amount)
        {
            Player p = r.player;
            if (p.dash > 0 || p.dashGrace > 0)
            {
                if (!p.dodged)
                {
                    p.dodged = true;
                    Fx text = Spawn(r, FxKind.Number, p.x, p.y, X.dodgeText);
                    text.text = M.dodge; text.color = X.dodgeTextColor;
                    Fx shock = Spawn(r, FxKind.Sprite, p.x, p.y, X.dodgeShock);
                    shock.sprite = FxSprite.Shock; shock.element = Element.Dodge; shock.useElement = true; shock.burst = true;
                    Event(r, "dodge");
                }
                return false;
            }
            if (p.hurt > 0) return false;
            float dealt = Round(amount * (r.desires.Contains("rage") ? db.desire.rageTaken : 1));
            r.hp = Mathf.Max(0, r.hp - dealt);
            p.hurt = H.hurtTime;
            Act(p, HeroAction.Hurt);
            r.shake = H.hurtShake;
            r.hurtFlash = H.hurtFlash;
            Fx number = Spawn(r, FxKind.Number, p.x, p.y, X.hurtNumber);
            number.text = $"-{dealt}"; number.color = X.hurtNumberColor;
            Spawn(r, FxKind.Burst, p.x, p.y, X.hurtBurst).color = X.hurtBurstColor;
            Event(r, "hurt");
            if (r.hp <= 0) { Act(p, HeroAction.Die); r.outcome = Outcome.Dead; }
            return true;
        }

        public static void Act(Player player, HeroAction action)
        {
            player.previousPose = player.action == action ? null : new PoseSnapshot
            {
                action = player.action, actionTime = player.actionTime, strideTime = player.strideTime, angle = player.angle, motion = player.motion,
            };
            player.action = action;
            player.actionTime = 0;
        }

        // 커서가 적의 몸 위에 있으면 그 적을 겨눈다 (겹치면 앞쪽 적).
        Point AimAtEnemy(RunState r, Point pointer)
        {
            Point target = pointer;
            foreach (Enemy e in r.enemies)
            {
                float height = EnemyHeight(e);
                float dx = (pointer.x - e.x) / (e.radius + 3);
                float dy = ((pointer.y - e.y) * A.projectionY + height / 2) / (height / 2 + 10);
                if (e.hp > 0 && dx * dx + dy * dy < 1 && (target == pointer || target.y < e.y)) target = e;
            }
            return target;
        }

        float RangeToWall(Strand s)
        {
            float x = s.vx < 0 ? A.wallLeft : A.wallRight;
            float y = s.vy < 0 ? A.wallTop : A.wallBottom;
            float tx = s.vx != 0 ? (x - s.x) / s.vx : float.PositiveInfinity;
            float ty = s.vy != 0 ? (y - s.y) / s.vy : float.PositiveInfinity;
            return Mathf.Max(0, Mathf.Min(tx, ty));
        }

        // ---------------------------------------------------------------- needle & thread

        void Fire(RunState r, Vector2 pointerPos)
        {
            Player p = r.player;
            if (p.shotCooldown > 0 || p.dash > 0) return;
            if (r.yarn < 1)
            {
                if (r.dry <= 0) Event(r, "empty", M.emptyYarn);
                r.dry = H.dryTime;
                return;
            }
            Point aim = AimAtEnemy(r, new Point(pointerPos.x, pointerPos.y));
            float dx = aim.x - p.x, dy = (aim.y - p.y) * A.projectionY;
            if (Hypot(dx, dy) < H.aimDeadZone) return;
            Vector2 dir = Unit(dx, dy);
            r.yarn -= 1;
            p.shotCooldown = H.shotInterval / r.fireRate;
            p.facing = dir.x >= 0 ? 1 : -1;
            p.angle = Mathf.Atan2(dir.y, dir.x);
            Act(p, HeroAction.Shoot);
            r.strands.Add(new Strand
            {
                x = p.x, y = p.y, vx = dir.x, vy = dir.y / A.projectionY, speed = db.needle.launch * db.needle.start,
                range = db.needle.reach, state = StrandState.Flight, bounce = r.rules.Contains("bounce"), birth = r.time, element = r.element,
            });
            float screenAngle = Mathf.Atan2(dir.y, dir.x);
            p.recoil = new Timed { x = -dir.x, y = -dir.y, time = F.recoilTime, max = F.recoilTime };
            r.kick = new Timed { x = -dir.x * F.recoilCamera, y = -dir.y * F.recoilCamera, time = F.recoilCameraTime, max = F.recoilCameraTime };
            Flip(r, FlipSheet.Muzzle, p.x + dir.x * X.muzzleForward, p.y + dir.y / A.projectionY * X.muzzleForward, X.muzzle, screenAngle, r.element);
            r.shotCount++;
            Event(r, "shoot");
            if (r.shotCount >= KnotInterval(r))
            {
                var knot = new Knot { x = p.x, y = p.y, index = r.nextKnot++ };
                foreach (Strand s in r.strands) if (s.anchor == null) s.anchor = knot;
                r.knots.Add(knot);
                r.shotCount = 0;
                Event(r, "knot");
            }
        }

        void Attach(RunState r, Strand s, Enemy e)
        {
            s.target = e;
            s.state = StrandState.Stuck;
            s.landed = r.time;
            Vector2 dir = Unit(s.vx, s.vy * A.projectionY);
            if (s.vx == 0 && s.vy == 0) dir = Vector2.zero;
            s.bite = dir;
            e.flash = F.stickFlash;
            e.jolt = new Timed { x = dir.x, y = dir.y, time = F.stickJolt, max = F.stickJolt };
            float push = e.IsBoss ? F.stickPush * F.stickBossPush : F.stickPush;
            e.push = new Timed { x = dir.x * push, y = dir.y / A.projectionY * push, time = F.stickTime };
            r.freeze = Mathf.Max(r.freeze, F.stickFreeze);
            r.shake = Mathf.Max(r.shake, F.stickShake);
            Flip(r, FlipSheet.Puncture, e.x, e.y, X.puncture, Mathf.Atan2(dir.y, dir.x), s.element ?? Element.Plain);
        }

        void UpdateNeedles(RunState r, float dt)
        {
            foreach (Strand s in r.strands.ToList())
            {
                if (s.state != StrandState.Flight) continue;
                float wall = RangeToWall(s);
                s.speed = NeedleSpeed(s, r.time);
                float step = Mathf.Min(s.range, Mathf.Min(wall, s.speed * dt));
                var next = new Point(s.x + s.vx * step, s.y + s.vy * step);
                Enemy hit = FirstImpact(s, next, r.enemies);
                s.x = next.x; s.y = next.y;
                s.range -= step;
                if (hit != null) Attach(r, s, hit);
                else if (wall <= step && s.range > 0 && s.bounce)
                {
                    if (s.x <= A.wallLeft + .001f || s.x >= A.wallRight - .001f) s.vx *= -1;
                    if (s.y <= A.wallTop + .001f || s.y >= A.wallBottom - .001f) s.vy *= -1;
                    s.bounce = false;
                }
                else if (wall <= step || s.range <= 0)
                {
                    s.state = StrandState.Ground;
                    s.landed = r.time;
                    r.shake = Mathf.Max(r.shake, db.needle.groundShake);
                    Fx fx = Flip(r, FlipSheet.Thunk, next.x, next.y, X.thunk, 0, s.element ?? Element.Plain);
                    fx.squash = true;
                }
            }
        }

        void Pull(RunState r)
        {
            Player p = r.player;
            bool echoed = r.strands.Count == 0 && r.echo != null;
            List<Strand> strands = echoed ? r.echo.strands.Where(s => s.target == null || s.target.hp > 0).ToList() : r.strands;
            if (strands.Count == 0) { Event(r, "empty", M.pullEmpty); return; }
            bool rage = r.knots.Count >= db.desire.rageKnots && r.desires.Contains("rage");
            if (!echoed)
            {
                r.strands = new List<Strand>();
                r.yarn = Mathf.Min(YarnMax(r), r.yarn + Mathf.Ceil(strands.Count / 2f));
                r.echo = r.rules.Contains("echo") ? new Echo { until = r.time + db.augments.echoWindow, strands = strands.Select(s => s.Copy()).ToList() } : null;
            }
            else r.echo = null;
            r.knots.Clear();
            r.shotCount = 0;
            Act(p, HeroAction.Pull);

            List<Enemy> living = r.enemies.Where(e => e.hp > 0).ToList();
            var pinned = new HashSet<Enemy>(strands.Where(s => s.target != null).Select(s => s.target));
            var direct = new Dictionary<Enemy, int>();
            var order = new List<Enemy>();
            var cutAngle = new Dictionary<Enemy, float>();
            int impacts = 0;
            foreach (Strand s in strands)
            {
                var (start, end) = Endpoints(p, s);
                Element element = s.element ?? r.element;
                Fx snap = Spawn(r, FxKind.Pull, start.x, start.y, X.pullStrand);
                snap.toX = end.x; snap.toY = end.y; snap.element = element; snap.rage = rage; snap.seed = r.random() * 100;
                float along = Mathf.Atan2((end.y - start.y) * A.projectionY, end.x - start.x);
                Fx blade = Spawn(r, FxKind.Cut, start.x, start.y, new FxSpec { time = F.cutTime, alpha = 1 });
                blade.toX = end.x; blade.toY = end.y; blade.angle = along; blade.element = element;
                var a = new Point(start.x, start.y / WorldVertical);
                var b = new Point(end.x, end.y / WorldVertical);
                foreach (Enemy e in living)
                {
                    if (e != s.target && LineDistance(e.x, e.y / WorldVertical, a, b) > e.radius + F.pullHitPad) continue;
                    if (!direct.ContainsKey(e)) { direct[e] = 0; order.Add(e); }
                    direct[e]++;
                    if (!cutAngle.ContainsKey(e) || e == s.target) cutAngle[e] = along;
                    impacts++;
                }
            }
            float damage = r.damage * (rage ? db.desire.rageMultiplier : 1) * (r.mood == Mood.Awakened ? db.desire.awakenDamage : 1);
            var wounded = new List<Enemy>();
            int transfers = 0;
            foreach (Enemy e in living)
            {
                int own = direct.TryGetValue(e, out int n) ? n : 0;
                int transferred = pinned.Contains(e) ? impacts - own : 0;
                int count = own + transferred;
                if (count == 0) continue;
                wounded.Add(e);
                if (transferred > 0) transfers++;
                float dealt = Round(damage * count);
                e.hp = Mathf.Max(0, e.hp - dealt);
                e.flash = F.pullEnemyFlash;
                float gap = Hypot(p.x - e.x, (p.y - e.y) * A.projectionY);
                if (gap == 0) gap = 1;
                float yank = Mathf.Min(e.IsBoss ? F.yankBoss : F.yankReach, Mathf.Max(0, gap - F.yankMinGap));
                var toward = new Vector2((p.x - e.x) / gap, (p.y - e.y) / gap);
                e.yank = new Timed { x = toward.x * yank, y = toward.y * yank, time = F.yankTime };
                e.stretch = new Timed { x = toward.x, y = toward.y * A.projectionY, time = F.stretchTime };
                Element element = strands.FirstOrDefault(s => s.target == e)?.element ?? r.element;
                float angle = Mathf.Atan2(toward.y * A.projectionY, toward.x);
                Fx number = Spawn(r, FxKind.Number, e.x, e.y, X.damageNumber);
                number.text = dealt.ToString("0"); number.color = transferred > 0 ? X.transferColor : X.damageColor; number.pop = Mathf.Min(count, F.maxPopCount);
                float cut = cutAngle.TryGetValue(e, out float c) ? c : angle;
                bool heavy = count >= 2 || e.hp <= 0;
                float big = e.IsBoss ? X.bossScale : 1;
                int grow = Mathf.Min(count, X.maxGrowthHits);
                e.cut = new Cut { angle = cut, time = F.cutTime, max = F.cutTime, side = r.random() < .5f ? -1 : 1 };
                float height = EnemyHeight(e);
                Fx slash = Flip(r, FlipSheet.Slash, e.x, e.y, X.slash, cut, element);
                slash.lift = height * .5f; slash.mirror = e.cut.side < 0; slash.size = (X.slash.size + grow * X.slashGrowth) * big;
                if (heavy)
                {
                    Fx snip = Flip(r, FlipSheet.Snip, e.x, e.y, X.snip, cut + Mathf.PI / 2, element);
                    snip.lift = height * .5f; snip.size = (X.snip.size + grow * X.snipGrowth) * big;
                }
                Fx flash = Flip(r, FlipSheet.Flash, e.x, e.y, X.hitFlash, cut, element);
                flash.lift = height * .5f; flash.size = X.hitFlash.size * big;
                if (transferred > 0)
                {
                    Enemy source = order.FirstOrDefault(other => other != e);
                    if (source != null)
                    {
                        Fx t = Spawn(r, FxKind.Transfer, source.x, source.y, X.transfer);
                        t.toX = e.x; t.toY = e.y;
                    }
                }
            }
            foreach (Enemy e in wounded)
            {
                if (e.hp <= 0) Kill(r, e, strands.Where(s => s.target == e).ToList());
                else if (e.IsBoss) BossThresholds(r, e);
            }
            r.combo = impacts;
            r.freeze = impacts > 0 ? Mathf.Min(F.freezeMax, F.freezeBase + impacts * F.freezePerHit) : 0;
            r.shake = impacts > 0 ? Mathf.Min(F.shakeMax, F.shakeBase + impacts * F.shakePerHit) : F.shakeEmpty;
            if (impacts > 0)
            {
                float kx = 0, ky = 0;
                foreach (Enemy e in wounded) { Vector2 u = Unit(p.x - e.x, (p.y - e.y) * A.projectionY); kx += u.x; ky += u.y; }
                Vector2 k = Unit(kx, ky);
                float power = Mathf.Min(F.kickMax, F.kickBase + impacts * F.kickPerHit);
                r.kick = new Timed { x = k.x * power, y = k.y * power, time = F.kickTime, max = F.kickTime };
                r.flash = Mathf.Min(F.flashMax, F.flashBase + impacts * F.flashPerHit);
                r.flashTint = X.pullFlashColor;
                r.snapFlash = F.cutFlash;
                Fx glow = Flip(r, FlipSheet.Flash, p.x, p.y, X.heroFlash, 0, r.element);
                glow.size = X.heroFlash.size + Mathf.Min(impacts, X.maxFlashHits) * X.heroFlashGrowth;
                if (impacts >= 2)
                {
                    Fx combo = Spawn(r, FxKind.Combo, p.x, p.y, X.combo);
                    combo.text = string.Format(M.combo, impacts); combo.color = rage ? X.rageComboColor : X.comboColor;
                }
            }
            string message = impacts > 0 ? string.Format(M.pullHits, impacts) + (transfers > 0 ? string.Format(M.pullTransfers, transfers) : "") : M.pullNone;
            Event(r, "pull", message);
        }

        void BossThresholds(RunState r, Enemy e)
        {
            float ratio = e.hp / e.maxHp;
            while (e.threshold < E.bossThresholds.Length && ratio <= E.bossThresholds[e.threshold])
            {
                e.threshold++;
                Spawn(r, FxKind.Sprite, e.x, e.y, X.bossSummon).sprite = FxSprite.Boss;
                string type = E.bossSummons[Mathf.Min(e.threshold - 1, E.bossSummons.Length - 1)];
                foreach (float dx in new[] { -E.summonOffset.x, E.summonOffset.x })
                {
                    SpawnEnemy(r, type, new Point(Mathf.Clamp(e.x + dx, db.spawn.topSpan, A.width - db.spawn.topSpan),
                        Mathf.Clamp(e.y + E.summonOffset.y, db.spawn.topSpan, A.height - db.spawn.topSpan)));
                }
                Event(r, "round", M.bossSummon);
            }
        }

        void EnemyShots(RunState r, Enemy e, bool circle)
        {
            float angled = Mathf.Atan2(e.aim.y, e.aim.x);
            int count = circle ? E.circleShots : e.IsBoss || r.floor > E.bigFanFromFloor ? E.bigFanShots : E.smallFanShots;
            for (int i = 0; i < count; i++)
            {
                float angle = circle ? angled + i * Mathf.PI * 2 / count : angled + (i - (count - 1) / 2f) * E.fanSpread;
                float speed = circle ? E.circleSpeed : E.fanSpeed;
                r.bullets.Add(new Bullet { x = e.x, y = e.y, vx = Mathf.Cos(angle) * speed, vy = Mathf.Sin(angle) * speed, hit = e.damage });
            }
            Event(r, "enemyShot");
        }

        static void Ease(Enemy e, Timed motion, float duration, float dt)
        {
            float eased = 1 - Mathf.Pow(Mathf.Clamp01((motion.time - dt) / duration), 3);
            e.x += motion.x * (eased - motion.done);
            e.y += motion.y * (eased - motion.done);
            motion.done = eased;
            motion.time -= dt;
        }

        void UpdateEnemy(RunState r, Enemy e, float dt)
        {
            Player p = r.player;
            float gap = Distance(p, e);
            Vector2 near = Unit(p.x - e.x, p.y - e.y);
            bool boss = e.IsBoss;
            bool enraged = boss && e.hp / e.maxHp <= E.enrageAt;
            e.flash = Mathf.Max(0, e.flash - dt);
            if (e.stretch != null && (e.stretch.time -= dt) <= 0) e.stretch = null;
            if (e.jolt != null && (e.jolt.time -= dt) <= 0) e.jolt = null;
            if (e.push != null) { Ease(e, e.push, F.stickTime, dt); if (e.push.time <= 0) e.push = null; }
            if (e.cut != null && (e.cut.time -= dt) <= 0) e.cut = null;
            if (e.yank != null) { Ease(e, e.yank, F.yankTime, dt); if (e.yank.time <= 0) e.yank = null; }
            e.timer -= dt;
            if (e.action == EnemyAction.Move)
            {
                e.facing = near.x < 0 ? -1 : 1;
                e.wait -= dt;
                float preferred = e.type.preferredDistance;
                float speed = gap > preferred + E.approachSlack ? e.speed : gap < preferred - E.retreatSlack ? -e.speed * E.retreatSpeed : 0;
                e.x += near.x * speed * dt;
                e.y += near.y * speed * dt;
                if (e.wait <= 0 && (e.type.id != "stalker" || gap < E.meleeTrigger))
                {
                    e.aim = near;
                    e.action = e.type.id == "stalker" ? EnemyAction.WindupMelee
                        : e.type.id == "shooter" || (boss && r.random() < E.bossShotChance) ? EnemyAction.WindupShot : EnemyAction.WindupDash;
                    e.timer = e.action == EnemyAction.WindupDash ? (enraged ? E.enragedWindupDash : E.windupDash)
                        : e.action == EnemyAction.WindupShot ? E.windupShot : E.windupMelee;
                }
            }
            else if (e.action == EnemyAction.Charge)
            {
                float speed = enraged ? E.enragedChargeSpeed : boss ? E.bossChargeSpeed : E.chargeSpeed;
                e.x += e.aim.x * speed * dt;
                e.y += e.aim.y * speed * dt;
                if (gap < e.radius + E.chargeHitPad) DamagePlayer(r, e.damage);
                if (e.timer <= 0) { e.action = EnemyAction.Rest; e.timer = enraged ? E.enragedRestAfterCharge : E.restAfterCharge; }
            }
            else if (e.timer <= 0)
            {
                switch (e.action)
                {
                    case EnemyAction.WindupDash: e.action = EnemyAction.Charge; e.timer = E.chargeTime; break;
                    case EnemyAction.WindupMelee:
                        if (gap < E.meleeRange) DamagePlayer(r, e.damage);
                        Spawn(r, FxKind.Slash, e.x, e.y, X.meleeSlash).color = X.meleeSlashColor;
                        e.action = EnemyAction.Rest; e.timer = E.restAfterMelee;
                        break;
                    case EnemyAction.WindupShot: EnemyShots(r, e, enraged); e.action = EnemyAction.Rest; e.timer = E.restAfterShot; break;
                    case EnemyAction.Rest: e.action = EnemyAction.Move; e.wait = e.type.wait * (enraged ? E.enragedWaitMultiplier : 1); break;
                }
            }
            e.x = Mathf.Clamp(e.x, A.enemyMarginX, A.width - A.enemyMarginX);
            e.y = Mathf.Clamp(e.y, A.enemyMarginY, A.height - A.enemyMarginY);
        }

        // ---------------------------------------------------------------- frame

        public void Update(RunState r, float dt, CombatInput input)
        {
            if (r.phase != Phase.Combat || r.outcome != Outcome.None) return;
            r.shake = Mathf.Max(0, r.shake - dt);
            r.flash = Mathf.Max(0, r.flash - dt);
            r.snapFlash = Mathf.Max(0, r.snapFlash - dt);
            r.hurtFlash = Mathf.Max(0, r.hurtFlash - dt);
            Player p = r.player;
            if (p.recoil != null && (p.recoil.time -= dt) <= 0) p.recoil = null;
            if (r.kick != null && (r.kick.time -= dt) <= 0) r.kick = null;
            r.effects.RemoveAll(fx => (fx.time -= dt) <= 0);
            if (r.freeze > 0) { r.freeze -= dt; return; }
            r.time += dt;
            r.elapsed += dt;
            p.actionTime += dt;
            p.dashCooldown = Mathf.Max(0, p.dashCooldown - dt);
            p.dashGrace = Mathf.Max(0, p.dashGrace - dt);
            p.hurt = Mathf.Max(0, p.hurt - dt);
            p.shotCooldown = Mathf.Max(0, p.shotCooldown - dt);
            r.dry = Mathf.Max(0, r.dry - dt);
            if (r.echo != null && r.echo.until <= r.time) r.echo = null;

            Vector2 move = Unit(input.x, input.y);
            bool moving = input.x != 0 || input.y != 0;
            if (input.dash && p.dash <= 0 && p.dashCooldown <= 0) StartDash(r, moving, move);
            if (p.dash > 0) UpdateDash(r, dt);
            else
            {
                p.motion += ((moving ? 1 : 0) - p.motion) * Mathf.Min(1, dt * H.motionLerp);
                float factor = Mathf.Max(H.minSpeedFactor, 1 - r.knots.Count * r.encumbrance);
                float speed = H.moveSpeed * (1 + r.Gear(StatKind.Speed)) * factor * (r.mood == Mood.Frenzy ? H.frenzySpeed : 1);
                p.vx = move.x * speed;
                p.vy = move.y * speed * WorldVertical;
                p.x = Mathf.Clamp(p.x + p.vx * dt, A.wallLeft, A.wallRight);
                p.y = Mathf.Clamp(p.y + p.vy * dt, A.wallTop, A.wallBottom);
                if (moving) p.strideTime += dt * (H.strideBase + factor * H.strideFactor);
                float hold = p.action switch
                {
                    HeroAction.Shoot => H.shootHold, HeroAction.Pull => H.pullHold, HeroAction.Hurt => H.hurtHold, HeroAction.Dash => H.dashHold, _ => 0,
                };
                if (p.actionTime > hold)
                {
                    HeroAction state = moving ? HeroAction.Run : HeroAction.Idle;
                    if (p.action != state) Act(p, state);
                    if (move.x != 0) p.facing = Mathf.Sign(move.x);
                }
            }
            if (input.shoot) Fire(r, input.aim);
            if (r.yarn < YarnMax(r))
            {
                r.winding += dt / H.rechargeSeconds * r.recharge * (p.motion < H.idleMotionThreshold && p.dash <= 0 ? H.idleRechargeMultiplier : 1)
                    * (r.mood == Mood.Frenzy ? H.frenzyRechargeMultiplier : 1);
                if (r.winding >= 1) { r.yarn++; r.winding = 0; }
            }
            else r.winding = 0;
            UpdateNeedles(r, dt);
            if (input.pull) Pull(r);
            r.enemies.RemoveAll(e => e.hp <= 0);

            float duration = RoundDuration(r);
            if (duration > 0 && r.elapsed < duration)
            {
                r.spawnIn -= dt;
                if (r.spawnIn <= 0 && r.enemies.Count < db.spawn.maxEnemies)
                {
                    SpawnConfig s = db.spawn;
                    r.spawned++;
                    string type = r.round == 1 && r.spawned <= s.firstRoundStalkers ? s.firstRoundType : s.rotation[(r.spawned + r.floor) % s.rotation.Length];
                    SpawnEnemy(r, type, null, r.spawned % (r.round == 1 ? s.eliteEveryFirstRound : s.eliteEveryLater) == 0);
                    r.spawnIn = Mathf.Max(s.minInterval, (r.round == 1 ? s.intervalFirstRound : s.intervalLater) - r.floor * s.intervalFloorStep);
                }
            }
            float worldDt = p.dash > 0 ? dt * F.dashSlow : dt;
            foreach (Enemy e in r.enemies.ToList())
            {
                if (r.outcome != Outcome.None) break;
                UpdateEnemy(r, e, worldDt);
                foreach (Trail trail in r.trails)
                {
                    if (trail.used.Contains(e) || LineDistance(e.x, e.y, trail.a, trail.b) >= e.radius + F.dashTrailPad) continue;
                    trail.used.Add(e);
                    var strand = new Strand { x = e.x, y = e.y, anchor = trail.a, birth = r.time };
                    r.strands.Add(strand);
                    Attach(r, strand, e);
                }
            }
            r.trails.RemoveAll(t => (t.time -= dt) <= 0);
            r.bullets.RemoveAll(b =>
            {
                var from = new Point(b.x, b.y);
                b.x += b.vx * worldDt; b.y += b.vy * worldDt; b.age += worldDt;
                if (!b.spent && r.outcome == Outcome.None && LineDistance(p.x, p.y, from, b) < H.bulletHitRadius)
                {
                    if (DamagePlayer(r, b.hit)) return true;
                    b.spent = true;
                }
                return !(b.age < E.bulletLife && b.x > 0 && b.y > 0 && b.x < A.width && b.y < A.height);
            });
            r.drops.RemoveAll(d =>
            {
                float gap = Distance(d, p);
                if (gap < H.pickupRadius) { Collect(r, d); return true; }
                if (gap < H.magnetRadius)
                {
                    Vector2 dir = Unit(p.x - d.x, p.y - d.y);
                    float step = Mathf.Min(gap, H.magnetSpeed * dt);
                    d.x += dir.x * step; d.y += dir.y * step;
                }
                return false;
            });
            if (r.outcome != Outcome.None) return;
            bool bossRound = r.round == BossRound;
            if ((duration > 0 && r.elapsed >= duration && r.enemies.Count == 0) || (bossRound && !r.enemies.Any(e => e.IsBoss)))
            {
                foreach (Drop d in r.drops) Collect(r, d);
                r.drops.Clear(); r.enemies.Clear(); r.bullets.Clear(); r.strands.Clear(); r.knots.Clear();
                if (bossRound)
                {
                    r.profile.shards += db.rewards.shardFloorClear;
                    r.profile.best = Mathf.Max(r.profile.best, r.floor);
                    Gear.Save(db, r.profile);
                    DesireDef[] unowned = db.desire.desires.Where(d => !r.desires.Contains(d.id)).ToArray();
                    r.offer = unowned.Length > 0 ? unowned[Mathf.Min(unowned.Length - 1, Mathf.FloorToInt(r.random() * unowned.Length))] : db.desire.refine;
                    r.phase = Phase.Desire;
                }
                else r.phase = Phase.Rest;
            }
        }

        void StartDash(RunState r, bool moving, Vector2 move)
        {
            Player p = r.player;
            Vector2 dir = moving ? move : new Vector2(-Mathf.Cos(p.angle), -Mathf.Sin(p.angle));
            p.dx = dir.x; p.dy = dir.y;
            p.dash = F.dashDuration;
            p.dashDone = 0;
            p.dashCooldown = Mathf.Max(F.dashMinCooldown, F.dashCooldown * r.dashRate);
            p.dashFrom = new Point(p.x, p.y);
            p.dodged = false;
            p.ghostIn = 0;
            float away = Mathf.Atan2(p.dy * A.attackY, p.dx);
            Fx trail = Spawn(r, FxKind.Sprite, p.x + p.dx * F.dashFxOffset, p.y + p.dy * F.dashFxOffset, X.dashTrail);
            trail.sprite = FxSprite.Dash; trail.angle = away;
            Fx burst = Spawn(r, FxKind.Sprite, p.x, p.y, X.dashBurst);
            burst.sprite = FxSprite.Dodge; burst.angle = away + Mathf.PI; burst.burst = true;
            Spawn(r, FxKind.Dust, p.x, p.y, X.dust).angle = away + Mathf.PI;
            if (p.dx != 0) p.facing = -Mathf.Sign(p.dx);
            p.dashFacing = p.facing;
            Act(p, HeroAction.Dash);
            Event(r, "dash");
        }

        void UpdateDash(RunState r, float dt)
        {
            Player p = r.player;
            float step = Mathf.Min(dt, p.dash);
            float travel = DashTravel(1 - (p.dash - step) / F.dashDuration);
            float moved = travel - p.dashDone;
            p.dashDone = travel;
            p.x = Mathf.Clamp(p.x + p.dx * moved, A.wallLeft, A.wallRight);
            p.y = Mathf.Clamp(p.y + p.dy * moved * WorldVertical, A.wallTop, A.wallBottom);
            p.dash = Mathf.Max(0, p.dash - dt);
            p.motion = 1;
            p.ghostIn -= step;
            if (p.ghostIn <= 0)
            {
                p.ghostIn += F.dashGhostInterval;
                Fx ghost = Spawn(r, FxKind.Afterimage, p.x, p.y, new FxSpec { time = F.dashGhostLife, alpha = 1 });
                ghost.facing = p.dashFacing; ghost.progress = 1 - p.dash / F.dashDuration;
            }
            if (p.dash <= 0)
            {
                p.dashGrace = F.dashGrace;
                Spawn(r, FxKind.Dust, p.x, p.y, X.dustEnd).angle = Mathf.Atan2(p.dy * A.attackY, p.dx);
                if (r.rules.Contains("dashThread")) r.trails.Add(new Trail { a = p.dashFrom, b = new Point(p.x, p.y), time = F.dashTrailTime });
            }
        }
    }
}
