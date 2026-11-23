// 15차 아버지의 일기 검증 (EditMode): 층 돌파 → 조각 해금, 보스 욕망 흡수/거절 기록 → 마지막 줄. 값은 Main 씬 GameDatabase.
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TOD.Tests
{
    public class DiaryTests
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
            // 층 돌파와 욕망 선택 때 프로필을 저장하므로 실제 저장 키를 건드리지 않게 바꿔 둔다.
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

        // 보스를 쓰러뜨려 정화가 시작될 때까지 (층 돌파 = profile.best 갱신)
        void ClearFloor(RunState r, int floor)
        {
            r.floor = floor;
            r.roundKind = RoundKind.Boss;
            r.phase = Phase.Combat;
            r.bossDeath = null;
            r.enemies.Clear();
            r.strands.Clear();
            r.player.x = 500; r.player.y = 350;
            EnemyType def = db.Enemy("boss");
            var boss = new Enemy { x = 700, y = 300, type = def, hp = 1, maxHp = def.hp, radius = def.radius, speed = 0, damage = 0, wait = 99, action = EnemyAction.Move };
            boss.height = combat.EnemyHeight(boss);
            r.enemies.Add(boss);
            r.strands.Add(new Strand { x = boss.x, y = boss.y, target = boss, state = StrandState.Stuck, element = Element.Plain });
            combat.Update(r, .0001f, new CombatInput { pull = true, aim = new Vector2(700, 300) });
            for (int i = 0; i < 60 * 6 && r.phase != Phase.Purifying; i++) combat.Update(r, 1 / 60f, new CombatInput());
            Assert.AreEqual(Phase.Purifying, r.phase, $"{floor}층 보스 처치 뒤 정화가 시작되지 않음");
        }

        [Test]
        public void DiaryHasFivePiecesWithBothEndings()
        {
            Assert.AreEqual(5, db.diary.entries.Length);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, Diary.All(db).Select(e => e.floor).ToArray());
            foreach (DiaryEntry e in db.diary.entries)
            {
                Assert.That(e.lines.Length, Is.InRange(3, 5), $"{e.floor}층 본문 줄 수");
                Assert.IsNotEmpty(e.absorbedLine);
                Assert.IsNotEmpty(e.refusedLine);
                Assert.AreNotEqual(e.absorbedLine, e.refusedLine);
                foreach (string text in e.lines.Append(e.title).Append(e.absorbedLine).Append(e.refusedLine))
                    StringAssert.DoesNotContain("·", text, "게임 문구에 가운뎃점 금지");
            }
        }

        [Test]
        public void FirstFloorClearUnlocksPiece()
        {
            var profile = new Profile { nextId = 1 };
            RunState r = combat.CreateRun(profile, () => .5f);
            Assert.AreEqual(0, Diary.Unlocked(db, profile).Count);

            ClearFloor(r, 1);
            Assert.AreEqual(1, profile.best);
            Assert.AreEqual(1, Diary.Unlocked(db, profile).Count, "1층 돌파 → 첫 조각");
            DiaryEntry first = Diary.Unlocked(db, profile)[0];
            Assert.IsTrue(Diary.IsNew(profile, first), "읽기 전에는 새 조각");
            Assert.AreEqual(1, Diary.PendingNotice(db, profile));

            Assert.AreEqual(1, Diary.TakeNotice(db, profile));
            Assert.AreEqual(0, Diary.PendingNotice(db, profile), "한 번 알리면 다시 알리지 않는다");
            Diary.MarkRead(profile, first);
            Assert.IsFalse(Diary.IsNew(profile, first));

            // 2층 돌파 → 두 번째 조각, 다시 1층을 깨도 늘지 않는다
            combat.ChooseDesire(r, null);
            ClearFloor(r, 2);
            Assert.AreEqual(2, Diary.Unlocked(db, profile).Count);
            Assert.AreEqual(1, Diary.PendingNotice(db, profile));
            ClearFloor(r, 1);
            Assert.AreEqual(2, profile.best);
            Assert.AreEqual(2, Diary.Unlocked(db, profile).Count);
        }

        [Test]
        public void AbsorbChoiceChangesLastLine()
        {
            var profile = new Profile { nextId = 1 };
            RunState r = combat.CreateRun(profile, () => .5f);
            DiaryEntry one = db.diary.entries.First(e => e.floor == 1), two = db.diary.entries.First(e => e.floor == 2);

            ClearFloor(r, 1);
            Assert.AreEqual(DiaryChoice.None, Diary.Choice(profile, 1));
            Assert.AreEqual(db.diary.undecidedLine, Diary.LastLine(db, profile, one), "고르기 전에는 번진 줄");

            combat.ChooseDesire(r, r.offer.id);   // 흡수
            Assert.AreEqual(DiaryChoice.Absorbed, Diary.Choice(profile, 1));
            Assert.AreEqual(one.absorbedLine, Diary.LastLine(db, profile, one));

            ClearFloor(r, 2);
            combat.ChooseDesire(r, null);         // 거절
            Assert.AreEqual(DiaryChoice.Refused, Diary.Choice(profile, 2));
            Assert.AreEqual(two.refusedLine, Diary.LastLine(db, profile, two));
            Assert.AreEqual(one.absorbedLine, Diary.LastLine(db, profile, one), "다른 층 기록은 그대로");

            // 같은 층을 다시 깨고 이번엔 거절 → 가장 최근 선택으로 덮는다
            ClearFloor(r, 1);
            combat.ChooseDesire(r, null);
            Assert.AreEqual(one.refusedLine, Diary.LastLine(db, profile, one));

            // 저장했다 불러와도 기록이 남는다
            Gear.Save(db, profile);
            Profile loaded = Gear.Load(db);
            CollectionAssert.AreEqual(profile.floorChoices, loaded.floorChoices);
            Assert.AreEqual(2, loaded.best);
        }
    }
}
