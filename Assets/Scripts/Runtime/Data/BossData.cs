// 23차-boss7: 칠죄종 보스 7종 데이터 (종류, 패턴, 2페이즈). 값은 씬 GameDatabase 의 bosses 에 MCP 로 넣는다.
// 시뮬레이션은 Sim/CombatBoss.cs, 그리기는 View/ArenaRenderer.Boss.cs.
//
// 패턴(BossMove) 종류와 필드의 뜻:
//   Slam   = 주인공 자리에 원 (radius). count 번 연타, interval 초 간격. lead = 주인공 이동을 앞질러 노리는 정도(초)
//   Cone   = 보스 앞 부채꼴 (radius, width = 각도°). count 번이면 spread° 씩 돌려 가며
//   Charge = 주인공 쪽 직선 (radius = 길이, width = 폭) 예고 뒤 speed 로 돌진, 몸에 닿으면 피해. count 번 연속
//   Nova   = 보스 둘레 원 (radius). inner > 0 이면 안쪽이 안전한 고리
//   Rain   = 주인공 둘레 spread 안에 원 count 개 (radius), interval 초씩 엇갈려 터짐
//   Lines  = 보스에서 뻗는 직선 count 갈래 (radius = 길이, width = 폭, spread = 갈래 사이 각도°). 360 이면 고르게
//   Volley = 탄 count 발 (spread = 부채 각도°, 360 = 둘레), speed = 탄 속도. 예고는 가는 선
//   Leap   = 주인공 자리 원 (radius) 으로 뛰어올라 내려앉음 (보스가 그 자리로 옮겨 감)
//   Pull   = speed 만큼 주인공을 보스 쪽으로 끌어당긴 뒤 보스 둘레 원 (radius, inner)
using System;
using UnityEngine;

namespace TOD
{
    public enum BossMoveKind { Slam, Cone, Charge, Nova, Rain, Lines, Volley, Leap, Pull }

    [Serializable] public class BossMove
    {
        public string name;                   // 패턴 이름 (HANDOFF 기록과 개발자 확인용, 화면에는 안 띄움)
        public BossMoveKind kind;
        public float windup;                  // 예고 시간 (초) — 바닥 표시가 차오르는 동안 피할 수 있다
        public float damage;                  // 보스 피해 배율 (BossDef.damage × 층 배율 × 이것)
        public float radius;                  // 원·부채꼴 반지름, 직선 길이 (전투장 px)
        public float inner;                   // 고리의 안전한 안쪽 반지름 (0 = 꽉 찬 원)
        public float width;                   // 직선 폭 (px) / 부채꼴 각도 (°)
        public int count;                     // 반복 수, 개수, 갈래, 탄 수 (0 이면 1)
        public float interval;                // 반복 사이 간격, 엇갈림 (초)
        public float spread;                  // 흩어짐 반경 (Rain), 갈래 사이 각도 (Lines, Cone), 부채 각도 (Volley)
        public float speed;                   // 돌진 속도 (Charge), 탄 속도 (Volley), 끌어당김 거리 (Pull)
        public float lead;                    // 주인공 이동 앞지르기 (초)
        public float knock;                   // 맞으면 밀려나는 거리 (px)
        public float rest;                    // 패턴이 끝난 뒤 숨 고르기 (초)
    }

    [Serializable] public class BossDef
    {
        public Element element;
        public string name;                   // 보스 HUD 이름 (예: "금관 오우거 맘몬")
        public string phase2Name;             // 2페이즈 이름 (비면 name)
        [TextArea] public string roarLine;    // 2페이즈 포효 때 띠 문구 (비면 안 띄움)
        public string rig;                    // MonsterRig.type (Arena/Enemies/Rigs/Boss_<죄종>) — 비면 공용 보스 리그
        [Header("1층 기준 (층마다 enemies.hpPerFloor·damagePerFloor·speedPerFloor 배율)")]
        public float hp;
        public float damage, speed, height, radius, preferredDistance;
        [Header("2페이즈")]
        public float phase2Hp;                // 2페이즈 체력 = hp × 이것
        public float phase2Speed;             // 이동 배율
        public float phase2Tempo;             // 예고·숨 고르기 시간 배율 (1 보다 작으면 빨라짐)
        public string[] phase2Summons;        // 포효 뒤 부르는 졸개 (EnemyType.id)
        [Header("패턴: 1페이즈 moves, 2페이즈 moves2 (같은 패턴을 두 번 연달아 쓰지 않는다)")]
        public BossMove[] moves;
        public BossMove[] moves2;
        public float idle;                    // 패턴 사이에 걸어 다니는 시간 (초)
        public Color telegraph;               // 예고 색 (죄종 색)
    }

    [Serializable] public class BossConfig
    {
        public BossDef[] defs;
        [Header("2페이즈 전환: 경직 → 포효(넉백) → 체력바가 다시 차오름. 전환 중 무적")]
        public float staggerTime;             // 경직 (초)
        public float roarTime;                // 포효 자세 (초)
        public float refillTime;              // 체력바가 차오르는 시간 (포효와 함께 시작)
        public float roarRadius;              // 넉백 반경 (px)
        public float roarKnock, roarKnockTime;   // 밀려나는 거리 (px), 시간 (초)
        public float roarShake, roarFlash;    // 화면 흔들림, 번쩍임 (초)
        public Color roarFlashTint;
        public float staggerJolt;             // 경직 중 몸 떨림 (px)
        public Color roarColor;               // 충격파 고리 색
        public float roarWaveTime;            // 충격파 고리가 퍼지는 시간 (초)
        public int roarWaves;                 // 충격파 고리 겹 수
        [Header("예고 그리기")]
        public float edgeWidth;               // 테두리 두께 (px)
        public float edgeAlpha, fillAlpha;    // 테두리, 차오르는 면 알파
        public float baseAlpha;               // 아직 안 찬 면 알파
        public float flashTime;               // 터진 뒤 번쩍 남는 시간 (초)
        public float bandWidth;               // 부채꼴, 고리를 채우는 띠 간격 (px)
        public float hitShake;                // 터질 때 화면 흔들림 (초)
        public float chargePad;               // 돌진 몸 닿음 여유 (px)
        public float leapHeight;              // 뛰어오르기 최고 높이 (px, 그리기)

        public BossDef Def(Element e) => defs == null ? null : Array.Find(defs, d => d != null && d.element == e);
    }
}
