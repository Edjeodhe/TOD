// 22차-ui-fixes: 조각 고르기 카드가 나타날 때 레전더리(와 신화) 카드가 번쩍인다 (GameUI 의 나머지 부분).
// 흐름: 창이 열리고 delay 동안 숨어 있다가 → 작게 나타나며 카드 위 섬광 + 뒤로 퍼지는 금빛 빛과 바느질 고리
//       → 1 + punch 까지 커졌다 돌아옴 → 테두리가 금빛에서 등급 색으로 가라앉는다. 끝나면 평소 반짝임(shines)이 이어 받는다.
// 구조는 Slots.uxml puzzle 의 entrance-halo, entrance-ring, entrance-flash, 모양은 Main.uss, 값은 gear.cardEntrance(CardEntrance).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        class Entrance
        {
            public VisualElement card, flash, halo, ring;
            public float start, strength;
            public Color rank;
            public bool sounded;
        }

        readonly List<Entrance> entrances = new();
        float entranceTick;   // 지난 TickEntrances 때 (창을 그린 직후 긴 프레임이 오면 그만큼 시작을 미룬다)

        // 창을 새로 열 때 부른다. container 의 카드 중 rank-N 클래스로 등급을 알아 번쩍일 카드만 고른다.
        void StartEntrances(VisualElement container)
        {
            foreach (Entrance e in entrances) EndEntrance(e);
            entrances.Clear();
            CardEntrance c = db.gear.cardEntrance;
            if (c == null || c.rankStrength == null || container == null) return;
            int order = 0;
            entranceTick = Now;
            foreach (VisualElement card in container.Children())
            {
                int cost = -1;
                for (int i = 0; i < c.rankStrength.Length; i++) if (card.ClassListContains($"rank-{i}")) { cost = i; break; }
                if (cost < 0 || c.rankStrength[cost] <= 0) continue;
                Color[] ranks = db.gear.rankColors;
                var e = new Entrance
                {
                    card = card, flash = card.Q("entrance-flash"), halo = card.Q("entrance-halo"), ring = card.Q("entrance-ring"),
                    start = Now + c.delay + order * c.stagger, strength = c.rankStrength[cost],
                    rank = ranks != null && cost < ranks.Length ? ranks[cost] : c.edgeColor,
                };
                order++;
                if (e.flash != null) e.flash.style.backgroundColor = c.flashColor;
                if (e.halo != null) e.halo.style.unityBackgroundImageTintColor = c.haloColor;
                if (e.ring != null) e.ring.style.unityBackgroundImageTintColor = c.haloColor;
                entrances.Add(e);
                ApplyEntrance(e, c, -1);   // 첫 프레임부터 숨긴다
            }
        }

        void TickEntrances()
        {
            if (entrances.Count == 0) return;
            CardEntrance c = db.gear.cardEntrance;
            // 카드를 많이 그린 직후 한 프레임이 길게 걸리면(글꼴, 배치) 그 사이에 번쩍임이 지나가 버리므로, 긴 프레임만큼 시작을 미룬다
            float gap = Now - entranceTick - .05f;
            entranceTick = Now;
            if (gap > 0) foreach (Entrance e in entrances) e.start += gap;
            for (int i = entrances.Count - 1; i >= 0; i--)
            {
                Entrance e = entrances[i];
                float t = c != null ? (Now - e.start) / Mathf.Max(.05f, c.time) : 2;
                if (e.card.panel == null || t >= 1) { EndEntrance(e); entrances.RemoveAt(i); continue; }
                if (t >= 0 && !e.sounded)
                {
                    e.sounded = true;
                    if (!string.IsNullOrEmpty(c.sound)) game.sound.Play(c.sound, Mathf.Min(1.3f, .8f + .2f * e.strength));
                }
                ApplyEntrance(e, c, t);
            }
        }

        // t < 0: 아직 숨음, 0~1: 번쩍이는 중
        void ApplyEntrance(Entrance e, CardEntrance c, float t)
        {
            if (t < 0)
            {
                e.card.style.opacity = 0;
                e.card.style.scale = new Scale(Vector2.one * c.startScale);
                ShowIf(e.flash, false); ShowIf(e.halo, false); ShowIf(e.ring, false);
                return;
            }
            float s = e.strength;
            e.card.style.opacity = 1;
            // 크기: startScale → 1 + punch (punchAt 에서) → 1
            float big = 1 + c.punch * s, at = Mathf.Clamp(c.punchAt, .05f, .9f);
            float scale = t < at ? Mathf.Lerp(c.startScale, big, EaseOutQuad(t / at)) : Mathf.Lerp(big, 1, EaseInOut(Mathf.Clamp01((t - at) / (1 - at))));
            e.card.style.scale = new Scale(new Vector2(scale, scale));
            // 섬광: 처음에 가장 밝고 빨리 걷힌다
            float fade = 1 - t;
            if (e.flash != null) { ShowIf(e.flash, true); e.flash.style.opacity = Mathf.Clamp01(c.flashPeak * s) * fade * fade * fade; }
            // 뒤 빛: 커지며 옅어진다. 바느질 고리는 조금 더 빨리 크게 퍼진다
            if (e.halo != null)
            {
                ShowIf(e.halo, true);
                float h = Mathf.Lerp(.55f, c.haloScale, EaseOutQuad(t));
                e.halo.style.scale = new Scale(new Vector2(h, h));
                e.halo.style.opacity = Mathf.Clamp01(c.haloPeak * s) * fade * fade;
            }
            if (e.ring != null)
            {
                ShowIf(e.ring, true);
                float r = Mathf.Lerp(.35f, c.haloScale * 1.15f, EaseOutQuad(Mathf.Min(1, t * 1.4f)));
                e.ring.style.scale = new Scale(new Vector2(r, r));
                e.ring.style.opacity = Mathf.Clamp01(s) * Mathf.Max(0, 1 - t * 1.4f);
            }
            // 테두리, 돌기, 종이: 금빛으로 번쩍였다가 등급 색으로 가라앉는다
            float gold = Mathf.Pow(Mathf.Max(0, 1 - t), Mathf.Lerp(3, .6f, Mathf.Clamp01(c.edgeHold)));
            Color edge = Color.Lerp(e.rank, c.edgeColor, gold);
            e.card.style.unityBackgroundImageTintColor = PaperTint(Color.Lerp(e.rank, Color.white, gold * .6f));
            SetBorder(e.card.Q("frame"), edge);
            foreach (string knob in new[] { "knob-top", "knob-right" }) { VisualElement k = e.card.Q(knob); if (k != null) k.style.backgroundColor = edge; }
        }

        void EndEntrance(Entrance e)
        {
            e.card.style.opacity = StyleKeyword.Null;
            e.card.style.scale = StyleKeyword.Null;   // USS :hover 커짐이 다시 먹게
            e.card.style.unityBackgroundImageTintColor = PaperTint(e.rank);
            SetBorder(e.card.Q("frame"), e.rank);
            foreach (string knob in new[] { "knob-top", "knob-right" }) { VisualElement k = e.card.Q(knob); if (k != null) k.style.backgroundColor = e.rank; }
            ShowIf(e.flash, false); ShowIf(e.halo, false); ShowIf(e.ring, false);
        }

        static void ShowIf(VisualElement e, bool visible) { if (e != null) Show(e, visible); }
    }
}
