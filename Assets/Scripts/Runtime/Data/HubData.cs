// 15차 재봉실 허브 데이터. 값은 씬 GameDatabase 의 hub 에 MCP 로 넣는다 (코드에는 빈 필드만).
// 좌표: 바닥 좌표 = 전투장과 같은 (x 오른쪽 +, y 아래 +) 단위이고 Space.Ground 로 투영한다. 캔버스 px 는 투영 뒤 화면 px.
using System;
using UnityEngine;

namespace TOD
{
    public enum HubStation { SewingBox, Attic, Merchant, Tower, Diary }

    [Serializable] public class HubFacility
    {
        public HubStation station;
        public string name;                   // 시설 이름 (꼬리표)
        public string prompt;                 // E 옆 안내 문구 (예: "재봉 상자 열기")
        public Vector2 position;              // 바닥 좌표: 그림 아래 가운데가 닿는 자리
        public Sprite sprite;
        public float height;                  // 그림 높이 (캔버스 px)
        public bool flip;
        public float radius;                  // 상호작용 거리 (바닥 좌표, 0 = hub.interactRadius)
        public float promptLift;              // 꼬리표를 그림 위쪽 끝에서 몇 px 위에 (캔버스 px)
        public Vector2 block;                 // 걸어서 못 들어가는 바닥 타원 반지름 (x, y). 0 = 막지 않음
        public Vector2 blockOffset;           // 막는 타원 중심 (position 기준)
    }

    [Serializable] public class HubConfig
    {
        [Header("방")]
        public Sprite backdrop;               // 재봉실 배경 한 장
        public Rect backdropRect;             // 배경을 깔 캔버스 px 영역 (카메라도 이 안에 머문다)
        public Rect walk;                     // 걸을 수 있는 바닥 좌표 영역
        public Vector2 heroStart, returnSpot; // 처음 자리, 등반에서 돌아온 자리
        public float walkSpeed, verticalFactor, cameraZoom;
        public float interactRadius;
        public float highlight;               // 가까운 시설 그림 밝기 배율 (1 = 그대로)
        public HubFacility[] facilities;

        [Header("상인 단추 조각 상점 (기억의 파편)")]
        public int shopStock;                 // 진열 수
        public int[] piecePrices;             // 등급 코스트(1~4) → 파편 가격 (0번 미사용)
        public string shopTitle, shopText;
        public string shopEmpty;              // 더 팔 조각이 없을 때
        public string soldOut, priceFormat;   // "팔림", "파편 {0}"
        public string noShards;               // 파편이 모자랄 때
        public string bought;                 // "{0}을 샀습니다"
        public string buySound;

        [Header("안내")]
        public string hint;                   // 화면 아래 조작 안내
    }
}
