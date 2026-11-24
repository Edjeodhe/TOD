// 23차 쉬어가기 NPC 4종 ('바느질 요괴들') 데이터. 값은 씬 GameDatabase 의 npcs 에 MCP 로 넣는다 (코드에는 빈 필드만).
//   쉬어가기 라운드마다 chance 확률로 한 명이 나온다(weight 가중, 아직 못 만난 NPC 는 unmetWeight 배). 다가가 E 로 말을 걸면 대사와 효과.
//   한 번 만난 NPC 는 Profile.npcMet 에 남아 재봉실(허브)에 나오고, 등반마다 한 번 hubRewards 중 하나를 준다(Profile.npcGifted).
//   그림은 리깅 페이지(tools/rig_editor/rig.html)가 저장한 Assets/Rig/npc_<id>.json 과 Assets/sprites/npc_<id>_*.png (CutoutRig 가 그대로 읽음).
using System;
using UnityEngine;

namespace TOD
{
    // 축복(수녀 재봉사), 조각 선물(행상 쥐), 경험치(촛불 유령), 도박 거래(단추 눈 인형)
    public enum NpcKind { Blessing, Gift, Memory, Gamble }

    // Effects = 능력치 효과(증강과 같은 계산, 등반 동안 영구), Piece = 무작위 조각, Xp = 경험치(amount = 지금 레벨 필요량 배율),
    // Gold = 골드, Shards = 기억의 파편, Heal = 체력 회복(amount = 최대 체력 비율), Hurt = 체력 잃음(amount = 최대 체력 비율, 1 아래로는 안 내려감),
    // Boon = (재봉실) 다음 등반 시작 때 effects 를 건다, Nothing = 아무것도 없음
    public enum NpcRewardKind { Effects, Piece, Xp, Gold, Shards, Heal, Hurt, Boon, Nothing }

    [Serializable] public class NpcReward
    {
        public NpcRewardKind kind;
        public float weight;                  // 고를 때 가중치
        public float amount;                  // 종류마다 뜻이 다름 (위 주석)
        public AugmentEffect[] effects;       // Effects, Boon
        public string name;                   // 축복 이름 (예: "바늘의 축복") — 알림 {0}
        public string notice;                 // 받았을 때 알림 ({0} 이름 또는 조각 이름, {1} 수치)
        public string line;                   // 이 보상을 줄 때 NPC 가 하는 말 (비우면 NpcDef.giveLines 에서)
        public bool bad;                      // 나쁜 결과 (도박): 효과음과 알림 색
    }

    [Serializable] public class NpcDef
    {
        public NpcKind kind;
        public string id;                     // 리그 파일 이름 (npc_<id>.json)
        public string name;                   // 꼬리표 이름 (예: "눈먼 수녀 재봉사")
        public string prompt;                 // E 안내 (예: "말 걸기")
        public float weight;                  // 쉬어가기 등장 가중치
        public TextAsset rig;                 // 리깅 페이지 JSON (Assets/Rig/npc_<id>.json)
        public float height;                  // 그림 높이 (캔버스 px). 리그 height 에 맞춰 배율을 정한다
        public float bubbleLift;              // 말풍선을 머리 위로 띄우는 높이 (캔버스 px)

        [Header("쉬어가기")]
        public string[] approachLines;        // 가까이 오면 혼잣말 (처음 한 번)
        public string[] giveLines;            // 효과를 줄 때 (보상 line 이 비었을 때)
        public string[] doneLines;            // 이미 받은 뒤 다시 말을 걸면
        public float hpCost;                  // 도박: 거는 체력 (최대 체력 비율)
        public string offerLine;              // 도박: 첫 E 의 제안 ({0} 거는 체력). 한 번 더 E 를 누르면 받아들인다
        public string offerNotice;            // 도박 제안 알림 ({0} 거는 체력)
        public NpcReward[] rewards;           // 하나를 가중치로 고른다

        [Header("재봉실 (만난 뒤, 등반마다 한 번)")]
        public string[] hubLines;             // 선물을 줄 때 (보상 line 이 비었을 때)
        public string[] hubDoneLines;         // 이번에는 이미 받았을 때
        public NpcReward[] hubRewards;        // Piece, Shards, Boon 중에서
        public HubStation station;            // 재봉실 시설 자리 (hub.facilities 에 같은 station 으로 위치, 반지름, 꼬리표)
    }

    [Serializable] public class NpcConfig
    {
        [Range(0, 1)] public float chance;    // 쉬어가기 라운드마다 NPC 가 나올 확률
        public float unmetWeight;             // 아직 못 만난 NPC 가중치 배율
        public Vector2 offset;                // 나오는 자리 (플레이어 기준, 전투장 좌표)
        public float interactRadius;          // 말 걸기 거리 (전투장 좌표)
        public float charTime;                // 말풍선 글자 사이 간격 (초)
        public float lineHold;                // 다 찍힌 뒤 말풍선이 머무는 시간 (초)
        public float talkHold;                // 말 건 뒤 리그가 '말하기' 자세를 유지하는 시간 (초)
        public string metNotice;              // 처음 만났을 때 알림 ({0} 이름): "재봉실에서 다시 만날 수 있습니다"
        public string boonNotice;             // 등반을 시작할 때 재봉실 축복이 걸렸다는 알림 ({0} 이름들)
        public string talkSound, goodSound, badSound;   // 효과음 id
        public Color shadowColor;             // 발밑 그림자
        public Vector2 shadowSize;            // 발밑 그림자 지름 (캔버스 px)
        public string[] statNames;            // RunStat 순서 능력치 이름 (알림 수치 "피해 +2")
        public NpcDef[] npcs;

        public NpcDef Get(NpcKind kind) => npcs == null ? null : Array.Find(npcs, n => n != null && n.kind == kind);
    }

    // 재봉실 NPC 가 다음 등반에 걸어 두는 능력치 (Profile.npcBoons, 등반 시작 때 쓰고 비운다)
    [Serializable] public class NpcBoon
    {
        public string name;
        public AugmentEffect[] effects;
    }
}
