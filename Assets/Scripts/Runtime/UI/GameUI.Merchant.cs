// 16차 상인 단추 말풍선 (GameUI 의 나머지 부분).
// 상점 창이 닫혀 있으면 상인 머리 위 월드 말풍선(merchant-world 층), 상점 창이 열려 있으면 창 오른쪽 위에 상인 얼굴 + 말풍선(merchant-peek).
// 구조는 템플릿 MerchantBubble.uxml, 모양은 Merchant.uss. 글자는 한 자씩 찍히고, 아직 안 찍힌 글자는 투명하게 둬서 풍선 크기가 흔들리지 않는다.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        public VisualTreeAsset merchantBubble;    // MerchantBubble.uxml (merchant-bubble, merchant-peek)

        public event Action<string> DialogClosed; // 닫힌 대화 상자 id (CloseDialog)

        VisualElement merchantWorld, worldBubble;
        Label worldBubbleText;
        string shownMerchantText;

        void InitMerchant()
        {
            merchantWorld = root.Q("merchant-world");
            if (merchantBubble == null || merchantWorld == null) return;
            worldBubble = Slot(merchantBubble, "merchant-bubble");
            worldBubbleText = worldBubble.Q<Label>("merchant-text");
            merchantWorld.Add(worldBubble);
            Show(worldBubble, false);
            root.Query(className: "merchant-peek-slot").ForEach(slot =>
            {
                VisualElement peek = Slot(merchantBubble, "merchant-peek");
                slot.Add(peek);
            });
        }

        // 매 프레임. head = 상인 머리 위 캔버스 점 (상인이 없으면 null), present = 이 화면에 상인이 있음
        public void UpdateMerchant(MerchantTalk talk, float now, Vector2? head, bool present)
        {
            if (worldBubble == null) return;
            bool showing = present && talk.Showing(now);
            string text = showing ? Typed(talk.Line, talk.Visible(now)) : "";
            bool inShop = Opened is "shop" or "hubshop";

            // 월드 말풍선
            bool world = showing && !inShop && Opened == "" && head != null && root.panel != null;
            Show(worldBubble, world);
            if (world)
            {
                if (shownMerchantText != text) { worldBubbleText.text = text; shownMerchantText = text; }
                Vector3 screen = worldCamera.WorldToScreenPoint(Space.Canvas(head.Value));
                Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
                worldBubble.style.left = p.x;
                worldBubble.style.top = Mathf.Max(p.y, 170);
            }

            // 상점 창 안 얼굴 + 말풍선
            if (!inShop) return;
            VisualElement dialog = root.Q($"dialog-{Opened}");
            VisualElement peek = dialog?.Q("merchant-peek");
            if (peek == null) return;
            MerchantFrame frame = talk.Pose(now);
            VisualElement face = peek.Q("merchant-face");
            Sprite sprite = talk.Sprite(frame.mood);
            if (sprite != null && face.resolvedStyle.backgroundImage.sprite != sprite) face.style.backgroundImage = new StyleBackground(sprite);
            face.style.scale = new Scale(new Vector2(frame.scaleX, frame.scaleY));
            face.style.rotate = new Rotate(frame.angle);
            face.style.translate = new Translate(0, -frame.hop * 1.2f);
            VisualElement bubble = peek.Q("merchant-peek-bubble");
            Show(bubble, showing);
            if (showing) { Label label = bubble.Q<Label>("merchant-text"); if (label.text != text) label.text = text; }
        }

        // 찍힌 글자 + 아직 안 찍힌 글자는 투명 (풍선 크기 고정)
        static string Typed(string line, int visible)
        {
            if (string.IsNullOrEmpty(line)) return "";
            visible = Mathf.Clamp(visible, 0, line.Length);
            return visible >= line.Length ? line : line.Substring(0, visible) + "<alpha=#00>" + line.Substring(visible);
        }
    }
}
