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
            root.Q<Button>("close-hubpack")?.RegisterCallback<ClickEvent>(_ => CloseDialog());
            root.Q<Button>("hubpack-board")?.RegisterCallback<ClickEvent>(_ => packBoard?.Invoke());
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

        // 19차 단추 메뉴: '시작 조각 챙기기' 카드(누르면 카드 고르기 창)와 '퍼즐 칸 추가' 강화 카드.
        // 20차: 카드 설명 줄은 빼고(.hub-wares .text), 아랫줄(노란 글씨)만 크게. packed/cells = 챙긴 칸 / 챙길 칸.
        public void ShowHubMenu(HubConfig hub, Profile profile, int packed, int cells, UpgradeDef slotUpgrade, int slotCost, bool slotMax, int slotLevel,
            Action pack, Action buySlot)
        {
            Text(root, "hubshop-title", hub.shopTitle);
            Text(root, "hubshop-text", hub.shopText);
            Text(root, "hubshop-shards", profile.shards.ToString());
            shines.Clear();
            VisualElement container = root.Q("hub-wares");
            container.Clear();
            Card(container, hub.packIcon, null, hub.packName, "", string.Format(hub.packFoot ?? "", packed, cells), pack);
            if (slotUpgrade != null)
            {
                string foot = slotMax ? T.upgradeMax : string.Format(T.upgradeCost, slotCost, slotLevel);
                Card(container, slotUpgrade.icon, null, slotUpgrade.name, "", foot, buySlot, !slotMax && profile.shards >= slotCost);
            }
            Show(root.Q<Label>("hubshop-empty"), false);
            ShowDialog("hubshop");
        }

        // 20차 시작 조각 챙기기 창: 무작위 진열 조각을 증강 고르기처럼 퍼즐 카드로. 카드 아래 꼬리표 = 가격과 칸 수, 고른 카드는 '챙김'.
        Action packBoard;

        public void ShowPackCards(Profile profile, System.Collections.Generic.IList<int> kinds, Func<int, bool> taken, Func<int, int> price,
            int usedCells, int cells, int picks, long secondsLeft, Action<int> pick, Action openBoard)
        {
            HubConfig h = db.hub;
            packBoard = openBoard;
            int chosen = 0;
            foreach (int k in kinds) if (taken(k)) chosen++;
            long left = Math.Max(0, secondsLeft);
            string time = string.Format(h.packTimeFormat ?? "{0}:{1:00}", left / 3600, left % 3600 / 60);
            Text(root, "hubpack-eyebrow", h.packCardsEyebrow ?? "");
            Text(root, "hubpack-title", h.packCardsTitle ?? h.packName ?? "");
            Text(root, "hubpack-status", string.Format(h.packStatus ?? "{0}/{1} {2}/{3} {4}", chosen, picks, usedCells, cells, time));
            Text(root, "hubpack-shards", profile.shards.ToString());
            if (!string.IsNullOrEmpty(h.packBoardButton)) root.Q<Button>("hubpack-board").text = h.packBoardButton;
            shines.Clear();
            VisualElement container = root.Q("hubpack-cards");
            container.Clear();
            foreach (int kind in kinds)
            {
                int k = kind;
                Button card = PieceOfferCard(container, k, () => pick(k));
                bool isTaken = taken(k);
                int cost = price(k), size = db.gear.pieces[k].shape.Length;
                Label tag = (Label)Slot(hubSlots, "hub-price");
                tag.text = isTaken ? h.packTaken ?? "" : string.Format(h.packPrice ?? "{0}", cost, size);
                tag.EnableInClassList("short", !isTaken && profile.shards < cost);
                card.Add(tag);
                card.EnableInClassList("hub-taken", isTaken);
                // 고를 수 없는 카드(칸 모자람, 고를 수 다 씀)는 흐리게. 눌러서 이유를 듣는다
                bool blocked = !isTaken && (chosen >= picks || usedCells + size > cells);
                card.EnableInClassList("hub-blocked", blocked);
            }
            bool fresh = Opened != "hubpack";   // 고를 때마다 다시 그리므로 처음 열 때만 번쩍
            ShowDialog("hubpack");
            if (fresh) StartEntrances(container);   // 22차-ui-fixes: 레전더리 카드 번쩍 (GameUI.Entrance.cs)
        }
    }
}
