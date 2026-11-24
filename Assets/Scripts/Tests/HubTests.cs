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
            HubShop.Restock(db, p, () => (roll++ * .37f) % 1f);
            Assert.AreEqual(Mathf.Min(db.hub.shopStock, Gear.Unowned(db, p).Count), p.hubStock.Count);
            Assert.AreEqual(p.hubStock.Count, p.hubStock.Distinct().Count(), "같은 조각이 두 번 진열됨");
            foreach (int k in p.hubStock)
            {
                Assert.IsFalse(Gear.Owns(p, k), "이미 가진 조각이 진열됨");
                Assert.IsTrue(Unlocks.KindOpen(db, p, k), "잠긴 세트 조각이 진열됨");
            }
        }

        // 19차 챙기기: 진열 조각을 판에 놓으면 파편을 내고 산다. 들고 갈 수가 차면 못 놓고, 빼면 파편을 돌려준다.
        [Test]
        public void PackingSpendsShardsAndRefunds()
        {
            Profile p = EmptyProfile();
            HubShop.Restock(db, p, () => .1f);
            Assert.IsNotEmpty(p.hubStock);
            int kind = p.hubStock[0];
            int price = HubShop.Price(db, kind);
            Assert.Greater(price, 0, "가격이 0 입니다");
            Vector2Int at = FreeSpot(p, kind);

            p.shards = price - 1;
            Assert.AreEqual(PackResult.NoShards, HubShop.Pack(db, p, kind, at.x, at.y, 0, out _));
            Assert.IsFalse(Gear.Owns(p, kind));

            p.shards = price + 5;
            Assert.AreEqual(PackResult.Packed, HubShop.Pack(db, p, kind, at.x, at.y, 0, out OwnedItem item));
            Assert.AreEqual(5, p.shards);
            Assert.AreEqual(1, Gear.Placed(db, p).Count, "챙긴 조각이 판에 없습니다");
            Assert.IsFalse(HubShop.Offers(db, p).Contains(kind));
            Assert.AreEqual(PackResult.NotOffered, HubShop.Pack(db, p, kind, at.x, at.y, 0, out _), "같은 조각을 두 번 챙김");

            Assert.AreEqual(price, HubShop.Unpack(db, p, item.id));
            Assert.AreEqual(price + 5, p.shards, "돌려준 파편이 맞지 않습니다");
            Assert.IsFalse(Gear.Owns(p, kind));
            Assert.IsTrue(HubShop.Offers(db, p).Contains(kind), "돌려준 조각이 다시 진열되지 않습니다");

            // 들고 갈 수가 다 차면 더 못 챙긴다
            p.shards = 999;
            int slots = Unlocks.StarterSlots(db, p);
            int packed = 0;
            foreach (int k in HubShop.Offers(db, p).ToList())
            {
                if (packed == slots) { Assert.AreEqual(PackResult.Full, HubShop.Pack(db, p, k, 0, 0, 0, out _)); break; }
                Vector2Int spot = FreeSpot(p, k);
                if (spot.x >= 0 && HubShop.Pack(db, p, k, spot.x, spot.y, 0, out _) == PackResult.Packed) packed++;
            }
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
