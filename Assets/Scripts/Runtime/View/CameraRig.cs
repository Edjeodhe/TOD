// 카메라: 캐릭터를 따라가고(맵 경계 안으로 제한), 흔들림·당기기 킥·정화 줌아웃을 더한다.
using UnityEngine;

namespace TOD
{
    public class CameraRig : MonoBehaviour
    {
        public Camera view;
        public float shakeScale;          // 14
        public float baseSize;            // 직교 크기 (캔버스 590px 높이 = 2.95)
        public float referenceAspect;     // 24차: baseSize 가 맞춰진 화면 비율 (16:9 = 1.7778). 0 이면 비율 맞춤 없음

        public float Zoom { get; private set; } = 1;   // baseSize 대비 실제 직교 크기 (화면 비율 맞춤 포함)

        // focus: 캔버스 좌표의 바라볼 점, bounds: 카메라 중심이 머물 수 있는 캔버스 영역
        // punch: 24차 줌 펀치 (직교 크기를 줄이는 비율). 화면 비율 맞춤 뒤에 곱해서 어떤 비율에서도 똑같이 다가간다. 환경설정 '화면 흔들림'을 따른다
        public void Apply(Vector2 focus, Rect bounds, float zoom, float shake, Timed kick, float clock, float punch = 0)
        {
            if (baseSize <= 0) baseSize = view.orthographicSize;   // 값이 아직 안 들어갔으면 씬 카메라 크기를 쓴다
            float size = FitSize(baseSize * zoom, bounds) * (1 - Mathf.Clamp01(punch * Settings.Shake));
            Zoom = size / baseSize;
            view.orthographicSize = size;
            float halfH = size * Space.Ppu;
            float halfW = halfH * view.aspect;
            // 맵이 화면보다 작으면 가운데에 고정
            float cx = bounds.width < halfW * 2 ? bounds.center.x : Mathf.Clamp(focus.x, bounds.xMin + halfW, bounds.xMax - halfW);
            float cy = bounds.height < halfH * 2 ? bounds.center.y : Mathf.Clamp(focus.y, bounds.yMin + halfH, bounds.yMax - halfH);
            float k = kick != null ? Mathf.Pow(kick.time / kick.max, 2) : 0;
            float s = shake * shakeScale * Settings.Shake;   // 24차 환경설정 화면 흔들림
            float dx = Mathf.Sin(clock * 67) * s + (kick?.x ?? 0) * k;
            float dy = Mathf.Cos(clock * 49) * s * .6f + (kick?.y ?? 0) * k;
            Vector3 at = Space.Canvas(cx - dx, cy - dy);
            view.transform.position = new Vector3(at.x, at.y, view.transform.position.z);
        }

        // 24차 화면 비율 맞춤 (UI 의 Expand 와 같은 생각):
        //  - 16:9 보다 좁은 화면(4:3, 16:10): 기준 너비가 다 보이게 멀어진다. 다만 머물 영역(bounds) 높이를 넘지는 않는다 (방 위아래 빈 곳이 안 보이게).
        //  - 16:9 보다 넓은 화면(21:9): 높이 그대로 옆이 더 보인다. 재봉실·다락처럼 그림 크기가 정해진 방은 방 전체(벽, 탑 문)가 다 보이고
        //    양옆은 방 밖 어두운 테두리가 넓어진다 (다가가서 위아래를 자르면 벽의 시설이 가려져서).
        //  16:9 에서는 아무것도 바뀌지 않는다.
        float FitSize(float size, Rect bounds)
        {
            float aspect = view.aspect;
            if (referenceAspect <= 0 || aspect <= 0 || aspect >= referenceAspect) return size;
            float wide = size * referenceAspect / aspect;
            float cap = bounds.height > 0 ? Mathf.Max(size, bounds.height / (2 * Space.Ppu)) : wide;
            return Mathf.Min(wide, cap);
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
