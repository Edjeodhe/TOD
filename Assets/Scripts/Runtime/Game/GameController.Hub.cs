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
            if (HubShop.Ensure(db, Profile, () => Random.value, UnixNow)) Gear.Save(db, Profile);
        }

        // 20차: 진열이 바뀌는 실제 시각 (유닉스 초)
        static long UnixNow => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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

        // 등반이 끝날 때(Finish): 돌아오면 탑 입구 앞에 선다. (20차: 상인 진열은 등반 대신 실제 시각으로 바뀐다)
        void HubAfterRun()
        {
            hubFromRun = true;
            Profile.npcGifted?.Clear();   // 23차: 재봉실 NPC 선물은 등반마다 한 번
            if (HubShop.Ensure(db, Profile, () => Random.value, UnixNow)) Gear.Save(db, Profile);
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
            bool dash = (k != null && k.spaceKey.wasPressedThisFrame) || hubAutoDash;   // 23차: 재봉실에서도 Space 대시
            hubAutoDash = false;
            if (hub.Tick(dt, move, free, Clock, dash)) sound.Play("dash");
            HubFacility near = free ? hub.Near : null;
            ui.ShowHubPrompt(near?.prompt, near != null ? hub.PromptAnchor(near) : Vector2.zero);
        }

        // 검증용: 0 이 아니면 키보드 대신 이 방향으로 걷는다 (플레이 캡처 때 execute_code 로 넣는다)
        public Vector2 hubAutoMove;
        public bool hubAutoDash;   // 검증용: true 면 다음 프레임에 대시 한 번

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
                case HubStation.NpcBlessing: case HubStation.NpcGift: case HubStation.NpcMemory: case HubStation.NpcGamble:
                    TalkNpcHub(HubView.NpcOf(station)); break;   // 23차: 만난 NPC 의 선물 (등반마다 한 번)
            }
        }

        // ---------------------------------------------------------------- 상인 단추 메뉴 (19차, 20차)
        // '시작 조각 챙기기' = 무작위 진열 카드 창(dialog-hubpack). 카드를 누르면 파편을 내고 챙기고, 고른 카드를 다시 누르면 되돌린다.
        //   고른 조각은 판에 놓지 않은 채 들어오고 재봉 상자(I, 아웃게임에서 언제든)에서 배치한다. 한도는 퍼즐 칸 수(HubShop.Take).
        // '퍼즐 칸 추가' = 챙길 칸 +1 강화 (key starter, 옛 '들고 갈 수 늘리기').

        public void ShowHubShop()
        {
            if (HubShop.Ensure(db, Profile, () => Random.value, UnixNow)) Gear.Save(db, Profile);
            UpgradeDef slots = Upgrade(Unlocks.StarterKey);
            ui.ShowHubMenu(db.hub, Profile, HubShop.PackedCells(db, Profile), Unlocks.StarterCells(db, Profile), slots,
                slots != null ? Cost(slots) : 0, slots != null && Maxed(slots), slots != null ? Level(slots) : 0, OpenPacking, BuySlot);
        }

        void OpenPacking()
        {
            if (HubShop.Ensure(db, Profile, () => Random.value, UnixNow)) Gear.Save(db, Profile);
            RenderPackCards();
            sound.Play("loot");
        }

        void RenderPackCards() =>
            ui.ShowPackCards(Profile, HubShop.Offers(db, Profile), k => HubShop.Taken(Profile, k), k => HubShop.Price(db, k),
                HubShop.PackedCells(db, Profile), Unlocks.StarterCells(db, Profile), HubShop.Picks(db), HubShop.SecondsLeft(db, Profile, UnixNow),
                PickPackCard, OpenOutgameInventory);

        void PickPackCard(int kind)
        {
            HubConfig h = db.hub;
            string name = db.gear.pieces[kind].name;
            if (HubShop.Taken(Profile, kind))
            {
                int refund = HubShop.Return(db, Profile, kind);
                if (refund >= 0) ui.Say(string.Format(h.packReturned ?? "{0}", name, refund));
            }
            else
            {
                switch (HubShop.Take(db, Profile, kind, out _))
                {
                    case PackResult.NoShards: ui.Say(h.noShards); MerchantSay(MerchantCue.NoMoney); RenderPackCards(); return;
                    case PackResult.TooBig:
                        ui.Say(string.Format(h.packTooBig ?? "{0}/{1}", HubShop.PackedCells(db, Profile) + HubShop.Cells(db, kind), Unlocks.StarterCells(db, Profile)));
                        RenderPackCards(); return;
                    case PackResult.PickLimit: ui.Say(string.Format(h.packLimit ?? "{0}", HubShop.Picks(db))); RenderPackCards(); return;
                    case PackResult.Packed:
                        sound.Play(string.IsNullOrEmpty(h.buySound) ? "loot" : h.buySound);
                        ui.Say(string.Format(h.packed ?? "{0}", name));
                        MerchantSay(MerchantCue.Buy, false);   // 23차: 챙길 때 말풍선 글자 소리 없음
                        break;
                    default: RenderPackCards(); return;
                }
            }
            Gear.Save(db, Profile);
            ui.UpdateMeta(Profile);
            RenderPackCards();
        }

        // 20차: 아웃게임 재봉 상자 = 챙긴 조각을 배치하는 판 (I, 카드 창의 버튼)
        void OpenOutgameInventory()
        {
            if (Run != null) return;
            ui.CloseDialog();
            picking = null;
            selected = null;
            ShowScreen(UiScreen.Inventory);
            sound.Play("loot");
        }

        // 아웃게임의 I: 재봉실(다락 방 포함)에서 언제든 열고, 열려 있으면 닫는다
        void ToggleOutgameInventory()
        {
            if (Run != null) return;
            if (screen == UiScreen.Inventory && picking == null) { BackFromRoom(); return; }
            if (screen == UiScreen.Lobby && (Opened == "" || Opened == "hubpack" || Opened == "hubshop") && !roomMoving) OpenOutgameInventory();
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
