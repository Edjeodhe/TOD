// 23차 포션 칸: 재봉 상자 판 아래 줄(potion-row, 등반 중에만, 마우스를 올리면 potion-tip)과 전투 HUD 아래 줄(potion-bar).
// 칸 하나는 Slots.uxml potion-slot 복제, 모양은 Potion.uss. 칸 수와 문구는 db.potions (PotionData.cs).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        readonly List<VisualElement> invPotions = new(), hudPotions = new();
        readonly Dictionary<VisualElement, (string id, int count, bool cooling)> potionDrawn = new();
        float[] potionFlashAt = new float[0];
        Color[] potionFlashColor = new Color[0];
        int potionTipSlot = -1;
        Vector2 potionTipPos;

        const float PotionFlashTime = .45f;

        void InitPotions()
        {
            BuildPotionSlots(inventory.Q("potion-slots"), invPotions, true);
            BuildPotionSlots(root.Q("potion-bar"), hudPotions, false);
            Text(inventory, "potion-title", db.potions?.title ?? "");
            potionFlashAt = new float[hudPotions.Count];
            potionFlashColor = new Color[hudPotions.Count];
            for (int i = 0; i < potionFlashAt.Length; i++) potionFlashAt[i] = -99;
            VisualElement tip = inventory.Q("potion-tip");
            tip.RegisterCallback<GeometryChangedEvent>(_ => { if (potionTipSlot >= 0) MovePotionTip(potionTipPos); });   // 글 길이로 크기가 바뀌면 다시 화면 안으로
        }

        void BuildPotionSlots(VisualElement container, List<VisualElement> list, bool hover)
        {
            container.Clear();
            list.Clear();
            int n = Potions.SlotCount(db);
            for (int i = 0; i < n; i++)
            {
                VisualElement slot = Slot(slots, "potion-slot");
                slot.Q<Label>("key").text = (i + 1).ToString();
                if (hover)
                {
                    int k = i;
                    slot.RegisterCallback<PointerEnterEvent>(e => ShowPotionTip(k, e.position));
                    slot.RegisterCallback<PointerMoveEvent>(e => MovePotionTip(e.position));
                    slot.RegisterCallback<PointerLeaveEvent>(_ => HidePotionTip());
                }
                else slot.pickingMode = PickingMode.Ignore;   // 전투 중에는 바늘 발사를 막지 않게
                container.Add(slot);
                list.Add(slot);
            }
        }

        void DrawPotionSlots(List<VisualElement> list, PotionSlot[] data, bool cooling)
        {
            for (int i = 0; i < list.Count; i++)
            {
                PotionSlot s = data != null && i < data.Length ? data[i] : null;
                PotionDef def = s == null || s.Empty ? null : Potions.Def(db, s.id);
                var state = (def?.id, def == null ? 0 : s.count, cooling && def != null);
                VisualElement slot = list[i];
                if (potionDrawn.TryGetValue(slot, out var drawn) && drawn == state) continue;   // 바뀐 칸만
                potionDrawn[slot] = state;
                slot.EnableInClassList("empty", def == null);
                slot.EnableInClassList("filled", def != null);
                slot.EnableInClassList("cooling", state.Item3);
                Icon(slot.Q("icon"), def?.icon);
                slot.Q<Label>("count").text = def == null ? "" : s.count.ToString();
            }
        }

        // 재봉 상자를 그릴 때 (RenderInventory 끝): 등반 중이고 줄 고르기가 아니면 판 아래 포션 줄
        void RenderPotionRow()
        {
            bool show = game.Run != null && boardMode?.pick == null && invPotions.Count > 0;
            Show(inventory.Q("potion-row"), show);
            HidePotionTip();
            if (show) DrawPotionSlots(invPotions, game.Run.potions, false);
        }

        // 전투 HUD: 매 프레임 (마신 칸은 잠깐 포션 색으로 번쩍)
        public void UpdatePotions(RunState r)
        {
            if (r == null) return;
            DrawPotionSlots(hudPotions, r.potions, r.time < r.potionReady);
            for (int i = 0; i < hudPotions.Count && i < potionFlashAt.Length; i++)
            {
                float t = (Now - potionFlashAt[i]) / PotionFlashTime;
                VisualElement glow = hudPotions[i].Q("glow");
                if (t < 0 || t > 1) { glow.style.opacity = 0; hudPotions[i].style.scale = StyleKeyword.Null; continue; }
                glow.style.unityBackgroundImageTintColor = potionFlashColor[i];
                glow.style.opacity = (1 - t) * .9f;
                float s = 1 + .18f * Mathf.Sin(Mathf.PI * t);
                hudPotions[i].style.scale = new Scale(new Vector2(s, s));
            }
        }

        public void PotionUsed(int slot, Color color)
        {
            if (slot < 0 || slot >= potionFlashAt.Length) return;
            potionFlashAt[slot] = Now;
            potionFlashColor[slot] = color;
        }

        void ShowPotionTip(int slot, Vector2 panelPos)
        {
            VisualElement tip = inventory.Q("potion-tip");
            PotionConfig c = db.potions;
            PotionSlot s = game.Run?.potions != null && slot < game.Run.potions.Length ? game.Run.potions[slot] : null;
            PotionDef def = s == null || s.Empty ? null : Potions.Def(db, s.id);
            Icon(tip.Q("potion-tip-icon"), def?.icon);
            Show(tip.Q("potion-tip-icon"), def != null);
            Text(tip, "potion-tip-name", def?.name ?? "");
            Show(tip.Q("potion-tip-name"), def != null);
            Text(tip, "potion-tip-count", def == null ? "" : string.Format(c?.countFormat ?? "{0}", s.count, Mathf.Max(1, c?.stack ?? 1)));
            Text(tip, "potion-tip-text", def?.text ?? c?.emptyTip ?? "");
            Text(tip, "potion-tip-key", def == null ? "" : string.Format(c?.keyHint ?? "{0}", slot + 1));
            Show(tip.Q("potion-tip-key"), def != null);
            potionTipSlot = slot;
            Show(tip, true);
            MovePotionTip(panelPos);
        }

        void MovePotionTip(Vector2 panelPos)
        {
            VisualElement tip = inventory.Q("potion-tip");
            if (tip.ClassListContains("hidden")) return;
            potionTipPos = panelPos;
            Vector2 p = inventory.WorldToLocal(panelPos);
            float w = float.IsNaN(tip.layout.width) || tip.layout.width < 1 ? 380 : tip.layout.width;
            float h = float.IsNaN(tip.layout.height) || tip.layout.height < 1 ? 180 : tip.layout.height;
            float x = p.x + 28, y = p.y - h - 16;   // 판 아래 칸이라 창은 위쪽에
            if (x + w > inventory.layout.width - 8) x = p.x - w - 20;
            if (y < 8) y = p.y + 20;
            tip.style.left = Mathf.Max(8, x);
            tip.style.top = Mathf.Max(8, y);
        }

        void HidePotionTip()
        {
            potionTipSlot = -1;
            Show(inventory.Q("potion-tip"), false);
        }
    }
}
