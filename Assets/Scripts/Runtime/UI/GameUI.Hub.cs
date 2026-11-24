// 15차 재봉실 허브: 시설 꼬리표와 상인 단추 창 (GameUI 의 나머지 부분). 19차: 단추 창 = 챙기기, 들고 갈 수 늘리기 메뉴.
// 구조는 Main.uxml(lobby 의 hub-prompt, hub-hint, overlay 의 dialog-hubshop), 가격 띠는 HubSlots.uxml 템플릿, 모양은 Hub.uss.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        public VisualTreeAsset hubSlots;      // HubSlots.uxml (hub-price, 19차부터 안 씀)

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

        // 19차 단추 메뉴: '시작 조각 챙기기' 카드(누르면 챙기기 판)와 '들고 갈 수 늘리기' 강화 카드
        public void ShowHubMenu(HubConfig hub, Profile profile, int packed, int slots, UpgradeDef slotUpgrade, int slotCost, bool slotMax, int slotLevel,
            Action pack, Action buySlot)
        {
            Text(root, "hubshop-title", hub.shopTitle);
            Text(root, "hubshop-text", hub.shopText);
            Text(root, "hubshop-shards", profile.shards.ToString());
            shines.Clear();
            VisualElement container = root.Q("hub-wares");
            container.Clear();
            Card(container, hub.packIcon, null, hub.packName, hub.packText, string.Format(hub.packFoot ?? "", packed, slots), pack);
            if (slotUpgrade != null)
            {
                string text = slotUpgrade.text;
                if (!string.IsNullOrEmpty(T.starterSlotsLine)) text += "\n" + string.Format(T.starterSlotsLine, slots);
                string foot = slotMax ? T.upgradeMax : string.Format(T.upgradeCost, slotCost, slotLevel);
                Card(container, slotUpgrade.icon, null, slotUpgrade.name, text, foot, buySlot, !slotMax && profile.shards >= slotCost);
            }
            Show(root.Q<Label>("hubshop-empty"), false);
            ShowDialog("hubshop");
        }
    }
}
