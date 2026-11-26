// 24차-hit-audio: 당기기 타격음 겹 (이벤트와 겹 계산). 값은 Main 씬 GameDatabase 그대로.
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TOD.Tests
{
    public class HitAudioTests
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

        Enemy Add(RunState r, string type, float x, float y, float hp = 0)
        {
            EnemyType def = db.Enemy(type);
            float life = hp > 0 ? hp : def.hp;
            var e = new Enemy { x = x, y = y, type = def, hp = life, maxHp = life, radius = def.radius, speed = 0, damage = 0, wait = 99, action = EnemyAction.Move };
            e.height = combat.EnemyHeight(e);
            r.enemies.Add(e);
            return e;
        }

        void Pin(RunState r, Enemy e, int strands = 1)
        {
            for (int i = 0; i < strands; i++)
                r.strands.Add(new Strand { x = e.x, y = e.y, target = e, state = StrandState.Stuck });
        }

        void Pull(RunState r)
        {
            r.events.Clear();
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(500, 0) });
        }

        static GameEvent? Find(RunState r, string type) =>
            r.events.Any(e => e.type == type) ? r.events.First(e => e.type == type) : null;

        [Test]
        public void PullHitSendsLayeredEventWithHitCount()
        {
            RunState r = NewRun();
            r.player.x = 500; r.player.y = 350;
            Enemy boss = Add(r, "boss", 700, 350);
            Pin(r, boss);
            Pull(r);
            Assert.AreEqual(1, Find(r, "hit")?.count, "한 타 = hit 세기 1");
            Assert.IsNull(Find(r, "pullkill"), "안 죽으면 처치 강조 없음");
            Assert.IsNotNull(Find(r, "pull"), "당기는 소리(pull)는 그대로");

            RunState empty = NewRun();
            empty.player.x = 500; empty.player.y = 350;
            empty.strands.Add(new Strand { x = 800, y = 350, state = StrandState.Stuck });
            Pull(empty);
            Assert.IsNull(Find(empty, "hit"), "헛당기기는 타격음 없음");
        }

        [Test]
        public void KillsAndBindingAddAccentEvents()
        {
            RunState r = NewRun();
            r.player.x = 500; r.player.y = 350;
            for (int i = 0; i < 3; i++) Pin(r, Add(r, "stalker", 600 + i * 40, 350 + i * 60, 1));
            Pull(r);
            Assert.GreaterOrEqual(Find(r, "hit")?.count ?? 0, 3, "세 마리 = 세 타 이상");
            Assert.AreEqual(3, Find(r, "pullkill")?.count, "처치 강조 세기 = 처치 수");

            RunState b = NewRun();
            b.player.x = 500; b.player.y = 350;
            Pin(b, Add(b, "boss", 700, 350), db.bind.strands);
            Pull(b);
            Assert.IsNotNull(Find(b, "bind"), "결박 강조");
            Assert.AreEqual(db.bind.strands, Find(b, "hit")?.count, "결박 = 가닥 수만큼 타");
        }

        [Test]
        public void CritAddsOneAccentPerPull()
        {
            Assert.Greater(db.hero.critChance, 0, "치명타 확률 데이터");
            RunState r = NewRun(0);   // 난수 0 = 치명타
            r.player.x = 500; r.player.y = 350;
            for (int i = 0; i < 2; i++) Pin(r, Add(r, "boss", 650 + i * 80, 300 + i * 100));
            Pull(r);
            Assert.AreEqual(1, r.events.Count(e => e.type == "hitcrit"), "치명타 강조는 당기기마다 한 번");
            Assert.AreEqual(2, Find(r, "hitcrit")?.count, "세기 = 치명타 맞은 적 수");
        }

        [Test]
        public void CueDataPointsAtRealSounds()
        {
            Assert.IsNotNull(db.hitAudio?.cues, "hitAudio 데이터");
            foreach (string trigger in new[] { "hit", "pullkill", "hitcrit", "bind" })
                Assert.IsTrue(SoundCues.Has(db.hitAudio, trigger), $"{trigger} 겹");
            foreach (SoundCue c in db.hitAudio.cues)
            {
                SynthSound s = Array.Find(db.sounds, x => x.id == c.sound);
                Assert.IsNotNull(s, $"{c.trigger} → {c.sound} 소리 없음");
                if (c.sound.StartsWith("hit_")) Assert.IsNotNull(s.sample, $"{c.sound} WAV 연결");
            }
            Assert.GreaterOrEqual(db.hitAudio.cues.Select(c => c.sound).Where(x => x.StartsWith("hit_")).Distinct().Count(), 3, "hit 은 쿵·찢김·튕김 세 겹 이상");
        }

        [Test]
        public void MoreHitsMakeThickerLayersAndJitterStaysInRange()
        {
            float Total(int count) => SoundCues.Plan(db.hitAudio, "hit", count, () => .5f).Sum(s => s.volume);
            int Shots(int count) => SoundCues.Plan(db.hitAudio, "hit", count, () => .5f).Count;
            Assert.Greater(Shots(6), Shots(1), "타수가 많으면 겹(복사본)이 늘어남");
            Assert.Greater(Total(6), Total(1), "타수가 많으면 더 큼");

            foreach (SoundCue c in db.hitAudio.cues.Where(c => c.trigger == "hit" && c.minCount <= 1))
            {
                float basePitch = c.pitch > 0 ? c.pitch : 1;
                var low = SoundCues.Plan(db.hitAudio, "hit", 1, () => 0f).First(s => s.sound == c.sound);
                var high = SoundCues.Plan(db.hitAudio, "hit", 1, () => 1f).First(s => s.sound == c.sound);
                Assert.AreEqual(basePitch - c.pitchJitter, low.pitch, 1e-4f, $"{c.sound} 피치 아래 끝");
                Assert.AreEqual(basePitch + c.pitchJitter, high.pitch, 1e-4f, $"{c.sound} 피치 위 끝");
                Assert.Greater(c.pitchJitter + c.volumeJitter, 0, $"{c.sound} 반복해도 같은 소리가 안 나게 흔들기");
            }

            var cfg = new HitAudioConfig { cues = new[] { new SoundCue { trigger = "t", sound = "s", volume = 1, minCount = 3 } } };
            Assert.AreEqual(0, SoundCues.Plan(cfg, "t", 2, () => .5f).Count, "minCount 아래면 그 겹 없음");
            Assert.AreEqual(1, SoundCues.Plan(cfg, "t", 3, () => .5f).Count);
        }
    }
}
