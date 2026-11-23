// 입력·화면·오디오·등반 진행 연결 (웹판 main.js).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TOD
{
    public class GameController : MonoBehaviour
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
            BorderId = Shader.PropertyToID("_TOD_Border"), CanvasHeightId = Shader.PropertyToID("_TOD_CanvasHeight");

        float Clock => Time.unscaledTime;
        string Opened => ui.Opened;
        Vector2 Center => new(db.arena.width / 2, db.arena.height / 2);

        void Start()
        {
            Space.Configure(db.arena);
            Combat = new Combat(db);
            Profile = Gear.Load(db);
            arena.Text = ui;
            arena.Init();
            sound.Init(db.sounds);
            sound.Enabled = true;
            ui.Init(this);
            aim = Center;
            Shader.SetGlobalFloat(CanvasHeightId, db.arena.canvasHeight);
            ShowScreen(UiScreen.Lobby);
        }

        // ---------------------------------------------------------------- screens

        public void ShowScreen(UiScreen next)
        {
            if (Run != null && next != UiScreen.Combat) return;
            if (next is UiScreen.Inventory or UiScreen.Attic) roomReturn = screen is UiScreen.Summary ? UiScreen.Summary : UiScreen.Lobby;
            screen = next;
            if (next == UiScreen.Lobby) summary = null;
            ui.ShowScreen(next);
            arenaRoot.SetActive(next == UiScreen.Combat);
            if (next == UiScreen.Inventory) RenderInventory();
            if (next == UiScreen.Attic) RenderAttic();
            if (next == UiScreen.Summary && summary is { } s) ui.RenderSummary(s.outcome, s.floor, s.kills, Profile.shards);
            ui.UpdateMeta(Profile);
        }

        public void BackFromRoom() => ShowScreen(summary != null ? roomReturn : UiScreen.Lobby);

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
            shop = null;
            aim = Center;
            arena.Clear();
            Combat.BeginFloor(Run);
            ShowScreen(UiScreen.Combat);
        }

        public void Finish(Outcome outcome)
        {
            if (Run == null) return;
            summary = (outcome, Run.floor, Run.kills);
            Gear.Save(db, Profile);
            Run = null;
            paused = false;
            animatingDeath = false;
            ui.CloseDialog();
            arena.Clear();
            Shader.SetGlobalFloat(EnabledId, 0);
            ShowScreen(UiScreen.Summary);
        }

        bool IsCombat => Run != null && Run.phase == Phase.Combat && !paused && Opened == "" && Run.outcome == Outcome.None && Run.augmentQueue == 0;

        public void TogglePause()
        {
            if (Run == null || Run.phase != Phase.Combat || Run.outcome != Outcome.None || (Opened != "" && Opened != "pause")) return;
            paused = !paused;
            if (paused) ui.ShowDialog("pause"); else ui.CloseDialog();
        }

        public void AskRetreat() => ui.ShowDialog("retreat");

        public void CancelRetreat()
        {
            ui.CloseDialog();
            if (paused) { paused = false; TogglePause(); }
            else if (Run?.phase == Phase.Shop) ShowShop();
        }

        List<AugmentDef> offered;

        void ShowAugments()
        {
            offered = Combat.AugmentChoices(Run);
            ui.ShowAugments(offered, Run.level - Run.augmentQueue + 1, PickAugment);
        }

        void PickAugment(string id)
        {
            Combat.ApplyAugment(Run, id);
            AddPlayerFx(FxSprite.Stitch, db.fx.augment);
            ui.CloseDialog();
            sound.Play("upgrade");
        }

        // 자동 진행(검증용 AutoPilot): 열린 대화 상자의 기본 선택을 고른다.
        public void AutoChoose()
        {
            if (Run == null) return;
            switch (Opened)
            {
                case "augment": PickAugment(offered[0].id); break;
                case "desire": Absorb(); break;
                case "shop": NextFloor(); break;
                case "rest": NextRound(); break;
                case "pause": TogglePause(); break;
            }
        }

        void AddPlayerFx(FxSprite sprite, FxSpec spec)
        {
            Run.effects.Add(new Fx { kind = FxKind.Sprite, sprite = sprite, x = Run.player.x, y = Run.player.y, size = spec.size, time = spec.time, max = spec.time, lift = spec.lift, alpha = spec.alpha });
        }

        public void Absorb()
        {
            Combat.ChooseDesire(Run, Run.offer.id);
            AddPlayerFx(FxSprite.Boss, db.fx.absorb);
            sound.Play("desire");
            ui.CloseDialog();
        }

        public void Refuse() { Combat.ChooseDesire(Run, null); ui.CloseDialog(); }

        public void NextRound() { ui.CloseDialog(); Run.round++; Combat.BeginRound(Run); }

        public void NextFloor()
        {
            ui.CloseDialog();
            shop = null;
            Run.floor++;
            Combat.BeginFloor(Run);
        }

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
            ui.ShowShop(db.shop, Price, w => shop.sold.Contains(w.id), Run.gold, SaleLabel(), Run.floor + 1, Buy);
        }

        void Buy(string id)
        {
            WareDef ware = db.shop.wares.First(w => w.id == id);
            if (shop.sold.Contains(id) || Run.gold < Price(ware)) return;
            Run.gold -= Price(ware);
            shop.sold.Add(id);
            float potion = Run.desires.Contains("hunger") ? db.desire.hungerPotion : 1;
            switch (id)
            {
                case "heal": Run.hp = Mathf.Min(Run.maxHp, Run.hp + ware.amount * potion); break;
                case "pure": Combat.AdjustDesire(Run, -ware.amount * potion); break;
                case "piece": ui.Say(string.Format(db.messages.loot, Gear.AddItem(db, Profile).name)); break;
                case "buff": Run.damage += ware.amount; break;
            }
            Gear.Save(db, Profile);
            sound.Play("loot");
            ShowShop();
        }

        // ---------------------------------------------------------------- 재봉 상자

        void RenderInventory() => ui.RenderInventory(Profile, selected, turns);

        public void SelectPiece(int id)
        {
            selected = id;
            turns = Profile.placements.FirstOrDefault(p => p.id == id)?.turns ?? 0;
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
            Profile.placements.RemoveAll(p => p.id == selected);
            Gear.Save(db, Profile);
            RenderInventory();
        }

        public void ClickCell(int x, int y)
        {
            OwnedItem item = Profile.items.FirstOrDefault(i => i.id == selected);
            PlacedPiece occupying = Gear.Placed(db, Profile).FirstOrDefault(p => p.cells.Contains(new Vector2Int(x, y)));
            if (item == null)
            {
                selected = occupying?.Id ?? selected;
                turns = occupying?.placement.turns ?? turns;
                RenderInventory();
            }
            else if (!Gear.CanPlace(db, Profile, item, turns, x, y)) ui.Say(db.ui.invalidPlacement);
            else
            {
                Profile.placements.RemoveAll(p => p.id == item.id);
                Profile.placements.Add(new Placement { id = item.id, x = x, y = y, turns = turns });
                selected = null;
                Gear.Save(db, Profile);
                RenderInventory();
                sound.Play("loot");
            }
        }

        public void HoverCell(Vector2Int? at)
        {
            if (screen != UiScreen.Inventory) return;
            ui.PreviewPiece(Profile, Profile.items.FirstOrDefault(i => i.id == selected), turns, at);
        }

        // ---------------------------------------------------------------- 예견의 다락

        int Level(UpgradeDef u) => u.key switch { "expansion" => Profile.expansion, "rows" => Profile.rows, _ => Profile.capacity };
        int Cost(UpgradeDef u) => u.price + u.priceStep * Level(u);
        bool Maxed(UpgradeDef u) => Level(u) >= (u.max >= 0 ? u.max : Gear.Dimensions(db, Profile).height / 2);

        void RenderAttic() => ui.RenderAttic(Profile, Cost, Maxed, Level, key =>
        {
            pendingUpgrade = key;
            UpgradeDef u = db.upgrades.First(x => x.key == key);
            ui.ShowUpgradeConfirm(u, Cost(u));
        });

        public void ConfirmUpgrade()
        {
            UpgradeDef u = db.upgrades.FirstOrDefault(x => x.key == pendingUpgrade);
            if (u == null || Maxed(u) || Profile.shards < Cost(u)) return;
            Profile.shards -= Cost(u);
            switch (u.key)
            {
                case "expansion": Profile.expansion++; break;
                case "rows": Profile.rows++; break;
                default: Profile.capacity++; break;
            }
            Gear.Save(db, Profile);
            ui.CloseDialog();
            RenderAttic();
            ui.UpdateMeta(Profile);
            sound.Play("upgrade");
            ui.RoomFlash();
            ui.Say(string.Format(db.ui.upgradeDone, u.name, Level(u)));
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
            if (Opened != "" || paused) return;
            if (Run.augmentQueue > 0) ShowAugments();
            else if (Run.phase == Phase.Desire) ui.ShowDesire(Run.offer, Run.floor, Run.offer.id == db.desire.refine.id);
            else if (Run.phase == Phase.Shop) ShowShop();
            else if (Run.phase == Phase.Rest) ui.ShowRest(Run.floor, Run.round, Run.round == Combat.BossRound - 1);
            else if (Run.phase == Phase.Victory) Finish(Outcome.Clear);
        }

        void ReadKeys()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame)
            {
                if (Opened == "upgrade") ui.CloseDialog();
                else if (Opened == "retreat") CancelRetreat();
                else TogglePause();
            }
            if (k.enterKey.wasPressedThisFrame && screen == UiScreen.Lobby && Run == null && Opened == "") StartRun();
            if (k.rKey.wasPressedThisFrame && screen == UiScreen.Inventory) Rotate();
            if (IsCombat && k.spaceKey.wasPressedThisFrame) dashRequested = true;
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
                arena.Render(Run, aim, Clock, IsCombat, heroTime);
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
            Shader.SetGlobalFloat(BorderId, fraction >= db.desire.highFraction ? Mathf.Clamp((fraction - .3f) * .81f, 0, .65f) + Mathf.Sin(Clock * 3) * .055f : 0);
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) return;
            holdingFire = false; fireRequested = false; pullRequested = false; dashRequested = false;
            if (IsCombat && (autopilot == null || !autopilot.isActiveAndEnabled)) TogglePause();
        }
    }
}
