// 15차 재봉실 허브: 시설 꼬리표("E 재봉 상자 열기")와 상인 단추 조각 상점 창 (GameUI 의 나머지 부분).
// 구조는 Main.uxml(lobby 의 hub-prompt, hub-hint, overlay 의 dialog-hubshop), 가격 띠는 HubSlots.uxml 템플릿, 모양은 Hub.uss.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        public VisualTreeAsset hubSlots;      // HubSlots.uxml (hub-price)

        const float PromptMinTop = 150;     // 꼬리표 아래 끝의 최소 높이 (패널 px, 위 띠 100px 아래)

        VisualElement hubPrompt;
        Label hubPromptText;

        void InitHub()
        {
            hubPrompt = root.Q("hub-prompt");
            hubPromptText = root.Q<Label>("hub-prompt-text");
            root.Q<Button>("close-hubshop")?.RegisterCallback<ClickEvent>(_ => CloseDialog());
            if (db.hub != null) Text(root, "hub-hint", db.hub.hint ?? "");
            InitMerchant();   // 16차 상인 말풍선 (GameUI.Merchant.cs)
        }

        // text 가 null 이면 숨긴다. canvas = 꼬리표 아래 가운데가 올 캔버스 px
        public void ShowHubPrompt(string text, Vector2 canvas)
        {
            if (hubPrompt == null) return;
            bool visible = !string.IsNullOrEmpty(text) && root.panel != null;
            Show(hubPrompt, visible);
            if (!visible) return;
            if (hubPromptText.text != text) hubPromptText.text = text;
            Vector3 screen = worldCamera.WorldToScreenPoint(Space.Canvas(canvas));
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
            hubPrompt.style.left = p.x;
            hubPrompt.style.top = Mathf.Max(p.y, PromptMinTop);   // 방 위쪽 시설(탑 문)이라도 위 띠에 가리지 않게
        }

        public void ShowHubShop(HubConfig hub, IList<int> stock, Func<int, int> price, Func<int, bool> sold, int shards, Action<int> buy)
        {
            Text(root, "hubshop-title", hub.shopTitle);
            Text(root, "hubshop-text", hub.shopText);
            Text(root, "hubshop-shards", shards.ToString());
            shines.Clear();
            VisualElement container = root.Q("hub-wares");
            container.Clear();
            bool any = false;
            foreach (int kind in stock)
            {
                bool done = sold(kind);
                int cost = price(kind);
                Button card = PieceOfferCard(container, kind, () => buy(kind));
                if (Gear.IsPattern(db, kind) && !string.IsNullOrEmpty(hub.patternTag))   // 17차: 아버지의 도안 표시
                {
                    card.AddToClassList("hub-pattern");
                    card.Add(new Label(hub.patternTag) { name = "hub-pattern-tag", pickingMode = PickingMode.Ignore });
                    card.Q<Label>("hub-pattern-tag").AddToClassList("hub-pattern-tag");
                }
                // 팔림, 파편 부족도 누를 수 있게 두고(상인이 대답함) 흐리게만
                card.EnableInClassList("hub-sold", done);
                card.EnableInClassList("ware-dim", !done && shards < cost);
                Label tag = (Label)Slot(hubSlots, "hub-price");
                tag.text = done ? hub.soldOut : string.Format(hub.priceFormat, cost);
                tag.EnableInClassList("short", !done && shards < cost);
                card.Add(tag);
                any |= !done;
            }
            Label empty = root.Q<Label>("hubshop-empty");
            empty.text = hub.shopEmpty;
            Show(empty, !any);
            ShowDialog("hubshop");
        }
    }
}
