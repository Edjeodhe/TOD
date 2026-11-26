// 24차: 전투 화면 후처리(PostFX.shader)의 세기. 재봉실(로비)은 후처리를 끄므로, 전투에서 캐릭터가 다른 셰이더를 쓴 것처럼 보이지 않게
// 캐릭터 그림을 바꾸는 단계(잉크 외곽선, 색 보정, 종이 결)를 데이터로 조절한다. 값은 씬 GameDatabase.postFx (MCP).
using System;
using UnityEngine;

namespace TOD
{
    [Serializable]
    public class PostFxLook
    {
        [Range(0, 1)] public float ink;      // 잉크 외곽선 (밝기 경계를 어둡게 덧칠, 1 = 예전 세기)
        [Range(0, 1)] public float tone;     // '유령 신부 톤' 색 보정 (채도 낮춤, 청회색 그림자, 1 = 예전)
        [Range(0, 1)] public float paper;    // 종이 얼룩과 섬유결 (1 = 예전)
        [Range(0, 1)] public float vignette; // 가장자리 어둡게 (1 = 예전)
    }
}
