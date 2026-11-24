// 입력·화면·오디오·등반 진행 연결 (웹판 main.js).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TOD
{
    public partial class GameController : MonoBehaviour   // 15차 해금, 시작 조각: GameController.Outgame.cs
    {
        public GameDatabase db;
        public ArenaRenderer arena;
        public GameUI ui;
        public SoundBank sound;
        public Camera worldCamera;
        public GameObject arenaRoot;
        public AutoPilot autopilot;
        public float whisperInterval, deathDuration;

        public Combat Combat { get; private set; }
        public Profile Profile { get; private set; }
        // 17차: 재봉 상자가 다루는 가방. 등반 중 = 등반 가방(RunState.bag), 아웃게임 = 영구 시작 조각(Profile)
        public Profile Bag => Run != null ? Run.bag : Profile;
        public RunState Run { get; private set; }

        UiScreen screen = UiScreen.Lobby;
        UiScreen roomReturn = UiScreen.Lobby;
        (Outcome outcome, int floor, int kills)? summary;
        bool paused, animatingDeath, holdingFire, fireRequested, pullRequested, dashRequested;
        float deathClock, lastWhisper;
        Vector2 aim;
        int? selected;
        int turns;
        string pendingUpgrade;
        Shop shop;

        class Shop { public string sale; public float end; public readonly HashSet<string> sold = new(); public bool expired; }

        static readonly int EnabledId = Shader.PropertyToID("_TOD_Enabled"), DesireId = Shader.PropertyToID("_TOD_Desire"), MotionId = Shader.PropertyToID("_TOD_Motion"),
            FlashId = Shader.PropertyToID("_TOD_Flash"), SnapId = Shader.PropertyToID("_TOD_Snap"), HurtId = Shader.PropertyToID("_TOD_Hurt"),
            BorderId = Shader.PropertyToID("_TOD_Border"), CanvasHeightId = Shader.PropertyToID("_TOD_CanvasHeight"),
            PurifiedId = Shader.PropertyToID("_TOD_Purified"), WarpId = Shader.PropertyToID("_TOD_Warp"), WarpTintId = Shader.PropertyToID("_TOD_WarpTint"),
            WarpParamsId = Shader.PropertyToID("_TOD_WarpParams");

        float Clock => Time.unscaledTime;
        string Opened => ui.Opened;
        Vector2 Center => new(db.arena.width / 2, db.arena.height / 2);

        void Start()
        {
            Space.Configure(db.arena);
            Combat = new Combat(db);
            Profile = Gear.Load(db);
            arena.Text = ui;
            arena.Init(Combat);
            sound.Init(db.sounds);
            sound.Enabled = true;
            ui.Init(this);
            InitHub();   // 15차 재봉실 허브: GameController.Hub.cs
            InitMoira();   // 19차 모이라 카드: 확인 창이 닫히면 카드 갱신
            aim = Center;
            Shader.SetGlobalFloat(CanvasHeightId, db.arena.canvasHeight);
            ShowScreen(UiScreen.Lobby);
        }

        // ---------------------------------------------------------------- screens

        public void ShowScreen(UiScreen next)
        {
            if (Run != null && next != UiScreen.Combat) return;
            if (next == UiScreen.Attic && Run == null && hub != null && !hub.InAttic && screen != UiScreen.Summary) { GoToAttic(); return; }   // 17차: 다락은 걸어 들어가는 방
            if (next is UiScreen.Inventory or UiScreen.Attic) roomReturn = screen is UiScreen.Summary ? UiScreen.Summary : UiScreen.Lobby;
            if (next != UiScreen.Inventory) picking = null;   // 19차: 줄 고르기는 판이 열린 동안만
            screen = next;
            if (next == UiScreen.Lobby) summary = null;
            ui.ShowScreen(next);
            arenaRoot.SetActive(next == UiScreen.Combat);
            SyncHub(next);   // 15차: 로비 = 재봉실 허브
            if (next == UiScreen.Inventory) RenderInventory();
            if (next == UiScreen.Attic) RenderAttic();
            if (hub != null && hub.moira != null) hub.moira.Talking = next == UiScreen.Attic;   // 19차: 모이라가 카드 넷을 든다
            if (next == UiScreen.Summary && summary is { } s) ui.RenderSummary(s.outcome, s.floor, s.kills, Profile.shards);
            ui.UpdateMeta(Profile);
            if (next == UiScreen.Lobby) ShowUnlocks();   // 15차: 새로 열린 세트 알림
        }

        public void BackFromRoom()
        {
            if (runInventory) { CloseRunInventory(); return; }
            if (picking != null) { EndPick(); return; }   // 19차: 모이라의 줄 고르기 → 모이라 창으로
            if (screen == UiScreen.Attic && Run == null && hub != null && hub.InAttic && summary == null) { LeaveMoira(); return; }   // 19차: 카드가 손으로 돌아간 뒤
            ShowScreen(summary != null ? roomReturn : UiScreen.Lobby);
        }

        public void ToggleSound()
        {
            sound.Enabled = !sound.Enabled;
            if (sound.Enabled) sound.Play("loot");
            ui.Say(sound.Enabled ? db.ui.soundOn : db.ui.soundOff);
        }

        // ---------------------------------------------------------------- run flow

        public void StartRun()
        {
            if (Run != null || Opened != "") return;
            ui.CloseDialog();
            summary = null;
            paused = false;
            Run = Combat.CreateRun(Profile, () => Random.value);
            HubShop.ConsumePacked(Profile);   // 19차: 챙긴 시작 조각은 이번 등반 가방으로 넘어가고 영구 프로필에서는 사라진다
            Moira.ConsumeFate(Profile);       // 19차: 모이라가 읽어 둔 1층 속성은 이번 등반(RunState.fate)에 쓰였다
            Gear.Save(db, Profile);
            offered = null;
            augmentRerolled = false;
            runInventory = false;
            shop = null;
            aim = Center;
            arena.Clear();
            Combat.BeginFloor(Run);
            ShowScreen(UiScreen.Combat);
            arena.BeginRound(Run);
        }

        public void Finish(Outcome outcome)
        {
            if (Run == null) return;
            summary = (outcome, Run.floor, Run.kills);
            HubAfterRun();   // 15차: 귀환 자리, 상인 새 진열
            Gear.Save(db, Profile);
            Run = null;
            paused = false;
            animatingDeath = false;
            ui.CloseDialog();
            arena.Clear();
            Shader.SetGlobalFloat(EnabledId, 0);
            ShowScreen(UiScreen.Summary);
        }

        // 플레이어가 움직일 수 있는 상태 (전투·정화·라운드 종료 모두 포함, 대화 상자·일시 정지 제외)
        bool IsCombat => Run != null && Run.phase != Phase.Victory && !paused && Opened == "" && !runInventory && Run.outcome == Outcome.None;

        // 등반 중 재봉 상자(I): 라운드가 끝났거나 쉬어가는 라운드일 때만 연다. 전투는 멈춘다.
        bool runInventory;
        public bool CanOpenRunInventory => Run != null && Run.phase == Phase.Cleared && Opened == "" && !paused;

        public void ToggleRunInventory()
        {
            if (runInventory) { CloseRunInventory(); return; }
            if (!CanOpenRunInventory) return;
            runInventory = true;
            selected = null;
            ui.ShowScreen(UiScreen.Inventory);
            ui.SetInventoryInRun(true);
            RenderInventory();
            sound.Play("loot");
        }

        void CloseRunInventory()
        {
            if (!runInventory) return;
            runInventory = false;
            Combat.RefreshGear(Run);
            Gear.Save(db, Profile);
            ui.SetInventoryInRun(false);
            ui.ShowScreen(UiScreen.Combat);
        }

        public void TogglePause()
        {
            if (Run == null || Run.phase == Phase.Victory || Run.outcome != Outcome.None || (Opened != "" && Opened != "pause")) return;
            paused = !paused;
            if (paused) ui.ShowDialog("pause"); else ui.CloseDialog();
        }

        public void AskRetreat() { shopOpenedBeforeRetreat = Opened == "shop"; ui.ShowDialog("retreat"); }

        public void CancelRetreat()
        {
            bool fromShop = shopOpenedBeforeRetreat;
            ui.CloseDialog();
            if (paused) { paused = false; TogglePause(); }
            else if (fromShop) ShowShop();
        }

        bool shopOpenedBeforeRetreat;
        List<AugmentDef> offered;

        // Esc 로 닫았다가 E 로 다시 열면 같은 선택지를 보여 준다 (다시 굴리기 방지).
        void ShowAugments()
        {
            offered ??= Combat.AugmentChoices(Run);
            ui.ShowAugments(offered, Run, PickAugment, !augmentRerolled);
        }

        // 증강 창 아래 '다시 굴리기': 선택 한 번마다 한 번.
        bool augmentRerolled;
        public void RerollAugments()
        {
            if (Run == null || Opened != "augment" || augmentRerolled || offered == null) return;
            augmentRerolled = true;
            offered = Combat.AugmentChoices(Run, offered);
            sound.Play("reroll");
            ShowAugments();
        }

        // 쉬어가기 탑 아이템 창의 '다시 굴리기': 층마다 한 번.
        public void RerollArtifacts()
        {
            if (Run == null || Opened != "artifact" || !Combat.RerollArtifacts(Run)) return;
            sound.Play("reroll");
            ui.ShowArtifacts(Run.floor, Run.artifactOffer, PickArtifact, !Run.artifactRerolled);
        }

        void PickAugment(string id)
        {
            Combat.ApplyAugment(Run, id);
            offered = null;
            augmentRerolled = false;
            AddPlayerFx(FxSprite.Stitch, db.fx.augment);
            ui.CloseDialog();
            sound.Play("upgrade");
        }

        // E: 라운드가 끝난 뒤 쌓인 증강 → 가까운 계단·제단·상인 순으로 상호작용한다.
        public void Interact()
        {
            if (Run == null) { HubInteract(); return; }   // 15차: 허브 시설
            if (Opened != "" || paused) return;
            if (Run.phase != Phase.Combat && Run.augmentQueue > 0) { ShowAugments(); return; }
            switch (Combat.Nearest(Run))
            {
                case Interactable.Stairs:
                    if (Run.roundKind == RoundKind.Boss && Run.desirePending) ui.ShowDesire(Run.offer, Run.floor, Run.offer.id == db.desire.refine.id);
                    else Ascend();
                    break;
                case Interactable.Altar: ui.ShowArtifacts(Run.floor, Run.artifactOffer, PickArtifact, !Run.artifactRerolled); break;
                case Interactable.Merchant: ShowShop(); MerchantSay(MerchantCue.Greet); break;   // 16차 상인 대사
            }
        }

        void PickArtifact(int kind)
        {
            Combat.TakeArtifact(Run, kind);
            ui.CloseDialog();
            sound.Play("loot");
        }

        void Ascend()
        {
            ui.CloseDialog();
            shop = null;
            sound.Play("round");
            Combat.Ascend(Run);
            if (Run.phase != Phase.Victory) arena.BeginRound(Run);
        }

        // 자동 진행(검증용 AutoPilot): 열린 대화 상자의 기본 선택을 고른다.
        public void AutoChoose()
        {
            if (Run == null) return;
            switch (Opened)
            {
                case "augment": PickAugment(offered[0].id); break;
                case "desire": Absorb(); break;
                case "artifact": PickArtifact(Run.artifactOffer[0]); break;
                case "shop": ui.CloseDialog(); break;
                case "pause": TogglePause(); break;
            }
        }

        void AddPlayerFx(FxSprite sprite, FxSpec spec)
        {
            Run.effects.Add(new Fx { kind = FxKind.Sprite, sprite = sprite, x = Run.player.x, y = Run.player.y, size = spec.size, time = spec.time, max = spec.time, lift = spec.lift, alpha = spec.alpha });
        }

        // 보스 정화 뒤 계단에서: 욕망 흡수/거절 후 바로 다음 층으로 올라간다.
        public void Absorb()
        {
            Combat.ChooseDesire(Run, Run.offer.id);
            AddPlayerFx(FxSprite.Boss, db.fx.absorb);
            sound.Play("desire");
            ui.CloseDialog();
            if (Run.outcome == Outcome.None) Ascend();
        }

        public void Refuse() { Combat.ChooseDesire(Run, null); Ascend(); }
        // ---------------------------------------------------------------- shop (단추)

        int Price(WareDef ware)
        {
            float markup = Combat.DesireFraction(Run) >= db.desire.highFraction ? db.desire.priceMarkup : 1;
            float sale = shop.sale == ware.id && shop.end > Clock ? db.shop.saleDiscount : 1;
            return Mathf.CeilToInt(ware.basePrice * markup * sale);
        }

        string SaleLabel()
        {
            int seconds = Mathf.CeilToInt(shop.end - Clock);
            if (seconds <= 0) return db.ui.saleOver;
            if (shop.sold.Contains(shop.sale)) return db.ui.saleSold;
            return string.Format(db.ui.saleActive, seconds, db.shop.wares.First(w => w.id == shop.sale).name);
        }

        void ShowShop()
        {
            shop ??= new Shop { sale = db.shop.wares[Mathf.FloorToInt(Run.random() * db.shop.wares.Length) % db.shop.wares.Length].id, end = Clock + db.shop.saleSeconds };
            ui.ShowShop(db.shop, Price, w => shop.sold.Contains(w.id), Run.gold, SaleLabel(), Buy);
        }

        void Buy(string id)
        {
            WareDef ware = db.shop.wares.First(w => w.id == id);
            if (shop.sold.Contains(id)) { MerchantSay(MerchantCue.SoldOut); return; }
            if (Run.gold < Price(ware)) { MerchantSay(MerchantCue.NoMoney); return; }
            if (id == "piece" && Gear.Unowned(db, Run.bag).Count == 0) { ui.Say(db.ui.ownedAll); MerchantSay(MerchantCue.SoldOut); return; }   // 아이템은 고유: 다 가졌으면 팔지 않는다
            Run.gold -= Price(ware);
            shop.sold.Add(id);
            float potion = Run.desires.Contains("hunger") ? db.desire.hungerPotion : 1;
            switch (id)
            {
                case "heal": Run.hp = Mathf.Min(Run.maxHp, Run.hp + ware.amount * potion); break;
                case "pure": Combat.AdjustDesire(Run, -ware.amount * potion); break;
                case "piece": ui.Say(string.Format(db.messages.loot, Gear.AddItem(db, Run.bag)?.name)); break;
                case "buff": Run.damage += ware.amount; break;
            }
            Gear.Save(db, Profile);
            sound.Play("loot");
            MerchantSay(MerchantCue.Buy);
            ShowShop();
        }

        // ---------------------------------------------------------------- 재봉 상자

        // 19차 아웃게임 판: 모이라의 줄 고르기(picking) 아니면 단추의 '시작 조각 챙기기'(진열 조각을 판에 놓으면 산다)
        bool Packing => Run == null && picking == null;

        void RenderInventory()
        {
            var board = new BoardMode { pick = picking };
            if (Packing)
            {
                board.offers = HubShop.Offers(db, Profile).Select(k => new OwnedItem { id = OfferId + k, kind = k }).ToList();
                board.price = k => HubShop.Price(db, k);
            }
            ui.RenderInventory(Bag, selected, turns, board);
        }

        // 19차: 판 옆 목록의 진열 조각(아직 안 산 것)은 id = OfferId + 종류인 가짜 조각이다. 판에 놓는 순간 산다(HubShop.Pack).
        public const int OfferId = 1000000;

        public OwnedItem ItemFor(int? id)
        {
            if (id == null) return null;
            OwnedItem item = Bag.items.FirstOrDefault(i => i.id == id);
            if (item != null || !Packing || id < OfferId) return item;
            int kind = id.Value - OfferId;
            return HubShop.Offered(db, Profile, kind) ? new OwnedItem { id = id.Value, kind = kind } : null;
        }

        bool IsOffer(OwnedItem item) => item != null && item.id >= OfferId;

        // 진열 조각을 판에 놓기 = 사기. 놓았으면 true.
        bool PackOffer(OwnedItem offer, int x, int y)
        {
            PackResult result = HubShop.Pack(db, Profile, offer.kind, x, y, turns, out OwnedItem item);
            switch (result)
            {
                case PackResult.Full: ui.Say(string.Format(db.gear.starterFull, Unlocks.StarterSlots(db, Profile))); return false;
                case PackResult.NoShards: ui.Say(db.hub.noShards); return false;
                case PackResult.Blocked: ui.Say(db.ui.invalidPlacement); return false;
                case PackResult.NotOffered: return false;
            }
            selected = null;
            Gear.Save(db, Profile);
            RenderInventory();
            ui.UpdateMeta(Profile);
            ui.Sew(item.id);
            sound.Play(string.IsNullOrEmpty(db.hub.buySound) ? "loot" : db.hub.buySound);
            ui.Say(string.Format(db.hub.packed ?? "{0}", Gear.Def(db, item).name));
            return true;
        }

        // 아웃게임 판 = 등반 전에 고른 시작 조각이라 저장한다. 등반 가방은 귀환하면 버리므로 저장하지 않는다.
        void SaveBag() { if (Run == null) Gear.Save(db, Profile); }

        // 17차: 아웃게임 판에는 들고 갈 수(Unlocks.StarterSlots)만큼만 놓는다.
        bool StarterFull(OwnedItem item)
        {
            if (Run != null || !Unlocks.StarterFull(db, Profile, item)) return false;
            ui.Say(string.Format(db.gear.starterFull, Unlocks.StarterSlots(db, Profile)));
            return true;
        }

        // 개발자 모드가 조각·배치를 바꾼 뒤: 저장하고, 등반 중이면 장비 능력치·세트를 바로 반영하고, 재봉 상자가 열려 있으면 다시 그린다.
        public void DevGearChanged()
        {
            SaveBag();
            if (Run != null) Combat.RefreshGear(Run);
            if (screen == UiScreen.Inventory || runInventory) { selected = null; RenderInventory(); }
            ui.UpdateMeta(Profile);
        }

        public void SelectPiece(int id)
        {
            if (picking != null) return;
            selected = id;
            turns = Bag.placements.FirstOrDefault(p => p.id == id)?.turns ?? 0;
            RenderInventory();
        }

        public void Rotate()
        {
            if (selected == null) return;
            turns = (turns + 1) % 4;
            RenderInventory();
        }

        public void Unequip()
        {
            if (Packing && selected is int id && ItemFor(id) is { } packed && !IsOffer(packed))   // 19차: 챙긴 조각을 빼면 파편을 돌려준다
            {
                string name = Gear.Def(db, packed).name;
                int refund = HubShop.Unpack(db, Profile, id);
                selected = null;
                Gear.Save(db, Profile);
                RenderInventory();
                ui.UpdateMeta(Profile);
                if (refund >= 0) ui.Say(string.Format(db.hub.unpacked ?? "{0}", name, refund));
                return;
            }
            Bag.placements.RemoveAll(p => p.id == selected);
            SaveBag();
            RenderInventory();
        }

        public void ClickCell(int x, int y)
        {
            if (picking != null) { PickAt(new Vector2Int(x, y)); return; }
            OwnedItem item = ItemFor(selected);
            if (IsOffer(item)) { if (!PackOffer(item, x, y)) RenderInventory(); return; }
            PlacedPiece occupying = Gear.Placed(db, Bag).FirstOrDefault(p => p.cells.Contains(new Vector2Int(x, y)));
            if (item == null)
            {
                selected = occupying?.Id ?? selected;
                turns = occupying?.placement.turns ?? turns;
                RenderInventory();
            }
            else if (StarterFull(item)) RenderInventory();
            else if (!Gear.CanPlace(db, Bag, item, turns, x, y)) ui.Say(db.ui.invalidPlacement);
            else
            {
                Bag.placements.RemoveAll(p => p.id == item.id);
                Bag.placements.Add(new Placement { id = item.id, x = x, y = y, turns = turns });
                selected = null;
                SaveBag();
                RenderInventory();
                ui.Sew(item.id);   // 장착: 바늘이 조각 둘레를 땀땀이 한 바퀴
                sound.Play("loot");
            }
        }

        // 꾹 눌러 끌기 시작: 그 조각을 고른 상태로 (원래 회전 그대로)
        public void BeginDrag(int id)
        {
            selected = id;
            turns = Bag.placements.FirstOrDefault(p => p.id == id)?.turns ?? 0;
            RenderInventory();
        }

        // 끌어서 놓기: 판 칸에 놓을 수 있으면 옮겨 장착(바느질), 못 놓으면 안내 후 원래대로. 판 밖이면 취소.
        public void DropDrag(int id, Vector2Int? cell)
        {
            OwnedItem item = ItemFor(id);
            Placement old = Bag.placements.FirstOrDefault(p => p.id == id);
            selected = null;
            if (IsOffer(item))
            {
                if (cell is Vector2Int spot) { selected = id; if (PackOffer(item, spot.x, spot.y)) return; selected = null; }
                RenderInventory();
                return;
            }
            if (item != null && cell is Vector2Int at && !(old != null && old.x == at.x && old.y == at.y && old.turns == turns))
            {
                if (StarterFull(item)) { RenderInventory(); return; }
                if (Gear.CanPlace(db, Bag, item, turns, at.x, at.y))
                {
                    Bag.placements.RemoveAll(p => p.id == id);
                    Bag.placements.Add(new Placement { id = id, x = at.x, y = at.y, turns = turns });
                    SaveBag();
                    RenderInventory();
                    ui.Sew(id);
                    sound.Play("loot");
                    return;
                }
                ui.Say(db.ui.invalidPlacement);
            }
            RenderInventory();
        }

        public void HoverCell(Vector2Int? at)
        {
            if (screen != UiScreen.Inventory && !runInventory) return;
            if (picking != null) { ui.PickHover(Bag, picking, at); return; }
            ui.PreviewPiece(Bag, ItemFor(selected), turns, at);
        }

        // ---------------------------------------------------------------- 예견의 다락: 모이라 (19차)
        // 모이라에게 말을 걸면 모이라(MoiraRig)가 카드 넷을 쥔 손을 들고, 카드가 손에서 날아와 펼쳐진다(GameUI.Moira.cs):
        //   인벤토리 확장, 인벤토리 칸 강화 = 누르면 재봉 상자 판이 열리고(picking) 판 위에서 반투명하게 고른 자리(가장자리, 가로/세로 줄)를 확인하면 강화.
        //   욕망 최대치 = 바로 확인 창. 운명 읽기 = 파편을 내고 다음 등반 1층 속성의 암시를 듣는다(이미 읽었으면 다시 듣기).
        // 상인 단추의 '들고 갈 수 늘리기'(starter)도 같은 확인 창(ConfirmUpgrade)을 쓴다.

        public const string FateKey = "fate";
        string picking;          // 판에서 고르는 중인 강화 key (null = 아님)
        int pendingPick = -1;    // 확인 창에 올린 자리
        string moiraLine;        // 모이라의 지금 한 줄 (말을 걸 때, 운명 읽기 뒤)
        bool dealCards;          // 다음 RenderAttic 에서 카드를 손에서 새로 날린다

        int Level(UpgradeDef u) => Moira.Level(Profile, u.key);
        int Cost(UpgradeDef u) => Moira.Cost(Profile, u);
        bool Maxed(UpgradeDef u) => Moira.Maxed(db, Profile, u);
        UpgradeDef Upgrade(string key) => db.upgrades.FirstOrDefault(x => x.key == key);

        void InitMoira()
        {
            ui.DialogClosed += id =>   // 확인 창이 닫히면(확인, 취소, Esc) 카드 내용만 갱신
            {
                if (id == "upgrade" && screen == UiScreen.Attic && picking == null) RenderAttic();
            };
        }

        void RenderAttic()
        {
            HubConfig h = db.hub;
            var cards = new List<MoiraCardInfo>();
            foreach (UpgradeDef u in Moira.Upgrades(db))
            {
                if (u.key is Moira.Rows or Moira.Cols) continue;   // 칸 강화(lines)로 합쳤다
                bool max = Maxed(u);
                string key = u.key;
                cards.Add(new MoiraCardInfo
                {
                    key = key, icon = u.icon, name = u.name, text = u.text,
                    foot = max ? db.ui.upgradeMax : string.Format(h.cardPrice ?? "{0}", Cost(u)),   // 아래 = 드는 파편만
                    level = string.Format(h.cardLevel ?? "{0}", Level(u)),                          // 위 = 지금 단계
                    enabled = !max && Profile.shards >= Cost(u), opensBoard = Moira.NeedsPick(key),
                    pick = () => PickUpgrade(key),
                    locked = () => ui.Say(max ? db.ui.upgradeMax : h.noShards),
                });
            }
            bool read = Moira.FateRead(Profile);
            cards.Add(new MoiraCardInfo
            {
                key = FateKey, icon = h.fateIcon, name = h.fateName, text = h.fateText,
                foot = read ? h.fateReadFoot : string.Format(h.cardPrice ?? h.fateFoot ?? "{0}", h.fatePrice),
                enabled = read || Profile.shards >= h.fatePrice,
                pick = ReadFateCard, locked = () => ui.Say(h.noShards),
            });
            bool deal = dealCards;
            dealCards = false;
            ui.ShowMoiraCards(cards, deal ? moiraLine : null, Profile.shards, MoiraHand, deal);
        }

        // 카드가 날아 나가는 자리 = 모이라가 든 카드 부채 (패널 px)
        Vector2 MoiraHand() => hub != null && hub.moira != null ? ui.WorldToPanel(hub.moira.CardPoint) : new Vector2(960, 300);

        // 모이라에게 말을 걸 때: 한 줄을 새로 고르고 카드를 나눈다
        void TalkToMoira()
        {
            string[] lines = db.hub.moiraLines;
            moiraLine = lines != null && lines.Length > 0 ? lines[Random.Range(0, lines.Length)] : null;
            if (Moira.FateRead(Profile)) moiraLine = FateSpeech() ?? moiraLine;   // 읽어 둔 운명이 있으면 그 암시를 다시
            dealCards = true;
            ShowScreen(UiScreen.Attic);
        }

        // 돌아가기, Esc: 카드가 모두 모이라 손으로 돌아간 뒤 방으로
        void LeaveMoira()
        {
            if (hub != null && hub.moira != null) hub.moira.Talking = false;
            ui.LeaveMoira(() => { if (screen == UiScreen.Attic) ShowScreen(UiScreen.Lobby); });
        }

        // 모이라 창의 카드
        void PickUpgrade(string key)
        {
            UpgradeDef u = Upgrade(key);
            if (u == null || Maxed(u)) return;
            if (Profile.shards < Cost(u)) { ui.Say(db.hub.noShards); return; }
            if (Moira.NeedsPick(key)) { BeginPick(key); return; }
            AskUpgrade(u, -1, string.Format(db.hub.confirmPlain ?? db.ui.upgradeBody, Cost(u), u.name));
        }

        string FateSpeech()
        {
            ElementDef def = db.elements?.Def(Moira.FateElement(Profile));
            if (def == null || string.IsNullOrEmpty(def.fateLine)) return null;
            return string.IsNullOrEmpty(db.hub.fateIntro) ? def.fateLine : db.hub.fateIntro + " " + def.fateLine;
        }

        void ReadFateCard()
        {
            if (Moira.FateRead(Profile))   // 다시 듣기 (파편 없음)
            {
                ui.SayMoira(moiraLine = FateSpeech());
                RenderAttic();
                return;
            }
            if (Profile.shards < db.hub.fatePrice) { ui.Say(db.hub.noShards); RenderAttic(); return; }
            pendingUpgrade = FateKey;
            pendingPick = -1;
            ui.ShowUpgradeConfirm(db.hub.moiraEyebrowConfirm, db.hub.moiraTitleConfirm, string.Format(db.hub.fateConfirm ?? "{0}", db.hub.fatePrice), db.hub.moiraConfirmButton);
        }

        void AskUpgrade(UpgradeDef u, int pick, string body)
        {
            pendingUpgrade = u.key;
            pendingPick = pick;
            bool merchant = u.key == Unlocks.StarterKey;
            ui.ShowUpgradeConfirm(merchant ? db.hub.slotEyebrow : db.hub.moiraEyebrowConfirm, merchant ? db.hub.slotTitle : db.hub.moiraTitleConfirm,
                body, merchant ? db.hub.slotConfirm : db.hub.moiraConfirmButton);
        }

        void BeginPick(string key)
        {
            picking = key;
            selected = null;
            ShowScreen(UiScreen.Inventory);
            sound.Play("loot");
        }

        void EndPick()
        {
            picking = null;
            pendingPick = -1;
            dealCards = true;
            ui.ClearMoiraCards();
            ShowScreen(UiScreen.Attic);
        }

        // 판 위 고르기: 칸 강화와 확장은 판 가장자리 손잡이(PickHandle, PickSide)로 고른다. (옛 rows/cols 는 칸을 눌러 줄)
        void PickAt(Vector2Int cell)
        {
            if (picking == Moira.Rows) PickLine(cell.y);
            else if (picking == Moira.Cols) PickLine(cell.x);
        }

        public void PickSide(int side) { if (picking == Moira.Expansion) PickLine(side); }

        // 칸 강화 손잡이: 가로 줄 = 줄 번호, 세로 줄 = Moira.ColBase + 열 번호
        public void PickHandle(int pick) { if (picking == Moira.Lines) PickLine(pick); }

        void PickLine(int pick)
        {
            UpgradeDef u = Upgrade(picking);
            if (u == null) return;
            if (Maxed(u)) { ui.Say(db.ui.upgradeMax); return; }
            if (!Moira.CanPick(db, Profile, picking, pick)) { ui.Say(db.hub.lineTaken); return; }
            if (Profile.shards < Cost(u)) { ui.Say(db.hub.noShards); return; }
            HubConfig h = db.hub;
            string body = picking switch
            {
                Moira.Lines => string.Format(h.confirmLine, pick >= Moira.ColBase ? string.Format(h.colName, pick - Moira.ColBase + 1) : string.Format(h.rowName, pick + 1), Cost(u), u.name),
                Moira.Rows => string.Format(h.confirmRow, pick + 1, Cost(u), u.name),
                Moira.Cols => string.Format(h.confirmCol, pick + 1, Cost(u), u.name),
                _ => string.Format(h.confirmExpand, h.sideNames != null && pick < h.sideNames.Length ? h.sideNames[pick] : pick.ToString(), Cost(u), u.name),
            };
            AskUpgrade(u, pick, body);
        }

        public void ConfirmUpgrade()
        {
            if (pendingUpgrade == FateKey)
            {
                ElementDef read = Moira.ReadFate(db, Profile, () => Random.value);
                if (read == null) return;
                Gear.Save(db, Profile);
                moiraLine = FateSpeech();
                ui.CloseDialog();   // → RenderAttic (카드 갱신)
                ui.UpdateMeta(Profile);
                sound.Play("upgrade");
                ui.RoomFlash();
                ui.SayMoira(moiraLine);
                return;
            }
            UpgradeDef u = Upgrade(pendingUpgrade);
            if (u == null || !Moira.Apply(db, Profile, u, pendingPick)) return;
            Gear.Save(db, Profile);
            pendingPick = -1;
            ui.CloseDialog();   // 모이라 창이면 DialogClosed → RenderAttic
            ui.UpdateMeta(Profile);
            sound.Play("upgrade");
            ui.RoomFlash();
            ui.Say(string.Format(db.ui.upgradeDone, u.name, Level(u)));
            if (u.key == Unlocks.StarterKey) { ShowHubShop(); return; }   // 단추 메뉴로
            if (picking != null) { selected = null; RenderInventory(); }   // 판에 남아 바뀐 줄을 보여 준다 (이어서 더 고를 수 있음)
        }

        // ---------------------------------------------------------------- frame

        void SyncDialog()
        {
            if (Run == null) return;
            if (Run.outcome == Outcome.Dead && !animatingDeath)
            {
                animatingDeath = true;
                deathClock = Clock;
                ui.CloseDialog();
            }
            if (animatingDeath)
            {
                if (Clock - deathClock >= deathDuration) Finish(Outcome.Dead);
                return;
            }
            if (Run.outcome != Outcome.None) { Finish(Run.outcome); return; }
            if (Run.phase == Phase.Victory) Finish(Outcome.Clear);
        }

        void ReadKeys()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame) Escape();
            if (k.enterKey.wasPressedThisFrame && screen == UiScreen.Lobby && Run == null && Opened == "") StartRun();
            if (k.rKey.wasPressedThisFrame && (screen == UiScreen.Inventory || runInventory)) Rotate();
            if (IsCombat && k.spaceKey.wasPressedThisFrame) dashRequested = true;
            if (k.eKey.wasPressedThisFrame && !runInventory) Interact();
            if (k.iKey.wasPressedThisFrame) ToggleRunInventory();
        }

        // 17차 Esc: 맨 위에 열린 창부터 닫는다. 증강·아이템 창은 닫아도 나중에 E 로 다시 연다.
        // 일기 게시판은 DiaryBoard 가 스스로 Esc 를 받는다(쪽지 → 게시판 순서). 고르기 전에는 못 닫는 욕망 제안만 남긴다.
        void Escape()
        {
            if (runInventory) { CloseRunInventory(); return; }
            switch (Opened)
            {
                case "diary": case "desire": return;
                case "retreat": CancelRetreat(); return;
                case "pause": TogglePause(); return;
                case "": break;
                default: ui.CloseDialog(); return;
            }
            if (Run == null)
            {
                if (screen is UiScreen.Inventory or UiScreen.Attic) BackFromRoom();   // 챙기기 판, 모이라의 줄 고르기, 모이라 창
                return;
            }
            TogglePause();
        }

        CombatInput ReadCombatInput()
        {
            var input = new CombatInput();
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            if (k != null)
            {
                input.x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
                input.y = (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0) - (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0);
            }
            if (m != null)
            {
                Vector2 screenPos = m.position.ReadValue();
                aim = Space.WorldToArena(worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -worldCamera.transform.position.z)));
                bool overUI = ui.PointerOverUI(screenPos);
                if (m.leftButton.wasPressedThisFrame && !overUI) { fireRequested = true; holdingFire = true; }
                if (!m.leftButton.isPressed) holdingFire = false;
                if (m.rightButton.wasPressedThisFrame && !overUI) pullRequested = true;
            }
            if (autopilot != null && autopilot.isActiveAndEnabled) autopilot.Drive(Run, ref input, ref aim, ref fireRequested, ref pullRequested, ref dashRequested);
            input.shoot = holdingFire || fireRequested;
            input.pull = pullRequested;
            input.dash = dashRequested;
            input.aim = aim;
            return input;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .032f);
            ui.Tick();
            ReadKeys();
            TickHub(dt);   // 15차: 허브 걷기 (로비일 때만)
            TickMerchant();   // 16차: 상인 단추 자세와 말풍선 (GameController.Merchant.cs)
            ui.BeginLabels();
            if (IsCombat)
            {
                bool canAct = Run.freeze <= 0;
                Combat.Update(Run, dt, ReadCombatInput());
                if (canAct) { fireRequested = false; pullRequested = false; dashRequested = false; }
                if (Combat.DesireFraction(Run) >= db.desire.highFraction && Clock - lastWhisper > whisperInterval) { sound.Play("whisper"); lastWhisper = Clock; }
            }
            else { fireRequested = false; pullRequested = false; dashRequested = false; holdingFire = false; }
            if (Run != null)
            {
                foreach (GameEvent e in Run.events)
                {
                    sound.Play(e.type, e.type == "pull" ? 1 + Run.combo * .12f : 1);
                    if (e.type is "loot" or "empty") ui.Say(e.message);
                    else if (!string.IsNullOrEmpty(e.message)) ui.Banner(e.message, e.type == "desire" ? 3.2f : 1.7f);
                }
                Run.events.Clear();
                float heroTime = Run.outcome == Outcome.Dead ? Mathf.Min(deathDuration, Clock - (animatingDeath ? deathClock : Clock)) : Run.player.actionTime;
                arena.Render(Run, aim, Clock, IsCombat, heroTime, Combat.Nearest(Run));
                ApplyPostFx();
                ui.UpdateHud(Run, Combat);
                ui.UpdateMeta(Profile);
                SyncDialog();
                if (Opened == "shop" && Run != null)
                {
                    if (Clock >= shop.end && !shop.expired) { shop.expired = true; ShowShop(); }
                    ui.SetSaleLabel(SaleLabel());
                }
            }
            ui.EndLabels();
        }

        void ApplyPostFx()
        {
            RunState r = Run;
            float fraction = Combat.DesireFraction(r);
            Shader.SetGlobalFloat(EnabledId, 1);
            Shader.SetGlobalFloat(MotionId, 1);
            Shader.SetGlobalFloat(DesireId, Mathf.Max(0, fraction - .4f) / .6f);
            Color tint = r.flashTint;
            Shader.SetGlobalVector(FlashId, new Vector4(tint.r, tint.g, tint.b, Mathf.Min(.07f, r.flash * .5f)));
            Shader.SetGlobalFloat(SnapId, r.snapFlash > 0 ? .12f * Mathf.Pow(r.snapFlash / db.feel.cutFlash, 1.6f) : 0);
            Shader.SetGlobalFloat(HurtId, r.hurtFlash > 0 ? Mathf.Pow(r.hurtFlash / db.hero.hurtFlash, 1.5f) : 0);
            // 정화된 화면은 비네트·욕망 테두리가 약해진다 (고리가 화면을 덮은 정도만큼).
            float purified = arena.PurifiedCoverage(r);
            float border = fraction >= db.desire.highFraction ? Mathf.Clamp((fraction - .3f) * .81f, 0, .65f) + Mathf.Sin(Clock * 3) * .055f : 0;
            Shader.SetGlobalFloat(BorderId, border * (1 - purified));
            Shader.SetGlobalFloat(PurifiedId, purified * db.purify.vignetteCut);
            // 욕망이 warpFrom(80%) 이상이면 화면이 붉게 일렁인다.
            DesireConfig d = db.desire;
            float warp = d.warpFrom < 1 ? Mathf.Clamp01((fraction - d.warpFrom) / (1 - d.warpFrom)) : 0;
            if (fraction >= d.warpFrom) warp = Mathf.Max(warp, .15f);
            Shader.SetGlobalFloat(WarpId, warp * (1 - purified));
            Shader.SetGlobalColor(WarpTintId, d.warpTint);
            Shader.SetGlobalVector(WarpParamsId, new Vector4(d.warpAmplitude, d.warpFrequency, d.warpSpeed, 0));
            Shader.SetGlobalFloat(CanvasHeightId, db.arena.canvasHeight * arena.Zoom);
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) return;
            holdingFire = false; fireRequested = false; pullRequested = false; dashRequested = false;
            if (IsCombat && (autopilot == null || !autopilot.isActiveAndEnabled)) TogglePause();
        }
    }
}
