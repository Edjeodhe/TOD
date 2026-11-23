// 몬스터 뼈대 리그. 몬스터 종류마다 템플릿 하나를 MCP 로 조립해 두고(Arena/Enemies/Rigs/<종류>),
// EnemyView 가 자기 Flip 아래로 복제해 쓴다. 동작 값은 모두 씬 데이터(인스펙터에서 조정)이다.
//   걷기 : walk 파형을 "걸은 거리 / stride" 위상으로 돌린다 → 빨리 걸으면 발도 빨라지고 발이 미끄러지지 않는다.
//          실제 이동 속도로 idle ↔ walk 를 섞는다 (walkFull 캔버스 px/초에서 걷기 100%).
//   행동 : 예고(melee·shot·dash) → 돌진(charge, 걷기와 겹침) → 쉬는 동안 마무리(strike·fire·brake). 시작 자세 → 최고 → 정리.
//   피격 : 깜빡임(flash)이 새로 켜질 때마다 hurt 를 위에 더한다.
using System;
using UnityEngine;

namespace TOD
{
    public class MonsterRig : BoneRig
    {
        public string type;                   // EnemyType.id
        public float height;                  // 선 자세의 키 (그림 px, 발밑 → 머리 위). EnemyType 키에 맞춰 배율을 정한다
        public float stride;                  // 걷기 한 주기에 나아가는 거리 (그림 px)
        public float maxStepRate;             // 걷기 위상이 1초에 도는 최대 주기 수 (돌진처럼 빠를 때 다리가 헛돌지 않게)
        public float walkFull;                // 이 속도(캔버스 px/초)부터 걷기 100%
        public float walkFollow;              // 걷기 비율이 속도를 따라가는 빠르기 (1/초)
        public Color shadeColor;              // 뒤쪽 팔다리 그늘 색 (양은 뼈의 shade)

        [Header("접지: 가장 낮은 발바닥을 바닥(리그 원점)에 맞춘다")]
        public RigFoot[] feet;                // 발마다 뒤꿈치·발끝 두 점 (그림 px, 뼈 그림 기준)
        [Range(0, 1)] public float groundFollow;   // 0 = 끔, 1 = 늘 바닥에 붙임
        public float maxGroundCorrection;     // 그림 px

        [Header("반복 동작")]
        public LoopMotion idle, walk;
        [Header("행동 (예고 → 마무리)")]
        public ShotMotion melee, shot, dash, charge, strike, fire, brake;
        [Header("피격")]
        public ShotMotion hurt;

        SpriteRenderer[] arts;
        Enemy current;
        Vector2 last;
        float phase, speed, weight, actionStart, hurtStart, lastFlash, seed;
        EnemyAction lastAction, before;
        Pose pose;

        public SpriteRenderer[] Arts
        {
            get
            {
                if (arts != null) return arts;
                arts = new SpriteRenderer[bones.Length];
                for (int i = 0; i < bones.Length; i++) arts[i] = bones[i] != null ? bones[i].art : null;
                return arts;
            }
        }

        // e 의 이번 프레임 자세를 계산해 뼈에 적용한다. scale = 그림 px → 캔버스 px.
        public void Animate(Enemy e, float time, float dt, float scale)
        {
            Vector2 now = new(e.x, e.y);
            if (e != current)
            {
                current = e;
                last = now;
                phase = speed = weight = 0;
                lastAction = before = e.action;
                actionStart = time;
                hurtStart = -99;
                lastFlash = e.flash;
                seed = UnityEngine.Random.value * 10;
            }

            // 실제로 움직인 거리로 걷기 위상과 속도를 잰다 (당겨지거나 밀리는 이동은 빼고)
            float dist = Vector2.Distance(now, last);
            bool forced = e.yank != null || e.push != null;
            if (dt > 0)
            {
                speed = Mathf.Lerp(speed, forced ? 0 : dist / dt, 1 - Mathf.Exp(-12 * dt));
                if (!forced && stride > 0)
                {
                    float sign = (now.x - last.x) * e.facing < -.3f * dist ? -1 : 1;   // 뒷걸음질
                    float cycles = dist / Mathf.Max(1e-3f, stride * scale);
                    if (maxStepRate > 0) cycles = Mathf.Min(cycles, maxStepRate * dt);
                    phase += sign * cycles;
                }
                bool walking = e.action == EnemyAction.Move || e.action == EnemyAction.Charge;
                float target = walking && walkFull > 0 ? Mathf.Clamp01(speed / walkFull) : 0;
                weight = Mathf.Lerp(weight, target, 1 - Mathf.Exp(-walkFollow * dt));
            }
            last = now;

            if (e.action != lastAction) { before = lastAction; lastAction = e.action; actionStart = time; }
            if (e.flash > lastFlash + 1e-3f) hurtStart = time;
            lastFlash = e.flash;

            if (pose.ang == null || pose.ang.Length != bones.Length) pose = NewPose();
            else { Array.Clear(pose.ang, 0, pose.ang.Length); pose.root = Vector2.zero; }
            Loop(idle, time + seed, ref pose, 1 - weight);
            if (walk != null) Loop(walk, phase * walk.length, ref pose, weight);
            ShotMotion act = Action(e.action, before);
            if (act != null && act.length > 0) Shot(act, time - actionStart, ref pose);
            if (hurt != null && hurt.length > 0 && time - hurtStart < hurt.length) Shot(hurt, time - hurtStart, ref pose);

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(scale, scale, 1);
            ApplyPose(pose);
            Ground();
        }

        // 리그 로컬 공간에서 발바닥 높이를 재어 뿌리 뼈를 위아래로 옮긴다 (부모의 늘이기·회전과 무관하게).
        void Ground()
        {
            if (feet == null || feet.Length == 0 || groundFollow <= 0 || bones.Length == 0) return;
            float lowest = float.NegativeInfinity;   // 그림 px, 아래 +
            foreach (RigFoot f in feet)
            {
                if (f.bone == null) continue;
                Vector3 local = transform.InverseTransformPoint(f.bone.transform.TransformPoint(Px(f.sole - f.bone.pivot)));
                lowest = Mathf.Max(lowest, -local.y * Space.Ppu - f.depth);
            }
            if (float.IsNegativeInfinity(lowest)) return;
            float correction = Mathf.Clamp(-lowest * groundFollow, -maxGroundCorrection, maxGroundCorrection);
            bones[0].transform.localPosition += new Vector3(0, -correction / Space.Ppu, 0);
        }

        ShotMotion Action(EnemyAction action, EnemyAction previous) => action switch
        {
            EnemyAction.WindupMelee => melee,
            EnemyAction.WindupShot => shot,
            EnemyAction.WindupDash => dash,
            EnemyAction.Charge => charge,
            EnemyAction.Rest => previous switch
            {
                EnemyAction.WindupMelee => strike,
                EnemyAction.WindupShot => fire,
                EnemyAction.Charge => brake,
                _ => null,
            },
            _ => null,
        };
    }
}
