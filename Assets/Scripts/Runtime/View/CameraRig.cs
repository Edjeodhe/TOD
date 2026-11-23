// 화면 흔들림과 당기기 킥 (웹판 render: ctx.translate(shake, kick)). 카메라를 반대로 옮겨 같은 효과를 낸다.
using UnityEngine;

namespace TOD
{
    public class CameraRig : MonoBehaviour
    {
        public Camera view;
        public Vector2 canvasCenter;      // 카메라가 기본으로 비추는 캔버스 좌표 (500, 295)
        public float shakeScale;          // 14

        public void Apply(float shake, Timed kick, float clock)
        {
            float k = kick != null ? Mathf.Pow(kick.time / kick.max, 2) : 0;
            float s = shake * shakeScale;
            float dx = Mathf.Sin(clock * 67) * s + (kick?.x ?? 0) * k;
            float dy = Mathf.Cos(clock * 49) * s * .6f + (kick?.y ?? 0) * k;
            Vector3 at = Space.Canvas(canvasCenter.x - dx, canvasCenter.y - dy);
            view.transform.position = new Vector3(at.x, at.y, view.transform.position.z);
        }

        public void ResetView() => Apply(0, null, 0);
    }
}
