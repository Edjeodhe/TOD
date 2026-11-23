// 주인공 종이 인형 (웹판 render.js 의 heroPose / drawHero / dodgeState).
// 파츠 오브젝트는 MCP로 Hero/Parts/<파츠 id> 에 만들어 두고, 여기서는 이름으로 찾아 매 프레임 위치만 정한다.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public class HeroFrame
    {
        public RigPose pose;
        public Affine @base;
        public Affine[] matrices;
    }

    public class HeroView : MonoBehaviour
    {
        public TextAsset rigJson;
        public Transform partsRoot;
        public SpriteRenderer shadow, dodge, dodgeGlow;
        public Sprite[] dodgeFrames;

        static readonly string[] GaitParts = { "near_thigh", "near_shin", "near_foot", "far_thigh", "far_shin", "far_foot" };
        public Rig Rig { get; private set; }
        GameDatabase db;
        SpriteRenderer[] parts;
        int[] gait;
        int nearArm, nearForearm, head, needle;
        MaterialPropertyBlock block;
        static readonly int BrightId = Shader.PropertyToID("_Bright"), SatId = Shader.PropertyToID("_Sat"), FillId = Shader.PropertyToID("_Fill"),
            ShapeId = Shader.PropertyToID("_Shape");

        public void Init(GameDatabase database)
        {
            db = database;
            Rig = Rig.Load(rigJson.text);
            parts = new SpriteRenderer[Rig.parts.Length];
            var byName = new Dictionary<string, SpriteRenderer>();
            foreach (Transform child in partsRoot) byName[child.name] = child.GetComponent<SpriteRenderer>();
            for (int i = 0; i < Rig.parts.Length; i++) byName.TryGetValue(Rig.parts[i].id, out parts[i]);
            for (int rank = 0; rank < Rig.drawOrder.Length; rank++)
            {
                SpriteRenderer sr = parts[Rig.drawOrder[rank]];
                if (sr != null) sr.sortingOrder = rank;
            }
            gait = System.Array.ConvertAll(GaitParts, Rig.Index);
            nearArm = Rig.Index("near_arm"); nearForearm = Rig.Index("near_forearm"); head = Rig.Index("head"); needle = Rig.Index("needle");
            block = new MaterialPropertyBlock();
            block.SetFloat(ShapeId, 1);
            shadow.SetPropertyBlock(block);
            dodgeGlow.sortingOrder = Rig.drawOrder.Length;
            dodge.sortingOrder = Rig.drawOrder.Length + 1;
        }

        public float Scale(float y)
        {
            VisualConfig v = db.visual;
            return v.heroScaleBase * (v.heroScaleNear + v.heroScaleFar * Mathf.Clamp01(y / db.arena.height));
        }

        static float Smooth(float b) => b * b * (3 - 2 * b);

        RigPose Sample(HeroAction action, float t, float strideTime, float angle, float motion)
        {
            string clip = ClipName(action);
            RigPose pose = Rig.Pose(clip, action == HeroAction.Run ? strideTime : Mathf.Max(0, t));
            if (action == HeroAction.Shoot)
            {
                // 겨눈 방향으로 팔·머리를 틀고, 달리며 쏘면 하체는 달리기 자세를 섞는다.
                float aim = Mathf.Atan2(Mathf.Sin(angle), Mathf.Abs(Mathf.Cos(angle))) * Mathf.Rad2Deg * (1 - Mathf.Clamp01((t - .14f) / .12f));
                if (nearArm >= 0) pose.ang[nearArm] += aim * .74f;
                if (nearForearm >= 0) pose.ang[nearForearm] += aim * .26f;
                if (head >= 0) pose.ang[head] += aim * .11f;
                if (motion > .2f)
                {
                    RigPose stride = Rig.Pose("run", strideTime);
                    foreach (int i in gait) if (i >= 0) pose.ang[i] += (stride.ang[i] - pose.ang[i]) * motion;
                }
            }
            return pose;
        }

        static string ClipName(HeroAction action) => action switch
        {
            HeroAction.Idle => "idle", HeroAction.Run => "run", HeroAction.Shoot => "shoot", HeroAction.Pull => "pull",
            HeroAction.Dash => "dash", HeroAction.Hurt => "hurt", _ => "die",
        };

        bool InDash(Player p) => p.action != HeroAction.Die && (p.dash > 0 || p.dashGrace > 0);

        public HeroFrame Pose(Player p, float actionTime)
        {
            FeelConfig f = db.feel;
            bool inDash = InDash(p);
            HeroAction action = inDash ? HeroAction.Dash : p.action;
            float t = inDash ? (p.dash > 0 ? f.dashDuration - p.dash : f.dashDuration + f.dashGrace - p.dashGrace)
                : actionTime + (action == HeroAction.Pull ? .035f : 0);
            float facing = inDash ? p.dashFacing : p.facing;
            RigPose pose = Sample(action, t, p.strideTime, p.angle, p.motion);
            PoseSnapshot previous = p.previousPose;
            float blend = Mathf.Clamp01(t / (action is HeroAction.Shoot or HeroAction.Pull ? .055f : .085f));
            if (previous != null && previous.action != action && action != HeroAction.Die && blend < 1)
            {
                float weight = 1 - Smooth(blend);
                RigPose from = Sample(previous.action, previous.actionTime, previous.strideTime, previous.angle, previous.motion);
                for (int i = 0; i < pose.ang.Length; i++) pose.ang[i] += (from.ang[i] - pose.ang[i]) * weight;
                pose.root += (from.root - pose.root) * weight;
            }
            // 던진 반동: 뒤로 확 밀렸다가 지나쳐 튕기며 멈춘다.
            float rx = 0, ry = 0;
            if (p.recoil != null)
            {
                float u = 1 - p.recoil.time / p.recoil.max;
                float k = u < .25f ? u / .25f : Mathf.Exp(-(u - .25f) * 9) * Mathf.Cos((u - .25f) * 16);
                rx = p.recoil.x * f.recoilHero * k;
                ry = p.recoil.y * f.recoilHero * k * Space.Proj;
            }
            float scale = Scale(p.y);
            Affine @base = Affine.Identity.Translate(p.x + rx, Space.ProjectY(p.y) + ry).Scale(scale * facing, scale);
            bool grounded = action == HeroAction.Run || action == HeroAction.Pull || (action == HeroAction.Shoot && p.motion > .2f);
            Affine[] matrices = grounded ? Rig.GroundedMatrices(pose, @base, action == HeroAction.Pull ? null : "run") : Rig.Matrices(pose, @base);
            return new HeroFrame { pose = pose, @base = @base, matrices = matrices };
        }

        // 바늘 끝(손) 위치 → 전투장 좌표 (손에 든 실의 시작점)
        public Vector2 Hand(HeroFrame frame)
        {
            if (needle < 0) return Vector2.zero;
            Vector2 c = frame.matrices[needle].Apply(Vector2.zero);
            return new Vector2(c.x, (c.y - Space.Top) / Space.Proj);
        }

        public (int index, float hop) DodgeState(Player p)
        {
            if (!InDash(p)) return (-1, 0);
            FeelConfig f = db.feel;
            float elapsed = p.dash > 0 ? f.dashDuration - p.dash : f.dashDuration + f.dashGrace - p.dashGrace;
            int index = elapsed < db.visual.dodgeLaunchEnd ? 0 : elapsed < db.visual.dodgeAirEnd ? 1 : 2;
            float hop = Mathf.Sin(Mathf.PI * Mathf.Clamp01((elapsed - .02f) / .22f)) * db.visual.dodgeHop;
            return (index, hop);
        }

        public float DodgeScale(float y) => db.visual.dodgeScale * Scale(y) / db.visual.heroScaleBase;

        public void Render(Player p, HeroFrame frame, float alpha, Color shadowColor)
        {
            Vector3 root = Space.Ground(p.x, p.y);
            transform.position = root;
            var (dodgeIndex, hop) = DodgeState(p);
            float scale = Scale(p.y);
            float size = 25 * scale / db.visual.heroScaleBase * (dodgeIndex >= 0 ? 1 - hop / 40 : 1);
            Place(shadow.transform, root, new Vector2(p.x, Space.ProjectY(p.y) + 3), 0, new Vector2(size * 2 / shadow.sprite.rect.width, size * .68f / shadow.sprite.rect.height));
            shadow.color = alpha < 1 ? Painter.Alpha(shadowColor, 0) : shadowColor;

            bool dodging = dodgeIndex >= 0 && dodgeFrames.Length > dodgeIndex;
            dodge.gameObject.SetActive(dodging);
            dodgeGlow.gameObject.SetActive(dodging);
            foreach (SpriteRenderer sr in parts) if (sr != null) sr.enabled = !dodging;
            if (dodging)
            {
                float s = DodgeScale(p.y);
                var at = new Vector2(p.x, Space.ProjectY(p.y) - hop);
                dodge.sprite = dodgeFrames[dodgeIndex];
                Place(dodge.transform, root, at, 0, new Vector2(s * p.dashFacing, s));
                dodge.color = new Color(1, 1, 1, alpha);
                dodgeGlow.sprite = dodgeFrames[dodgeIndex];
                Place(dodgeGlow.transform, root, at, 0, new Vector2(s * 1.06f * p.dashFacing, s * 1.06f));
                dodgeGlow.color = new Color(1, 1, 1, .55f);
                block.Clear();
                block.SetColor(FillId, db.visual.dodgeColor);
                dodgeGlow.SetPropertyBlock(block);
                return;
            }

            // 피격: 처음 절반은 붉게 물들고, 무적 시간 동안 깜빡인다.
            bool glow = p.hurt > .31f;
            if (p.hurt > 0 && p.hurt <= .31f && alpha >= 1 && Mathf.FloorToInt(p.hurt * 22) % 2 == 1) alpha = .45f;
            for (int i = 0; i < parts.Length; i++)
            {
                SpriteRenderer sr = parts[i];
                RigPart part = Rig.parts[i];
                if (sr == null || sr.sprite == null) continue;
                sr.enabled = !part.hidden;
                Affine m = frame.matrices[i];
                Rect rect = sr.sprite.rect;
                Vector2 center = m.Apply(new Vector2(rect.width / 2 - part.pivot.x, rect.height / 2 - part.pivot.y));
                float det = m.a * m.d - m.b * m.c;
                float k = Mathf.Sqrt(m.a * m.a + m.b * m.b);
                float angle = det >= 0 ? Mathf.Atan2(m.b, m.a) : Mathf.Atan2(-m.b, -m.a);
                Place(sr.transform, root, center, angle, new Vector2(det >= 0 ? k : -k, k));
                block.Clear();
                if (glow)
                {
                    sr.color = new Color(db.palette.heroHurt.r, db.palette.heroHurt.g, db.palette.heroHurt.b, alpha);
                    block.SetFloat(SatId, 0);
                    block.SetFloat(BrightId, 1.6f);
                }
                else
                {
                    sr.color = new Color(1, 1, 1, alpha);
                    block.SetFloat(SatId, 1);
                    block.SetFloat(BrightId, 1);
                    block.SetColor(FillId, new Color(22 / 255f, 19 / 255f, 27 / 255f, part.shade));
                }
                sr.SetPropertyBlock(block);
            }
        }

        // 캔버스 좌표 center 에 회전·크기를 적용해 root 기준 로컬로 배치
        static void Place(Transform t, Vector3 root, Vector2 canvas, float canvasAngle, Vector2 scale)
        {
            t.localPosition = Space.Canvas(canvas) - root;
            t.localRotation = Quaternion.Euler(0, 0, Space.Degrees(canvasAngle));
            t.localScale = new Vector3(scale.x, scale.y, 1);
        }
    }
}
