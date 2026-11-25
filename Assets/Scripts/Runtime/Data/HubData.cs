// 15차 재봉실 허브 데이터. 값은 씬 GameDatabase 의 hub 에 MCP 로 넣는다 (코드에는 빈 필드만).
// 좌표: 바닥 좌표 = 전투장과 같은 (x 오른쪽 +, y 아래 +) 단위이고 Space.Ground 로 투영한다. 캔버스 px 는 투영 뒤 화면 px.
using System;
using UnityEngine;

namespace TOD
{
    // Moira = 다락 방의 모이라(19차, 17차 '모이라의 실궤' 자리. 강화 4종), AtticExit = 다락 방의 뚜껑문(재봉실로 내려감)
    // SewingBox 는 19차에 아웃게임에서 뺐다(직렬화 번호를 지키려고 값만 남김). 재봉 상자는 등반 중(I)과 단추의 '시작 조각 챙기기'에서만 연다.
    // 23차: Npc* = 쉬어가기에서 만난 NPC 4종의 재봉실 자리 (만난 NPC 만 보인다. NpcDef.station)
    public enum HubStation { SewingBox, Attic, Merchant, Tower, Diary, Moira, AtticExit, NpcBlessing, NpcGift, NpcMemory, NpcGamble }

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

        [Header("22차 미래 보기 (점성구 → 파편 → 자세히 살펴보기 E → 모이라가 구슬을 만지고 빨려 드는 줌 → 구슬 속 영상과 자막)")]
        public float riseTime;                  // 탁자 점성구가 떠오르며 죄종 색으로 빛나는 시간
        public float liftHeight;                // 떠오르는 높이 (그림 px)
        public float sparkDelay;                // 떠오르기 시작한 뒤 파편 불티가 출발할 때까지
        public float promptDelay;               // 불티가 다 닿은 뒤 "자세히 살펴보기 (E)" 가 뜰 때까지
        public string promptText;               // "자세히 살펴보기 (E)"
        public float promptLift;                // 안내가 점성구 위로 떨어진 높이 (패널 px)
        public float touchLead;                 // E 뒤 모이라가 구슬을 감싸 쥐는 동안 기다림 (줌 시작 전)
        public float zoomTime;                  // 구슬로 빨려 드는 줌 시간
        [Range(.2f, 1.2f)] public float zoomFill;   // 줌 끝에 구슬 반지름이 화면 반 높이에서 차지하는 비율
        public Vector2 zoomOffset;              // 줌 끝 카메라 중심을 구슬 중심에서 옮김 (구슬 반지름 배율, +y 위)
        public float swirlStrength;             // 줌 동안 둘레가 빨려 드는 일그러짐 최대 세기
        public float swirlRest;                 // 줌이 끝난 뒤 남는 일그러짐
        [Range(0, 1)] public float tintAmount;  // 구슬 바깥 주변을 죄종 색으로 물들이는 양
        public float videoFade;                 // 영상이 구슬 안에 번져 나오는 시간
        public float videoTime;                 // 24차: 반복 그림이 도는 동안 자막이 한 번 퍼지는 시간 (이 뒤 넘어가기 안내). 반복 그림이 없는 영상은 영상 길이
        public float lineFade;                  // 자막 한 줄이 나오고 사라지는 시간
        public float lineTime;                  // 자막 한 줄이 머무는 시간 (줄이 많으면 영상이 끝난 뒤 마지막 장면에 멈춘 채 이어진다)
        public float subtitleY;                 // 자막 높이 (화면 높이 비율)
        public float endHold;                   // (24차부터 안 씀: 구슬 속 그림은 끝없이 돌고 E 로만 넘어감)
        public float zoomOutTime;               // 돌아갈 때 줌이 풀리는 시간
        public string riseSound, zoomSound;     // 효과음 id (점성구가 떠오를 때, 빨려 들 때)

        [Header("22차 미래 보기 2차: 정면 가속 줌, 확대 뒤 완전 정지 (swirlStrength = 구슬 쪽으로 당기는 세기, 회전 없음)")]
        public float zoomPower;                 // 줌 가속 (지수 이즈인. 1 = 고르게, 3 = 점점 빨라짐)
        public float rushBlur;                  // 줌 속도에 맞춰 구슬 바깥이 방사형으로 번지는 최대 양 (반지름 비율)
        public float rushFade;                  // 줌이 멈춘 뒤 당김과 번짐이 걷히는 시간
        public float freezeTime;                // 줌 시작 뒤 모이라(손, 몸, 숨쉬기)가 서서히 멈추기까지 (영상 동안은 완전히 정지)
    }

    // 20차 칸 강화, 확장 연출: 빛나는 실 머리가 그 줄의 첫 칸에서 끝 칸까지 달리며 실을 잇는다.
    // 확장이면 머리가 지나는 칸이 하나씩 튀어나오고, 칸 강화면 그 칸의 "+N" 숫자가 하나 올라 튀어나온다(21차). 칸마다 번쩍임.
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

    // 22차 욕망 최대치 강화 연출 (GameUI.Vessel.cs): 모이라 카드 위에 욕망 게이지가 나타나고,
    // 게이지 틀이 이전 최대치 길이에서 새 최대치 길이로 자라며 새로 생긴 자리가 밝은 욕망으로 차오른다. 숫자도 함께 오른다.
    // 길이(px) = barBase + (최대치 - desire.maxBase) * unitPx (barMax 까지). 시간은 초.
    [Serializable]
    public class VesselShow
    {
        public float barBase, unitPx, barMax;  // 게이지 틀 길이
        public float appear;                   // 게이지가 나타나는 시간
        public float growDelay, grow;          // 나타난 뒤 기다림, 틀이 자라는 시간
        public float fill;                     // 자란 자리가 차오르는 시간
        public float lineDelay, lineGap;       // 틀이 자라기 시작한 뒤 첫 문구까지, 문구 사이
        public float hold, fade;               // 다 찬 뒤 머무는 시간, 사라지는 시간
        public Color newColor, glintColor;     // 새로 생긴 자리 색, 자라는 끝의 빛
        public string growSound, fullSound;    // 효과음 id (자라기 시작, 다 참)
    }

    // 22차 인벤토리 확장 알림 (GameUI.Stamp.cs): 찢어진 종이 쪽지에 문구가 쓰이고, 오른쪽 아래에 붉은 인주 도장 자국이 '쿵' 박힌다.
    [Serializable]
    public class StampShow
    {
        public Texture2D mark;                 // 도장 자국 그림 (Assets/UI/Paper/stamp_seal.png)
        public float appear;                   // 쪽지가 내려앉는 시간
        public float stampDelay;               // 쪽지가 앉은 뒤 도장이 찍히기까지
        public float punchFrom, punchTime;     // 도장 자국이 처음 나타나는 크기(배), 제자리로 눌리는 시간
        public float press;                    // 찍힐 때 쪽지가 눌리는 정도 (크기 비율)
        public float shake, shakeTime;         // 찍힐 때 쪽지 흔들림 (px), 시간
        public float hold, fade;               // 찍힌 뒤 머무는 시간, 사라지는 시간
        public string sound;                   // 쿵 효과음 id
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
        [Header("드리운 그림자 (방 뒤쪽 등불에서 앞으로 떨어짐)")]
        public Color castColor;               // 그림자 색 (알파 = 발밑에서의 진하기)
        public float castLength;              // 그림 높이에 대한 그림자 길이 비율 (0 = 없음)
        public float castFade;                // 끝으로 갈수록 옅어지는 정도 (지수)
        public float castBlur;                // 끝으로 갈수록 흐릿한 정도 (그림 uv)
        public float castLean;                // 그림자가 방 가운데에서 바깥으로 기우는 정도 (가로 이동 / 길이)
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
        public string unpackLabel;            // (23차부터 안 씀) 판에서 빼기 버튼 → 판 위 조각 우클릭
        public string slotEyebrow, slotTitle, slotConfirm;   // '퍼즐 칸 추가'(옛 들고 갈 수 늘리기) 확인 창

        [Header("20차 시작 조각 챙기기: 무작위 진열에서 카드로 고르기 (칸 수 한도)")]
        public int packPicks;                 // 한 진열에서 고를 수 있는 조각 수
        public float shopRefreshHours;        // 진열이 새로 바뀌는 간격 (실제 시각, 시간)
        public string packCardsEyebrow, packCardsTitle;
        public string packStatus;             // 카드 창 위 줄 ({0} 고른 수, {1} 고를 수, {2} 쓴 칸, {3} 챙길 칸, {4} 새 진열까지 남은 시간)
        public string packTimeFormat;         // 남은 시간 ({0} 시간, {1} 분)
        public string packTaken;              // 고른 카드 아래 꼬리표
        public string packPrice;              // 고르지 않은 카드 아래 꼬리표 ({0} 파편, {1} 칸 수)
        public string packTooBig;             // 칸이 모자랄 때 ({0} 쓴 칸 + 이 조각, {1} 챙길 칸)
        public string packLimit;              // 이번 진열에서 더 못 고를 때 ({0} 고를 수)
        public string packReturned;           // 되돌렸을 때 ({0} 이름, {1} 돌려받은 파편)
        public string packBoardButton;        // 카드 창의 '재봉 상자에 놓기 (I)' 버튼

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
        [Header("24차 예언 카드 (재봉실 캐릭터 옆, GameUI.FateBadge.cs)")]
        public string fateBadgeTag;           // 카드 위 꼬리표 (예: "예언")
        public string fateBadgeText;          // 아래 설명 ({0} 세트 이름, {1} 오른 비율 %, 예: "세트 조각 확률 +{1}%")
        public Vector2 fateBadgeOffset;       // 캐릭터 발밑 기준 카드 왼쪽 아래 자리 (캔버스 px, +x 오른쪽, +y 아래)
        public float fateBadgePop;            // 처음 뜰 때 튀어나오는 시간 (초)
        public FateShow fateShow;             // 20차 운명 읽기 연출 (GameUI.FateShow.cs)
        public LineGrow lineGrow;             // 20차 칸 강화, 확장 연출 (GameUI.Board.cs)

        [Header("22차 욕망 최대치 강화 연출 (GameUI.Vessel.cs)")]
        public string vesselTitle;            // 게이지 위 제목 한 줄 ({0} 이전 최대치, {1} 새 최대치, {2} 늘어난 양)
        public string[] vesselLines;          // 게이지 아래 감성 문구, 한 줄씩 차례로 (자리는 vesselTitle 과 같음, 2줄 권장)
        public string vesselValue;            // 게이지 옆 숫자 ({0} 지금 올라가는 최대치), 예: "{0}"
        public VesselShow vesselShow;

        [Header("22차 인벤토리 확장 알림 (GameUI.Stamp.cs)")]
        public string expandStamp;            // 모이라 '인벤토리 확장' 뒤 쪽지 문구 (한 줄, {0} = 늘어난 칸 수, 예: "칸이 {0}칸 확장되었습니다")
        public string slotStamp;              // 단추 '퍼즐 칸 추가' 뒤 쪽지 문구 (비우면 expandStamp)
        public StampShow stampShow;

        [Header("안내")]
        public string hint;                   // 화면 아래 조작 안내
    }
}
