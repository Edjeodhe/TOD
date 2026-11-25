// 15차-outgame-unlock 검증 (EditMode): 세트 해금 표, 잠긴 세트 제외, 해금 알림 기록. 17차: 시작 조각, 등반 가방, 일기 보상, 가격. 19차: 모이라 줄 고르기, 챙긴 조각 한 번, 옛 저장. 값은 Main 씬 GameDatabase.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TOD.Tests
{
    public class OutgameTests
    {
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
            Unlocks.ignoreLocks = false;
        }

        [OneTimeTearDown]
        public void Close()
        {
            Unlocks.ignoreLocks = false;
            PlayerPrefs.DeleteKey(db.gear.saveKey);
            db.gear.saveKey = realSaveKey;
            if (opened.IsValid()) EditorSceneManager.CloseScene(opened, true);
        }

        static Profile EmptyProfile(int best = 0) => new Profile { nextId = 1, best = best };

        int Kind(string set, int skip = 0) => Enumerable.Range(0, db.gear.pieces.Length).Where(k => db.gear.pieces[k].set == set).Skip(skip).First();

        // 21차 재봉 상자 목록 정렬: 놓인 조각 먼저, 그 안에서 등급순/속성순/획득순
        [Test]
        public void StashSortKeepsPlacedFirst()
        {
            var items = Enumerable.Range(0, db.gear.pieces.Length).Select(k => new OwnedItem { id = 100 + k, kind = k }).Reverse().ToList();
            int placedKind = items[items.Count / 2].kind;
            bool Placed(OwnedItem i) => i.kind == placedKind;
            int SetIndex(OwnedItem i) { int k = System.Array.FindIndex(db.gear.sets, s => s.id == Gear.Def(db, i).set); return k < 0 ? int.MaxValue : k; }

            List<OwnedItem> grade = Gear.SortStash(db, items, Placed, StashSort.Grade);
            Assert.AreEqual(placedKind, grade[0].kind, "놓인 조각이 맨 위");
            for (int i = 2; i < grade.Count; i++) Assert.GreaterOrEqual(Gear.Def(db, grade[i - 1]).cost, Gear.Def(db, grade[i]).cost, "등급순: 높은 등급 먼저");

            List<OwnedItem> set = Gear.SortStash(db, items, Placed, StashSort.Set);
            Assert.AreEqual(placedKind, set[0].kind);
            for (int i = 2; i < set.Count; i++) Assert.LessOrEqual(SetIndex(set[i - 1]), SetIndex(set[i]), "속성순: 세트 목록 순서");

            List<OwnedItem> got = Gear.SortStash(db, items, Placed, StashSort.Acquired);
            Assert.AreEqual(placedKind, got[0].kind);
            CollectionAssert.AreEqual(items.Where(i => !Placed(i)).ToList(), got.Skip(1).ToList(), "획득순: 얻은 순서 그대로");
        }

        // 23차 레어도 가중치: 등급(cost)별 rankWeights 비율대로 뽑히고, 높은 등급일수록 덜 나오며, 0 이면 무작위로는 안 나온다
        [Test]
        public void PiecePicksFollowRankWeights()
        {
            Assert.IsNotNull(db.gear.rankWeights, "gear.rankWeights 데이터가 없습니다");
            List<int> pool = Gear.Unowned(db, EmptyProfile(99));
            var rng = new System.Random(24);
            float Roll() => (float)rng.NextDouble();
            var hits = new int[8];
            const int draws = 20000;
            for (int i = 0; i < draws; i++) hits[db.gear.pieces[Gear.PickWeighted(db, pool, Roll)].cost]++;
            float total = pool.Sum(k => Gear.Weight(db, k));
            foreach (int cost in pool.Select(k => db.gear.pieces[k].cost).Distinct())
            {
                float expected = pool.Where(k => db.gear.pieces[k].cost == cost).Sum(k => Gear.Weight(db, k)) / total;
                Assert.AreEqual(expected, hits[cost] / (float)draws, .02f, $"코스트 {cost} 등급의 뽑힌 비율");
            }
            float RankWeight(int cost) => Gear.Weight(db, pool.First(k => db.gear.pieces[k].cost == cost));
            for (int c = 2; c <= 4; c++) Assert.Less(RankWeight(c), RankWeight(c - 1), $"코스트 {c} 가 {c - 1} 보다 덜 나와야 합니다");

            float[] saved = db.gear.rankWeights;
            try
            {
                db.gear.rankWeights = new float[] { 0, 1, 1, 1, 0 };
                List<int> picked = Gear.PickWeighted(db, pool, pool.Count, Roll);
                Assert.IsTrue(picked.All(k => db.gear.pieces[k].cost != 4), "가중치 0 등급이 뽑혔습니다");
                Assert.AreEqual(pool.Count(k => db.gear.pieces[k].cost != 4), picked.Count);
                Assert.AreEqual(picked.Count, picked.Distinct().Count(), "같은 조각이 두 번 뽑힘");
            }
            finally { db.gear.rankWeights = saved; }
        }

        [Test]
        public void UnlockTableFollowsBestFloor()
        {
            Assert.IsNotNull(db.gear.setUnlockFloor, "gear.setUnlockFloor 데이터가 없습니다");
            string Open(int best) => string.Join(",", db.gear.sets.Where(s => Unlocks.IsOpen(db, EmptyProfile(best), s.id)).Select(s => s.id));
            Assert.AreEqual("wrath,sloth,envy", Open(0));
            Assert.AreEqual("wrath,sloth,envy", Open(1));
            Assert.AreEqual("wrath,sloth,envy,greed,gluttony", Open(2));
            Assert.AreEqual(db.gear.sets.Length, db.gear.sets.Count(s => Unlocks.IsOpen(db, EmptyProfile(3), s.id)));
        }

        [Test]
        public void LockedSetsAreLeftOutOfRewards()
        {
            Profile p = EmptyProfile();
            var open = new[] { "wrath", "sloth", "envy" };
            Assert.IsTrue(Gear.Unowned(db, p).All(k => open.Contains(db.gear.pieces[k].set)), "탑, 상점 후보에 잠긴 세트가 섞였습니다");
            while (Gear.AddItem(db, p) != null) { }
            Assert.IsTrue(p.items.All(i => open.Contains(Gear.Def(db, i).set)), "무작위 지급에 잠긴 세트가 섞였습니다");
            Assert.AreEqual(Enumerable.Range(0, db.gear.pieces.Length).Count(k => open.Contains(db.gear.pieces[k].set) && !Gear.IsPattern(db, k)), p.items.Count);

            // 탑 보상(쉬어가기 탑)도 열린 세트만
            RunState r = combat.CreateRun(EmptyProfile(), () => .3f);
            combat.BeginFloor(r);
            r.roundIndex = System.Array.IndexOf(db.rounds.sequence, RoundKind.Rest);
            combat.BeginRound(r);
            Assert.Greater(r.artifactOffer.Count, 0);
            Assert.IsTrue(r.artifactOffer.All(k => open.Contains(db.gear.pieces[k].set)), "탑 보상에 잠긴 세트가 섞였습니다");

            // 개발자 '잠금 무시'
            Unlocks.ignoreLocks = true;
            try { Assert.AreEqual(Enumerable.Range(0, db.gear.pieces.Length).Count(k => !Gear.IsPattern(db, k)), Gear.Unowned(db, EmptyProfile()).Count); }   // 도안은 늘 빠짐
            finally { Unlocks.ignoreLocks = false; }

            // 2층을 돌파하면 탐욕, 식탐이 열린다 (22차)
            Assert.IsTrue(Gear.Unowned(db, EmptyProfile(2)).Any(k => db.gear.pieces[k].set == "greed"));
            Assert.IsFalse(Gear.Unowned(db, EmptyProfile(2)).Any(k => db.gear.pieces[k].set == "pride"));
        }

        [Test]
        public void UnlockNoticeShowsOnce()
        {
            Profile p = EmptyProfile(0);
            Assert.AreEqual(0, Unlocks.Fresh(db, p).Count, "처음부터 열린 세트는 알리지 않는다");
            p.best = 2;
            CollectionAssert.AreEquivalent(new[] { "greed", "gluttony" }, Unlocks.Fresh(db, p).Select(s => s.id).ToArray());
            Unlocks.MarkSeen(p, Unlocks.Fresh(db, p));
            Assert.AreEqual(0, Unlocks.Fresh(db, p).Count);
            p.best = 3;
            CollectionAssert.AreEquivalent(new[] { "lust", "pride" }, Unlocks.Fresh(db, p).Select(s => s.id).ToArray());

            // 저장했다 불러와도 기록이 남는다
            Unlocks.MarkSeen(p, Unlocks.Fresh(db, p));
            Gear.Save(db, p);
            Profile loaded = Gear.Load(db);
            Assert.AreEqual(0, Unlocks.Fresh(db, loaded).Count);
            PlayerPrefs.DeleteKey(db.gear.saveKey);
        }

        // 20차: 챙길 퍼즐 칸 = 기본 + '퍼즐 칸 추가' 단계 × 칸 (데이터 기본 3, 단계마다 1 → 1단계 4칸, 2단계 5칸)
        [Test]
        public void StarterCellsGrowPerLevel()
        {
            UpgradeDef up = db.upgrades.FirstOrDefault(u => u.key == Unlocks.StarterKey);
            Assert.IsNotNull(up, "퍼즐 칸 추가(starter) 데이터가 없습니다");
            Profile p = EmptyProfile();
            p.starter = 1;
            Assert.AreEqual(4, Unlocks.StarterCells(db, p));
            p.starter = 2;
            Assert.AreEqual(5, Unlocks.StarterCells(db, p));
        }

        // 17차: 등반 조각은 등반 가방에만 쌓이고, 영구 프로필에는 시작 조각과 파편만 남는다
        [Test]
        public void RunPiecesStayInTheRunBag()
        {
            Profile p = EmptyProfile();
            p.best = 99;
            Gear.AddItem(db, p, Kind("wrath"));
            Gear.AddItem(db, p, Kind("wrath", 1));
            p.placements.Add(new Placement { id = p.items[0].id, x = 0, y = 0 });   // 하나만 들고 간다

            RunState r = combat.CreateRun(p, () => .5f);
            Assert.AreEqual(2, r.bag.items.Count, "20차: 챙긴 시작 조각은 판에 안 놓았어도 들고 간다 (가방 목록에)");
            Assert.AreEqual(1, Gear.Placed(db, r.bag).Count, "판에 놓은 것은 같은 자리에");

            int kind = Gear.Unowned(db, r.bag).First();
            r.artifactOffer.Clear();
            r.artifactOffer.Add(kind);
            combat.TakeArtifact(r, kind);
            Assert.IsTrue(Gear.Owns(r.bag, kind), "탑 보상은 등반 가방으로");
            Assert.AreEqual(2, p.items.Count, "영구 프로필의 조각은 그대로");
        }

        // 19차: 세로 줄 강화 = 고른 세로 줄 위 조각 능력치 +rowBonus
        [Test]
        public void ColumnUpgradeBoostsChosenColumns()
        {
            Profile p = EmptyProfile();
            Gear.AddItem(db, p, Kind("wrath"));
            PieceDef def = Gear.Def(db, p.items[0]);
            int cells = def.shape.Length;
            p.placements.Add(new Placement { id = p.items[0].id, x = 0, y = 3 });   // 위쪽 줄 강화에는 안 걸리는 아래 줄
            float plain = Gear.Stats(db, p)[def.stat];
            p.colLines.AddRange(Enumerable.Range(0, Gear.Dimensions(db, p).width));   // 모든 세로 줄
            float boosted = Gear.Stats(db, p)[def.stat];
            Assert.Greater(boosted, plain);
            Assert.AreEqual(def.value * (1 + db.gear.rowBonus), boosted, .001f);
            Assert.Greater(cells, 0);
        }

        // 19차: 옛 저장(version 1, 2)의 조각은 모두 버린다(시작 조각은 이제 한 번 쓰고 사라짐). 파편은 그대로,
        // 가로 줄 단계 n → 위 2n 줄, 확장 단계 e → 오른쪽과 아래에 줄 e 개씩.
        [Test]
        public void OldSaveDropsPiecesAndKeepsUpgrades()
        {
            var old = new Profile { version = 2, shards = 17, best = 2, rows = 1, cols = 1, expansion = 1 };
            old.rowLines = null; old.colLines = null;
            for (int k = 0; k < 6; k++) old.items.Add(new OwnedItem { id = k + 1, kind = k });
            old.placements.Add(new Placement { id = 1, x = 0, y = 0 });
            old.nextId = 7;
            PlayerPrefs.SetString(db.gear.saveKey, JsonUtility.ToJson(old));
            Profile loaded = Gear.Load(db);
            PlayerPrefs.DeleteKey(db.gear.saveKey);
            Assert.AreEqual(Profile.CurrentVersion, loaded.version);
            Assert.AreEqual(17, loaded.shards);
            Assert.AreEqual(2, loaded.best);
            Assert.AreEqual(0, loaded.items.Count, "옛 조각이 남았습니다");
            Assert.AreEqual(0, loaded.placements.Count);
            CollectionAssert.AreEqual(new[] { 0, 1 }, loaded.rowLines);
            CollectionAssert.AreEqual(new[] { 0, 1 }, loaded.colLines);
            Assert.AreEqual(1, loaded.extraCols);
            Assert.AreEqual(1, loaded.extraRows);
            Assert.AreEqual(2, loaded.expansion);
        }

        // 22차 신화 도안: 일기 장이 풀리면 영구 보유(단추 진열과 탑 보상에는 없음), 하나만 들고 가고, 흡수한 층이면 붉게 물든 모습 + 시작 욕망
        [Test]
        public void MythicPatternFollowsDiaryAndDesire()
        {
            DiaryEntry entry = db.diary.entries.First(e => e.patternKind >= 0 && e.patternTainted >= 0);
            Profile p = EmptyProfile();
            Assert.IsFalse(Mythic.Owned(db, p).Contains(entry), "돌파 전에는 없음");
            p.best = entry.floor;
            Assert.IsTrue(Mythic.Owned(db, p).Contains(entry), "돌파하면 영구 보유");
            Assert.IsFalse(Gear.Unowned(db, p).Contains(entry.patternKind), "도안이 탑 보상 후보에 있습니다");
            Assert.IsFalse(HubShop.Pool(db, p).Contains(entry.patternKind), "도안이 단추 진열 후보에 있습니다");

            Assert.IsTrue(Diary.MarkRead(p, entry));
            Assert.AreEqual(entry.readShards, p.shards);
            Assert.IsFalse(Diary.MarkRead(p, entry), "두 번 읽어도 보상은 한 번");

            Mythic.Toggle(db, p, entry.floor);
            Assert.IsTrue(Gear.Owns(p, entry.patternKind), "고르면 판 목록에 보통 도안");
            Assert.AreEqual(0, HubShop.PackedCells(db, p), "신화는 챙기기 칸에 안 듦");
            Assert.AreEqual(0, Unlocks.Cost(db, p, p.items[0]), "신화는 등급 합에 안 듦");
            Assert.AreEqual(0f, Mythic.DesireStart(db, p));

            Diary.RecordChoice(p, entry.floor, true);
            Mythic.Ensure(db, p);
            Assert.IsFalse(Gear.Owns(p, entry.patternKind));
            Assert.IsTrue(Gear.Owns(p, entry.patternTainted), "흡수한 층이면 붉게 물든 도안");
            Assert.Greater(Mythic.DesireStart(db, p), 0f);
            RunState r = combat.CreateRun(p, () => .5f);
            Assert.AreEqual(Mythic.DesireStart(db, p), r.desire, 1e-3f, "욕망을 안고 시작");

            Mythic.Toggle(db, p, entry.floor);
            Assert.AreEqual(0, p.items.Count, "다시 누르면 내려놓음");
        }

        // 22차: 신화 도안은 맞닿은 세트마다 그 세트 조각 1개로 친다
        [Test]
        public void MythicCountsForEveryTouchingSet()
        {
            DiaryEntry entry = db.diary.entries.First(e => db.gear.pieces[e.patternKind].shape.Length == 2);
            Profile p = EmptyProfile();
            p.best = 99;
            int w = Kind("wrath"), s = Kind("sloth");
            Gear.AddItem(db, p, w);
            Gear.AddItem(db, p, s);
            p.mythicPick = entry.floor;
            Mythic.Ensure(db, p);
            OwnedItem myth = p.items.First(i => Mythic.IsMythic(db, i.kind));
            p.placements.Add(new Placement { id = p.items[0].id, x = 0, y = 0 });
            p.placements.Add(new Placement { id = p.items[1].id, x = 3, y = 0 });
            int turns = db.gear.pieces[myth.kind].shape[1].x != 0 ? 0 : 1;   // 가로 2칸으로
            p.placements.Add(new Placement { id = myth.id, x = 1, y = 0, turns = turns });   // (1,0)(2,0): 왼쪽 분노, 오른쪽 나태와 맞닿음
            Assert.AreEqual(3, Gear.Placed(db, p).Count);
            GearStats stats = Gear.Stats(db, p);
            Assert.AreEqual(2, stats.groups["wrath"].size, "분노 1 + 도안");
            Assert.AreEqual(2, stats.groups["sloth"].size, "나태 1 + 도안");
            Assert.AreEqual(0, stats.groups["envy"].size, "안 맞닿은 세트에는 안 셈");
        }

        Vector2Int FreeSpot(Profile p, int kind)
        {
            var (w, h, _) = Gear.Dimensions(db, p);
            var probe = new OwnedItem { id = -1, kind = kind };
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (Gear.CanPlace(db, p, probe, 0, x, y)) return new Vector2Int(x, y);
            Assert.Fail("놓을 자리가 없습니다");
            return default;
        }

        // 19차: 챙긴 시작 조각은 다음 등반 가방에 들어가고, 등반을 시작하면 영구 프로필에서 사라진다
        [Test]
        public void PackedPiecesLastOneRun()
        {
            Profile p = EmptyProfile();
            HubShop.Restock(db, p, () => .2f, 1000);
            int kind = p.hubStock[0];
            p.shards = 99;
            p.starter = 10;
            Assert.AreEqual(PackResult.Packed, HubShop.Take(db, p, kind, out _));
            RunState r = combat.CreateRun(p, () => .5f);
            HubShop.ConsumePacked(p);
            Assert.IsTrue(Gear.Owns(r.bag, kind), "챙긴 조각이 등반 가방에 없습니다");
            Assert.AreEqual(0, p.items.Count, "등반을 시작했는데 챙긴 조각이 남았습니다");
            Assert.AreEqual(0, p.placements.Count);
        }

        // 19차 모이라 칸 강화: 판 가장자리 손잡이로 고른 가로 줄 또는 세로 줄 하나, 같은 줄은 다시 못 고름(단계는 둘을 같이 셈).
        // 확장은 고른 쪽에 줄이 붙고 위/왼쪽이면 조각과 줄 번호가 밀린다.
        [Test]
        public void MoiraUpgradesChosenLineAndSide()
        {
            UpgradeDef lines = db.upgrades.First(u => u.key == Moira.Lines);
            UpgradeDef expand = db.upgrades.First(u => u.key == Moira.Expansion);
            Assert.IsFalse(db.upgrades.Any(u => u.key is Moira.Rows or Moira.Cols), "옛 가로/세로 줄 강화가 데이터에 남아 있습니다");
            Profile p = EmptyProfile();
            p.shards = 999;
            Assert.IsTrue(Moira.Apply(db, p, lines, 2));
            CollectionAssert.AreEqual(new[] { 2 }, p.rowLines);
            Assert.AreEqual(999 - lines.price, p.shards);
            Assert.IsFalse(Moira.Apply(db, p, lines, 2), "같은 줄을 두 번 강화함");
            Assert.IsFalse(Moira.Apply(db, p, lines, 99), "판 밖 줄을 강화함");
            Assert.IsTrue(Moira.Apply(db, p, lines, Moira.ColBase + 0));
            CollectionAssert.AreEqual(new[] { 0 }, p.colLines);
            Assert.AreEqual(2, Moira.Level(p, Moira.Lines), "가로, 세로를 같이 센 단계");
            Assert.AreEqual(999 - lines.price * 2 - lines.priceStep, p.shards);

            Gear.AddItem(db, p, Kind("wrath"));
            p.placements.Add(new Placement { id = p.items[0].id, x = 1, y = 1 });
            var (w0, h0, limit0) = Gear.Dimensions(db, p);
            Assert.IsTrue(Moira.Apply(db, p, expand, Moira.Top));
            Assert.AreEqual(h0 + 1, Gear.Dimensions(db, p).height);
            Assert.AreEqual(w0, Gear.Dimensions(db, p).width);
            Assert.AreEqual(2, p.placements[0].y, "위에 줄을 붙였는데 조각이 안 밀림");
            CollectionAssert.AreEqual(new[] { 3 }, p.rowLines, "위에 줄을 붙였는데 강화한 줄 번호가 안 밀림");
            Assert.IsTrue(Moira.Apply(db, p, expand, Moira.Left));
            Assert.AreEqual(2, p.placements[0].x);
            CollectionAssert.AreEqual(new[] { 1 }, p.colLines);
            Assert.IsTrue(Moira.Apply(db, p, expand, Moira.Right));
            Assert.AreEqual(w0 + 2, Gear.Dimensions(db, p).width);
            Assert.AreEqual(limit0 + 3 * db.gear.costLimitStep, Gear.Dimensions(db, p).limit);
            Assert.AreEqual(3, Moira.Level(p, Moira.Expansion));
        }

        // 19차 운명 읽기: 파편을 내고 다음 등반 1층 속성을 정한다. 다시 읽으면 값을 안 내고 같은 속성, 등반 1층이 그 속성이 되고 쓰고 나면 비운다.
        [Test]
        public void FateSetsFirstFloorElement()
        {
            Assert.Greater(db.hub.fatePrice, 0);
            Assert.IsTrue(db.elements.defs.All(d => !string.IsNullOrEmpty(d.fateLine)), "속성마다 모이라의 암시(fateLine)가 있어야 합니다");
            Profile p = EmptyProfile();
            p.shards = db.hub.fatePrice - 1;
            Assert.IsNull(Moira.ReadFate(db, p, () => .5f), "파편이 모자라도 읽었음");
            p.shards = db.hub.fatePrice + 4;
            ElementDef read = Moira.ReadFate(db, p, () => .99f);
            Assert.IsNotNull(read);
            Assert.AreEqual(4, p.shards);
            Assert.AreSame(read, Moira.ReadFate(db, p, () => .1f), "다시 읽었는데 속성이 바뀜");
            Assert.AreEqual(4, p.shards, "다시 읽을 때 파편을 냄");
            for (int i = 0; i < 6; i++)
            {
                RunState r = combat.CreateRun(p, () => Random.value);
                combat.BeginFloor(r);
                Assert.AreEqual(read.element, r.floorElement, "1층 속성이 읽은 운명과 다릅니다");
            }
            Moira.ConsumeFate(p);
            Assert.IsFalse(Moira.FateRead(p));
        }

        // 17차: 시작 조각 가격 = 일반 3, 레어 4, 유니크 5, 레전더리 6
        [Test]
        public void StarterPricesByRank()
        {
            int[] want = { 0, 3, 4, 5, 6 };
            for (int cost = 1; cost <= 4; cost++)
            {
                int kind = System.Array.FindIndex(db.gear.pieces, d => d.cost == cost);
                if (kind >= 0) Assert.AreEqual(want[cost], HubShop.Price(db, kind), $"등급 {cost}");
            }
        }
    }
}
