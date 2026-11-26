// 24차 환경설정 검증 (EditMode): 키 바꾸기(겹치면 서로 바꿈), 문구 속 키 글자 바꾸기, 저장과 다시 읽기.
// 값은 Main 씬의 GameDatabase.settings 를 쓰고, 저장 키는 테스트용으로 바꿨다가 지운다.
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TOD.Tests
{
    public class SettingsTests
    {
        GameDatabase db;
        Scene opened;
        SettingsConfig cfg;

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
            cfg = JsonUtility.FromJson<SettingsConfig>(JsonUtility.ToJson(db.settings));   // 씬 값을 건드리지 않게 복사
            cfg.saveKey = db.settings.saveKey + ".tests";
        }

        [SetUp]
        public void Fresh()
        {
            PlayerPrefs.DeleteKey(cfg.saveKey);
            Settings.Load(cfg);
        }

        [OneTimeTearDown]
        public void Restore()
        {
            PlayerPrefs.DeleteKey(cfg.saveKey);
            Settings.Load(db.settings);
            if (opened.IsValid()) EditorSceneManager.CloseScene(opened, true);
        }

        [Test]
        public void DefaultsComeFromData()
        {
            Assert.AreEqual(Key.E, Settings.KeyOf(KeyAction.Interact));
            Assert.AreEqual(Key.W, Settings.KeyOf(KeyAction.MoveUp));
            Assert.AreEqual(Key.Digit1, Settings.KeyOf(KeyAction.Potion1));
            Assert.AreEqual(cfg.master, Settings.Now.master);
            foreach (KeyAction a in Settings.Actions) Assert.AreNotEqual(Key.None, Settings.KeyOf(a), $"{a} 기본 키가 없습니다");
        }

        [Test]
        public void BindingAUsedKeySwaps()
        {
            Settings.Bind(KeyAction.Interact, Key.I);   // I 는 재봉 상자 키 → 재봉 상자가 E 를 받는다
            Assert.AreEqual(Key.I, Settings.KeyOf(KeyAction.Interact));
            Assert.AreEqual(Key.E, Settings.KeyOf(KeyAction.Inventory));
        }

        [Test]
        public void RelabelSwapsKeyNamesInOnePass()
        {
            Assert.AreEqual("E  위로 올라가기", Settings.Relabel("E  위로 올라가기"));   // 안 바꿨으면 그대로
            Settings.Bind(KeyAction.Interact, Key.I);
            Assert.AreEqual("I 살펴보기     E 재봉 상자", Settings.Relabel("E 살펴보기     I 재봉 상자"));
            Settings.Bind(KeyAction.MoveUp, Key.T);
            Assert.AreEqual("TASD 걷기", Settings.Relabel("WASD 걷기"));
            Assert.AreEqual("IRA", Settings.Relabel("IRA"), "낱말 속 글자는 바꾸지 않습니다");
        }

        [Test]
        public void SavedSettingsReload()
        {
            Settings.Bind(KeyAction.Dash, Key.LeftShift);
            Settings.Now.master = .25f;
            Settings.Save();
            Settings.Load(cfg);
            Assert.AreEqual(Key.LeftShift, Settings.KeyOf(KeyAction.Dash));
            Assert.AreEqual(.25f, Settings.Now.master, 1e-4f);
            Settings.ResetKeys();
            Assert.AreEqual(Key.Space, Settings.KeyOf(KeyAction.Dash));
        }

        [Test]
        public void FixedKeysCannotBeBound()
        {
            Assert.IsTrue(Settings.Reserved(Key.Escape));
            Assert.IsTrue(Settings.Reserved(Key.UpArrow), "방향키는 이동 보조 키로 늘 같이 쓰인다");
            Assert.IsTrue(Settings.Reserved(Key.Numpad1));
            Assert.IsFalse(Settings.Reserved(Key.Q));
        }
    }
}
