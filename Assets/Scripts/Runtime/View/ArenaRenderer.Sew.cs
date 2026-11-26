// 24차-hit-sparks: 바느질 타격 파티클 그리기. Fx 하나(FxKind.Sew) = 가운데 터짐 + 조각 여럿.
// 조각 궤적은 seed 와 효과 나이로 바로 계산한다 (프레임 스톱 동안 Fx 시간이 서면 조각도 그 자리에 선다).
// 바닥 평면으로 퍼지고(납작하게 Space.Proj), 높이는 위로 튀었다 떨어져 적 발밑 바닥에 닿으면 멈춘다.
using UnityEngine;

namespace TOD
{
    public partial class ArenaRenderer
    {
        int sewFrame = -1, sewDrawn;

        void DrawSew(Fx fx)
        {
            SewSparkConfig s = db.fx.sparks;
            if (s == null) return;
            if (sewFrame != Time.frameCount) { sewFrame = Time.frameCount; sewDrawn = 0; }
            float u = 1 - fx.Fade;            // 0 → 1
            float age = u * fx.max;           // 초
            int hits = Mathf.Max(1, fx.pop);
            bool bound = fx.bound;
            float lift = float.IsNaN(fx.lift) ? 0 : fx.lift;
            Vector2 ground = C(fx.x, fx.y);
            Vector2 center = ground - new Vector2(0, lift);
            Color sin = fx.color ?? Color.white;
            if (bound) sin = Color.Lerp(sin, Color.white, s.bindWhite);
            float glow = (bound ? s.bindGlow : s.glow) * fx.alpha;

            // 가운데 터짐
            if (s.burst != null && s.burst.Has)
            {
                float bt = s.burstTime > 0 ? s.burstTime : fx.max;
                if (age < bt)
                {
                    float size = s.BurstSize(hits, bound) * fx.size;
                    Color c = bound ? Color.Lerp(s.burst.tint, sin, s.bindTint) : s.burst.tint;
                    SewClip(s.burst, center, size, age, Mathf.Clamp01(age / bt), false, Painter.Alpha(c, fx.alpha), glow, fx.seed);
                }
            }

            if (s.pieces == null || s.pieces.Length == 0) return;
            float total = 0;
            foreach (SewPiece p in s.pieces) if (p?.clip != null && p.clip.Has) total += Mathf.Max(0, p.weight);
            if (total <= 0) return;
            int count = s.Count(hits, bound);
            float scale = s.Scale(hits, bound) * fx.size;
            float speedScale = s.Speed(hits, bound);
            var rng = new System.Random((int)fx.seed);
            float R() => (float)rng.NextDouble();
            float baseAngle = float.IsNaN(fx.angle) ? 0 : fx.angle;
            for (int i = 0; i < count; i++)
            {
                // 난수는 그리기 여부와 관계없이 같은 순서로 뽑는다 (조각마다 궤적이 프레임 사이에 바뀌지 않게)
                float pick = R() * total, dirRoll = R(), dirRand = R(), speedRand = R(), riseRand = R(), lifeRand = R(), sizeRand = R(), spinRand = R(), offRand = R(), rotRand = R();
                SewPiece p = null;
                foreach (SewPiece q in s.pieces)
                {
                    if (q?.clip == null || !q.clip.Has || q.weight <= 0) continue;
                    p = q;
                    if ((pick -= q.weight) <= 0) break;
                }
                if (p == null) continue;
                float life = Mathf.Clamp01(p.life * (.75f + .5f * lifeRand));
                if (life <= 0 || u >= life) continue;
                if (s.maxPieces > 0 && sewDrawn >= s.maxPieces) return;
                sewDrawn++;

                float ang = dirRoll < s.omni ? dirRand * Mathf.PI * 2 : baseAngle + (dirRand * 2 - 1) * s.spread;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * Space.Proj);
                float v = p.speed * (1 + p.speedJitter * (speedRand * 2 - 1)) * speedScale;
                float k = Mathf.Max(.01f, p.drag);
                float flat = s.startOffset * scale * (.6f + .4f * offRand) + v * (1 - Mathf.Exp(-k * age)) / k;
                // 높이: 처음 위로 rise, 중력으로 떨어져 바닥(-lift)에서 멈춤
                float vz = p.rise * (.6f + .8f * riseRand) * speedScale;
                float g = Mathf.Max(1, p.gravity);
                float land = (vz + Mathf.Sqrt(vz * vz + 2 * g * lift)) / g;   // 바닥에 닿는 때
                float tz = Mathf.Min(age, land);
                float z = vz * tz - .5f * g * tz * tz;
                Vector2 at = center + dir * flat - new Vector2(0, z);

                float a = fx.alpha;
                float tail = Mathf.Max(.01f, s.fadeTail);
                float lu = u / life;
                if (lu > 1 - tail) a *= (1 - lu) / tail;
                float size = p.clip.size * scale * (1 + p.sizeJitter * (sizeRand * 2 - 1));
                Color c = bound ? Color.Lerp(p.clip.tint, sin, Mathf.Clamp01(s.bindTint * p.tintMix)) : p.clip.tint;
                float spinAge = (p.loop ? tz : age) + (p.loop ? spinRand * 4 : 0);   // 바닥에 닿으면 돌기를 멈춤
                SewClip(p.clip, at, size, spinAge, Mathf.Clamp01(age / Mathf.Max(.01f, life * fx.max)), p.loop, Painter.Alpha(c, a), glow, rotRand * Mathf.PI * 2);
            }
        }

        // 플립북 한 칸 (빛 가산 → 본체). color 를 곱한다. ClipIndex 는 ArenaRenderer.Elements.cs.
        void SewClip(FlipClip clip, Vector2 at, float size, float elapsed, float progress, bool loop, Color color, float glow, float angle)
        {
            int i = ClipIndex(clip, elapsed, progress, loop);
            Sprite body = clip.frames[i];
            if (body == null || size <= 0 || color.a <= 0) return;
            if (glow > 0 && clip.glow != null && clip.glow.Length > 0)
            {
                Sprite gs = clip.glow[Mathf.Min(i, clip.glow.Length - 1)];
                if (gs != null) painter.Sprite(Layer.Effects, gs, at, angle, size, size, Painter.Alpha(color, glow), additive: true);
            }
            painter.Sprite(Layer.Effects, body, at, angle, size, size, color);
        }
    }
}
