// 19차 아웃게임 재봉 상자 판: 상인 단추의 '시작 조각 챙기기'(진열 조각을 놓으면 산다)와 모이라의 줄 고르기.
// 줄 고르기: 가로/세로 줄은 마우스를 올린 줄이 반투명하게 덮이고(board-ghost), 누르면 그 줄. 이미 강화한 줄은 금빛 띠(board-pick).
// 확장은 판 네 가장자리에 반투명 띠(Slots.uxml pick-strip 복제)가 붙고, 누른 쪽에 줄 하나가 생긴다.
// 구조는 Main.uxml(inventory-title, inventory-help, board-pick), 모양은 Outgame.uss.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    // 판을 어떤 용도로 그릴지 (GameController.RenderInventory 가 채움)
    public class BoardMode
    {
        public string pick;                  // 모이라 강화 key (확장, 가로 줄, 세로 줄). null = 고르기 아님
        public List<OwnedItem> offers;       // 단추 진열 조각 (가짜 id). null = 챙기기 아님
        public Func<int, int> price;         // 조각 종류 → 파편 가격
    }

    public partial class GameUI
    {
        BoardMode boardMode;
        string inventoryTitle0, inventoryHelp0, unequipLabel0;

        void InitBoard()
        {
            inventoryTitle0 = inventory.Q<Label>("inventory-title")?.text;
            inventoryHelp0 = inventory.Q<Label>("inventory-help")?.text;
            unequipLabel0 = inventory.Q<Button>("unequip")?.text;
        }

        // 아웃게임 판의 제목, 안내, 위쪽 알림 줄
        void SetOutgameBoardTexts(Profile profile, int packedCount, BoardMode mode)
        {
            HubConfig h = db.hub;
            Label note = inventory.Q<Label>("inventory-run-note");
            string title = inventoryTitle0, help = inventoryHelp0, noteText;
            if (mode.pick != null)
            {
                UpgradeDef u = Array.Find(db.upgrades, x => x.key == mode.pick);
                title = string.Format(h.pickTitle ?? "{0}", u?.name);
                help = mode.pick switch { Moira.Rows => h.pickRows, Moira.Cols => h.pickCols, _ => h.pickExpand };
                noteText = u == null ? "" : Moira.Maxed(db, profile, u) ? T.upgradeMax : string.Format(T.upgradeCost, Moira.Cost(profile, u), Moira.Level(profile, u.key));
            }
            else
            {
                title = h.packTitle ?? title;
                help = h.packHelp ?? help;
                noteText = string.Format(h.packNote ?? T.inventoryOutgame ?? "", packedCount, Unlocks.StarterSlots(db, profile), profile.shards);
            }
            Text(inventory, "inventory-title", title ?? "");
            Text(inventory, "inventory-help", help ?? "");
            note.text = noteText ?? "";
            Show(note, !string.IsNullOrEmpty(note.text));
            Button unequip = inventory.Q<Button>("unequip");
            unequip.text = mode.pick == null && !string.IsNullOrEmpty(h.unpackLabel) ? h.unpackLabel : unequipLabel0;
        }

        // 등반 중 재봉 상자: 처음 문구로 되돌린다
        void ResetBoardTexts()
        {
            if (inventoryTitle0 != null) Text(inventory, "inventory-title", inventoryTitle0);
            if (inventoryHelp0 != null) Text(inventory, "inventory-help", inventoryHelp0);
            Button unequip = inventory.Q<Button>("unequip");
            if (unequipLabel0 != null) unequip.text = unequipLabel0;
        }

        // 줄 고르기 층: 강화한 줄(금빛 띠), 확장 가장자리 띠. 고르기가 아니면 비운다.
        void DrawPickLayer(Profile profile, int width, int height)
        {
            if (game.Run != null) ResetBoardTexts();
            VisualElement layer = inventory.Q("board-pick");
            VisualElement wrap = inventory.Q("board-wrap");
            layer.Clear();
            bool expand = boardMode?.pick == Moira.Expansion;
            // 확장 띠가 판 밖으로 나오므로 그만큼 판 둘레를 띄운다 (아니면 USS 값)
            StyleLength margin = expand ? new StyleLength(pitch + 10) : new StyleLength(StyleKeyword.Null);
            wrap.style.marginTop = margin; wrap.style.marginBottom = margin;
            wrap.style.marginLeft = margin; wrap.style.marginRight = margin;
            if (boardMode?.pick == null) return;

            // 강화한 줄: 판 위 금빛 띠 (가로 줄 = 가로 띠, 세로 줄 = 세로 띠). 고르는 종류의 줄만 진하게.
            foreach (int y in profile.rowLines ?? new List<int>())
                layer.Add(Band(0, y * pitch, width * pitch, pitch, boardMode.pick == Moira.Rows ? "pick-done" : "pick-done-faint"));
            foreach (int x in profile.colLines ?? new List<int>())
                layer.Add(Band(x * pitch, 0, pitch, height * pitch, boardMode.pick == Moira.Cols ? "pick-done" : "pick-done-faint"));

            if (!expand) return;
            // 확장: 네 가장자리 띠 (위, 오른쪽, 아래, 왼쪽). 누르면 그쪽에 줄 하나.
            float w = width * pitch, h = height * pitch, gap = 6;
            var rects = new[]
            {
                new Rect(0, -pitch - gap, w, pitch), new Rect(w + gap, 0, pitch, h),
                new Rect(0, h + gap, w, pitch), new Rect(-pitch - gap, 0, pitch, h),
            };
            for (int side = 0; side < 4; side++)
            {
                var strip = (Button)Slot(slots, "pick-strip");
                Rect r = rects[side];
                strip.style.left = r.x; strip.style.top = r.y; strip.style.width = r.width; strip.style.height = r.height;
                string name = db.hub.sideNames != null && side < db.hub.sideNames.Length ? db.hub.sideNames[side] : "";
                strip.text = "+";
                strip.tooltip = name;
                int s = side;
                strip.clicked += () => game.PickSide(s);
                layer.Add(strip);
            }
        }

        VisualElement Band(float x, float y, float w, float h, string cls)
        {
            VisualElement band = Slot(slots, "pick-band");
            band.AddToClassList(cls);
            band.style.left = x; band.style.top = y; band.style.width = w; band.style.height = h;
            return band;
        }

        // 마우스를 올린 줄을 반투명하게 덮는다. 이미 강화한 줄이면 붉게.
        public void PickHover(Profile profile, string key, Vector2Int? at)
        {
            VisualElement ghost = inventory.Q("board-ghost");
            ghost.Clear();
            ghost.RemoveFromClassList("invalid");
            if (at == null || key == Moira.Expansion) return;
            var (width, height, _) = Gear.Dimensions(db, profile);
            bool rows = key == Moira.Rows;
            int line = rows ? at.Value.y : at.Value.x;
            bool taken = !Moira.CanPick(db, profile, key, line);
            VisualElement band = rows ? Band(0, line * pitch, width * pitch, pitch, "pick-hover") : Band(line * pitch, 0, pitch, height * pitch, "pick-hover");
            band.EnableInClassList("pick-taken", taken);
            ghost.Add(band);
        }
    }
}
