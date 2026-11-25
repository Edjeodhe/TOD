// 24차 예언 카드: 미래 보기를 읽어 둔 동안(다음 등반 전까지) 재봉실 캐릭터 옆에 '확률 오른 세트'를 작은 카드로 띄운다.
// 카드는 Main.uxml 의 fate-world/fate-badge. 모이라 카드 창을 닫고 방으로 돌아오면 톡 튀어나온다.
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        VisualElement fateBadge, fateBadgeIcon;
        Label fateBadgeTag, fateBadgeSet, fateBadgeText;
        float fateBadgeShownAt = -1;
        SetDef fateBadgeFor;

        // set = 예언한 세트 (null 이면 숨김), foot = 캐릭터 발밑 캔버스 점, bonus = 오른 비율 (2 = +200%)
        public void UpdateFateBadge(SetDef set, Sprite icon, float bonus, Vector2? foot)
        {
            if (fateBadge == null)
            {
                fateBadge = root?.Q("fate-badge");
                if (fateBadge == null) return;
                fateBadgeIcon = fateBadge.Q("fate-badge-icon");
                fateBadgeTag = fateBadge.Q<Label>("fate-badge-tag");
                fateBadgeSet = fateBadge.Q<Label>("fate-badge-set");
                fateBadgeText = fateBadge.Q<Label>("fate-badge-text");
            }
            bool show = set != null && foot != null && root.panel != null && Opened == "" && !FateShowing;
            Show(fateBadge, show);
            if (!show) { fateBadgeShownAt = -1; return; }
            HubConfig h = db.hub;
            if (fateBadgeFor != set)
            {
                fateBadgeFor = set;
                fateBadgeTag.text = h.fateBadgeTag ?? "";
                fateBadgeSet.text = set.name;
                fateBadgeSet.style.color = NameColor(set.color);
                fateBadgeText.text = string.Format(h.fateBadgeText ?? "{0} +{1}%", set.name, Mathf.RoundToInt(bonus * 100));
                fateBadge.style.unityBackgroundImageTintColor = PaperTint(set.color);
                SetBorder(fateBadge, set.color);
                fateBadgeIcon.style.backgroundImage = icon != null ? new StyleBackground(icon) : StyleKeyword.None;
                fateBadgeIcon.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (fateBadgeShownAt < 0) fateBadgeShownAt = Time.unscaledTime;
            float pop = h.fateBadgePop > 0 ? Mathf.Clamp01((Time.unscaledTime - fateBadgeShownAt) / h.fateBadgePop) : 1;
            float s = pop < 1 ? .3f + .7f * EaseOutBack(pop) : 1;
            fateBadge.style.scale = new Scale(new Vector2(s, s));
            fateBadge.style.opacity = Mathf.Clamp01(pop * 2.5f);
            // 발밑 + 오프셋 = 카드 왼쪽 아래 (살짝 둥실)
            Vector2 at = foot.Value + h.fateBadgeOffset + new Vector2(0, Mathf.Sin(Time.unscaledTime * 2.2f) * 3);
            Vector3 screen = worldCamera.WorldToScreenPoint(Space.Canvas(at));
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
            float height = fateBadge.resolvedStyle.height;
            fateBadge.style.left = p.x;
            fateBadge.style.top = p.y - (float.IsNaN(height) ? 0 : height);
        }
    }
}
