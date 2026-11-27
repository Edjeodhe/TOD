// 26차 아래 대화창 (NPC 에게 E 로 말을 걸면). 왼쪽 = 주인공 얼굴, 오른쪽 = 말하는 NPC 얼굴, 가운데 아래 = 종이 대화 상자.
// 값은 씬 GameDatabase.talk 에 MCP 로 넣는다 (코드에는 빈 필드만). NPC 얼굴은 NpcDef.portrait.
// UI 는 Main.uxml 의 dialog-talk, 모양은 UI/Talk.uss, 채우기는 GameUI.Talk.cs, 열고 닫기는 GameController.Npc.cs.
using System;
using UnityEngine;

namespace TOD
{
    [Serializable] public class TalkConfig
    {
        public Sprite heroPortrait;           // 왼쪽 주인공 얼굴
        public Sprite paper;                  // 대화 상자 종이 (비우면 USS 기본 그림)
        public Sprite nameTag;                // 이름표 종이 (비우면 USS 기본 그림)
        public float charTime;                // 글자 사이 간격 (초). 0 이면 npcs.charTime
        public float portraitPop;             // 얼굴이 아래에서 올라오는 시간 (초)
        public float portraitRise;            // 올라오는 거리 (UI px)
        [Range(0, 1)] public float listenerDim;   // 듣는 쪽(주인공) 얼굴 밝기
        [Range(0, 1)] public float listenerScale; // 듣는 쪽 얼굴 크기 배율
        public float talkBob;                 // 말하는 동안 얼굴이 들썩이는 높이 (UI px)
        public float talkBobSpeed;            // 들썩임 빠르기 (초당 회)
        public string nextHint;               // 다 찍힌 뒤 안내 (예: "E 닫기")
        public string skipHint;               // 찍히는 동안 안내 (예: "E 넘기기")
    }
}
