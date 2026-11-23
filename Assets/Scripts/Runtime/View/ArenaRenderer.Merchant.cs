// 16차 쉬어가기 라운드의 상인 단추: 허브와 같은 자세 그림과 트윈(MerchantTalk)으로 그린다. 말풍선 자리(MerchantHead)도 여기서 정한다.
using UnityEngine;

namespace TOD
{
    public partial class ArenaRenderer
    {
        public MerchantTalk merchantTalk { get; set; }
        public Vector2? MerchantHead { get; private set; }

        void DrawMerchant(RunState r, Interactable nearest, float clock)
        {
            MerchantHead = null;
            if (r.merchant == null) return;
            MerchantFrame frame = merchantTalk != null ? merchantTalk.Pose(clock) : new MerchantFrame { scaleX = 1, scaleY = 1 };
            Sprite sprite = merchantTalk?.Sprite(frame.mood) ?? merchantSprite;
            Vector2 at = C(r.merchant);
            bool near = nearest == Interactable.Merchant;
            float h = 0;
            if (sprite != null)
            {
                Rect rect = sprite.rect;
                h = Pal.merchantHeight > 0 ? Pal.merchantHeight : rect.height;
                float w = h * rect.width / rect.height;
                painter.Ellipse(Layer.Ground, at, w * .4f * (1 - frame.hop / Mathf.Max(1, h) * 1.5f), w * .4f * .34f, Pal.shadow);
                // Painter 는 스프라이트 피벗을 점에 놓는다: 발밑이 at 에 오도록 피벗 높이만큼 올리고, 뜀 높이를 더한다.
                float pivot = rect.height > 0 ? sprite.pivot.y / rect.height : .5f;
                Vector2 point = at - new Vector2(0, pivot * h * frame.scaleY + frame.hop);
                painter.Sprite(Layer.Ground, sprite, point, frame.angle * Mathf.Deg2Rad, w * frame.scaleX, h * frame.scaleY, Color.white);
            }
            if (near) painter.Ellipse(Layer.Ground, at, 46, 46 * Space.Proj, Painter.Alpha(Pal.promptActive, .7f), 1.5f);
            PropLabel(db.ui.promptMerchant, at - new Vector2(0, h * .92f + 18), near);
            MerchantHead = at - new Vector2(0, h + db.merchant.bubbleLift);
        }
    }
}
