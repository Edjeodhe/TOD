// 22차 모이라의 미래 보기 연출 (Main.uxml attic 의 fate, 모양은 Moira.uss, 값은 db.hub.fateShow, 방 쪽은 FateOrbView):
//   1. 탁자 점성구가 살짝 떠오르며 죄종 색으로 빛난다(riseTime).
//   2. 파편을 냈으면 파편 수 자리에서 불티가 하나씩 점성구로 빨려 들어간다(다시 보기는 불티 없음).
//   3. "자세히 살펴보기" 안내(E)가 점성구 위에 뜬다. E 나 누름으로 다음.
//   4. 모이라가 앉은 채 두 앞다리로 구슬을 감싸 쥐고(touchLead), 카메라가 정면으로 점점 빨라지며(zoomPower 이즈인) 구슬로 빨려 든다(zoomTime).
//      회전은 없고, 구슬 바깥이 속도만큼 구슬 쪽으로 당겨지며 방사형으로 번진다(swirlStrength, rushBlur, 멈춘 뒤 rushFade 에 걷힘). 모이라는 freezeTime 동안 서서히 멈춘다.
//   5. 구슬 유리 안에 그 죄종의 영상이 번져 나와 videoTime 동안 돈다. 주변은 미동도 없이 죄종 색으로 살짝 덮인다(tintAmount). 자막(ElementDef.fateWhispers)이 차례로 죄종 색 글자로 나온다.
//   6. 영상이 끝나면 endHold 초 뒤(또는 E, 누름) 줌이 회전 없이 부드럽게 풀리며 원래 화면으로 돌아오고, 모이라가 한 줄을 남긴다(done).
//   Esc 는 언제든 건너뛰기(바로 돌아옴).
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace TOD
{
    public partial class GameUI
    {
        VisualElement fate, fateDim, fatePrompt;
        Label fateText, fateSkip, fatePromptText;
        VisualElement[] fateSparks;
        FateOrbView fateWorld;
        string[] fateLines = new string[0];
        Color fateTint;
        float fateAt = -1, fateArrive, fatePromptAt, fateTouchAt = -1, fateCloseAt = -1, fateVideoAt = -1;
        bool fatePaid, fateVideoStarted;
        Action fateDone;
        Action<string> fateSound;
        // 닫기 시작할 때의 값 (여기서부터 0 으로 풀린다)
        float closeZoom, closeMix, closeSwirl, closeBlur, closeStill, closeTint, closeLift, closeGlow;
        string fateShownText;

        public bool FateShowing => fateAt > 0;

        void InitFate()
        {
            fate = attic.Q("fate");
            if (fate == null) return;
            fateDim = fate.Q("fate-dim");
            fatePrompt = fate.Q("fate-prompt");
            fatePromptText = fate.Q<Label>("fate-prompt-text");
            fateText = fate.Q<Label>("fate-text"); fateSkip = fate.Q<Label>("fate-skip");
            fateSparks = fate.Query(className: "fate-spark").ToList().ToArray();
            fate.RegisterCallback<PointerDownEvent>(e => { AdvanceFate(); e.StopPropagation(); });
        }

        FateShow FS => db.hub?.fateShow;

        // lines = 자막 (차례로), tint = 죄종 색, world = 방 쪽 연출, clip = 구슬 속 영상, sound = 효과음 재생, done = 다 끝나 원래 화면
        // paid = 파편을 냈을 때만 불티가 날아간다 (다시 보기는 점성구부터)
        public void ShowFate(string[] lines, Color tint, FateOrbView world, VideoClip clip, Action<string> sound, Action done, bool paid)
        {
            if (fate == null) InitFate();
            FateShow s = FS;
            if (fate == null || s == null || world == null) { done?.Invoke(); return; }
            fateLines = lines ?? new string[0];
            fateTint = tint;
            fateWorld = world;
            fateDone = done;
            fateSound = sound;
            fatePaid = paid;
            fateAt = Now;
            fateTouchAt = fateCloseAt = fateVideoAt = -1;
            fateVideoStarted = false;
            float sparkStart = fateAt + s.sparkDelay;
            fateArrive = paid ? sparkStart + s.sparkTime + s.sparkStagger * Mathf.Max(0, fateSparks.Length - 1) : fateAt + s.riseTime;
            fatePromptAt = Mathf.Max(fateArrive, fateAt + s.riseTime) + s.promptDelay;
            world.Begin(tint, clip, s.zoomFill, s.zoomOffset);
            fateText.text = "";
            fateShownText = null;
            fateText.style.color = Color.Lerp(tint, Color.white, .42f);   // 죄종 색을 밝힌 글자 (어두운 띠 위에서 읽히게)
            fateSkip.text = s.skipHint ?? "";
            if (fatePromptText != null) fatePromptText.text = s.promptText ?? "";
            Show(fate, true);
            attic.AddToClassList("fate-on");
            attic.RemoveFromClassList("fate-zoom");
            if (!string.IsNullOrEmpty(s.riseSound)) sound?.Invoke(s.riseSound);
            TickFate();
        }

        // E, 누름: 안내가 떴으면 구슬 속으로, 영상이 끝났으면 돌아가기
        public void AdvanceFate()
        {
            if (!FateShowing || fateCloseAt > 0) return;
            if (fateTouchAt < 0)
            {
                if (Now < fatePromptAt) return;
                fateTouchAt = Now;
                attic.AddToClassList("fate-zoom");
                FateShow s = FS;
                if (!string.IsNullOrEmpty(s.zoomSound)) fateSound?.Invoke(s.zoomSound);
                return;
            }
            if (fateVideoAt > 0 && Now >= fateVideoAt + FateLength(FS)) BeginFateClose();
        }

        // Esc: 언제든 건너뛰기
        public void SkipFate()
        {
            if (!FateShowing || fateCloseAt > 0) return;
            BeginFateClose();
        }

        void BeginFateClose()
        {
            fateCloseAt = Now;
            closeZoom = fateWorld.Zoom; closeMix = fateWorld.VideoMix; closeSwirl = fateWorld.Swirl; closeBlur = fateWorld.Blur; closeStill = fateWorld.Still;
            closeTint = fateWorld.TintAmount; closeLift = fateWorld.Lift; closeGlow = fateWorld.Glow;
        }

        // 자막 한 줄 자리: 영상 시간을 줄 수로 나눈 것과 lineTime(가장 짧은 자리) 중 긴 쪽. 1~2줄이면 영상 안에 고르게 퍼지고, 줄이 많으면 마지막 장면에 멈춘 채 이어진다
        float FateSlot(FateShow s) => Mathf.Max(Mathf.Max(.5f, s.lineTime), (s.videoTime - FateSubStart(s)) / Mathf.Max(1, fateLines.Length));
        // 첫 자막이 나오는 때 (영상 시작부터): 한 줄이면 영상 처음부터 끝까지 (번져 나오는 동안 같이 나타남), 여러 줄이면 영상이 반쯤 번진 뒤
        float FateSubStart(FateShow s) => fateLines.Length <= 1 ? 0 : s.videoFade * .5f;
        // 영상과 자막이 다 끝나는 때 (영상 시작부터)
        float FateLength(FateShow s) => Mathf.Max(s.videoTime, FateSubStart(s) + FateSlot(s) * fateLines.Length);

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
            FateOrbView w = fateWorld;
            float now = Now;
            float h = attic.layout.height;
            if (float.IsNaN(h) || h < 10) h = 1080;
            Vector2 ball = WorldToPanel(w.OrbCenter);
            Vector2 ballTop = WorldToPanel(w.OrbCenter + new Vector3(0, w.OrbRadius, 0));

            // 1. 떠오름과 빛
            float rise = EaseInOut(Mathf.Clamp01((now - fateAt) / Mathf.Max(.05f, s.riseTime)));
            float settle = fateTouchAt > 0 ? EaseInOut(Mathf.Clamp01((now - fateTouchAt) / Mathf.Max(.05f, s.touchLead))) : 0;   // 손이 감싸면 다시 내려앉는다
            float bob = Mathf.Sin(now * 2.2f) * 2.5f * rise * (1 - settle);
            float lift = s.liftHeight * rise * (1 - settle) + bob;

            // 2. 불티: 파편 수 → 점성구
            VisualElement counter = attic.Q("attic-shards");
            Vector2 from = counter != null ? counter.worldBound.center - attic.worldBound.position : new Vector2(attic.layout.width - 100, 120);
            int arrived = 0;
            for (int i = 0; i < fateSparks.Length; i++)
            {
                VisualElement sp = fateSparks[i];
                float u = (now - fateAt - s.sparkDelay - i * s.sparkStagger) / Mathf.Max(.05f, s.sparkTime);
                if (u >= 1) arrived++;
                if (!fatePaid || u <= 0 || u >= 1 || fateTouchAt > 0) { sp.style.opacity = 0; continue; }
                float e = EaseInOut(u);
                Vector2 p = Vector2.Lerp(from, ball, e) + new Vector2(Mathf.Sin(i * 2.1f) * 60, -160) * Mathf.Sin(Mathf.PI * u);
                PlaceAt(sp, p, Mathf.Lerp(52, 22, e));
                sp.style.rotate = new Rotate(u * 540 + i * 40);
                sp.style.opacity = Mathf.Min(1, u * 6) * (1 - Mathf.Pow(u, 6));
            }
            float charge = fatePaid && fateSparks.Length > 0 ? (float)arrived / fateSparks.Length : 1;
            float glow = rise * (.45f + .55f * charge);

            // 3. 안내
            float promptA = fateTouchAt < 0 ? Mathf.Clamp01((now - fatePromptAt) / .35f) : Mathf.Clamp01(1 - (now - fateTouchAt) / .25f);
            if (fatePrompt != null)
            {
                fatePrompt.style.left = ballTop.x;
                fatePrompt.style.top = ballTop.y - s.promptLift - (1 - promptA) * 10;
                fatePrompt.style.opacity = promptA * (.85f + .15f * Mathf.Sin(now * 4));
            }

            // 4. 정면으로 빨려 드는 줌: 점점 빨라진다(이즈인). 당김과 번짐은 줌 속도를 따르고 멈춘 뒤 rushFade 에 걷힌다. 모이라는 freezeTime 동안 서서히 멈춘다
            float zt = fateTouchAt > 0 ? now - fateTouchAt - s.touchLead : -1;
            float zoomTime = Mathf.Max(.05f, s.zoomTime);
            float ze = Mathf.Clamp01(zt / zoomTime);
            float power = Mathf.Max(1, s.zoomPower);
            float zoom = Mathf.Pow(ze, power);
            float speed = zt <= 0 ? 0 : zt < zoomTime ? Mathf.Pow(ze, power - 1) : 1 - EaseInOut(Mathf.Clamp01((zt - zoomTime) / Mathf.Max(.05f, s.rushFade)));
            float swirl = s.swirlStrength * speed;
            float blur = s.rushBlur * speed;
            float still = zt <= 0 ? 0 : s.freezeTime > 0 ? EaseInOut(Mathf.Clamp01(zt / s.freezeTime)) : 1;
            if (ze >= 1) still = 1;
            float tint = s.tintAmount * EaseInOut(ze);

            // 5. 영상과 자막
            if (ze >= 1 && fateVideoAt < 0) fateVideoAt = now;
            float mix = 0;
            string sub = "";
            float subA = 0;
            if (fateVideoAt > 0)
            {
                if (!fateVideoStarted) { fateVideoStarted = true; w.PlayVideo(s.videoTime); if (!string.IsNullOrEmpty(s.lightSound)) fateSound?.Invoke(s.lightSound); }
                float vt = now - fateVideoAt;
                mix = EaseInOut(Mathf.Clamp01(vt / Mathf.Max(.05f, s.videoFade)));
                int n = fateLines.Length;
                if (n > 0)
                {
                    float start = FateSubStart(s), slot = FateSlot(s);
                    int i = Mathf.Clamp(Mathf.FloorToInt((vt - start) / slot), 0, n - 1);
                    float t = vt - start - i * slot;
                    if (vt >= start)
                    {
                        sub = fateLines[i] ?? "";
                        float fade = Mathf.Max(.05f, s.lineFade);
                        subA = Mathf.Clamp01(t / fade) * Mathf.Clamp01((i == n - 1 ? FateLength(s) - vt : slot - t) / fade);   // 마지막 줄은 영상(자막)이 끝날 때 사라진다
                    }
                }
                if (s.endHold > 0 && vt > FateLength(s) + s.endHold && fateCloseAt < 0) BeginFateClose();
            }
            bool ended = fateVideoAt > 0 && now >= fateVideoAt + FateLength(s);

            // 6. 닫기: 닫기 시작한 값에서 0 으로
            float close = 0;
            if (fateCloseAt > 0)
            {
                close = Mathf.Clamp01((now - fateCloseAt) / Mathf.Max(.05f, s.zoomOutTime));
                float k = 1 - EaseInOut(close);
                zoom = closeZoom * k;
                mix = closeMix * Mathf.Clamp01(1 - close * 2.5f);
                swirl = closeSwirl * k;
                blur = closeBlur * k;
                still = closeStill * k;
                tint = closeTint * k;
                lift = closeLift * k;
                glow = closeGlow * k;
                subA *= Mathf.Clamp01(1 - close * 3);
                attic.RemoveFromClassList("fate-zoom");
            }

            w.Lift = lift;
            w.Glow = glow;
            w.Touch = fateTouchAt > 0 && (fateCloseAt < 0 || close < .35f);
            w.Zoom = zoom;
            w.Swirl = swirl;
            w.Blur = blur;
            w.Still = still;
            w.TintAmount = tint;
            w.VideoMix = mix;

            // 자막: 죄종 색 글자가 숨 쉬듯 번진다
            fateText.style.top = h * s.subtitleY - 60;
            if (sub != fateShownText) { fateText.text = sub; fateShownText = sub; }
            fateText.style.opacity = subA;
            fateText.style.letterSpacing = Mathf.Lerp(6, 1, subA);
            fateSkip.style.opacity = ended && fateCloseAt < 0 ? Mathf.Clamp01((now - fateVideoAt - FateLength(s)) * 2) * .9f : 0;
            fateDim.style.opacity = 0;

            if (fateCloseAt > 0 && close >= 1)
            {
                fateAt = -1;
                w.End();
                Show(fate, false);
                attic.RemoveFromClassList("fate-on");
                attic.RemoveFromClassList("fate-zoom");
                Action done = fateDone;
                fateDone = null;
                done?.Invoke();
            }
        }
    }
}
