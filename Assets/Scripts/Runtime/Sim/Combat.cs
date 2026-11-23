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
        RoundConfig R => db.rounds;

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
        // 당길 때 걸린 실 가닥 수에 따른 피해 증가 (0 = 없음). 첫 가닥은 기본, 둘째부터 strandBonus 씩.
        public float StrandBonus(int strands) => Mathf.Min(H.strandBonusMax, H.strandBonus * Mathf.Max(0, strands - 1));

        // 재봉 조각 '여러 갈래 바늘': 한 번 쏠 때 더 나가는 바늘 수 (0 = 한 개만). 줄 강화로 값이 조금 늘어도 정수로 끊는다.
        public int ExtraNeedles(RunState r) => Mathf.Clamp(Mathf.FloorToInt(r.Gear(StatKind.Multishot) + 1e-3f), 0, Mathf.Max(0, H.multishotMax));
        // 재봉 조각 '굵은 실': 실 굵기 배율 (1 = 기본). 그리기 두께에 곱하고, 당김 판정 반경은 ThreadHitPad 만큼 넓어진다.
        public float ThreadScale(RunState r) => Mathf.Clamp(1 + r.Gear(StatKind.ThreadWidth), 1, Mathf.Max(1, H.threadWidthMax));
        public float ThreadHitPad(RunState r) => F.pullHitPad + H.threadHitWidth * (ThreadScale(r) - 1);

        public int YarnMax(RunState r) => Mathf.Max(H.minYarn, (int)Round(H.baseYarn + r.Gear(StatKind.Yarn) + r.yarnBonus - (r.desires.Contains("obsession") ? db.desire.obsessionYarn : 0)));
        public float DesireFraction(RunState r) => r.desire / r.maxDesire;
        public int KnotInterval(RunState r) => r.rules.Contains("earlyKnot") ? H.earlyKnotInterval : H.knotInterval;
        public float EnemyHeight(Enemy e) => e.type.height * (e.elite ? E.eliteHeight : 1);
        public float TimeLeft(RunState r) => r.roundKind == RoundKind.Survival ? Mathf.Max(0, r.roundLength - r.elapsed) : 0;

        public static (Point start, Point end) Endpoints(Player player, Strand s) => (s.anchor ?? player, (Point)s.target ?? s);

        float NeedleSpeed(Strand s, float now)
        {
            NeedleConfig n = db.needle;
            float left = Mathf.Clamp01(s.range / Mathf.Max(1, s.maxRange));
            float cruise = n.settle + (n.launch - n.settle) * Mathf.Pow(left, n.curve);
            float kick = Mathf.Clamp01((now - s.birth) / n.kick);
            return cruise * (n.start + (1 - n.start) * (1 - Mathf.Pow(1 - kick, 3))) * needleScale;
        }

        float needleScale = 1;   // UpdateNeedles 가 등반의 바늘 속도 능력치로 맞춘다

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
                profile = profile, random = random, gearStats = gear,
                element = Gear.ThreadElement(gear), maxDesire = db.desire.maxBase + profile.capacity * db.desire.capacityStep,
                hp = hp, maxHp = hp, yarn = Round(H.baseYarn + gear[StatKind.Yarn]), recharge = 1 + gear[StatKind.Recharge],
                damage = H.baseDamage + gear[StatKind.Damage], encumbrance = H.encumbrance,
                fireInterval = H.shotInterval, reach = db.needle.reach,
            };
            r.player.x = A.width / 2;
            r.player.y = A.height / 2;
            r.player.angle = H.initialAngle;
            r.player.dashCharges = Mathf.Max(1, F.dashCharges);
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

        // 층 시작: 컨셉 10개 중 3개를 뽑아 생존 R1·생존 R2·보스 라운드에 하나씩 쓴다.
        public void BeginFloor(RunState r)
        {
            List<ConceptDef> pool = db.concepts.ToList();
            var picked = new List<ConceptDef>();
            int survivalRounds = R.sequence.Count(k => k != RoundKind.Rest);
            while (picked.Count < survivalRounds && pool.Count > 0)
            {
                int i = Mathf.Min(pool.Count - 1, Mathf.FloorToInt(r.random() * pool.Count));
                picked.Add(pool[i]);
                pool.RemoveAt(i);
            }
            r.floorConcepts = picked.ToArray();
            r.roundIndex = 0;
            r.purified = false;
            AdjustDesire(r, Mathf.Max(0, Round(db.desire.perFloor * r.floor - r.Gear(StatKind.Purity))));
            if (r.outcome == Outcome.None) BeginRound(r);
        }

        // 라운드마다 새 맵: 플레이어를 가운데에 두고 모든 전투 요소를 비운다.
        public void BeginRound(RunState r)
        {
            r.round = r.roundIndex + 1;
            r.roundKind = R.sequence[r.roundIndex];
            int combatIndex = R.sequence.Take(r.roundIndex + 1).Count(k => k != RoundKind.Rest) - 1;
            r.concept = r.roundKind == RoundKind.Rest
                ? r.floorConcepts.LastOrDefault()
                : r.floorConcepts[Mathf.Clamp(combatIndex, 0, r.floorConcepts.Length - 1)];
            r.player.x = A.width / 2;
            r.player.y = A.height / 2;
            r.elapsed = 0; r.spawned = 0;
            foreach (Enemy e in r.enemies) r.enemyPool.Push(e);
            r.enemies.Clear(); r.drops.Clear();
            r.bullets.Clear(); r.strands.Clear(); r.knots.Clear(); r.trails.Clear(); r.effects.Clear();
            r.echo = null; r.shotCount = 0; r.winding = 0; r.dry = 0; r.nextKnot = 0;
            r.yarn = YarnMax(r);
            r.stairs = r.altar = r.merchant = null;
            r.artifactOffer.Clear(); r.artifactTaken = false; r.desirePending = false; r.artifactRerolled = false; r.bossDeath = null;
            r.purifyTime = -1; r.purifyCenter = null;
            if (r.roundKind != RoundKind.Boss) r.purified = false;
            r.spawnTimers.Clear();
            foreach (SpawnEntry s in db.spawn.table) r.spawnTimers[s] = s.startDelay;
            string name;
            switch (r.roundKind)
            {
                case RoundKind.Survival:
                    r.phase = Phase.Combat;
                    int survival = R.sequence.Take(r.roundIndex + 1).Count(k => k == RoundKind.Survival) - 1;
                    r.roundLength = R.survivalSeconds[Mathf.Clamp(survival, 0, R.survivalSeconds.Length - 1)];
                    name = r.concept?.name ?? string.Format(M.roundName, r.round);
                    break;
                case RoundKind.Boss:
                    r.phase = Phase.Combat;
                    r.roundLength = 0;
                    SpawnEnemy(r, "boss", Near(r, R.bossOffset));
                    name = M.bossRoundName;
                    break;
                default:
                    // 쉬어가는 라운드: 아티팩트 제단·확률 상인·보스로 가는 계단
                    r.phase = Phase.Cleared;
                    r.roundLength = 0;
                    r.altar = Near(r, R.altarOffset);
                    if (r.random() < R.merchantChance) r.merchant = Near(r, R.merchantOffset);
                    r.stairs = Near(r, R.restStairsOffset);
                    r.stairsAge = R.stairsRevealTime;
                    var kinds = Gear.Unowned(db, r.profile);   // 아이템은 고유: 이미 가진 종류는 탑에 나오지 않는다
                    while (r.artifactOffer.Count < R.artifactChoices && kinds.Count > 0)
                    {
                        int i = Mathf.Min(kinds.Count - 1, Mathf.FloorToInt(r.random() * kinds.Count));
                        r.artifactOffer.Add(kinds[i]);
                        kinds.RemoveAt(i);
                    }
                    name = db.ui.restRoundName;
                    break;
            }
            Event(r, "round", string.Format(M.roundBanner, r.floor, name));
        }

        Point Near(RunState r, Vector2 offset) => new(
            Mathf.Clamp(r.player.x + offset.x, A.wallLeft + 60, A.wallRight - 60),
            Mathf.Clamp(r.player.y + offset.y, A.wallTop + 60, A.wallBottom - 60));

        // 계단에서 E: 다음 라운드, 보스 뒤라면 다음 층 (마지막 층이면 승리).
        public void Ascend(RunState r)
        {
            if (r.roundKind == RoundKind.Boss)
            {
                if (r.floor >= A.finalFloor) { r.phase = Phase.Victory; return; }
                r.floor++;
                BeginFloor(r);
                return;
            }
            r.roundIndex = Mathf.Min(r.roundIndex + 1, R.sequence.Length - 1);
            BeginRound(r);
        }

        public Interactable Nearest(RunState r)
        {
            Interactable best = Interactable.None;
            float bestGap = R.interactRadius;
            void Check(Point p, Interactable kind)
            {
                if (p == null) return;
                float gap = Distance(p, r.player);
                if (gap <= bestGap) { bestGap = gap; best = kind; }
            }
            if (r.phase == Phase.Cleared && r.stairsAge >= R.stairsRevealTime) Check(r.stairs, Interactable.Stairs);
            if (!r.artifactTaken) Check(r.altar, Interactable.Altar);
            Check(r.merchant, Interactable.Merchant);
            return best;
        }

        // 쉬어가기 탑의 조각 3개를 한 번 다시 굴린다 (지금 보이는 조각은 되도록 빼고).
        public bool RerollArtifacts(RunState r)
        {
            if (r.artifactTaken || r.artifactRerolled) return false;
            r.artifactRerolled = true;
            var shown = new HashSet<int>(r.artifactOffer);
            var kinds = Gear.Unowned(db, r.profile).Where(k => !shown.Contains(k)).ToList();
            if (kinds.Count < R.artifactChoices) kinds = Gear.Unowned(db, r.profile);
            r.artifactOffer.Clear();
            while (r.artifactOffer.Count < R.artifactChoices && kinds.Count > 0)
            {
                int i = Mathf.Min(kinds.Count - 1, Mathf.FloorToInt(r.random() * kinds.Count));
                r.artifactOffer.Add(kinds[i]);
                kinds.RemoveAt(i);
            }
            return true;
        }

        // 등반 중 재봉 상자(I)에서 배치를 바꾼 뒤: 장비 능력치 차이만큼 바로 반영한다.
        public void RefreshGear(RunState r)
        {
            GearStats before = r.gearStats, after = Gear.Stats(db, r.profile);
            r.damage += after[StatKind.Damage] - before[StatKind.Damage];
            r.recharge += after[StatKind.Recharge] - before[StatKind.Recharge];
            float hp = Round(after[StatKind.Health] - before[StatKind.Health]);
            r.maxHp = Mathf.Max(1, r.maxHp + hp);
            r.hp = Mathf.Clamp(r.hp + Mathf.Max(0, hp), 1, r.maxHp);
            r.gearStats = after;   // Multishot·ThreadWidth 는 쏠 때·당길 때 r.Gear 로 바로 읽으므로 여기서 바뀐 값이 곧 반영된다
            r.element = Gear.ThreadElement(after);
            r.yarn = Mathf.Min(r.yarn, YarnMax(r));
        }

        public PieceDef TakeArtifact(RunState r, int kind)
        {
            if (r.artifactTaken || !r.artifactOffer.Contains(kind)) return null;
            r.artifactTaken = true;
            PieceDef def = Gear.AddItem(db, r.profile, kind);
            Gear.Save(db, r.profile);
            Event(r, "loot", string.Format(M.loot, def.name));
            return def;
        }

        public List<AugmentDef> AugmentChoices(RunState r, ICollection<AugmentDef> avoid = null)
        {
            AugmentConfig c = db.augments;
            List<AugmentDef> pool = c.augments.Where(a => !a.rule || !r.rules.Contains(a.id)).ToList();
            // 다시 굴리기: 방금 본 카드는 되도록 빼고 고른다 (남는 수가 모자라면 그대로 둔다).
            if (avoid != null && pool.Count(a => !avoid.Contains(a)) >= c.choices) pool = pool.Where(a => !avoid.Contains(a)).ToList();
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

        // 증강은 능력치 효과 목록을 그대로 적용한다 (규칙형은 rules 에 기록만).
        public void ApplyAugment(RunState r, string id)
        {
            AugmentDef def = db.Augment(id);
            if (def == null) return;
            if (def.rule) r.rules.Add(id);
            r.augmentsTaken[id] = (r.augmentsTaken.TryGetValue(id, out int taken) ? taken : 0) + 1;
            foreach (AugmentEffect fx in def.effects ?? Array.Empty<AugmentEffect>())
            {
                float Apply(float v) => fx.op == StatOp.Multiply ? v * fx.value : v + fx.value;
                switch (fx.stat)
                {
                    case RunStat.FireInterval: r.fireInterval = Mathf.Max(db.augments.minFireInterval, Apply(r.fireInterval)); break;
                    case RunStat.Reach: r.reach = Apply(r.reach); break;
                    case RunStat.NeedleSpeed: r.needleSpeed = Apply(r.needleSpeed); break;
                    case RunStat.Damage: r.damage = Apply(r.damage); break;
                    case RunStat.YarnMax:
                        float before = r.yarnBonus;
                        r.yarnBonus = Apply(r.yarnBonus);
                        r.yarn = Mathf.Min(YarnMax(r), r.yarn + Mathf.Max(0, r.yarnBonus - before));
                        break;
                    case RunStat.Recharge: r.recharge = Apply(r.recharge); break;
                    case RunStat.Encumbrance: r.encumbrance = Apply(r.encumbrance); break;
                    case RunStat.DashCooldown: r.dashRate = Apply(r.dashRate); break;
                    case RunStat.MoveSpeed: r.moveScale = Apply(r.moveScale); break;
                    case RunStat.Desire: AdjustDesire(r, fx.op == StatOp.Multiply ? r.desire * (fx.value - 1) : fx.value); break;
                    case RunStat.MaxHealth:
                        float gain = Apply(r.maxHp) - r.maxHp;
                        r.maxHp += gain;
                        r.hp = Mathf.Min(r.maxHp, r.hp + Mathf.Max(0, gain));   // 늘어난 만큼 채워 준다
                        break;
                }
            }
            r.augmentQueue--;
        }

        // 증강 카드에 보여 줄 "지금 → 고른 뒤" (첫 번째 효과 기준).
        public string AugmentPreview(RunState r, AugmentDef def)
        {
            if (def.effects == null || def.effects.Length == 0) return "";
            AugmentEffect fx = def.effects[0];
            float Apply(float v) => fx.op == StatOp.Multiply ? v * fx.value : v + fx.value;
            string Pair(float a, float b, string format, string unit = "") => $"{a.ToString(format)}{unit} → {b.ToString(format)}{unit}";
            switch (fx.stat)
            {
                case RunStat.Damage: return Pair(r.damage, Apply(r.damage), "0.#");
                case RunStat.FireInterval:
                    float next = Mathf.Max(db.augments.minFireInterval, Apply(r.fireInterval));
                    return Pair(1 / r.fireInterval, 1 / next, "0.00", "발/초");
                case RunStat.MoveSpeed: return Pair(r.moveScale * 100, Apply(r.moveScale) * 100, "0", "%");
                case RunStat.MaxHealth: return Pair(r.maxHp, Apply(r.maxHp), "0");
                case RunStat.Reach: return Pair(r.reach, Apply(r.reach), "0");
                case RunStat.NeedleSpeed: return Pair(r.needleSpeed * 100, Apply(r.needleSpeed) * 100, "0", "%");
                case RunStat.YarnMax: return Pair(YarnMax(r), YarnMax(r) + (Apply(r.yarnBonus) - r.yarnBonus), "0");
                case RunStat.DashCooldown:
                    float charge = F.dashCooldown;
                    return Pair(charge * r.dashRate, charge * Apply(r.dashRate), "0.00", "초");
                case RunStat.Recharge: return Pair(r.recharge * 100, Apply(r.recharge) * 100, "0", "%");
                default: return "";
            }
        }

        // 보스 정화 뒤 계단에서 고르는 욕망 흡수 (null = 거절)
        public void ChooseDesire(RunState r, string id)
        {
            r.desirePending = false;
            if (id == null) return;
            if (id == db.desire.refine.id) r.damage += db.desire.refineDamage;
            else r.desires.Add(id);
            r.yarn = Mathf.Min(r.yarn, YarnMax(r));
            AdjustDesire(r, db.desire.absorb);
        }

        // ---------------------------------------------------------------- enemies, rewards, damage

        Enemy SpawnEnemy(RunState r, string typeId, Point location, bool elite = false)
        {
            EnemyType def = db.Enemy(typeId);
            SpawnConfig s = db.spawn;
            if (location == null)
            {
                // 화면 밖 고리 위의 무작위 지점 (맵 경계 안)
                float angle = r.random() * Mathf.PI * 2;
                float distance = s.spawnDistanceMin + r.random() * (s.spawnDistanceMax - s.spawnDistanceMin);
                location = new Point(
                    Mathf.Clamp(r.player.x + Mathf.Cos(angle) * distance, A.enemyMarginX, A.width - A.enemyMarginX),
                    Mathf.Clamp(r.player.y + Mathf.Sin(angle) * distance / WorldVertical, A.enemyMarginY, A.height - A.enemyMarginY));
            }
            ConceptDef c = r.concept;
            float hp = Round(def.hp * (1 + E.hpPerFloor * (r.floor - 1)) * (elite ? E.eliteHp : 1) * (c?.hpScale ?? 1));
            // 오브젝트 풀: 사라진 적 객체를 다시 쓴다.
            Enemy enemy = r.enemyPool.Count > 0 ? r.enemyPool.Pop() : new Enemy();
            enemy.x = location.x; enemy.y = location.y; enemy.type = def; enemy.elite = elite;
            enemy.hp = hp; enemy.maxHp = hp; enemy.radius = def.radius;
            enemy.wait = def.wait * (1 - E.waitJitter / 2 + r.random() * E.waitJitter);
            enemy.speed = def.speed * (1 + E.speedPerFloor * (r.floor - 1)) * (c?.speedScale ?? 1);
            enemy.damage = def.damage * (1 + E.damagePerFloor * (r.floor - 1)) * (elite ? E.eliteDamage : 1);
            enemy.action = EnemyAction.Move; enemy.timer = 0; enemy.facing = 1; enemy.flash = 0; enemy.threshold = 0;
            enemy.aim = Vector2.right; enemy.stretch = enemy.jolt = enemy.push = enemy.yank = null; enemy.cut = null;
            enemy.height = EnemyHeight(enemy);
            r.enemies.Add(enemy);
            return enemy;
        }

        void Retire(RunState r, Enemy e) { r.enemies.Remove(e); r.enemyPool.Push(e); }

        // 유형별 스폰 표: 간격·마릿수에 무작위 흔들림, 컨셉 가중치와 층 배율을 곱한다.
        void UpdateSpawns(RunState r, float dt)
        {
            SpawnConfig s = db.spawn;
            ConceptDef c = r.concept;
            float floorRate = 1 + s.floorRateStep * (r.floor - 1);
            foreach (SpawnEntry entry in s.table)
            {
                float weight = c?.typeWeights?.FirstOrDefault(w => w.type == entry.type)?.weight ?? 1;
                if (weight <= 0) continue;
                float timer = r.spawnTimers.TryGetValue(entry, out float t) ? t : entry.startDelay;
                timer -= dt;
                if (timer <= 0)
                {
                    int count = Mathf.Max(1, entry.count + Mathf.RoundToInt((r.random() * 2 - 1) * entry.countJitter));
                    for (int i = 0; i < count && r.enemies.Count < s.maxEnemies; i++)
                    {
                        r.spawned++;
                        SpawnEnemy(r, entry.type, null, r.random() < entry.eliteChance * (c?.eliteScale ?? 1));
                    }
                    float interval = entry.interval / (weight * (c?.spawnRate ?? 1) * floorRate);
                    timer = Mathf.Max(s.minInterval, interval * (1 + (r.random() * 2 - 1) * entry.intervalJitter));
                }
                r.spawnTimers[entry] = timer;
            }
        }

        // 생존 시간이 끝나면: 스폰 중지, 남은 적은 흩어지며 사라지고, 드롭은 회수, 화면 가운데에 계단.
        void ClearRound(RunState r)
        {
            foreach (Enemy e in r.enemies.ToList())
            {
                Spawn(r, FxKind.Burst, e.x, e.y, X.killBurst).color = X.killBurstColor;
                Retire(r, e);
            }
            foreach (Drop d in r.drops) d.age = Mathf.Max(d.age, db.rewards.dropPopTime);   // 남은 드롭은 몸으로 빨려 든다
            r.bullets.Clear(); r.strands.Clear(); r.knots.Clear(); r.trails.Clear(); r.echo = null;
            r.phase = Phase.Cleared;
            r.stairs = Near(r, R.stairsOffset);
            r.stairsAge = 0;
            Event(r, "cleared", db.ui.cleared);
        }

        // 보스 처치: 실이 몸을 감아 조이고 → 종이처럼 잘려 떨어진 뒤 정화가 시작된다. 남은 적은 실이 풀리며 사라진다.
        void StartBossDeath(RunState r, Enemy boss)
        {
            foreach (Enemy e in r.enemies.ToList())
            {
                if (e == boss) continue;
                Spawn(r, FxKind.Burst, e.x, e.y, X.killBurst).color = db.purify.ringColor;
                Retire(r, e);
            }
            r.bullets.Clear(); r.strands.Clear(); r.knots.Clear(); r.trails.Clear(); r.echo = null;
            r.bossDeath = new BossDeath
            {
                x = boss.x, y = boss.y, type = boss.type, elite = boss.elite, facing = boss.facing, height = boss.height,
                cutAngle = boss.cut?.angle ?? -.5f,
            };
            r.phase = Phase.BossDying;
            r.shake = Mathf.Max(r.shake, db.bossDeath.shake);
            Event(r, "bossbind");
        }

        // 보스 처치 연출이 끝나면: 보스가 있던 자리에서 정화가 퍼지고, 끝나면 계단이 나타난다.
        void BeginPurify(RunState r)
        {
            foreach (Enemy e in r.enemies.ToList())
            {
                Spawn(r, FxKind.Burst, e.x, e.y, X.killBurst).color = db.purify.ringColor;
                Retire(r, e);
            }
            r.bullets.Clear();
            r.profile.shards += db.rewards.shardFloorClear;
            r.profile.best = Mathf.Max(r.profile.best, r.floor);
            Gear.Save(db, r.profile);
            DesireDef[] unowned = db.desire.desires.Where(d => !r.desires.Contains(d.id)).ToArray();
            r.offer = unowned.Length > 0 ? unowned[Mathf.Min(unowned.Length - 1, Mathf.FloorToInt(r.random() * unowned.Length))] : db.desire.refine;
            r.desirePending = true;
            r.purifyCenter = r.bossDeath != null ? new Point(r.bossDeath.x, r.bossDeath.y) : new Point(r.player.x, r.player.y);
            r.purifyTime = 0;
            r.phase = Phase.Purifying;
            Event(r, "purify", db.ui.purified);
        }

        // 정화 고리 반지름 (전투장 단위 — 바닥에서는 원, 화면에서는 세로로 눌린 타원) — 맵 모서리까지 닿는다.
        public float PurifyRadius(RunState r)
        {
            if (r.purifyCenter == null) return 0;
            float far = 0;
            foreach (var (cx, cy) in new[] { (0f, 0f), (A.width, 0f), (0f, A.height), (A.width, A.height) })
                far = Mathf.Max(far, Hypot(cx - r.purifyCenter.x, cy - r.purifyCenter.y));
            float u = Mathf.Clamp01(r.purifyTime / db.purify.duration);
            return far * (1 - Mathf.Pow(1 - u, 2.2f));
        }

        void GainXp(RunState r, int amount)
        {
            r.xp += amount;
            while (r.xp >= XpNeeded(r))
            {
                r.xp -= XpNeeded(r); r.level++; r.augmentQueue++;
                r.player.levelUp = db.visual.levelUpTime;   // 몸에 초록 기운 (증강은 라운드가 끝난 뒤 고른다)
                Event(r, "levelup");
            }
        }

        void DropPiece(RunState r, Enemy e, Drop d)
        {
            RewardConfig rw = db.rewards;
            float angle = r.random() * Mathf.PI * 2;
            float speed = rw.dropPopSpeed * (.45f + r.random() * .55f);
            d.x = e.x; d.y = e.y;
            d.vx = Mathf.Cos(angle) * speed;
            d.vy = Mathf.Sin(angle) * speed / WorldVertical;
            d.age = -r.random() * .08f;          // 조금씩 엇갈려 빨려 든다
            r.drops.Add(d);
        }

        // 드롭 한 알: 튀어나와 미끄러지다가, popTime 뒤에는 점점 빨라지며 몸으로 빨려 든다.
        bool UpdateDrop(RunState r, Drop d, float dt)
        {
            RewardConfig rw = db.rewards;
            Player p = r.player;
            d.age += dt;
            float gap = Distance(d, p);
            if (d.age >= rw.dropPopTime && gap < H.pickupRadius) { Collect(r, d); return true; }
            if (d.age < rw.dropPopTime)
            {
                float drag = Mathf.Exp(-6 * dt);
                d.x = Mathf.Clamp(d.x + d.vx * dt, A.wallLeft, A.wallRight);
                d.y = Mathf.Clamp(d.y + d.vy * dt, A.wallTop, A.wallBottom);
                d.vx *= drag; d.vy *= drag;
                return false;
            }
            d.speed = d.speed <= 0 ? rw.dropVacuumSpeed : d.speed + rw.dropVacuumAccel * dt;
            Vector2 dir = Unit(p.x - d.x, p.y - d.y);
            float step = Mathf.Min(gap, d.speed * dt);
            d.x += dir.x * step; d.y += dir.y * step;
            if (Distance(d, p) < H.pickupRadius) { Collect(r, d); return true; }
            return false;
        }

        void Collect(RunState r, Drop drop)
        {
            // 몸에 닿는 순간 작은 반짝임 (경험치는 초록, 골드는 금빛)
            RewardConfig rw = db.rewards;
            if (drop.xp > 0 || drop.gold > 0)
            {
                Fx glint = Spawn(r, FxKind.Burst, r.player.x + (r.random() - .5f) * 14, r.player.y + (r.random() - .5f) * 8, X.pickup);
                glint.color = drop.xp > 0 ? rw.xpColor : rw.goldColor;
                glint.lift = 20;
            }
            if (drop.xp > 0 || drop.gold > 0) Event(r, drop.xp > 0 ? "xp" : "gold");
            if (drop.heal > 0)
            {
                // 하트: 최대 체력 비율만큼 회복하고, 몸에 피가 차오르는 연출을 켠다.
                float amount = Round(r.maxHp * drop.heal);
                float healed = Mathf.Min(r.maxHp, r.hp + amount) - r.hp;
                r.hp += healed;
                r.player.heal = rw.healTime;
                Fx number = Spawn(r, FxKind.Number, r.player.x, r.player.y, X.damageNumber);
                number.text = "+" + Mathf.Max(0, healed).ToString("0"); number.color = rw.healColor; number.lift = 44;
                Event(r, "heal");
            }
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
            int xp = boss ? rw.xpBoss : e.elite ? rw.xpElite : rw.xpNormal;
            int gold = (int)(e.type.gold * (e.elite ? E.eliteGold : 1));
            // 경험치 구·골드를 여러 알로 나눠 튀어나오게 한다 (합계는 그대로).
            int orbs = Mathf.Clamp(xp, xp > 0 ? 1 : 0, Mathf.Max(1, rw.maxXpOrbs));
            for (int i = 0; i < orbs; i++) DropPiece(r, e, new Drop { xp = xp / orbs + (i < xp % orbs ? 1 : 0) });
            int coins = Mathf.Clamp(gold, gold > 0 ? 1 : 0, Mathf.Max(1, rw.maxCoins));
            for (int i = 0; i < coins; i++) DropPiece(r, e, new Drop { gold = gold / coins + (i < gold % coins ? 1 : 0) });
            // 낮은 확률로 체력 회복 하트 (정예는 조금 더 자주)
            if (r.random() < (e.elite ? rw.healEliteChance : rw.healChance)) DropPiece(r, e, new Drop { heal = rw.healAmount });
            int shard = boss ? rw.shardBoss : e.elite ? rw.shardElite : 0;
            bool item = (boss && rw.bossDropsItem) || (e.elite && r.random() < rw.eliteItemChance);
            if (shard > 0 || item) DropPiece(r, e, new Drop { shard = shard, item = item });
            if (r.desires.Contains("hunger")) r.hp = Mathf.Min(r.maxHp, r.hp + db.desire.hungerHeal);
            if (stuck.Count > 0 && r.desires.Contains("obsession"))
            {
                Enemy nearby = r.enemies.Where(o => o.hp > 0 && o != e).OrderBy(o => Distance(o, e)).FirstOrDefault();
                if (nearby != null) r.strands.AddRange(stuck.Select(s => { Strand copy = s.Copy(); copy.target = nearby; return copy; }));
            }
            if (boss) { StartBossDeath(r, e); return; }
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
            p.shotCooldown = r.fireInterval;
            p.facing = dir.x >= 0 ? 1 : -1;
            p.angle = Mathf.Atan2(dir.y, dir.x);
            Act(p, HeroAction.Shoot);
            // 털실 1개로 바늘 1 + ExtraNeedles 개를 부채꼴로 (가운데가 겨눈 방향, 화면 각도로 multishotSpread 간격)
            float screenAngle = Mathf.Atan2(dir.y, dir.x);
            int needles = 1 + ExtraNeedles(r);
            for (int i = 0; i < needles; i++)
            {
                int slot = i == 0 ? 0 : (i + 1) / 2 * (i % 2 == 1 ? -1 : 1);   // 0, -1, +1, -2, +2 ...
                float a = screenAngle + slot * H.multishotSpread;
                r.strands.Add(new Strand
                {
                    x = p.x, y = p.y, vx = Mathf.Cos(a), vy = Mathf.Sin(a) / A.projectionY, speed = db.needle.launch * db.needle.start,
                    range = r.reach, maxRange = r.reach, state = StrandState.Flight, bounce = r.rules.Contains("bounce"), birth = r.time, element = r.element,
                    extra = i > 0,
                });
            }
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
            needleScale = r.needleSpeed;
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
                // 덤으로 나간 갈래 바늘(extra)은 털실을 쓰지 않았으므로 돌려받는 셈에서 뺀다
                r.yarn = Mathf.Min(YarnMax(r), r.yarn + Mathf.Ceil(strands.Count(s => !s.extra) / 2f));
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
            float hitPad = ThreadHitPad(r);   // 굵은 실일수록 실 선분 둘레가 넓게 벤다
            foreach (Strand s in strands)
            {
                var (start, end) = Endpoints(p, s);
                Element element = s.element ?? r.element;
                Fx snap = Spawn(r, FxKind.Pull, start.x, start.y, X.pullStrand);
                snap.toX = end.x; snap.toY = end.y; snap.element = element; snap.rage = rage; snap.seed = r.random() * 100;
                snap.followHero = s.anchor == null;   // 손에 든 실은 지금 손 위치로 감겨 들어온다 (당긴 자리에 실이 남지 않게)
                float along = Mathf.Atan2((end.y - start.y) * A.projectionY, end.x - start.x);
                Fx blade = Spawn(r, FxKind.Cut, start.x, start.y, new FxSpec { time = F.cutTime, alpha = 1 });
                blade.toX = end.x; blade.toY = end.y; blade.angle = along; blade.element = element; blade.followHero = s.anchor == null;
                var a = new Point(start.x, start.y / WorldVertical);
                var b = new Point(end.x, end.y / WorldVertical);
                foreach (Enemy e in living)
                {
                    if (e != s.target && LineDistance(e.x, e.y / WorldVertical, a, b) > e.radius + hitPad) continue;
                    if (!direct.ContainsKey(e)) { direct[e] = 0; order.Add(e); }
                    direct[e]++;
                    if (!cutAngle.ContainsKey(e) || e == s.target) cutAngle[e] = along;
                    impacts++;
                }
            }
            float damage = r.damage * (rage ? db.desire.rageMultiplier : 1) * (r.mood == Mood.Awakened ? db.desire.awakenDamage : 1)
                * (1 + StrandBonus(strands.Count));   // 실을 많이 걸어 둘수록 한 번에 세게 당긴다
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
                    SpawnEnemy(r, type, new Point(Mathf.Clamp(e.x + dx, A.enemyMarginX, A.width - A.enemyMarginX),
                        Mathf.Clamp(e.y + E.summonOffset.y, A.enemyMarginY, A.height - A.enemyMarginY)));
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
            if (r.phase == Phase.Victory || r.outcome != Outcome.None) return;
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
            p.levelUp = Mathf.Max(0, p.levelUp - dt);
            p.heal = Mathf.Max(0, p.heal - dt);
            p.dashGrace = Mathf.Max(0, p.dashGrace - dt);
            p.hurt = Mathf.Max(0, p.hurt - dt);
            p.shotCooldown = Mathf.Max(0, p.shotCooldown - dt);
            r.dry = Mathf.Max(0, r.dry - dt);
            if (r.echo != null && r.echo.until <= r.time) r.echo = null;
            if (r.stairs != null)
            {
                // 계단이 꼭대기부터 한 단씩 '다라라락' 놓인다: 조각이 자리 잡는 순간마다 소리 이벤트.
                float before = r.stairsAge;
                r.stairsAge += dt;
                for (int k = 0; k < R.stairPieces; k++)
                {
                    float landed = k * R.stairStepDelay + R.stairSettle;
                    if (before < landed && r.stairsAge >= landed) Event(r, "stairstep");
                }
            }

            Vector2 move = Unit(input.x, input.y);
            bool moving = input.x != 0 || input.y != 0;
            // 대쉬 칸: 비어 있는 칸은 하나씩 차오른다 (dashCooldown × 대쉬 충전 능력치).
            int dashMax = Mathf.Max(1, F.dashCharges);
            if (p.dashCharges < dashMax)
            {
                p.dashCharge += dt / Mathf.Max(.05f, F.dashCooldown * r.dashRate);
                if (p.dashCharge >= 1) { p.dashCharges++; p.dashCharge = p.dashCharges < dashMax ? p.dashCharge - 1 : 0; }
            }
            else p.dashCharge = 0;
            if (input.dash && p.dash <= 0 && p.dashCooldown <= 0 && p.dashCharges > 0) StartDash(r, moving, move);
            if (p.dash > 0) UpdateDash(r, dt);
            else
            {
                p.motion += ((moving ? 1 : 0) - p.motion) * Mathf.Min(1, dt * H.motionLerp);
                float factor = Mathf.Max(H.minSpeedFactor, 1 - r.knots.Count * r.encumbrance);
                float speed = H.moveSpeed * (1 + r.Gear(StatKind.Speed)) * r.moveScale * factor * (r.mood == Mood.Frenzy ? H.frenzySpeed : 1);
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
            foreach (Enemy dead in r.enemies.Where(e => e.hp <= 0).ToList()) Retire(r, dead);

            if (r.phase == Phase.Combat && r.roundKind == RoundKind.Survival && r.elapsed < r.roundLength) UpdateSpawns(r, dt);
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
            r.drops.RemoveAll(d => UpdateDrop(r, d, dt));
            if (r.outcome != Outcome.None) return;
            switch (r.phase)
            {
                case Phase.Combat when r.roundKind == RoundKind.Survival && r.elapsed >= r.roundLength: ClearRound(r); break;
                case Phase.Combat when r.roundKind == RoundKind.Boss && !r.enemies.Any(e => e.IsBoss): BeginPurify(r); break;
                case Phase.BossDying:
                    BossDeathConfig bd = db.bossDeath;
                    float before = r.bossDeath.time;
                    r.bossDeath.time += dt;
                    // 잘리는 순간: 화면 섬광·흔들림·효과음
                    if (before < bd.wrapTime && r.bossDeath.time >= bd.wrapTime)
                    {
                        r.shake = Mathf.Max(r.shake, bd.shake * 1.6f);
                        r.snapFlash = F.cutFlash;
                        r.freeze = Mathf.Max(r.freeze, F.freezeMax);
                        Event(r, "bosscut");
                    }
                    if (r.bossDeath.time >= bd.Total) BeginPurify(r);
                    break;
                case Phase.Purifying:
                    r.purifyTime += dt;
                    if (r.purifyTime >= db.purify.duration)
                    {
                        r.purified = true;
                        r.phase = Phase.Cleared;
                        r.stairs = Near(r, R.stairsOffset);
                        r.stairsAge = 0;
                    }
                    break;
            }
        }

        void StartDash(RunState r, bool moving, Vector2 move)
        {
            Player p = r.player;
            Vector2 dir = moving ? move : new Vector2(-Mathf.Cos(p.angle), -Mathf.Sin(p.angle));
            p.dx = dir.x; p.dy = dir.y;
            p.dash = F.dashDuration;
            p.dashDone = 0;
            p.dashCharges--;
            p.dashCooldown = F.dashMinCooldown;
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
