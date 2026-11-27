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

        // voiced = false 면 말풍선만 띄우고 소리(말 소리, 글자 찍힘)는 내지 않는다 (23차: 조각 챙길 때)
        public void MerchantSay(MerchantCue cue, bool voiced = true)
        {
            if (merchantTalk == null) return;
            string line = merchantTalk.Say(cue, Clock);
            if (line == null) return;
            merchantBlips = 0;
            merchantBlipLine = voiced ? line : null;
            if (voiced && !string.IsNullOrEmpty(db.merchant.talkSound)) sound.Play(db.merchant.talkSound);
        }

        // 26차: E 로 말 걸기. 얼굴이 있으면 인사를 아래 대화창으로(자세는 인사, 머리 위 말풍선은 안 띄움) → E 로 닫으면 상점.
        // 얼굴이 없으면 예전처럼 바로 상점 + 상점 창 안 말풍선 인사
        void GreetMerchant(System.Action openShop)
        {
            MerchantConfig c = db.merchant;
            if (c != null && c.portrait != null && merchantTalk != null && db.talk != null)
            {
                string line = merchantTalk.Say(MerchantCue.Greet, Clock);
                merchantTalk.Hush();
                if (line != null && OpenTalk(c.talkName, c.portrait, line, openShop))
                {
                    if (!string.IsNullOrEmpty(c.talkSound)) sound.Play(c.talkSound);
                    return;
                }
            }
            openShop();
            MerchantSay(MerchantCue.Greet);
        }

        // 매 프레임: 다가오면 혼잣말, 글자 찍힘 소리, 말풍선 위치
        void TickMerchant()
        {
            if (merchantTalk == null) return;
            bool hubMerchant = InHub && !hub.InAttic;   // 17차: 다락 방에는 상인이 없다
            bool runMerchant = Run != null && Run.merchant != null && screen == UiScreen.Combat && !runInventory;
            bool near = hubMerchant ? hub.Near?.station == HubStation.Merchant : runMerchant && Combat.Nearest(Run) == Interactable.Merchant;
            if (near && !merchantNear && Opened == "")
            {
                // 23차: 다가올 때 머리 위에 뜨는 혼잣말은 소리 없이 (말 소리, 글자 찍힘 모두)
                if (merchantTalk.Approach(Clock) != null) { merchantBlips = 0; merchantBlipLine = null; }
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
