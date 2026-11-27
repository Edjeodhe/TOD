// 25차 HUD: 머리 위 대시 칸, 왼쪽 아래 미니맵 (전투와 재봉실). 값은 씬 GameDatabase.hud (MCP 로 넣음).
using System;
using UnityEngine;

namespace TOD
{
    // 대시 칸을 주인공 머리 위에 '=>' 종이 아이콘 스프라이트로 (tools/bake_icon_dash.py → icon_dash_sheet, 정사각 칸 가로 띠)
    [Serializable] public class DashHeadConfig
    {
        public Sprite[] fill;                 // 빈 칸(0) → 꽉 찬 칸(끝). 차오르는 칸은 충전 비율로 고른다
        public Sprite[] use;                  // 대시를 쓴 순간: 번쩍 → 터짐 → 빈 칸
        public float useTime;                 // use 프레임을 다 도는 시간 (초)
        public float lift;                    // 발밑에서 칸 줄 아래 끝까지 (캔버스 px)
        public float iconSize;                // 칸 하나 크기 (정사각, UI px, 1920x1080 기준)
        public float gap;                     // 칸 사이 (UI px, 음수면 겹침: 칸 그림에 투명 여백이 있어서)
    }

    // 미니맵: 주인공이 늘 가운데, 맵이 움직인다. y 는 화면처럼 바닥 세로 압축(arena.projectionY)을 곱한다.
    [Serializable] public class MinimapConfig
    {
        public Vector2 size;                  // 판 크기 (UI px)
        public float runRange;                // 전투: 판 가로가 보여 주는 바닥 너비 (전투장 단위)
        public float hubRange;                // 재봉실: 판 가로가 보여 주는 바닥 너비 (재봉실 바닥 단위)
        public float heroRadius, npcRadius, enemyRadius;   // 점 반지름 (UI px)
        public float bossRadius, bossRing;    // 보스 = 두꺼운 고리 (반지름, 고리 두께)
        public float dotOutline;              // 점 둘레 어두운 테두리 두께
        public Color hero, npc, enemy, boss, dotEdge;
        public Color background;              // 맵 안쪽 (걸을 수 있는 곳)
        public Color outside;                 // 맵 밖
        public Color bounds;                  // 맵 경계 선
        public float boundsWidth;
    }

    [Serializable] public class HudConfig
    {
        public DashHeadConfig dashHead;
        public MinimapConfig minimap;
        public float yarnFootDrop;            // 25차 3부: 발밑에서 털실 칸 줄 위 끝까지 아래로 (캔버스 px)
    }
}
