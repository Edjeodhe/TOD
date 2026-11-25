// 23차 보스 욕망을 고른 뒤 그 층에서 화면 전체가 물들고 어두워지는 막 (GameUI 의 나머지 부분).
// 색과 진하기는 GameController.FloorFade.cs 가 매 프레임 정한다. 구조는 Main.uxml(floor-veil), 모양은 Main.uss(.floor-veil).
// 막은 욕망 한 줄(desire-echo) 바로 아래 층이라, 어두워지는 동안에도 문구는 막 위에서 읽힌다.
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        VisualElement floorVeil;

        // 막이 다 걷히는 때 (Time.unscaledTime). ElementHud 가 새 층 암시 문구를 이 뒤로 미룬다.
        public float FloorVeilEnd { get; set; } = -1;

        // a <= 0 이면 숨긴다.
        public void SetFloorVeil(Color color)
        {
            floorVeil ??= root?.Q("floor-veil");
            if (floorVeil == null) return;
            bool on = color.a > .001f;
            Show(floorVeil, on);
            if (on) floorVeil.style.backgroundColor = color;
        }
    }
}
