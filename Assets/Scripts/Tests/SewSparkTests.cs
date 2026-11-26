// 24차-hit-sparks: 당기기로 맞힌 적마다 바느질 타격 파티클 Fx (타수, 결박 → 죄종 색), 데이터 배율. 값은 Main 씬 GameDatabase 그대로.
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TOD.Tests
{
    public class SewSparkTests
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

        Enemy Add(RunState r, string type, float x, float y)
        {
            EnemyType def = db.Enemy(type);
            var e = new Enemy { x = x, y = y, type = def, hp = def.hp * 100, maxHp = def.hp * 100, radius = def.radius, speed = 0, damage = 0, wait = 99, action = EnemyAction.Move };
            e.height = combat.EnemyHeight(e);
            r.enemies.Add(e);
            return e;
        }

        Fx PullOnce(int strands, Element element)
        {
            RunState r = NewRun();
            r.player.x = 500; r.player.y = 350;
            Enemy a = Add(r, "boss", 700, 350);
            for (int i = 0; i < strands; i++)
                r.strands.Add(new Strand { x = a.x, y = a.y, target = a, state = StrandState.Stuck, element = element });
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
            return r.effects.FirstOrDefault(f => f.kind == FxKind.Sew);
        }

        [Test]
        public void SparkDataIsFilled()
        {
            SewSparkConfig s = db.fx.sparks;
            Assert.IsNotNull(s, "fx.sparks 데이터");
            Assert.Greater(s.time, 0);
            Assert.IsTrue(s.burst != null && s.burst.Has, "가운데 터짐 그림");
            Assert.GreaterOrEqual(s.pieces?.Count(p => p?.clip != null && p.clip.Has) ?? 0, 5, "조각 다섯 종류 그림");
        }

        // 타수가 많을수록 더 많이·크게, 결박이면 더
        [Test]
        public void MoreHitsMeansMoreAndBigger()
        {
            SewSparkConfig s = db.fx.sparks;
            Assert.Greater(s.Count(3, false), s.Count(1, false));
            Assert.Greater(s.Scale(3, false), s.Scale(1, false));
            Assert.Greater(s.BurstSize(3, false), s.BurstSize(1, false));
            Assert.GreaterOrEqual(s.Count(1, true), s.Count(1, false));
            Assert.Greater(s.Scale(1, true), s.Scale(1, false));
            Assert.AreEqual(s.Count(s.maxHits, false), s.Count(s.maxHits + 10, false), "타수 상한");
        }

        [Test]
        public void PullHitSpawnsSewSparks()
        {
            Fx one = PullOnce(1, Element.Plain);
            Assert.IsNotNull(one, "맞힌 자리에 바느질 파티클");
            Assert.AreEqual(1, one.pop, "타수 1");
            Assert.IsFalse(one.bound);
            Assert.IsNull(one.color, "결박이 아니면 죄종 색 없음");
            Assert.AreEqual(db.fx.sparks.time, one.time, 1e-3f);

            int bindAt = Mathf.Max(2, db.bind.strands);
            Fx bound = PullOnce(bindAt, Element.Lust);
            Assert.IsNotNull(bound);
            Assert.AreEqual(bindAt, bound.pop, "타수 = 꽂힌 바늘 수");
            Assert.IsTrue(bound.bound, "결박");
            Assert.AreEqual(Element.Lust, bound.element);
            Assert.AreEqual(db.Tone(Element.Lust).color, bound.color.Value, "결박이면 죄종 색");
        }
    }
}
