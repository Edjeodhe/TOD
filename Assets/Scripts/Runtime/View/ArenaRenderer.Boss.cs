// 23차-boss7: 칠죄종 보스 패턴의 바닥 예고(원, 고리, 부채꼴, 직선)와 2페이즈 포효 충격파 (ArenaRenderer 의 나머지 부분).
// 예고는 Ground 층: 옅은 전체 면 + 시간이 갈수록 차오르는 면 + 테두리. 다 차는 순간 터지고 flashTime 동안 번쩍인다.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public partial class ArenaRenderer
    {
        BossConfig BossCfg => db.bosses;
        readonly List<Vector2> arc = new();
        readonly List<Keyframe> arcWidth = new();

        void DrawBossTelegraphs(RunState r, float clock)
        {
            BossConfig c = BossCfg;
            if (c == null) return;
            foreach (BossTelegraph t in r.telegraphs)
            {
                if (t.delay > 0) continue;
                Color tone = t.color.a > 0 ? new Color(t.color.r, t.color.g, t.color.b, 1) : Pal.windupRing;
                if (t.resolved)
                {
                    float f = c.flashTime > 0 ? Mathf.Clamp01(t.flash / c.flashTime) : 0;
                    if (t.damage > 0 && f > 0) Shape(t, 1, Painter.Alpha(Color.Lerp(tone, Color.white, .45f), .75f * f), true);
                    continue;
                }
                float k = t.Progress;
                float pulse = .8f + .2f * Mathf.Sin(clock * (10 + 16 * k));
                bool hint = t.damage <= 0 && t.shape == TeleShape.Line && t.width < 20;   // 탄 길 (가는 선)
                Shape(t, 1, Painter.Alpha(tone, c.baseAlpha * (hint ? .6f : 1)), false);
                if (!hint) Shape(t, k, Painter.Alpha(tone, c.fillAlpha * (.55f + .45f * k)), false);
                Edge(t, Painter.Alpha(Color.Lerp(tone, Color.white, .25f), c.edgeAlpha * pulse * (hint ? .7f : 1)));
            }
        }

        // 도형 면: k = 차오른 정도 (원은 반지름, 고리는 바깥에서 안으로, 부채꼴은 반지름, 직선은 길이)
        void Shape(BossTelegraph t, float k, Color color, bool additive)
        {
            Vector2 c = C(t.x, t.y);
            float P = Space.Proj;
            switch (t.shape)
            {
                case TeleShape.Circle:
                    if (t.radius * k > 1) painter.Ellipse(Layer.Ground, c, t.radius * k, t.radius * k * P, color, 0, additive);
                    break;
                case TeleShape.Ring:
                {
                    float inner = Mathf.Lerp(t.radius, t.inner, k);
                    float band = t.radius - inner;
                    if (band > 1) painter.Ellipse(Layer.Ground, c, (t.radius + inner) / 2, (t.radius + inner) / 2 * P, color, band, additive);
                    break;
                }
                case TeleShape.Cone:
                    ConeFill(t, t.radius * k, color, additive);
                    break;
                case TeleShape.Line:
                {
                    float ca = Mathf.Cos(t.angle), sa = Mathf.Sin(t.angle);
                    float m = Mathf.Sqrt(sa * sa + P * P * ca * ca);
                    Vector2 b = C(t.x + ca * t.radius * k, t.y + sa * t.radius * k);
                    if (t.radius * k > 1) painter.Line(Layer.Ground, c, b, t.width * m, color, additive);
                    break;
                }
            }
        }

        void Edge(BossTelegraph t, Color color)
        {
            float w = BossCfg.edgeWidth;
            Vector2 c = C(t.x, t.y);
            float P = Space.Proj;
            switch (t.shape)
            {
                case TeleShape.Circle:
                    painter.Ellipse(Layer.Ground, c, t.radius, t.radius * P, color, w);
                    break;
                case TeleShape.Ring:
                    painter.Ellipse(Layer.Ground, c, t.radius, t.radius * P, color, w);
                    painter.Ellipse(Layer.Ground, c, t.inner, t.inner * P, color, w * .7f);
                    break;
                case TeleShape.Cone:
                {
                    float half = t.width / 2;
                    arc.Clear();
                    arc.Add(c);
                    int n = Mathf.Max(6, Mathf.CeilToInt(t.width * t.radius / 14));
                    for (int i = 0; i <= n; i++)
                    {
                        float a = t.angle - half + t.width * i / n;
                        arc.Add(C(t.x + Mathf.Cos(a) * t.radius, t.y + Mathf.Sin(a) * t.radius));
                    }
                    arc.Add(c);
                    painter.Line(Layer.Ground, arc, w, color);
                    break;
                }
                case TeleShape.Line:
                {
                    float ca = Mathf.Cos(t.angle), sa = Mathf.Sin(t.angle);
                    Vector2 b = C(t.x + ca * t.radius, t.y + sa * t.radius);
                    Vector2 side = new(-sa * t.width / 2, ca * P * t.width / 2);
                    arc.Clear();
                    arc.Add(c + side); arc.Add(b + side); arc.Add(b - side); arc.Add(c - side);
                    painter.Line(Layer.Ground, arc, w, color);
                    break;
                }
            }
        }

        // 부채꼴 면: 가운데에서 바깥으로 띠(호) 여러 줄. 띠 두께는 투영된 방향에 맞춰 점마다 바꾼다.
        void ConeFill(BossTelegraph t, float radius, Color color, bool additive)
        {
            float step = Mathf.Max(8, BossCfg.bandWidth);
            if (radius < 2) return;
            int bands = Mathf.Max(1, Mathf.CeilToInt(radius / step));
            float thick = radius / bands;
            float P = Space.Proj;
            float half = t.width / 2;
            for (int j = 0; j < bands; j++)
            {
                float rb = (j + .5f) * thick;
                int n = Mathf.Max(3, Mathf.CeilToInt(t.width * rb / 16));
                arc.Clear();
                arcWidth.Clear();
                for (int i = 0; i <= n; i++)
                {
                    float a = t.angle - half + t.width * i / n;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    arc.Add(C(t.x + ca * rb, t.y + sa * rb));
                    arcWidth.Add(new Keyframe((float)i / n, Mathf.Sqrt(ca * ca + P * P * sa * sa)));
                }
                LineRenderer lr = painter.Line(Layer.Ground, arc, thick * 1.08f, color, additive);
                lr.widthCurve = new AnimationCurve(arcWidth.ToArray());
            }
        }

        // 2페이즈 포효: 보스 둘레로 충격파 고리 여러 겹이 퍼진다
        void DrawBossRoar(RunState r)
        {
            BossConfig c = BossCfg;
            if (c == null) return;
            foreach (Enemy e in r.enemies)
            {
                BossBrain b = e.brain;
                if (b == null || !b.Shielded || !b.roared) continue;
                float age = b.transition - c.staggerTime;
                float life = Mathf.Max(.05f, c.roarWaveTime);
                for (int i = 0; i < Mathf.Max(1, c.roarWaves); i++)
                {
                    float a = (age - i * .14f) / life;
                    if (a <= 0 || a >= 1) continue;
                    float grow = 1 - Mathf.Pow(1 - a, 3);
                    float rad = c.roarRadius * 1.2f * grow;
                    Color col = Painter.Alpha(c.roarColor, 1 - a);
                    painter.Ellipse(Layer.Ground, C(e), rad, rad * Space.Proj, col, 4 + 14 * (1 - a), true);
                    if (i == 0) painter.Ellipse(Layer.Ground, C(e), rad, rad * Space.Proj, Painter.Alpha(c.roarColor, .18f * (1 - a)), 0, true);
                }
            }
        }
    }
}
