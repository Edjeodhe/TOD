// 22차-ui-fixes: 조각 고르기 카드(쉬어가는 라운드 3택, 단추 챙기기 창)가 나타날 때 높은 등급 카드의 번쩍임 값.
// 값은 씬 GameDatabase 의 gear.cardEntrance 에 MCP 로 넣는다. 그리기는 GameUI.Entrance.cs.
using System;
using UnityEngine;

namespace TOD
{
    [Serializable] public class CardEntrance
    {
        public float[] rankStrength;    // 등급(코스트)별 세기, 0 = 번쩍임 없음 (4 레전더리, 5 신화)
        public float delay;             // 창이 열린 뒤 첫 번쩍임까지 (초). 그 전까지 카드는 숨어 있다
        public float stagger;           // 번쩍이는 카드가 여럿이면 하나씩 이만큼 늦게
        public float time;              // 번쩍임 전체 길이 (초)
        public float startScale;        // 나타나는 순간 카드 크기
        public float punch;             // 세기 1일 때 가장 커지는 양 (1 + punch)
        public float punchAt;           // 가장 커지는 때 (전체 길이에 대한 비율)
        public float flashPeak;         // 카드 위 섬광의 처음 불투명도 (세기 1)
        public float haloScale;         // 뒤 빛이 퍼져 끝나는 크기 (처음 크기 대비)
        public float haloPeak;          // 뒤 빛의 처음 불투명도
        public float edgeHold;          // 테두리가 금빛으로 남는 비율 (0~1, 클수록 오래)
        public Color flashColor;        // 섬광 색
        public Color haloColor;         // 뒤 빛과 바느질 고리 색
        public Color edgeColor;         // 금빛 테두리 색
        public string sound;            // 번쩍일 때 효과음 id (sounds), 빈 칸이면 없음
    }
}
