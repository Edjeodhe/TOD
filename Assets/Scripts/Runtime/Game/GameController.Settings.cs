// 24차 환경설정 (Esc 창): 음량, 그래픽 적용과 창 열고 닫기. 값과 저장은 Settings.cs, 창은 GameUI.Settings.cs.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public partial class GameController
    {
        void InitSettings()
        {
            ApplyAudio();
            ApplyVideo();
            Settings.Changed += ui.RefreshSettings;
            ui.RefreshSettings();
        }

        void OnDestroy() => Settings.Changed -= ui.RefreshSettings;

        // 재봉실, 결과 화면: 그냥 연다. 등반 중: 일시 정지와 같이 (TogglePause)
        public void OpenSettings()
        {
            if (Run != null) { if (!paused) TogglePause(); return; }
            if (Opened != "") return;
            ui.ShowSettings(false);
        }

        public void CloseSettings()
        {
            if (paused) TogglePause();
            else ui.CloseDialog();
        }

        // ---------------------------------------------------------------- 소리

        public void ApplyAudio()
        {
            SavedSettings s = Settings.Now;
            AudioListener.volume = s.master;
            sound.Volume = s.sfx;
            sound.Enabled = s.master > .001f;
            ui.SetSoundMuted(!sound.Enabled);
        }

        // 위 띠 ♪ 단추, 창 안 ♪ 단추: 끄면 직전 음량을 기억했다가 다시 켤 때 돌려놓는다
        public void ToggleSound()
        {
            SavedSettings s = Settings.Now;
            if (s.master > .001f) { s.lastMaster = s.master; s.master = 0; }
            else s.master = s.lastMaster > .01f ? s.lastMaster : db.settings.master > .01f ? db.settings.master : 1;
            ApplyAudio();
            Settings.Save();
            if (sound.Enabled) sound.Play("loot");
            ui.Say(sound.Enabled ? db.ui.soundOn : db.ui.soundOff);
        }

        public void SetMasterVolume(float v)
        {
            SavedSettings s = Settings.Now;
            s.master = Mathf.Clamp01(v);
            if (s.master > .01f) s.lastMaster = s.master;
            ApplyAudio();
            Settings.Save();
        }

        public void SetSfxVolume(float v)
        {
            Settings.Now.sfx = Mathf.Clamp01(v);
            ApplyAudio();
            Settings.Save();
            sound.Play("loot");   // 얼마나 큰지 들려준다
        }

        // ---------------------------------------------------------------- 그래픽

        public static readonly FullScreenMode[] ScreenModes = { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };

        public FullScreenMode CurrentScreenMode => Settings.Now.screenMode >= 0 ? (FullScreenMode)Settings.Now.screenMode : Screen.fullScreenMode;
        public Vector2Int CurrentResolution => Settings.Now.width > 0 ? new Vector2Int(Settings.Now.width, Settings.Now.height) : new Vector2Int(Screen.width, Screen.height);

        // 모니터가 지원하는 크기 (주사율만 다른 것은 하나로), 작은 것부터. 지금 크기는 늘 들어간다.
        public List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            Vector2Int min = db.settings.minResolution;
            foreach (Resolution r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= min.x && v.y >= min.y && !list.Contains(v)) list.Add(v);
            }
            Vector2Int now = CurrentResolution;
            if (!list.Contains(now)) list.Add(now);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        public void SetScreenMode(FullScreenMode mode) { Settings.Now.screenMode = (int)mode; ApplyVideo(); Settings.Save(); }

        public void SetResolution(Vector2Int size) { Settings.Now.width = size.x; Settings.Now.height = size.y; ApplyVideo(); Settings.Save(); }

        public void SetVsync(bool on) { Settings.Now.vsync = on; ApplyVideo(); Settings.Save(); }

        public void SetFrameCap(int fps) { Settings.Now.frameCap = fps; ApplyVideo(); Settings.Save(); }

        public void SetShake(float v) { Settings.Now.shake = Mathf.Clamp01(v); Settings.Save(); }

        void ApplyVideo()
        {
            SavedSettings s = Settings.Now;
            QualitySettings.vSyncCount = s.vsync ? 1 : 0;
            Application.targetFrameRate = s.frameCap > 0 ? s.frameCap : -1;   // 수직 동기화가 켜져 있으면 Unity 가 이 값을 무시한다
            if (Application.isEditor) return;   // 에디터 게임 창 크기는 건드리지 않는다
            if (s.screenMode < 0 && s.width <= 0) return;
            FullScreenMode mode = CurrentScreenMode;
            Vector2Int size = CurrentResolution;
            if (size.x != Screen.width || size.y != Screen.height || mode != Screen.fullScreenMode) Screen.SetResolution(size.x, size.y, mode);
        }

        // ---------------------------------------------------------------- 키

        // 방향 키 (기본 WASD, 방향키는 늘 같이)
        Vector2 MoveKeys() => new(
            (Settings.Held(KeyAction.MoveRight) ? 1 : 0) - (Settings.Held(KeyAction.MoveLeft) ? 1 : 0),
            (Settings.Held(KeyAction.MoveDown) ? 1 : 0) - (Settings.Held(KeyAction.MoveUp) ? 1 : 0));
    }
}
