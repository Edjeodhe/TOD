// 23차 포션: 등반 중 포션 칸 (재봉 상자 판 아래 5칸, 전투 HUD 아래쪽 5칸). 값과 문구는 씬 GameDatabase.potions 에 MCP 로 넣는다.
// 얻는 곳: 등반 시작 때 start, 쉬어가기 상인의 포션 물품(WareDef.potion), 정예 처치 드롭(eliteChance, eliteDrops). 전투 중 숫자키 1~5로 마신다.
using System;
using UnityEngine;

namespace TOD
{
    public enum PotionEffect { Heal, Purify, Yarn }

    [Serializable] public class PotionDef
    {
        public string id, name, text;          // text = 마우스 올림 설명 ("체력을 32 회복합니다.")
        public Sprite icon;
        public PotionEffect effect;
        public float amount;                   // 회복량, 욕망 감소량, 털실 수 (Yarn 에서 0 이하 = 가득)
        public Color color;                    // 마셨을 때 머리 위 숫자 색, 칸 빛
        public string sound;                   // 마실 때 소리 (SynthSound id)
    }

    [Serializable] public class PotionConfig
    {
        public int slots;                      // 칸 수 (5)
        public int stack;                      // 한 칸에 겹치는 최대 개수
        public PotionDef[] potions;
        public string[] start;                 // 등반 시작 때 받는 포션 id (중복 = 여러 개)
        public float eliteChance;              // 정예 처치 때 포션을 떨어뜨릴 확률 (보스 제외)
        public string[] eliteDrops;            // 정예가 떨어뜨리는 포션 id (무작위 하나)
        public float cooldown;                 // 마신 뒤 다음 포션까지 (초)
        public float dropSize;                 // 바닥에 떨어진 포션 그림 크기 (px)
        public Color dropGlow;                 // 떨어진 포션 둘레 빛
        public string pickupSound;             // 주울 때 소리
        public string title;                   // 재봉 상자 포션 칸 제목 ("포션")
        public string keyHint;                 // 마우스 올림 창 맨 아래 줄 ("{0}번 키로 마십니다")
        public string countFormat;             // 마우스 올림 창 개수 ("{0} / {1}개")
        public string emptyTip;                // 빈 칸에 마우스를 올렸을 때 ("빈 칸. 상인에게 사거나 정예가 떨어뜨린 포션이 들어갑니다.")
        public string full;                    // 칸이 가득 차 살 수 없을 때 알림
        public string noNeed;                  // 마셔도 소용없을 때 (체력 가득, 욕망 0, 털실 가득)
    }
}
