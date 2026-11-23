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
        public Sprite goldIcon, shardIcon, knotIcon, needleSprite, streakSprite, barbSprite, xpOrbSprite, softSprite, heartSprite, levelUpIcon;
        public Sprite stairsSprite, altarSprite, merchantSprite, archSprite;
        public Sprite[] stairPieces;          // 계단 조각: 0 = 꼭대기 문, 1.. = 위 단 → 아래 단 (Codex stairs_steps)
        public FxSpriteEntry[] fxSprites;
        public FlipbookEntry[] flipbooks;

        public IWorldText Text { get; set; }
        Pool<EnemyView> enemyPool;
        readonly Dictionary<Enemy, EnemyView> enemyViews = new();
        readonly Dictionary<Enemy, List<Strand>> pins = new();
        readonly List<Enemy> stale = new();
        float particleClock = -1, viewClock = -1, zoomPhase;
        Combat combat;
        Vector2 handNow;                       // 이번 프레임 손 위치 (전투장 좌표). 당기기 연출이 이 위치로 감겨 든다.
        Rect view;                             // 이번 프레임 카메라가 보는 캔버스 영역
        RenderPalette Pal => db.palette;
        NeedleVisual NV => db.needleVisual;

        public float Zoom => cameraRig.Zoom;

        public void Init(Combat simulation)
        {
            combat = simulation;
            enemyPool = new Pool<EnemyView>(enemyTemplate, enemyRoot);
            hero.Init(db);
            background.Init(db);
        }

        // 라운드마다 새 맵: 바닥 청크를 새 시드로 다시 깔고 적 뷰·입자를 비운다.
        public void BeginRound(RunState r)
        {
            background.BeginRound(UnityEngine.Random.Range(1, 1 << 30));
            enemyPool?.ReleaseAll();
            enemyViews.Clear();
            particles.Clear();
            zoomPhase = 0;
        }

        // 정화된 정도 (0~1): 정화 고리가 지금 카메라 화면을 덮은 비율. 정화가 끝난 층은 1.
        public float PurifiedCoverage(RunState r)
        {
            if (r.purified) return 1;
            if (r.phase != Phase.Purifying || r.purifyCenter == null || combat == null) return 0;
            float far = 1;
            foreach (Vector2 corner in new[] { view.min, view.max, new Vector2(view.xMin, view.yMax), new Vector2(view.xMax, view.yMin) })
            {
                float ay = (corner.y - Space.Top) / Space.Proj;
                far = Mathf.Max(far, Mathf.Sqrt(Mathf.Pow(corner.x - r.purifyCenter.x, 2) + Mathf.Pow(ay - r.purifyCenter.y, 2)));
            }
            return Mathf.Clamp01(combat.PurifyRadius(r) / far);
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

        public void Render(RunState r, Vector2 aim, float clock, bool active, float heroActionTime, Interactable nearest)
        {
            painter.Begin();
            float viewDt = viewClock < 0 ? 0 : Mathf.Clamp(clock - viewClock, 0, .1f);
            viewClock = clock;
            UpdateCamera(r, viewDt, clock);
            background.Render(r, view, clock, Zoom);
            ApplyPurifyGlobals(r);
            DrawMap();
            if (particleClock < 0 || r.time < particleClock || r.time - particleClock > .5f) particleClock = r.time;
            float dt = Mathf.Min(.05f, r.time - particleClock);
            particleClock = r.time;
            particles.SetRunning(dt > 0);

            DrawDrops(r, clock);
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

            DrawProps(r, nearest, clock, dt);
            DrawPurifyRing(r, dt);
            CaptureDeaths(r);
            threadScale = combat != null ? combat.ThreadScale(r) : 1;
            DrawBossDeath(r, dt);

            Player player = r.player;
            HeroFrame frame = hero.Pose(player, heroActionTime);
            hero.Apply(player, frame);
            Vector2 hand = hero.Hand();
            handNow = hand;
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
            hero.Render(player, r.outcome == Outcome.Absorbed ? .55f : 1, Pal.shadow);
            DrawLevelUp(player, dt);
            DrawHeal(player, dt);
            DrawEffects(r);
            if (active && aim.x >= 0 && aim.x <= db.arena.width && aim.y >= 0 && aim.y <= db.arena.height)
            {
                painter.Ellipse(Layer.Effects, C(aim.x, aim.y), 10, 10 * Space.Proj, Pal.aimRing, 1);
                Vector2 dot = C(aim.x, aim.y);
                painter.Rect(Layer.Effects, dot.x - 1, dot.y - 1, 2, 2, Pal.aimDot);
            }
        }

        // 경험치 구(초록)·골드: 튀어 올랐다가 몸으로 빨려 든다. 빛을 둘러 멀리서도 보이게 한다.
        void DrawDrops(RunState r, float clock)
        {
            RewardConfig rw = db.rewards;
            Vector2 hero = C(r.player.x, r.player.y);
            foreach (Drop d in r.drops)
            {
                float pop = Mathf.Max(.01f, rw.dropPopTime);
                float t = Mathf.Clamp01(d.age / pop);
                float hop = d.age < pop ? Mathf.Sin(Mathf.PI * t) * rw.dropHop : 0;
                Vector2 ground = C(d.x, d.y);
                bool flying = d.age >= pop;
                // 빨려 드는 중에는 몸(허리 높이) 쪽으로 떠오른다.
                float rise = flying ? Mathf.Clamp01(1 - Vector2.Distance(ground, hero) / 160) * 18 : 0;
                Vector2 at = ground - new Vector2(0, 6 + hop + rise);
                float bob = Mathf.Sin(clock * 6 + d.x * .05f) * 1.2f;
                painter.Ellipse(Layer.Ground, ground, 5, 5 * Space.Proj * .6f, Painter.Alpha(Pal.shadow, .5f));
                if (d.xp > 0)
                {
                    float size = rw.orbSize > 0 ? rw.orbSize : 12;
                    if (softSprite != null)
                        painter.Sprite(Layer.Effects, softSprite, at, 0, size * rw.dropGlow, size * rw.dropGlow, Painter.Alpha(rw.xpColor, .55f), additive: true);
                    painter.Sprite(Layer.Effects, xpOrbSprite != null ? xpOrbSprite : goldIcon, at + new Vector2(0, bob), 0, size, size, Color.white);
                }
                else if (d.gold > 0)
                {
                    float size = rw.coinSize > 0 ? rw.coinSize : 16;
                    if (softSprite != null)
                        painter.Sprite(Layer.Effects, softSprite, at, 0, size * rw.dropGlow * .8f, size * rw.dropGlow * .8f, Painter.Alpha(rw.goldColor, .4f), additive: true);
                    // 동전이 돌듯 가로로 줄었다 늘어난다.
                    float spin = .55f + .45f * Mathf.Abs(Mathf.Cos(clock * 5 + d.x));
                    painter.Sprite(Layer.Effects, goldIcon, at + new Vector2(0, bob), 0, size * spin, size, Color.white);
                }
                else if (d.heal > 0)
                {
                    // 하트: 붉은 빛을 두르고 심장처럼 두근거린다.
                    float size = rw.heartSize > 0 ? rw.heartSize : 18;
                    float beat = 1 + .12f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(clock * 7 + d.x)), 8);
                    if (softSprite != null)
                        painter.Sprite(Layer.Effects, softSprite, at, 0, size * rw.dropGlow, size * rw.dropGlow, Painter.Alpha(rw.heartGlowColor, .55f), additive: true);
                    painter.Sprite(Layer.Effects, heartSprite != null ? heartSprite : goldIcon, at + new Vector2(0, bob), 0, size * beat, size * beat, Color.white);
                }
                else painter.Sprite(Layer.Effects, shardIcon, at, 0, 20, 20, Color.white);
                if (flying)
                {
                    Vector2 dir = (hero - ground).normalized;
                    Color tail = d.xp > 0 ? rw.xpColor : d.heal > 0 ? rw.heartGlowColor : rw.goldColor;
                    painter.Line(Layer.Effects, at, at - dir * 14, 2, Painter.Alpha(tail, .5f), additive: true);
                }
            }
        }

        // ---------------------------------------------------------------- camera, map, props

        // 캐릭터를 따라가는 카메라 (맵 경계 + 여유 안). 정화 중에는 줌아웃했다가 끝나면 돌아온다.
        void UpdateCamera(RunState r, float dt, float clock)
        {
            PurifyConfig pc = db.purify;
            bool zoomingOut = r.phase == Phase.Purifying;
            float span = zoomingOut ? pc.zoomOut : pc.zoomIn;
            zoomPhase = Mathf.MoveTowards(zoomPhase, zoomingOut ? 1 : 0, span > 0 ? dt / span : 1);
            float zoom = 1 + (Mathf.Max(1, pc.zoom) - 1) * Mathf.SmoothStep(0, 1, zoomPhase);
            MapConfig m = db.map;
            float o = m.cameraOverscan;
            Rect bounds = Rect.MinMaxRect(-o, Space.ProjectY(0) - m.wallHeight - o, db.arena.width + o, Space.ProjectY(db.arena.height) + o);
            cameraRig.Apply(C(r.player.x, r.player.y), bounds, zoom, r.shake, r.kick, clock);
            view = cameraRig.VisibleCanvas();
        }

        // 바닥 셰이더(SpriteFX 의 _Purify)가 쓰는 정화 원: (월드 중심 x, y, 월드 반지름(가로), 세로 압축).
        static readonly int PurifyCircleId = Shader.PropertyToID("_TOD_PurifyCircle"), PurifyLookId = Shader.PropertyToID("_TOD_PurifyLook"),
            PurifyFillId = Shader.PropertyToID("_TOD_PurifyFill");

        void ApplyPurifyGlobals(RunState r)
        {
            PurifyConfig pc = db.purify;
            float radius = r.purified ? 1e6f : r.phase == Phase.Purifying && combat != null ? combat.PurifyRadius(r) : -1;
            Vector3 center = r.purifyCenter != null ? Space.Ground(r.purifyCenter.x, r.purifyCenter.y) : Vector3.zero;
            Shader.SetGlobalVector(PurifyCircleId, new Vector4(center.x, center.y, radius / Space.Ppu, Space.Proj));
            Shader.SetGlobalVector(PurifyLookId, new Vector4(pc.floorBright, pc.floorSat, Mathf.Max(1, pc.edgeSoftness) / Space.Ppu, pc.edgeNoise / Space.Ppu));
            Shader.SetGlobalColor(PurifyFillId, pc.floorFill);
        }

        // 맵 위쪽 탑 벽(아치 반복)과 바닥 테두리. 보이는 부분만 그린다.
        void DrawMap()
        {
            MapConfig m = db.map;
            float width = db.arena.width, top = Space.ProjectY(0);
            float x0 = Mathf.Max(0, view.xMin), x1 = Mathf.Min(width, view.xMax);
            if (x1 > x0 && view.yMin < top)
            {
                painter.Rect(Layer.Background, x0, top - m.wallHeight, x1 - x0, m.wallHeight, m.wallColor);
                if (archSprite != null && m.archHeight > 0 && m.archSpacing > 0)
                {
                    float h = m.archHeight, w = h * archSprite.rect.width / archSprite.rect.height;
                    for (float x = Mathf.Floor(x0 / m.archSpacing) * m.archSpacing + m.archSpacing / 2; x < x1 + m.archSpacing; x += m.archSpacing)
                        if (x - w / 2 >= 0 && x + w / 2 <= width) painter.Sprite(Layer.Background, archSprite, new Vector2(x, top - h / 2), 0, w, h, Color.white);
                }
                painter.Line(Layer.Background, new Vector2(x0, top), new Vector2(x1, top), 3, m.wallEdge);
            }
            Border(m.borderInset, 2, m.borderColor);
            Border(m.borderInnerInset, 1, m.borderInnerColor);
        }

        void Border(float inset, float width, Color color)
        {
            if (color.a <= 0) return;
            float w = db.arena.width - inset, h = db.arena.height - inset;
            painter.Line(Layer.Ground, new[] { C(inset, inset), C(w, inset), C(w, h), C(inset, h), C(inset, inset) }, width, color);
        }

        // 쉬어가는 라운드의 제단·상인, 라운드가 끝나면 나타나는 계단과 그 위 안내 문구.
        void DrawProps(RunState r, Interactable nearest, float clock, float dt)
        {
            if (r.altar != null) DrawProp(altarSprite, Pal.altarHeight, r.altar, r.artifactTaken ? null : db.ui.promptAltar, nearest == Interactable.Altar, r.artifactTaken ? .55f : 1);
            if (r.merchant != null) DrawProp(merchantSprite, Pal.merchantHeight, r.merchant, db.ui.promptMerchant, nearest == Interactable.Merchant, 1);
            if (r.stairs == null) return;
            if (stairPieces != null && stairPieces.Length > 0) { DrawStairCascade(r, nearest, clock, dt); return; }
            if (stairsSprite == null) return;
            float reveal = db.rounds.stairsRevealTime;
            float k = reveal > 0 ? Mathf.Clamp01(r.stairsAge / reveal) : 1;
            Vector2 at = C(r.stairs);
            Color glow = Pal.stairsGlow;
            float pulse = .5f + .5f * Mathf.Sin(clock * 2.4f);
            painter.Ellipse(Layer.Ground, at, 120, 120 * Space.Proj, Painter.Alpha(glow, (.16f + .08f * pulse) * k), additive: true);
            Rect rect = stairsSprite.rect;
            painter.Sprite(Layer.Ground, stairsSprite, at - new Vector2(0, rect.height * .35f), 0, rect.width, rect.height, new Color(1, 1, 1, k));
            if (k >= 1) PropLabel(db.ui.promptStairs, at - new Vector2(0, rect.height * .7f + 22), nearest == Interactable.Stairs);
        }

        // 계단: 꼭대기 문(0번)부터 맨 아래 단까지 한 조각씩 차례로 '다라라락' 놓인다.
        // 조각은 하늘에서 떨어지지 않고, 제자리 바로 위(stairDrop)에서 살짝 내려앉으며 나타나고, 닿을 때 먼지가 인다.
        void DrawStairCascade(RunState r, Interactable nearest, float clock, float dt)
        {
            RoundConfig rc = db.rounds;
            Vector2 at = C(r.stairs);
            Color glow = Pal.stairsGlow;
            int n = stairPieces.Length;
            float scale = Pal.stairScale > 0 ? Pal.stairScale : .4f;
            float rise = Pal.stairRise > 0 ? Pal.stairRise : 22;
            float drop = Pal.stairDrop;
            float settle = Mathf.Max(.01f, rc.stairSettle);
            bool done = r.stairsAge >= rc.stairsRevealTime;
            float pulse = .5f + .5f * Mathf.Sin(clock * 2.4f);
            // 바닥 빛: 계단이 놓이는 만큼 은은하게
            float shown = Mathf.Clamp01(r.stairsAge / Mathf.Max(.01f, rc.stairsRevealTime));
            if (softSprite != null)   // 바닥 빛: 가장자리가 부드러운 빛 (단단한 원판처럼 보이지 않게)
                painter.Sprite(Layer.Ground, softSprite, at, 0, 230, 230 * Space.Proj, Painter.Alpha(glow, (.18f + .07f * pulse) * shown), additive: true);
            float topY = at.y;
            // 그늘 타원은 두지 않는다(계단이 떠 보였다). 맨 아래 조각은 앞면이 거의 없는 '바닥에 붙은 첫 단'이라 발밑(at.y)에 납작하게 닿는다.
            // 바닥 청크와 같은 색감: 라운드 컨셉 덮개(정화된 층은 정화 밝기)
            ConceptDef concept = r.concept;
            PurifyConfig pc = db.purify;
            // 조각 i 의 바닥(그림 아래 끝): 맨 아래 단(바닥에 붙은 첫 단)이 발밑 at.y, 그 위로 한 단씩 올라간다.
            // 그림은 아래 단일수록 조금씩 크게 그려져 있어, 한 단 높이(rise, 맨 위 단 기준)를 바로 아래 조각의 그림 높이에 비례해 늘린다
            // (일정하게 두면 아래쪽 단이 윗단 앞면을 점점 더 가린다).
            float unit = n > 1 && stairPieces[1] != null ? stairPieces[1].rect.height : 0;
            float[] bottoms = new float[n];
            bottoms[n - 1] = at.y;
            for (int i = n - 2; i >= 0; i--)
            {
                Sprite below = stairPieces[i + 1];
                bottoms[i] = bottoms[i + 1] - (unit > 0 && below != null ? rise * below.rect.height / unit : rise);
            }
            // 뒤(꼭대기)부터 그려야 앞 단이 위에 겹친다.
            for (int i = 0; i < n; i++)
            {
                Sprite piece = stairPieces[i];
                if (piece == null) continue;
                float w = piece.rect.width * scale, h = piece.rect.height * scale;
                float bottom = bottoms[i];
                float t = (r.stairsAge - i * rc.stairStepDelay) / settle;
                if (i == 0) topY = bottom - h;
                if (t <= 0) continue;
                float u = Mathf.Clamp01(t);
                float ease = 1 - Mathf.Pow(1 - u, 3);
                float squash = u < 1 ? 1 : 1 - .06f * Mathf.Exp(-(t - 1) * 10) * Mathf.Sin((t - 1) * 30);
                Vector2 center = new(at.x, bottom - h / 2 - drop * (1 - ease));
                SpriteRenderer sr = painter.Sprite(Layer.Ground, piece, center, 0, w, h * squash, new Color(1, 1, 1, Mathf.Min(1, u * 2.2f)));
                if (r.purified) painter.Style(sr, bright: pc.floorBright, sat: pc.floorSat, fill: pc.floorFill, fillAmount: pc.floorFill.a);
                else if (concept != null) painter.Style(sr, fill: concept.floorFill, fillAmount: concept.floorFill.a);
                // 자리 잡는 순간 앞 모서리에서 먼지·빛 부스러기
                if (dt > 0 && t >= 1 && t - dt / settle < 1)
                {
                    for (int d = 0; d < 6; d++)
                        particles.Emit(d % 2 == 0 ? ParticleKind.Spark : ParticleKind.Glint, new Vector2(at.x + (Rand() - .5f) * w * .9f, bottom - 2),
                            new Vector2((Rand() - .5f) * 80, -10 - Rand() * 30), .35f + Rand() * .2f, d % 2 == 0 ? Pal.dust : glow, 2 + Rand() * 1.5f);
                }
            }
            // 꼭대기 문 빛: 다 놓인 뒤 숨 쉬듯
            if (stairPieces[0] != null && softSprite != null && done)
            {
                float lw = stairPieces[0].rect.width * scale;
                painter.Sprite(Layer.Effects, softSprite, new Vector2(at.x, topY + (bottoms[0] - topY) * .45f), 0, lw * .9f, lw * 1.2f, Painter.Alpha(glow, (.22f + .1f * pulse)), additive: true);
            }
            if (dt > 0 && done && Rand() < .25f)
                particles.Emit(ParticleKind.Glint, new Vector2(at.x + (Rand() - .5f) * 60, topY + Rand() * 40), new Vector2(0, -40), .9f, glow, 1.8f);
            if (done) PropLabel(db.ui.promptStairs, new Vector2(at.x, topY - 18), nearest == Interactable.Stairs);
        }

        void DrawProp(Sprite sprite, float height, Point p, string label, bool near, float alpha)
        {
            Vector2 at = C(p);
            float h = 0;
            if (sprite != null)
            {
                Rect rect = sprite.rect;
                h = height > 0 ? height : rect.height;
                float w = h * rect.width / rect.height;
                painter.Ellipse(Layer.Ground, at, w * .4f, w * .4f * .34f, Pal.shadow);
                painter.Sprite(Layer.Ground, sprite, at - new Vector2(0, h * .42f), 0, w, h, new Color(1, 1, 1, alpha));
            }
            if (near) painter.Ellipse(Layer.Ground, at, 46, 46 * Space.Proj, Painter.Alpha(Pal.promptActive, .7f), 1.5f);
            if (!string.IsNullOrEmpty(label)) PropLabel(label, at - new Vector2(0, h * .85f + 18), near);
        }

        void PropLabel(string text, Vector2 at, bool near) =>
            // 작은 글자는 테두리가 글자 안을 덮지 않게 글자 크기에 맞춰 얇게 둔다.
            Text?.Label(text, at, PropPx, near ? Pal.promptActive : Pal.promptIdle, Pal.textStroke, Mathf.Max(1.5f, PropPx * .14f), near ? 1.1f : 1, 0, false);

        float PropPx => Pal.propLabelPx > 0 ? Pal.propLabelPx : 15;

        // 보스 정화: 보스가 있던 자리에서 바닥이 번지듯 밝아진다. 둥근 테두리 선은 그리지 않고,
        // 경계는 셰이더가 꽃잎처럼 흐트러뜨리며, 경계를 따라 상아·청록 꽃잎 빛이 피어오른다.
        void DrawPurifyRing(RunState r, float dt)
        {
            if (r.phase != Phase.Purifying || r.purifyCenter == null || combat == null) return;
            PurifyConfig pc = db.purify;
            float radius = combat.PurifyRadius(r);
            Vector2 at = C(r.purifyCenter);
            float u = pc.duration > 0 ? Mathf.Clamp01(r.purifyTime / pc.duration) : 1;
            // 가운데에서 한 번 크게 피었다가 사그라지는 빛
            if (softSprite != null && pc.centerGlow > 0)
            {
                float bloom = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.6f));
                float size = pc.centerGlow * (.6f + u * .8f);
                painter.Sprite(Layer.Effects, softSprite, at - new Vector2(0, 30), 0, size, size * .8f, Painter.Alpha(pc.glowColor, .35f * bloom), additive: true);
                painter.Sprite(Layer.Effects, softSprite, at - new Vector2(0, 30), 0, size * .45f, size * .4f, Painter.Alpha(pc.ringColor, .45f * bloom), additive: true);
            }
            if (dt <= 0 || pc.petalRate <= 0) return;
            // 경계 위 무작위 지점(화면 안만)에서 꽃잎 빛이 떠오른다
            int count = Mathf.FloorToInt(pc.petalRate * dt) + (Rand() < pc.petalRate * dt % 1 ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                float angle = Rand() * Mathf.PI * 2;
                float rr = radius * (.86f + Rand() * .14f);
                Vector2 p = at + new Vector2(Mathf.Cos(angle) * rr, Mathf.Sin(angle) * rr * Space.Proj);
                if (!view.Contains(p)) continue;
                Color c = Rand() < .55f ? pc.ringColor : pc.glowColor;
                particles.Emit(ParticleKind.Glint, p, new Vector2((Rand() - .5f) * 20, -30 - Rand() * 50), .9f + Rand() * .6f, c, 1.8f + Rand() * 1.6f);
            }
        }

        // 보스 처치: 실이 몸을 감아 조이고 → 하얀 칼빛과 함께 종이처럼 잘리고 → 조각과 종이 부스러기가 흩어진다.
        void DrawBossDeath(RunState r, float dt)
        {
            BossDeath b = r.bossDeath;
            if (r.phase != Phase.BossDying || b == null || b.type == null) return;
            BossDeathConfig bd = db.bossDeath;
            Sprite art = b.type.sprite;
            float h = b.height, w = h * art.rect.width / art.rect.height;
            Vector2 foot = C(b.x, b.y);
            Vector2 center = foot - new Vector2(0, h / 2);
            // 몬스터 그림은 발밑 기준(피벗)이라, 그림을 놓는 점과 회전 중심은 피벗 위치다.
            Vector2 anchor = center + new Vector2((art.pivot.x / art.rect.width - .5f) * w * b.facing, (.5f - art.pivot.y / art.rect.height) * h);
            float t = b.time;
            float wrapK = bd.wrapTime > 0 ? Mathf.Clamp01(t / bd.wrapTime) : 1;
            float cutT = t - bd.wrapTime;
            float fallT = cutT - bd.cutTime;
            Color thread = bd.threadColor;
            // 절단선: 비스듬히 평행한 cuts 줄 (cutAngle 기준), 몸 높이에 고르게
            float angle = b.cutAngle;
            var u = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var n = new Vector2(-u.y, u.x);
            int cuts = Mathf.Max(1, bd.cuts);
            float span = h * .62f;
            float Offset(int k) => (k + 1f) / (cuts + 1f) * span - span / 2;

            if (fallT < 0)
            {
                // 묶이는 동안: 떨며 어두워지고, 실이 위에서 아래로 차례로 감기며 조인다.
                float shake = Mathf.Sin(t * 70) * 2.2f * wrapK;
                bool flash = cutT >= 0;
                DrawBody(Layer.Ground, bossParts, art, foot, anchor, new Vector2(shake, 0), 0, w * b.facing, h, Color.white,
                    flash ? 2.2f : 1 - .25f * wrapK, flash ? .2f : 1 - .4f * wrapK, null, null);
                int loops = Mathf.Max(1, bd.loops);
                float tighten = 1 - Mathf.Pow(1 - wrapK, 2);
                for (int i = 0; i < loops; i++)
                {
                    float show = Mathf.Clamp01(wrapK * loops - i);
                    if (show <= 0) continue;
                    float y = foot.y - h * (.12f + .76f * (i + .5f) / loops);
                    float rx = w * (.62f - .3f * tighten) * (.85f + .15f * Mathf.Sin(i * 1.7f));
                    var points = new List<Vector2>();
                    int seg = 28;
                    for (int k = 0; k <= seg * show; k++)
                    {
                        float a = k / (float)seg * Mathf.PI * 2 + i * .9f;
                        points.Add(new Vector2(foot.x + shake + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * rx * .28f - k / (float)seg * h * .05f));
                    }
                    if (points.Count < 2) continue;
                    painter.Line(Layer.Effects, points, bd.threadWidth * 3, Painter.Alpha(thread, .35f), additive: true);
                    painter.Line(Layer.Effects, points, bd.threadWidth, thread);
                }
                if (cutT >= 0)
                {
                    // 잘리는 순간: 절단선을 따라 하얀 칼빛이 차례로 그어진다.
                    for (int k = 0; k < cuts; k++)
                    {
                        float kk = Mathf.Clamp01(cutT / Mathf.Max(.01f, bd.cutTime) * cuts - k);
                        if (kk <= 0) continue;
                        Vector2 c0 = center + n * Offset(k);
                        float len = w * 1.4f;
                        Vector2 a = c0 - u * len * .5f, e = a + u * len * kk;
                        painter.Blade(Layer.Effects, a, e, 10 * (1 - kk * .5f), Painter.Alpha(bd.cutColor, .5f), true);
                        painter.Blade(Layer.Effects, a, e, 3, Color.white, true);
                    }
                }
                return;
            }

            // 떨어지는 동안: 절단선 사이 조각마다 바깥으로 밀려나며 기울어 떨어지고 옅어진다.
            float fk = bd.fallTime > 0 ? Mathf.Clamp01(fallT / bd.fallTime) : 1;
            float alpha = fk < .6f ? 1 : 1 - (fk - .6f) / .4f;
            for (int piece = 0; piece <= cuts; piece++)
            {
                float side = piece - cuts / 2f;
                float drift = (10 + 26 * (1 - Mathf.Pow(1 - fk, 2.4f))) * side;
                float drop = 60 * fk * fk * (1 + Mathf.Abs(side) * .3f);
                float rot = side * .18f * fk;
                Vector2 offset = n * drift + new Vector2(0, drop);
                Vector4? lower = piece > 0 ? Plane(anchor, center, offset, rot, n, Offset(piece - 1), 1) : null;
                Vector4? upper = piece < cuts ? Plane(anchor, center, offset, rot, n, Offset(piece), -1) : null;
                DrawBody(Layer.Ground, bossParts, art, foot, anchor, offset, rot, w * b.facing, h, new Color(1, 1, 1, alpha),
                    fk < .08f ? 1.8f : .9f, .8f, lower ?? upper, lower != null ? upper : null);
                // 잘린 종이 가장자리: 밝은 종이색 선
                if (upper != null)
                {
                    Vector2 edge = anchor + offset + Rotate(center + n * Offset(piece) - anchor, rot);
                    Vector2 dir = Rotate(u, rot);
                    painter.Line(Layer.Effects, edge - dir * w * .55f, edge + dir * w * .55f, 1.6f, Painter.Alpha(bd.flakeColor, alpha * .9f));
                }
            }
            // 종이 부스러기·끊어진 실 끝
            if (dt > 0 && fk < .5f)
            {
                int flakes = Mathf.Max(0, bd.flakes);
                for (int i = 0; i < flakes * dt * 6 && i < flakes; i++)
                {
                    Vector2 p0 = center + n * Offset(Mathf.FloorToInt(Rand() * cuts)) + u * (Rand() - .5f) * w;
                    var v = new Vector2((Rand() - .5f) * bd.flakeSpeed, -Rand() * bd.flakeSpeed * .6f);
                    particles.Emit(Rand() < .7f ? ParticleKind.Spark : ParticleKind.Glint, p0, v, .6f + Rand() * .5f, Rand() < .7f ? bd.flakeColor : thread, 2.4f + Rand() * 2);
                }
            }
        }

        static Vector2 Rotate(Vector2 v, float a) => new(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));

        // 조각을 offset 만큼 옮기고 피벗(anchor) 기준으로 rot 만큼 돌렸을 때 절단선(center + n·d)의 월드 반평면. keep = +1 이면 n 쪽을 남긴다.
        static Vector4 Plane(Vector2 anchor, Vector2 center, Vector2 offset, float rot, Vector2 n, float d, float keep)
        {
            Vector2 point = anchor + offset + Rotate(center + n * d - anchor, rot);
            Vector2 normal = Rotate(n, rot) * keep;
            Vector3 world = Space.Canvas(point);
            var nw = new Vector2(normal.x, -normal.y).normalized;
            return new Vector4(nw.x, nw.y, Vector2.Dot(nw, world), 1);
        }

        // 레벨업: 발밑에서 초록 빛 기둥이 솟고, 충격파 고리 두 겹이 퍼지며, ▲ 화살표가 몸을 타고 올라간다.
        // 몸은 처음에 밝은 초록으로 번쩍였다가(HeroView) 은은한 기운이 감돈다. 숫자·글자 없이 "올라갔다"가 보이게.
        void DrawLevelUp(Player p, float dt)
        {
            VisualConfig v = db.visual;
            if (p.levelUp <= 0 || v.levelUpTime <= 0) return;
            float elapsed = v.levelUpTime - p.levelUp;
            float k = p.levelUp / v.levelUpTime;
            float fade = Mathf.Min(1, k * 3);
            Color c = v.levelUpColor;
            Vector2 foot = C(p.x, p.y);
            float body = 50 * hero.Scale(p.y) / Mathf.Max(.001f, db.visual.heroScaleBase);
            float beamH = v.levelUpBeam > 0 ? v.levelUpBeam : 180;
            // 빛 기둥: 처음 0.15초에 위로 쭉 뻗고, 천천히 가늘어지며 사라진다
            float rise = Mathf.Clamp01(elapsed / .15f);
            float beamFade = Mathf.Clamp01(1 - elapsed / (v.levelUpTime * .7f));
            if (softSprite != null && beamFade > 0)
            {
                float width = 34 * (.5f + .5f * beamFade);
                Vector2 mid = foot - new Vector2(0, beamH * rise * .5f);
                painter.Sprite(Layer.Effects, softSprite, mid, 0, width * 2.4f, beamH * rise * 1.1f, Painter.Alpha(c, .45f * beamFade), additive: true);
                painter.Sprite(Layer.Effects, softSprite, mid, 0, width * .9f, beamH * rise, Painter.Alpha(Color.Lerp(c, Color.white, .6f), .8f * beamFade), additive: true);
            }
            // 충격파 고리 두 겹 (발밑, 0.45초)
            for (int i = 0; i < 2; i++)
            {
                float t = (elapsed - i * .12f) / .45f;
                if (t <= 0 || t >= 1) continue;
                float rr = 10 + (1 - Mathf.Pow(1 - t, 2)) * 85;
                painter.Ellipse(Layer.Ground, foot, rr, rr * Space.Proj, Painter.Alpha(c, (1 - t) * .95f), 4 - i * 1.5f, additive: true);
            }
            // ▲ 화살표: 발밑에서 머리 위로 차례로 솟는다
            int chevrons = Mathf.Max(0, v.levelUpChevrons);
            for (int i = 0; i < chevrons; i++)
            {
                float t = (elapsed - i * .18f) / .7f;
                if (t <= 0 || t >= 1) continue;
                float y = foot.y - body * .1f - t * (body * 1.4f + 30);
                float a = Mathf.Sin(Mathf.PI * t);
                float w = 18 - i * 2f;
                var tip = new Vector2(foot.x, y - w * .7f);
                var pts = new List<Vector2> { new(foot.x - w, y), tip, new(foot.x + w, y) };
                painter.Line(Layer.Effects, pts, 14, Painter.Alpha(c, .55f * a), additive: true);
                painter.Line(Layer.Effects, pts, 5.5f, Painter.Alpha(Color.Lerp(c, Color.white, .2f), a));
            }
            // 레벨업 아이콘: 몸 둘레 여기저기에서 차례로 톡 튀어나와 살짝 떠오르며 사라진다.
            if (levelUpIcon != null)
            {
                int icons = Mathf.Max(0, v.levelUpIcons);
                float size = v.levelUpIconSize > 0 ? v.levelUpIconSize : 18;
                float spread = v.levelUpIconSpread > 0 ? v.levelUpIconSpread : 36;
                for (int i = 0; i < icons; i++)
                {
                    float start = i * .11f + Noise(i * 7.3f + p.x * .01f) * .08f;
                    float t = (elapsed - start) / .75f;
                    if (t <= 0 || t >= 1) continue;
                    float ang = (i / (float)icons + Noise(i * 3.1f) * .4f) * Mathf.PI * 2;
                    float dist = spread * (.55f + Noise(i * 5.7f) * .45f);
                    Vector2 home = foot + new Vector2(Mathf.Cos(ang) * dist, -body * .55f + Mathf.Sin(ang) * dist * .75f);
                    float popIn = t < .2f ? EaseOutBack(t / .2f) : 1;
                    float alpha = t < .7f ? 1 : 1 - (t - .7f) / .3f;
                    Vector2 at = home - new Vector2(0, t * 18);
                    float sz = size * popIn * (.85f + Noise(i * 9.1f) * .3f);
                    if (softSprite != null) painter.Sprite(Layer.Effects, softSprite, at, 0, sz * 2.2f, sz * 2.2f, Painter.Alpha(c, .35f * alpha), additive: true);
                    painter.Sprite(Layer.Effects, levelUpIcon, at, 0, sz, sz, new Color(1, 1, 1, alpha));
                }
            }
            // 은은하게 남는 기운
            if (softSprite != null)
            {
                float pulse = .8f + .2f * Mathf.Sin(elapsed * 9);
                float aura = v.levelUpGlow > 0 ? v.levelUpGlow : 120;
                painter.Sprite(Layer.Effects, softSprite, foot - new Vector2(0, body * .5f), 0, aura * pulse, aura * 1.35f * pulse, Painter.Alpha(c, .5f * fade), additive: true);
            }
            if (dt <= 0) return;
            int count = Mathf.FloorToInt(v.levelUpRate * dt * fade) + (Rand() < v.levelUpRate * dt * fade % 1 ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                float ang = Rand() * Mathf.PI * 2;
                Vector2 at = foot + new Vector2(Mathf.Cos(ang) * 16, -Rand() * body);
                particles.Emit(ParticleKind.Glint, at, new Vector2(Mathf.Cos(ang) * 14, -70 - Rand() * 60), .7f + Rand() * .4f, Color.Lerp(c, Color.white, Rand() * .5f), 2.4f + Rand() * 1.8f);
            }
        }

        // 하트를 먹으면: 몸 실루엣이 발끝부터 머리까지 회복 색(rewards.healColor, 연두)으로 차오르고, 차오르는 선이 빛나며 거품이 떠오른다.
        void DrawHeal(Player p, float dt)
        {
            RewardConfig rw = db.rewards;
            if (p.heal <= 0 || rw.healTime <= 0) return;
            float elapsed = rw.healTime - p.heal;
            float fillT = Mathf.Clamp01(elapsed / (rw.healTime * .55f));
            float level01 = 1 - Mathf.Pow(1 - fillT, 2);
            float fade = Mathf.Clamp01(p.heal / (rw.healTime * .35f));
            List<PartSnap> parts = hero.Snapshot();
            if (parts.Count == 0) return;
            float top = float.MaxValue, bottom = float.MinValue, left = float.MaxValue, right = float.MinValue;
            foreach (PartSnap part in parts)
            {
                float r = Mathf.Max(Mathf.Abs(part.w), Mathf.Abs(part.h)) * .5f;
                top = Mathf.Min(top, part.center.y - r); bottom = Mathf.Max(bottom, part.center.y + r);
                left = Mathf.Min(left, part.center.x - r); right = Mathf.Max(right, part.center.x + r);
            }
            // 실루엣 범위를 조금 안쪽으로 (그림 여백 제외)
            float span = bottom - top;
            top += span * .12f; bottom -= span * .08f;
            float line = Mathf.Lerp(bottom, top, level01);
            Vector3 world = Space.Canvas(new Vector2(0, line));
            var keepBelow = new Vector4(0, -1, -world.y, 1);    // 월드 y 가 선보다 아래인 부분만 남긴다
            Color col = rw.healColor;
            foreach (PartSnap part in parts)
            {
                SpriteRenderer sr = painter.Sprite(Layer.Effects, part.sprite, part.center, part.angle, part.w, part.h, new Color(1, 1, 1, .78f * fade));
                painter.Style(sr, fill: col, fillAmount: 1, cut: keepBelow);
            }
            // 차오르는 선: 가로로 빛나는 수면
            if (fillT < 1)
            {
                float cx = (left + right) * .5f, half = (right - left) * .32f;
                painter.Line(Layer.Effects, new Vector2(cx - half, line), new Vector2(cx + half, line), 6, Painter.Alpha(col, .5f), additive: true);
                painter.Line(Layer.Effects, new Vector2(cx - half, line), new Vector2(cx + half, line), 2, Painter.Alpha(Color.Lerp(col, Color.white, .6f), .9f));
            }
            if (dt > 0 && Rand() < 30 * dt * fade)
            {
                var at = new Vector2(Mathf.Lerp(left, right, .3f + Rand() * .4f), Mathf.Lerp(bottom, line, Rand()));
                particles.Emit(ParticleKind.Glint, at, new Vector2((Rand() - .5f) * 10, -50 - Rand() * 40), .6f, Color.Lerp(col, Color.white, .3f), 2f + Rand() * 1.5f);
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

        // 바늘이 어디에 어떤 각도로 있는지: 날 때는 손 높이(flightLift)로 곧게, 떨어지면 diveTime 동안 바닥으로 수직 낙하해 박힌다.
        // 반환값: 바늘 끝(바닥에 닿는 점), 바늘 각도(라디안, 캔버스 기준), 바늘 귀(실이 이어지는 점)
        (Vector2 tip, float angle, Vector2 eye) NeedlePose(RunState r, Strand s, Vector2 ground, float length)
        {
            float lift = NV.flightLift;
            float flightAngle = Mathf.Atan2(s.vy * Space.Proj, s.vx);
            float stick = NV.stickAngle * Mathf.Deg2Rad;
            // 박힌 바늘은 날아온 쪽으로 살짝 기운다 (꼬리가 뒤로 남는다).
            float tilt = NV.stickTilt * Mathf.Deg2Rad * (s.vx >= 0 ? -1 : 1);
            float angle;
            Vector2 tip;
            if (s.state == StrandState.Flight)
            {
                angle = flightAngle;
                tip = ground - new Vector2(0, lift);
            }
            else
            {
                float age = r.time - s.landed;
                float k = NV.diveTime > 0 ? Mathf.Clamp01(age / NV.diveTime) : 1;
                float fall = k * k;                                   // 떨어질수록 빨라진다
                float quiver = Mathf.Sin(age * 46) * Mathf.Exp(-age * 7) * .42f;
                angle = Mathf.LerpAngle(flightAngle * Mathf.Rad2Deg, (stick + tilt) * Mathf.Rad2Deg, Mathf.SmoothStep(0, 1, k)) * Mathf.Deg2Rad + (k >= 1 ? quiver : 0);
                tip = ground - new Vector2(0, lift * (1 - fall)) + new Vector2(0, 2 * fall);   // 끝이 바닥에 조금 묻힌다
            }
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return (tip, angle, tip - dir * length * .9f);
        }

        // 박힌 자리의 원: 숨쉬듯 커졌다 작아지는 고리 + 가산 빛.
        void DrawStuckRing(RunState r, Strand s, Vector2 at, ElementTone t)
        {
            float alpha = NV.ringAlpha > 0 ? NV.ringAlpha : .74f;
            float radius = NV.ringRadius > 0 ? NV.ringRadius : 7;
            float width = NV.ringWidth > 0 ? NV.ringWidth : 1.15f;
            float age = r.time - s.landed;
            float appear = NV.diveTime > 0 ? Mathf.Clamp01((age - NV.diveTime) / .12f + 1) : 1;
            radius *= .6f + .4f * appear;
            radius += NV.ringPulse * Mathf.Sin(r.time * NV.ringPulseSpeed + s.birth * 7);
            if (NV.ringGlow > 0) painter.Ellipse(Layer.Ground, at, radius, radius * Space.Proj, Painter.Alpha(t.glow, alpha * .45f * appear), NV.ringGlow, additive: true);
            painter.Ellipse(Layer.Ground, at, radius, radius * Space.Proj, Painter.Alpha(t.color, alpha * appear), width);
        }

        void DrawThread(RunState r, Strand s, Vector2 hand, float dt)
        {
            var (start, end) = Combat.Endpoints(r.player, s);
            Vector2 from = s.anchor != null ? new Vector2(start.x, start.y) : hand;
            Element element = s.element ?? Element.Plain;
            ElementTone t = Tone(element);
            bool flight = s.state == StrandState.Flight;
            float speed = Mathf.Clamp01((s.speed > 0 ? s.speed : 1400) / 2700);
            float needleLength = flight ? 29 + speed * 14 : 24;      // 나는 바늘은 속도만큼 늘어난다 (모션 스미어)
            var pose = s.target == null ? NeedlePose(r, s, C(end), needleLength) : default;
            var a = C(from.x, from.y);
            var b = s.target != null ? C(end.x, end.y) - new Vector2(0, 23) : pose.eye;
            float seed = s.birth * 13;
            // 막 꽂힌 실은 퉁긴 현처럼 떨리다가 팽팽해진다.
            float twang = s.state == StrandState.Stuck ? 7 * Mathf.Exp(-(r.time - s.landed) * 9) : 0;
            // 굵은 실(재봉 조각 ThreadWidth): 당김 판정 반경과 같은 배율로 굵게
            List<Vector2> points = DrawStrand(Layer.Ground, a, b, element, seed, r.time, (s.anchor != null ? 1.45f : 1.7f) * threadScale, s.anchor != null ? .82f : 1, twang, flight);
            EmitAlong(points, element, element == Element.Frustration ? .5f : element == Element.Plain ? .06f : .16f, dt);
            float bead = (r.time * 2.65f + s.birth) % 1;
            Vector2 p = points[Mathf.FloorToInt(bead * (points.Count - 1))];
            painter.Rect(Layer.Ground, p.x - 1.2f, p.y - 1.2f, 2.6f, 2.6f, Painter.Alpha(t.core, .816f));
            if (s.state == StrandState.Ground)
            {
                bool planted = r.time - s.landed >= NV.diveTime;
                if (planted)
                {
                    DrawStuckRing(r, s, C(end), t);
                    if (element == Element.Despair)
                    {
                        float wave = (r.time * .8f + s.birth) % 1;
                        float radius = (NV.ringRadius > 0 ? NV.ringRadius : 7) + wave * 16;
                        painter.Ellipse(Layer.Ground, C(end), radius, radius * Space.Proj, Painter.Alpha(t.color, (1 - wave) * 120 / 255f), 1);
                    }
                }
                // 공중에서 떨어지는 동안에는 바닥에 그림자 점만 남는다.
                else painter.Ellipse(Layer.Ground, C(end), 4, 4 * Space.Proj, Pal.shadow);
                DrawNeedleAt(pose, needleLength, t.color);
            }
            if (flight)
            {
                float angle = pose.angle;
                Vector2 head = pose.tip;
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
                // 손 높이로 나는 바늘 아래 바닥에 작은 그림자를 둬서 높이를 읽히게 한다.
                painter.Ellipse(Layer.Ground, C(end), 4, 4 * Space.Proj, Pal.shadow);
                DrawNeedleAt(pose, needleLength, t.color);
            }
        }

        // NeedlePose 의 끝점 기준으로 바늘을 그린다 (DrawNeedle 은 바늘 몸통 기준점을 받는다).
        void DrawNeedleAt((Vector2 tip, float angle, Vector2 eye) pose, float length, Color color)
        {
            var dir = new Vector2(Mathf.Cos(pose.angle), Mathf.Sin(pose.angle));
            DrawNeedle(Layer.Ground, pose.tip - dir * length * .44f, pose.angle, length, color);
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
                        Vector2 spot = new(at.x, at.y - fx.lift - (1 - fade) * 34);
                        // 숫자는 그림 글꼴(아틀라스)로, 그림에 없는 글자가 섞이면 UI 글꼴로 쓴다.
                        if (!DrawGlyphs(db.visual.damageFont, fx.text, spot, scale, Painter.Alpha(color, fade)))
                            Text?.Label(fx.text, spot, 24, Painter.Alpha(color, fade), Painter.Alpha(Pal.textStroke, fade), 4, scale, 0, false);
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
                        if (!float.IsNaN(fx.lift)) at -= new Vector2(0, fx.lift);
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

        // 그림 글꼴로 한 줄을 가운데 정렬해 그린다. 글자 하나라도 그림에 없으면 false (호출한 쪽이 UI 글꼴로 대신 쓴다).
        bool DrawGlyphs(GlyphFont font, string text, Vector2 center, float scale, Color color)
        {
            if (font == null || font.glyphs == null || string.IsNullOrEmpty(font.chars) || string.IsNullOrEmpty(text)) return false;
            float h = font.height * scale;
            float total = 0;
            int count = 0;
            foreach (char ch in text)
            {
                int i = font.chars.IndexOf(ch);
                if (i < 0 || i >= font.glyphs.Length || font.glyphs[i] == null) return false;
                Rect r = font.glyphs[i].rect;
                total += h * r.width / r.height + (count++ > 0 ? font.spacing * h : 0);
            }
            float x = center.x - total / 2;
            foreach (char ch in text)
            {
                Sprite g = font.glyphs[font.chars.IndexOf(ch)];
                float w = h * g.rect.width / g.rect.height;
                painter.Sprite(Layer.Effects, g, new Vector2(x + w / 2, center.y), 0, w, h, color);
                x += w + font.spacing * h;
            }
            return true;
        }

        // 실이 칼날이 된다: 닻에서 대상을 지나 뻗는 빛의 칼날 → 두 가닥 머리카락 선으로 갈라지며 흩어진다.
        void DrawCut(Fx fx, float fade)
        {
            float t = 1 - fade;
            Color color = Tone(fx.element).color;
            Vector2 a = fx.followHero ? C(handNow.x, handNow.y) : C(fx.x, fx.y) - new Vector2(0, 8);
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

        // 처치된 적의 마지막 리그 모습(파츠)을 잡아 둔다 → 처치 조각·보스 처치 연출이 살아 있을 때와 같은 크기·발 위치·자세로 그린다.
        // (8차: 예전에는 통그림 type.sprite 를 키 h 로 그려, 리그보다 몸집이 커 보였다.) 뷰가 풀로 돌아가기 전에 불러야 한다.
        float threadScale = 1;   // 이번 프레임 실 굵기 배율 (Combat.ThreadScale)
        readonly Dictionary<Fx, List<EnemyPart>> sliceParts = new();
        readonly List<Fx> sliceStale = new();
        List<EnemyPart> bossParts;
        BossDeath bossPartsOwner;

        void CaptureDeaths(RunState r)
        {
            sliceStale.Clear();
            foreach (Fx fx in sliceParts.Keys) if (!r.effects.Contains(fx)) sliceStale.Add(fx);
            foreach (Fx fx in sliceStale) sliceParts.Remove(fx);
            if (r.bossDeath == null) { bossParts = null; bossPartsOwner = null; }
            foreach (KeyValuePair<Enemy, EnemyView> pair in enemyViews)
            {
                Enemy e = pair.Key;
                if (r.enemies.Contains(e)) continue;
                if (r.bossDeath != null && bossPartsOwner != r.bossDeath && e.type == r.bossDeath.type)
                {
                    bossParts = pair.Value.Snapshot();
                    bossPartsOwner = r.bossDeath;
                }
                foreach (Fx fx in r.effects)
                {
                    if (fx.kind != FxKind.Sliced || fx.type != e.type || sliceParts.ContainsKey(fx)) continue;
                    if (Mathf.Abs(fx.x - e.x) + Mathf.Abs(fx.y - e.y) > 40) continue;
                    sliceParts[fx] = pair.Value.Snapshot();
                    break;
                }
            }
        }

        // 적 몸 한 벌: 파츠 모습이 있으면 파츠마다, 없으면 통그림 한 장. anchor(통그림 피벗) 기준으로 rot 만큼 돌리고 offset 만큼 옮긴다.
        void DrawBody(Layer layer, List<EnemyPart> parts, Sprite art, Vector2 foot, Vector2 anchor, Vector2 offset, float rot,
            float w, float h, Color color, float bright, float sat, Vector4? cut, Vector4? cut2)
        {
            if (parts == null || parts.Count == 0)
            {
                SpriteRenderer sr = painter.Sprite(layer, art, anchor + offset, rot, w, h, color);
                painter.Style(sr, bright: bright, sat: sat, cut: cut, cut2: cut2);
                return;
            }
            foreach (EnemyPart part in parts)
            {
                Vector2 at = anchor + offset + Rotate(foot + part.at - anchor, rot);
                SpriteRenderer sr = painter.Sprite(layer, part.sprite, at, part.angle + rot, part.w, part.h, color);
                painter.Style(sr, bright: bright, sat: sat, fill: part.shade, fillAmount: part.shade.a, cut: cut, cut2: cut2);
            }
        }

        // 처치된 적이 절단선을 따라 두 조각으로 떨어진다.
        void DrawSliced(Fx fx, float fade)
        {
            if (sliceParts.TryGetValue(fx, out List<EnemyPart> parts) && parts.Count > 0) { DrawSlicedParts(fx, fade, parts); return; }
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

        // DrawSliced 와 같은 움직임을 캔버스 공간에서 (파츠 모습은 좌우 뒤집힘이 이미 들어 있으므로 법선·회전 방향에 facing 을 곱한다).
        void DrawSlicedParts(Fx fx, float fade, List<EnemyPart> parts)
        {
            float t = 1 - fade;
            float h = fx.type.height * (fx.elite ? db.enemies.eliteHeight : 1);
            Vector2 d = new(Mathf.Cos(fx.angle), Mathf.Sin(fx.angle));
            Vector2 n = new Vector2(-d.y, d.x) * fx.facing;
            Vector2 foot = new(fx.x, Space.ProjectY(fx.y));
            Color color = new(1, 1, 1, fade < .35f ? fade / .35f : 1);
            foreach (float s in new[] { 1f, -1f })
            {
                float spread = 8 + 26 * (1 - Mathf.Pow(1 - t, 2.5f)) * (s == fx.side ? 1.3f : .8f);
                float drop = 30 * t * t;
                Vector2 offset = n * s * spread + new Vector2(d.x * s * spread * .5f, drop);
                float rot = fx.facing * s * t * .35f;
                Vector2 c = foot + offset + Rotate(new Vector2(0, -h / 2), rot);
                Vector2 normal = Rotate(n * s, rot);
                Vector3 cw = Space.Canvas(c);
                Vector2 nw = new Vector2(normal.x, -normal.y).normalized;
                DrawBody(Layer.Effects, parts, null, foot, foot, offset, rot, 0, 0, color,
                    t < .12f ? 2.4f : .85f, t < .12f ? .3f : .7f, new Vector4(nw.x, nw.y, Vector2.Dot(nw, cw), 1), null);
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
            // 손에 든 실은 당긴 순간의 자리가 아니라 지금 손 위치로 감겨 든다 (맵에 실이 남지 않게).
            Vector2 a = fx.followHero ? C(handNow.x, handNow.y) : C(fx.x, fx.y) - new Vector2(0, 6);
            Vector2 b = C(fx.toX, fx.toY) - new Vector2(0, 18);
            ElementTone tone = Tone(fx.rage ? Element.Chaos : fx.element);
            float delay = Mathf.Clamp(NV.pullSnapDelay, 0, .95f);
            float snap = Mathf.Clamp01((t - delay) / (1 - delay));
            float pulled = Mathf.Pow(snap, NV.pullSnapPower > 0 ? NV.pullSnapPower : 2.4f);
            Vector2 lead = b + (a - b) * pulled;
            float tension = (1 - snap) * 3.2f;
            DrawStrand(Layer.Effects, a, lead, fx.element, fx.seed, fx.time * 3 + fx.seed, (2.6f + 2.8f * fade) * threadScale, Mathf.Min(1, fade * 1.6f), tension, false);
            if (snap > 0 && snap < 1)
            {
                float radius = 3 + 4 * (1 - snap);
                painter.Ellipse(Layer.Effects, lead, radius, radius, tone.core, additive: true);
                if (Rand() < .6f) particles.Emit(ParticleKind.Spark, lead, new Vector2((Rand() - .5f) * 120, (Rand() - .5f) * 120), .18f, tone.color, 1.6f);
            }
            if (t > .9f)
            {
                float radius = 10 + t * 26;
                painter.Ellipse(Layer.Effects, fx.followHero ? a : C(fx.x, fx.y), radius, radius * Space.Proj, Painter.Alpha(tone.color, .53f), 1.5f);
            }
        }

        // 대쉬 잔상: 생긴 순간의 파츠 자세를 같은 하늘색 실루엣으로 그리며 옅어지고 살짝 떠오른다.
        readonly Dictionary<Fx, List<PartSnap>> ghosts = new();
        readonly List<Fx> ghostStale = new();

        void DrawAfterimages(RunState r)
        {
            ghostStale.Clear();
            foreach (Fx fx in ghosts.Keys) if (!r.effects.Contains(fx)) ghostStale.Add(fx);
            foreach (Fx fx in ghostStale) ghosts.Remove(fx);
            foreach (Fx fx in r.effects)
            {
                if (fx.kind != FxKind.Afterimage) continue;
                if (!ghosts.TryGetValue(fx, out List<PartSnap> parts)) ghosts[fx] = parts = hero.Snapshot();
                float fade = fx.Fade;
                Vector2 rise = new(0, -(1 - fade) * 4);
                foreach (PartSnap part in parts)
                {
                    SpriteRenderer solid = painter.Sprite(Layer.Ground, part.sprite, part.center + rise, part.angle, part.w, part.h, new Color(1, 1, 1, fade * .5f));
                    painter.Style(solid, fill: db.visual.dodgeColor, fillAmount: 1);
                }
                foreach (PartSnap part in parts)
                {
                    SpriteRenderer glow = painter.Sprite(Layer.Ground, part.sprite, part.center + rise, part.angle, part.w * 1.04f, part.h * 1.04f, new Color(1, 1, 1, fade * .3f), additive: true);
                    painter.Style(glow, fill: db.visual.dodgeColor, fillAmount: 1);
                }
            }
        }
    }
}
