// 16차 속성 그리기: 상성 글자, 부가 세트 발동, 보스 속성 공격, 주인공 상태, 층 속성 장식.
// 값은 db.elements(ElementConfig). Blender 그림(FlipClip)이 아직 없으면 ElementTone.hit 으로 대신 그린다.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public partial class ArenaRenderer
    {
        ElementConfig EC => db.elements;

        // 피해 숫자 오른쪽에 "약점" / "저항" 작은 글자
        void DrawAffinityTag(int relation, Vector2 spot, float textWidth, float scale, float fade)
        {
            AffinityConfig a = EC?.affinity;
            if (a == null || Text == null) return;
            string text = relation > 0 ? a.strongTag : a.weakTag;
            if (string.IsNullOrEmpty(text)) return;
            Color color = relation > 0 ? a.strongColor : a.weakColor;
            float px = a.tagPx > 0 ? a.tagPx : 14;
            Vector2 at = new(spot.x + textWidth / 2 + a.tagOffset.x + px * text.Length * .5f, spot.y + a.tagOffset.y);
            Text.Label(text, at, px, Painter.Alpha(color, fade), Painter.Alpha(Pal.textStroke, fade), 3, Mathf.Max(.8f, scale * .9f), 0, false);
        }

        static int ClipIndex(FlipClip clip, float elapsed, float progress, bool loop)
        {
            int n = clip.frames.Length;
            int i = clip.fps > 0 ? Mathf.FloorToInt(elapsed * clip.fps) : Mathf.FloorToInt(progress * n);
            return loop ? ((i % n) + n) % n : Mathf.Clamp(i, 0, n - 1);
        }

        // 플립북 한 칸: 빛(가산)을 먼저, 본체(보통 합성)를 위에. at = 그림 피벗 자리, size = 칸 높이 (캔버스 px).
        void DrawClip(FlipClip clip, Vector2 at, float size, float elapsed, float progress, bool loop, float alpha, float angle = 0, Layer layer = Layer.Effects, bool flip = false)
        {
            int i = ClipIndex(clip, elapsed, progress, loop);
            Sprite body = clip.frames[i];
            if (body == null) return;
            float w = size * body.rect.width / Mathf.Max(1, body.rect.height) * (flip ? -1 : 1);
            if (clip.glow != null && clip.glow.Length > 0)
            {
                Sprite g = clip.glow[Mathf.Min(i, clip.glow.Length - 1)];
                if (g != null) painter.Sprite(layer, g, at, angle, w, size, Painter.Alpha(clip.tint, alpha), additive: true);
            }
            painter.Sprite(layer, body, at, angle, w, size, Painter.Alpha(clip.tint, alpha));
        }

        // 21차 증강 성장: 몸 뒤 층(Ground, 주인공 뒤) + 몸 앞 층(Effects), 끝 무렵 머리 위로 오른 능력치 글자가 떠오른다
        void DrawGrowth(RunState r, Fx fx, float fade)
        {
            VisualConfig v = db.visual;
            float age = 1 - fade;
            Vector2 foot = fx.followHero ? C(r.player.x, r.player.y) : C(fx.x, fx.y);
            float size = v.growthSize > 0 ? v.growthSize : 260;
            Vector2 at = foot - new Vector2(0, size * (v.growthFoot > 0 ? v.growthFoot : .3f));
            if (v.growthBack != null && v.growthBack.Has) DrawClip(v.growthBack, at, size, age * fx.max, age, false, fx.alpha, 0, Layer.Ground);
            if (v.growthFront != null && v.growthFront.Has) DrawClip(v.growthFront, at, size, age * fx.max, age, false, fx.alpha);
            if (string.IsNullOrEmpty(fx.text) || Text == null) return;
            float start = v.growthTextAt > 0 ? v.growthTextAt : .55f;
            if (age < start) return;
            float k = (age - start) / Mathf.Max(.05f, 1 - start);
            float pop = k < .15f ? .6f + EaseOutBack(k / .15f) * .4f : 1;
            float alpha = 1 - Mathf.Clamp01((k - .7f) / .3f);
            Vector2 spot = foot - new Vector2(0, (v.growthTextLift > 0 ? v.growthTextLift : 130) + v.growthTextRise * (1 - (1 - k) * (1 - k)));
            Color color = v.growthTextColor.a > 0 ? v.growthTextColor : new Color(1, .9f, .55f);
            Text.Label(fx.text, spot, v.growthTextSize > 0 ? v.growthTextSize : 22, Painter.Alpha(color, alpha), Painter.Alpha(Pal.textStroke, alpha), 4, pop, 0, false);
        }

        Vector2 FxAt(Fx fx) => fx.follow != null ? C(fx.follow) : C(fx.x, fx.y);

        // 부가 세트 발동: Blender 이펙트(procFx) 또는 속성 그림을 크게 + 퍼지는 고리 + 머리 위 작은 글자
        void DrawProc(Fx fx, float fade)
        {
            ElementTone tone = Tone(fx.element);
            ElementDef def = EC?.Def(fx.element);
            float age = 1 - fade;
            Vector2 c = FxAt(fx) - new Vector2(0, float.IsNaN(fx.lift) ? 30 : fx.lift);
            float big = fx.size > 0 ? fx.size : 1;
            if (def?.procFx != null && def.procFx.Has)
                DrawClip(def.procFx, c, (def.procFx.size > 0 ? def.procFx.size : 160) * big, age * fx.max, age, false, 1);
            else if (tone.hit != null)
            {
                float s = (tone.hitSize > 0 ? tone.hitSize : 90) * (EC.procHitScale > 0 ? EC.procHitScale : 1.8f) * big
                    * (.5f + .7f * (1 - Mathf.Pow(1 - Mathf.Min(1, age * 3.5f), 3)));
                float spin = (Hash(fx.seed) - .5f) + age * 1.6f;
                painter.Sprite(Layer.Effects, tone.hit, c, spin, s * 1.35f, s * 1.35f, Painter.Alpha(tone.glow, .55f * fade), additive: true);
                painter.Sprite(Layer.Effects, tone.hit, c, -spin * .6f, s, s, Painter.Alpha(tone.color, fade), additive: true);
                painter.Sprite(Layer.Effects, tone.hit, c, spin * 1.7f, s * .55f, s * .55f, Painter.Alpha(tone.core, fade), additive: true);
            }
            // 바닥에 퍼지는 속성 고리 두 겹
            Vector2 ground = FxAt(fx);
            for (int k = 0; k < 2; k++)
            {
                float t = Mathf.Clamp01(age * 1.6f - k * .18f);
                if (t <= 0) continue;
                float radius = (20 + 70 * (1 - Mathf.Pow(1 - t, 2))) * big;
                painter.Ellipse(Layer.Effects, ground, radius, radius * Space.Proj, Painter.Alpha(k == 0 ? tone.color : tone.glow, (1 - t) * .9f), 3 - k, additive: true);
            }
            if (effectsDt > 0 && age < .35f)
                for (int i = 0; i < 3; i++)
                {
                    float a = Rand() * Mathf.PI * 2;
                    particles.Emit(ParticleKind.Spark, c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (120 + Rand() * 160), .35f + Rand() * .2f, Rand() < .5f ? tone.color : tone.core, 2 + Rand() * 2);
                }
            if (!string.IsNullOrEmpty(fx.text) && Text != null)
            {
                float pop = age < .15f ? .5f + EaseOutBack(age / .15f) * .5f : 1;
                Text.Label(fx.text, c - new Vector2(0, 46 + age * 18), 18, Painter.Alpha(Color.Lerp(tone.color, Color.white, .35f), fade), Painter.Alpha(Pal.textStroke, fade), 4, pop, 0, false);
            }
        }

        // 보스 속성 공격이 주인공에게 맞음: Blender 이펙트(boss.fx) 또는 속성 그림
        void DrawBossHit(Fx fx, float fade)
        {
            ElementTone tone = Tone(fx.element);
            BossElementAttack a = EC?.Def(fx.element)?.boss;
            float age = 1 - fade;
            Vector2 c = FxAt(fx) - new Vector2(0, 34);
            float size = fx.size > 0 ? fx.size : 120;
            if (a?.fx != null && a.fx.Has)
            {
                // 바닥 피벗 시트: 발밑에 놓는다 (감전 연쇄처럼 작게 띄운 것은 fx.size 비율로 줄인다)
                float s = (a.fx.size > 0 ? a.fx.size : size) * size / Mathf.Max(1, EC.bossHitSize);
                DrawClip(a.fx, FxAt(fx), s, age * fx.max, age, false, 1);
                return;
            }
            if (tone.hit == null) return;
            float g = size * (.5f + .6f * (1 - Mathf.Pow(1 - Mathf.Min(1, age * 3), 3)));
            float spin = (Hash(fx.seed) - .5f) * 1.4f + age;
            painter.Sprite(Layer.Effects, tone.hit, c, spin, g * 1.3f, g * 1.3f, Painter.Alpha(tone.glow, .5f * fade), additive: true);
            painter.Sprite(Layer.Effects, tone.hit, c, -spin, g, g, Painter.Alpha(tone.color, fade), additive: true);
        }

        // 주인공 상태: 속성 입자 + 눈부심(화면이 하얗게 번짐) + 어둠(주인공 둘레만 보임)
        void DrawPlayerStatus(RunState r, Player p, float dt)
        {
            if (EC == null) return;
            Vector2 foot = C(p);
            void Emit(Element el, ParticleKind kind, Vector2 v, float rate, float life, float size)
            {
                if (dt <= 0 || Rand() >= rate * dt) return;
                ElementTone t = Tone(el);
                particles.Emit(kind, foot + new Vector2((Rand() - .5f) * 30, -Rand() * 60), v, life, Rand() < .5f ? t.color : t.core, size);
            }
            float rate = db.visual.statusRate > 0 ? db.visual.statusRate * 1.5f : 20;
            if (p.burnTime > 0) Emit(Element.Wrath, ParticleKind.Ember, new Vector2((Rand() - .5f) * 20, -50 - Rand() * 40), rate, .6f, 2.4f);
            if (p.slowTime > 0) Emit(Element.Sloth, ParticleKind.Glint, new Vector2((Rand() - .5f) * 10, 14), rate, .8f, 2f);
            if (p.poisonTime > 0) Emit(Element.Envy, ParticleKind.Drop, new Vector2((Rand() - .5f) * 8, -24), rate, .8f, 2f);
            if (p.shockLeft > 0) Emit(Element.Unused3, ParticleKind.Spark, new Vector2((Rand() - .5f) * 160, (Rand() - .5f) * 160), rate * 1.5f, .2f, 2f);
            if (p.dark > 0) Emit(Element.Greed, ParticleKind.Drop, new Vector2((Rand() - .5f) * 14, -20), rate, 1f, 3f);
            if (p.slowTime > 0) painter.Ellipse(Layer.Ground, foot, 26, 26 * Space.Proj, Painter.Alpha(Tone(Element.Sloth).glow, .35f), 2, additive: true);
            // 화상, 중독 동안 발밑 웅덩이 (Blender boss_fire_pool, boss_poison_pool)
            foreach (Element el in new[] { Element.Wrath, Element.Envy })
            {
                float left = el == Element.Wrath ? p.burnTime : p.poisonTime;
                FlipClip pool = EC.Def(el)?.boss?.pool;
                if (left <= 0 || pool == null || !pool.Has) continue;
                DrawClip(pool, foot, pool.size > 0 ? pool.size : 140, renderClock, 0, true, Mathf.Clamp01(left / .4f), 0, Layer.Ground);
            }

            // 어둠: 주인공 둘레 darkRadius 만 보이고 바깥은 어둡다 (가운데 뚫린 그림 + 화면 나머지 덮개). 들어올 때·끝날 때 부드럽게.
            if (p.dark > 0 && p.darkMax > 0)
            {
                float k = Mathf.Clamp01(Mathf.Min(p.dark, p.darkMax - p.dark) / .35f);
                float radius = Mathf.Lerp(Mathf.Max(view.width, view.height), Mathf.Max(60, p.darkRadius), k);
                Color dark = EC.darkColor;
                Vector2 c = foot - new Vector2(0, 30);
                if (EC.visionMask != null)
                {
                    float d = radius * 2;
                    painter.Sprite(Layer.Overlay, EC.visionMask, c, 0, d, d, dark);
                    // 그림 바깥 화면
                    void Cover(float x, float y, float w, float h) { if (w > 0 && h > 0) painter.Rect(Layer.Overlay, x, y, w, h, dark); }
                    Cover(view.xMin - 10, view.yMin - 10, view.width + 20, c.y - radius - view.yMin + 10);
                    Cover(view.xMin - 10, c.y + radius, view.width + 20, view.yMax - c.y - radius + 10);
                    Cover(view.xMin - 10, c.y - radius, c.x - radius - view.xMin + 10, d);
                    Cover(c.x + radius, c.y - radius, view.xMax - c.x - radius + 10, d);
                }
                else painter.Rect(Layer.Overlay, view.xMin, view.yMin, view.width, view.height, Painter.Alpha(dark, dark.a * .6f * k));
            }
            // 눈부심: 맞은 순간 하얗게 번졌다가 천천히 걷힌다
            if (p.blind > 0 && p.blindMax > 0)
            {
                float u = p.blind / p.blindMax;
                float power = p.blindPower > 0 ? p.blindPower : EC.blindAlpha;
                Color w = EC.blindColor;
                painter.Rect(Layer.Overlay, view.xMin - 10, view.yMin - 10, view.width + 20, view.height + 20, Painter.Alpha(w, power * Mathf.Pow(u, .7f)));
                if (softSprite != null)
                    painter.Sprite(Layer.Overlay, softSprite, foot - new Vector2(0, 30), 0, 420, 420, Painter.Alpha(w, power * u), additive: true);
            }
        }

        // ---------------------------------------------------------------- 층 속성 장식

        struct LiveDecor { public AnimatedDecor def; public Vector2 at; public float scale, phase; public bool flip; }

        // 보스 탄: Blender 탄 그림이 있으면 나아가는 쪽으로 돌려 그린다 (없으면 false = 둥근 탄)
        bool DrawBossBullet(Bullet b)
        {
            FlipClip clip = b.boss != null ? EC?.Def(b.element)?.boss?.bullet : null;
            if (clip == null || !clip.Has) return false;
            float angle = Mathf.Atan2(b.vy * Space.Proj, b.vx);
            DrawClip(clip, C(b) - new Vector2(0, 14), clip.size > 0 ? clip.size : 44, b.age + b.x * .001f, 0, true, b.spent ? .4f : 1, angle, Layer.Ground);
            return true;
        }

        // 결박 폭발: 어둠, 피, 바람, 독은 Blender bind_<속성>_burst (발밑). 그렸으면 true (기본 빛살 모양 대신)
        bool DrawBindClip(Fx fx, float fade)
        {
            FlipClip clip = EC?.Def(fx.rage ? Element.Wrath : fx.element)?.bindBurst;
            if (clip == null || !clip.Has) return false;
            float age = 1 - fade;
            Vector2 foot = fx.follow != null ? C(fx.follow) : C(fx.x, fx.y);
            float s = (clip.size > 0 ? clip.size : 260) * (fx.size > 0 && db.bind.burst.size > 0 ? fx.size / db.bind.burst.size : 1);
            DrawClip(clip, foot, s, age * fx.max, age, false, 1);
            return true;
        }
        readonly List<LiveDecor> liveDecor = new();

        // 라운드마다: 층 속성의 정적 소품을 일반 장식 목록(decor)에 더하고, 움직이는 장식을 따로 흩는다.
        void ScatterElementDecor(RunState r, int seed)
        {
            liveDecor.Clear();
            ElementDef def = ElementLook.Floor(db, r);
            if (def == null || EC == null) return;
            DecorConfig d = db.decor;
            var rng = new System.Random(seed ^ 0x16E1E);
            ArenaConfig a = db.arena;
            float margin = d != null ? d.margin : 80, spacing = d != null ? d.spacing : 200, clear = d != null ? d.clearRadius : 260;
            float x0 = a.wallLeft + margin, x1 = a.wallRight - margin, y0 = a.wallTop + margin, y1 = a.wallBottom - margin;
            float area = (x1 - x0) * (y1 - y0) / 1e6f;
            var start = new Vector2(r.player.x, r.player.y);
            Vector2 scaleRange = d != null ? d.scaleRange : Vector2.one;

            bool Free(Vector2 at)
            {
                if (Vector2.Distance(at, start) < clear) return false;
                foreach (Decor o in decor) if (Vector2.Distance(o.at, at) < spacing) return false;
                foreach (LiveDecor o in liveDecor) if (Vector2.Distance(o.at, at) < spacing) return false;
                return true;
            }
            Vector2 Somewhere() => new(x0 + (float)rng.NextDouble() * (x1 - x0), y0 + (float)rng.NextDouble() * (y1 - y0));
            float Scale() { float s = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble()); return s <= 0 ? 1 : s; }

            // 정적 소품 (Codex decor_<속성>_*.png)
            var props = new List<DecorDef>();
            if (def.props != null) foreach (DecorDef p in def.props) if (p != null && p.sprite != null) props.Add(p);
            int want = props.Count > 0 ? Mathf.RoundToInt(area * EC.propDensity) : 0;
            for (int tries = 0, placed = 0; placed < want && tries < want * 30; tries++)
            {
                Vector2 at = Somewhere();
                if (!Free(at)) continue;
                DecorDef pick = Pick(props, p => p.weight, rng);
                if (pick.height <= 0 && EC.propHeight > 0)
                    pick = new DecorDef { sprite = pick.sprite, height = EC.propHeight, weight = pick.weight, flat = pick.flat };
                decor.Add(new Decor { def = pick, at = at, scale = Scale(), flip = rng.NextDouble() < .5 });
                placed++;
            }
            // 움직이는 장식 (Blender 플립북)
            var anim = new List<AnimatedDecor>();
            if (def.animated != null) foreach (AnimatedDecor p in def.animated) if (p?.clip != null && p.clip.Has) anim.Add(p);
            want = anim.Count > 0 ? Mathf.RoundToInt(area * EC.animatedDensity) : 0;
            for (int tries = 0; liveDecor.Count < want && tries < want * 30; tries++)
            {
                Vector2 at = Somewhere();
                if (!Free(at)) continue;
                liveDecor.Add(new LiveDecor { def = Pick(anim, p => p.weight, rng), at = at, scale = Scale(), phase = (float)rng.NextDouble() * 10, flip = rng.NextDouble() < .5 });
            }
        }

        static T Pick<T>(List<T> list, System.Func<T, float> weight, System.Random rng)
        {
            float total = 0;
            foreach (T t in list) total += Mathf.Max(0, weight(t));
            float pick = (float)rng.NextDouble() * total;
            foreach (T t in list) { pick -= Mathf.Max(0, weight(t)); if (pick <= 0) return t; }
            return list[list.Count - 1];
        }

        void DrawElementDecor(RunState r, float clock)
        {
            foreach (LiveDecor o in liveDecor)
            {
                FlipClip clip = o.def.clip;
                float h = (clip.size > 0 ? clip.size : 120) * o.scale;
                Vector2 at = C(o.at.x, o.at.y);
                if (at.x + h < view.xMin || at.x - h > view.xMax || at.y + h < view.yMin || at.y - h * 1.2f > view.yMax) continue;   // 화면 밖
                DrawClip(clip, at, h, clock + o.phase, 0, true, o.def.alpha > 0 ? o.def.alpha : 1, 0, Layer.Ground, o.flip);
            }
        }
    }
}
