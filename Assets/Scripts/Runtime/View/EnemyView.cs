// 적 한 마리 (웹판 render.js drawEnemy). 템플릿 계층(MCP로 생성):
//  Enemy(SortingGroup)/Shadow, Ring, HpBack, HpFill, Pins/Pin0..3/{Stroke,Needle},
//  Body/Center/Rot/Squash/Unrot/Back/Flip/{BodyA,BodyB}  ← 당김 방향 늘이기 + 좌우 뒤집기 + 두 동강
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public class EnemyView : MonoBehaviour
    {
        public SpriteRenderer shadow, ring, hpBack, hpFill;
        public Transform body, center, rot, squash, unrot, back, flip;
        public SpriteRenderer bodyA, bodyB;
        public SpriteRenderer[] pinStrokes, pinNeedles;

        MaterialPropertyBlock block;
        static readonly int BrightId = Shader.PropertyToID("_Bright"), SatId = Shader.PropertyToID("_Sat"), CutId = Shader.PropertyToID("_CutPlane"),
            ShapeId = Shader.PropertyToID("_Shape"), RingId = Shader.PropertyToID("_Ring");

        void Ensure() => block ??= new MaterialPropertyBlock();

        static Vector3 Local(float px, float py) => new(px / Space.Ppu, -py / Space.Ppu, 0);

        void Shape(SpriteRenderer sr, float shape, float ringAmount = 0)
        {
            block.Clear();
            block.SetFloat(ShapeId, shape);
            block.SetFloat(RingId, ringAmount);
            sr.SetPropertyBlock(block);
        }

        // 바닥 타원을 발밑 기준 로컬(캔버스 px)로 놓는다.
        static void Ellipse(SpriteRenderer sr, float cx, float cy, float rx, float ry)
        {
            sr.transform.localPosition = Local(cx, cy);
            float w = sr.sprite.rect.width;
            sr.transform.localScale = new Vector3(rx * 2 / w, ry * 2 / w, 1);
        }

        public void Render(Enemy e, float time, List<Strand> attached, GameDatabase db, float dt)
        {
            Ensure();
            RenderPalette pal = db.palette;
            float y = Space.ProjectY(e.y);
            transform.position = Space.Ground(e.x, e.y);
            Sprite art = e.type.sprite;
            float h = e.height;
            float w = h * (art.rect.width / art.rect.height);
            float bob = Mathf.Sin(Mathf.Floor(time * 12) / 12 * 8 + e.x) * (e.action == EnemyAction.Move ? 2 : .5f);
            bool boss = e.IsBoss;

            Ellipse(shadow, 0, 3, e.radius, e.radius * .34f);
            shadow.color = pal.shadow;
            Shape(shadow, 1);
            ring.gameObject.SetActive(e.elite || boss);
            if (e.elite || boss)
            {
                float r = e.radius + 5 + 1;
                Ellipse(ring, 0, 0, r, r * Space.Proj);
                ring.color = e.elite ? pal.eliteRing : pal.bossRing;
                Shape(ring, 2, 2 / r);
            }

            // 몸통: 흔들림 + 박힘 떨림 → 당김 방향 늘이기(몸 중심 기준) → 좌우·예고 웅크림
            Vector2 offset = new(0, bob);
            if (e.jolt != null) { float k = e.jolt.time / .12f; offset += new Vector2(e.jolt.x * 5 * k, e.jolt.y * Space.Proj * 5 * k); }
            body.localPosition = Local(offset.x, offset.y);
            center.localPosition = Local(0, -h / 2);
            back.localPosition = Local(0, h / 2);
            float stretchAngle = 0, sx = 1, sy = 1;
            if (e.stretch != null)
            {
                float k = e.stretch.time / db.feel.stretchTime;
                float pulse = Mathf.Sin(Mathf.PI * Mathf.Min(1, (1 - k) * 1.6f + .25f)) * k;
                stretchAngle = Mathf.Atan2(e.stretch.y, e.stretch.x);
                sx = 1 + .34f * pulse; sy = 1 - .2f * pulse;
            }
            rot.localRotation = Quaternion.Euler(0, 0, Space.Degrees(stretchAngle));
            squash.localScale = new Vector3(sx, sy, 1);
            unrot.localRotation = Quaternion.Euler(0, 0, -Space.Degrees(stretchAngle));
            flip.localScale = new Vector3(e.facing, e.action == EnemyAction.WindupMelee ? .84f : 1, 1);

            foreach (SpriteRenderer half in new[] { bodyA, bodyB })
            {
                half.sprite = art;
                half.transform.localScale = new Vector3(w / art.rect.width, h / art.rect.height, 1);
            }
            float bright = e.flash > .15f ? 2.6f : e.flash > 0 ? 1.9f : 1;
            float sat = e.flash > .15f ? .4f : 1;
            if (e.cut != null)
            {
                // 실 따라 두 동강: 임팩트 순간 벌어졌다가(히트스톱 동안 유지) 다시 붙는다.
                float k = Mathf.Pow(e.cut.time / e.cut.max, 2);
                float angle = e.facing > 0 ? e.cut.angle : Mathf.PI - e.cut.angle;
                Halves(angle, 7 * k, e.cut.side, h, bright, sat);
            }
            else
            {
                bodyB.gameObject.SetActive(false);
                bodyA.transform.localPosition = Vector3.zero;
                block.Clear();
                block.SetFloat(BrightId, bright);
                block.SetFloat(SatId, sat);
                bodyA.SetPropertyBlock(block);
            }

            bool showHp = !boss && (e.hp < e.maxHp || (attached != null && attached.Count > 0));
            hpBack.gameObject.SetActive(showHp);
            hpFill.gameObject.SetActive(showHp);
            if (showHp)
            {
                SetRect(hpBack, -16, -h - 7, 32, 3, pal.hpBack);
                SetRect(hpFill, -16, -h - 7, 32 * e.hp / e.maxHp, 3, pal.hpFill);
            }

            int pins = attached == null ? 0 : Mathf.Min(4, attached.Count);
            for (int i = 0; i < pinNeedles.Length; i++)
            {
                bool on = i < pins;
                pinNeedles[i].gameObject.SetActive(on);
                pinStrokes[i].gameObject.SetActive(on);
                if (!on) continue;
                Strand s = attached[i];
                float o = (i - (pins - 1) / 2f) * 7;
                ElementTone tone = db.Tone(s.element ?? Element.Plain);
                // 박힘: 바늘이 표면을 지나쳐 들어갔다가 단단한 것에 맞은 듯 튕겨 나온다.
                float age = Mathf.Max(0, time - s.landed);
                float bite = 8 * Mathf.Exp(-age * 13) * Mathf.Cos(age * 34);
                Vector2 at = new(o + s.bite.x * bite, -20 - (i % 2) * 5 + s.bite.y * bite);
                Needle(pinStrokes[i], pinNeedles[i], at, 1.02f + o * .04f, 16, tone.color);
            }
        }

        void Halves(float angle, float gap, float side, float h, float bright, float sat)
        {
            bodyB.gameObject.SetActive(true);
            Vector2 d = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 n = new(-d.y, d.x);
            // 절단선: Flip 공간의 몸 중심(0,-h/2)을 지나는 직선 (캔버스 로컬 → Unity 로컬)
            Vector3 c = flip.TransformPoint(Local(0, -h / 2));
            Vector3 dirWorld = flip.TransformDirection(new Vector3(d.x, -d.y, 0));
            Vector2 nWorld = new Vector2(-dirWorld.y, dirWorld.x).normalized;
            Vector2 nLocalUnity = new(n.x, -n.y);
            Vector3 nTest = flip.TransformDirection(nLocalUnity);
            if (Vector2.Dot(nWorld, nTest) < 0) nWorld = -nWorld;   // 캔버스 법선 n 쪽을 +로 맞춘다
            int index = 0;
            foreach (float s in new[] { 1f, -1f })
            {
                SpriteRenderer half = index++ == 0 ? bodyA : bodyB;
                float push = gap * (s == side ? 1.25f : .6f);
                half.transform.localPosition = Local(n.x * s * push + d.x * s * push * .35f, n.y * s * push + d.y * s * push * .35f);
                Vector2 normal = nWorld * s;
                block.Clear();
                block.SetFloat(BrightId, bright);
                block.SetFloat(SatId, sat);
                block.SetVector(CutId, new Vector4(normal.x, normal.y, Vector2.Dot(normal, (Vector2)c), 1));
                half.SetPropertyBlock(block);
            }
        }

        static void SetRect(SpriteRenderer sr, float x, float y, float w, float h, Color color)
        {
            float px = sr.sprite.rect.width;
            sr.transform.localPosition = Local(x + w / 2, y + h / 2);
            sr.transform.localScale = new Vector3(w / px, h / px, 1);
            sr.color = color;
        }

        // 바늘 한 자루: 색 실 한 줄 + 바늘 그림 (웹판 drawNeedle)
        public static void Needle(SpriteRenderer stroke, SpriteRenderer needle, Vector2 at, float angle, float length, Color color)
        {
            Quaternion q = Quaternion.Euler(0, 0, Space.Degrees(angle));
            Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
            float px = stroke.sprite.rect.width;
            stroke.transform.localPosition = Local(at.x + dir.x * -.15f * length, at.y + dir.y * -.15f * length);
            stroke.transform.localRotation = q;
            stroke.transform.localScale = new Vector3(1.2f * length / px, 1.35f / px, 1);
            stroke.color = color;
            Rect r = needle.sprite.rect;
            needle.transform.localPosition = Local(at.x + dir.x * -.06f * length, at.y + dir.y * -.06f * length);
            needle.transform.localRotation = q;
            needle.transform.localScale = new Vector3(length / r.width, 5.2f / r.height, 1);
        }
    }
}
