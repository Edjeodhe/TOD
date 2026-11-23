// 16차 상인 단추: 상황별 대사와 자세 (허브와 쉬어가기 라운드 공용). 계산은 Sim/MerchantTalk.cs,
// 그리기는 HubView(허브)와 ArenaRenderer.Merchant.cs(쉬어가기), 말풍선은 GameUI.Merchant.cs.
namespace TOD
{
    public partial class GameController
    {
        MerchantTalk merchantTalk;
        bool merchantNear;
        int merchantBlips;
        string merchantBlipLine;

        public MerchantTalk Merchant => merchantTalk;

        void InitMerchant()
        {
            merchantTalk = new MerchantTalk(db.merchant, () => UnityEngine.Random.value);
            if (hub != null) hub.merchant = merchantTalk;
            arena.merchantTalk = merchantTalk;
            ui.DialogClosed += id => { if (id is "shop" or "hubshop") MerchantSay(MerchantCue.Farewell); };
        }

        public void MerchantSay(MerchantCue cue)
        {
            if (merchantTalk == null) return;
            string line = merchantTalk.Say(cue, Clock);
            if (line == null) return;
            merchantBlips = 0;
            merchantBlipLine = line;
            if (!string.IsNullOrEmpty(db.merchant.talkSound)) sound.Play(db.merchant.talkSound);
        }

        // 매 프레임: 다가오면 혼잣말, 글자 찍힘 소리, 말풍선 위치
        void TickMerchant()
        {
            if (merchantTalk == null) return;
            bool hubMerchant = InHub;
            bool runMerchant = Run != null && Run.merchant != null && screen == UiScreen.Combat && !runInventory;
            bool near = hubMerchant ? hub.Near?.station == HubStation.Merchant : runMerchant && Combat.Nearest(Run) == Interactable.Merchant;
            if (near && !merchantNear && Opened == "")
            {
                string line = merchantTalk.Approach(Clock);
                if (line != null) { merchantBlips = 0; merchantBlipLine = line; if (!string.IsNullOrEmpty(db.merchant.talkSound)) sound.Play(db.merchant.talkSound, .7f); }
            }
            merchantNear = near;
            merchantTalk.Update(Clock);

            // 글자가 blipEvery 자씩 찍힐 때마다 짧은 소리 (빈칸은 건너뜀)
            string text = merchantTalk.Line;
            if (text != null && text == merchantBlipLine && db.merchant.blipEvery > 0 && !string.IsNullOrEmpty(db.merchant.blipSound))
            {
                int shown = merchantTalk.Visible(Clock);
                int due = shown / db.merchant.blipEvery;
                if (due > merchantBlips)
                {
                    merchantBlips = due;
                    if (shown > 0 && shown <= text.Length && !char.IsWhiteSpace(text[shown - 1])) sound.Play(db.merchant.blipSound, .85f + UnityEngine.Random.value * .3f);
                }
            }

            UnityEngine.Vector2? head = hubMerchant ? hub.MerchantHead() : runMerchant ? arena.MerchantHead : null;
            ui.UpdateMerchant(merchantTalk, Clock, head, hubMerchant || runMerchant);
        }
    }
}
