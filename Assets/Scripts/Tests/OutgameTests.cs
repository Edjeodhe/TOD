// 15차-outgame-unlock 검증 (EditMode): 세트 해금 표, 잠긴 세트 제외, 해금 알림 기록, 다락 '시작 조각'. 값은 Main 씬 GameDatabase.
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
            Assert.AreEqual(db.gear.pieces.Count(d => open.Contains(d.set)), p.items.Count);

            // 탑 보상(쉬어가기 탑)도 열린 세트만
            RunState r = combat.CreateRun(EmptyProfile(), () => .3f);
            combat.BeginFloor(r);
            r.roundIndex = System.Array.IndexOf(db.rounds.sequence, RoundKind.Rest);
            combat.BeginRound(r);
            Assert.Greater(r.artifactOffer.Count, 0);
            Assert.IsTrue(r.artifactOffer.All(k => open.Contains(db.gear.pieces[k].set)), "탑 보상에 잠긴 세트가 섞였습니다");

            // 개발자 '잠금 무시'
            Unlocks.ignoreLocks = true;
            try { Assert.AreEqual(db.gear.pieces.Length, Gear.Unowned(db, EmptyProfile()).Count); }
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

        [Test]
        public void StarterPieceIsPlacedAndFree()
        {
            UpgradeDef up = db.upgrades.FirstOrDefault(u => u.key == Unlocks.StarterKey);
            Assert.IsNotNull(up, "다락 강화 starter 데이터가 없습니다");
            Assert.Greater(up.max, 0);

            Profile p = EmptyProfile();
            int kind = Kind("fire");
            Gear.AddItem(db, p, kind);
            p.starterKind = kind;
            Assert.IsNull(Unlocks.PlaceStarter(db, p), "강화를 사기 전에는 놓지 않는다");

            p.starter = 1;
            PieceDef placed = Unlocks.PlaceStarter(db, p);
            Assert.IsNotNull(placed);
            Assert.AreEqual(1, p.placements.Count);
            Assert.AreEqual(0, Unlocks.Cost(db, p, p.items[0]), "시작 조각은 등급 한도에 들지 않는다");
            Assert.IsNull(Unlocks.PlaceStarter(db, p), "이미 판에 있으면 다시 놓지 않는다");

            // 같은 세트 조각은 시작 조각 옆에 붙는다
            Gear.AddItem(db, p, Kind("fire", 1));
            OwnedItem second = p.items[1];
            p.starterKind = second.kind;
            Assert.IsNotNull(Unlocks.PlaceStarter(db, p));
            Assert.AreEqual(2, Gear.Stats(db, p).groups["fire"].size, "같은 세트와 맞닿게 놓여야 한다");

            // 등반 시작 흐름: 판이 비어 있으면 시작 조각이 놓인 채로 등반이 시작된다
            p.placements.Clear();
            Unlocks.PlaceStarter(db, p);
            RunState r = combat.CreateRun(p, () => .5f);
            Assert.AreEqual(1, Gear.Placed(db, r.profile).Count);
        }
    }
}
