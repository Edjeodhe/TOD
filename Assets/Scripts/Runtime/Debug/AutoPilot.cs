// 검증용 자동 조작. 컴포넌트를 켜면(enabled) 원을 그리며 움직이고, 가까운 적을 겨눠 쏘고, 실이 모이면 당긴다.
// 대화 상자는 autoAdvance 가 켜져 있으면 기본 선택으로 넘긴다. 값은 MCP로 넣는다.
using System.Linq;
using UnityEngine;

namespace TOD
{
    public class AutoPilot : MonoBehaviour
    {
        public GameController game;
        public bool autoAdvance, autoStart;
        public float orbitRadius, orbitSpeed, dialogDelay, dashDistance;
        public int pullAtStrands;

        float dialogTimer;

        public void Drive(RunState r, ref CombatInput input, ref Vector2 aim, ref bool fire, ref bool pull, ref bool dash)
        {
            Player p = r.player;
            float t = Time.time * orbitSpeed;
            Vector2 center = new(game.db.arena.width / 2, game.db.arena.height / 2);
            Vector2 goal = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * orbitRadius;
            Vector2 move = goal - new Vector2(p.x, p.y);
            if (move.magnitude > 12) { move.Normalize(); input.x = Mathf.Round(move.x); input.y = Mathf.Round(move.y); }
            Enemy target = r.enemies.OrderBy(e => (e.x - p.x) * (e.x - p.x) + (e.y - p.y) * (e.y - p.y)).FirstOrDefault();
            if (target != null)
            {
                aim = new Vector2(target.x, target.y);
                fire = true;
                float gap = Vector2.Distance(new Vector2(target.x, target.y), new Vector2(p.x, p.y));
                if (gap < dashDistance || r.bullets.Any(b => Vector2.Distance(new Vector2(b.x, b.y), new Vector2(p.x, p.y)) < dashDistance * .6f)) dash = true;
            }
            if (r.strands.Count(s => s.state != StrandState.Flight) >= pullAtStrands || (r.yarn < 1 && r.strands.Count > 0)) pull = true;
        }

        void Update()
        {
            if (game == null) return;
            if (autoStart && game.Run == null) { game.StartRun(); return; }
            if (!autoAdvance || game.Run == null || game.ui.Opened == "") { dialogTimer = 0; return; }
            dialogTimer += Time.unscaledDeltaTime;
            if (dialogTimer >= dialogDelay) { dialogTimer = 0; game.AutoChoose(); }
        }
    }
}
