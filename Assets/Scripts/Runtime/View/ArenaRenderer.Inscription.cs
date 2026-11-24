// 23차: 결박 당기기 때 적 머리 위에 찍히는 라틴어 죄종 글자 (IRA, ACEDIA, INVIDIA, GULA, LUXURIA, AVARITIA, SUPERBIA).
// 크게 벌어진 글자가 가속하며 쾅 찍히고(자간이 모임), 하얗게 달궈졌다 죄종 색 금속으로 식고, 빛이 한 번 훑고, 양옆 가로줄이 뻗은 뒤 떠오르며 사라진다.
// 수치는 db.bind.title, 색은 db.bind.looks[].titleFill · titleGlow. 글자는 GameUI(IWorldText.Inscription)가 Cinzel SDF 로 그린다.
using UnityEngine;

namespace TOD
{
    public struct InscriptionPose
    {
        public float fontPx, scale, spacingPx, alpha;
        public Color face, hi, edge, glow, shade;     // 금속 면 아래(죄종 색) · 위쪽 밝은 띠 · 테두리 · 후광 · 아래 두께
        public float aura;                            // 뒤 빛무리 세기 (0~1)
        public float edgePx, glowPx, glowBlur, shadePx;
        public float sheen, sheenWidth, sheenPower;   // 빛 훑기 위치 (글자 0~1, 밖이면 없음), 폭, 세기
        public float rulePx, ruleThick;               // 양옆 가로줄 길이, 굵기 (모두 캔버스 px, 화면 px 는 받는 쪽이 바꿈)
    }

    public partial class ArenaRenderer
    {
        void DrawInscription(Fx fx, float fade)
        {
            if (Text == null || string.IsNullOrEmpty(fx.text)) return;
            InscriptionConfig c = db.bind.title;
            if (c == null || c.size <= 0) return;
            float t = 1 - fade;
            float stampT = Mathf.Max(.01f, c.stampTime);
            BindLook look = LookOf(fx);
            Color face = fx.color ?? Color.white;
            Color glow = look != null && look.titleGlow.a > 0 ? look.titleGlow : face;

            // 찍힘: 크게 → 1 (가속, 마지막에 쾅) · 자간이 벌어졌다 모인다
            float u = Mathf.Clamp01(t / stampT);
            float slam = u * u * u;
            float scale = Mathf.Lerp(c.stamp, 1, slam);
            float spread = Mathf.Lerp(Mathf.Max(1, c.spread), 1, slam);
            float after = t - stampT;   // 찍힌 뒤 지난 때 (0~1 기준)
            if (after > 0)
            {
                float b = c.bounceTime > 0 ? after / c.bounceTime : 1;
                if (b < 1) scale *= 1 - c.bounce * Mathf.Sin(b * Mathf.PI) * (1 - b);   // 눌렸다 튀어 돌아옴
                scale *= 1 + c.settle * after;                                            // 천천히 커짐
            }
            scale *= fx.size > 0 ? fx.size : 1;   // 보스는 크게

            // 달궈짐: 찍힌 순간 하얗게 → 죄종 색 금속으로 식는다 (후광도 그때 크게)
            float flash = after >= 0 && c.flashTime > 0 ? Mathf.Clamp01(1 - after / c.flashTime) : 0;
            flash *= flash;
            Color hot = Color.Lerp(face, Color.white, flash * .85f);

            // 나타남 · 사라짐
            float alpha = Mathf.Clamp01(u * 2.2f);
            if (c.fadeFrom < 1 && t > c.fadeFrom) alpha *= 1 - (t - c.fadeFrom) / (1 - c.fadeFrom);
            alpha *= fx.alpha > 0 ? fx.alpha : 1;

            // 자리: 적 머리 위, 찍힌 순간 좌우로 떨림, 천천히 떠오름
            Vector2 at = FxAt(fx);
            float lift = float.IsNaN(fx.lift) ? 0 : fx.lift;
            float shake = flash * c.shake * Mathf.Sin((t + fx.seed) * 140);
            var spot = new Vector2(at.x + shake, at.y - lift - c.rise * Mathf.Max(0, after));

            // 빛 훑기: sheenFrom~sheenTo 동안 글자 왼쪽 밖 → 오른쪽 밖
            float sheen = -9;
            if (c.sheenTo > c.sheenFrom && t >= c.sheenFrom && t <= c.sheenTo)
                sheen = Mathf.Lerp(-.35f, 1.35f, (t - c.sheenFrom) / (c.sheenTo - c.sheenFrom));

            float rule = after > 0 ? 1 - Mathf.Pow(1 - Mathf.Clamp01(after / .18f), 3) : 0;
            var pose = new InscriptionPose
            {
                fontPx = c.size, scale = scale, spacingPx = c.size * c.spacing * spread, alpha = alpha,
                face = hot, hi = Color.Lerp(hot, Color.white, c.hiLift), edge = c.edge, glow = glow,
                aura = Mathf.Clamp01(c.aura * (1 + flash * c.auraFlash)),
                shade = new Color(face.r * c.shadeDark, face.g * c.shadeDark, face.b * c.shadeDark, 1),
                edgePx = c.edgePx, glowPx = c.glowPx * (1 + flash * 1.4f), glowBlur = c.glowBlur * (1 + flash), shadePx = c.shadePx,
                sheen = sheen, sheenWidth = c.sheenWidth, sheenPower = c.sheenPower,
                rulePx = c.size * c.rule * rule, ruleThick = c.ruleThick,
            };
            Text.Inscription(fx.text, spot, pose);
        }
    }
}
