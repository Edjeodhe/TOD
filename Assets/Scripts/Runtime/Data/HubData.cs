// 15차 재봉실 허브 데이터. 값은 씬 GameDatabase 의 hub 에 MCP 로 넣는다 (코드에는 빈 필드만).
// 좌표: 바닥 좌표 = 전투장과 같은 (x 오른쪽 +, y 아래 +) 단위이고 Space.Ground 로 투영한다. 캔버스 px 는 투영 뒤 화면 px.
using System;
using UnityEngine;

namespace TOD
{
    // Moira = 다락 방의 모이라(19차, 17차 '모이라의 실궤' 자리. 강화 4종), AtticExit = 다락 방의 뚜껑문(재봉실로 내려감)
    // SewingBox 는 19차에 아웃게임에서 뺐다(직렬화 번호를 지키려고 값만 남김). 재봉 상자는 등반 중(I)과 단추의 '시작 조각 챙기기'에서만 연다.
    public enum HubStation { SewingBox, Attic, Merchant, Tower, Diary, Moira, AtticExit }

    // 20차 운명 읽기 연출: 파편 불티가 점성구로 빨려 들어감 → 점성구가 탁자에서 화면 가운데로 나옴 → 빛이 퍼짐 → 암시가 어두운 글자로 한 자씩 번짐.
    // 누르거나 Esc(또는 hold 초 뒤)면 빛이 걷히고 점성구가 탁자로 돌아간다. 시간은 초, 크기는 패널 px.
    [Serializable]
    public class FateShow
    {
        public float sparkTime, sparkStagger;   // 불티 하나가 파편 수 → 점성구로 가는 시간, 불티 사이 간격
        public float orbDelay, orbFly;          // 점성구가 떠나기 전 기다림(불티가 다 닿을 때까지), 가운데로 나오는 시간
        public float orbSize;                   // 가운데에서의 점성구 지름
        public Sprite orbSprite;                // 가운데에서 쓸 큰 점성구 그림 (비우면 리그 그림)
        public Sprite orbGlass;                 // 같은 자리의 유리만 흑백 그림 (속성 fateColor 로 물들여 orbSprite 위에 덮음)
        [Range(0, 1)] public float textTint;    // 글자 색을 속성 색의 어두운 판으로 섞는 양
        public Color shadeColor;                // 구 뒤 어두운 그늘 (가장자리를 어둠에 묻어 떠 보이지 않게)
        public float shadeSize;                 // 그늘 지름 (구 지름 배율)
        public float bob;                       // 가운데에서 위아래로 흔들리는 폭 (패널 px)
        [Range(0, 1)] public float orbY;        // 가운데 높이 (화면 높이 비율)
        public float lightDelay, lightTime;     // 점성구가 나온 뒤 빛이 퍼지기 시작할 때까지, 퍼지는 시간
        public float lightSize;                 // 다 퍼졌을 때 빛 지름
        public Color lightColor;                // 빛 색 (속성 hintColor 와 lightTint 만큼 섞음)
        [Range(0, 1)] public float lightTint;
        [Range(0, 1)] public float dim;         // 방을 어둡게
        public float textDelay;                 // 빛이 퍼지기 시작한 뒤 글자가 나오기 시작할 때까지
        public float charTime, charFade;        // 글자 사이 간격, 한 글자가 짙어지는 시간
        public Color textColor;                 // 어두운 글자 색
        [Range(0, 1)] public float textY;       // 글 높이 (화면 높이 비율)
        public float hold;                      // 글이 다 나온 뒤 저절로 닫히기까지 (0 = 누를 때까지)
        public float closeTime;                 // 닫힐 때 빛이 걷히고 구가 탁자로 돌아가는 시간
        public string skipHint;                 // 아래 안내 ("눌러서 닫기")
        public string lightSound;               // 빛이 퍼질 때 효과음 id (SoundBank)
    }

    // 20차 칸 강화, 확장 연출: 빛나는 실 머리가 그 줄의 첫 칸에서 끝 칸까지 달리며 실을 잇는다.
    // 확장이면 머리가 지나는 칸이 하나씩 튀어나오고, 칸 강화면 금빛 띠가 머리를 따라 채워진다. 칸마다 번쩍임.
    // 시간은 초, 크기는 칸 크기(pitch)에 대한 비율.
    [Serializable]
    public class LineGrow
    {
        public float startDelay;               // 판이 열린 뒤 기다림
        public float cellTime;                 // 머리가 칸 하나를 지나는 시간
        public float cellPop;                  // 새 칸이 튀어나오는 시간 (확장)
        public float flashTime;                // 칸 번쩍임 시간
        public float fadeTime;                 // 끝난 뒤 실과 머리가 사라지는 시간
        public float threadWidth, headSize, flashSize;
        public Color threadColor, headColor, flashColor;
        public string stepSound, endSound;     // 칸마다, 끝날 때 효과음 id
    }

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
        public Vector2 shadowSize;            // 20차 발밑 그림자 타원 지름 (캔버스 px). 0 = 없음
        public Vector2 shadowOffset;          // 그림자 중심: 그림 아래 가운데에서 (캔버스 px, y 아래 +)
        public float brightness;              // 20차 이 시설만 더 곱하는 밝기 (0 = 1). 다시 그리지 않은 그림(상인 단추)을 방에 맞출 때
    }

    // 20차: 시설 그림을 방 빛에 맞추는 값 (모든 시설에 곱함. 모이라는 MoiraRig 의 값)
    [Serializable] public class FacilityLook
    {
        public Color tint;                    // 그림에 곱하는 색 (방의 보랏빛 어둠)
        [Range(0, 2)] public float saturation, brightness;
        public Color shadowColor;             // 발밑 그림자 색 (알파 = 진하기)
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
        public FacilityLook look;             // 20차 시설 그림 색 맞추기, 발밑 그림자 색
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

        [Header("19차 상인 단추 메뉴: 시작 조각 챙기기, 들고 갈 수 늘리기")]
        public Sprite packIcon;               // '시작 조각 챙기기' 카드 그림
        public string packName, packText;     // 카드 이름, 설명
        public string packFoot;               // 카드 아랫줄 ({0} 챙긴 수, {1} 들고 갈 수)
        public string packTitle, packHelp;    // 챙기기 판 제목, 안내 줄
        public string packNote;               // 판 위 안내 ({0} 챙긴 수, {1} 들고 갈 수, {2} 파편)
        public string packed, unpacked;       // "{0}을 챙겼습니다", "{0}을 돌려주고 파편 {1}개를 받았습니다"
        public string unpackLabel;            // 판에서 빼기 버튼 (챙기기 판)
        public string slotEyebrow, slotTitle, slotConfirm;   // '들고 갈 수 늘리기' 확인 창

        [Header("19차 모이라 (다락)")]
        public Sprite moiraPortrait;          // 모이라 창 왼쪽 그림
        public string moiraEyebrow, moiraTitle;
        public string[] moiraLines;           // 말을 걸 때마다 무작위 한 줄
        public string moiraEmpty;             // 강화가 모두 끝났을 때
        public string pickRows, pickCols, pickExpand;        // 판 위 안내 (줄 고르기)
        public string pickTitle;              // 고르기 판 제목 ({0} 강화 이름)
        public string confirmRow, confirmCol, confirmExpand; // 확인 문구 ({0} 줄 번호 또는 변 이름, {1} 파편, {2} 강화 이름)
        public string confirmPlain;           // 위치 없는 강화(욕망 최대치) 확인 ({0} 파편, {1} 강화 이름)
        public string[] sideNames;            // 위, 오른쪽, 아래, 왼쪽
        public string lineTaken;              // 이미 강화한 줄을 골랐을 때
        public string moiraEyebrowConfirm, moiraTitleConfirm, moiraConfirmButton;   // 확인 창 머리 (예: "모이라의 의식", "미래를 읽을까요?")
        public string pickLines, confirmLine; // 칸 강화 판 안내, 확인 문구 ({0} "위에서 3번째 가로 줄" 같은 줄 이름, {1} 파편, {2} 강화 이름)
        public string rowName, colName;       // 줄 이름 ({0} 번호): "위에서 {0}번째 가로 줄", "왼쪽에서 {0}번째 세로 줄"

        [Header("19차 모이라 카드 (하스스톤식 고르기)")]
        public Texture2D cardFront, cardBack; // 카드 앞(빈 창, 이름 띠), 뒤
        public float cardDelay;               // 말을 건 뒤 모이라가 카드를 드는 동안 기다리는 시간 (초)
        public float cardFly, cardStagger;    // 카드가 손에서 자리로 날아가는 시간, 카드 사이 간격 (초)
        public string cardLocked;             // (안 씀) 파편이 모자랄 때 카드 아랫줄
        public string cardLevel;              // 카드 위 단계 꼬리표 ({0} 지금 단계), 예: "{0}단계"
        public string cardPrice;              // 카드 아래 가격 ({0} 파편), 예: "파편 {0}개"
        public string cardHint;               // 카드 아래 안내 ("카드를 고르세요   Esc 그만두기")

        [Header("19차 운명 읽기 (네 번째 카드)")]
        public Sprite fateIcon;
        public string fateName, fateText;     // 카드 이름, 설명
        public int fatePrice;                 // 기억의 파편
        public string fateFoot, fateReadFoot; // 카드 아랫줄 ({0} 파편), 이미 읽었을 때 ("다시 듣기")
        public string fateConfirm;            // 확인 문구 ({0} 파편)
        public string fateIntro;              // 모이라가 암시 앞에 붙이는 말 (비우면 암시만)
        public FateShow fateShow;             // 20차 운명 읽기 연출 (GameUI.FateShow.cs)
        public LineGrow lineGrow;             // 20차 칸 강화, 확장 연출 (GameUI.Board.cs)

        [Header("안내")]
        public string hint;                   // 화면 아래 조작 안내
    }
}
