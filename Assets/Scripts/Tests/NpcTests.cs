// 23차 쉬어가기 NPC 4종 검증 (EditMode): 데이터, 쉬어가기 등장, 말 걸기 효과(축복, 조각, 경험치, 도박 두 번 E), 재봉실 선물(등반마다 한 번), 다음 등반 축복. 값은 Main 씬 GameDatabase.
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TOD.Tests
{
    public class NpcTests
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
            Space.Configure(db.arena);
            combat = new Combat(db);
        }

        [OneTimeTearDown]
        public void Close()
        {
            if (opened.IsValid()) EditorSceneManager.CloseScene(opened, true);
        }

        RunState RestRun()
        {
            var p = new Profile { nextId = 1 };
            var rng = new System.Random(23);
            RunState r = combat.CreateRun(p, () => (float)rng.NextDouble());
            combat.BeginFloor(r);
            for (int i = 0; i < 8 && r.roundKind != RoundKind.Rest; i++) combat.Ascend(r);
            Assert.AreEqual(RoundKind.Rest, r.roundKind, "쉬어가기 라운드에 가지 못함");
            return r;
        }

        [Test]
        public void NpcDataIsComplete()
        {
            Assert.IsNotNull(db.npcs);
            foreach (NpcKind kind in System.Enum.GetValues(typeof(NpcKind)))
            {
                NpcDef def = db.npcs.Get(kind);
                Assert.IsNotNull(def, $"{kind} 데이터 없음");
                Assert.IsNotNull(def.rig, $"{kind} 리그 JSON 없음");
                Assert.IsTrue(def.rewards != null && def.rewards.Length > 0, $"{kind} 보상 없음");
                Assert.IsTrue(def.hubRewards != null && def.hubRewards.Length > 0, $"{kind} 재봉실 보상 없음");
                Assert.IsTrue(db.hub.facilities.Any(f => f.station == def.station), $"{kind} 재봉실 자리 없음");
                foreach (string line in def.approachLines.Concat(def.giveLines).Concat(def.doneLines).Concat(def.hubLines))
                    StringAssert.DoesNotContain("·", line, "가운뎃점 금지");
            }
        }

        [Test]
        public void RestRoundSpawnsAnNpc()
        {
            float chance = db.npcs.chance;
            db.npcs.chance = 1;   // 확률 1 이면 반드시 나온다
            RunState r;
            try { r = RestRun(); }
            finally { db.npcs.chance = chance; }
            Assert.IsNotNull(r.npc, "쉬어가기에 NPC 가 나오지 않음");
            combat.Ascend(r);
            Assert.IsNull(r.npc, "다음 라운드에 NPC 가 남음");
        }

        [Test]
        public void BlessingOnceAndMetIsSaved()
        {
            RunState r = RestRun();
            combat.DevSummonNpc(r, NpcKind.Blessing);
            float damage = r.damage, hp = r.maxHp, fire = r.fireInterval, reach = r.reach, move = r.moveScale;
            NpcTalkResult first = combat.TalkNpc(r);
            Assert.IsTrue(first.rewarded && first.first);
            Assert.IsTrue(r.damage > damage || r.maxHp > hp || r.fireInterval < fire || r.reach > reach || r.moveScale > move, "축복이 능력치를 바꾸지 않음");
            CollectionAssert.Contains(r.profile.npcMet, (int)NpcKind.Blessing);
            float after = r.damage + r.maxHp + r.reach;
            NpcTalkResult again = combat.TalkNpc(r);
            Assert.IsFalse(again.rewarded, "두 번 받음");
            Assert.AreEqual(after, r.damage + r.maxHp + r.reach, 1e-4f);
        }

        [Test]
        public void GiftAndMemoryGiveRewards()
        {
            RunState r = RestRun();
            int items = r.bag.items.Count;
            combat.DevSummonNpc(r, NpcKind.Gift);
            combat.TalkNpc(r);
            Assert.AreEqual(items + 1, r.bag.items.Count, "행상 쥐가 조각을 주지 않음");
            combat.DevSummonNpc(r, NpcKind.Memory);
            int level = r.level, xp = r.xp;
            combat.TalkNpc(r);
            Assert.IsTrue(r.level > level || r.xp > xp, "촛불 유령이 경험치를 주지 않음");
        }

        [Test]
        public void GambleOffersThenPaysHealth()
        {
            RunState r = RestRun();
            combat.DevSummonNpc(r, NpcKind.Gamble);
            float hp = r.hp;
            NpcTalkResult offer = combat.TalkNpc(r);
            Assert.IsTrue(offer.offer, "첫 E 는 제안");
            Assert.AreEqual(hp, r.hp, "제안만 했는데 체력이 줄었음");
            Assert.AreEqual(1, r.npcStage);
            NpcTalkResult roll = combat.TalkNpc(r);
            Assert.IsTrue(roll.rewarded);
            Assert.AreEqual(2, r.npcStage);
            Assert.GreaterOrEqual(r.hp, 1, "도박으로 죽으면 안 됨");
        }

        [Test]
        public void HubGiftOncePerRunAndBoonAppliesNextRun()
        {
            var p = new Profile { nextId = 1 };
            NpcTalkResult none = combat.HubGift(p, NpcKind.Blessing, () => .5f);
            Assert.IsFalse(none.rewarded, "만나지 않은 NPC 가 선물을 줌");
            p.npcMet.Add((int)NpcKind.Blessing);
            Assert.IsTrue(combat.HubGift(p, NpcKind.Blessing, () => .5f).rewarded);
            Assert.IsFalse(combat.HubGift(p, NpcKind.Blessing, () => .5f).rewarded, "등반 사이에 두 번 받음");
            Assert.AreEqual(1, p.npcBoons.Count, "수녀의 재봉실 선물은 다음 등반 축복");
            p.npcGifted.Clear();   // 등반이 끝나면 (GameController.HubAfterRun)
            Assert.IsTrue(Combat.NpcCanGift(p, NpcKind.Blessing));

            RunState r = combat.CreateRun(p, () => .5f);
            float before = r.damage + r.maxHp + 1 / r.fireInterval;
            Assert.IsNotNull(combat.ApplyBoons(r));
            Assert.AreNotEqual(before, r.damage + r.maxHp + 1 / r.fireInterval, "축복이 걸리지 않음");
            Assert.AreEqual(0, p.npcBoons.Count, "축복은 한 번만");
        }
    }
}
