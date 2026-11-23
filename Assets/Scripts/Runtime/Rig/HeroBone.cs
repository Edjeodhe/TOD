// 주인공 뼈 하나 (MCP 로 만든 Arena/Hero/Rig/.../<뼈 id>). 자식 Art 가 그림이며, 그림의 pivot 이 뼈 원점에 오도록 놓여 있다.
using UnityEngine;

namespace TOD
{
    public class HeroBone : MonoBehaviour
    {
        public string id;
        public Vector2 pivot;                 // 그림 안의 회전 중심 (그림 px, 왼쪽 위 기준)
        public float rest;                    // 기본 각도 (캔버스 기준 도)
        [Range(0, 1)] public float shade;     // 뒤쪽 팔다리 그늘 (단색 덮기 양)
        public SpriteRenderer art;
    }
}
