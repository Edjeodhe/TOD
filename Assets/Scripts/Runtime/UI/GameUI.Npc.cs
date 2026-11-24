// 23차 쉬어가기 NPC, 재봉실 NPC 말풍선 (GameUI 의 나머지 부분). 상인 단추 말풍선과 같은 템플릿(MerchantBubble.uxml 의 merchant-bubble)을 한 번 더 복제해
// 같은 월드 층(merchant-world)에 둔다. 글자는 한 자씩 찍히고, 아직 안 찍힌 글자는 투명하게 둬서 풍선 크기가 흔들리지 않는다.
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        VisualElement npcBubble;
        Label npcBubbleText, npcBubbleName;
        string npcShown;

        bool EnsureNpcBubble()
        {
            if (npcBubble != null) return true;
            VisualElement layer = root?.Q("merchant-world");
            if (merchantBubble == null || layer == null) return false;
            npcBubble = Slot(merchantBubble, "merchant-bubble");
            npcBubble.name = "npc-bubble";
            npcBubble.AddToClassList("npc-bubble");
            npcBubbleText = npcBubble.Q<Label>("merchant-text");
            npcBubbleName = npcBubble.Q<Label>(className: "merchant-name");   // 템플릿의 이름 줄 ("단추") → NPC 이름
            layer.Add(npcBubble);
            Show(npcBubble, false);
            return true;
        }

        // 매 프레임. line = 지금 말(없으면 null), visible = 찍힌 글자 수, head = 머리 위 캔버스 점, speaker = 이름 줄
        public void UpdateNpcBubble(string line, int visible, Vector2? head, string speaker = null)
        {
            if (!EnsureNpcBubble()) return;
            if (npcBubbleName != null && speaker != null && npcBubbleName.text != speaker) npcBubbleName.text = speaker;
            bool show = !string.IsNullOrEmpty(line) && head != null && Opened == "" && root.panel != null;
            Show(npcBubble, show);
            if (!show) return;
            string text = Typed(line, visible);
            if (npcShown != text) { npcBubbleText.text = text; npcShown = text; }
            Vector3 screen = worldCamera.WorldToScreenPoint(Space.Canvas(head.Value));
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
            npcBubble.style.left = p.x;
            npcBubble.style.top = Mathf.Max(p.y, 170);
        }
    }
}
