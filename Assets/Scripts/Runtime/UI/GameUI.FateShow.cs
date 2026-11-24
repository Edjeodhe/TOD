// 20차 모이라의 운명 읽기 연출 (Main.uxml attic 의 fate, 모양은 Moira.uss, 값은 db.hub.fateShow):
//   1. 파편 수 자리에서 파편 불티가 하나씩 호를 그리며 탁자 위 점성구로 빨려 들어간다(점성구 둘레가 점점 밝아짐).
//   2. 점성구가 탁자에서 떠올라 화면 가운데로 나오며 커진다(그동안 방은 어두워짐). 탁자 위 진짜 점성구는 감춘다(MoiraRig.BallOut).
//   3. 점성구에서 빛이 퍼진다(넓은 빛 + 밝은 중심 + 퍼지는 고리).
//   4. 빛 위에 모이라의 암시가 어두운 글자로 한 자씩 번져 나온다.
//   누르거나 Esc: 글이 다 안 나왔으면 다 보여 주고, 다 나왔으면 빛이 걷히고 점성구가 탁자로 돌아간 뒤 끝(done).
using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        VisualElement fate, fateDim, fateLight, fateCore, fateRing, fateShade, fateOrb, fateGlass;
        Label fateText, fateSkip;
        VisualElement[] fateSparks;
        float fateAt = -1, fateCloseAt = -1, fateTextAt, fateSkipTo = -1;
        string fateLine = "", fateShown;
        Color fateTint, fateInk;
        Func<Rect> fateBall;                  // 탁자 위 점성구 자리 (패널 px)
        Action<bool> fateBallOut;
        Action fateDone, fateBurst;
        bool fateBursted;

        public bool FateShowing => fateAt > 0;

        void InitFate()
        {
            fate = attic.Q("fate");
            if (fate == null) return;
            fateDim = fate.Q("fate-dim"); fateLight = fate.Q("fate-light"); fateCore = fate.Q("fate-core");
            fateRing = fate.Q("fate-ring"); fateOrb = fate.Q("fate-orb"); fateShade = fate.Q("fate-shade"); fateGlass = fate.Q("fate-orb-glass");
            fateText = fate.Q<Label>("fate-text"); fateSkip = fate.Q<Label>("fate-skip");
            fateSparks = fate.Query(className: "fate-spark").ToList().ToArray();
            fate.RegisterCallback<PointerDownEvent>(e => { SkipFate(); e.StopPropagation(); });
        }

        FateShow FS => db.hub?.fateShow;

        // 속성 색의 어두운 판 (글자): 같은 색조, 채도 조금, 아주 어둡게
        static Color InkOf(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out _);
            return Color.HSVToRGB(h, Mathf.Clamp(s, .35f, .75f), .24f);
        }

        // line = 암시, tint = 속성 색(fateColor), orb = 점성구 그림, ball = 탁자 위 점성구 자리, ballOut = 진짜 점성구 감추기, burst = 빛이 퍼질 때, done = 다 닫힘
        // paid = 파편을 냈을 때만 불티가 날아간다 (다시 듣기는 점성구부터)
        public void ShowFate(string line, Color tint, Sprite orb, Func<Rect> ball, Action<bool> ballOut, Action burst, Action done, bool paid)
        {
            if (fate == null) InitFate();
            if (fate == null || FS == null) { done?.Invoke(); return; }
            fateLine = line ?? "";
            fateShown = null;
            fateTint = tint;
            fateBall = ball; fateBallOut = ballOut; fateBurst = burst; fateDone = done;
            fateBursted = false;
            fateAt = Now; fateCloseAt = -1; fateSkipTo = -1;
            if (!paid) fateAt = Now - Mathf.Max(FS.orbDelay, FS.sparkTime + FS.sparkStagger * fateSparks.Length);   // 불티는 이미 다 닿은 것으로
            if (FS.orbSprite != null) orb = FS.orbSprite;
            fateOrb.style.backgroundImage = orb != null ? new StyleBackground(orb) : new StyleBackground(StyleKeyword.Null);
            fateShade.style.unityBackgroundImageTintColor = FS.shadeColor;
            // 20차: 유리만 흑백 그림을 속성 색으로 물들여 덮는다 (받침은 원래 그림 그대로)
            fateGlass.style.backgroundImage = FS.orbGlass != null ? new StyleBackground(FS.orbGlass) : new StyleBackground(StyleKeyword.Null);
            fateGlass.style.unityBackgroundImageTintColor = tint;
            Color light = Color.Lerp(FS.lightColor, tint, FS.lightTint);
            fateLight.style.unityBackgroundImageTintColor = light;
            fateCore.style.unityBackgroundImageTintColor = Color.Lerp(light, Color.white, .3f) * new Color(1, 1, 1, .8f);
            fateInk = Color.Lerp(FS.textColor, InkOf(tint), FS.textTint);
            fateText.style.color = fateInk;
            fateSkip.style.color = fateInk;
            fateText.text = "";
            fateSkip.text = FS.skipHint ?? "";
            Show(fate, true);
            attic.AddToClassList("fate-on");
            fateBallOut?.Invoke(true);
            TickFate();
        }

        // 누름, Esc
        public void SkipFate()
        {
            if (!FateShowing || fateCloseAt > 0) return;
            if (fateSkipTo < 0 && Now < FateTextEnd) { fateSkipTo = Now; return; }   // 글을 다 보여 준다 (빛도 다 퍼진 상태로)
            fateCloseAt = Now;
        }

        float LightStart => fateAt + FS.orbDelay + FS.orbFly + FS.lightDelay;
        float TextStart => LightStart + FS.textDelay;
        float FateTextEnd => TextStart + fateLine.Length * FS.charTime + FS.charFade;

        static float EaseInOut(float u) => u * u * (3 - 2 * u);

        static void PlaceAt(VisualElement e, Vector2 center, float size)
        {
            e.style.left = center.x - size / 2;
            e.style.top = center.y - size / 2;
            e.style.width = size;
            e.style.height = size;
        }

        void TickFate()
        {
            if (!FateShowing) return;
            FateShow s = FS;
            float now = Now;
            if (fateSkipTo > 0) now = Mathf.Max(now, FateTextEnd);   // 건너뛰기: 연출 시계를 글이 다 나온 때로
            float w = attic.layout.width, h = attic.layout.height;
            if (float.IsNaN(w) || w < 10) { w = 1920; h = 1080; }
            Rect ball = fateBall?.Invoke() ?? new Rect(w / 2 - 40, h * .3f - 40, 80, 80);
            Vector2 center = new(w / 2, h * s.orbY);
            float close = fateCloseAt > 0 ? Mathf.Clamp01((Now - fateCloseAt) / Mathf.Max(.05f, s.closeTime)) : 0;
            float keep = 1 - close;

            // 1. 불티: 파편 수 → 탁자 위 점성구
            VisualElement counter = attic.Q("attic-shards");
            Vector2 from = counter != null ? counter.worldBound.center - attic.worldBound.position : new Vector2(w - 100, 120);
            int arrived = 0;
            for (int i = 0; i < fateSparks.Length; i++)
            {
                float u = (now - fateAt - i * s.sparkStagger) / Mathf.Max(.05f, s.sparkTime);
                VisualElement sp = fateSparks[i];
                if (u >= 1) arrived++;
                if (u <= 0 || u >= 1 || close > 0) { sp.style.opacity = 0; continue; }
                float e = EaseInOut(u);
                Vector2 p = Vector2.Lerp(from, ball.center, e) + new Vector2(Mathf.Sin(i * 2.1f) * 60, -160) * Mathf.Sin(Mathf.PI * u);
                float size = Mathf.Lerp(52, 24, e);
                PlaceAt(sp, p, size);
                sp.style.rotate = new Rotate(u * 540 + i * 40);
                sp.style.opacity = Mathf.Min(1, u * 6) * (1 - Mathf.Pow(u, 6));
            }
            float charge = fateSparks.Length > 0 ? (float)arrived / fateSparks.Length : 1;

            // 2. 점성구: 탁자 → 가운데 (닫힐 때는 거꾸로)
            float fly = Mathf.Clamp01((now - fateAt - s.orbDelay) / Mathf.Max(.05f, s.orbFly));
            float m = EaseInOut(fly) * (1 - EaseInOut(close));
            float bob = Mathf.Sin(Now * 1.4f) * s.bob * m;
            Vector2 orbAt = Vector2.Lerp(ball.center, center, m) + new Vector2(0, bob);
            float orbSize = Mathf.Lerp(Mathf.Max(ball.width, ball.height), s.orbSize, m);
            PlaceAt(fateOrb, orbAt, orbSize);
            fateOrb.style.opacity = 1;
            // 구 뒤 그늘: 가장자리를 어둠에 묻는다 (빛보다 앞, 구보다 뒤)
            PlaceAt(fateShade, orbAt + new Vector2(0, orbSize * .06f), orbSize * Mathf.Max(1, s.shadeSize));
            fateShade.style.opacity = m * keep;
            fateDim.style.opacity = s.dim * Mathf.Clamp01((now - fateAt) / Mathf.Max(.05f, s.orbDelay + s.orbFly)) * keep;

            // 3. 빛
            float lu = Mathf.Clamp01((now - LightStart) / Mathf.Max(.05f, s.lightTime));
            if (lu > 0 && !fateBursted) { fateBursted = true; fateBurst?.Invoke(); }
            float le = EaseOutQuad(lu);
            PlaceAt(fateLight, orbAt, Mathf.Lerp(orbSize, s.lightSize, le) * (1 + .03f * Mathf.Sin(Now * 1.7f)));
            fateLight.style.opacity = le * .95f * keep;
            // 밝은 중심: 불티가 닿을수록 점성구 둘레가 밝아지고, 빛이 퍼지면 크게
            float coreSize = orbSize * (1.5f + .8f * charge) * (1 + .6f * le);
            PlaceAt(fateCore, orbAt, coreSize);
            fateCore.style.opacity = Mathf.Max(charge * .55f, le) * keep;
            float ringSize = Mathf.Lerp(orbSize, s.lightSize * 1.25f, EaseOutQuad(lu));
            PlaceAt(fateRing, orbAt, ringSize);
            fateRing.style.borderTopLeftRadius = fateRing.style.borderTopRightRadius =
                fateRing.style.borderBottomLeftRadius = fateRing.style.borderBottomRightRadius = ringSize / 2;
            fateRing.style.opacity = lu > 0 && lu < 1 ? (1 - lu) * .9f * keep : 0;

            // 4. 어두운 글자가 한 자씩 번진다
            fateText.style.top = h * s.textY - 60;
            fateText.style.height = 120;
            // 아직 덜 짙어진 글자는 <color=#RRGGBBAA> 로 투명도를 준다 (UI Toolkit 은 <alpha> 를 안 받음)
            var sb = new StringBuilder();
            string rgb = ColorUtility.ToHtmlStringRGB(fateInk);
            for (int i = 0; i < fateLine.Length; i++)
            {
                float a = Mathf.Clamp01((now - TextStart - i * s.charTime) / Mathf.Max(.01f, s.charFade));
                if (a >= 1) { sb.Append(fateLine[i]); continue; }
                sb.Append("<color=#").Append(rgb).Append(Mathf.RoundToInt(a * a * 255).ToString("X2")).Append('>').Append(fateLine[i]).Append("</color>");
            }
            string shown = sb.ToString();
            if (shown != fateShown) { fateText.text = shown; fateShown = shown; }
            fateText.style.opacity = keep * keep;
            float doneText = now - FateTextEnd;
            fateSkip.style.opacity = doneText > 0 ? Mathf.Clamp01(doneText * 2) * keep : 0;
            if (s.hold > 0 && doneText > s.hold && fateCloseAt < 0) fateCloseAt = Now;

            if (close >= 1)
            {
                fateAt = -1;
                Show(fate, false);
                attic.RemoveFromClassList("fate-on");
                fateBallOut?.Invoke(false);
                Action done = fateDone;
                fateDone = null;
                done?.Invoke();
            }
        }
    }
}
