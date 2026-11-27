// 25차 미니맵: 매 프레임 점 목록(NPC, 적, 보스)과 주인공 자리를 GameUI 에 넘긴다 (그리기는 GameUI.Minimap.cs).
// 전투 = 전투장 바닥 좌표, 맵 경계 = arena.wall*. 재봉실 = 재봉실 바닥 좌표, 맵 경계 = 지금 방의 walk.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public partial class GameController
    {
        void TickMinimap()
        {
            MinimapConfig c = db.hud?.minimap;
            List<Vector2> npcs = ui.MapNpcs, enemies = ui.MapEnemies, bosses = ui.MapBosses;
            npcs.Clear(); enemies.Clear(); bosses.Clear();
            if (c == null) { ui.HideMinimap(); return; }

            if (Run != null && screen == UiScreen.Combat && Run.outcome == Outcome.None)
            {
                RunState r = Run;
                foreach (Enemy e in r.enemies)
                {
                    if (e.hp <= 0) continue;
                    (e.IsBoss ? bosses : enemies).Add(new Vector2(e.x, e.y));
                }
                if (r.merchant != null) npcs.Add(new Vector2(r.merchant.x, r.merchant.y));
                if (r.npc != null) npcs.Add(new Vector2(r.npc.x, r.npc.y));
                ArenaConfig a = db.arena;
                ui.ShowMinimap(new GameUI.MinimapView
                {
                    hero = new Vector2(r.player.x, r.player.y),
                    bounds = Rect.MinMaxRect(a.wallLeft, a.wallTop, a.wallRight, a.wallBottom),
                    range = c.runRange,
                });
                return;
            }

            if (hub != null && hubRoot != null && hubRoot.activeSelf && Run == null && screen == UiScreen.Lobby)
            {
                foreach (HubFacility f in hub.Room.facilities ?? new HubFacility[0])
                {
                    bool person = HubView.IsNpc(f.station) || f.station is HubStation.Merchant or HubStation.Moira;
                    if (!person || (hub.stationVisible != null && !hub.stationVisible(f.station))) continue;
                    npcs.Add(f.position);
                }
                ui.ShowMinimap(new GameUI.MinimapView
                {
                    hero = new Vector2(hub.Player.x, hub.Player.y),
                    bounds = hub.Room.walk,
                    range = c.hubRange,
                });
                return;
            }
            ui.HideMinimap();
        }
    }
}
