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
            if (hub != null && hubRoot != null && hubRoot.activeSelf) hub.AnimateMoira(dt);   // 19차: 모이라 카드 창 동안에도
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
                case HubStation.Attic: MoveRoom(true); break;            // 17차: 다락 방으로 올라간다
                case HubStation.AtticExit: MoveRoom(false); break;
                case HubStation.Moira: TalkToMoira(); sound.Play("loot"); break;   // 19차: 모이라 = 강화 4종 창
                case HubStation.Merchant: ShowHubShop(); MerchantSay(MerchantCue.Greet); break;
                case HubStation.Tower: StartRun(); break;
                case HubStation.Diary: ui.ShowDiary(); break;
            }
        }

        // ---------------------------------------------------------------- 상인 단추 메뉴 (19차)
        // '시작 조각 챙기기' = 재봉 상자 판이 열리고 진열 조각을 판에 놓으면 파편으로 산다(다음 등반 한 번만, GameController.cs PackOffer).
        // '들고 갈 수 늘리기' = 17차 모이라의 '시작 조각 선택' 강화를 단추가 맡는다.

        public void ShowHubShop()
        {
            if (HubShop.Ensure(db, Profile, () => Random.value)) Gear.Save(db, Profile);
            UpgradeDef slots = Upgrade(Unlocks.StarterKey);
            ui.ShowHubMenu(db.hub, Profile, Profile.placements.Count, Unlocks.StarterSlots(db, Profile), slots,
                slots != null ? Cost(slots) : 0, slots != null && Maxed(slots), slots != null ? Level(slots) : 0, OpenPacking, BuySlot);
        }

        void OpenPacking()
        {
            picking = null;
            selected = null;
            ui.CloseDialog();
            ShowScreen(UiScreen.Inventory);
            sound.Play("loot");
        }

        void BuySlot()
        {
            UpgradeDef u = Upgrade(Unlocks.StarterKey);
            if (u == null || Maxed(u)) return;
            if (Profile.shards < Cost(u)) { ui.Say(db.hub.noShards); MerchantSay(MerchantCue.NoMoney); return; }
            AskUpgrade(u, -1, string.Format(db.ui.upgradeBody, Cost(u), u.name));
        }
    }
}
