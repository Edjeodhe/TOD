// 19차 다락의 예견가 모이라(거미) 뼈대 리그. 뼈와 그림은 리깅 페이지(tools/rig_editor/moira.html)에서 정한 값으로
// MCP 가 씬 Hub/Moira 에 조립했다(art_src/rig/moira_rig.json 이 첫 값, 조립 뒤에는 씬이 원본).
//   대기   : idle 반복 (몸 숨쉬기, 머리 갸웃, 점성구 위 손 일렁임, 다리 까딱).
//   말 걸기: talk 가 켜지면 raise(시작 → 최고 → 유지)로 카드를 쥔 손을 들고 다른 손으로 가리킨다. 든 동안 hold 반복을 더한다.
//            talkSwap 뼈는 말하는 동안 그림을 talkArt(카드 넷을 쥔 손)로 바꾼다.
//   점성구 : ballBone 그림의 밝기(_Bright)가 천천히 맥동한다.
using UnityEngine;

namespace TOD
{
    public class MoiraRig : BoneRig
    {
        public float height;                  // 리그 전체 높이 (그림 px, 바닥 → 머리 위). 시설 height 에 맞춰 배율을 정한다
        public LoopMotion idle, hold;
        public ShotMotion raise;
        public float talkFollow = 6;          // 말 걸기 비중이 따라가는 빠르기 (1/초)
        [Range(0, 1)] public float swapAt = .35f;   // 이 비중부터 말할 때 그림으로
        [Range(0, 1)] public float swapAfter = .8f; // 그리고 손을 드는 동작이 최고 시점의 이만큼 지난 뒤 (팔이 얼굴 앞을 지날 때 카드가 보이지 않게)

        [Header("말할 때 그림 바꾸기")]
        public HeroBone talkSwap;             // 그림을 바꿀 뼈 (카드를 쥐는 손)
        public SpriteRenderer talkArt;        // 말할 때 켜는 그림 (talkSwap 아래, 꺼 둠)
        public Transform cardAnchor;          // 카드가 날아 나가는 자리 (부채 가운데)

        [Header("점성구")]
        public SpriteRenderer ball;
        public float ballPulse = .12f, ballSpeed = 1.3f;

        public bool Talking { get; set; }
        public float TalkWeight { get; private set; }

        float talkStart = -99, time;
        bool wasTalking;
        Pose pose;
        MaterialPropertyBlock block;
        static readonly int BrightId = Shader.PropertyToID("_Bright");

        // 매 프레임: dt 만큼 진행해 자세를 뼈에 적용한다. highlight = 가까이 왔을 때 밝기 배율.
        public void Animate(float dt, float highlight)
        {
            time += dt;
            if (Talking && !wasTalking) talkStart = time;
            wasTalking = Talking;
            TalkWeight += ((Talking ? 1 : 0) - TalkWeight) * Mathf.Min(1, dt * talkFollow);

            if (pose.ang == null || pose.ang.Length != bones.Length) pose = NewPose();
            for (int i = 0; i < pose.ang.Length; i++) pose.ang[i] = 0;
            pose.root = Vector2.zero;
            Loop(idle, time, ref pose);
            Loop(hold, time, ref pose, TalkWeight);
            if (raise != null && TalkWeight > 0) Shot(raise, Mathf.Min(time - talkStart, raise.length), ref pose, TalkWeight);
            ApplyPose(pose);

            bool raised = raise == null || time - talkStart >= raise.peakTime * swapAfter;
            bool swap = TalkWeight > swapAt && (raised || !Talking);
            if (talkSwap != null && talkSwap.art != null) talkSwap.art.enabled = !swap;
            if (talkArt != null) talkArt.enabled = swap;

            block ??= new MaterialPropertyBlock();
            foreach (HeroBone b in bones)
            {
                if (b == null || b.art == null) continue;
                float bright = highlight;
                if (b.art == ball) bright *= 1 + ballPulse * (.5f + .5f * Mathf.Sin(time * ballSpeed * 2 * Mathf.PI));
                b.art.GetPropertyBlock(block);
                block.SetFloat(BrightId, bright);
                b.art.SetPropertyBlock(block);
            }
            if (talkArt != null)
            {
                talkArt.GetPropertyBlock(block);
                block.SetFloat(BrightId, highlight);
                talkArt.SetPropertyBlock(block);
            }
        }

        // 카드가 날아 나가는 월드 자리 (없으면 리그 가운데 위)
        public Vector3 CardPoint => cardAnchor != null ? cardAnchor.position : transform.position + Vector3.up * height * .6f / Space.Ppu;
    }
}
