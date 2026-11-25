// 23차-boss7: 보스 HUD 의 죄종 보스 이름과 2페이즈 표시 (GameUI 의 나머지 부분).
// - 이름 = BossDef.name (2페이즈면 phase2Name). 공용 보스는 UXML 기본 이름 그대로.
// - 전환 중(체력바가 다시 차오름) refill, 2페이즈 phase2 클래스 (Main.uss).
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        void UpdateBossHud(Enemy boss)
        {
            VisualElement box = hud.Q("boss-hud");
            if (box == null) return;
            BossBrain b = boss.brain;
            box.EnableInClassList("refill", b != null && b.Shielded && b.roared);
            box.EnableInClassList("phase2", b != null && b.phase >= 2);
            if (b == null) return;
            string name = b.phase >= 2 && !string.IsNullOrEmpty(b.def.phase2Name) ? b.def.phase2Name : b.def.name;
            if (string.IsNullOrEmpty(name)) return;
            Text(hud, "boss-name", name);
        }
    }
}
