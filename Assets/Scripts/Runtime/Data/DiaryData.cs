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
    }
}
