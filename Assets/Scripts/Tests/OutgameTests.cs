// 15차-outgame-unlock 검증 (EditMode): 세트 해금 표, 잠긴 세트 제외, 해금 알림 기록. 17차: 시작 조각, 등반 가방, 세로 줄, 옛 저장, 일기 보상, 가격. 값은 Main 씬 GameDatabase.
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

        [Test]
        public void UnlockTableFollowsBestFloor()
        {
            Assert.IsNotNull(db.gear.setUnlockFloor, "gear.setUnlockFloor 데이터가 없습니다");
            string Open(int best) => string.Join(",", db.gear.sets.Where(s => Unlocks.IsOpen(db, EmptyProfile(best), s.id)).Select(s => s.id));
            Assert.AreEqual("fire,ice,lightning", Open(0));
            Assert.AreEqual("fire,ice,lightning", Open(1));
            Assert.AreEqual("fire,ice,lightning,light,wind", Open(2));
            Assert.AreEqual("fire,ice,lightning,light,dark,blood,wind", Open(3));
            Assert.AreEqual(db.gear.sets.Length, db.gear.sets.Count(s => Unlocks.IsOpen(db, EmptyProfile(4), s.id)));
        }

        [Test]
        public void LockedSetsAreLeftOutOfRewards()
        {
            Profile p = EmptyProfile();
            var open = new[] { "fire", "ice", "lightning" };
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

            // 2층을 돌파하면 빛, 바람이 열린다
            Assert.IsTrue(Gear.Unowned(db, EmptyProfile(2)).Any(k => db.gear.pieces[k].set == "light"));
            Assert.IsFalse(Gear.Unowned(db, EmptyProfile(2)).Any(k => db.gear.pieces[k].set == "poison"));
        }

        [Test]
        public void UnlockNoticeShowsOnce()
        {
            Profile p = EmptyProfile(0);
            Assert.AreEqual(0, Unlocks.Fresh(db, p).Count, "처음부터 열린 세트는 알리지 않는다");
            p.best = 3;
            CollectionAssert.AreEquivalent(new[] { "light", "wind", "dark", "blood" }, Unlocks.Fresh(db, p).Select(s => s.id).ToArray());
            Unlocks.MarkSeen(p, Unlocks.Fresh(db, p));
            Assert.AreEqual(0, Unlocks.Fresh(db, p).Count);
            p.best = 4;
            CollectionAssert.AreEqual(new[] { "poison" }, Unlocks.Fresh(db, p).Select(s => s.id).ToArray());

            // 저장했다 불러와도 기록이 남는다
            Unlocks.MarkSeen(p, Unlocks.Fresh(db, p));
            Gear.Save(db, p);
            Profile loaded = Gear.Load(db);
            Assert.AreEqual(0, Unlocks.Fresh(db, loaded).Count);
            PlayerPrefs.DeleteKey(db.gear.saveKey);
        }

        // 17차: 들고 갈 시작 조각 수 = 단계마다 하나씩 (1단계 3개, 2단계 4개)
        [Test]
        public void StarterSlotsGrowPerLevel()
        {
            UpgradeDef up = db.upgrades.FirstOrDefault(u => u.key == Unlocks.StarterKey);
            Assert.IsNotNull(up, "실궤 강화 starter 데이터가 없습니다");
            Profile p = EmptyProfile();
            p.starter = 1;
            Assert.AreEqual(3, Unlocks.StarterSlots(db, p));
            p.starter = 2;
            Assert.AreEqual(4, Unlocks.StarterSlots(db, p));

            // 다 찼으면 새 조각은 못 놓고, 이미 놓인 조각은 옮길 수 있다
            p.starter = 0;
            int slots = Unlocks.StarterSlots(db, p);
            for (int i = 0; i < slots + 1; i++) Gear.AddItem(db, p);
            for (int i = 0; i < slots; i++) p.placements.Add(new Placement { id = p.items[i].id, x = i % 6, y = i / 6 });
            Assert.IsTrue(Unlocks.StarterFull(db, p, p.items[slots]));
            Assert.IsFalse(Unlocks.StarterFull(db, p, p.items[0]));
        }

        // 17차: 등반 조각은 등반 가방에만 쌓이고, 영구 프로필에는 시작 조각과 파편만 남는다
        [Test]
        public void RunPiecesStayInTheRunBag()
        {
            Profile p = EmptyProfile();
            p.best = 99;
            Gear.AddItem(db, p, Kind("fire"));
            Gear.AddItem(db, p, Kind("fire", 1));
            p.placements.Add(new Placement { id = p.items[0].id, x = 0, y = 0 });   // 하나만 들고 간다

            RunState r = combat.CreateRun(p, () => .5f);
            Assert.AreEqual(1, r.bag.items.Count, "판에 놓은 시작 조각만 들고 간다");
            Assert.AreEqual(1, Gear.Placed(db, r.bag).Count);

            int kind = Gear.Unowned(db, r.bag).First();
            r.artifactOffer.Clear();
            r.artifactOffer.Add(kind);
            combat.TakeArtifact(r, kind);
            Assert.IsTrue(Gear.Owns(r.bag, kind), "탑 보상은 등반 가방으로");
            Assert.AreEqual(2, p.items.Count, "영구 프로필의 조각은 그대로");
        }

        // 17차: 세로 줄 강화 = 왼쪽 두 칸 단위 세로 줄 위 조각 능력치 +rowBonus
        [Test]
        public void ColumnUpgradeBoostsLeftColumns()
        {
            Profile p = EmptyProfile();
            Gear.AddItem(db, p, Kind("fire"));
            PieceDef def = Gear.Def(db, p.items[0]);
            int cells = def.shape.Length;
            p.placements.Add(new Placement { id = p.items[0].id, x = 0, y = 3 });   // 위쪽 줄 강화에는 안 걸리는 아래 줄
            float plain = Gear.Stats(db, p)[def.stat];
            p.cols = 3;   // 왼쪽 6칸 모두
            float boosted = Gear.Stats(db, p)[def.stat];
            Assert.Greater(boosted, plain);
            Assert.AreEqual(def.value * (1 + db.gear.rowBonus), boosted, .001f);
            Assert.Greater(cells, 0);
        }

        // 17차: 옛 저장(version 1)의 조각은 등반 조각이라 처음 지급 조각만 남긴다. 파편은 그대로.
        [Test]
        public void OldSaveDropsRunPieces()
        {
            var old = new Profile { version = 1, shards = 17, best = 2 };
            for (int k = 0; k < 6; k++) old.items.Add(new OwnedItem { id = k + 1, kind = k });
            old.placements.Add(new Placement { id = 1, x = 0, y = 0 });
            old.nextId = 7;
            PlayerPrefs.SetString(db.gear.saveKey, JsonUtility.ToJson(old));
            Profile loaded = Gear.Load(db);
            PlayerPrefs.DeleteKey(db.gear.saveKey);
            Assert.AreEqual(Profile.CurrentVersion, loaded.version);
            Assert.AreEqual(17, loaded.shards);
            Assert.AreEqual(2, loaded.best);
            CollectionAssert.AreEquivalent(db.gear.startingKinds.Distinct().ToArray(), loaded.items.Select(i => i.kind).ToArray());
            Assert.AreEqual(0, loaded.placements.Count);
        }

        // 17차 일기 보상: 처음 읽으면 파편 한 번, 그 쪽지의 도안이 상인 진열에 붙고 탑 보상에는 안 나온다
        [Test]
        public void DiaryReadGivesShardsAndPattern()
        {
            DiaryEntry entry = db.diary.entries.First(e => e.patternKind >= 0);
            Profile p = EmptyProfile();
            p.best = entry.floor;
            Assert.IsFalse(Gear.Unowned(db, p).Contains(entry.patternKind), "도안이 탑 보상 후보에 있습니다");
            Assert.IsFalse(HubShop.Display(db, p).Contains(entry.patternKind), "읽기 전인데 도안이 진열됨");

            Assert.IsTrue(Diary.MarkRead(p, entry));
            Assert.AreEqual(entry.readShards, p.shards);
            Assert.Greater(entry.readShards, 0);
            Assert.IsFalse(Diary.MarkRead(p, entry), "두 번 읽어도 보상은 한 번");
            Assert.AreEqual(entry.readShards, p.shards);

            Assert.IsTrue(HubShop.Display(db, p).Contains(entry.patternKind));
            p.shards = 99;
            Assert.IsNotNull(HubShop.Buy(db, p, entry.patternKind));
            Assert.IsTrue(Gear.Owns(p, entry.patternKind));
            Assert.IsFalse(HubShop.Display(db, p).Contains(entry.patternKind), "산 도안은 진열에서 빠진다");
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
