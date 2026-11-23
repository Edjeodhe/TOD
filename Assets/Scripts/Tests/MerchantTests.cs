// 16차 상인 단추 검증 (EditMode): 대사 데이터, 자세 바꿔 끼우기, 트윈, 글자 찍힘. 값은 Main 씬의 GameDatabase 를 그대로 쓴다.
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TOD.Tests
{
    public class MerchantTests
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

        [Test]
        public void EveryCueHasLinesAndEveryMoodHasPose()
        {
            Assert.IsNotNull(db.merchant, "GameDatabase.merchant 가 비어 있습니다");
            foreach (MerchantCue cue in System.Enum.GetValues(typeof(MerchantCue)))
            {
                MerchantLines set = db.merchant.lines.FirstOrDefault(l => l.cue == cue);
                Assert.IsNotNull(set, $"{cue} 대사가 없습니다");
                Assert.GreaterOrEqual(set.lines.Length, 3, $"{cue} 대사는 3줄 이상");
                foreach (string line in set.lines) StringAssert.DoesNotContain("·", line, "게임 문구에 가운뎃점 금지");
            }
            foreach (MerchantMood mood in System.Enum.GetValues(typeof(MerchantMood)))
                Assert.IsTrue(db.merchant.poses.Any(p => p.mood == mood && p.sprite != null), $"{mood} 자세 그림이 없습니다");
            Assert.Greater(db.merchant.charsPerSecond, 0);
        }

        [Test]
        public void SayingSwapsPoseThenReturnsToIdle()
        {
            var talk = new MerchantTalk(db.merchant, () => .3f);
            string line = talk.Say(MerchantCue.Buy, 10);
            Assert.IsNotNull(line);
            MerchantMood buyMood = db.merchant.lines.First(l => l.cue == MerchantCue.Buy).mood;
            Assert.AreEqual(buyMood, talk.Mood);
            Assert.AreEqual(0, talk.Visible(10));
            Assert.AreEqual(line.Length, talk.Visible(10 + line.Length / db.merchant.charsPerSecond + .01f), "다 찍히지 않음");
            Assert.IsTrue(talk.Showing(10 + .1f));
            float end = 10 + talk.LineLength + db.merchant.poseHold + .1f;
            talk.Update(end);
            Assert.IsFalse(talk.Showing(end));
            Assert.AreEqual(MerchantMood.Idle, talk.Mood, "자세가 기본으로 돌아오지 않음");
            Assert.AreSame(talk.Sprite(MerchantMood.Idle), db.merchant.poses.First(p => p.mood == MerchantMood.Idle).sprite);
        }

        [Test]
        public void TweensMoveAndSettle()
        {
            var talk = new MerchantTalk(db.merchant, () => .5f);
            talk.SetMood(MerchantMood.Happy, 0);
            float maxHop = 0;
            for (float t = 0; t < db.merchant.hopTime; t += .02f) maxHop = Mathf.Max(maxHop, talk.Pose(t).hop);
            Assert.Greater(maxHop, db.merchant.hopHeight * .5f, "기쁨 깡충이 없음");
            talk.SetMood(MerchantMood.Refuse, 0);
            float maxAngle = 0;
            for (float t = 0; t < db.merchant.shakeTime; t += .02f) maxAngle = Mathf.Max(maxAngle, Mathf.Abs(talk.Pose(t).angle));
            Assert.Greater(maxAngle, db.merchant.shakeAngle * .3f, "거절 흔들기가 없음");
            // 오래 지나면 숨쉬기만 남는다
            MerchantFrame calm = talk.Pose(100);
            Assert.AreEqual(0, calm.hop, 1e-3f);
            Assert.AreEqual(1, calm.scaleY, db.merchant.breathAmount + 1e-3f);
        }

        [Test]
        public void ApproachRespectsCooldown()
        {
            var talk = new MerchantTalk(db.merchant, () => .1f);
            Assert.IsNotNull(talk.Approach(0));
            Assert.IsNull(talk.Approach(talk.LineLength + .5f < db.merchant.approachCooldown ? talk.LineLength + .5f : db.merchant.approachCooldown * .5f), "쿨다운 안에 또 혼잣말");
            Assert.IsNotNull(talk.Approach(db.merchant.approachCooldown + talk.LineLength + 1));
        }
    }
}
