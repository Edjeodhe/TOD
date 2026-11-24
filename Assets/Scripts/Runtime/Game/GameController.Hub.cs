// 15차 재봉실 허브: 로비에서 걸어 다니기, 시설 E 상호작용, 상인 단추 조각 상점 (GameController 의 나머지 부분).
// 그리기는 HubView, 꼬리표와 상점 창은 GameUI.Hub.cs, 상점 판정은 Sim/HubShop.cs.
using UnityEngine;
using UnityEngine.InputSystem;

namespace TOD
{
    public partial class GameController
    {
        public HubView hub;
        public GameObject hubRoot;
        bool hubFromRun;

        bool InHub => Run == null && screen == UiScreen.Lobby && hub != null;

        void InitHub()
        {
            if (hub == null) return;
            hub.Init();
            InitMerchant();   // 16차 상인 단추 대사 (GameController.Merchant.cs)
            Profile.hubStock ??= new System.Collections.Generic.List<int>();
            if (HubShop.Ensure(db, Profile, () => Random.value)) Gear.Save(db, Profile);
        }

        // ShowScreen 에서: 로비 = 허브 켜기. 17차: 다락 방의 실궤 창(UiScreen.Attic)이 열린 동안에도 방을 뒤에 그대로 둔다.
        void SyncHub(UiScreen next)
        {
            bool behindChest = next == UiScreen.Attic && hub != null && hub.InAttic;
            if (hubRoot != null) hubRoot.SetActive(next == UiScreen.Lobby || behindChest);
            ui.SetAtticOverRoom(behindChest);
            if (next != UiScreen.Lobby || hub == null) { ui.ShowHubPrompt(null, Vector2.zero); return; }
            hub.Enter(hubFromRun);
            hubFromRun = false;
            ui.SetHubRoom(hub.Room);
        }

        // 17차: 재봉실 ↔ 다락 방. 화면이 잠깐 어두워지는 사이에 방을 바꾼다.
        bool roomMoving;
        void MoveRoom(bool attic)
        {
            if (hub == null || roomMoving) return;
            roomMoving = true;
            if (!string.IsNullOrEmpty(db.hub.roomSound)) sound.Play(db.hub.roomSound);
            ui.FadeRoom(db.hub.roomFade, () =>
            {
                hub.SetRoom(attic);
                ui.SetHubRoom(hub.Room);
                roomMoving = false;
            });
        }

        // 결과 화면의 '예견의 다락' 등: 재봉실로 돌아와 다락 방에 선다.
        public void GoToAttic()
        {
            if (Run != null) return;
            ui.CloseDialog();
            ShowScreen(UiScreen.Lobby);
            if (hub != null && !hub.InAttic) { hub.SetRoom(true); ui.SetHubRoom(hub.Room); }
        }

        // 등반이 끝날 때(Finish): 돌아오면 탑 입구 앞에 서고, 상인이 새 조각을 진열한다.
        void HubAfterRun()
        {
            hubFromRun = true;
            HubShop.Restock(db, Profile, () => Random.value);
            Gear.Save(db, Profile);
        }

        void TickHub(float dt)
        {
            if (!InHub) return;
            Vector2 move = Vector2.zero;
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                move.x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
                move.y = (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0) - (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0);
            }
            if (hubAutoMove != Vector2.zero) move = hubAutoMove;
            bool free = Opened == "" && !roomMoving;
            hub.Tick(dt, move, free, Clock);
            HubFacility near = free ? hub.Near : null;
            ui.ShowHubPrompt(near?.prompt, near != null ? hub.PromptAnchor(near) : Vector2.zero);
        }

        // 검증용: 0 이 아니면 키보드 대신 이 방향으로 걷는다 (플레이 캡처 때 execute_code 로 넣는다)
        public Vector2 hubAutoMove;

        // 허브의 E: 가까운 시설
        void HubInteract()
        {
            if (!InHub || Opened != "") return;
            HubFacility f = hub.Near;
            if (f == null) return;
            UseFacility(f.station);
        }

        public void UseFacility(HubStation station)
        {
            switch (station)
            {
                case HubStation.SewingBox: ShowScreen(UiScreen.Inventory); break;
                case HubStation.Attic: MoveRoom(true); break;            // 17차: 다락 방으로 올라간다
                case HubStation.AtticExit: MoveRoom(false); break;
                case HubStation.Chest: ShowScreen(UiScreen.Attic); sound.Play("loot"); break;   // 모이라의 실궤 = 강화 창
                case HubStation.Merchant: ShowHubShop(); MerchantSay(MerchantCue.Greet); break;
                case HubStation.Tower: StartRun(); break;
                case HubStation.Diary: ui.ShowDiary(); break;
            }
        }

        // ---------------------------------------------------------------- 상인 단추 (파편으로 조각 사기)

        public void ShowHubShop()
        {
            if (HubShop.Ensure(db, Profile, () => Random.value)) Gear.Save(db, Profile);
            ui.ShowHubShop(db.hub, HubShop.Display(db, Profile), k => HubShop.Price(db, k), k => HubShop.Sold(Profile, k), Profile.shards, BuyHubPiece);
        }

        void BuyHubPiece(int kind)
        {
            if (HubShop.Sold(Profile, kind)) { MerchantSay(MerchantCue.SoldOut); return; }
            if (Profile.shards < HubShop.Price(db, kind)) { ui.Say(db.hub.noShards); MerchantSay(MerchantCue.NoMoney); return; }
            PieceDef piece = HubShop.Buy(db, Profile, kind);
            if (piece == null) return;
            Gear.Save(db, Profile);
            sound.Play(string.IsNullOrEmpty(db.hub.buySound) ? "loot" : db.hub.buySound);
            ui.Say(string.Format(db.hub.bought, piece.name));
            ui.UpdateMeta(Profile);
            MerchantSay(MerchantCue.Buy);
            ShowHubShop();
        }
    }
}
