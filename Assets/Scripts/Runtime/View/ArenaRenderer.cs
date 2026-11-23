// 전투장 그리기 (웹판 render.js 의 render / drawThread / drawEffects ...). 그리는 순서와 수식은 웹판을 그대로 따른다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public interface IWorldText
    {
        // 캔버스 좌표에 글자를 띄운다 (매 프레임 다시 호출).
        void Label(string text, Vector2 canvas, float fontPx, Color fill, Color stroke, float strokePx, float scale, float rotationDeg, bool italic);
    }

    [Serializable] public class FxSpriteEntry { public FxSprite id; public Sprite sprite; public bool tintable, directional; }
    [Serializable] public class FlipbookEntry { public FlipSheet sheet; public Sprite[] core, rim; }

    public class ArenaRenderer : MonoBehaviour
    {
        public GameDatabase db;
        public Painter painter;
        public HeroView hero;
        public EnemyView enemyTemplate;
        public Transform enemyRoot;
        public ParticleFx particles;
        public BackgroundView background;
        public CameraRig cameraRig;
        public Sprite goldIcon, shardIcon, knotIcon, needleSprite, streakSprite, barbSprite;
        public FxSpriteEntry[] fxSprites;
        public FlipbookEntry[] flipbooks;

        public IWorldText Text { get; set; }
        Pool<EnemyView> enemyPool;
        readonly Dictionary<Enemy, EnemyView> enemyViews = new();
        readonly Dictionary<Enemy, List<Strand>> pins = new();
        readonly List<Enemy> stale = new();
        float particleClock = -1;
        RenderPalette Pal => db.palette;

        public void Init()
        {
            enemyPool = new Pool<EnemyView>(enemyTemplate, enemyRoot);
            hero.Init(db);
            background.Init(db.arena.height);
        }

        static float EaseOutBack(float t) => 1 + 2.4f * Mathf.Pow(t - 1, 3) + 1.4f * Mathf.Pow(t - 1, 2);
        static float Noise(float n) { float s = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return s - Mathf.Floor(s); }
        static float Rand() => UnityEngine.Random.value;
        static Vector2 C(float x, float y) => Space.ToCanvas(x, y);
        static Vector2 C(Point p) => Space.ToCanvas(p.x, p.y);
        ElementTone Tone(Element e) => db.Tone(e);
        Color ToneColor(Fx fx) => fx.color ?? Tone(fx.element).color;

        public void Clear()
        {
            painter.Begin();
            enemyPool?.ReleaseAll();
            enemyViews.Clear();
            particles.Clear();
        }

        public void Render(RunState r, Vector2 aim, float clock, bool active, float heroActionTime)
        {
            painter.Begin();
            Color[] floors = db.visual.floorPalette;
            background.Render(floors[(r.floor - 1) % floors.Length], clock, Pal, db.arena.height);
            cameraRig.Apply(r.shake, r.kick, clock);
            if (particleClock < 0 || r.time < particleClock || r.time - particleClock > .5f) particleClock = r.time;
            float dt = Mathf.Min(.05f, r.time - particleClock);
            particleClock = r.time;
            particles.SetRunning(dt > 0);

            foreach (Drop d in r.drops) painter.Sprite(Layer.Ground, d.shard > 0 ? shardIcon : goldIcon, C(d.x, d.y) - new Vector2(0, 3), 0, 18, 18, Color.white);
            foreach (Enemy e in r.enemies)
            {
                if (e.action == EnemyAction.WindupDash)
                {
                    Vector2 end = C(e.x + e.aim.x * 340, e.y + e.aim.y * 340);
                    painter.Line(Layer.Ground, C(e), end, 20, Pal.windupLane);
                    painter.Line(Layer.Ground, C(e), end, 2, Pal.windupDash, dashed: true);
                }
                if (e.action is EnemyAction.WindupMelee or EnemyAction.WindupShot)
                {
                    float radius = e.action == EnemyAction.WindupMelee ? 66 : e.radius + 10;
                    painter.Ellipse(Layer.Ground, C(e), radius, radius * Space.Proj, Pal.windupRing, 2);
                }
            }
            foreach (Knot k in r.knots)
            {
                painter.Ellipse(Layer.Ground, C(k), 15, 15 * Space.Proj, Pal.knotRing, 1.4f);
                painter.Sprite(Layer.Ground, knotIcon, C(k) - new Vector2(0, 5), 0, 22, 22, Color.white);
            }
            foreach (Trail trail in r.trails) painter.Line(Layer.Ground, C(trail.a), C(trail.b), 1.8f, Painter.Alpha(Tone(r.element).color, .73f), dashed: true);

            Player player = r.player;
            HeroFrame frame = hero.Pose(player, heroActionTime);
            Vector2 hand = hero.Hand(frame);
            pins.Clear();
            foreach (Strand s in r.strands)
            {
                DrawThread(r, s, hand, dt);
                if (s.target == null) continue;
                if (!pins.TryGetValue(s.target, out List<Strand> list)) pins[s.target] = list = new List<Strand>();
                list.Add(s);
            }
            if (r.echo != null)
            {
                foreach (Strand s in r.echo.strands)
                {
                    if (s.target != null && s.target.hp <= 0) continue;
                    var (start, end) = Combat.Endpoints(player, s);
                    painter.Line(Layer.Ground, s.anchor != null ? C(start) : C(hand.x, hand.y), C(end), 1, Pal.echoLine, dashed: true);
                }
            }
            foreach (Bullet b in r.bullets)
            {
                painter.Ellipse(Layer.Ground, C(b), 12, 10, Painter.Alpha(Pal.bulletGlow, .35f), additive: true);
                painter.Ellipse(Layer.Ground, C(b), 6, 5, b.spent ? Pal.bulletSpent : Pal.bullet);
            }
            DrawAfterimages(r);
            SyncEnemies(r, dt);
            hero.Render(player, frame, r.outcome == Outcome.Absorbed ? .55f : 1, Pal.shadow);
            DrawEffects(r);
            if (active && aim.x >= 0 && aim.x <= db.arena.width && aim.y >= 0 && aim.y <= db.arena.height)
            {
                painter.Ellipse(Layer.Effects, C(aim.x, aim.y), 10, 10 * Space.Proj, Pal.aimRing, 1);
                Vector2 dot = C(aim.x, aim.y);
                painter.Rect(Layer.Effects, dot.x - 1, dot.y - 1, 2, 2, Pal.aimDot);
            }
        }

        void SyncEnemies(RunState r, float dt)
        {
            stale.Clear();
            foreach (Enemy e in enemyViews.Keys) if (!r.enemies.Contains(e)) stale.Add(e);
            foreach (Enemy e in stale) { enemyPool.Release(enemyViews[e]); enemyViews.Remove(e); }
            foreach (Enemy e in r.enemies)
            {
                if (!enemyViews.TryGetValue(e, out EnemyView view)) enemyViews[e] = view = enemyPool.Get();
                pins.TryGetValue(e, out List<Strand> attached);
                view.Render(e, r.time, attached, db, dt);
                float y = Space.ProjectY(e.y);
                if (attached != null && attached.Count > 0)
                    Text?.Label($"×{attached.Count}", new Vector2(e.x, y - e.height - 11), 12, Pal.pinCount, Pal.textStroke, 0, 1, 0, false);
                if (e.cut != null)
                {
                    // 절단선 위의 흰 칼빛
                    float k = Mathf.Pow(e.cut.time / e.cut.max, 2);
                    if (k > .05f)
                    {
                        float angle = e.cut.angle;
                        var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        float w = e.height * (e.type.sprite.rect.width / e.type.sprite.rect.height);
                        var mid = new Vector2(e.x, y - e.height / 2);
                        painter.Line(Layer.Effects, mid - d * w * .8f, mid + d * w * .8f, 1 + 2.5f * k, Painter.Alpha(Pal.sliceGlow, Mathf.Min(1, k * 1.4f)), additive: true);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- threads

        List<Vector2> StrandPoints(Vector2 a, Vector2 b, Element element, float seed, float time, float tension)
        {
            float dx = b.x - a.x, dy = b.y - a.y;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length == 0) length = 1;
            float nx = -dy / length, ny = dx / length;
            int segments = Mathf.Max(2, Mathf.CeilToInt(length / (element == Element.Frustration ? db.visual.frustrationSegment : db.visual.strandSegment)));
            float beat = Mathf.Floor(time * 22);
            var points = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float f = i / (float)segments;
                float envelope = Mathf.Sin(Mathf.PI * f);
                float offset = element switch
                {
                    Element.Despair => Mathf.Sin(f * length / 11 - time * 7 + seed) * 2.6f * envelope,
                    Element.Frustration => (Noise(beat * 31 + i + seed) - .5f) * 9 * envelope,
                    Element.Chaos => (Noise(beat * 17 + i * 3 + seed) - .5f) * 2.4f * envelope,
                    _ => 0,
                };
                if (tension != 0) offset += Mathf.Sin(f * Mathf.PI * 3 + time * 90) * tension * envelope;
                points.Add(new Vector2(a.x + dx * f + nx * offset, a.y + dy * f + ny * offset));
            }
            return points;
        }

        List<Vector2> DrawStrand(Layer layer, Vector2 a, Vector2 b, Element element, float seed, float time, float width, float alpha, float tension, bool flight)
        {
            ElementTone t = Tone(element);
            List<Vector2> points = StrandPoints(a, b, element, seed, time, tension);
            painter.Line(layer, points, width * 3.4f, Painter.Alpha(t.glow, (element == Element.Plain ? .2f : .267f) * alpha), additive: true);
            painter.Line(layer, points, flight ? width * .8f : width, Painter.Alpha(t.color, .9f * alpha));
            painter.Line(layer, points, width * .38f, Painter.Alpha(t.core, .667f * alpha));
            if (element == Element.Chaos)
            {
                // 가시 실: 실을 따라 엇갈린 가시
                float length = Vector2.Distance(a, b);
                Vector2 u = length > 0 ? (b - a) / length : Vector2.right;
                float angle = Mathf.Atan2(u.y, u.x);
                int k = 0;
                for (float d = 14; d < length - 8; d += 21, k++)
                {
                    float side = k % 2 == 1 ? 1 : -1;
                    painter.Sprite(layer, barbSprite, a + u * d, angle, 7, 6 * side, Painter.Alpha(t.color, alpha));
                }
            }
            return points;
        }

        void EmitAlong(List<Vector2> points, Element element, float rate, float dt)
        {
            if (dt <= 0) return;
            ElementTone t = Tone(element);
            float count = rate * dt * points.Count;
            int n = Mathf.FloorToInt(count) + (Rand() < count % 1 ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                Vector2 p = points[Mathf.FloorToInt(Rand() * points.Count) % points.Count];
                switch (element)
                {
                    case Element.Chaos: particles.Emit(ParticleKind.Ember, p, new Vector2((Rand() - .5f) * 18, -24 - Rand() * 30), .5f + Rand() * .3f, t.color, 1.8f); break;
                    case Element.Despair: particles.Emit(ParticleKind.Drop, p, new Vector2((Rand() - .5f) * 6, 8), .7f, t.core, 1.9f); break;
                    case Element.Frustration: particles.Emit(ParticleKind.Spark, p, new Vector2((Rand() - .5f) * 150, (Rand() - .5f) * 150), .14f, t.core, 1.4f); break;
                    default: particles.Emit(ParticleKind.Glint, p, new Vector2(0, -8), .4f, t.core, 1.3f); break;
                }
            }
        }

        void DrawNeedle(Layer layer, Vector2 at, float angle, float length, Color color)
        {
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            painter.Sprite(layer, painter.white, at + dir * -.15f * length, angle, 1.2f * length, 1.35f, color);
            painter.Sprite(layer, needleSprite, at + dir * -.06f * length, angle, length, 5.2f, Color.white);
        }

        void DrawThread(RunState r, Strand s, Vector2 hand, float dt)
        {
            var (start, end) = Combat.Endpoints(r.player, s);
            Vector2 from = s.anchor != null ? new Vector2(start.x, start.y) : hand;
            Element element = s.element ?? Element.Plain;
            ElementTone t = Tone(element);
            var a = C(from.x, from.y);
            var b = C(end.x, end.y) - new Vector2(0, s.target != null ? 23 : 4);
            float seed = s.birth * 13;
            bool flight = s.state == StrandState.Flight;
            // 막 꽂힌 실은 퉁긴 현처럼 떨리다가 팽팽해진다.
            float twang = s.state == StrandState.Stuck ? 7 * Mathf.Exp(-(r.time - s.landed) * 9) : 0;
            List<Vector2> points = DrawStrand(Layer.Ground, a, b, element, seed, r.time, s.anchor != null ? 1.45f : 1.7f, s.anchor != null ? .82f : 1, twang, flight);
            EmitAlong(points, element, element == Element.Frustration ? .5f : element == Element.Plain ? .06f : .16f, dt);
            float bead = (r.time * 2.65f + s.birth) % 1;
            Vector2 p = points[Mathf.FloorToInt(bead * (points.Count - 1))];
            painter.Rect(Layer.Ground, p.x - 1.2f, p.y - 1.2f, 2.6f, 2.6f, Painter.Alpha(t.core, .816f));
            if (s.state == StrandState.Ground)
            {
                float age = r.time - s.landed;
                float quiver = Mathf.Sin(age * 46) * Mathf.Exp(-age * 7) * .42f;
                painter.Ellipse(Layer.Ground, C(end), 7, 7 * Space.Proj, Painter.Alpha(t.color, .737f), 1.15f);
                if (element == Element.Despair)
                {
                    float wave = (r.time * .8f + s.birth) % 1;
                    float radius = 7 + wave * 16;
                    painter.Ellipse(Layer.Ground, C(end), radius, radius * Space.Proj, Painter.Alpha(t.color, (1 - wave) * 120 / 255f), 1);
                }
                DrawNeedle(Layer.Ground, C(end) - new Vector2(0, 8), 1.07f + quiver, 24, t.color);
            }
            if (flight)
            {
                float angle = Mathf.Atan2(s.vy * Space.Proj, s.vx);
                float speed = Mathf.Clamp01((s.speed > 0 ? s.speed : 1400) / 2700);
                Vector2 head = C(end) - new Vector2(0, 3);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float length = 46 + speed * 104;
                SpriteRenderer streak = painter.Sprite(Layer.Ground, streakSprite, head + dir * -.46f * length, angle, length, 18 + speed * 10,
                    new Color(1, 1, 1, .45f + speed * .55f), additive: true);
                painter.Style(streak, fill: t.color, fillAmount: .72f);
                if (dt > 0)
                {
                    for (int n = 0; n < 2; n++)
                        particles.Emit(ParticleKind.Glint, head - dir * Rand() * 18, -dir * 40 + new Vector2((Rand() - .5f) * 40, (Rand() - .5f) * 40), .28f, t.core, 1.6f);
                }
                // 바늘은 속도만큼 늘어난다 (모션 스미어).
                DrawNeedle(Layer.Ground, head, angle, 29 + speed * 14, t.color);
            }
        }

        // ---------------------------------------------------------------- effects

        FxSpriteEntry SpriteEntry(FxSprite id) => Array.Find(fxSprites, f => f.id == id);
        FlipbookEntry Book(FlipSheet sheet) => Array.Find(flipbooks, f => f.sheet == sheet);

        void DrawEffects(RunState r)
        {
            foreach (Fx fx in r.effects)
            {
                if (fx.kind == FxKind.Afterimage) continue;
                float fade = fx.Fade;
                Vector2 at = C(fx.x, fx.y);
                switch (fx.kind)
                {
                    case FxKind.Flip: DrawFlip(fx); break;
                    case FxKind.Cut: DrawCut(fx, fade); break;
                    case FxKind.Sliced: DrawSliced(fx, fade); break;
                    case FxKind.Sprite: DrawSpriteFx(fx, fade, at); break;
                    case FxKind.Number:
                    {
                        float age = 1 - fade;
                        int pop = fx.pop > 0 ? fx.pop : 1;
                        float scale = (1 + Mathf.Min(pop, 6) * .07f) * (age < .16f ? .6f + EaseOutBack(age / .16f) * .4f + (1 - age / .16f) * .35f : 1);
                        Color color = fx.color ?? Color.white;
                        Text?.Label(fx.text, new Vector2(at.x, at.y - fx.lift - (1 - fade) * 34), 24, Painter.Alpha(color, fade), Painter.Alpha(Pal.textStroke, fade), 4, scale, 0, false);
                        break;
                    }
                    case FxKind.Combo:
                    {
                        float age = 1 - fade;
                        float scale = age < .18f ? .4f + EaseOutBack(age / .18f) * .75f : 1.15f - (age - .18f) * .2f;
                        Color color = fx.color ?? Color.white;
                        Text?.Label(fx.text, new Vector2(at.x, at.y - 118 - age * 16), 30, Painter.Alpha(color, fade), Painter.Alpha(Pal.textStroke, fade), 6, scale, Space.Degrees(-.06f), true);
                        break;
                    }
                    case FxKind.Dust:
                    {
                        float age = 1 - fade;
                        float angle = float.IsNaN(fx.angle) ? 0 : fx.angle;
                        for (int i = 0; i < 6; i++)
                        {
                            float spread = (i - 2.5f) * .32f + angle;
                            float d = (10 + i * 4) * Mathf.Sqrt(age) * 2.2f;
                            float rx = 4 + age * 7;
                            painter.Ellipse(Layer.Effects, new Vector2(at.x + Mathf.Cos(spread) * d, at.y + Mathf.Sin(spread) * d * Space.Proj - 3 - age * 6), rx, rx * .55f,
                                Painter.Alpha(Pal.dust, fade * .45f));
                        }
                        break;
                    }
                    case FxKind.Pull: DrawPullSnap(fx, fade); break;
                    case FxKind.Transfer:
                    {
                        float t = 1 - fade;
                        Vector2 a = at, b = C(fx.toX, fx.toY);
                        Vector2 lead = b + (a - b) * t;
                        painter.Line(Layer.Effects, a, lead, 2.4f + 4.7f * fade, Painter.Alpha(Pal.transferGlow, fade), additive: true);
                        painter.Line(Layer.Effects, a, lead, 1.7f + 2.4f * fade, Painter.Alpha(Pal.transferCore, fade));
                        float radius = 12 + t * 30;
                        painter.Ellipse(Layer.Effects, b, radius, radius * Space.Proj, Painter.Alpha(Pal.transferGlow, fade), 1.5f);
                        break;
                    }
                    default: // Burst, Slash: 퍼지는 고리 + 다섯 조각
                    {
                        float radius = fx.kind == FxKind.Slash ? 29 : 14;
                        Color color = Painter.Alpha(fx.color ?? Color.white, fade);
                        float ringR = 5 + (1 - fade) * radius;
                        painter.Ellipse(Layer.Effects, at, ringR, ringR * Space.Proj, color, 2);
                        for (int i = 0; i < 5; i++)
                        {
                            float angle = i * Mathf.PI * 2 / 5 + fx.x;
                            painter.Rect(Layer.Effects, at.x + Mathf.Cos(angle) * (1 - fade) * radius - 1, at.y + Mathf.Sin(angle) * (1 - fade) * radius - 2, 3, 4, color);
                        }
                        break;
                    }
                }
            }
        }

        void DrawSpriteFx(Fx fx, float fade, Vector2 at)
        {
            FxSpriteEntry entry = SpriteEntry(fx.sprite);
            if (entry?.sprite == null) return;
            float progress = 1 - fade;
            // 터지는 스프라이트는 빠르게 튀어나오며 살짝 넘치고, 나머지는 천천히 커진다.
            float size = fx.burst ? fx.size * (.35f + EaseOutBack(Mathf.Min(1, progress * 2.6f)) * .75f + progress * .15f) : fx.size * (.44f + Mathf.Sqrt(progress) * .82f);
            bool hasAngle = !float.IsNaN(fx.angle);
            float spin = hasAngle || entry.directional ? 0 : progress * .31f;
            float angle = (hasAngle ? fx.angle : 0) + spin;
            float alpha = Mathf.Min(1, fade * (fx.burst ? 1.7f : 1.26f)) * fx.alpha;
            Vector2 center = at - new Vector2(0, fx.lift);
            float h = fx.squash ? size * .62f : size;
            bool tinted = entry.tintable && (fx.useElement || fx.color.HasValue);
            Color tint = fx.useElement ? Tone(fx.element).color : fx.color ?? Color.white;
            SpriteRenderer main = painter.Sprite(Layer.Effects, entry.sprite, center, angle, size, h, new Color(1, 1, 1, alpha), additive: true);
            if (tinted) painter.Style(main, fill: tint, fillAmount: .72f);
            SpriteRenderer soft = painter.Sprite(Layer.Effects, entry.sprite, center, angle, size * 1.04f, h * 1.04f, new Color(1, 1, 1, alpha * .25f), additive: true);
            if (tinted) painter.Style(soft, fill: tint, fillAmount: .72f);
        }

        // 플립북은 프레임 단위로 끊어 재생한다 (모양 자체가 변하며 부서진다). 테두리는 실 속성 색으로 곱한다.
        void DrawFlip(Fx fx)
        {
            FlipbookEntry book = Book(fx.sheet);
            if (book == null || book.core.Length == 0) return;
            int frames = book.core.Length;
            float progress = 1 - fx.Fade;
            int frame = Mathf.Min(frames - 1, Mathf.FloorToInt(progress * frames));
            Vector2 center = C(fx.x, fx.y) - new Vector2(0, fx.lift);
            float angle = float.IsNaN(fx.angle) ? 0 : fx.angle;
            float w = fx.size, h = fx.size * (fx.squash ? Space.Proj : 1) * (fx.mirror ? -1 : 1);
            Color tint = ToneColor(fx);
            if (book.rim.Length > frame) painter.Sprite(Layer.Effects, book.rim[frame], center, angle, w, h, new Color(tint.r, tint.g, tint.b, fx.alpha), additive: true);
            painter.Sprite(Layer.Effects, book.core[frame], center, angle, w, h, new Color(1, 1, 1, fx.alpha), additive: true);
        }

        // 실이 칼날이 된다: 닻에서 대상을 지나 뻗는 빛의 칼날 → 두 가닥 머리카락 선으로 갈라지며 흩어진다.
        void DrawCut(Fx fx, float fade)
        {
            float t = 1 - fade;
            Color color = Tone(fx.element).color;
            Vector2 a = C(fx.x, fx.y) - new Vector2(0, 8);
            Vector2 b0 = C(fx.toX, fx.toY) - new Vector2(0, 22);
            float len = Vector2.Distance(a, b0);
            if (len == 0) len = 1;
            Vector2 u = (b0 - a) / len, n = new(-u.y, u.x);
            float reach = len + 70 * Mathf.Min(1, t / .12f + .4f);
            Vector2 b = a + u * reach;
            if (t < .4f)
            {
                float k = 1 - t / .4f;
                painter.Blade(Layer.Effects, a, b, 9 * k, Painter.Alpha(color, .4f), true);
                painter.Blade(Layer.Effects, a, b, 3.2f * k + .6f, Color.white, true);
            }
            if (t > .12f)
            {
                float k = (t - .12f) / .88f, off = 2 + k * 11;
                Color line = Painter.Alpha(color, Mathf.Pow(1 - k, 1.5f));
                foreach (float s in new[] { 1f, -1f })
                    painter.Line(Layer.Effects, a + u * reach * .25f + n * off * s, b + n * off * s, 1.3f, line, additive: true);
            }
        }

        // 처치된 적이 절단선을 따라 두 조각으로 떨어진다.
        void DrawSliced(Fx fx, float fade)
        {
            Sprite im = fx.type.sprite;
            float t = 1 - fade;
            float h = fx.type.height * (fx.elite ? db.enemies.eliteHeight : 1);
            float angle = fx.facing > 0 ? fx.angle : Mathf.PI - fx.angle;
            Vector2 d = new(Mathf.Cos(angle), Mathf.Sin(angle)), n = new(-d.y, d.x);
            float cy = -h / 2;
            Affine root = Affine.Identity.Translate(fx.x, Space.ProjectY(fx.y)).Scale(fx.facing, 1);
            foreach (float s in new[] { 1f, -1f })
            {
                float spread = 8 + 26 * (1 - Mathf.Pow(1 - t, 2.5f)) * (s == fx.side ? 1.3f : .8f);
                float drop = 30 * t * t;
                Affine m = root.Translate(n.x * s * spread + d.x * s * spread * .5f, n.y * s * spread + drop).Rotate(s * t * .35f * Mathf.Rad2Deg);
                float scale = h / im.rect.height;
                SpriteRenderer sr = PlaceAffine(Layer.Effects, im, m.Scale(scale, scale), new Color(1, 1, 1, fade < .35f ? fade / .35f : 1));
                Vector2 c = m.Apply(new Vector2(0, cy));
                Vector2 normal = m.Apply(n * s) - m.Apply(Vector2.zero);
                Vector3 cw = Space.Canvas(c);
                Vector2 nw = new Vector2(normal.x, -normal.y).normalized;
                painter.Style(sr, bright: t < .12f ? 2.4f : .85f, sat: t < .12f ? .3f : .7f, cut: new Vector4(nw.x, nw.y, Vector2.Dot(nw, cw), 1));
            }
        }

        // 캔버스 아핀 행렬(원점 = 스프라이트 피벗)로 스프라이트 배치
        SpriteRenderer PlaceAffine(Layer layer, Sprite sprite, Affine m, Color color)
        {
            float det = m.a * m.d - m.b * m.c;
            float k = Mathf.Sqrt(m.a * m.a + m.b * m.b);
            float angle = det >= 0 ? Mathf.Atan2(m.b, m.a) : Mathf.Atan2(-m.b, -m.a);
            Rect rect = sprite.rect;
            return painter.Sprite(layer, sprite, m.Apply(Vector2.zero), angle, rect.width * (det >= 0 ? k : -k), rect.height * k, color);
        }

        // 팽팽한 실이 히트스톱 동안 떨리다가 점점 빨라지며 닻으로 감겨 든다.
        void DrawPullSnap(Fx fx, float fade)
        {
            float t = 1 - fade;
            Vector2 a = C(fx.x, fx.y) - new Vector2(0, 6);
            Vector2 b = C(fx.toX, fx.toY) - new Vector2(0, 18);
            ElementTone tone = Tone(fx.rage ? Element.Chaos : fx.element);
            float snap = Mathf.Clamp01((t - .16f) / .84f);
            float pulled = Mathf.Pow(snap, 2.4f);
            Vector2 lead = b + (a - b) * pulled;
            float tension = (1 - snap) * 3.2f;
            DrawStrand(Layer.Effects, a, lead, fx.element, fx.seed, fx.time * 3 + fx.seed, 2.6f + 2.8f * fade, Mathf.Min(1, fade * 1.6f), tension, false);
            if (snap > 0 && snap < 1)
            {
                float radius = 3 + 4 * (1 - snap);
                painter.Ellipse(Layer.Effects, lead, radius, radius, tone.core, additive: true);
                if (Rand() < .6f) particles.Emit(ParticleKind.Spark, lead, new Vector2((Rand() - .5f) * 120, (Rand() - .5f) * 120), .18f, tone.color, 1.6f);
            }
            if (t > .9f)
            {
                float radius = 10 + t * 26;
                painter.Ellipse(Layer.Effects, C(fx.x, fx.y), radius, radius * Space.Proj, Painter.Alpha(tone.color, .53f), 1.5f);
            }
        }

        // 대쉬 잔상: 모두 같은 하늘색 실루엣으로 옅어지며 살짝 떠오른다.
        void DrawAfterimages(RunState r)
        {
            foreach (Fx fx in r.effects)
            {
                if (fx.kind != FxKind.Afterimage) continue;
                float fade = fx.Fade;
                float elapsed = fx.progress * db.feel.dashDuration;
                int index = elapsed < db.visual.dodgeLaunchEnd ? 0 : 1;
                if (hero.dodgeFrames.Length <= index) continue;
                float hop = Mathf.Sin(Mathf.PI * Mathf.Clamp01((elapsed - .02f) / .22f)) * db.visual.dodgeHop + (1 - fade) * 4;
                Sprite frame = hero.dodgeFrames[index];
                float s = hero.DodgeScale(fx.y);
                Vector2 at = C(fx.x, fx.y) - new Vector2(0, hop);
                Rect rect = frame.rect;
                SpriteRenderer solid = painter.Sprite(Layer.Ground, frame, at, 0, rect.width * s * fx.facing, rect.height * s, new Color(1, 1, 1, fade * .5f));
                painter.Style(solid, fill: db.visual.dodgeColor, fillAmount: 1);
                SpriteRenderer glow = painter.Sprite(Layer.Ground, frame, at, 0, rect.width * s * fx.facing * 1.04f, rect.height * s * 1.04f, new Color(1, 1, 1, fade * .3f), additive: true);
                painter.Style(glow, fill: db.visual.dodgeColor, fillAmount: 1);
            }
        }
    }
}
