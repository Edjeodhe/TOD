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
                help = mode.pick switch { Moira.Lines => h.pickLines, Moira.Rows => h.pickRows, Moira.Cols => h.pickCols, _ => h.pickExpand };
                noteText = u == null ? "" : Moira.Maxed(db, profile, u) ? T.upgradeMax : string.Format(T.upgradeCost, Moira.Cost(profile, u), Moira.Level(profile, u.key));
            }
            else
            {
                title = h.packTitle ?? title;
                help = h.packHelp ?? help;
                noteText = string.Format(h.packNote ?? T.inventoryOutgame ?? "", HubShop.PackedCells(db, profile), Unlocks.StarterCells(db, profile), profile.shards);   // 20차: 칸 수
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
            bool expand = boardMode?.pick == Moira.Expansion, lines = boardMode?.pick == Moira.Lines;
            float handle = Mathf.Round(pitch * .62f);
            // 확장 띠, 칸 강화 손잡이가 판 밖으로 나오므로 그만큼 판 둘레를 띄운다 (아니면 USS 값)
            StyleLength Margin(float v) => v > 0 ? new StyleLength(v) : new StyleLength(StyleKeyword.Null);
            float around = expand ? pitch + 10 : 0, lead = expand ? pitch + 10 : lines ? handle + 12 : 0;
            wrap.style.marginTop = Margin(lead); wrap.style.marginLeft = Margin(lead);
            wrap.style.marginBottom = Margin(around); wrap.style.marginRight = Margin(around);
            if (boardMode?.pick == null) return;

            // 강화한 줄: 판 위 금빛 띠 (가로 줄 = 가로 띠, 세로 줄 = 세로 띠). 고르는 종류의 줄만 진하게.
            foreach (int y in profile.rowLines ?? new List<int>())
            {
                VisualElement band = Band(0, y * pitch, width * pitch, pitch, boardMode.pick is Moira.Rows or Moira.Lines ? "pick-done" : "pick-done-faint");
                band.name = $"line-r{y}";   // 20차 PlayLineGrow 가 찾아 채운다
                layer.Add(band);
            }
            foreach (int x in profile.colLines ?? new List<int>())
            {
                VisualElement band = Band(x * pitch, 0, pitch, height * pitch, boardMode.pick is Moira.Cols or Moira.Lines ? "pick-done" : "pick-done-faint");
                band.name = $"line-c{x}";
                layer.Add(band);
            }

            if (lines)   // 칸 강화: 왼쪽 = 가로 줄 손잡이, 위 = 세로 줄 손잡이. 올리면 그 줄 전체가 반투명하게 덮인다
            {
                for (int y = 0; y < height; y++) AddHandle(layer, profile, y, new Rect(-handle - 8, y * pitch + 3, handle, pitch - 6), "▶");
                for (int x = 0; x < width; x++) AddHandle(layer, profile, Moira.ColBase + x, new Rect(x * pitch + 3, -handle - 8, pitch - 6, handle), "▼");
                return;
            }

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

        void AddHandle(VisualElement layer, Profile profile, int pick, Rect r, string glyph)
        {
            var strip = (Button)Slot(slots, "pick-strip");
            strip.AddToClassList("pick-handle");
            bool taken = !Moira.CanPick(db, profile, Moira.Lines, pick);
            strip.EnableInClassList("pick-taken", taken);
            strip.style.left = r.x; strip.style.top = r.y; strip.style.width = r.width; strip.style.height = r.height;
            strip.text = glyph;
            strip.RegisterCallback<PointerEnterEvent>(_ => HoverLine(profile, pick));
            strip.RegisterCallback<PointerLeaveEvent>(_ => HoverLine(profile, -1));
            strip.clicked += () => game.PickHandle(pick);
            layer.Add(strip);
        }

        // 칸 강화 손잡이에 올린 줄을 반투명하게 덮는다 (이미 강화한 줄이면 붉게). -1 = 지움
        void HoverLine(Profile profile, int pick)
        {
            VisualElement ghost = inventory.Q("board-ghost");
            ghost.Clear();
            ghost.EnableInClassList("paper", pick >= 0);   // 20차: 양피지는 덜 비치게
            if (pick < 0) return;
            var (width, height, _) = Gear.Dimensions(db, profile);
            bool col = pick >= Moira.ColBase;
            int line = col ? pick - Moira.ColBase : pick;
            VisualElement band = col ? Band(line * pitch, 0, pitch, height * pitch, "pick-hover") : Band(0, line * pitch, width * pitch, pitch, "pick-hover");
            band.EnableInClassList("pick-taken", !Moira.CanPick(db, profile, Moira.Lines, pick));
            ghost.Add(band);
        }

        // ---------------------------------------------------------------- 20차 칸 강화, 확장 연출
        // 빛나는 실 머리가 그 줄의 첫 칸(위 또는 왼쪽)에서 끝 칸까지 달리며 실을 잇는다. 머리가 지나는 칸은
        // 확장이면 하나씩 튀어나오고(그 전에는 숨김), 칸 강화면 금빛 띠가 머리를 따라 채워진다. 칸마다 번쩍임. 값은 db.hub.lineGrow.
        class Grow
        {
            public readonly List<VisualElement> cells = new(), flashes = new();
            public readonly List<Vector2> centers = new();
            public VisualElement thread, head, band;
            public Rect bandRect;
            public bool expand, vertical;
            public float start;
            public int stepped = -1;
            public bool ended;
            public Action<string> sound;
        }

        Grow grow;

        // 확인 직후 RenderInventory 다음에 부른다. key = 강화 key, pick = 고른 자리 (확장 = 변 0~3, 칸 강화 = 줄 번호 또는 ColBase + 열)
        public void PlayLineGrow(Profile profile, string key, int pick, Action<string> sound)
        {
            EndGrow();
            LineGrow g = db.hub?.lineGrow;
            if (g == null || g.cellTime <= 0 || pick < 0) return;
            var (width, height, _) = Gear.Dimensions(db, profile);
            bool vertical;
            int line;
            switch (key)
            {
                case Moira.Expansion: vertical = pick is 1 or 3; line = pick switch { 1 => width - 1, 2 => height - 1, _ => 0 }; break;
                case Moira.Lines: vertical = pick >= Moira.ColBase; line = vertical ? pick - Moira.ColBase : pick; break;
                case Moira.Rows: vertical = false; line = pick; break;
                case Moira.Cols: vertical = true; line = pick; break;
                default: return;
            }
            VisualElement layer = inventory.Q("board-pick");
            var gr = new Grow { expand = key == Moira.Expansion, vertical = vertical, start = Now + g.startDelay, sound = sound };
            int n = vertical ? height : width;
            for (int i = 0; i < n; i++)
            {
                var at = vertical ? new Vector2Int(line, i) : new Vector2Int(i, line);
                if (!cells.TryGetValue(at, out Button cell)) continue;
                gr.cells.Add(cell);
                gr.centers.Add(new Vector2((at.x + .5f) * pitch, (at.y + .5f) * pitch));
                VisualElement flash = Slot(slots, "grow-flash");
                flash.style.unityBackgroundImageTintColor = g.flashColor;
                flash.style.opacity = 0;
                gr.flashes.Add(flash);
                if (gr.expand) { cell.style.opacity = 0; cell.style.scale = new Scale(Vector2.zero); }
            }
            if (gr.cells.Count == 0) return;
            if (!gr.expand)
            {
                gr.band = layer.Q(vertical ? $"line-c{line}" : $"line-r{line}");
                if (gr.band != null) gr.bandRect = vertical ? new Rect(line * pitch, 0, pitch, height * pitch) : new Rect(0, line * pitch, width * pitch, pitch);
            }
            gr.thread = Slot(slots, "grow-thread");
            gr.thread.style.backgroundColor = g.threadColor;
            gr.head = Slot(slots, "grow-head");
            gr.head.style.unityBackgroundImageTintColor = g.headColor;
            layer.Add(gr.thread);
            foreach (VisualElement f in gr.flashes) layer.Add(f);
            layer.Add(gr.head);
            grow = gr;
            TickGrow();
        }

        // 다 끝났거나 판을 새로 그렸을 때: 칸과 띠를 원래대로, 연출 요소를 지운다
        void EndGrow()
        {
            if (grow == null) return;
            foreach (VisualElement c in grow.cells) { c.style.opacity = StyleKeyword.Null; c.style.scale = StyleKeyword.Null; }
            if (grow.band != null) { grow.band.style.width = grow.bandRect.width; grow.band.style.height = grow.bandRect.height; }
            grow.thread?.RemoveFromHierarchy();
            grow.head?.RemoveFromHierarchy();
            foreach (VisualElement f in grow.flashes) f.RemoveFromHierarchy();
            grow = null;
        }

        void TickGrow()
        {
            if (grow == null) return;
            if (grow.thread.parent == null) { grow.thread = null; EndGrow(); return; }   // 판을 새로 그림
            LineGrow g = db.hub.lineGrow;
            int n = grow.cells.Count;
            float t = Now - grow.start;
            float travel = n * g.cellTime;
            float u = Mathf.Clamp01(t / travel);
            Vector2 dir = grow.vertical ? new Vector2(0, 1) : new Vector2(1, 0);   // 패널 y 는 아래로 +
            Vector2 a = grow.centers[0] - dir * pitch * .5f, b = grow.centers[n - 1] + dir * pitch * .5f;
            Vector2 headAt = Vector2.Lerp(a, b, u);
            float fade = t < 0 ? 0 : 1 - Mathf.Clamp01((t - travel) / Mathf.Max(.05f, g.fadeTime));

            // 실: 첫 칸 가장자리 → 머리
            float th = Mathf.Max(2, g.threadWidth * pitch);
            VisualElement thread = grow.thread;
            if (grow.vertical) { thread.style.left = a.x - th / 2; thread.style.top = a.y; thread.style.width = th; thread.style.height = headAt.y - a.y; }
            else { thread.style.left = a.x; thread.style.top = a.y - th / 2; thread.style.width = headAt.x - a.x; thread.style.height = th; }
            thread.style.opacity = fade;
            // 머리: 달리는 동안 맥동, 끝에 닿으면 크게 터지며 사라짐
            float burst = t > travel ? Mathf.Clamp01((t - travel) / Mathf.Max(.05f, g.fadeTime)) : 0;
            PlaceAt(grow.head, headAt, g.headSize * pitch * (1 + .18f * Mathf.Sin(Now * 22) + burst * 1.6f));
            grow.head.style.opacity = t < 0 ? 0 : fade;
            // 칸 강화: 금빛 띠가 머리를 따라 채워짐
            if (grow.band != null)
            {
                Rect r = grow.bandRect;
                if (grow.vertical) grow.band.style.height = Mathf.Clamp(headAt.y - r.y, 0, r.height);
                else grow.band.style.width = Mathf.Clamp(headAt.x - r.x, 0, r.width);
            }
            // 칸: 머리가 칸 가운데에 닿을 때 번쩍임 (확장이면 그때 튀어나옴)
            for (int i = 0; i < n; i++)
            {
                float ti = (i + .5f) * g.cellTime;
                float f = (t - ti) / Mathf.Max(.05f, g.flashTime);
                VisualElement flash = grow.flashes[i];
                if (f > 0 && f < 1) { PlaceAt(flash, grow.centers[i], g.flashSize * pitch * (.5f + .9f * EaseOutQuad(f))); flash.style.opacity = 1 - f; }
                else flash.style.opacity = 0;
                if (grow.expand)
                {
                    float k = Mathf.Clamp01((t - ti + g.cellPop * .25f) / Mathf.Max(.05f, g.cellPop));
                    grow.cells[i].style.opacity = Mathf.Clamp01(k * 2);
                    float s = k <= 0 ? 0 : EaseOutBack(k);
                    grow.cells[i].style.scale = new Scale(new Vector2(s, s));
                }
                if (t >= ti && grow.stepped < i)
                {
                    grow.stepped = i;
                    if (!string.IsNullOrEmpty(g.stepSound)) grow.sound?.Invoke(g.stepSound);
                }
            }
            if (t >= travel && !grow.ended)
            {
                grow.ended = true;
                if (!string.IsNullOrEmpty(g.endSound)) grow.sound?.Invoke(g.endSound);
            }
            if (t > travel + g.fadeTime) EndGrow();
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
            if (at == null || key is Moira.Expansion or Moira.Lines) return;   // 확장, 칸 강화는 가장자리 손잡이에서
            ghost.AddToClassList("paper");
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
