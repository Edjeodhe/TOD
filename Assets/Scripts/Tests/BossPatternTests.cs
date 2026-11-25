// 23차-boss7 검증 (EditMode): 칠죄종 보스 7종의 데이터, 예고 판정, 2페이즈 전환, 자동 전투로 잰 보스전 길이.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TOD.Tests
{
    public class BossPatternTests
    {
        static readonly Element[] Sins = { Element.Pride, Element.Greed, Element.Envy, Element.Wrath, Element.Lust, Element.Gluttony, Element.Sloth };

        GameDatabase db;
        Combat combat;
        Scene opened;
        string realSaveKey;

        [OneTimeSetUp]
        public void LoadDatabase()
        {
            db = Object.FindAnyObjectByType<GameDatabase>();
            if (db == null)
            {
                opened = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Additive);
                db = Object.FindAnyObjectByType<GameDatabase>();
            }
            Assert.IsNotNull(db, "Main 씬의 GameDatabase 를 찾지 못했습니다");
            realSaveKey = db.gear.saveKey;
            db.gear.saveKey = realSaveKey + ".tests";
            Space.Configure(db.arena);
            combat = new Combat(db);
        }

        [OneTimeTearDown]
        public void Close()
        {
            PlayerPrefs.DeleteKey(db.gear.saveKey);
            db.gear.saveKey = realSaveKey;
            if (opened.IsValid()) EditorSceneManager.CloseScene(opened, true);
        }

        RunState BossRun(Element sin, int floor = 1, int seed = 7)
        {
            var rng = new System.Random(seed);
            RunState r = combat.CreateRun(new Profile { nextId = 1 }, () => (float)rng.NextDouble());
            combat.DevGoToFloor(r, floor, sin);
            Assert.IsTrue(combat.DevGoToRound(r, RoundKind.Boss));
            r.enemies.RemoveAll(e => !e.IsBoss);
            return r;
        }

        static Enemy Boss(RunState r) => r.enemies.First(e => e.IsBoss);

        // 7죄종 모두 따로 그린 리그, 1페이즈 3개 이상, 2페이즈 3개 이상(새 패턴 포함), 체력이 예전 공용 보스보다 훨씬 많다
        [Test]
        public void SevenDistinctBosses()
        {
            Assert.IsNotNull(db.bosses?.defs);
            var rigs = new HashSet<string>();
            var names = new HashSet<string>();
            MonsterRig[] templates = Object.FindObjectsByType<MonsterRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Element sin in Sins)
            {
                BossDef d = db.bosses.Def(sin);
                Assert.IsNotNull(d, $"{sin} 보스 없음");
                Assert.IsTrue(rigs.Add(d.rig), $"{sin} 리그가 겹침");
                Assert.IsTrue(names.Add(d.name), $"{sin} 이름이 겹침");
                Assert.IsTrue(templates.Any(t => t.type == d.rig), $"{sin} 리그 템플릿 {d.rig} 없음");
                Assert.GreaterOrEqual(d.moves.Length, 3, $"{sin} 1페이즈 패턴");
                Assert.GreaterOrEqual(d.moves2.Length, 3, $"{sin} 2페이즈 패턴");
                Assert.IsTrue(d.moves2.Any(m2 => d.moves.All(m1 => m1.kind != m2.kind)), $"{sin} 2페이즈에 새 패턴 종류");
                Assert.Greater(d.hp, db.Enemy("boss").hp * 3, $"{sin} 체력");
                Assert.IsTrue(d.moves.Concat(d.moves2).All(m => m.windup >= .6f), $"{sin} 예고가 너무 짧음");
            }
        }

        // 예고 안에 있으면 맞고, 밖이거나 대쉬 중이면 안 맞는다
        [Test]
        public void TelegraphHitsOnlyInside()
        {
            RunState r = BossRun(Element.Greed);
            Enemy boss = Boss(r);
            r.player.x = boss.x + 400; r.player.y = boss.y;
            float before = r.hp;
            r.telegraphs.Add(new BossTelegraph { shape = TeleShape.Circle, x = r.player.x, y = r.player.y, radius = 100, time = .2f, max = .2f, damage = 20, owner = boss });
            r.telegraphs.Add(new BossTelegraph { shape = TeleShape.Circle, x = r.player.x + 500, y = r.player.y, radius = 100, time = .2f, max = .2f, damage = 20, owner = boss });
            boss.brain.cooldown = 99;
            for (int i = 0; i < 30; i++) combat.Update(r, 1 / 60f, new CombatInput());
            Assert.AreEqual(before - 20, r.hp, 1, "안에 있던 원 하나만 맞음");

            r.player.hurt = 0;
            before = r.hp;
            r.telegraphs.Add(new BossTelegraph { shape = TeleShape.Circle, x = r.player.x, y = r.player.y, radius = 100, time = .05f, max = .05f, damage = 20, owner = boss });
            combat.Update(r, 1 / 60f, new CombatInput { dash = true, x = 1 });
            for (int i = 0; i < 6; i++) combat.Update(r, 1 / 60f, new CombatInput());
            Assert.AreEqual(before, r.hp, "대쉬로 피함");

            var cone = new BossTelegraph { shape = TeleShape.Cone, x = 0, y = 0, angle = 0, radius = 100, width = Mathf.PI / 2 };
            Assert.IsTrue(Combat.Inside(cone, 50, 10));
            Assert.IsFalse(Combat.Inside(cone, -50, 0));
            var line = new BossTelegraph { shape = TeleShape.Line, x = 0, y = 0, angle = Mathf.PI / 2, radius = 300, width = 40 };
            Assert.IsTrue(Combat.Inside(line, 10, 200));
            Assert.IsFalse(Combat.Inside(line, 30, 200));
            var ring = new BossTelegraph { shape = TeleShape.Ring, x = 0, y = 0, radius = 300, inner = 120 };
            Assert.IsFalse(Combat.Inside(ring, 50, 0), "고리 안쪽은 안전");
            Assert.IsTrue(Combat.Inside(ring, 200, 0));
        }

        // 1페이즈 체력이 0 → 경직(무적) → 포효로 주변 넉백 → 체력바가 다시 차오름 → 2페이즈. 2페이즈에서 0 이면 처치 연출.
        [Test]
        public void PhaseTwoTransition()
        {
            foreach (Element sin in Sins)
            {
                RunState r = BossRun(sin);
                Enemy boss = Boss(r);
                r.player.x = boss.x + 80; r.player.y = boss.y;
                Assert.IsNotNull(boss.brain, $"{sin} 칠죄종 보스");
                Assert.IsTrue(combat.DevBossPhase(r));
                Assert.IsTrue(r.enemies.Contains(boss), "1페이즈에서는 죽지 않는다");
                Assert.IsTrue(boss.brain.Shielded);
                Assert.Less(boss.hp / boss.maxHp, .01f, "체력바가 비어 있다");
                Assert.AreEqual(Phase.Combat, r.phase);
                float px = r.player.x;
                BossConfig c = db.bosses;
                int frames = Mathf.CeilToInt((c.staggerTime + Mathf.Max(c.roarTime, c.refillTime) + .2f) * 60);
                float midHp = -1;
                for (int i = 0; i < frames; i++)
                {
                    combat.Update(r, 1 / 60f, new CombatInput());
                    if (boss.brain.Shielded) combat.DevBind(r);   // 전환 중 때려도
                    if (i == Mathf.CeilToInt((c.staggerTime + c.refillTime * .5f) * 60)) midHp = boss.hp / boss.maxHp;
                }
                Assert.Greater(r.player.x - px, 60, $"{sin} 포효에 밀려남");
                Assert.That(midHp, Is.InRange(.2f, .8f), $"{sin} 체력바가 차오르는 중");
                Assert.IsFalse(boss.brain.Shielded);
                Assert.AreEqual(2, boss.brain.phase);
                Assert.AreEqual(1f, boss.hp / boss.maxHp, .001f, $"{sin} 2페이즈 체력 가득");
                combat.DevEndRound(r);
                Assert.AreEqual(Phase.BossDying, r.phase, $"{sin} 2페이즈 처치");
            }
        }

        // 자동 전투: 주인공이 보스 둘레를 돌며 쏘고, 실 6가닥이면 당기고, 예고 안이면 밖으로 빠지다가 터지기 직전엔 대쉬.
        // 장비 없는 1층 기준 보스전 길이와 받은 피해를 잰다 (HANDOFF 23차-boss7 에 기록).
        [Test]
        public void BotFightLength()
        {
            var lines = new List<string>();
            var first = new Dictionary<Element, float>();
            foreach (int floor in new[] { 1, 4 })
                foreach (Element sin in Sins)
                {
                    var times = new List<float>();
                    var taken = new List<float>();
                    var deaths = 0;
                    for (int seed = 1; seed <= 3; seed++)
                    {
                        (float time, float hurt, bool died) = Fight(sin, floor, seed);
                        times.Add(time); taken.Add(hurt);
                        if (died) deaths++;
                    }
                    lines.Add($"{floor}층 {sin}: {times.Average():0}초 ({times.Min():0}~{times.Max():0}), 받은 피해 {taken.Average():0}, 사망 {deaths}/3");
                    if (floor == 1) first[sin] = times.Average();
                }
            Debug.Log("[boss7] 자동 전투\n" + string.Join("\n", lines));
            foreach (var kv in first) Assert.That(kv.Value, Is.InRange(45f, 150f), $"{kv.Key} 1층 보스전 길이\n" + string.Join("\n", lines));
        }

        (float time, float hurt, bool died) Fight(Element sin, int floor, int seed)
        {
            RunState r = BossRun(sin, floor, seed);
            r.devGod = false;
            Enemy boss = Boss(r);
            float hurt = 0, hp = r.hp;
            float t = 0;
            const float dt = 1 / 60f;
            float orbit = seed;
            while (t < 300 && r.phase == Phase.Combat && r.outcome == Outcome.None)
            {
                r.enemies.RemoveAll(e => !e.IsBoss && e.hp > 0 && Combat.Distance(e, r.player) > 2000);
                var input = new CombatInput { shoot = true, aim = new Vector2(boss.x, boss.y) };
                Player p = r.player;
                Vector2 move = Vector2.zero;
                bool danger = false;
                foreach (BossTelegraph tel in r.telegraphs)
                {
                    if (tel.resolved || tel.delay > 0 || (tel.damage <= 0 && tel.shape != TeleShape.Line)) continue;
                    if (!Combat.Inside(tel, p.x, p.y) && !(tel.shape == TeleShape.Line && Combat.Inside(Wider(tel, 50), p.x, p.y))) continue;
                    danger = true;
                    Vector2 away = new(p.x - tel.x, p.y - tel.y);
                    if (tel.shape == TeleShape.Line) { var dir = new Vector2(Mathf.Cos(tel.angle), Mathf.Sin(tel.angle)); away = new Vector2(-dir.y, dir.x) * Mathf.Sign(Vector2.Dot(away, new Vector2(-dir.y, dir.x)) + .01f); }
                    if (tel.shape == TeleShape.Ring) away = -away;   // 고리는 안쪽이 안전
                    move += away.normalized;
                    if (tel.Progress > .8f && p.dashCharges > 0) input.dash = true;
                }
                foreach (Bullet b in r.bullets)
                    if (Mathf.Abs(b.x - p.x) + Mathf.Abs(b.y - p.y) < 70) { input.dash = true; move += new Vector2(-b.vy, b.vx).normalized; danger = true; }
                if (!danger)
                {
                    // 보스 둘레 260px 로 천천히 돈다
                    orbit += dt * .5f;
                    Vector2 goal = new Vector2(boss.x, boss.y) + new Vector2(Mathf.Cos(orbit), Mathf.Sin(orbit)) * 260;
                    move = goal - new Vector2(p.x, p.y);
                    if (move.magnitude < 15) move = Vector2.zero;
                }
                if (move != Vector2.zero) { move.Normalize(); input.x = move.x; input.y = move.y; }
                int stuck = r.strands.Count(s => s.state != StrandState.Flight);
                if (stuck >= 6 || (r.yarn < 1 && stuck > 0)) input.pull = true;
                combat.Update(r, dt, input);
                if (r.hp < hp) hurt += hp - r.hp;
                hp = r.hp;
                t += dt;
                if (!r.enemies.Contains(boss)) break;
            }
            return (t, hurt, r.outcome == Outcome.Dead);
        }

        static BossTelegraph Wider(BossTelegraph t, float pad) => new() { shape = t.shape, x = t.x, y = t.y, angle = t.angle, radius = t.radius, width = t.width + pad };
    }
}
