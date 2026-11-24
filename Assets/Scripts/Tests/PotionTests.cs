// 23차 포션 칸 검증 (EditMode). 값은 Main 씬의 GameDatabase.potions 를 그대로 쓴다.
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TOD.Tests
{
    public class PotionTests
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
        }

        [OneTimeTearDown]
        public void Close()
        {
            PlayerPrefs.DeleteKey(db.gear.saveKey);
            db.gear.saveKey = realSaveKey;
            if (opened.IsValid()) EditorSceneManager.CloseScene(opened, true);
        }

        RunState NewRun(float roll = .5f) => combat.CreateRun(new Profile { nextId = 1 }, () => roll);

        PotionDef Find(PotionEffect effect) => db.potions.potions.First(p => p.effect == effect);

        [Test]
        public void DataHasFiveSlotsAndThreeKinds()
        {
            Assert.IsNotNull(db.potions, "potions 데이터");
            Assert.AreEqual(5, db.potions.slots);
            Assert.GreaterOrEqual(db.potions.potions.Length, 3);
            foreach (PotionDef p in db.potions.potions)
            {
                Assert.IsNotNull(p.icon, p.id + " 아이콘");
                Assert.IsFalse(string.IsNullOrEmpty(p.name) || string.IsNullOrEmpty(p.text), p.id + " 이름, 설명");
                Assert.IsFalse(p.name.Contains("·") || p.text.Contains("·"), "가운뎃점 금지");
            }
            Assert.IsTrue(db.shop.wares.Any(w => Potions.Def(db, w.potion) != null), "상인이 포션을 판다");
        }

        [Test]
        public void RunStartsWithStartPotions()
        {
            RunState r = NewRun();
            Assert.AreEqual(5, r.potions.Length);
            Assert.AreEqual(db.potions.start.Length, r.potions.Sum(s => s.count));
        }

        [Test]
        public void AddStacksThenFillsEmptySlotsThenRefuses()
        {
            PotionSlot[] slots = Potions.NewSlots(db);
            foreach (PotionSlot s in slots) { s.id = null; s.count = 0; }
            string heal = Find(PotionEffect.Heal).id, pure = Find(PotionEffect.Purify).id;
            for (int i = 0; i < db.potions.stack; i++) Assert.AreEqual(0, Potions.Add(db, slots, heal), "같은 포션은 한 칸에 겹친다");
            Assert.AreEqual(1, Potions.Add(db, slots, heal), "가득 찬 칸 다음은 새 칸");
            Assert.AreEqual(2, Potions.Add(db, slots, pure));
            Assert.AreEqual(1, Potions.Add(db, slots, heal), "덜 찬 같은 칸이 먼저");
            while (Potions.CanAdd(db, slots, pure)) Potions.Add(db, slots, pure);
            Assert.IsTrue(slots.All(s => !s.Empty), "빈 칸이 다 찼다");
            Assert.AreEqual(-1, Potions.Add(db, slots, "nope"), "없는 포션");
        }

        [Test]
        public void HealPotionHealsAndIsConsumed()
        {
            RunState r = NewRun();
            foreach (PotionSlot s in r.potions) { s.id = null; s.count = 0; }
            PotionDef heal = Find(PotionEffect.Heal);
            Potions.Add(db, r.potions, heal.id);
            Potions.Add(db, r.potions, heal.id);
            Assert.IsFalse(combat.UsePotion(r, 0), "체력이 가득하면 마시지 않음");
            Assert.AreEqual(2, r.potions[0].count);
            r.hp = 10;
            Assert.IsTrue(combat.UsePotion(r, 0));
            Assert.AreEqual(Mathf.Min(r.maxHp, 10 + heal.amount), r.hp, .01f);
            Assert.AreEqual(1, r.potions[0].count);
            r.hp = 10;
            Assert.IsFalse(combat.UsePotion(r, 0), "마신 직후에는 쿨다운");
            r.time = r.potionReady + .01f;
            Assert.IsTrue(combat.UsePotion(r, 0));
            Assert.IsTrue(r.potions[0].Empty, "다 마시면 빈 칸");
            Assert.IsFalse(combat.UsePotion(r, 0), "빈 칸");
            Assert.IsFalse(combat.UsePotion(r, 9), "없는 칸");
        }

        [Test]
        public void PurifyAndYarnPotions()
        {
            RunState r = NewRun();
            foreach (PotionSlot s in r.potions) { s.id = null; s.count = 0; }
            PotionDef pure = Find(PotionEffect.Purify), yarn = Find(PotionEffect.Yarn);
            Potions.Add(db, r.potions, pure.id);
            Potions.Add(db, r.potions, yarn.id);
            r.desire = 50;
            Assert.IsTrue(combat.UsePotion(r, 0));
            Assert.AreEqual(Mathf.Max(0, 50 - pure.amount), r.desire, .01f);
            r.time = r.potionReady + .01f;
            r.yarn = 0;
            Assert.IsTrue(combat.UsePotion(r, 1));
            Assert.AreEqual(yarn.amount > 0 ? Mathf.Min(combat.YarnMax(r), yarn.amount) : combat.YarnMax(r), r.yarn, .01f);
        }

        [Test]
        public void EliteDropsPotionOnlyWhenRollHitsAndRoomExists()
        {
            RunState r = NewRun(0f);   // random() = 0 → 확률 통과
            EnemyType type = db.enemies.types.First(t => t.id != "boss");
            var elite = new Enemy { type = type, elite = true };
            var normal = new Enemy { type = type, elite = false };
            Assert.IsNotNull(Potions.EliteDrop(db, r, elite));
            Assert.IsNull(Potions.EliteDrop(db, r, normal), "일반 몹은 안 떨굼");
            RunState miss = NewRun(.999f);
            if (db.potions.eliteChance < .999f) Assert.IsNull(Potions.EliteDrop(db, miss, elite), "확률 실패");
            foreach (PotionSlot s in r.potions) { s.id = "__full"; s.count = 99; }
            Assert.IsNull(Potions.EliteDrop(db, r, elite), "칸이 가득이면 안 떨굼");
        }
    }
}
