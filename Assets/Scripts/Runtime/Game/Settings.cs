// 24차 환경설정: 플레이어가 바꾼 소리, 그래픽, 키 값. PlayerPrefs 에 JSON 으로 저장하고, 키 입력은 모두 여기를 거친다.
// 기본값은 GameDatabase.settings (SettingsData.cs). 창은 GameUI.Settings.cs, 적용은 GameController.Settings.cs.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TOD
{
    [Serializable]
    public class SavedSettings
    {
        public float master, sfx;             // 0~1
        public float lastMaster;              // 소리 단추로 끄기 직전 음량 (다시 켤 때 돌아감)
        public int screenMode = -1;           // FullScreenMode (-1 = 손대지 않음)
        public int width, height;             // 0 = 손대지 않음
        public bool vsync;
        public int frameCap;                  // 0 = 제한 없음
        public float shake;                   // 화면 흔들림 배율
        public KeyBind[] keys;
    }

    public static class Settings
    {
        static SettingsConfig cfg;
        static readonly Dictionary<KeyAction, Key> keys = new();
        static readonly Dictionary<KeyAction, List<Key>> alts = new();

        public static SavedSettings Now { get; private set; }
        public static event Action Changed;
        public static float Shake => Now != null ? Now.shake : 1;
        public static readonly KeyAction[] Actions = (KeyAction[])Enum.GetValues(typeof(KeyAction));

        public static void Load(SettingsConfig config)
        {
            cfg = config ?? new SettingsConfig();
            Now = Defaults();
            string json = string.IsNullOrEmpty(cfg.saveKey) ? null : PlayerPrefs.GetString(cfg.saveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { JsonUtility.FromJsonOverwrite(json, Now); }
                catch (Exception e) { Debug.LogWarning($"[TOD] 환경설정을 읽지 못해 기본값을 씁니다: {e.Message}"); Now = Defaults(); }
            }
            Now.master = Mathf.Clamp01(Now.master);
            Now.sfx = Mathf.Clamp01(Now.sfx);
            Rebuild();
        }

        static SavedSettings Defaults() => new()
        {
            master = cfg.master, sfx = cfg.sfx, lastMaster = cfg.master,
            vsync = cfg.vsync, frameCap = cfg.frameCap, shake = cfg.shake,
            keys = (KeyBind[])(cfg.keys ?? new KeyBind[0]).Clone(),
        };

        // 저장된 키 + (저장 뒤에 새로 생긴 행동은) 기본 키
        static void Rebuild()
        {
            keys.Clear();
            foreach (KeyBind b in cfg.keys ?? new KeyBind[0]) keys[b.action] = b.key;
            foreach (KeyBind b in Now.keys ?? new KeyBind[0]) if (b.key != Key.None) keys[b.action] = b.key;
            alts.Clear();
            foreach (KeyBind b in cfg.altKeys ?? new KeyBind[0])
            {
                if (!alts.TryGetValue(b.action, out var list)) alts[b.action] = list = new List<Key>();
                list.Add(b.key);
            }
            labelCache.Clear();
        }

        public static void Save()
        {
            var list = new List<KeyBind>();
            foreach (var kv in keys) list.Add(new KeyBind { action = kv.Key, key = kv.Value });
            Now.keys = list.ToArray();
            if (!string.IsNullOrEmpty(cfg?.saveKey)) { PlayerPrefs.SetString(cfg.saveKey, JsonUtility.ToJson(Now)); PlayerPrefs.Save(); }
            labelCache.Clear();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- 키

        public static Key KeyOf(KeyAction a) => keys.TryGetValue(a, out Key k) ? k : Key.None;
        static Key DefaultOf(KeyAction a) { foreach (KeyBind b in cfg?.keys ?? new KeyBind[0]) if (b.action == a) return b.key; return Key.None; }

        // 다른 행동이 이미 쓰는 키면 서로 바꾼다 (한 키 = 한 행동)
        public static void Bind(KeyAction a, Key k)
        {
            Key old = KeyOf(a);
            foreach (KeyAction other in Actions)
                if (other != a && KeyOf(other) == k) keys[other] = old;
            keys[a] = k;
            Save();
        }

        // 다시 정할 수 없는 키: Esc(창, 일시 정지), F1(개발자 패널), 윈도 키, 고정 보조 키(방향키, 숫자 패드)
        public static bool Reserved(Key k)
        {
            if (k is Key.None or Key.Escape or Key.F1 or Key.LeftMeta or Key.RightMeta or Key.ContextMenu or Key.PrintScreen) return true;
            foreach (var list in alts.Values) if (list.Contains(k)) return true;
            return false;
        }

        public static void ResetKeys()
        {
            Now.keys = new KeyBind[0];
            Rebuild();
            Save();
        }

        public static bool Pressed(KeyAction a) => Check(a, c => c.wasPressedThisFrame);
        public static bool Held(KeyAction a) => Check(a, c => c.isPressed);

        static bool Check(KeyAction a, Func<UnityEngine.InputSystem.Controls.KeyControl, bool> test)
        {
            Keyboard k = Keyboard.current;
            if (k == null) return false;
            Key main = KeyOf(a);
            if (main != Key.None && test(k[main])) return true;
            if (alts.TryGetValue(a, out var list))
                foreach (Key alt in list) if (alt != Key.None && test(k[alt])) return true;
            return false;
        }

        // 화면에 보일 키 이름 (짧게)
        public static string KeyName(Key k) => k switch
        {
            Key.None => "-",
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.NumpadEnter => "Num Enter",
            Key.Tab => "Tab",
            Key.Backspace => "Backspace",
            Key.LeftShift => "L Shift", Key.RightShift => "R Shift",
            Key.LeftCtrl => "L Ctrl", Key.RightCtrl => "R Ctrl",
            Key.LeftAlt => "L Alt", Key.RightAlt => "R Alt",
            Key.UpArrow => "↑", Key.DownArrow => "↓", Key.LeftArrow => "←", Key.RightArrow => "→",
            >= Key.Digit1 and <= Key.Digit9 => ((int)(k - Key.Digit1) + 1).ToString(),
            Key.Digit0 => "0",
            >= Key.Numpad0 and <= Key.Numpad9 => "Num " + (int)(k - Key.Numpad0),
            >= Key.A and <= Key.Z => k.ToString(),
            _ => Keyboard.current != null ? Keyboard.current[k].displayName : k.ToString(),
        };

        public static string KeyName(KeyAction a) => KeyName(KeyOf(a));

        // 화면 문구 속 기본 키 글자(E, I, R, Space, Enter, WASD)를 지금 키로 바꾼다. 바꾼 키가 없으면 그대로.
        // 숫자(포션 키)는 문구 속 숫자와 헷갈려서 바꾸지 않는다.
        static readonly Dictionary<string, string> labelCache = new();
        static readonly KeyAction[] Relabeled = { KeyAction.Dash, KeyAction.Interact, KeyAction.Inventory, KeyAction.Rotate, KeyAction.StartRun };

        public static string Relabel(string text)
        {
            if (string.IsNullOrEmpty(text) || cfg == null) return text;
            if (labelCache.TryGetValue(text, out string done)) return done;
            // 한 번에 바꾼다 (E ↔ I 처럼 서로 바꾼 키가 덮어쓰지 않게)
            var map = new Dictionary<string, string>();
            KeyAction[] move = { KeyAction.MoveUp, KeyAction.MoveLeft, KeyAction.MoveDown, KeyAction.MoveRight };
            string wasd = "", now = "";
            foreach (KeyAction a in move) { wasd += KeyName(DefaultOf(a)); now += KeyName(KeyOf(a)); }
            if (wasd != now) map[wasd] = now;
            foreach (KeyAction a in Relabeled)
            {
                Key from = DefaultOf(a), to = KeyOf(a);
                if (from != to && from != Key.None) map[KeyName(from)] = KeyName(to);
            }
            string s = text;
            if (map.Count > 0)
            {
                var tokens = new List<string>(map.Keys);
                tokens.Sort((x, y) => y.Length.CompareTo(x.Length));   // 긴 낱말(WASD, Space) 먼저
                string pattern = "(?<![A-Za-z0-9])(" + string.Join("|", tokens.ConvertAll(Regex.Escape)) + ")(?![A-Za-z0-9])";
                s = Regex.Replace(text, pattern, m => map[m.Value]);
            }
            if (labelCache.Count > 256) labelCache.Clear();
            labelCache[text] = s;
            return s;
        }
    }
}
