// 24차-hit-sparks: 당기기로 실이 적을 지나가며 맞힌 자리에서 터지는 바느질 타격 파티클 (실밥, 솜, 천 쪼가리, 단추, 바늘 반짝임).
// 그림은 Blender 시트(tools/render_hit_sparks.py → Assets/Art/VFX/24/sew_*.png, 흰색·밝은 회색이라 색을 곱해 물들인다).
// 값은 모두 씬 GameDatabase(fx.sparks)에 MCP 로 넣는다. 시뮬레이션은 Fx 하나(FxKind.Sew)만 만들고, 조각은 뷰가 seed 로 그린다.
using System;
using UnityEngine;

namespace TOD
{
    // 조각 한 종류
    [Serializable] public class SewPiece
    {
        public string name;
        public FlipClip clip;                 // frames + glow. fps = 도는 속도, size = 그리는 크기(캔버스 px), tint = 평소 색
        public bool loop = true;              // false = 한 번 재생(바늘 반짝임)
        public float weight = 1;              // 뽑힐 비중
        public float speed, speedJitter;      // 바닥 방향으로 퍼지는 처음 속도(px/s), ± 비율
        public float rise;                    // 처음 위로 튀는 속도 (px/s)
        public float gravity;                 // 떨어지는 가속 (px/s²). 바닥(적 발밑)에 닿으면 멈추고 돌지 않음
        public float drag;                    // 퍼지는 속도가 줄어드는 정도 (1/s)
        public float life;                    // 효과 시간 중 사는 비율 (0~1, 조각마다 ±25%)
        public float sizeJitter;              // 크기 ± 비율
        public float tintMix = 1;             // 결박 때 죄종 색을 섞는 양에 곱함 (솜은 덜 물들게 등)
    }

    [Serializable] public class SewSparkConfig
    {
        public float time;                    // 효과 전체 시간 (초). 0 이면 끔
        [Header("가운데 터짐 (0번 칸이 가장 큼 → 프레임 스톱 동안 보임)")]
        public FlipClip burst;                // size = 1타 크기(캔버스 px), fps 0 = burstTime 동안 한 번
        public float burstTime;               // 터짐 재생 시간 (초)
        public float burstGrowth;             // 타수 하나 늘 때마다 터짐 크기 + (px)
        [Header("조각")]
        public SewPiece[] pieces;
        public int baseCount, perHit, maxCount;   // 1타 개수, 타수 하나마다 +, 상한
        public int maxHits;                   // 타수 효과 상한 (이보다 많이 맞아도 같은 크기)
        public float sizePerHit, speedPerHit; // 타수 하나마다 조각 크기·속도 배율 +
        public float spread;                  // 주인공 반대쪽으로 퍼지는 부채꼴 반각 (라디안)
        public float omni;                    // 사방으로 퍼지는 조각 비율 (0~1)
        public float startOffset;             // 처음부터 가운데에서 떨어져 있는 거리 (px, 프레임 스톱 동안 터진 모양이 보이게)
        public float fadeTail;                // 조각 수명 끝 이 비율 동안 사라짐
        public float glow;                    // 빛(가산) 세기
        public int maxPieces;                 // 한 화면에 그리는 조각 상한
        [Header("결박 (같은 적에 바늘 여러 개): 죄종 색으로 물들고 더 크게·많이")]
        public float bindCount, bindSize, bindSpeed;   // 배율
        public float bindTint;                // 죄종 색 섞는 양 (0~1)
        public float bindGlow;                // 결박 빛 세기
        public float bindWhite;               // 죄종 색을 흰색 쪽으로 밝히는 양 (어두운 죄종도 보이게)

        int Hits(int hits) => Mathf.Clamp(hits, 1, Mathf.Max(1, maxHits));

        public int Count(int hits, bool bound)
        {
            float n = baseCount + perHit * (Hits(hits) - 1);
            if (bound) n *= Mathf.Max(1, bindCount);
            return Mathf.Clamp(Mathf.RoundToInt(n), 0, Mathf.Max(0, Mathf.RoundToInt(maxCount * (bound ? Mathf.Max(1, bindCount) : 1))));
        }

        public float Scale(int hits, bool bound) => (1 + sizePerHit * (Hits(hits) - 1)) * (bound ? Mathf.Max(1, bindSize) : 1);
        public float Speed(int hits, bool bound) => (1 + speedPerHit * (Hits(hits) - 1)) * (bound ? Mathf.Max(1, bindSpeed) : 1);
        public float BurstSize(int hits, bool bound) => ((burst?.size ?? 0) + burstGrowth * (Hits(hits) - 1)) * (bound ? Mathf.Max(1, bindSize) : 1);
    }
}
