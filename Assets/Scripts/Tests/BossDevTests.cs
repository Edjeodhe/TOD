// 23차-boss-dev 검증 (EditMode): 보스 처치 기억의 파편이 떨어졌다가 몸에 닿을 때 오르는지, 개발자 모드 라운드·층 옮기기.
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TOD.Tests
{
    public class BossDevTests
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

        RunState NewRun() => combat.CreateRun(new Profile { nextId = 1 }, () => .5f);

        // 보스를 잡으면: 처치 연출 동안은 파편이 그대로 → 연출이 끝나면 여러 알이 바닥에 떨어지고 → 몸에 닿을 때마다 조금씩 올라 합계가 맞는다.
        [Test]
        public void BossShardsFallThenAbsorb()
        {
            RewardConfig rw = db.rewards;
            int total = rw.shardBoss + rw.shardFloorClear;
            Assert.Greater(total, 0, "보스 파편 값이 없습니다");
            RunState r = NewRun();
            combat.BeginFloor(r);
            Assert.IsTrue(combat.DevGoToRound(r, RoundKind.Boss));
            int before = r.profile.shards;
            combat.DevEndRound(r);
            Assert.AreEqual(Phase.BossDying, r.phase);
            Assert.IsFalse(r.drops.Any(d => d.shard > 0), "처치 연출 동안에는 파편이 떨어지지 않는다");
            for (int i = 0; i < 60 * 6 && r.phase == Phase.BossDying; i++) combat.Update(r, 1 / 60f, new CombatInput());
            Assert.AreEqual(Phase.Purifying, r.phase);
            Assert.AreEqual(Mathf.Clamp(rw.bossShardPieces, 1, total), r.drops.Count(d => d.shower), "파편이 여러 알로 떨어진다");
            Assert.AreEqual(total, r.drops.Where(d => d.shower).Sum(d => d.shard), "알마다 나눠 담은 합 = 보스 + 층 돌파");
            Assert.AreEqual(before, r.profile.shards, "떨어진 순간에는 아직 오르지 않는다");
            int steps = 0;
            var seen = new System.Collections.Generic.HashSet<int> { r.profile.shards };
            for (int i = 0; i < 60 * 6 && r.drops.Any(d => d.shower); i++) { combat.Update(r, 1 / 60f, new CombatInput()); seen.Add(r.profile.shards); steps++; }
            Assert.IsFalse(r.drops.Any(d => d.shower), "모두 몸으로 빨려 든다");
            Assert.AreEqual(before + total, r.profile.shards, "흡수한 만큼 오른다");
            Assert.Greater(seen.Count, 2, "한 번에가 아니라 알마다 오른다");
            Assert.Less(steps, 60 * 4, "정화(4초)가 끝나기 전에 다 빨려 든다");
        }

        // 계단이 뜨기 전에 다음 라운드로 넘어가도 날아오던 파편은 잃지 않는다.
        [Test]
        public void PendingShardsAreKeptOnNextRound()
        {
            RunState r = NewRun();
            combat.BeginFloor(r);
            combat.DevGoToRound(r, RoundKind.Boss);
            int before = r.profile.shards;
            combat.DevEndRound(r);
            for (int i = 0; i < 60 * 6 && r.phase == Phase.BossDying; i++) combat.Update(r, 1 / 60f, new CombatInput());
            combat.Ascend(r);
            Assert.AreEqual(before + db.rewards.shardBoss + db.rewards.shardFloorClear, r.profile.shards);
        }

        // 개발자 모드: 쉬어가기, 지정 층 바로 가기
        [Test]
        public void DevJumpsToRoundAndFloor()
        {
            RunState r = NewRun();
            combat.BeginFloor(r);
            Assert.IsTrue(combat.DevGoToRound(r, RoundKind.Rest));
            Assert.AreEqual(RoundKind.Rest, r.roundKind);
            Assert.IsNotNull(r.altar, "쉬어가기 탑");
            combat.DevGoToFloor(r, 5);
            Assert.AreEqual(5, r.floor);
            Assert.AreEqual(1, r.round);
            combat.DevGoToFloor(r, 99);
            Assert.AreEqual(db.arena.finalFloor, r.floor, "마지막 층을 넘지 않는다");
        }
    }
}
