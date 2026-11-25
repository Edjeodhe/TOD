// 22차 미래 보기의 방 쪽 연출 (씬 Hub/Moira/FateOrb, 값은 MCP 로 연결). 시간표는 GameUI.FateShow.cs 가 정하고 매 프레임 아래 값을 넣는다:
//   Lift, Glow   : 탁자 점성구가 떠오른 높이(그림 px)와 죄종 색으로 빛나는 양 (MoiraRig.BallLift, FateOrb 셰이더 _Glow)
//   Touch        : 모이라가 앉아 두 앞다리로 구슬을 감싸 만진다 (MoiraRig.Touching)
//   Zoom         : 0 = 원래 카메라, 1 = 구슬이 코앞 (zoomFill 만큼 화면을 채움). 크기는 배율이 고르게 줄도록 지수로 섞는다
//   Swirl, Blur, TintAmount : PostFX 의 정면으로 빨려 드는 당김(회전 없음), 방사형 번짐, 구슬 바깥 물들임 (_TOD_FateSwirl.w, _TOD_FateBlur, _TOD_FateTint)
//   Still        : 0 = 모이라가 평소처럼 움직임, 1 = 지금 자세 그대로 완전히 멈춤 (MoiraRig.Motion = 1 - Still, PostFX 촛불 깜빡임 _TOD_FateStill)
//   VideoMix     : 구슬 유리 안의 그림 (24차: 죄종별 구아슈 셀 애니 프레임 loops 를 fps 로 끝없이 돌림. 프레임이 없는 죄종만 옛 영상 VideoPlayer → RenderTexture) → FateOrb 셰이더 _VideoTex
// LateUpdate 에서 넣으므로 같은 프레임의 모이라 자세(점성구 자리)를 따른다. End 가 카메라와 리그를 원래대로 돌린다.
using UnityEngine;
using UnityEngine.Video;

namespace TOD
{
    // 24차 미래 보기 구슬 속 반복 그림 (죄종 하나): 셀 애니 프레임을 fps 로 처음부터 끝까지 돌고 다시 처음으로 (끝 프레임이 첫 프레임으로 이어지게 그림)
    [System.Serializable]
    public class FateLoop
    {
        public Element sin;
        public Texture2D[] frames;   // Assets/Art/Fate/<죄종>/frame_###.png 차례대로
        public float fps = 8;
    }

    public class FateOrbView : MonoBehaviour
    {
        public MoiraRig rig;
        public Camera view;
        public VideoPlayer player;            // renderMode RenderTexture, 소리 없음, 반복 없음
        public RenderTexture videoTarget;
        [Tooltip("큰 점성구 그림에서 유리 원 (uv 중심 xy, uv 반지름 zw)")]
        public Vector4 glassRect = new(.5f, .548f, .491f, .444f);
        [Tooltip("영상이 보이는 동안 구슬 그리기 순서 (0 = 리그 순서 그대로)")]
        public int aboveHands = 500;
        [Tooltip("24차: 죄종별 구슬 속 반복 그림 (있으면 영상 대신 이것을 끝없이 돈다)")]
        public FateLoop[] loops;

        public float Lift { get; set; }
        public float Glow { get; set; }
        public bool Touch { get; set; }
        public float Zoom { get; set; }
        public float Swirl { get; set; }
        public float Blur { get; set; }
        public float Still { get; set; }
        public float TintAmount { get; set; }
        public float VideoMix { get; set; }
        public bool Active { get; private set; }

        float fill = .8f;
        Vector2 offset;
        Color tint = Color.white;
        Vector3 camFrom;
        float sizeFrom;
        MaterialPropertyBlock block;
        FateLoop loop;            // 이번에 도는 반복 그림 (없으면 영상)
        float loopStart = -1;     // 반복을 시작한 때 (unscaled), -1 = 아직 (첫 프레임에 멈춤)

        static readonly int TintId = Shader.PropertyToID("_Tint"), GlowId = Shader.PropertyToID("_Glow"), MixId = Shader.PropertyToID("_Mix"),
            VideoId = Shader.PropertyToID("_VideoTex"), RectId = Shader.PropertyToID("_GlassRect"), AspectId = Shader.PropertyToID("_VideoAspect"),
            BrightId = Shader.PropertyToID("_Bright"), SatId = Shader.PropertyToID("_Sat"),
            SwirlId = Shader.PropertyToID("_TOD_FateSwirl"), FateTintId = Shader.PropertyToID("_TOD_FateTint"), BlurId = Shader.PropertyToID("_TOD_FateBlur"), StillId = Shader.PropertyToID("_TOD_FateStill");

        SpriteRenderer Orb => rig != null ? rig.fateOrb : null;

        // 구슬 중심 (월드), 유리 반지름 (월드)
        public Vector3 OrbCenter
        {
            get
            {
                SpriteRenderer o = Orb;
                if (o == null || !o.enabled) return rig != null ? rig.BallPoint : transform.position;
                Bounds b = o.bounds;
                return new Vector3(b.min.x + b.size.x * glassRect.x, b.min.y + b.size.y * glassRect.y, 0);
            }
        }
        public float OrbRadius
        {
            get
            {
                SpriteRenderer o = Orb;
                if (o == null || !o.enabled) return rig != null ? rig.BallSize.x * .45f : .3f;
                return o.bounds.size.x * glassRect.z;
            }
        }

        public void Begin(Color sinColor, Element sin, VideoClip clip, float zoomFill, Vector2 zoomOffset)
        {
            loop = FindLoop(sin);
            loopStart = -1;
            if (loop != null) clip = null;
            if (view == null) view = Camera.main;
            if (!Active)
            {
                camFrom = view.transform.position;
                sizeFrom = view.orthographicSize;
            }
            Active = true;
            tint = sinColor;
            fill = Mathf.Clamp(zoomFill, .2f, 1.2f);
            offset = zoomOffset;
            Lift = Glow = Zoom = Swirl = Blur = Still = TintAmount = VideoMix = 0;
            Touch = false;
            if (rig != null) rig.FateOrbOn = true;
            if (player != null)
            {
                player.Stop();
                player.clip = clip;
                if (videoTarget != null) player.targetTexture = videoTarget;
                if (clip != null) player.Prepare();
            }
            if (videoTarget != null) ClearVideo();
        }

        FateLoop FindLoop(Element sin)
        {
            if (loops == null) return null;
            foreach (FateLoop l in loops)
                if (l != null && l.sin == sin && l.frames != null && l.frames.Length > 0) return l;
            return null;
        }

        // 반복 그림이 있으면 true (영상 대신 끝없이 돈다)
        public bool Looping => loop != null;

        // 영상 길이(초). 영상이 없으면 0
        public float ClipLength => player != null && player.clip != null ? (float)player.clip.length : 0;

        // 반복 그림이면 첫 프레임부터 돌리고, 영상이면 제 길이 그대로(1배속) 처음부터 끝없이 반복해 튼다
        public void PlayVideo()
        {
            if (loop != null) { loopStart = Time.unscaledTime; return; }
            if (player == null || player.clip == null) return;
            player.isLooping = true;
            player.playbackSpeed = 1;
            player.time = 0;
            player.Play();
        }

        Texture2D LoopFrame()
        {
            int n = loop.frames.Length;
            float t = loopStart < 0 ? 0 : Time.unscaledTime - loopStart;
            int i = Mathf.FloorToInt(t * Mathf.Max(.1f, loop.fps)) % n;
            return loop.frames[i] != null ? loop.frames[i] : loop.frames[0];
        }

        // 지금까지 튼 비율 (영상이 없으면 시간표를 따르도록 -1)
        public float VideoProgress => player != null && player.clip != null && player.clip.length > 0 ? (float)(player.time / player.clip.length) : -1;

        public void End()
        {
            if (!Active) return;
            Active = false;
            loop = null;
            loopStart = -1;
            if (player != null) player.Stop();
            if (view != null)
            {
                view.transform.position = camFrom;
                view.orthographicSize = sizeFrom;
            }
            if (rig != null)
            {
                rig.FateOrbOn = false;
                rig.Touching = false;
                rig.BallLift = 0;
                rig.Motion = 1;
            }
            Shader.SetGlobalVector(SwirlId, Vector4.zero);
            Shader.SetGlobalFloat(BlurId, 0);
            Shader.SetGlobalFloat(StillId, 0);
            Shader.SetGlobalVector(FateTintId, Vector4.zero);
        }

        void ClearVideo()
        {
            RenderTexture old = RenderTexture.active;
            RenderTexture.active = videoTarget;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = old;
        }

        void OnDisable() => End();

        void LateUpdate()
        {
            if (!Active) return;
            if (rig != null)
            {
                rig.BallLift = Lift;
                rig.Touching = Touch;
                rig.Motion = 1 - Mathf.Clamp01(Still);
            }
            SpriteRenderer o = Orb;
            if (o != null)
            {
                block ??= new MaterialPropertyBlock();
                o.GetPropertyBlock(block);
                block.SetColor(TintId, tint);
                block.SetFloat(GlowId, Glow);
                block.SetFloat(MixId, VideoMix);
                block.SetVector(RectId, glassRect);
                if (loop != null)
                {
                    Texture2D f = LoopFrame();
                    if (f != null) block.SetTexture(VideoId, f);
                    block.SetFloat(AspectId, f != null && f.height > 0 ? (float)f.width / f.height : 1);
                }
                else
                {
                    if (videoTarget != null) block.SetTexture(VideoId, videoTarget);
                    VideoClip clip = player != null ? player.clip : null;
                    block.SetFloat(AspectId, clip != null && clip.height > 0 ? (float)clip.width / clip.height : 16f / 9f);
                }
                block.SetFloat(BrightId, rig != null ? rig.brightness : 1);
                block.SetFloat(SatId, rig != null ? rig.saturation : 1);
                o.SetPropertyBlock(block);
                if ((VideoMix > 0 || Zoom > .55f) && aboveHands != 0) o.sortingOrder = aboveHands;   // 줌 중간(일그러짐이 가장 셀 때)부터는 감싼 앞다리보다 앞에 (다리는 구슬 둘레를 감싸는 테두리가 됨)
            }

            // 카메라: 크기는 배율이 고르게 줄도록 지수로, 중심은 구슬이 화면에서 같은 비율로 다가오도록 크기에 맞춰
            Vector3 center = OrbCenter;
            float radius = Mathf.Max(.01f, OrbRadius);
            Vector3 target = center + new Vector3(offset.x, offset.y, 0) * radius;
            float sizeTo = radius / fill;
            float z = Mathf.Clamp01(Zoom);
            float size = sizeFrom * Mathf.Pow(sizeTo / sizeFrom, z);
            float k = Mathf.Abs(sizeFrom - sizeTo) > 1e-4f ? (size - sizeTo) / (sizeFrom - sizeTo) : 0;
            Vector3 at = Vector3.Lerp(target, camFrom, k);
            view.transform.position = new Vector3(at.x, at.y, camFrom.z);
            view.orthographicSize = size;

            // 화면 위 구슬 (PostFX)
            Vector3 vp = view.WorldToViewportPoint(center);
            float r = radius / (2 * size);   // 화면 높이 비율
            Shader.SetGlobalVector(SwirlId, new Vector4(vp.x, vp.y, r, Swirl));
            Shader.SetGlobalVector(FateTintId, new Vector4(tint.linear.r, tint.linear.g, tint.linear.b, TintAmount));
            Shader.SetGlobalFloat(BlurId, Blur);
            Shader.SetGlobalFloat(StillId, Still);
        }
    }
}
