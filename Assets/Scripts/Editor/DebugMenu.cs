// 플레이 중 검증용 메뉴 (MCP execute_menu_item 으로 실행). 화면 전환·파편 지급만 하고 오브젝트는 만들지 않는다.
using UnityEditor;
using UnityEngine;

namespace TOD.EditorTools
{
    public static class DebugMenu
    {
        static GameController Game => Application.isPlaying ? Object.FindAnyObjectByType<GameController>() : null;

        [MenuItem("TOD/Debug/Screen - Lobby")] static void Lobby() => Game?.ShowScreen(UiScreen.Lobby);
        [MenuItem("TOD/Debug/Screen - Inventory")] static void Inventory() => Game?.ShowScreen(UiScreen.Inventory);
        [MenuItem("TOD/Debug/Screen - Attic")] static void Attic() => Game?.ShowScreen(UiScreen.Attic);
        [MenuItem("TOD/Debug/Select First Piece")] static void SelectPiece() { if (Game?.Profile is { items: { Count: > 0 } } p) Game.SelectPiece(p.items[0].id); }

        [MenuItem("TOD/Debug/Grant 50 Shards")]
        static void Shards()
        {
            if (Game == null) return;
            Game.Profile.shards += 50;
            Game.ShowScreen(UiScreen.Attic);
        }

        [MenuItem("TOD/Debug/Reset Profile")]
        static void ResetProfile()
        {
            GameDatabase db = Object.FindAnyObjectByType<GameDatabase>();
            if (db != null) PlayerPrefs.DeleteKey(db.gear.saveKey);
            Debug.Log("[TOD] profile reset");
        }
    }
}
