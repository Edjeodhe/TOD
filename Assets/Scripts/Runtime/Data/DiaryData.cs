// 15차: 아버지의 일기 데이터. 값은 씬 GameDatabase 의 diary 에 MCP 로 넣는다 (코드에는 빈 필드만).
using System;
using UnityEngine;

namespace TOD
{
    // 일기 조각 하나 = 층 하나. floor 층을 처음 돌파하면 풀린다.
    [Serializable] public class DiaryEntry
    {
        public int floor;
        public string title;                  // 쪽지 머리 (예: "첫째 장")
        [TextArea(2, 8)] public string[] lines;   // 본문 줄 (4~5줄)
        public string absorbedLine;           // 그 층 보스 욕망을 흡수했을 때의 마지막 줄
        public string refusedLine;            // 거절했을 때의 마지막 줄
        public float tilt;                    // 게시판에 꽂힌 기울기 (도)
        public int readShards;                // 17차: 처음 읽으면 받는 기억의 파편
        public int patternKind = -1;          // 17차: 처음 읽으면 풀리는 '아버지의 도안'(전용 시작 조각) 종류, -1 = 없음
        [Range(0, 1)] public float blood;     // 18차: 쪽지에 묻은 피 (0 깨끗 ~ 1 흠뻑). 핏자국 수와 진하기
        public Color ink;                     // 18차: 본문 잉크 색 (알파 0 = USS 기본색). 뒤로 갈수록 핏빛
    }

    [Serializable] public class DiaryConfig
    {
        public DiaryEntry[] entries;
        public string boardTitle, boardSubtitle;   // 게시판 머리글
        public string lockedText;                  // 찢긴 빈 쪽지 문구 ({0} = 층)
        public string newTag;                      // 새 조각 표시
        public string undecidedLine;               // 아직 흡수/거절을 고르지 않은 층의 마지막 줄
        public string closeText, readHint;         // 닫기 버튼, 쪽지를 누르라는 안내
        public string noticeText;                  // 새 조각 알림 ({0} = 새로 찾은 조각 수)
        public string lobbyButton;                 // 허브 전 임시 로비 버튼 문구
        public bool showLobbyButton;               // 허브 게시판이 ShowDiary 를 부르면 끈다
        public float noticeDuration;               // 알림이 떠 있는 초
        public Color absorbedInk, refusedInk, undecidedInk;   // 마지막 줄 잉크 색
        public string rewardShards;                // 17차: 처음 읽었을 때 알림 ({0} = 파편)
        public string rewardPattern;               // 17차: 도안이 풀렸을 때 알림 ({0} = 조각 이름)
        public string rewardLine;                  // 17차: 쪽지 아래 보상 줄 ({0} = 파편, {1} = 도안 이름)
        [Header("18차 핏자국")]
        public Sprite[] bloodStains;               // 핏자국 그림 (Assets/UI/Paper/blood_*.png)
        public int bloodMaxStains;                 // blood 1 일 때 크게 읽기 쪽지의 핏자국 수 (게시판 쪽지는 절반)
        public Vector2 bloodSize;                  // 크게 읽기 쪽지의 핏자국 크기 범위 (패널 px, 최소~최대)
        public Vector2 bloodAlpha;                 // 핏자국 진하기 범위 (blood 0 → x, 1 → y)
        public Color bloodTint;                    // 핏자국에 곱하는 색
    }
}
