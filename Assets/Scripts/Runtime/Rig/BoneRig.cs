// 뼈대 리그 공통부 (주인공 HeroRig, 몬스터 MonsterRig). 뼈(HeroBone)·관절 위치는 MCP 로 씬에 조립해 두고,
// 동작 파형(LoopMotion)·한 번 동작(ShotMotion)을 매 프레임 자세(Pose)로 계산해 뼈 Transform 에 적용한다.
// 각도는 캔버스 기준(시계 방향 +, 도), 위치는 그림 px(아래 +y).
using System;
using UnityEngine;

namespace TOD
{
    public abstract class BoneRig : MonoBehaviour
    {
        [Header("뼈대 (MCP 로 조립)")]
        public HeroBone[] bones;              // 부모가 먼저 오는 순서. 0번이 뿌리 뼈
        public Vector2 rootPos;               // 뿌리 뼈의 기본 위치 (그림 px, 리그 원점 = 발밑)

        public int Count => bones.Length;

        public struct Pose
        {
            public float[] ang;               // 뼈마다 동작 각도 (기본 rest 제외)
            public Vector2 root;
        }

        public Pose NewPose() => new() { ang = new float[bones.Length] };

        public int Index(HeroBone bone) => bone == null ? -1 : Array.IndexOf(bones, bone);

        // weight 만큼 더한다 (두 반복 동작을 섞을 때 0~1)
        public void Loop(LoopMotion m, float t, ref Pose pose, float weight = 1)
        {
            if (m == null || weight <= 0) return;
            float u = m.length > 0 ? Mathf.Repeat(t, m.length) / m.length : 0;
            if (m.rootX != null) pose.root.x += m.rootX.At(u) * weight;
            if (m.rootY != null) pose.root.y += m.rootY.At(u) * weight;
            if (m.bones == null) return;
            foreach (BoneWave w in m.bones)
            {
                int i = BoneIndex(w.bone);
                if (i >= 0) pose.ang[i] += w.At(u) * weight;
            }
        }

        public void Shot(ShotMotion m, float t, ref Pose pose, float weight = 1)
        {
            if (m == null || weight <= 0) return;
            t = Mathf.Clamp(t, 0, m.length);
            pose.root += weight * new Vector2(m.Value(t, m.rootStart.x, m.rootPeak.x, m.rootSettle.x), m.Value(t, m.rootStart.y, m.rootPeak.y, m.rootSettle.y));
            if (m.bones == null) return;
            foreach (BonePose b in m.bones)
            {
                int i = BoneIndex(b.bone);
                if (i >= 0) pose.ang[i] += m.Value(t, b.start, b.peak, b.settle) * weight;
            }
        }

        protected int BoneIndex(string id)
        {
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null && bones[i].id == id) return i;
            return -1;
        }

        // 자세를 뼈 Transform 에 적용한다 (리그 자체의 위치·배율은 호출하는 쪽이 정한다).
        public void ApplyPose(Pose pose)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                HeroBone b = bones[i];
                if (b == null) continue;
                if (i == 0) b.transform.localPosition = Px(rootPos + pose.root);
                b.transform.localRotation = Quaternion.Euler(0, 0, -(b.rest + pose.ang[i]));
            }
        }

        protected static Vector3 Px(Vector2 px) => new(px.x / Space.Ppu, -px.y / Space.Ppu, 0);
    }
}
