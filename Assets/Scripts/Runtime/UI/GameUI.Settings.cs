// 24차 환경설정 창 (Esc): 소리 / 조작 / 그래픽 탭. 구조는 Main.uxml dialog-settings, 모양은 Settings.uss.
// 값을 바꾸면 GameController 가 적용하고 Settings.Save → Settings.Changed → RefreshSettings 로 창과 키 안내 글자를 다시 그린다.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        static readonly string[] SettingsTabs = { "sound", "keys", "video" };

        VisualElement settingsDialog;
        Slider masterSlider, sfxSlider, shakeSlider;
        readonly Dictionary<KeyAction, Button> bindButtons = new();
        readonly List<(Label label, string text)> keyedLabels = new();
        string settingsTab = "sound";
        string hubRoomHint;
        KeyAction? listening;
        int capturedFrame = -1;

        // 새 키를 기다리는 중이거나, 이번 프레임에 키를 받았다 (그 키로 게임이 움직이지 않게)
        public bool SettingsCapturing => listening != null || capturedFrame == Time.frameCount;

        void InitSettings()
        {
            settingsDialog = root.Q("dialog-settings");
            if (settingsDialog == null) return;
            foreach (string tab in SettingsTabs)
            {
                string t = tab;
                settingsDialog.Q<Button>($"settings-tab-{t}").clicked += () => { ShowSettingsTab(t); StopListening(); };
            }
            masterSlider = SettingsSlider("set-master", v => game.SetMasterVolume(v));
            sfxSlider = SettingsSlider("set-sfx", v => game.SetSfxVolume(v));
            shakeSlider = SettingsSlider("set-shake", v => game.SetShake(v));
            settingsDialog.Q<Button>("set-master-mute").clicked += game.ToggleSound;
            settingsDialog.Q<Button>("settings-close").clicked += game.CloseSettings;
            settingsDialog.Q<Button>("set-keys-reset").clicked += () => { StopListening(); Settings.ResetKeys(); };

            Cycle("mode", StepScreenMode);
            Cycle("res", StepResolution);
            Cycle("vsync", _ => game.SetVsync(!Settings.Now.vsync));
            Cycle("fps", StepFrameCap);

            foreach (KeyAction a in Settings.Actions)
            {
                Button b = settingsDialog.Q<Button>($"bind-{a}");
                if (b == null) continue;
                KeyAction action = a;
                b.clicked += () => { b.Blur(); StartListening(action); };   // 포커스를 놓아야 Space, Enter 가 단추를 다시 누르지 않는다
                bindButtons[a] = b;
            }

            // 화면 곳곳의 키 안내 (기본 키 글자를 바꾼 키로)
            foreach (Label l in root.Query<Label>(className: "keyed").ToList()) keyedLabels.Add((l, l.text));
            VisualElement hints = root.Q("controls-hint");
            if (hints != null) foreach (Label l in hints.Query<Label>().ToList()) keyedLabels.Add((l, l.text));
        }

        Slider SettingsSlider(string name, System.Action<float> changed)
        {
            Slider s = settingsDialog.Q<Slider>(name);
            if (s == null) return null;
            // 채운 쪽을 포도주 색으로 (홈 안에 깔고 너비만 바꾼다)
            VisualElement tracker = s.Q(className: "unity-base-slider__tracker");
            if (tracker != null) { var fill = new VisualElement { name = "fill", pickingMode = PickingMode.Ignore }; fill.AddToClassList("settings-fill"); tracker.Add(fill); }
            s.RegisterValueChangedCallback(e => { changed(e.newValue / 100f); SliderFill(s); });
            return s;
        }

        static void SliderFill(Slider s)
        {
            VisualElement fill = s?.Q("fill");
            if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp(s.value, 0, 100));
        }

        void Cycle(string id, System.Action<int> step)
        {
            settingsDialog.Q<Button>($"set-{id}-prev").clicked += () => step(-1);
            settingsDialog.Q<Button>($"set-{id}-next").clicked += () => step(1);
        }

        public void ShowSettings(bool inRun)
        {
            if (settingsDialog == null) return;
            StopListening();
            Show(settingsDialog.Q("settings-paused"), inRun);
            Show(settingsDialog.Q("settings-run"), inRun);
            Show(settingsDialog.Q("settings-out"), !inRun);
            ShowSettingsTab(settingsTab);
            RefreshSettings();
            ShowDialog("settings");
        }

        void ShowSettingsTab(string tab)
        {
            settingsTab = tab;
            foreach (string t in SettingsTabs)
            {
                settingsDialog.Q($"settings-tab-{t}").EnableInClassList("selected", t == tab);
                Show(settingsDialog.Q($"settings-page-{t}"), t == tab);
            }
        }

        // 창 값, ♪ 빗금, 화면 곳곳의 키 안내를 지금 설정으로
        public void RefreshSettings()
        {
            SavedSettings s = Settings.Now;
            if (s == null || root == null) return;
            SettingsConfig c = db.settings;
            foreach (var (label, text) in keyedLabels) label.text = Settings.Relabel(text);
            if (db.hub != null) Text(root, "hub-hint", Settings.Relabel(hubRoomHint ?? db.hub.hint ?? ""));
            if (hudPotions != null) for (int i = 0; i < hudPotions.Count && i < 5; i++) { Label k = hudPotions[i].Q<Label>("key"); if (k != null) k.text = Settings.KeyName(KeyAction.Potion1 + i); }
            if (settingsDialog == null) return;

            SetSlider(masterSlider, s.master, "set-master-value", c);
            SetSlider(sfxSlider, s.sfx, "set-sfx-value", c);
            SetSlider(shakeSlider, s.shake, "set-shake-value", c);

            int mode = System.Array.IndexOf(GameController.ScreenModes, game.CurrentScreenMode);
            Text(settingsDialog, "set-mode-value", c.screenModeNames != null && mode >= 0 && mode < c.screenModeNames.Length ? c.screenModeNames[mode] : game.CurrentScreenMode.ToString());
            Vector2Int res = game.CurrentResolution;
            Text(settingsDialog, "set-res-value", string.Format(c.resolutionFormat ?? "{0} x {1}", res.x, res.y));
            Text(settingsDialog, "set-vsync-value", s.vsync ? c.on : c.off);
            Label fps = settingsDialog.Q<Label>("set-fps-value");
            fps.text = s.vsync ? c.vsyncNote : s.frameCap > 0 ? string.Format(c.fpsFormat ?? "{0}", s.frameCap) : c.unlimited;
            fps.EnableInClassList("off", s.vsync);
            settingsDialog.Q("set-fps-prev").SetEnabled(!s.vsync);
            settingsDialog.Q("set-fps-next").SetEnabled(!s.vsync);

            foreach (var (a, b) in bindButtons)
            {
                bool wait = listening == a;
                b.text = wait ? c.listening : Settings.KeyName(a);
                b.EnableInClassList("listening", wait);
            }
        }

        void SetSlider(Slider slider, float value, string label, SettingsConfig c)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(value * 100);
            SliderFill(slider);
            Text(settingsDialog, label, value <= .001f ? c.off : string.Format(c.percentFormat ?? "{0}%", Mathf.RoundToInt(value * 100)));
        }

        void StepScreenMode(int d)
        {
            FullScreenMode[] modes = GameController.ScreenModes;
            int i = System.Array.IndexOf(modes, game.CurrentScreenMode);
            game.SetScreenMode(modes[(Mathf.Max(0, i) + d + modes.Length) % modes.Length]);
        }

        void StepResolution(int d)
        {
            List<Vector2Int> list = game.Resolutions();
            int i = Mathf.Max(0, list.IndexOf(game.CurrentResolution));
            game.SetResolution(list[Mathf.Clamp(i + d, 0, list.Count - 1)]);
        }

        void StepFrameCap(int d)
        {
            int[] caps = db.settings.frameCaps;
            if (caps == null || caps.Length == 0) return;
            int i = System.Array.IndexOf(caps, Settings.Now.frameCap);
            if (i < 0) i = 0;
            game.SetFrameCap(caps[(i + d + caps.Length) % caps.Length]);
        }

        // ---------------------------------------------------------------- 키 받기

        void StartListening(KeyAction a)
        {
            listening = a;
            RefreshSettings();
        }

        void StopListening()
        {
            if (listening == null) return;
            listening = null;
            RefreshSettings();
        }

        // GameUI.Tick 에서 매 프레임: 기다리는 동안 처음 눌린 키를 그 행동에 준다. Esc = 취소.
        void TickSettings()
        {
            if (listening == null) return;
            if (Opened != "settings") { listening = null; return; }
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame) { capturedFrame = Time.frameCount; StopListening(); return; }
            foreach (var control in k.allKeys)
            {
                if (control == null || !control.wasPressedThisFrame || Settings.Reserved(control.keyCode)) continue;
                KeyAction a = listening.Value;
                listening = null;
                capturedFrame = Time.frameCount;
                Settings.Bind(a, control.keyCode);   // Save → Changed → RefreshSettings
                return;
            }
        }
    }
}
