// 22차 욕망 최대치 강화 연출: 모이라 카드 위에 욕망 게이지(인게임 desire-bar 와 같은 잉크 틀, 보라 결)가 나타나고,
// 게이지 틀이 이전 최대치 길이에서 새 최대치 길이로 자라며 숫자가 함께 오른다. 새로 생긴 자리는 밝은 욕망으로 차오른 뒤 번쩍이고,
// 제목과 감성 문구(데이터 hub.vesselTitle, vesselLines)가 한 줄씩 떠오른 다음 저절로 사라진다.
// 구조는 Main.uxml(attic 안 vessel), 모양은 Moira.uss, 시간과 길이는 데이터 hub.vesselShow.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        const int VesselLineCount = 3;          // Main.uxml 의 vessel-line-0 ~ 2
        const float VesselInset = 19;           // 틀 안쪽 여백(bar-track 의 left 9 + right 10)

        VisualElement vessel;
        float vesselAt = -1, vesselOld, vesselNew;
        bool vesselGrowPlayed, vesselFullPlayed;
        Action<string> vesselSound;

        public bool VesselShowing => vesselAt >= 0;

        // 욕망 최대치 강화 직후: oldMax → newMax
        public void ShowVessel(float oldMax, float newMax, Action<string> play)
        {
            vessel ??= attic?.Q("vessel");
            if (vessel == null || db.hub.vesselShow == null) return;
            HubConfig h = db.hub;
            vesselOld = oldMax;
            vesselNew = newMax;
            vesselAt = Now;
            vesselGrowPlayed = vesselFullPlayed = false;
            vesselSound = play;
            object[] args = { Mathf.RoundToInt(oldMax), Mathf.RoundToInt(newMax), Mathf.RoundToInt(newMax - oldMax) };
            Text(vessel, "vessel-title", string.IsNullOrEmpty(h.vesselTitle) ? "" : string.Format(h.vesselTitle, args));
            for (int i = 0; i < VesselLineCount; i++)
            {
                Label line = vessel.Q<Label>("vessel-line-" + i);
                if (line == null) continue;
                bool has = h.vesselLines != null && i < h.vesselLines.Length && !string.IsNullOrEmpty(h.vesselLines[i]);
                line.text = has ? string.Format(h.vesselLines[i], args) : "";
                line.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
                line.style.opacity = 0;
            }
            Show(vessel, true);
            TickVessel();
        }

        public void HideVessel()
        {
            vesselAt = -1;
            if (vessel != null) Show(vessel, false);
        }

        float VesselLength(float max)
        {
            VesselShow s = db.hub.vesselShow;
            float len = s.barBase + (max - db.desire.maxBase) * s.unitPx;
            return Mathf.Clamp(len, VesselInset + 20, s.barMax > 0 ? s.barMax : 1200);
        }

        static float EaseOutCubic(float u) { u = 1 - u; return 1 - u * u * u; }

        void TickVessel()
        {
            if (vesselAt < 0 || vessel == null) return;
            if (attic == null || attic.ClassListContains("hidden")) { HideVessel(); return; }
            VesselShow s = db.hub.vesselShow;
            float t = Now - vesselAt;
            float growAt = s.appear + s.growDelay, fillAt = growAt + s.grow, fullAt = fillAt + s.fill, fadeAt = fullAt + s.hold;
            if (t > fadeAt + s.fade) { HideVessel(); return; }

            // 나타남, 사라짐: 위에서 살짝 내려앉으며
            float a = EaseOutCubic(Mathf.Clamp01(t / Mathf.Max(.01f, s.appear)));
            float out_ = Mathf.Clamp01((t - fadeAt) / Mathf.Max(.01f, s.fade));
            vessel.style.opacity = a * (1 - out_);
            vessel.style.translate = new Translate(0, (1 - a) * -16 - out_ * 10);
            vessel.style.scale = new Scale(Vector2.one * Mathf.Lerp(.94f, 1, a));

            // 틀이 자란다 (살짝 지나쳤다 돌아옴), 숫자도 함께
            float g = Mathf.Clamp01((t - growAt) / Mathf.Max(.01f, s.grow));
            float ge = g <= 0 ? 0 : EaseOutBack(g);
            float oldLen = VesselLength(vesselOld), newLen = VesselLength(vesselNew);
            float len = Mathf.LerpUnclamped(oldLen, newLen, ge);
            if (g > 0 && !vesselGrowPlayed) { vesselGrowPlayed = true; vesselSound?.Invoke(s.growSound); }
            VisualElement bar = vessel.Q("vessel-bar");
            bar.style.width = len;
            vessel.Q(className: "vessel-row").style.translate = new Translate((len - newLen) / 2, 0);   // 왼쪽 끝은 그대로, 오른쪽으로만 자람 (다 자란 모습이 가운데)
            float shown = Mathf.Lerp(vesselOld, vesselNew, Mathf.Clamp01(EaseOutCubic(g)));
            Text(vessel, "vessel-value", string.Format(db.hub.vesselValue ?? "{0}", Mathf.RoundToInt(shown)));

            // 자란 자리가 차오른다 (밝은 욕망 → 다 차면 번쩍이고 원래 욕망 색으로 가라앉음)
            float f = EaseOutCubic(Mathf.Clamp01((t - fillAt) / Mathf.Max(.01f, s.fill)));
            float oldInner = oldLen - VesselInset, curInner = len - VesselInset;
            VisualElement fill = vessel.Q("vessel-fill"), fresh = vessel.Q("vessel-new");
            fill.style.width = Mathf.Min(oldInner, curInner);
            fresh.style.left = oldInner;
            fresh.style.width = Mathf.Max(0, (curInner - oldInner) * f);
            float settle = Mathf.Clamp01((t - fullAt) / Mathf.Max(.01f, s.hold * .8f));
            Color desire = new(0.58f, 0.4f, 0.65f);   // Main.uss .meter-fill.desire
            fresh.style.unityBackgroundImageTintColor = Color.Lerp(s.newColor.a > 0 ? s.newColor : Color.white, desire, settle * settle);
            if (t >= fullAt && !vesselFullPlayed) { vesselFullPlayed = true; vesselSound?.Invoke(s.fullSound); }

            // 이전 최대치 자리 표시(바늘땀): 자라기 시작하면 나타났다가 다 차면 흐려짐
            VisualElement mark = vessel.Q("vessel-mark");
            mark.style.left = oldLen - 10;
            mark.style.opacity = Mathf.Clamp01(g * 4) * (1 - settle);

            // 자라는 끝의 빛: 자라는 동안은 틀 끝, 차오르는 동안은 차오르는 끝, 다 차면 번쩍
            VisualElement glint = vessel.Q("vessel-glint");
            float tip = t < fillAt ? len - 10 : 9 + oldInner + (curInner - oldInner) * f;
            glint.style.left = tip - 30;
            float flash = t >= fullAt ? Mathf.Clamp01(1 - (t - fullAt) / .6f) : 0;
            float glow = g > 0 && t < fullAt ? .75f + .25f * Mathf.Sin(t * 22) : 0;
            glint.style.opacity = Mathf.Max(glow, flash);
            if (s.glintColor.a > 0) glint.style.unityBackgroundImageTintColor = s.glintColor;
            vessel.Q("vessel-halo").style.opacity = flash;
            bar.style.scale = new Scale(Vector2.one * (1 + flash * .04f));

            // 문구: 한 줄씩 떠오름
            for (int i = 0; i < VesselLineCount; i++)
            {
                Label line = vessel.Q<Label>("vessel-line-" + i);
                if (line == null || line.style.display == DisplayStyle.None) continue;
                float u = EaseOutCubic(Mathf.Clamp01((t - growAt - s.lineDelay - i * s.lineGap) / .5f));
                line.style.opacity = u;
                line.style.translate = new Translate(0, (1 - u) * 10);
            }
        }
    }
}
