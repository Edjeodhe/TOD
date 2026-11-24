// 22차 인벤토리 확장 강화 알림: 찢어진 종이 쪽지가 내려앉고 "칸이 확장되었습니다"(데이터)가 쓰인 뒤,
// 쪽지 오른쪽 아래에 붉은 인주 도장 자국이 '쿵' 하고 박힌다. 도장 몸통은 보이지 않고 자국만 크게 나타났다가 제자리로 눌리며 찍히고,
// 그 순간 쪽지가 들썩이며 흔들린다. 잠시 머문 뒤 저절로 사라진다. 화면(다락, 재봉 상자 판, 상점)과 상관없이 맨 위에 뜬다.
// 구조는 Main.uxml(stamp-note), 모양은 Moira.uss(.stamp-*), 시간과 크기는 데이터 hub.stampShow.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        VisualElement stampNote;
        float stampAt = -1;
        bool stampHit;
        Action<string> stampSound;

        // delay = 쪽지가 내려앉기 시작하기까지 기다림 (확장이면 판 위 실 잇기가 끝난 뒤, GrowLeft)
        public void ShowStamp(string text, Action<string> play, float delay = 0)
        {
            stampNote ??= root?.Q("stamp-note");
            StampShow s = db.hub.stampShow;
            if (stampNote == null || s == null) { Say(text); return; }
            Text(stampNote, "stamp-text", text ?? "");
            if (s.mark != null) stampNote.Q("stamp-mark").style.backgroundImage = new StyleBackground(s.mark);
            stampAt = Now + Mathf.Max(0, delay);
            stampHit = false;
            stampSound = play;
            Show(stampNote, true);
            TickStamp();
        }

        void HideStamp()
        {
            stampAt = -1;
            if (stampNote != null) Show(stampNote, false);
        }

        void TickStamp()
        {
            if (stampAt < 0 || stampNote == null) return;
            StampShow s = db.hub.stampShow;
            float t = Now - stampAt;
            if (t < 0) { stampNote.style.opacity = 0; stampNote.Q("stamp-mark").style.opacity = 0; return; }   // 아직 기다리는 중
            float hitAt = s.appear + s.stampDelay, fadeAt = hitAt + s.hold;
            if (t > fadeAt + s.fade) { HideStamp(); return; }

            // 쪽지: 위에서 살짝 기울어 내려앉음, 끝나면 옅어지며 떠오름
            float a = EaseOutBack(Mathf.Clamp01(t / Mathf.Max(.01f, s.appear)));
            float out_ = EaseOutCubic(Mathf.Clamp01((t - fadeAt) / Mathf.Max(.01f, s.fade)));
            VisualElement paper = stampNote.Q("stamp-paper");
            stampNote.style.opacity = Mathf.Clamp01(t / Mathf.Max(.01f, s.appear * .6f)) * (1 - out_);

            // 쿵: 찍힌 순간 쪽지가 눌렸다 튀고 흔들림 (줄어드는 떨림)
            float k = t - hitAt;
            float jolt = k >= 0 && k < s.shakeTime ? 1 - k / s.shakeTime : 0;
            Vector2 shake = jolt > 0 ? new Vector2(Mathf.Sin(k * 83) , Mathf.Cos(k * 71)) * s.shake * jolt * jolt : Vector2.zero;
            float press = k >= 0 ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(k / .16f)) * s.press : 0;   // 눌렸다 돌아옴
            paper.style.translate = new Translate(shake.x, Mathf.Lerp(-28, 0, a) - out_ * 12 + shake.y + press * 40);
            paper.style.rotate = new Rotate(Mathf.Lerp(-4, -1, a) + shake.x * .08f);
            paper.style.scale = new Scale(Vector2.one * (Mathf.Lerp(.88f, 1, a) - press));

            // 도장 자국: 보이지 않다가 크게 나타나 제자리로 눌리며 박힘 (punch → 살짝 작게 → 1)
            VisualElement mark = stampNote.Q("stamp-mark");
            if (k < 0) { mark.style.opacity = 0; return; }
            if (!stampHit) { stampHit = true; stampSound?.Invoke(s.sound); }
            float p = Mathf.Clamp01(k / Mathf.Max(.01f, s.punchTime));
            float scale = p < .6f ? Mathf.Lerp(s.punchFrom, .9f, EaseOutCubic(p / .6f)) : Mathf.Lerp(.9f, 1, EaseOutCubic((p - .6f) / .4f));
            mark.style.scale = new Scale(Vector2.one * scale);
            mark.style.opacity = Mathf.Clamp01(k / .04f) * Mathf.Lerp(.55f, 1, p);
            // 찍히는 순간 둘레에 번지는 인주 기운 (짧게)
            VisualElement ink = stampNote.Q("stamp-ink");
            float burst = Mathf.Clamp01(k / .35f);
            ink.style.opacity = (1 - burst) * .7f;
            ink.style.scale = new Scale(Vector2.one * Mathf.Lerp(.8f, 1.5f, EaseOutCubic(burst)));
        }
    }
}
