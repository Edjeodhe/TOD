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

        // 20차: 층 속성 암시는 그 층의 첫 라운드에만 (다음 라운드에서는 다시 띄우지 않고, 다음 층 첫 라운드에서 다시)
        [Test]
        public void FloorHintOnlyOnFirstRound()
        {
            RunState r = NewRun();
            combat.BeginFloor(r);
            int first = r.hintSerial;
            Assert.Greater(first, 0, "첫 라운드에 암시가 없음");
            combat.Ascend(r);
            Assert.AreEqual(first, r.hintSerial, "두 번째 라운드에 암시가 다시 뜸");
            while (r.roundKind != RoundKind.Boss) combat.Ascend(r);
            Assert.AreEqual(first, r.hintSerial, "보스 라운드에 암시가 다시 뜸");
            combat.Ascend(r);
            Assert.AreEqual(2, r.floor);
            Assert.Greater(r.hintSerial, first, "다음 층 첫 라운드에 암시가 없음");
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

        // 24차 프레임 스톱: 당겨 맞히면 hitStopLead 만큼 이펙트, 주인공 자세가 흐른 뒤, 멈춤 동안은 이펙트까지 서고, 끝나면 다시 흐른다.
        [Test]
        public void PullFrameStopHoldsEffects()
        {
            RunState r = NewRun();
            r.player.x = 500; r.player.y = 350;
            Enemy a = Add(r, "boss", 700, 350);
            r.strands.Add(new Strand { x = a.x, y = a.y, target = a, state = StrandState.Stuck, element = Element.Plain });
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
            Assert.IsTrue(r.frameStop && r.freeze > 0, "맞히면 프레임 스톱");
            float hold = r.freeze;
            Fx fx = r.effects.Find(e => e.time > 0);
            Assert.IsNotNull(fx);
            float lead = Mathf.Max(0, db.feel.hitStopLead);
            float action = r.player.actionTime;
            for (float t = 0; t < lead + .001f; t += 1 / 120f) combat.Update(r, 1 / 120f, new CombatInput());
            Assert.IsTrue(r.FrameHeld, "먼저 흐를 시간이 지나면 멈춘다");
            Assert.AreEqual(hold, r.freeze, 1e-4f, "먼저 흐르는 동안은 멈춤 시간이 줄지 않는다");
            Assert.Greater(r.player.actionTime, action, "먼저 흐르는 동안 주인공 자세가 맞는 순간으로 간다");
            float fxTime = fx.time, heroTime = r.player.actionTime;
            combat.Update(r, hold * .5f, new CombatInput());
            Assert.AreEqual(fxTime, fx.time, 1e-5f, "멈춘 동안 이펙트가 서 있다");
            Assert.AreEqual(heroTime, r.player.actionTime, 1e-5f, "멈춘 동안 주인공도 서 있다");
            for (int i = 0; i < 60 && r.freeze > 0; i++) combat.Update(r, 1 / 60f, new CombatInput());
            Assert.IsFalse(r.frameStop, "끝나면 풀린다");
            combat.Update(r, 1 / 60f, new CombatInput());
            Assert.Less(fx.time, fxTime, "풀리면 이펙트가 다시 흐른다");
        }

        // 24차 줌 펀치: 당겨 맞히면 줌 값이 생기고, 결박·치명타면 더 크고, 프레임 스톱 동안은 최대에서 서 있다가, 시간이 지나면 0.
        [Test]
        public void PullZoomPunch()
        {
            FeelConfig F = db.feel;
            Assert.Greater(F.zoomPunchBase, 0, "줌 펀치 데이터");
            Assert.Greater(F.zoomPunchIn + F.zoomPunchOut, 0, "줌 펀치 시간 데이터");
            float Peak(int strands, bool crit)
            {
                RunState r = NewRun();
                if (crit) r.random = () => 0;   // 치명타 확률을 반드시 넘김
                r.player.x = 500; r.player.y = 350;
                Enemy a = Add(r, "boss", 700, 350);
                for (int i = 0; i < strands; i++)
                    r.strands.Add(new Strand { x = a.x, y = a.y, target = a, state = StrandState.Stuck, element = Element.Plain });
                Assert.AreEqual(0, Combat.ZoomPunch(r), "당기기 전엔 0");
                combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
                Assert.IsNotNull(r.zoomPunch, "맞히면 줌 펀치");
                float peak = r.zoomPunch.x;
                // 다가감: 프레임 스톱에 걸리면 최대에서 서 있다
                float best = 0;
                for (int i = 0; i < 240 && r.zoomPunch != null && !r.FrameHeld; i++) { combat.Update(r, 1 / 240f, new CombatInput()); best = Mathf.Max(best, Combat.ZoomPunch(r)); }
                if (r.FrameHeld)
                {
                    for (int i = 0; i < 240 && r.zoomPunch.max - r.zoomPunch.time < r.zoomPunch.done && r.FrameHeld; i++) combat.Update(r, 1 / 240f, new CombatInput());
                    float held = Combat.ZoomPunch(r);
                    if (r.FrameHeld)
                    {
                        combat.Update(r, 1 / 240f, new CombatInput());
                        if (r.FrameHeld) Assert.AreEqual(held, Combat.ZoomPunch(r), 1e-6f, "멈춘 동안 줌은 그대로");
                    }
                    best = Mathf.Max(best, held);
                }
                Assert.Greater(best, 0, "다가간다");
                Assert.LessOrEqual(best, peak + 1e-6f);
                for (int i = 0; i < 600 && r.zoomPunch != null; i++) combat.Update(r, 1 / 60f, new CombatInput());
                Assert.IsNull(r.zoomPunch, "시간이 지나면 끝");
                Assert.AreEqual(0, Combat.ZoomPunch(r), "돌아오면 0");
                return peak;
            }
            float one = Peak(1, false);
            Assert.AreEqual(F.zoomPunchBase, one, 1e-5f, "한 타 = 기본");
            float three = Peak(3, false);
            Assert.GreaterOrEqual(three, one, "타수가 많으면 조금 더");
            Assert.Greater(Peak(db.bind.strands, false), Peak(db.bind.strands - 1, false), "결박이면 더 크다");
            if (db.hero.critChance > 0) Assert.Greater(Peak(1, true), one, "치명타면 더 크다");
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
                    r.strands.Add(new Strand { x = a.x, y = a.y, target = a, state = StrandState.Stuck, element = Element.Wrath });
                bool binding = n >= db.bind.strands;
                float hit = r.damage * (1 + combat.StrandBonus(n)) * (binding ? 1 + db.bind.damageBonus : 1);
                combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
                Assert.AreEqual(a.maxHp - Combat.Round(hit * n), a.hp, $"{n}가닥 피해");
                Assert.AreEqual(binding, r.effects.Any(f => f.kind == FxKind.Bind), $"{n}가닥 결박 폭발");
                Assert.AreEqual(binding, r.effects.Any(f => f.kind == FxKind.Pull && f.bound), $"{n}가닥 속성 실");
            }
            Assert.AreEqual(BindStyle.Wrath, db.Tone(Element.Wrath).bindStyle, "분노 세트 = 분노 리본 (22차)");
            Assert.IsNotNull(db.Look(BindStyle.Wrath));
        }

        [Test]
        public void SetGroupsUseLargestConnectedCluster()
        {
            Profile p = EmptyProfile();
            // 8차: 칸 수 = 등급 (일반 1 · 레어 2 · 유니크 3 · 레전더리 4)
            p.items.Add(new OwnedItem { id = 1, kind = Kind("wrath", 1) });   // 22차: 그을린 골무 (분노 일반 1칸)
            p.items.Add(new OwnedItem { id = 2, kind = Kind("wrath", 2) });   // 성난 바늘꽂이 (분노 레어 2칸, 가로)
            p.items.Add(new OwnedItem { id = 3, kind = Kind("sloth", 1) });   // 늘어진 실타래 (나태 일반 1칸)
            p.placements.Add(new Placement { id = 1, x = 0, y = 0 });
            p.placements.Add(new Placement { id = 2, x = 1, y = 0 });   // (1,0)(2,0) → 골무와 변이 맞닿음
            p.placements.Add(new Placement { id = 3, x = 5, y = 3 });
            GearStats stats = Gear.Stats(db, p);
            Assert.AreEqual(2, stats.groups["wrath"].size);
            Assert.AreEqual(1, stats.groups["wrath"].tier);
            Assert.AreEqual(0, stats.groups["sloth"].tier, "나태 1개는 단계 없음");
            Assert.AreEqual(Element.Wrath, Gear.ThreadElement(stats));
            Assert.IsFalse(Gear.CanPlace(db, p, p.items[0], 0, 2, 0), "겹치는 칸에는 놓을 수 없다");
        }

        [Test]
        public void ItemsAreUnique()
        {
            Profile p = EmptyProfile();
            Gear.AddItem(db, p, 0);
            Gear.AddItem(db, p, 0);
            Assert.AreEqual(1, p.items.Count, "같은 종류는 한 번만 가진다");
            p.best = 99;   // 15차: 잠긴 세트는 무작위 지급에서 빠지므로 모두 연다 (잠금은 OutgameTests)
            while (Gear.AddItem(db, p) != null) { }
            Assert.AreEqual(Enumerable.Range(0, db.gear.pieces.Length).Count(k => !Gear.IsPattern(db, k)), p.items.Count, "무작위 지급은 아직 없는 종류에서만 (17차: 아버지의 도안 제외)");
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

        // 23차: 서서 대시하면 보는 쪽으로 나간다 (예전엔 반대로 물러났다). 보는 쪽도 그대로다.
        [Test]
        public void StandingDashGoesTheWayTheHeroFaces()
        {
            foreach (float face in new[] { -1f, 1f })
            {
                RunState r = NewRun();
                r.player.facing = face;
                r.player.angle = face > 0 ? 0 : Mathf.PI;
                float x0 = r.player.x;
                combat.Update(r, 1 / 60f, new CombatInput { dash = true });
                for (int i = 0; i < 20; i++) combat.Update(r, 1 / 60f, new CombatInput());
                Assert.Greater((r.player.x - x0) * face, 50, $"보는 쪽({face})으로 나간다");
                Assert.AreEqual(face, r.player.facing, "보는 쪽은 그대로");
            }
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

        // 자동 조작으로 마지막 층(22차: 7층) 끝(엔딩)까지 진행되는지: 라운드·보스·욕망 제안·상점 흐름을 모두 통과해야 한다.
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
                r.devGod = true;                               // 23차-boss7: 맞지 않게 (식탐 보스는 준 피해만큼 회복해서, 피하지 않는 자동 조작으로는 끝나지 않음)
                if (r.desire > r.maxDesire * .4f) r.desire = 0;   // 동화 엔딩 대신 등반 흐름을 본다 (22차: 7층이라 층 욕망이 커서 일찍 비운다)
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
            Assert.AreEqual(Phase.Victory, r.phase, $"{r.floor}층 {r.round}라운드에서 멈춤 (steps {steps}, outcome {r.outcome}, mood {r.mood}, desire {r.desire}/{r.maxDesire})");
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

        int Kind(string set, int cost, int skip = 0) => Enumerable.Range(0, db.gear.pieces.Length).Where(k => db.gear.pieces[k].set == set && db.gear.pieces[k].cost == cost).Skip(skip).DefaultIfEmpty(-1).First();

        // 22차: 분노 3개(2단계, 메인) + 나태 2개(1단계, 부가). 판 6×4
        Profile WrathMainSlothSub() => WithPieces((Kind("wrath", 1), 0, 0), (Kind("wrath", 2), 1, 0), (Kind("wrath", 1, 1), 0, 1), (Kind("sloth", 1), 4, 0), (Kind("sloth", 2), 5, 0));

        // 12차: 세트가 켜지면 실 속성이 그 세트가 되고, 당길 때 한 타마다 속성 피해(damagePerTier × 단계)가 더해진다 + 부가 효과.
        [Test]
        public void SetAddsElementalDamageAndStatus()
        {
            SetDef fire = db.gear.sets.First(s => s.id == "wrath");
            int one = Kind("wrath", 1), two = Kind("wrath", 2);
            RunState r = combat.CreateRun(WithPieces((one, 0, 0), (two, 1, 0)), () => .5f);   // 화염 2개 맞닿음 → 1단계
            Assert.AreEqual(Element.Wrath, r.element, "실 속성 = 화염");
            r.player.x = 500; r.player.y = 350;
            Enemy a = Add(r, "boss", 700, 350);
            r.strands.Add(new Strand { x = a.x, y = a.y, target = a, state = StrandState.Stuck, element = Element.Wrath });
            float hit = r.damage * (1 + combat.StrandBonus(1));
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
            Assert.AreEqual(a.maxHp - Combat.Round(hit + fire.damagePerTier), a.hp, "실 피해 + 화염 피해 (16차: 한 숫자로 합침)");
            Assert.Greater(a.burnTime, 0, "화상");
            Assert.AreEqual(fire.effectPerTier, a.burnDps, 1e-4f, "화상 초당 피해 = 1단계 값");
            Assert.IsTrue(r.effects.Any(f => f.kind == FxKind.ElementHit && f.element == Element.Wrath), "화염 그림");
        }

        // 16차: 메인 세트 = 판에 놓인 조각 수가 가장 많은 켜진 세트 (동률이면 단계 → 연결 조각 수 → 세트 순서). 부가 세트는 상시 효과 없음.
        [Test]
        public void MainSetHasMostPieces()
        {
            // 22차: 분노 3개(2단계) · 나태 2개(1단계) → 조각이 많은 분노가 메인
            RunState r = combat.CreateRun(WrathMainSlothSub(), () => .5f);
            Assert.AreEqual(2, r.gearStats.sets.Count);
            Assert.AreEqual(Element.Wrath, combat.MainSet(r).def.element, "메인 = 조각이 많은 화염");
            Assert.AreEqual(Element.Wrath, r.element, "실 속성 = 메인");
            Assert.AreEqual(1f, combat.SetScale(r, r.gearStats.sets[0]));
            Assert.AreEqual(0f, combat.SetScale(r, r.gearStats.sets[1]), "부가 세트는 상시 효과 없음");
            Assert.AreEqual(0f, combat.SetEffect(r, Element.Sloth), "부가 나태는 상시 둔화 없음");

            // 정렬 규칙: 조각 수가 단계보다 먼저, 그다음 단계 → 연결 → 세트 순서
            SetDef a = db.gear.sets[0], b = db.gear.sets[1];
            var many = new ActiveSet { def = b, tier = 1, size = 2, count = 4, order = 1 };
            var high = new ActiveSet { def = a, tier = 3, size = 3, count = 3, order = 0 };
            var list = new System.Collections.Generic.List<ActiveSet> { high, many };
            list.Sort(Gear.MainFirst);
            Assert.AreSame(many, list[0], "조각 수가 많으면 단계가 낮아도 메인");
            var tierA = new ActiveSet { def = b, tier = 2, size = 2, count = 3, order = 1 };
            var tierB = new ActiveSet { def = a, tier = 1, size = 3, count = 3, order = 0 };
            list = new System.Collections.Generic.List<ActiveSet> { tierB, tierA };
            list.Sort(Gear.MainFirst);
            Assert.AreSame(tierA, list[0], "조각 수가 같으면 단계");
            var sizeA = new ActiveSet { def = b, tier = 1, size = 3, count = 3, order = 1 };
            var sizeB = new ActiveSet { def = a, tier = 1, size = 2, count = 3, order = 0 };
            list = new System.Collections.Generic.List<ActiveSet> { sizeB, sizeA };
            list.Sort(Gear.MainFirst);
            Assert.AreSame(sizeA, list[0], "그다음 연결 조각 수");
            var orderA = new ActiveSet { def = a, tier = 1, size = 2, count = 3, order = 0 };
            var orderB = new ActiveSet { def = b, tier = 1, size = 2, count = 3, order = 1 };
            list = new System.Collections.Generic.List<ActiveSet> { orderB, orderA };
            list.Sort(Gear.MainFirst);
            Assert.AreSame(orderA, list[0], "마지막은 세트 순서");
        }

        // 16차: 부가 세트는 타격마다 충전되고, 다 찬 뒤 다음 타격에 터진다 (피해 + 이펙트, 충전 0으로).
        [Test]
        public void SubSetProcFiresAfterCharge()
        {
            RunState r = combat.CreateRun(WrathMainSlothSub(), () => .5f);
            ActiveSet bolt = combat.SubSets(r).Single();
            int need = bolt.def.procCharge;
            Assert.Greater(need, 0, "procCharge 데이터");
            r.roundLength = 9999;   // 생존 라운드가 첫 프레임에 끝나지 않게
            r.player.x = 500; r.player.y = 350;
            Enemy e = Add(r, "stalker", 700, 350);
            e.hp = e.maxHp = 100000;
            void PullOnce()
            {
                r.freeze = 0;
                r.strands.Add(new Strand { x = e.x, y = e.y, target = e, state = StrandState.Stuck, element = Element.Wrath });
                combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
            }
            for (int i = 0; i < need; i++)
            {
                Assert.IsFalse(combat.ProcReady(r, bolt), $"{i}타: 아직");
                PullOnce();
            }
            Assert.IsTrue(combat.ProcReady(r, bolt), "다 참 = 준비");
            Assert.IsFalse(r.effects.Any(f => f.kind == FxKind.Proc), "준비만 되고 아직 안 터짐");
            float before = e.hp;
            ActiveSet main = combat.MainSet(r);
            float pullOnly = r.damage * (1 + combat.StrandBonus(1)) + main.def.damagePerTier * main.tier;
            PullOnce();
            Assert.IsTrue(r.effects.Any(f => f.kind == FxKind.Proc && f.element == Element.Sloth), "나태 발동 이펙트");
            Assert.AreEqual(0, combat.ProcCharge(r, bolt), "발동 뒤 충전 0");
            float procDamage = bolt.def.procDamage + bolt.def.procDamagePerTier * bolt.tier;
            Assert.AreEqual(before - Combat.Round(pullOnly) - Combat.Round(procDamage), e.hp, 1e-3f, "당기기 + 발동 피해");
        }

        // 24차 상성 제거: 분노 메인으로 나태 몬스터를 당겨도 배율 없음 (예전 약점 ×1.5)
        [Test]
        public void NoAffinityMultiplier()
        {
            int f1 = Kind("wrath", 1), f2 = Kind("wrath", 2);
            RunState r = combat.CreateRun(WithPieces((f1, 0, 0), (f2, 1, 0)), () => .5f);
            r.player.x = 500; r.player.y = 350;
            Enemy e = Add(r, "boss", 700, 350);
            e.element = Element.Sloth;
            r.strands.Add(new Strand { x = e.x, y = e.y, target = e, state = StrandState.Stuck, element = Element.Wrath });
            float hit = r.damage * (1 + combat.StrandBonus(1)) + db.gear.sets.First(s => s.id == "wrath").damagePerTier;
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
            Assert.AreEqual(e.maxHp - Combat.Round(hit * combat.SinPullMultiplier(r, combat.MainSet(r), e)), e.hp, 1e-3f, "상성 배율이 남아 있음");
        }

        // 24차 층 날씨: 죄종 7개 모두 날씨가 있고, 7층에서 1층의 2배(한도 cap 안에서), 문구는 백분율
        [Test]
        public void FloorWeatherScalesWithFloor()
        {
            foreach (ElementDef d in db.elements.defs)
            {
                FloorWeather w = d.weather;
                Assert.IsNotNull(w, $"{d.element} 날씨 없음");
                Assert.AreNotEqual(WeatherKind.None, w.kind, $"{d.element} 날씨 종류 없음");
                Assert.IsFalse(string.IsNullOrEmpty(w.text), $"{d.element} 날씨 문구 없음");
                float one = Combat.WeatherValue(w, 1), seven = Combat.WeatherValue(w, 7);
                Assert.Greater(one, 0, $"{d.element} 1층 세기");
                float expected = w.cap > 0 ? Mathf.Min(w.cap, one * 2) : one * 2;
                Assert.AreEqual(expected, seven, 1e-4f, $"{d.element} 7층 = 1층 × 2");
            }
            RunState r = NewRun();
            r.floor = 1;
            combat.DevSetFloorElement(r, Element.Greed);
            Assert.AreEqual(WeatherKind.GainDown, combat.Weather(r).kind);
            StringAssert.Contains("30", combat.WeatherText(r), "탐욕 1층 -30%");
            Assert.AreEqual(0, combat.WeatherValue(r, WeatherKind.MoveDown), "다른 날씨 값이 새어 나옴");
        }

        // 24차 식탐 허기: 전투 중 체력이 서서히 줄되 1 아래로는 안 내려가고, 쉬어가기에선 안 줄어든다
        [Test]
        public void GluttonyHungerDrainsButNeverKills()
        {
            RunState r = NewRun();
            combat.BeginFloor(r);   // 생존 라운드 (BeginFloor 없이 돌리면 첫 프레임에 라운드가 끝남)
            r.floor = 7;
            r.roundLength = 999;
            combat.DevSetFloorElement(r, Element.Gluttony);
            r.hp = r.maxHp;
            void Tick() { r.enemies.Clear(); r.bullets.Clear(); combat.Update(r, .1f, new CombatInput()); }   // 허기만 남긴다 (몬스터는 계속 나옴)
            for (int i = 0; i < 20; i++) Tick();
            Assert.Less(r.hp, r.maxHp, "허기로 체력이 줄지 않음");
            r.hp = 2;
            for (int i = 0; i < 100; i++) Tick();
            Assert.AreEqual(1f, r.hp, 1e-3f, "허기가 체력 1 아래로 내려감");
            Assert.AreEqual(Outcome.None, r.outcome);
            Assert.AreEqual(Phase.Combat, r.phase, "라운드가 끝나 버려 허기를 확인 못 함");
        }

        // 16차: 층마다 속성을 뽑되 한 등반 안에서 겹치지 않는다. 그 층 몬스터는 모두 그 속성.
        [Test]
        public void FloorElementsDoNotRepeat()
        {
            RunState r = NewRun();
            var seen = new System.Collections.Generic.List<Element>();
            int count = db.elements.defs.Length;
            int roll = 0;
            r.random = () => (roll++ * .37f) % 1f;
            for (int floor = 1; floor <= count; floor++)
            {
                r.floor = floor; r.desire = 0; r.outcome = Outcome.None; r.mood = Mood.Normal;
                combat.BeginFloor(r);
                Assert.AreNotEqual(Element.Plain, r.floorElement);
                seen.Add(r.floorElement);
                Enemy e = combat.DevSpawn(r, "stalker");
                Assert.AreEqual(r.floorElement, e.element, "그 층 몬스터 = 층 속성");
            }
            Assert.AreEqual(count, seen.Distinct().Count(), "한 등반 안에서 겹치지 않음");
        }

        // 16차: 보스만 공격에 속성 효과 (화염 화상, 피 흡혈 회복, 바람 밀쳐냄). 일반 몬스터 탄은 그대로.
        [Test]
        public void BossAttacksCarryElement()
        {
            RunState r = NewRun();
            r.player.x = 1000; r.player.y = 700;
            Enemy boss = Add(r, "boss", 1300, 700);
            boss.wait = 99;
            void Shoot(Element element, Enemy from)
            {
                r.player.hurt = 0; r.player.dash = 0; r.player.dashGrace = 0; r.freeze = 0;
                boss.element = element;
                r.bullets.Add(new Bullet { x = r.player.x + 2, y = r.player.y, vx = -1, vy = 0, hit = 5, boss = from, element = element });
                combat.Update(r, .001f, new CombatInput());
            }
            Shoot(Element.Wrath, boss);
            Assert.Greater(r.player.burnTime, 0, "화염 보스: 화상");
            Assert.IsTrue(r.effects.Any(f => f.kind == FxKind.BossHit && f.element == Element.Wrath), "보스 속성 이펙트");
            boss.hp = boss.maxHp - 100;
            float bossHp = boss.hp;
            Shoot(Element.Gluttony, boss);
            Assert.Greater(boss.hp, bossHp, "피 보스: 준 피해만큼 회복");
            Shoot(Element.Lust, boss);
            Assert.IsNotNull(r.player.push, "바람 보스: 밀쳐냄");
            r.player.ClearStatus();
            Shoot(Element.Wrath, null);
            Assert.AreEqual(0f, r.player.burnTime, "일반 탄은 속성 효과 없음");
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
            Assert.AreEqual(boss.maxHp - Combat.Round((hit + elemental) * (1 + giant.effectValue)), boss.hp, "보스에게 +30%");

            RunState q = combat.CreateRun(WithPieces((quickKind, 0, 0)), () => .5f);
            Assert.IsTrue(combat.Bound(q, db.bind.strands - 1), "성급한 결박: 한 가닥 모자라도 결박");
            Assert.IsFalse(combat.Bound(NewRun(), db.bind.strands - 1), "없으면 결박 아님");
        }
    
        // 22차 칠죄종 메인 효과: 나태 3번 맞으면 잠, 시기 독이 죽을 때 옆 적에게 옮음, 탐욕 저당 잡힌 적 처치 금화
        [Test]
        public void SinEffectsApply()
        {
            RunState Run(string set)
            {
                RunState q = combat.CreateRun(WithPieces((Kind(set, 1), 0, 0), (Kind(set, 2), 1, 0)), () => .5f);
                Assert.AreEqual(set, combat.MainSet(q).def.id, $"{set} 메인");
                q.roundLength = 9999;
                q.player.x = 500; q.player.y = 350;
                return q;
            }
            void Pull(RunState q, Enemy e)
            {
                q.freeze = 0;
                q.strands.Add(new Strand { x = e.x, y = e.y, target = e, state = StrandState.Stuck, element = q.element });
                combat.Update(q, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
            }

            RunState r = Run("sloth");
            Enemy a = Add(r, "stalker", 700, 350);
            a.hp = a.maxHp = 100000;
            SetDef sloth = combat.MainSet(r).def;
            for (int i = 0; i < sloth.extra; i++) { Assert.AreEqual(0f, a.sleepTime, $"{i}타: 아직 안 잠"); Pull(r, a); }
            Assert.Greater(a.sleepTime, 0f, "나태: 정해진 겹이면 잠듦");

            r = Run("envy");
            a = Add(r, "stalker", 700, 350);
            Enemy b = Add(r, "stalker", 760, 350);
            a.hp = a.maxHp = 100000;
            Pull(r, a);
            Assert.Greater(a.poisonStacks, 0, "시기 독");
            a.hp = 1;
            b.poisonStacks = 0;
            Pull(r, a);
            Assert.LessOrEqual(a.hp, 0f);
            Assert.Greater(b.poisonStacks, 0, "독 걸린 적이 죽으면 옆 적에게 옮음");

            r = Run("greed");
            a = Add(r, "stalker", 700, 350);
            a.hp = a.maxHp = 100000;
            Pull(r, a);
            Assert.IsTrue(a.pawned, "탐욕 저당");
            a.hp = 1;
            r.drops.Clear();
            Pull(r, a);
            ActiveSet greed = combat.MainSet(r);
            Assert.AreEqual((int)(a.type.gold) + Mathf.RoundToInt(greed.def.extra * greed.tier), r.drops.Sum(d => d.gold), "처치 금화 + 저당 금화");
        }

        // 25차: 욕망 '집착'으로 옮겨붙은 실은 한 번만 옮겨간다. 예전엔 바늘 몇 발 꽂고 우클릭을 연타하면
        // 옮겨붙은 실로 또 당기고 또 옮겨붙어 주변 적이 전부 죽었고, 당길 때마다 털실도 공짜로 돌려받았다.
        [Test]
        public void ObsessionStrandsMoveOnlyOnce()
        {
            RunState r = NewRun();
            r.roundKind = RoundKind.Rest;
            r.player.x = 500; r.player.y = 350;
            r.desires.Add("obsession");
            var enemies = new System.Collections.Generic.List<Enemy>();
            for (int i = 0; i < 5; i++) { Enemy e = Add(r, "stalker", 650 + i * 30, 350); e.hp = 1; enemies.Add(e); }
            for (int i = 0; i < 3; i++) r.strands.Add(new Strand { x = enemies[0].x, y = enemies[0].y, target = enemies[0], state = StrandState.Stuck, element = Element.Plain });
            r.yarn = 0;
            for (int k = 0; k < 20; k++)
            {
                r.freeze = 0; r.frameStop = false;
                combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
            }
            Assert.AreEqual(2, r.kills, "첫 당기기로 1마리, 옮겨붙은 실로 1마리까지만");
            Assert.AreEqual(0, r.strands.Count, "옮겨붙은 실은 다시 옮겨가지 않음");
            Assert.LessOrEqual(r.yarn, Mathf.Ceil(3 / 2f) + .001f, "옮겨붙은 실은 털실을 다시 돌려주지 않음");
        }
    }
}
