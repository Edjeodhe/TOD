// 15차 재봉실 허브 데이터. 값은 씬 GameDatabase 의 hub 에 MCP 로 넣는다 (코드에는 빈 필드만).
// 좌표: 바닥 좌표 = 전투장과 같은 (x 오른쪽 +, y 아래 +) 단위이고 Space.Ground 로 투영한다. 캔버스 px 는 투영 뒤 화면 px.
using System;
using UnityEngine;

namespace TOD
{
    // Chest = 17차 다락 방의 '모이라의 실궤'(강화 5종), AtticExit = 다락 방의 뚜껑문(재봉실로 내려감)
    public enum HubStation { SewingBox, Attic, Merchant, Tower, Diary, Chest, AtticExit }

    // 17차: 걸어 다니는 방 하나 (재봉실 본방은 HubConfig 의 옛 필드를 그대로 쓰고, 다락 방은 HubConfig.attic)
    [Serializable] public class HubRoom
    {
        public string title, subtitle;        // 위 띠 방 이름 (예: "예견의 다락", "모이라의 방")
        public Sprite backdrop;
        public Rect backdropRect;             // 배경을 깔 캔버스 px 영역 (카메라도 이 안에 머문다)
        public Rect walk;                     // 걸을 수 있는 바닥 좌표 영역
        public Vector2 heroStart;             // 방에 들어온 자리
        public float cameraZoom;
        public HubFacility[] facilities;
        public string hint;                   // 화면 아래 조작 안내
    }

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
        public string title, subtitle;        // 17차: 재봉실 위 띠 이름 (비면 UXML 그대로)

        [Header("17차 다락 방")]
        public HubRoom attic;
        public Vector2 atticReturn;           // 다락에서 내려오면 서는 재봉실 바닥 자리
        public float roomFade;                // 방을 오갈 때 어두워지는 시간 (초, 반쪽)
        public string roomSound;              // 방을 오갈 때 효과음 id

        [Header("상인 단추 조각 상점 (기억의 파편)")]
        public int shopStock;                 // 진열 수
        public int[] piecePrices;             // 등급 코스트(1~4) → 파편 가격 (0번 미사용)
        public string shopTitle, shopText;
        public string shopEmpty;              // 더 팔 조각이 없을 때
        public string soldOut, priceFormat;   // "팔림", "파편 {0}"
        public string noShards;               // 파편이 모자랄 때
        public string bought;                 // "{0}을 샀습니다"
        public string buySound;
        public string patternTag;             // 17차: 진열 카드의 '아버지의 도안' 표시

        [Header("안내")]
        public string hint;                   // 화면 아래 조작 안내
    }
}
