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

        // 결박: 같은 적에게 바늘이 db.bind.strands 개 꽂힌 채 당기면 추가 피해 + 결박 폭발, 하나 모자라면 없음.
        [Test]
        public void BindingFiveStrandsHitsHarder()
        {
            Assert.Greater(db.bind.strands, 1, "결박 가닥 수 데이터");
            foreach (int n in new[] { db.bind.strands - 1, db.bind.strands })
            {
                RunState r = NewRun();
                r.player.x = 500; r.player.y = 350;
                Enemy a = Add(r, "boss", 700, 350);
                for (int i = 0; i < n; i++)
                    r.strands.Add(new Strand { x = a.x, y = a.y, target = a, state = StrandState.Stuck, element = Element.Fire });
                bool binding = n >= db.bind.strands;
                float hit = r.damage * (1 + combat.StrandBonus(n)) * (binding ? 1 + db.bind.damageBonus : 1);
                combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
                Assert.AreEqual(a.maxHp - Combat.Round(hit * n), a.hp, $"{n}가닥 피해");
                Assert.AreEqual(binding, r.effects.Any(f => f.kind == FxKind.Bind), $"{n}가닥 결박 폭발");
                Assert.AreEqual(binding, r.effects.Any(f => f.kind == FxKind.Pull && f.bound), $"{n}가닥 속성 실");
            }
            Assert.AreEqual(BindStyle.Fire, db.Tone(Element.Fire).bindStyle, "화염 세트 = 화염 리본");
            Assert.IsNotNull(db.Look(BindStyle.Fire));
        }

        [Test]
        public void SetGroupsUseLargestConnectedCluster()
        {
            Profile p = EmptyProfile();
            // 8차: 칸 수 = 등급 (일반 1 · 레어 2 · 유니크 3 · 레전더리 4)
            p.items.Add(new OwnedItem { id = 1, kind = 0 });   // 불씨 골무 (혼돈 일반 1칸)
            p.items.Add(new OwnedItem { id = 2, kind = 1 });   // 찢어진 리본 (혼돈 레어 2칸, 가로)
            p.items.Add(new OwnedItem { id = 3, kind = 2 });   // 녹슨 안전핀 (좌절 일반 1칸)
            p.placements.Add(new Placement { id = 1, x = 0, y = 0 });
            p.placements.Add(new Placement { id = 2, x = 1, y = 0 });   // (1,0)(2,0) → 골무와 변이 맞닿음
            p.placements.Add(new Placement { id = 3, x = 5, y = 3 });
            GearStats stats = Gear.Stats(db, p);
            Assert.AreEqual(2, stats.groups["fire"].size);
            Assert.AreEqual(1, stats.groups["fire"].tier);
            Assert.AreEqual(1, stats.groups["lightning"].tier);
            Assert.AreEqual(0, stats.groups["ice"].tier);
            Assert.AreEqual(Element.Fire, Gear.ThreadElement(stats));
            Assert.IsFalse(Gear.CanPlace(db, p, p.items[0], 0, 2, 0), "겹치는 칸에는 놓을 수 없다");
        }

        [Test]
        public void ItemsAreUnique()
        {
            Profile p = EmptyProfile();
            Gear.AddItem(db, p, 0);
            Gear.AddItem(db, p, 0);
            Assert.AreEqual(1, p.items.Count, "같은 종류는 한 번만 가진다");
            while (Gear.AddItem(db, p) != null) { }
            Assert.AreEqual(db.gear.pieces.Length, p.items.Count, "무작위 지급은 아직 없는 종류에서만");
            Assert.AreEqual(p.items.Count, p.items.Select(i => i.kind).Distinct().Count());
            Assert.AreEqual(0, Gear.Unowned(db, p).Count);
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

        // 하트: 처치 때 확률로 떨어지고, 몸에 빨려 들면 최대 체력 비율만큼 회복하며 피가 차오르는 연출을 켠다.
        [Test]
        public void HeartDropHeals()
        {
            float chance = db.rewards.healChance;
            db.rewards.healChance = 1;   // 이 테스트에서만 반드시 떨어지게
            try
            {
                RunState r = NewRun();
                r.player.x = 500; r.player.y = 350;
                r.hp = r.maxHp * .5f;
                Enemy e = Add(r, "stalker", 620, 350);
                r.strands.Add(new Strand { x = e.x, y = e.y, target = e, state = StrandState.Stuck, element = Element.Plain });
                e.hp = 1;
                combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(620, 350) });
                Assert.IsTrue(r.drops.Exists(d => d.heal > 0), "하트가 떨어진다");
                float before = r.hp;
                for (int i = 0; i < 60 * 4 && r.drops.Count > 0; i++) combat.Update(r, 1 / 60f, new CombatInput());
                Assert.AreEqual(before + Combat.Round(r.maxHp * db.rewards.healAmount), r.hp, 1e-3, "최대 체력 비율만큼 회복");
                Assert.Greater(r.player.heal, 0, "피가 차오르는 연출");
            }
            finally { db.rewards.healChance = chance; }
        }

        // 아이템(재봉 조각)은 쉬어가기 탑에서만: 정예·보스는 조각을 떨어뜨리지 않고, 상인은 조각을 팔지 않는다.
        [Test]
        public void ItemsOnlyComeFromTheTower()
        {
            Assert.AreEqual(0, db.rewards.eliteItemChance, "정예 조각 드롭 없음");
            Assert.IsFalse(db.rewards.bossDropsItem, "보스 조각 드롭 없음");
            Assert.IsFalse(System.Array.Exists(db.shop.wares, w => w.id == "piece"), "상인은 조각을 팔지 않음");
            RunState r = combat.CreateRun(EmptyProfile(), () => 0f);   // 확률 굴림이 늘 0 이어도
            r.player.x = 500; r.player.y = 350;
            Enemy e = Add(r, "charger", 620, 350);
            e.elite = true;
            r.strands.Add(new Strand { x = e.x, y = e.y, target = e, state = StrandState.Stuck, element = Element.Plain });
            e.hp = 1;
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(620, 350) });
            Assert.IsFalse(r.drops.Exists(d => d.item), "정예를 잡아도 조각이 떨어지지 않는다");
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

        // 조각 하나(능력치 kind)를 재봉 상자 (0,0)에 끼운 프로필
        Profile WithPiece(StatKind stat)
        {
            int kind = System.Array.FindIndex(db.gear.pieces, d => d.stat == stat);
            Assert.GreaterOrEqual(kind, 0, $"{stat} 조각이 데이터에 없습니다");
            Profile p = EmptyProfile();
            p.items.Add(new OwnedItem { id = 1, kind = kind });
            p.placements.Add(new Placement { id = 1, x = 0, y = 0 });
            Assert.IsTrue(Gear.Placed(db, p).Count == 1, "조각이 상자에 놓인다");
            return p;
        }

        // '세 갈래 바늘' 조각: 한 번 쏘면 털실 1개로 바늘 3개가 부채꼴로 나가고, 매듭 셈은 1, 당겨도 털실은 1발 몫만 돌아온다.
        [Test]
        public void MultishotFiresFanForOneYarn()
        {
            RunState r = combat.CreateRun(WithPiece(StatKind.Multishot), () => .5f);
            Assert.AreEqual(2, combat.ExtraNeedles(r), "가운데 + 좌우 2개");
            r.roundLength = 999;   // 생존 시간이 0 이면 첫 프레임에 라운드가 끝나 실을 거둔다
            r.player.x = 500; r.player.y = 350;
            float yarn = r.yarn;
            combat.Update(r, 1 / 60f, new CombatInput { shoot = true, aim = new Vector2(800, 350) });
            Assert.AreEqual(3, r.strands.Count, "바늘 3개");
            Assert.AreEqual(yarn - 1, r.yarn, "털실은 1개만");
            Assert.AreEqual(1, r.shotCount, "매듭 셈은 발사 1회 = 1");
            float[] angles = r.strands.Select(s => Mathf.Atan2(s.vy * db.arena.projectionY, s.vx)).OrderBy(a => a).ToArray();
            Assert.AreEqual(0, angles[1], 1e-3, "가운데는 겨눈 방향");
            Assert.AreEqual(db.hero.multishotSpread, angles[2] - angles[1], 1e-3, "좌우 각도는 데이터");
            Assert.AreEqual(db.hero.multishotSpread, angles[1] - angles[0], 1e-3);
            Assert.AreEqual(1, r.strands.Count(s => !s.extra));
            r.strands.ForEach(s => s.state = StrandState.Ground);
            float before = r.yarn;
            combat.Update(r, 1 / 60f, new CombatInput { pull = true, aim = new Vector2(800, 350) });
            Assert.AreEqual(Mathf.Min(combat.YarnMax(r), before + 1), r.yarn, "당기면 쓴 털실 1개 몫만 돌아온다");

            RunState plain = NewRun();
            plain.roundLength = 999;
            plain.player.x = 500; plain.player.y = 350;
            combat.Update(plain, 1 / 60f, new CombatInput { shoot = true, aim = new Vector2(800, 350) });
            Assert.AreEqual(1, plain.strands.Count, "조각이 없으면 한 개");
        }

        // '굵은 실' 조각: 당길 때 실 선분 둘레의 판정 반경이 넓어져, 기본 실로는 비껴가던 적도 벤다. 굵기 배율은 상한까지.
        [Test]
        public void ThickThreadWidensPullHit()
        {
            RunState thick = combat.CreateRun(WithPiece(StatKind.ThreadWidth), () => .5f);
            RunState plain = NewRun();
            Assert.Greater(combat.ThreadScale(thick), 1, "굵기 배율이 커진다");
            Assert.LessOrEqual(combat.ThreadScale(thick), db.hero.threadWidthMax, "상한");
            Assert.AreEqual(1, combat.ThreadScale(plain), 1e-4);
            float basePad = combat.ThreadHitPad(plain), widePad = combat.ThreadHitPad(thick);
            Assert.Greater(widePad, basePad);
            foreach ((RunState r, bool hits) in new[] { (plain, false), (thick, true) })
            {
                r.player.x = 500; r.player.y = 350;
                // 땅에 떨어진 실: 손(500,350) → (500,100). 적은 실에서 (기본 판정 + 넓어진 몫의 절반)만큼 옆에
                Enemy e = Add(r, "stalker", 500 + db.Enemy("stalker").radius + (basePad + widePad) / 2, 250);
                r.strands.Add(new Strand { x = 500, y = 100, state = StrandState.Ground, element = Element.Plain });
                combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
                Assert.AreEqual(hits, e.hp < e.maxHp, hits ? "굵은 실은 벤다" : "기본 실은 비껴간다");
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

        // 조각 한 종류를 판 (x, y)에 놓은 프로필
        Profile WithPieces(params (int kind, int x, int y)[] placed)
        {
            Profile p = EmptyProfile();
            foreach (var (kind, x, y) in placed)
            {
                int id = p.nextId++;
                p.items.Add(new OwnedItem { id = id, kind = kind });
                p.placements.Add(new Placement { id = id, x = x, y = y });
            }
            return p;
        }

        int Kind(string set, int cost) => System.Array.FindIndex(db.gear.pieces, d => d.set == set && d.cost == cost);

        // 12차: 세트가 켜지면 실 속성이 그 세트가 되고, 당길 때 한 타마다 속성 피해(damagePerTier × 단계)가 더해진다 + 부가 효과.
        [Test]
        public void SetAddsElementalDamageAndStatus()
        {
            SetDef fire = db.gear.sets.First(s => s.id == "fire");
            int one = Kind("fire", 1), two = Kind("fire", 2);
            RunState r = combat.CreateRun(WithPieces((one, 0, 0), (two, 1, 0)), () => .5f);   // 화염 2개 맞닿음 → 1단계
            Assert.AreEqual(Element.Fire, r.element, "실 속성 = 화염");
            r.player.x = 500; r.player.y = 350;
            Enemy a = Add(r, "boss", 700, 350);
            r.strands.Add(new Strand { x = a.x, y = a.y, target = a, state = StrandState.Stuck, element = Element.Fire });
            float hit = r.damage * (1 + combat.StrandBonus(1));
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
            Assert.AreEqual(a.maxHp - Combat.Round(hit) - Combat.Round(fire.damagePerTier), a.hp, "실 피해 + 화염 피해");
            Assert.Greater(a.burnTime, 0, "화상");
            Assert.AreEqual(fire.effectPerTier, a.burnDps, 1e-4f, "화상 초당 피해 = 1단계 값");
            Assert.IsTrue(r.effects.Any(f => f.kind == FxKind.ElementHit && f.element == Element.Fire), "화염 그림");
        }

        // 12차: 세트가 둘 켜지면 단계 높은 쪽이 주 속성(100%), 나머지는 subSetScale 만큼.
        [Test]
        public void SecondSetIsHalf()
        {
            int f1 = Kind("fire", 1), f2 = Kind("fire", 2), l1 = Kind("lightning", 1);
            // 화염 2개 맞닿음(1단계, 연결 2) · 번개 1개(1단계, 연결 1) → 화염이 주 속성
            RunState r = combat.CreateRun(WithPieces((f1, 0, 0), (f2, 1, 0), (l1, 5, 3)), () => .5f);
            Assert.AreEqual(2, r.gearStats.sets.Count);
            Assert.AreEqual(Element.Fire, r.gearStats.sets[0].def.element, "주 속성 = 연결이 큰 화염");
            Assert.AreEqual(1f, combat.SetScale(r, r.gearStats.sets[0]));
            Assert.AreEqual(db.gear.subSetScale, combat.SetScale(r, r.gearStats.sets[1]));
            SetDef bolt = db.gear.sets.First(s => s.id == "lightning");
            Assert.AreEqual(bolt.effectPerTier * db.gear.subSetScale, combat.SetEffect(r, Element.Lightning), 1e-4f, "보조 번개 튐 = 절반");
        }

        // 12차: 보스를 쓰러뜨려 정화가 끝나면 계단과 함께 아티팩트 탑이 솟는다 (솟는 동안은 E 불가).
        [Test]
        public void TowerRisesAfterBoss()
        {
            RunState r = NewRun();
            combat.BeginFloor(r);   // 층 컨셉 뽑기
            r.roundIndex = System.Array.IndexOf(db.rounds.sequence, RoundKind.Boss);
            combat.BeginRound(r);
            combat.DevKillAll(r);
            for (int i = 0; i < 4000 && r.phase != Phase.Cleared; i++) combat.Update(r, 1 / 60f, new CombatInput());
            Assert.AreEqual(Phase.Cleared, r.phase);
            Assert.IsNotNull(r.altar, "보스 뒤 탑");
            Assert.AreEqual(db.rounds.artifactChoices, r.artifactOffer.Count, "아티팩트 3택");
            Assert.IsFalse(combat.TowerReady(r), "솟는 중에는 E 불가");
            for (int i = 0; i < 200; i++) combat.Update(r, 1 / 60f, new CombatInput());
            Assert.IsTrue(combat.TowerReady(r));
        }

        // 12차 고유 효과: '거인 사냥'은 보스·정예에게만, '성급한 결박'은 결박 가닥을 줄인다.
        [Test]
        public void ItemEffectsApply()
        {
            int giantKind = System.Array.FindIndex(db.gear.pieces, d => d.effect == ItemEffect.Giant);
            int quickKind = System.Array.FindIndex(db.gear.pieces, d => d.effect == ItemEffect.QuickBind);
            Assert.GreaterOrEqual(giantKind, 0); Assert.GreaterOrEqual(quickKind, 0);
            PieceDef giant = db.gear.pieces[giantKind];
            RunState r = combat.CreateRun(WithPieces((giantKind, 0, 0)), () => .5f);
            r.player.x = 500; r.player.y = 350;
            Enemy boss = Add(r, "boss", 700, 350);
            r.strands.Add(new Strand { x = boss.x, y = boss.y, target = boss, state = StrandState.Stuck, element = Element.Plain });
            float hit = r.damage * (1 + combat.StrandBonus(1));
            float elemental = r.gearStats.sets.Sum(s => s.def.damagePerTier * s.tier);
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
            Assert.AreEqual(boss.maxHp - Combat.Round(hit * (1 + giant.effectValue)) - Combat.Round(elemental * (1 + giant.effectValue)), boss.hp, "보스에게 +30%");

            RunState q = combat.CreateRun(WithPieces((quickKind, 0, 0)), () => .5f);
            Assert.IsTrue(combat.Bound(q, db.bind.strands - 1), "성급한 결박: 한 가닥 모자라도 결박");
            Assert.IsFalse(combat.Bound(NewRun(), db.bind.strands - 1), "없으면 결박 아님");
        }
    }
}
