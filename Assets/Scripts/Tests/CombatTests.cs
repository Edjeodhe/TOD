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
            // 한 타 피해 = 실 피해 × (1 + 실 가닥 보너스). 예시(피해 14, 보너스 없음)에서는 A 70 / B·C 28.
            float hit = r.damage * (1 + combat.StrandBonus(3));
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
            Assert.AreEqual(5, r.combo, "총 타격 수");
            Assert.AreEqual(a.maxHp - Combat.Round(hit * 5), a.hp, "A (바늘이 꽂힌 적) = 5타");
            Assert.AreEqual(b.maxHp - Combat.Round(hit * 2), b.hp, "B = 2타");
            Assert.AreEqual(c.maxHp - Combat.Round(hit * 2), c.hp, "C = 2타");
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

        // 증강은 능력치 전용: 규칙형은 데이터에 없고, 고르면 바늘 능력치가 오른다.
        [Test]
        public void AugmentChoicesAreUniqueStatOnly()
        {
            for (int i = 0; i < 50; i++)
            {
                RunState run = combat.CreateRun(EmptyProfile(), () => Random.value);
                var picks = combat.AugmentChoices(run);
                Assert.AreEqual(3, picks.Select(p => p.id).Distinct().Count());
                Assert.IsTrue(picks.All(p => !p.rule && p.effects != null && p.effects.Length > 0), "능력치형 증강만 나온다");
            }
            RunState r = NewRun();
            float interval = r.fireInterval, reach = r.reach, maxHp = r.maxHp;
            r.augmentQueue = 3;
            combat.ApplyAugment(r, "attackSpeed");
            combat.ApplyAugment(r, "reach");
            combat.ApplyAugment(r, "maxHealth");
            Assert.Less(r.fireInterval, interval, "발사 간격이 줄어든다");
            Assert.Greater(r.reach, reach, "사거리가 늘어난다");
            Assert.Greater(r.maxHp, maxHp, "최대 체력이 늘어난다");
            Assert.AreEqual(r.maxHp, r.hp, "늘어난 만큼 체력도 찬다");
            Assert.AreEqual(1, r.augmentsTaken["reach"]);
            Assert.AreEqual(0, r.augmentQueue);
        }

        // 실을 많이 걸어 둘수록 당길 때 한 타의 피해가 커진다 (상한 있음).
        [Test]
        public void MoreStrandsHitHarder()
        {
            Assert.AreEqual(0, combat.StrandBonus(1), 1e-4, "한 가닥은 기본 피해");
            Assert.Greater(combat.StrandBonus(4), combat.StrandBonus(2));
            Assert.AreEqual(db.hero.strandBonusMax, combat.StrandBonus(100), 1e-4, "상한");
        }

        // 보스를 잡으면 바로 정화되지 않고 묶임→잘림 연출 단계를 거친 뒤, 보스가 있던 자리에서 정화가 퍼진다.
        [Test]
        public void BossDeathPlaysBeforePurify()
        {
            RunState r = NewRun();
            r.roundKind = RoundKind.Boss;
            r.player.x = 500; r.player.y = 350;
            Enemy boss = Add(r, "boss", 700, 300);
            r.strands.Add(new Strand { x = boss.x, y = boss.y, target = boss, state = StrandState.Stuck, element = Element.Plain });
            boss.hp = 1;
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(700, 300) });
            Assert.AreEqual(Phase.BossDying, r.phase);
            Assert.IsNotNull(r.bossDeath);
            for (int i = 0; i < 60 * 4 && r.phase == Phase.BossDying; i++) combat.Update(r, 1 / 60f, new CombatInput());
            Assert.AreEqual(Phase.Purifying, r.phase);
            Assert.AreEqual(700, r.purifyCenter.x, 1e-3, "정화는 보스 자리에서 퍼진다");
        }

        // 쉬어가기 탑의 아이템 3개는 한 번만 다시 굴릴 수 있다.
        [Test]
        public void TowerRerollsOnce()
        {
            RunState r = combat.CreateRun(EmptyProfile(), () => Random.value);
            r.artifactOffer.AddRange(new[] { 0, 1, 2 });
            Assert.IsTrue(combat.RerollArtifacts(r));
            Assert.AreEqual(db.rounds.artifactChoices, r.artifactOffer.Count);
            Assert.IsFalse(r.artifactOffer.Contains(0) && r.artifactOffer.Contains(1) && r.artifactOffer.Contains(2), "다른 조각이 섞여 나온다");
            Assert.IsFalse(combat.RerollArtifacts(r), "두 번째는 안 된다");
        }

        // 대쉬는 2칸: 연달아 두 번 쓸 수 있고, 빈 칸은 시간이 지나면 다시 찬다.
        [Test]
        public void DashHasTwoChargesThatRefill()
        {
            RunState r = NewRun();
            int max = db.feel.dashCharges;
            Assert.AreEqual(max, r.player.dashCharges);
            var dash = new CombatInput { dash = true, x = 1 };
            combat.Update(r, 1 / 60f, dash);
            for (int i = 0; i < 40; i++) combat.Update(r, 1 / 60f, new CombatInput());
            combat.Update(r, 1 / 60f, dash);
            Assert.AreEqual(max - 2, r.player.dashCharges, "두 번 쓰면 두 칸이 빈다");
            for (int i = 0; i < 60 * 6; i++) combat.Update(r, 1 / 60f, new CombatInput());
            Assert.AreEqual(max, r.player.dashCharges, "시간이 지나면 모두 다시 찬다");
        }

        // 처치하면 경험치 구·골드가 튀어나왔다가 몸으로 빨려 들어 회수된다.
        [Test]
        public void KillDropsFlyIntoHero()
        {
            RunState r = NewRun();
            r.player.x = 500; r.player.y = 350;
            Enemy e = Add(r, "stalker", 620, 350);
            r.strands.Add(new Strand { x = e.x, y = e.y, target = e, state = StrandState.Stuck, element = Element.Plain });
            e.hp = 1;
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(620, 350) });
            Assert.Greater(r.drops.Count, 0, "드롭이 생긴다");
            int gold = r.gold, xp = r.xp + r.level * 1000;
            for (int i = 0; i < 60 * 4 && r.drops.Count > 0; i++) combat.Update(r, 1 / 60f, new CombatInput());
            Assert.AreEqual(0, r.drops.Count, "모두 몸으로 빨려 들어 회수된다");
            Assert.Greater(r.gold, gold, "골드가 오른다");
            Assert.Greater(r.xp + r.level * 1000, xp, "경험치가 오른다");
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
                // 라운드가 끝나 계단이 다 나타나면 올라간다 (보스 뒤에는 욕망 제안을 거절).
                if (r.phase == Phase.Cleared && r.stairs != null && r.stairsAge >= db.rounds.stairsRevealTime)
                {
                    if (r.desirePending) combat.ChooseDesire(r, null);
                    combat.Ascend(r);
                    continue;
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
