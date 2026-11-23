// 15차 재봉실 허브 검증 (EditMode): 상인 단추 조각 상점, 허브 데이터. 값은 Main 씬의 GameDatabase 를 그대로 쓴다.
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
            foreach (HubStation s in System.Enum.GetValues(typeof(HubStation)))
                Assert.IsTrue(db.hub.facilities.Any(f => f.station == s && f.sprite != null && !string.IsNullOrEmpty(f.prompt)), $"시설 {s} 데이터(그림, 문구)가 없습니다");
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

        [Test]
        public void BuyingSpendsShardsAndAddsPiece()
        {
            Profile p = EmptyProfile();
            HubShop.Restock(db, p, () => .1f);
            Assert.IsNotEmpty(p.hubStock);
            int kind = p.hubStock[0];
            int price = HubShop.Price(db, kind);
            Assert.Greater(price, 0, "가격이 0 입니다");

            p.shards = price - 1;
            Assert.IsNull(HubShop.Buy(db, p, kind), "파편이 모자라도 샀음");
            Assert.IsFalse(Gear.Owns(p, kind));

            p.shards = price + 5;
            Assert.IsNotNull(HubShop.Buy(db, p, kind));
            Assert.AreEqual(5, p.shards);
            Assert.IsTrue(Gear.Owns(p, kind));
            Assert.IsTrue(HubShop.Sold(p, kind));
            p.shards = 999;
            Assert.IsNull(HubShop.Buy(db, p, kind), "같은 조각을 두 번 삼");

            // 진열이 모두 팔리면 Ensure 가 새로 채운다
            foreach (int k in p.hubStock.ToList()) Gear.AddItem(db, p, k);
            if (Gear.Unowned(db, p).Count > 0)
            {
                Assert.IsTrue(HubShop.Ensure(db, p, () => .5f));
                Assert.IsTrue(p.hubStock.All(k => !Gear.Owns(p, k)));
            }
        }
    }
}
