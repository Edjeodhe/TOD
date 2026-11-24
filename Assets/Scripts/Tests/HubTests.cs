// 15차 재봉실 허브 검증 (EditMode): 상인 단추 진열과 챙기기(19차), 허브 데이터. 값은 Main 씬의 GameDatabase 를 그대로 쓴다.
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TOD.Tests
{
    public class HubTests
    {
        GameDatabase db;
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
        }

        [OneTimeTearDown]
        public void Close()
        {
            if (opened.IsValid()) EditorSceneManager.CloseScene(opened, true);
        }

        static Profile EmptyProfile() => new Profile { nextId = 1 };

        [Test]
        public void HubDataHasAllFiveStations()
        {
            Assert.IsNotNull(db.hub, "GameDatabase.hub 가 비어 있습니다");
            var all = db.hub.facilities.Concat(db.hub.attic?.facilities ?? new HubFacility[0]);   // 17차: 뚜껑문은 다락 방에, 19차: 모이라도
            foreach (HubStation s in System.Enum.GetValues(typeof(HubStation)))
                if (s == HubStation.SewingBox) Assert.IsFalse(all.Any(f => f.station == s), "19차: 아웃게임 재봉 상자가 남아 있습니다");
                else if (HubView.IsNpc(s)) Assert.IsTrue(all.Any(f => f.station == s && !string.IsNullOrEmpty(f.prompt) && f.height > 0), $"23차 NPC 자리 {s} 데이터가 없습니다");   // 그림은 리그(CutoutRig)
                else Assert.IsTrue(all.Any(f => f.station == s && f.sprite != null && !string.IsNullOrEmpty(f.prompt)), $"시설 {s} 데이터(그림, 문구)가 없습니다");
            Assert.Greater(db.hub.walkSpeed, 0);
            Assert.Greater(db.hub.interactRadius, 0);
            Assert.IsTrue(db.hub.walk.Contains(db.hub.heroStart) && db.hub.walk.Contains(db.hub.returnSpot), "시작/귀환 자리가 걷는 영역 밖입니다");
            foreach (string text in new[] { db.hub.shopTitle, db.hub.priceFormat, db.hub.soldOut, db.hub.bought, db.hub.noShards, db.hub.hint })
                StringAssert.DoesNotContain("·", text ?? "", "게임 문구에 가운뎃점 금지");
        }

        [Test]
        public void RestockOffersOnlyUnownedOpenPieces()
        {
            Profile p = EmptyProfile();
            p.best = 0;
            Gear.AddItem(db, p, 0);
            int roll = 0;
            HubShop.Restock(db, p, () => (roll++ * .37f) % 1f, 1000);
            Assert.AreEqual(Mathf.Min(db.hub.shopStock, Gear.Unowned(db, p).Count), p.hubStock.Count);
            Assert.AreEqual(p.hubStock.Count, p.hubStock.Distinct().Count(), "같은 조각이 두 번 진열됨");
            foreach (int k in p.hubStock)
            {
                Assert.IsFalse(Gear.Owns(p, k), "이미 가진 조각이 진열됨");
                Assert.IsTrue(Unlocks.KindOpen(db, p, k), "잠긴 세트 조각이 진열됨");
            }
        }

        // 20차 챙기기: 진열 카드를 고르면 파편을 내고 챙긴다(판에는 아직 안 놓음). 다시 누르면 되돌리고 파편을 돌려받는다.
        [Test]
        public void TakingSpendsShardsAndReturnRefunds()
        {
            Profile p = EmptyProfile();
            p.starter = 10;   // 칸은 넉넉히
            HubShop.Restock(db, p, () => .1f, 1000);
            Assert.IsNotEmpty(p.hubStock);
            int kind = p.hubStock[0];
            int price = HubShop.Price(db, kind);
            Assert.Greater(price, 0, "가격이 0 입니다");

            p.shards = price - 1;
            Assert.AreEqual(PackResult.NoShards, HubShop.Take(db, p, kind, out _));
            Assert.IsFalse(Gear.Owns(p, kind));

            p.shards = price + 5;
            Assert.AreEqual(PackResult.Packed, HubShop.Take(db, p, kind, out OwnedItem item));
            Assert.AreEqual(5, p.shards);
            Assert.IsTrue(Gear.Owns(p, kind));
            Assert.AreEqual(0, Gear.Placed(db, p).Count, "고른 조각은 판에 바로 놓이지 않고 재봉 상자에서 배치한다");
            Assert.IsTrue(HubShop.Taken(p, kind));
            Assert.AreEqual(PackResult.Taken, HubShop.Take(db, p, kind, out _), "같은 조각을 두 번 챙김");

            p.placements.Add(new Placement { id = item.id, x = 0, y = 0 });
            Assert.AreEqual(price, HubShop.Return(db, p, kind));
            Assert.AreEqual(price + 5, p.shards, "돌려준 파편이 맞지 않습니다");
            Assert.IsFalse(Gear.Owns(p, kind));
            Assert.AreEqual(0, p.placements.Count, "되돌린 조각의 배치가 남았습니다");
            Assert.IsFalse(HubShop.Taken(p, kind));
        }

        // 20차: 한도는 조각 수가 아니라 퍼즐 칸 수. 5칸이면 레전더리(4칸) + 일반(1칸)은 되고, 4칸 + 2칸은 안 된다. 한 진열에서 packPicks 개까지.
        [Test]
        public void TakingIsLimitedByCellsAndPicks()
        {
            int Find(int cells, params int[] not) => Enumerable.Range(0, db.gear.pieces.Length)
                .First(k => db.gear.pieces[k].shape.Length == cells && !Gear.IsPattern(db, k) && !not.Contains(k));
            int four = Find(4), one = Find(1), oneB = Find(1, one), two = Find(2);
            Profile p = EmptyProfile();
            p.best = 99;
            Assert.AreEqual(0, (5 - db.gear.starterSlotsBase) % Mathf.Max(1, db.gear.starterSlotsStep), "5칸이 되는 단계가 없습니다");
            p.starter = (5 - db.gear.starterSlotsBase) / Mathf.Max(1, db.gear.starterSlotsStep);
            Assert.AreEqual(5, Unlocks.StarterCells(db, p));
            p.shards = 999;
            p.hubStock = new System.Collections.Generic.List<int> { four, two, one, oneB };

            Assert.AreEqual(PackResult.Packed, HubShop.Take(db, p, four, out _));
            Assert.AreEqual(PackResult.TooBig, HubShop.Take(db, p, two, out _), "5칸인데 6칸을 챙김");
            Assert.AreEqual(PackResult.Packed, HubShop.Take(db, p, one, out _));
            Assert.AreEqual(5, HubShop.PackedCells(db, p));
            Assert.AreEqual(2, HubShop.Picks(db), "한 진열에서 고를 수 = 2 (데이터)");
            p.starter += 5;   // 칸이 남아도
            Assert.AreEqual(PackResult.PickLimit, HubShop.Take(db, p, oneB, out _), "고를 수를 넘김");
        }

        // 20차: 진열은 실제 시각으로 shopRefreshHours 마다 새로 뽑고, 그때 고른 기록도 비운다.
        [Test]
        public void StockRefreshesByTime()
        {
            Profile p = EmptyProfile();
            long refresh = HubShop.RefreshSeconds(db);
            Assert.AreEqual(2 * 3600, refresh, "진열 간격 = 2시간 (데이터)");
            Assert.IsTrue(HubShop.Ensure(db, p, () => .3f, 1000), "빈 진열을 채우지 않음");
            Assert.AreEqual(5, db.hub.shopStock, "진열 = 5개 (데이터)");
            Assert.AreEqual(db.hub.shopStock, p.hubStock.Count);
            p.hubTaken.Add(p.hubStock[0]);
            Assert.IsFalse(HubShop.Ensure(db, p, () => .7f, 1000 + refresh - 1), "시간이 안 됐는데 바뀜");
            Assert.AreEqual(1, p.hubTaken.Count);
            Assert.IsTrue(HubShop.Ensure(db, p, () => .7f, 1000 + refresh), "2시간이 지났는데 그대로");
            Assert.AreEqual(0, p.hubTaken.Count, "새 진열인데 고른 기록이 남음");
            Assert.AreEqual(1000 + refresh, p.hubStockTime);
        }

        Vector2Int FreeSpot(Profile p, int kind)
        {
            var (w, h, _) = Gear.Dimensions(db, p);
            var probe = new OwnedItem { id = -1, kind = kind };
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (Gear.CanPlace(db, p, probe, 0, x, y)) return new Vector2Int(x, y);
            return new Vector2Int(-1, -1);
        }
    }
}
