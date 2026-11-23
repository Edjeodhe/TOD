// 전투 로직 검증 (EditMode). 값은 Main 씬의 GameDatabase 를 그대로 쓴다.
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TOD.Tests
{
    public class CombatTests
    {
        GameDatabase db;
        Combat combat;
        Scene opened;

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
            // 처치 보상·층 돌파 때 프로필을 저장하므로 실제 저장 키를 건드리지 않게 바꿔 둔다.
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

        string realSaveKey;

        static Profile EmptyProfile() => new Profile { nextId = 1 };

        RunState NewRun(float roll = .5f) => combat.CreateRun(EmptyProfile(), () => roll);

        Enemy Add(RunState r, string type, float x, float y)
        {
            EnemyType def = db.Enemy(type);
            var e = new Enemy { x = x, y = y, type = def, hp = def.hp, maxHp = def.hp, radius = def.radius, speed = 0, damage = 0, wait = 99, action = EnemyAction.Move };
            e.height = combat.EnemyHeight(e);
            r.enemies.Add(e);
            return e;
        }

        // HANDOFF 3.3 예시: A에 바늘 1발, 바닥 실 두 가닥이 각각 B·C를 통과 → 총 5타, A 70 / B·C 각 28.
        [Test]
        public void PullTransfersAllHitsToPinnedEnemy()
        {
            RunState r = NewRun();
            r.player.x = 500; r.player.y = 350;
            Enemy a = Add(r, "boss", 700, 350);
            Enemy b = Add(r, "charger", 500, 250);
            Enemy c = Add(r, "charger", 500, 180);
            r.strands.Add(new Strand { x = a.x, y = a.y, target = a, state = StrandState.Stuck, element = Element.Plain });
            r.strands.Add(new Strand { x = 500, y = 100, state = StrandState.Ground, element = Element.Plain });
            r.strands.Add(new Strand { x = 505, y = 100, state = StrandState.Ground, element = Element.Plain });
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
            Assert.AreEqual(5, r.combo, "총 타격 수");
            Assert.AreEqual(a.maxHp - 70, a.hp, "A (바늘이 꽂힌 적)");
            Assert.AreEqual(b.maxHp - 28, b.hp, "B");
            Assert.AreEqual(c.maxHp - 28, c.hp, "C");
            Assert.AreEqual(0, r.strands.Count, "당기면 실을 모두 회수");
        }

        [Test]
        public void SetGroupsUseLargestConnectedCluster()
        {
            Profile p = EmptyProfile();
            p.items.Add(new OwnedItem { id = 1, kind = 0 });   // 붉은 골무 (혼돈 2칸)
            p.items.Add(new OwnedItem { id = 2, kind = 1 });   // 잊힌 매듭 (혼돈 3칸)
            p.items.Add(new OwnedItem { id = 3, kind = 2 });   // 낡은 단추 (좌절 1칸)
            p.placements.Add(new Placement { id = 1, x = 0, y = 0 });
            p.placements.Add(new Placement { id = 2, x = 2, y = 0 });
            p.placements.Add(new Placement { id = 3, x = 5, y = 3 });
            GearStats stats = Gear.Stats(db, p);
            Assert.AreEqual(2, stats.groups["chaos"].size);
            Assert.AreEqual(1, stats.groups["chaos"].tier);
            Assert.AreEqual(1, stats.groups["frustration"].tier);
            Assert.AreEqual(0, stats.groups["despair"].tier);
            Assert.AreEqual(Element.Chaos, Gear.ThreadElement(stats));
            Assert.IsFalse(Gear.CanPlace(db, p, p.items[0], 0, 2, 0), "겹치는 칸에는 놓을 수 없다");
        }

        [Test]
        public void DesireFrenzyThenAbsorbed()
        {
            RunState r = NewRun(.9f);   // 각성 확률(.25)보다 큰 값 → 폭주
            combat.AdjustDesire(r, r.maxDesire);
            Assert.AreEqual(Mood.Frenzy, r.mood);
            Assert.AreEqual(r.maxDesire * db.desire.frenzyReset, r.desire, 1e-3);
            combat.AdjustDesire(r, r.maxDesire);
            Assert.AreEqual(Outcome.Absorbed, r.outcome, "폭주 중 다시 100% → 탑에 동화");
        }

        [Test]
        public void AugmentChoicesAreUniqueAndSkipOwnedRules()
        {
            RunState r = NewRun();
            r.rules.Add("bounce");
            for (int i = 0; i < 50; i++)
            {
                RunState run = combat.CreateRun(EmptyProfile(), () => Random.value);
                run.rules.Add("bounce");
                var picks = combat.AugmentChoices(run);
                Assert.AreEqual(3, picks.Select(p => p.id).Distinct().Count());
                Assert.IsFalse(picks.Any(p => p.id == "bounce"));
            }
        }

        // 자동 조작으로 5층 끝(엔딩)까지 진행되는지: 라운드·보스·욕망 제안·상점 흐름을 모두 통과해야 한다.
        [Test]
        public void ClimbReachesVictory()
        {
            Random.InitState(7);
            RunState r = combat.CreateRun(EmptyProfile(), () => Random.value);
            combat.BeginFloor(r);
            int steps = 0;
            while (steps++ < 400000 && r.outcome == Outcome.None && r.phase != Phase.Victory)
            {
                r.hp = r.maxHp = 100000;                       // 생존을 보장해 진행 흐름만 본다
                if (r.desire > r.maxDesire * .8f) r.desire = 0;   // 동화 엔딩 대신 등반 흐름을 본다
                while (r.augmentQueue > 0) combat.ApplyAugment(r, combat.AugmentChoices(r)[0].id);
                switch (r.phase)
                {
                    case Phase.Rest: r.round++; combat.BeginRound(r); continue;
                    case Phase.Desire: combat.ChooseDesire(r, null); continue;
                    case Phase.Shop: r.floor++; combat.BeginFloor(r); continue;
                }
                Enemy target = r.enemies.OrderBy(e => (e.x - r.player.x) * (e.x - r.player.x) + (e.y - r.player.y) * (e.y - r.player.y)).FirstOrDefault();
                var input = new CombatInput
                {
                    shoot = target != null, aim = target != null ? new Vector2(target.x, target.y) : Vector2.zero,
                    pull = r.strands.Count(s => s.state != StrandState.Flight) >= 2 || (r.yarn < 1 && r.strands.Count > 0),
                };
                combat.Update(r, 1 / 60f, input);
            }
            Assert.AreEqual(Phase.Victory, r.phase, $"{r.floor}층 {r.round}라운드에서 멈춤 (steps {steps})");
            Assert.AreEqual(db.arena.finalFloor, r.floor);
            Assert.Greater(r.kills, 0);
        }
    }
}
