// 카메라: 캐릭터를 따라가고(맵 경계 안으로 제한), 흔들림·당기기 킥·정화 줌아웃을 더한다.
using UnityEngine;

namespace TOD
{
    public class CameraRig : MonoBehaviour
    {
        public Camera view;
        public float shakeScale;          // 14
        public float baseSize;            // 직교 크기 (캔버스 590px 높이 = 2.95)

        public float Zoom { get; private set; } = 1;

        // focus: 캔버스 좌표의 바라볼 점, bounds: 카메라 중심이 머물 수 있는 캔버스 영역
        public void Apply(Vector2 focus, Rect bounds, float zoom, float shake, Timed kick, float clock)
        {
            Zoom = zoom;
            if (baseSize <= 0) baseSize = view.orthographicSize;   // 값이 아직 안 들어갔으면 씬 카메라 크기를 쓴다
            view.orthographicSize = baseSize * zoom;
            float halfH = baseSize * zoom * Space.Ppu;
            float halfW = halfH * view.aspect;
            // 맵이 화면보다 작으면 가운데에 고정
            float cx = bounds.width < halfW * 2 ? bounds.center.x : Mathf.Clamp(focus.x, bounds.xMin + halfW, bounds.xMax - halfW);
            float cy = bounds.height < halfH * 2 ? bounds.center.y : Mathf.Clamp(focus.y, bounds.yMin + halfH, bounds.yMax - halfH);
            float k = kick != null ? Mathf.Pow(kick.time / kick.max, 2) : 0;
            float s = shake * shakeScale;
            float dx = Mathf.Sin(clock * 67) * s + (kick?.x ?? 0) * k;
            float dy = Mathf.Cos(clock * 49) * s * .6f + (kick?.y ?? 0) * k;
            Vector3 at = Space.Canvas(cx - dx, cy - dy);
            view.transform.position = new Vector3(at.x, at.y, view.transform.position.z);
        }

        // 보이는 영역 (캔버스 좌표)
        public Rect VisibleCanvas()
        {
            float halfH = view.orthographicSize * Space.Ppu;
            float halfW = halfH * view.aspect;
            Vector3 p = view.transform.position;
            return new Rect(p.x * Space.Ppu - halfW, -p.y * Space.Ppu - halfH, halfW * 2, halfH * 2);
        }
    }
}
