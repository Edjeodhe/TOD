// 23차 보스 욕망을 고른 뒤 그 층에 머문 채 문구가 뜨고 화면이 어두워진 다음 다음 층으로 (GameController 의 나머지 부분).
// 삼킨다 = 고르자마자 화면 전체가 살짝 붉게 물들고(swallowTint, tintIn) 문구가 사라지는 때에 맞춰 검붉게 어두워진다(swallowDark).
// 고개를 돌린다 = 붉은 기 없이 검게만 어두워진다(refuseDark).
// 다 어두워지면 다음 층을 열고(Ascend) darkHold 초 뒤 darkInTime 초에 걸쳐 밝아진다. 그동안 입력은 막는다.
// 막은 GameUI.FloorVeil.cs(Main.uxml floor-veil), 문구는 GameUI.DesireEcho.cs. 수치는 데이터 desire(swallowTint … darkInTime).
using UnityEngine;

namespace TOD
{
    public partial class GameController
    {
        bool floorFading, floorFadeSwallow, floorFadeAscended;
        float floorFadeStart, floorFadeBlack;

        // 욕망 한 줄이 떠 있는 동안 + 막이 걷힐 때까지 입력을 막는다.
        bool FloorFading => floorFading;

        void BeginFloorFade(bool swallowed)
        {
            DesireConfig d = db.desire;
            floorFading = true;
            floorFadeSwallow = swallowed;
            floorFadeAscended = false;
            floorFadeStart = Clock;
            // 문구가 사라지는 때에 맞춰 다 어두워진다 (문구가 없으면 바로 어두워지기 시작)
            float outTime = Mathf.Max(.05f, d.darkOutTime);
            float echoEnd = ui.DesireEchoEnd > Clock ? ui.DesireEchoEnd : Clock;
            floorFadeBlack = Mathf.Max(Clock + outTime, echoEnd);
            ui.FloorVeilEnd = floorFadeBlack + Mathf.Max(0, d.darkHold) + Mathf.Max(.05f, d.darkInTime);
            holdingFire = false; fireRequested = false; pullRequested = false; dashRequested = false;
            TickFloorFade();
        }

        void TickFloorFade()
        {
            if (!floorFading) return;
            if (Run == null) { EndFloorFade(); return; }
            DesireConfig d = db.desire;
            float now = Clock;
            float outTime = Mathf.Max(.05f, d.darkOutTime), inTime = Mathf.Max(.05f, d.darkInTime);
            Color dark = floorFadeSwallow ? d.swallowDark : d.refuseDark;
            Color veil;
            if (now < floorFadeBlack)
            {
                // 붉은 기(삼킴만) → 어둠
                float tint = floorFadeSwallow ? Smooth((now - floorFadeStart) / Mathf.Max(.05f, d.tintIn)) * d.swallowTint.a : 0;
                float p = Smooth(1 - (floorFadeBlack - now) / outTime);
                Color from = floorFadeSwallow ? d.swallowTint : dark;
                veil = Color.Lerp(from, dark, p);
                veil.a = Mathf.Lerp(tint, dark.a, p);
            }
            else
            {
                if (!floorFadeAscended)
                {
                    floorFadeAscended = true;
                    ui.SetFloorVeil(new Color(dark.r, dark.g, dark.b, dark.a));
                    Ascend();
                    if (Run == null || Run.phase == Phase.Victory) { EndFloorFade(); return; }
                }
                float q = Smooth((now - floorFadeBlack - Mathf.Max(0, d.darkHold)) / inTime);
                veil = dark;
                veil.a = dark.a * (1 - q);
                if (q >= 1) { EndFloorFade(); return; }
            }
            ui.SetFloorVeil(veil);
        }

        void EndFloorFade()
        {
            floorFading = false;
            ui.SetFloorVeil(Color.clear);
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
    }
}
