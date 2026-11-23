// 16차 상인 단추: 자세(통그림)와 트윈, 상황별 말풍선 대사. 값은 씬 GameDatabase 의 merchant 에 MCP 로 넣는다 (코드에는 빈 필드만).
using System;
using UnityEngine;

namespace TOD
{
    public enum MerchantMood { Idle, Greet, Talk, Happy, Refuse, Farewell }

    // 대사가 뜨는 계기
    public enum MerchantCue { Approach, Greet, Buy, NoMoney, SoldOut, Farewell }

    [Serializable] public class MerchantPose
    {
        public MerchantMood mood;
        public Sprite sprite;
    }

    [Serializable] public class MerchantLines
    {
        public MerchantCue cue;
        public MerchantMood mood;             // 이 대사를 할 때 바꿔 끼울 자세
        [TextArea(1, 3)] public string[] lines;   // 무작위로 하나
    }

    [Serializable] public class MerchantConfig
    {
        [Header("자세")]
        public MerchantPose[] poses;
        public float poseHold;                // 자세를 바꾼 뒤 기본 자세로 돌아가기까지 (초)

        [Header("트윈 (배율은 1 기준, 각도는 도, 높이는 캔버스 px)")]
        public float breathAmount, breathSpeed;       // 숨쉬기: 세로 늘이기 폭, 초당 횟수
        public float popTime, popSquash, popHop;      // 자세를 바꿀 때: 시간, 눌림 폭, 튀는 높이
        public float tiltAngle, tiltTime;             // 인사, 작별: 기울였다 돌아오기
        public float hopHeight, hopTime; public int hopCount;     // 기쁨: 깡충
        public float shakeAngle, shakeTime; public int shakeCount; // 거절: 좌우 흔들기
        public float talkAngle, talkSpeed;            // 말하는 동안 살짝 까딱

        [Header("말풍선")]
        public MerchantLines[] lines;
        public float charsPerSecond;          // 글자가 찍히는 속도
        public float lineHold;                // 다 찍힌 뒤 떠 있는 시간 (초)
        public float bubbleLift;              // 머리 위 몇 px 에 (캔버스 px)
        public float approachCooldown;        // 다가옴 혼잣말 사이 최소 간격 (초)
        public string talkSound, blipSound;   // 새 대사, 글자 찍힘(두 자마다) 효과음 id
        public int blipEvery;                 // 몇 자마다 찍힘 소리
    }
}
