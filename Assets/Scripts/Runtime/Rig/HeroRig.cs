// 주인공 뼈대 리그 (Unity 계층). 뼈(Transform)·피벗·관절 위치는 MCP 로 씬에 조립해 두고(Arena/Hero/Rig),
// 동작은 여기 C# 가 매 프레임 계산한다. 동작 값(파형·자세)도 씬 데이터이며 인스펙터에서 고친다.
//   반복 동작(idle, run) : 뼈마다 offset + a1·sin(2π(u+p1)) + a2·sin(4π(u+p2)),  u = 동작 시간 / length
//   한 번 동작(shoot, pull, dash, hurt, die) : 시작 자세(0초) → 최고 자세(peakTime) → 정리 자세(settleTime) → 0
//     (keepEnd 면 마지막 자세 유지, easeIn 이면 최고 자세까지 빠르게 들어간다)
// 각도는 캔버스 기준(시계 방향 +, 도), 위치는 그림 px(아래 +y) — 웹판 리그와 같은 단위다.
using System;
using UnityEngine;

namespace TOD
{
    [Serializable] public class Wave
    {
        public float offset, a1, p1, a2, p2;
        public float At(float u) => offset + a1 * Mathf.Sin(2 * Mathf.PI * (u + p1)) + a2 * Mathf.Sin(4 * Mathf.PI * (u + p2));
    }

    [Serializable] public class BoneWave : Wave { public string bone; }

    [Serializable] public class LoopMotion
    {
        public float length = 1;
        public Wave rootX, rootY;
        public BoneWave[] bones;
    }

    [Serializable] public class BonePose { public string bone; public float start, peak, settle; }

    [Serializable] public class ShotMotion
    {
        public float length, peakTime, settleTime;
        public bool keepEnd, easeIn;
        public Vector2 rootStart, rootPeak, rootSettle;
        public BonePose[] bones;

        // 0~1: 시작 → 최고 → 정리 → 0 구간 보간 (값 세 개에 공통으로 쓴다)
        public float Value(float t, float start, float peak, float settle)
        {
            if (peakTime > 0 && t < peakTime)
            {
                float u = t / peakTime;
                if (easeIn) u = 1 - (1 - u) * (1 - u);
                return Mathf.Lerp(start, peak, u);
            }
            if (t < settleTime) return Mathf.Lerp(peak, settle, settleTime > peakTime ? (t - peakTime) / (settleTime - peakTime) : 1);
            if (keepEnd || length <= settleTime) return settle;
            return Mathf.Lerp(settle, 0, Mathf.Clamp01((t - settleTime) / (length - settleTime)));
        }
    }

    [Serializable] public class RigFoot { public HeroBone bone; public Vector2 sole; public float depth; }

    public class HeroRig : BoneRig
    {
        [Header("주인공 뼈")]
        public HeroBone needle, nearArm, nearForearm, head;
        public HeroBone[] gait;               // 달리며 쏠 때 달리기 자세를 섞는 다리 뼈
        public RigFoot[] feet;
        public float runGroundDamp = .35f;    // 달리기 때 발 접지 보정을 얼마나 따를지 (사이클 평균 기준)
        public float maxGroundCorrection = 46;

        [Header("동작")]
        public LoopMotion idle, run;
        public ShotMotion shoot, pull, dash, hurt, die;
        [Header("쏘기: 겨눈 방향으로 팔·머리 틀기")]
        public float aimArm = .74f, aimForearm = .26f, aimHead = .11f, aimFadeStart = .14f, aimFadeTime = .12f;

        float runGroundMean = float.NaN;

        // 자세를 뼈 Transform 에 적용한다. root = 리그 원점(발밑)의 월드 위치, scale = 그림 px → 캔버스 px 배율.
        public void Apply(Pose pose, Vector3 root, float scale, float facing)
        {
            transform.position = root;
            transform.localScale = new Vector3(scale * facing, scale, 1);
            ApplyPose(pose);
        }

        // 발바닥이 바닥(리그 원점)에 닿도록 리그를 위아래로 옮긴다. damp < 1 이면 사이클 평균에서 그만큼만 따른다.
        public void Ground(float scale, float damp, float mean)
        {
            if (feet == null || feet.Length == 0) return;
            float offset = GroundOffset(scale);
            if (!float.IsNaN(mean)) offset = mean + (offset - mean) * damp;
            float correction = Mathf.Clamp(offset, -maxGroundCorrection, maxGroundCorrection);
            if (Mathf.Abs(correction) < .01f) return;
            transform.position += new Vector3(0, correction * scale / Space.Ppu, 0);
        }

        // 가장 낮은 발바닥이 원점보다 얼마나 아래에 있는지 (그림 px)
        float GroundOffset(float scale)
        {
            float best = float.NegativeInfinity;
            float baseY = transform.position.y;
            foreach (RigFoot f in feet)
            {
                if (f.bone == null) continue;
                Vector3 sole = f.bone.transform.TransformPoint(Px(f.sole - f.bone.pivot));
                best = Mathf.Max(best, (baseY - sole.y) * Space.Ppu / Mathf.Max(1e-4f, scale) - f.depth);
            }
            return float.IsNegativeInfinity(best) ? 0 : best;
        }

        // 달리기 사이클 평균 접지값 (처음 한 번만 계산)
        public float RunGroundMean(float scale)
        {
            if (!float.IsNaN(runGroundMean)) return runGroundMean;
            Vector3 position = transform.position;
            Vector3 size = transform.localScale;
            float sum = 0;
            for (int k = 0; k < 16; k++)
            {
                Pose pose = NewPose();
                Loop(run, run.length * k / 16, ref pose);
                Apply(pose, Vector3.zero, scale, 1);
                sum += GroundOffset(scale);
            }
            transform.position = position;
            transform.localScale = size;
            return runGroundMean = sum / 16;
        }
    }
}
