// 16차: 메인/부가 세트, 층 속성과 상성, 보스 속성 공격. 수치는 GameDatabase(gear.sets · elements)에서 읽는다.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOD
{
    public partial class Combat
    {
        ElementConfig EC => db.elements;

        // ---------------------------------------------------------------- 메인 / 부가 세트

        // 메인 세트 = 판에 놓인 조각 수가 가장 많은 켜진 세트 (Gear.MainFirst 순서의 맨 앞). 없으면 null = 무속성.
        public ActiveSet MainSet(RunState r) => r.gearStats != null && r.gearStats.sets.Count > 0 ? r.gearStats.sets[0] : null;
        public IEnumerable<ActiveSet> SubSets(RunState r) => r.gearStats == null ? Enumerable.Empty<ActiveSet>() : r.gearStats.sets.Skip(1);

        public int ProcCharge(RunState r, ActiveSet set) => r.procCharges.TryGetValue(set.def, out int n) ? n : 0;
        public bool ProcReady(RunState r, ActiveSet set) => set.def.procCharge > 0 && ProcCharge(r, set) >= set.def.procCharge;

        // 적 하나를 때릴 때마다 (바늘 명중 · 당기기 타격): 준비된 부가 세트는 이 적에게 터지고, 아니면 한 칸 찬다.
        // kill = false 이면 발동 피해로 죽어도 처치는 부르는 쪽(당기기)이 한다.
        void HitProcs(RunState r, Enemy e, bool kill)
        {
            foreach (ActiveSet set in SubSets(r).ToList())
            {
                if (set.def.procCharge <= 0) continue;
                if (ProcReady(r, set))
                {
                    if (e.hp <= 0) continue;   // 준비된 채로 다음 타격을 기다린다
                    r.procCharges[set.def] = 0;
                    FireProc(r, e, set, kill);
                }
                else r.procCharges[set.def] = ProcCharge(r, set) + 1;
            }
        }

        // 부가 세트 발동: 피해 + 그 속성 효과 한 번 + 이펙트
        void FireProc(RunState r, Enemy e, ActiveSet set, bool kill)
        {
            SetDef d = set.def;
            float v = d.effectPerTier * set.tier * d.procEffectScale;
            float amount = d.procDamage + d.procDamagePerTier * set.tier;
            if (d.element == Element.Pride && (e.elite || e.IsBoss || e.hp >= e.maxHp - .01f)) amount *= 1 + v;   // 교만: 정예·보스·체력 가득한 적에게 더 세게
            Fx fx = Spawn(r, FxKind.Proc, e.x, e.y, new FxSpec { time = EC.procTime, alpha = 1 });
            fx.element = d.element; fx.lift = EnemyHeight(e) * .5f; fx.seed = r.random() * 100; fx.follow = e;
            fx.size = (e.IsBoss ? X.bossScale : 1);
            fx.text = EC.Def(d.element)?.procText;
            float dealt = DamageEnemy(r, e, amount, d.element, kill, EnemyHeight(e) * .5f + db.visual.elementNumberLift);
            ApplyElementEffect(r, e, d, v, dealt, kill);
            r.shake = Mathf.Max(r.shake, EC.procShake);
            r.flash = Mathf.Max(r.flash, EC.procFlash);
            r.flashTint = db.Tone(d.element).glow;
            Event(r, "proc");
        }

        // 죄종 효과 한 번 (부가 발동): 분노 화상 · 나태 둔화와 잠 겹 · 탐욕 저당 · 시기 독 · 색욕 매혹과 끌림 · 식탐 회복
        void ApplyElementEffect(RunState r, Enemy e, SetDef d, float v, float dealt, bool kill)
        {
            Player p = r.player;
            switch (d.element)
            {
                case Element.Envy: SinStatus(r, e, d, 1, v, Mathf.Max(1, (int)d.effectCap / 2)); break;
                case Element.Lust:
                    foreach (Enemy o in r.enemies.Where(o => o.hp > 0 && (o == e || Distance(o, e) <= d.effectRange)))
                    {
                        float gap = Mathf.Max(1, Mathf.Sqrt((p.x - o.x) * (p.x - o.x) + (p.y - o.y) * (p.y - o.y) * A.projectionY * A.projectionY));
                        float yank = Mathf.Min(F.yankReach * (1 + v), Mathf.Max(0, gap - F.yankMinGap));
                        o.yank = new Timed { x = (p.x - o.x) / gap * yank, y = (p.y - o.y) / gap * yank, time = F.yankTime };
                        o.charmTime = Mathf.Max(o.charmTime, d.effectTime);
                    }
                    break;
                case Element.Gluttony: Leech(r, v, dealt); break;
                default: SinStatus(r, e, d, 1, v, 1); break;
            }
        }

        // ---------------------------------------------------------------- 층 속성 · 상성

        public float Affinity(Element attack, Element target) => EC == null ? 1 : EC.Multiplier(attack, target);
        public int Relation(Element attack, Element target) => EC == null ? 0 : EC.Relation(attack, target);

        // 층마다 8속성 중 하나 (한 등반 안에서 겹치지 않게, 다 쓰면 처음부터)
        void PickFloorElement(RunState r)
        {
            ElementDef[] defs = EC?.defs;
            if (defs == null || defs.Length == 0) { r.floorElement = Element.Plain; return; }
            List<Element> pool = defs.Select(d => d.element).Where(e => !r.floorElements.Contains(e)).ToList();
            if (pool.Count == 0) { r.floorElements.Clear(); pool = defs.Select(d => d.element).ToList(); }
            Element pick = pool[Mathf.Min(pool.Count - 1, Mathf.FloorToInt(r.random() * pool.Count))];
            if (r.floor <= 1 && r.fate > 0 && pool.Contains((Element)(r.fate - 1))) pick = (Element)(r.fate - 1);   // 19차: 모이라가 읽어 둔 1층
            if (devFloorElement is Element dev) pick = dev;   // 23차 개발자 모드: 죄종 층으로 바로 가기 (이미 나온 죄종이어도)
            r.floorElements.Add(pick);
            r.floorElement = pick;
        }

        // 개발자 모드: 지금 층 속성을 바꾼다 (살아 있는 몬스터도 바뀌고, 암시 문구를 다시 띄운다)
        public void DevSetFloorElement(RunState r, Element element)
        {
            r.floorElement = element;
            if (!r.floorElements.Contains(element)) r.floorElements.Add(element);
            foreach (Enemy e in r.enemies) e.element = element;
            r.hintSerial++;
        }

        // ---------------------------------------------------------------- 보스 속성 공격 (주인공에게)

        // 적이 주인공을 때렸을 때: 보스면 속성 효과를 더한다 (일반 몬스터는 그대로)
        bool HitPlayer(RunState r, float amount, Enemy source)
        {
            if (!DamagePlayer(r, amount)) return false;
            if (source != null && source.IsBoss) BossElementHit(r, source, amount);
            return true;
        }

        void BossElementHit(RunState r, Enemy boss, float amount)
        {
            ElementDef def = EC?.Def(boss.element);
            BossElementAttack a = def?.boss;
            if (a == null) return;
            Player p = r.player;
            switch (boss.element)
            {
                case Element.Wrath: p.burnDps = Mathf.Max(p.burnDps, a.value); p.burnTime = a.time; break;
                case Element.Sloth: p.slow = Mathf.Max(p.slow, Mathf.Clamp(a.value, 0, .9f)); p.slowTime = a.time; break;
                case Element.Pride: p.blind = p.blindMax = a.time; p.blindPower = a.value; break;
                case Element.Greed: p.dark = p.darkMax = a.time; p.darkRadius = a.range; break;
                case Element.Gluttony:
                {
                    if (boss.hp <= 0) break;
                    float heal = Mathf.Min(boss.maxHp - boss.hp, Round(amount * a.value));
                    if (heal < 1) break;
                    boss.hp += heal;
                    Fx number = Spawn(r, FxKind.Number, boss.x, boss.y, X.damageNumber);
                    number.text = "+" + heal.ToString("0"); number.color = db.Tone(Element.Gluttony).color;
                    number.useElement = true; number.element = Element.Gluttony; number.lift = EnemyHeight(boss) * .6f;
                    break;
                }
                case Element.Lust:
                {
                    // 22차 색욕 보스: 밀쳐내는 대신 보스 쪽으로 홀린 듯 끌려간다 (range 는 끌리는 거리, 보스에 닿기 전까지)
                    Vector2 away = new(boss.x - p.x, (boss.y - p.y) * A.projectionY);
                    a = new BossElementAttack { value = a.value, time = a.time, count = a.count, range = Mathf.Min(a.range, Mathf.Max(0, away.magnitude - boss.radius - 30)) };
                    if (away.sqrMagnitude < 1e-4f) away = Vector2.right;
                    away.Normalize();
                    float time = Mathf.Max(.05f, a.time);
                    p.push = new Timed { x = away.x * a.range, y = away.y / A.projectionY * a.range, time = time, max = time };
                    break;
                }
                case Element.Envy:
                    p.poisonStacks = Mathf.Min(Mathf.Max(1, a.count), p.poisonStacks + 1);
                    p.poisonDps = a.value; p.poisonTime = a.time;
                    break;
            }
            Fx fx = Spawn(r, FxKind.BossHit, p.x, p.y, new FxSpec { time = EC.bossHitTime, size = EC.bossHitSize, alpha = 1 });
            fx.element = boss.element; fx.follow = p; fx.seed = r.random() * 100;
            Event(r, "bosselement");
        }

        // 주인공 상태: 화상·중독 도트, 감전 연쇄, 둔화·눈부심·어둠 시간, 바람에 밀려남
        void UpdatePlayerStatus(RunState r, float dt)
        {
            Player p = r.player;
            p.burnTime = Mathf.Max(0, p.burnTime - dt);
            p.slowTime = Mathf.Max(0, p.slowTime - dt);
            p.poisonTime = Mathf.Max(0, p.poisonTime - dt);
            p.blind = Mathf.Max(0, p.blind - dt);
            p.dark = Mathf.Max(0, p.dark - dt);
            if (p.burnTime <= 0) p.burnDps = 0;
            if (p.slowTime <= 0) p.slow = 0;
            if (p.poisonTime <= 0) { p.poisonStacks = 0; p.poisonDps = 0; }
            if (p.push != null)
            {
                float k = 1 - Mathf.Clamp01((p.push.time - dt) / p.push.max);
                float eased = 1 - Mathf.Pow(1 - k, 3);
                p.x = Mathf.Clamp(p.x + p.push.x * (eased - p.push.done), A.wallLeft, A.wallRight);
                p.y = Mathf.Clamp(p.y + p.push.y * (eased - p.push.done), A.wallTop, A.wallBottom);
                p.push.done = eased;
                if ((p.push.time -= dt) <= 0) p.push = null;
            }
            if (p.shockLeft > 0 && (p.shockTimer -= dt) <= 0)
            {
                p.shockLeft--;
                p.shockTimer = p.shockInterval;
                PlayerDot(r, p.shockDamage, Element.Unused3);
                Fx zap = Spawn(r, FxKind.BossHit, p.x, p.y, new FxSpec { time = EC.bossHitTime * .6f, size = EC.bossHitSize * .6f, alpha = 1 });
                zap.element = Element.Unused3; zap.follow = p; zap.seed = r.random() * 100;
            }
            float burn = p.burnDps * dt, poison = p.poisonDps * p.poisonStacks * dt;
            if (burn <= 0 && poison <= 0) { p.dotTick = 0; return; }
            p.dotPending += burn + poison;
            p.dotTick += dt;
            if (p.dotTick < Mathf.Max(.05f, EC.playerDotInterval)) return;
            p.dotTick = 0;
            float amount = p.dotPending;
            p.dotPending = 0;
            PlayerDot(r, amount, poison > burn ? Element.Envy : Element.Wrath);
        }

        // 지속 피해: 피격 무적·경직 없이 체력만 깎고 속성 글꼴 숫자를 띄운다
        void PlayerDot(RunState r, float amount, Element element)
        {
            if (r.devGod || amount <= 0 || r.outcome != Outcome.None) return;
            float dealt = Mathf.Max(1, Round(amount));
            r.hp = Mathf.Max(0, r.hp - dealt);
            Fx number = Spawn(r, FxKind.Number, r.player.x, r.player.y, X.hurtNumber);
            number.text = $"-{dealt}"; number.color = db.Tone(element).color; number.useElement = true; number.element = element;
            if (r.hp <= 0) { Act(r.player, HeroAction.Die); r.outcome = Outcome.Dead; }
        }
    }
}
