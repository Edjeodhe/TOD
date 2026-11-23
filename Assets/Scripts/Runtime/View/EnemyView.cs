// 적 한 마리 (웹판 render.js drawEnemy). 템플릿 계층(MCP로 생성):
//  Enemy(SortingGroup)/Shadow, Ring, HpBack, HpFill, Pins/Pin0..3/{Stroke,Needle},
//  Body/Center/Rot/Squash/Unrot/Back/Flip/{BodyA,BodyB}  ← 당김 방향 늘이기 + 좌우 뒤집기 + 두 동강
// 몸은 종류별 뼈대 리그(MonsterRig 템플릿 = Arena/Enemies/Rigs/<종류>)를 Flip 아래로 복제해 그린다.
// 실에 베이는 동안(e.cut)에도 리그 그대로 두 조각으로 그린다: 리그 + 같은 자세를 베낀 쌍둥이 리그에 서로 반대쪽 절단면.
// (8차: 예전에는 통그림 type.sprite 로 바꿔 그려 몸집·자세가 달라져 커 보였다.) 리그가 없는 종류만 통그림 두 조각.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    // 리그 파츠 한 장의 지금 모습 (발밑 기준 캔버스 px, 각도·폭(음수 = 좌우 뒤집힘)·높이·그늘) — 처치 조각·보스 처치 연출이 같은 모습으로 그린다.
    public struct EnemyPart { public Sprite sprite; public Vector2 at; public float angle, w, h; public Color shade; }

    public class EnemyView : MonoBehaviour
    {
        public SpriteRenderer shadow, ring, hpBack, hpFill;
        public Transform body, center, rot, squash, unrot, back, flip;
        public SpriteRenderer bodyA, bodyB;
        public SpriteRenderer[] pinStrokes, pinNeedles;
        public MonsterRig[] rigs;             // 종류별 리그 템플릿 (씬에서 꺼 둔 Rigs 아래)

        readonly Dictionary<string, MonsterRig> own = new(), twins = new();
        MonsterRig shown, twinShown;
        Vector3 lastFoot;

        MaterialPropertyBlock block;
        static readonly int BrightId = Shader.PropertyToID("_Bright"), SatId = Shader.PropertyToID("_Sat"), CutId = Shader.PropertyToID("_CutPlane"),
            ShapeId = Shader.PropertyToID("_Shape"), RingId = Shader.PropertyToID("_Ring"), FillId = Shader.PropertyToID("_Fill");

        void Ensure() => block ??= new MaterialPropertyBlock();

        static Vector3 Local(float px, float py) => new(px / Space.Ppu, -py / Space.Ppu, 0);

        void Shape(SpriteRenderer sr, float shape, float ringAmount = 0)
        {
            block.Clear();
            block.SetFloat(ShapeId, shape);
            block.SetFloat(RingId, ringAmount);
            sr.SetPropertyBlock(block);
        }

        // 이 종류의 리그 (처음 쓸 때 템플릿을 Flip 아래로 복제). 템플릿이 없으면 null → 통그림으로 그린다.
        MonsterRig RigFor(string id)
        {
            if (own.TryGetValue(id, out MonsterRig rig)) return rig;
            MonsterRig template = null;
            if (rigs != null) foreach (MonsterRig r in rigs) if (r != null && r.type == id) template = r;
            if (template != null)
            {
                rig = Instantiate(template, flip);
                rig.name = template.name;
                rig.gameObject.SetActive(false);
            }
            return own[id] = rig;
        }

        void ShowRig(MonsterRig rig)
        {
            if (shown == rig) return;
            if (shown != null) shown.gameObject.SetActive(false);
            shown = rig;
            if (rig != null) rig.gameObject.SetActive(true);
        }

        // 두 동강 때 다른 반쪽을 그릴 쌍둥이 리그 (자세는 매 프레임 원본에서 베낀다)
        void ShowTwin(MonsterRig rig, string id)
        {
            MonsterRig twin = null;
            if (rig != null && !twins.TryGetValue(id, out twin))
            {
                twin = Instantiate(rig, flip);
                twin.name = rig.name + " (twin)";
                twin.enabled = false;
                twins[id] = twin;
            }
            if (twinShown != null && twinShown != twin) twinShown.gameObject.SetActive(false);
            twinShown = twin;
            if (twin != null) twin.gameObject.SetActive(true);
        }

        static void CopyPose(MonsterRig from, MonsterRig to)
        {
            to.transform.localRotation = from.transform.localRotation;
            to.transform.localScale = from.transform.localScale;
            for (int i = 0; i < from.bones.Length && i < to.bones.Length; i++)
            {
                if (from.bones[i] == null || to.bones[i] == null) continue;
                Transform a = from.bones[i].transform, b = to.bones[i].transform;
                b.localPosition = a.localPosition; b.localRotation = a.localRotation; b.localScale = a.localScale;
                SpriteRenderer sa = from.Arts[i], sb = to.Arts[i];
                if (sa != null && sb != null) { sb.sprite = sa.sprite; sb.enabled = sa.enabled; }
            }
        }

        // 지금 보이는 리그의 파츠 모습 (마지막으로 그린 프레임, 발밑 기준). 리그가 없으면 빈 목록.
        public List<EnemyPart> Snapshot()
        {
            var list = new List<EnemyPart>();
            if (shown == null || !shown.gameObject.activeInHierarchy) return list;
            Vector3 rigAt = shown.transform.localPosition;
            shown.transform.localPosition = Vector3.zero;   // 두 동강으로 벌어진 만큼은 빼고 잰다
            var order = new List<int>();
            SpriteRenderer[] arts = shown.Arts;
            for (int i = 0; i < arts.Length; i++) if (arts[i] != null && arts[i].enabled && arts[i].gameObject.activeInHierarchy) order.Add(i);
            order.Sort((a, b) => arts[a].sortingOrder.CompareTo(arts[b].sortingOrder));
            Vector2 foot = new(lastFoot.x * Space.Ppu, -lastFoot.y * Space.Ppu);
            foreach (int i in order)
            {
                SpriteRenderer sr = arts[i];
                Matrix4x4 m = sr.transform.localToWorldMatrix;
                Vector2 ax = m.GetColumn(0), ay = m.GetColumn(1);
                Vector3 pivot = m.GetColumn(3);
                float det = ax.x * ay.y - ax.y * ay.x;
                float unityAngle = det >= 0 ? Mathf.Atan2(ax.y, ax.x) : Mathf.Atan2(-ax.y, -ax.x);
                Rect rect = sr.sprite.rect;
                Color shade = shown.shadeColor;
                shade.a = shown.bones[i].shade;
                list.Add(new EnemyPart
                {
                    sprite = sr.sprite, at = new Vector2(pivot.x * Space.Ppu, -pivot.y * Space.Ppu) - foot, angle = -unityAngle,
                    w = rect.width / sr.sprite.pixelsPerUnit * ax.magnitude * Space.Ppu * (det >= 0 ? 1 : -1),
                    h = rect.height / sr.sprite.pixelsPerUnit * ay.magnitude * Space.Ppu,
                    shade = shade,
                });
            }
            shown.transform.localPosition = rigAt;
            return list;
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
            lastFoot = transform.position;
            Sprite art = e.type.sprite;
            float h = e.height;
            float w = h * (art.rect.width / art.rect.height);
            bool boss = e.IsBoss;
            MonsterRig rig = RigFor(e.type.id);
            // 리그가 있으면 걷기 출렁임은 리그 동작이 맡는다
            float bob = rig != null ? 0 : Mathf.Sin(Mathf.Floor(time * 12) / 12 * 8 + e.x) * (e.action == EnemyAction.Move ? 2 : .5f);

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
                sx = 1 + .34f * pulse * e.stretch.power; sy = Mathf.Max(.4f, 1 - .2f * pulse * e.stretch.power);
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
            if (rig == null) ShowRig(null);
            if (e.cut == null || rig == null) ShowTwin(null, null);
            if (e.cut != null && rig == null)
            {
                // 실 따라 두 동강: 임팩트 순간 벌어졌다가(히트스톱 동안 유지) 다시 붙는다.
                float k = Mathf.Pow(e.cut.time / e.cut.max, 2);
                float angle = e.facing > 0 ? e.cut.angle : Mathf.PI - e.cut.angle;
                Halves(angle, 7 * k, e.cut.side, h, bright, sat);
            }
            else if (rig != null)
            {
                bodyA.gameObject.SetActive(false);
                bodyB.gameObject.SetActive(false);
                ShowRig(rig);
                rig.Animate(e, time, dt, h / rig.height);
                Vector4 cutA = Vector4.zero, cutB = Vector4.zero;
                if (e.cut != null)
                {
                    // 두 동강도 리그 그대로: 원본 = 한쪽, 쌍둥이 = 반대쪽 (크기·발 위치·자세가 바뀌지 않는다)
                    ShowTwin(rig, e.type.id);
                    CopyPose(rig, twinShown);
                    float k = Mathf.Pow(e.cut.time / e.cut.max, 2);
                    float angle = e.facing > 0 ? e.cut.angle : Mathf.PI - e.cut.angle;
                    Vector3 rigBase = rig.transform.localPosition;
                    (Vector3 shiftA, Vector4 planeA) = Half(angle, 7 * k, e.cut.side, h, 1);
                    (Vector3 shiftB, Vector4 planeB) = Half(angle, 7 * k, e.cut.side, h, -1);
                    rig.transform.localPosition = rigBase + shiftA;
                    twinShown.transform.localPosition = rigBase + shiftB;
                    cutA = planeA; cutB = planeB;
                }
                StyleRig(rig, bright, sat, cutA);
                if (e.cut != null) StyleRig(twinShown, bright, sat, cutB);
            }
            else
            {
                bodyA.gameObject.SetActive(true);
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

        void StyleRig(MonsterRig rig, float bright, float sat, Vector4 cut)
        {
            SpriteRenderer[] arts = rig.Arts;
            for (int i = 0; i < arts.Length; i++)
            {
                if (arts[i] == null) continue;
                Color shade = rig.shadeColor;
                shade.a = rig.bones[i].shade;
                block.Clear();
                block.SetFloat(BrightId, bright);
                block.SetFloat(SatId, sat);
                block.SetColor(FillId, shade);
                block.SetVector(CutId, cut);
                arts[i].SetPropertyBlock(block);
            }
        }

        // 두 동강의 한쪽(s = +1 / -1): Flip 로컬에서 벌어지는 만큼과, 그 쪽만 남기는 월드 반평면.
        (Vector3 shift, Vector4 plane) Half(float angle, float gap, float side, float h, float s)
        {
            Vector2 d = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 n = new(-d.y, d.x);
            // 절단선: Flip 공간의 몸 중심(0,-h/2)을 지나는 직선 (캔버스 로컬 → Unity 로컬)
            Vector3 c = flip.TransformPoint(Local(0, -h / 2));
            Vector3 dirWorld = flip.TransformDirection(new Vector3(d.x, -d.y, 0));
            Vector2 nWorld = new Vector2(-dirWorld.y, dirWorld.x).normalized;
            Vector2 nLocalUnity = new(n.x, -n.y);
            Vector3 nTest = flip.TransformDirection(nLocalUnity);
            if (Vector2.Dot(nWorld, nTest) < 0) nWorld = -nWorld;   // 캔버스 법선 n 쪽을 +로 맞춘다
            float push = gap * (s == side ? 1.25f : .6f);
            Vector3 shift = Local(n.x * s * push + d.x * s * push * .35f, n.y * s * push + d.y * s * push * .35f);
            Vector2 normal = nWorld * s;
            return (shift, new Vector4(normal.x, normal.y, Vector2.Dot(normal, (Vector2)c), 1));
        }

        // 리그가 없는 종류: 통그림 두 조각
        void Halves(float angle, float gap, float side, float h, float bright, float sat)
        {
            bodyA.gameObject.SetActive(true);
            bodyB.gameObject.SetActive(true);
            int index = 0;
            foreach (float s in new[] { 1f, -1f })
            {
                SpriteRenderer half = index++ == 0 ? bodyA : bodyB;
                (Vector3 shift, Vector4 plane) = Half(angle, gap, side, h, s);
                half.transform.localPosition = shift;
                block.Clear();
                block.SetFloat(BrightId, bright);
                block.SetFloat(SatId, sat);
                block.SetVector(CutId, plane);
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
