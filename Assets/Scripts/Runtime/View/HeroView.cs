// 주인공 종이 인형. 뼈대는 MCP 로 조립한 Hero/Rig 계층이고, 동작은 HeroRig(C#)가 계산한다.
// 여기서는 상태(달리기·쏘기·당기기·대쉬·피격·쓰러짐)에 맞춰 동작을 고르고 섞어 뼈에 적용한 뒤, 색(피격·그늘)만 입힌다.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public class HeroFrame
    {
        public HeroRig.Pose pose;
        public Vector3 root;                  // 리그 원점(발밑) 월드 위치
        public float scale, facing;
    }

    // 대쉬 잔상용: 한 순간 파츠들의 그림·위치 (캔버스 기준)
    public struct PartSnap { public Sprite sprite; public Vector2 center; public float angle, w, h; }

    public class HeroView : MonoBehaviour
    {
        public HeroRig rig;
        public SpriteRenderer shadow;

        GameDatabase db;
        int nearArm, nearForearm, head;
        int[] gait;
        MaterialPropertyBlock block;
        static readonly int BrightId = Shader.PropertyToID("_Bright"), SatId = Shader.PropertyToID("_Sat"), FillId = Shader.PropertyToID("_Fill"),
            ShapeId = Shader.PropertyToID("_Shape");

        public void Init(GameDatabase database)
        {
            db = database;
            nearArm = rig.Index(rig.nearArm); nearForearm = rig.Index(rig.nearForearm); head = rig.Index(rig.head);
            gait = System.Array.ConvertAll(rig.gait ?? new HeroBone[0], rig.Index);
            block = new MaterialPropertyBlock();
            block.SetFloat(ShapeId, 1);
            shadow.SetPropertyBlock(block);
        }

        public float Scale(float y)
        {
            VisualConfig v = db.visual;
            float size = v.heroSize > 0 ? v.heroSize : 1;
            return v.heroScaleBase * size * (v.heroScaleNear + v.heroScaleFar * Mathf.Clamp01(y / db.arena.height));
        }

        static float Smooth(float b) => b * b * (3 - 2 * b);

        HeroRig.Pose Sample(HeroAction action, float t, float strideTime, float angle, float motion)
        {
            HeroRig.Pose pose = rig.NewPose();
            t = Mathf.Max(0, t);
            switch (action)
            {
                case HeroAction.Idle: rig.Loop(rig.idle, t, ref pose); break;
                case HeroAction.Run: rig.Loop(rig.run, strideTime, ref pose); break;
                case HeroAction.Shoot: rig.Shot(rig.shoot, t, ref pose); break;
                case HeroAction.Pull: rig.Shot(rig.pull, t, ref pose); break;
                case HeroAction.Dash: rig.Shot(rig.dash, t, ref pose); break;
                case HeroAction.Hurt: rig.Shot(rig.hurt, t, ref pose); break;
                default: rig.Shot(rig.die, t, ref pose); break;
            }
            if (action == HeroAction.Shoot)
            {
                // 겨눈 방향으로 팔·머리를 틀고, 달리며 쏘면 하체는 달리기 자세를 섞는다.
                float aim = Mathf.Atan2(Mathf.Sin(angle), Mathf.Abs(Mathf.Cos(angle))) * Mathf.Rad2Deg
                    * (1 - Mathf.Clamp01((t - rig.aimFadeStart) / Mathf.Max(.01f, rig.aimFadeTime)));
                if (nearArm >= 0) pose.ang[nearArm] += aim * rig.aimArm;
                if (nearForearm >= 0) pose.ang[nearForearm] += aim * rig.aimForearm;
                if (head >= 0) pose.ang[head] += aim * rig.aimHead;
                if (motion > .2f)
                {
                    HeroRig.Pose stride = rig.NewPose();
                    rig.Loop(rig.run, strideTime, ref stride);
                    foreach (int i in gait) if (i >= 0) pose.ang[i] += (stride.ang[i] - pose.ang[i]) * motion;
                }
            }
            return pose;
        }

        bool InDash(Player p) => p.action != HeroAction.Die && (p.dash > 0 || p.dashGrace > 0);

        public HeroFrame Pose(Player p, float actionTime)
        {
            FeelConfig f = db.feel;
            bool inDash = InDash(p);
            HeroAction action = inDash ? HeroAction.Dash : p.action;
            float t = inDash ? (p.dash > 0 ? f.dashDuration - p.dash : f.dashDuration + f.dashGrace - p.dashGrace)
                : actionTime + (action == HeroAction.Pull ? .035f : 0);
            float facing = inDash ? p.dashFacing : p.facing;
            HeroRig.Pose pose = Sample(action, t, p.strideTime, p.angle, p.motion);
            PoseSnapshot previous = p.previousPose;
            float blend = Mathf.Clamp01(t / (action is HeroAction.Shoot or HeroAction.Pull ? .055f : .085f));
            if (previous != null && previous.action != action && action != HeroAction.Die && blend < 1)
            {
                float weight = 1 - Smooth(blend);
                HeroRig.Pose from = Sample(previous.action, previous.actionTime, previous.strideTime, previous.angle, previous.motion);
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
            // 대쉬는 짧게 떠오른다 (예전 회피 통그림의 도약 대신).
            if (inDash) ry -= Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - .02f) / .22f)) * db.visual.dodgeHop;
            return new HeroFrame { pose = pose, root = Space.Canvas(p.x + rx, Space.ProjectY(p.y) + ry), scale = Scale(p.y), facing = facing };
        }

        // 뼈에 자세를 적용한다 (Hand·Render 전에 한 번).
        public void Apply(Player p, HeroFrame frame)
        {
            transform.position = Space.Ground(p.x, p.y);   // 부모(그림자 기준)를 먼저 놓고 리그를 놓는다
            rig.Apply(frame.pose, frame.root, frame.scale, frame.facing);
            bool inDash = InDash(p);
            HeroAction action = inDash ? HeroAction.Dash : p.action;
            bool grounded = action == HeroAction.Run || action == HeroAction.Pull || (action == HeroAction.Shoot && p.motion > .2f);
            if (grounded) rig.Ground(frame.scale, action == HeroAction.Pull ? 1 : rig.runGroundDamp, action == HeroAction.Pull ? float.NaN : rig.RunGroundMean(frame.scale));
        }

        // 바늘 뼈 원점(손) → 전투장 좌표 (손에 든 실의 시작점)
        public Vector2 Hand()
        {
            if (rig.needle == null) return Vector2.zero;
            Vector3 w = rig.needle.transform.position;
            return new Vector2(w.x * Space.Ppu, (-w.y * Space.Ppu - Space.Top) / Space.Proj);
        }

        public void Render(Player p, float alpha, Color shadowColor)
        {
            Vector3 root = Space.Ground(p.x, p.y);
            float scale = Scale(p.y);
            float hop = InDash(p) ? Mathf.Clamp01(1 - (rig.transform.position.y - root.y) * Space.Ppu / 40) : 1;
            float size = 25 * scale / db.visual.heroScaleBase * hop;
            Place(shadow.transform, root, new Vector2(p.x, Space.ProjectY(p.y) + 3), 0, new Vector2(size * 2 / shadow.sprite.rect.width, size * .68f / shadow.sprite.rect.height));
            shadow.color = alpha < 1 ? Painter.Alpha(shadowColor, 0) : shadowColor;

            // 피격: 처음 절반은 붉게 물들고(채도는 그대로), 무적 시간 동안 깜빡인다.
            bool glow = p.hurt > .31f;
            if (p.hurt > 0 && p.hurt <= .31f && alpha >= 1 && Mathf.FloorToInt(p.hurt * 22) % 2 == 1) alpha = .45f;
            Color hurt = db.palette.heroHurt;
            float amount = db.palette.heroHurtAmount > 0 ? db.palette.heroHurtAmount : .55f;
            // 레벨업: 몸이 초록 기운으로 은은하게 물들었다 빠진다 (피격이 우선).
            VisualConfig v = db.visual;
            float level = p.levelUp > 0 && v.levelUpTime > 0 ? p.levelUp / v.levelUpTime : 0;
            float levelFill = level > 0 ? v.levelUpFill * Mathf.Min(1, level * 3) * (.75f + .25f * Mathf.Sin(Time.time * 9)) : 0;
            foreach (HeroBone b in rig.bones)
            {
                SpriteRenderer sr = b != null ? b.art : null;
                if (sr == null) continue;
                sr.color = new Color(1, 1, 1, alpha);
                block.Clear();
                block.SetFloat(SatId, 1);
                block.SetFloat(BrightId, glow ? 1.1f : 1);
                Color fill = glow ? new Color(hurt.r, hurt.g, hurt.b, Mathf.Max(amount, b.shade))
                    : levelFill > b.shade ? new Color(v.levelUpColor.r, v.levelUpColor.g, v.levelUpColor.b, levelFill)
                    : new Color(22 / 255f, 19 / 255f, 27 / 255f, b.shade);
                block.SetColor(FillId, fill);
                sr.SetPropertyBlock(block);
            }
        }

        // 지금 자세의 파츠 그림들 (뒤에서 앞 순서) — 대쉬 잔상이 이 모양을 단색으로 그린다.
        public List<PartSnap> Snapshot()
        {
            var list = new List<PartSnap>();
            var order = new List<SpriteRenderer>();
            foreach (HeroBone b in rig.bones) if (b != null && b.art != null && b.art.enabled) order.Add(b.art);
            order.Sort((a, c) => a.sortingOrder.CompareTo(c.sortingOrder));
            foreach (SpriteRenderer sr in order)
            {
                Matrix4x4 m = sr.transform.localToWorldMatrix;
                Vector2 ax = m.GetColumn(0), ay = m.GetColumn(1);
                float det = ax.x * ay.y - ax.y * ay.x;
                float unityAngle = det >= 0 ? Mathf.Atan2(ax.y, ax.x) : Mathf.Atan2(-ax.y, -ax.x);
                Rect rect = sr.sprite.rect;
                Vector3 c = sr.bounds.center;
                list.Add(new PartSnap
                {
                    sprite = sr.sprite, center = new Vector2(c.x * Space.Ppu, -c.y * Space.Ppu), angle = -unityAngle,
                    w = rect.width / sr.sprite.pixelsPerUnit * ax.magnitude * Space.Ppu * (det >= 0 ? 1 : -1),
                    h = rect.height / sr.sprite.pixelsPerUnit * ay.magnitude * Space.Ppu,
                });
            }
            return list;
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
