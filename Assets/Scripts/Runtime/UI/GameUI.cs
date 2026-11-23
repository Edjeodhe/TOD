// UI Toolkit 화면 (웹판 main.js 의 DOM 갱신 부분). 화면 구조는 Assets/UI/*.uxml, 목록 항목은 UXML 템플릿을 복제한다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public enum UiScreen { Lobby, Inventory, Attic, Summary, Combat }

    public class GameUI : MonoBehaviour, IWorldText
    {
        public UIDocument document;
        public VisualTreeAsset choiceCard, pieceButton, slots;
        public Camera worldCamera;
        public Texture2D stitchVfx, bossVfx;

        GameController game;
        GameDatabase db;
        VisualElement root, hud, lobby, inventory, attic, summary, overlay, worldText, notice, banner, roomFlash, timer, hint;
        Label noticeText, bannerText, timerText, hintText;
        float noticeUntil, bannerUntil, flashUntil;
        readonly List<Label> labels = new();
        int labelsUsed;
        readonly List<VisualElement> pips = new();
        readonly List<VisualElement> dashPips = new();
        readonly List<(VisualElement card, VisualElement shine, float strength, float phase, Color color)> shines = new();
        VisualElement[] rounds;
        readonly Dictionary<Vector2Int, Button> cells = new();
        UiTextConfig T => db.ui;

        public static float Now => Time.unscaledTime;

        // ---------------------------------------------------------------- setup

        public void Init(GameController controller)
        {
            game = controller;
            db = controller.db;
            root = document.rootVisualElement;
            hud = root.Q("hud"); lobby = root.Q("lobby"); inventory = root.Q("inventory"); attic = root.Q("attic");
            summary = root.Q("summary"); overlay = root.Q("overlay"); worldText = root.Q("world-text");
            notice = root.Q("notice"); noticeText = root.Q<Label>("notice-text");
            banner = root.Q("banner"); bannerText = root.Q<Label>("banner-text");
            roomFlash = root.Q("room-flash");
            rounds = root.Q("rounds").Children().ToArray();
            timer = root.Q("timer"); timerText = root.Q<Label>("timer-text");
            hint = root.Q("hint"); hintText = root.Q<Label>("hint-text");

            Bind("start", game.StartRun);
            Bind("open-inventory", () => game.ShowScreen(UiScreen.Inventory));
            Bind("open-attic", () => game.ShowScreen(UiScreen.Attic));
            Bind("sound", game.ToggleSound);
            Bind("pause", game.TogglePause);
            Bind("resume", game.TogglePause);
            Bind("retreat", game.AskRetreat);
            Bind("shop-retreat", game.AskRetreat);
            Bind("confirm-retreat", () => game.Finish(Outcome.Retreat));
            Bind("cancel-retreat", game.CancelRetreat);
            Bind("absorb", game.Absorb);
            Bind("refuse", game.Refuse);
            Bind("close-shop", CloseDialog);
            Bind("close-artifact", CloseDialog);
            Bind("reroll-augment", game.RerollAugments);
            Bind("reroll-artifact", game.RerollArtifacts);
            Bind("confirm-upgrade", game.ConfirmUpgrade);
            Bind("cancel-upgrade", CloseDialog);
            Bind("restart", game.StartRun);
            Bind("summary-inventory", () => game.ShowScreen(UiScreen.Inventory));
            Bind("summary-attic", () => game.ShowScreen(UiScreen.Attic));
            Bind("summary-lobby", () => game.ShowScreen(UiScreen.Lobby));
            Bind("inventory-back", game.BackFromRoom);
            Bind("attic-back", game.BackFromRoom);
            Bind("rotate", game.Rotate);
            Bind("unequip", game.Unequip);
            root.Q("board").RegisterCallback<PointerLeaveEvent>(_ => game.HoverCell(null));
        }

        void Bind(string name, Action action) => root.Q<Button>(name).clicked += action;

        // UXML 템플릿에서 이름이 같은 요소를 하나 복제한다 (템플릿 슬롯 복제).
        VisualElement Slot(VisualTreeAsset asset, string name)
        {
            TemplateContainer tree = asset.Instantiate();
            VisualElement element = tree.Q(name);
            element.RemoveFromHierarchy();
            return element;
        }

        static void Show(VisualElement e, bool visible) => e.EnableInClassList("hidden", !visible);
        static void Text(VisualElement scope, string name, string value) { Label label = scope.Q<Label>(name); if (label != null && label.text != value) label.text = value; }
        static void Meter(VisualElement scope, string name, float value) => scope.Q(name).style.width = Length.Percent(Mathf.Clamp01(value) * 100);
        static void Icon(VisualElement e, Sprite sprite) => e.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;

        public void ShowScreen(UiScreen screen)
        {
            Show(lobby, screen == UiScreen.Lobby);
            Show(inventory, screen == UiScreen.Inventory);
            Show(attic, screen == UiScreen.Attic);
            Show(summary, screen == UiScreen.Summary);
            Show(hud, screen == UiScreen.Combat);
        }

        // ---------------------------------------------------------------- notices

        public void Say(string message)
        {
            noticeText.text = message;
            Show(notice, true);
            noticeUntil = Now + 2.6f;
        }

        public void Banner(string message, float duration)
        {
            bannerText.text = message;
            Show(banner, true);
            bannerUntil = Now + duration;
        }

        public void RoomFlash()
        {
            Show(roomFlash, true);
            flashUntil = Now + .9f;
        }

        public void Tick()
        {
            // 유니크·레전더리 카드: 빛 줄기가 카드를 가로질러 지나가고, 레전더리는 테두리가 숨 쉬듯 밝아진다.
            foreach (var (card, shine, strength, phase, color) in shines)
            {
                if (card.panel == null) continue;
                float u = Mathf.Repeat(Now * (.45f + strength * .35f) + phase, 1.6f) / 1.6f;
                shine.style.left = Length.Percent(-40 + u * 180);
                shine.style.opacity = strength;
                if (strength < .9f) continue;
                // 레전더리: 빛 줄기가 지날 때마다 테두리·돌기가 금빛으로 번쩍인다.
                float flash = Mathf.Pow(Mathf.Max(0, Mathf.Sin(u * Mathf.PI)), 6);
                Color edge = Color.Lerp(color, new Color(1f, .9f, .62f), flash * .8f);
                card.style.unityBackgroundImageTintColor = PaperTint(edge);
                SetBorder(card.Q("frame"), edge);
                foreach (string knob in new[] { "knob-top", "knob-right" }) card.Q(knob).style.backgroundColor = edge;
            }
            if (Now >= noticeUntil) Show(notice, false);
            if (Now >= bannerUntil) Show(banner, false);
            if (Now >= flashUntil) Show(roomFlash, false);
            else
            {
                // 다락 강화: 바느질 파동이 번쩍 커지며 사라진다.
                float t = 1 - (flashUntil - Now) / .9f;
                VisualElement fx = roomFlash[0];
                float s = .38f + t * .82f;
                fx.style.scale = new Scale(new Vector2(s, s));
                fx.style.opacity = t < .32f ? t / .32f : 1 - (t - .32f) / .68f;
            }
        }

        public bool PointerOverUI(Vector2 screen)
        {
            IPanel panel = root.panel;
            if (panel == null) return false;
            Vector2 p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
            VisualElement picked = panel.Pick(p);
            return picked != null && picked != root;
        }

        // ---------------------------------------------------------------- world labels

        public void BeginLabels() => labelsUsed = 0;

        public void EndLabels()
        {
            for (int i = labelsUsed; i < labels.Count; i++) labels[i].style.display = DisplayStyle.None;
        }

        public void Label(string text, Vector2 canvas, float fontPx, Color fill, Color stroke, float strokePx, float scale, float rotationDeg, bool italic)
        {
            if (root.panel == null || Opened != "") return;   // 대화 상자가 열려 있으면 전투 글자(안내 문구)는 숨긴다
            if (labelsUsed == labels.Count)
            {
                var created = (Label)Slot(slots, "world-label");
                worldText.Add(created);
                labels.Add(created);
            }
            Label label = labels[labelsUsed++];
            label.style.display = DisplayStyle.Flex;
            label.text = text;
            Vector3 screen = worldCamera.WorldToScreenPoint(Space.Canvas(canvas));
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
            float perPx = root.layout.height / (worldCamera.orthographicSize * 2 * Space.Ppu);
            label.style.left = p.x;
            label.style.top = p.y;
            label.style.fontSize = fontPx * perPx;
            label.style.color = fill;
            label.style.unityTextOutlineColor = stroke;
            label.style.unityTextOutlineWidth = strokePx * perPx * .5f;
            label.style.scale = new Scale(new Vector2(scale, scale));
            label.style.rotate = new Rotate(rotationDeg);
            label.style.unityFontStyleAndWeight = italic ? FontStyle.BoldAndItalic : FontStyle.Bold;
        }

        // ---------------------------------------------------------------- HUD (웹판 updateHUD)

        public void UpdateMeta(Profile profile)
        {
            Text(root, "meta-shards", profile.shards.ToString());
            Text(root, "best-record", $"◇ 최고 기록  {(profile.best > 0 ? string.Format(T.bestRecord, profile.best) : T.bestNone)}");
        }

        public void UpdateHud(RunState r, Combat combat)
        {
            int maxYarn = combat.YarnMax(r);
            int yarn = Mathf.FloorToInt(r.yarn);
            Text(hud, "level", string.Format(T.level, r.level));
            Text(hud, "hp-value", string.Format(T.ratio, r.hp, r.maxHp));
            Text(hud, "yarn-value", string.Format(T.ratio, yarn, maxYarn));
            float bonus = combat.StrandBonus(r.strands.Count);
            Text(hud, "thread-count", bonus > 0 ? string.Format(T.threadBonus, r.strands.Count, Mathf.RoundToInt(bonus * 100)) : string.Format(T.threadCount, r.strands.Count));
            Text(hud, "knot-ready", string.Format(T.knotReady, Mathf.Max(1, combat.KnotInterval(r) - r.shotCount)));
            string mood = r.mood == Mood.Frenzy ? T.frenzySuffix : r.mood == Mood.Awakened ? T.awakenSuffix : "";
            Text(hud, "desire-value", string.Format(T.desireValue, Mathf.FloorToInt(r.desire), r.maxDesire, mood));
            Text(hud, "gold", r.gold.ToString());
            Text(hud, "knots", r.knots.Count.ToString());
            Meter(hud, "hp-bar", r.hp / r.maxHp);
            Meter(hud, "desire-bar", r.desire / r.maxDesire);

            // 하단 경험치 바 (메이플식: 레벨 · EXP 현재/필요 [퍼센트])
            int need = Mathf.Max(1, combat.XpNeeded(r));
            float expRatio = Mathf.Clamp01(r.xp / (float)need);
            Text(hud, "exp-level", string.Format(T.expLevel, r.level));
            Text(hud, "exp-text", string.Format(T.expText, r.xp, need, expRatio * 100));
            hud.Q("exp-fill").style.width = Length.Percent(expRatio * 100);

            // 대쉬 칸: 찬 칸은 밝게, 차오르는 칸은 게이지로
            Player player = r.player;
            int dashMax = Mathf.Max(1, db.feel.dashCharges);
            Text(hud, "dash-value", string.Format(T.dashValue, player.dashCharges, dashMax));
            VisualElement dashRow = hud.Q("dash-pips");
            while (dashPips.Count < dashMax) { VisualElement pip = Slot(slots, "dash-pip"); dashRow.Add(pip); dashPips.Add(pip); }
            for (int i = 0; i < dashPips.Count; i++)
            {
                VisualElement pip = dashPips[i];
                pip.style.display = i < dashMax ? DisplayStyle.Flex : DisplayStyle.None;
                pip.EnableInClassList("full", i < player.dashCharges);
                pip.Q("fill").style.width = Length.Percent(i == player.dashCharges ? player.dashCharge * 100 : 0);
            }

            VisualElement pipRow = hud.Q("yarn-pips");
            int pipCount = Mathf.Min(25, maxYarn);
            while (pips.Count < pipCount) { VisualElement pip = Slot(slots, "yarn-pip"); pipRow.Add(pip); pips.Add(pip); }
            int charged = Mathf.RoundToInt(yarn / (float)maxYarn * pipCount);
            for (int i = 0; i < pips.Count; i++)
            {
                VisualElement pip = pips[i];
                pip.style.display = i < pipCount ? DisplayStyle.Flex : DisplayStyle.None;
                pip.EnableInClassList("full", i < charged);
                pip.Q("wind").style.width = Length.Percent(i == charged ? r.winding * 100 : 0);
            }

            Text(hud, "floor-label", string.Format(T.floorLabel, r.floor));
            // 라운드 목록: 생존 R1·R2는 컨셉 이름, 쉬어가기, 보스(컨셉 이름)
            bool done = r.phase != Phase.Combat;
            int combatIndex = 0;
            for (int i = 0; i < rounds.Length && i < db.rounds.sequence.Length; i++)
            {
                RoundKind kind = db.rounds.sequence[i];
                string name = kind == RoundKind.Rest ? T.restRoundName
                    : combatIndex < r.floorConcepts.Length ? (kind == RoundKind.Boss ? string.Format(T.bossRoundLabel, r.floorConcepts[combatIndex].name) : r.floorConcepts[combatIndex].name)
                    : T.survivalLabel;
                if (kind != RoundKind.Rest) combatIndex++;
                Text(rounds[i], $"round-name-{i + 1}", name);
                rounds[i].EnableInClassList("current", i == r.roundIndex && !done);
                rounds[i].EnableInClassList("done", i < r.roundIndex || (i == r.roundIndex && done));
            }
            string route = r.phase switch
            {
                Phase.Purifying => T.purified,
                Phase.Cleared when r.roundKind == RoundKind.Rest => T.routeShop,
                Phase.Cleared => string.Format(T.routeCleared, r.floor, r.round),
                _ when r.roundKind == RoundKind.Boss => T.routeBoss,
                _ => string.Format(T.routeRemaining, r.enemies.Count),
            };
            Text(hud, "route-info", route);

            // 생존 타이머 (분:초)
            bool timing = r.phase == Phase.Combat && r.roundKind == RoundKind.Survival;
            Show(timer, timing);
            if (timing)
            {
                int left = Mathf.CeilToInt(combat.TimeLeft(r));
                timerText.text = string.Format(T.timer, left / 60, left % 60);
                timer.EnableInClassList("urgent", left <= 10);
            }
            // 라운드가 끝난 뒤 쌓인 증강 안내
            bool pending = r.phase != Phase.Combat && r.augmentQueue > 0 && Opened == "";
            Show(hint, pending);
            if (pending) hintText.text = string.Format(T.augmentHint, r.augmentQueue);
            else if (game.CanOpenRunInventory) { Show(hint, true); hintText.text = T.inventoryHint; }

            Enemy boss = r.enemies.FirstOrDefault(e => e.IsBoss);
            Show(hud.Q("boss-hud"), boss != null);
            if (boss != null) Meter(hud, "boss-bar", boss.hp / boss.maxHp);
        }

        // ---------------------------------------------------------------- dialogs

        public string Opened { get; private set; } = "";

        public void ShowDialog(string id)
        {
            Opened = id;
            Show(overlay, true);
            foreach (VisualElement dialog in overlay.Children()) Show(dialog, dialog.name == $"dialog-{id}");
        }

        public void CloseDialog()
        {
            Opened = "";
            Show(overlay, false);
        }

        Button Card(VisualElement container, Sprite icon, string tag, string name, string text, string foot, Action onClick, bool enabled = true)
        {
            var card = (Button)Slot(choiceCard, "card");
            Icon(card.Q("icon"), icon);
            Text(card, "tag", tag ?? "");
            Text(card, "name", name);
            Text(card, "text", text);
            Text(card, "foot", foot ?? "");
            card.Q("tag").style.display = string.IsNullOrEmpty(tag) ? DisplayStyle.None : DisplayStyle.Flex;
            card.Q("foot").style.display = string.IsNullOrEmpty(foot) ? DisplayStyle.None : DisplayStyle.Flex;
            card.SetEnabled(enabled);
            card.clicked += onClick;
            container.Add(card);
            return card;
        }

        // 퍼즐 조각 모양 카드: 색 띠(태그) · 둥근 액자 속 아이콘 · 이름 · 효과 · (조각 모양) · 설명
        Button PuzzleCard(VisualElement container, Color color, Sprite icon, string tag, string name, string effect, string text, Action onClick)
        {
            var card = (Button)Slot(slots, "puzzle");
            card.Q("shine").style.display = DisplayStyle.None;
            // 종이 카드(Assets/UI/Paper/card.png)를 등급 색으로 살짝 물들이고, 띠(찢어진 양피지)는 진하게 물들인다. 돌기 테두리는 USS 의 잉크 선.
            card.style.unityBackgroundImageTintColor = PaperTint(color);
            foreach (string knob in new[] { "knob-top", "knob-right" }) card.Q(knob).style.backgroundColor = color;
            SetBorder(card.Q("frame"), color);
            card.Q("band").style.unityBackgroundImageTintColor = Color.Lerp(color, Color.white, .3f);
            Icon(card.Q("icon"), icon);
            Text(card, "tag", tag ?? "");
            Text(card, "name", name);
            Text(card, "effect", effect ?? "");
            Text(card, "text", text ?? "");
            card.Q("shape").style.display = DisplayStyle.None;
            card.clicked += onClick;
            container.Add(card);
            return card;
        }

        static Color PaperTint(Color c) => Color.Lerp(Color.white, c, .38f);

        static void SetBorder(VisualElement e, Color c)
        {
            e.style.borderTopColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c; e.style.borderRightColor = c;
        }

        public void ShowAugments(List<AugmentDef> choices, RunState r, Action<string> pick, bool canReroll)
        {
            SetReroll("reroll-augment", canReroll);
            root.Q<Label>("augment-title").text = string.Format(T.augmentTitle, r.level - r.augmentQueue + 1);
            VisualElement container = root.Q("augment-choices");
            container.Clear();
            foreach (AugmentDef a in choices)
            {
                Color color = a.color.a > 0 ? a.color : new Color(.9f, .66f, .75f);
                int owned = r.augmentsTaken.TryGetValue(a.id, out int n) ? n : 0;
                PuzzleCard(container, color, a.icon, string.Format(T.augmentLevel, owned + 1), a.name, a.text, game.Combat.AugmentPreview(r, a), () => pick(a.id));
            }
            ShowDialog("augment");
        }

        public void ShowDesire(DesireDef offer, int floor, bool refine)
        {
            Text(root, "desire-eyebrow", string.Format(T.desireEyebrow, floor));
            Text(root, "desire-name", offer.name);
            Text(root, "desire-text", offer.text);
            Text(root, "desire-penalty", refine ? offer.penalty : string.Format(T.desirePenalty, offer.penalty));
            ShowDialog("desire");
        }

        public void ShowShop(ShopConfig shop, Func<WareDef, int> price, Func<WareDef, bool> sold, int gold, string saleLabel, Action<string> buy)
        {
            Text(root, "sale-label", saleLabel);
            VisualElement container = root.Q("wares");
            container.Clear();
            foreach (WareDef w in shop.wares)
            {
                bool done = sold(w);
                int cost = price(w);
                Card(container, w.icon, null, w.name, w.text, done ? T.soldOut : string.Format(T.price, cost), () => buy(w.id), !done && gold >= cost);
            }
            ShowDialog("shop");
        }

        public void SetSaleLabel(string text) => Text(root, "sale-label", text);

        // 쉬어가는 라운드의 아티팩트(재봉 조각) 3택 1
        void SetReroll(string name, bool available)
        {
            var button = root.Q<Button>(name);
            button.text = available ? T.reroll : T.rerollUsed;
            button.SetEnabled(available);
        }

        // 등반 중 재봉 상자: 안내 문구를 보여 준다 (닫기 버튼·Esc·I 는 전투로 돌아간다).
        public void SetInventoryInRun(bool inRun)
        {
            Label note = root.Q<Label>("inventory-run-note");
            note.text = T.inventoryInRun;
            Show(note, inRun);
        }

        public void ShowArtifacts(int floor, IEnumerable<int> kinds, Action<int> pick, bool canReroll)
        {
            SetReroll("reroll-artifact", canReroll);
            shines.Clear();
            Text(root, "artifact-eyebrow", string.Format(T.artifactEyebrow, floor));
            VisualElement container = root.Q("artifacts");
            container.Clear();
            foreach (int kind in kinds)
            {
                PieceDef def = db.gear.pieces[kind];
                SetDef set = db.Set(def.set);
                string value = def.value < 1 ? $"{Mathf.RoundToInt(def.value * 100)}%" : def.value.ToString("0.##");
                string effect = string.Format(T.pieceEffect, T.statNames[(int)def.stat], value);
                string text = string.Format(T.pieceShape, def.shape.Length, set.name, set.bonus) + (string.IsNullOrEmpty(def.flavor) ? "" : "\n" + def.flavor);
                // 카드 색은 등급(코스트) 색: 일반 회색 · 레어 하늘색 · 유니크 노란색 · 레전더리 붉은색. 세트 색은 조각 모양에 남긴다.
                Color[] ranks = db.gear.rankColors;
                Color rankColor = ranks != null && def.cost < ranks.Length ? ranks[def.cost] : set.color;
                Button card = PuzzleCard(container, rankColor, def.icon, $"{db.gear.rankNames[def.cost]} · {set.name}", def.name, effect, text, () => pick(kind));
                card.AddToClassList($"rank-{def.cost}");
                float shine = db.gear.rankShine != null && def.cost < db.gear.rankShine.Length ? db.gear.rankShine[def.cost] : 0;
                VisualElement band = card.Q("shine");
                band.style.display = shine > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (shine > 0) shines.Add((card, band, shine, shines.Count * .37f, rankColor));
                VisualElement shape = card.Q("shape");
                shape.style.display = DisplayStyle.Flex;
                PieceIcon(shape, def.shape.ToList(), set.color);
                shape.style.scale = new Scale(new Vector2(1.6f, 1.6f));
                // 아이콘이 아직 없으면 조각 모양만 크게 보여 준다
                if (def.icon == null) card.Q("frame").style.display = DisplayStyle.None;
            }
            ShowDialog("artifact");
        }

        public void ShowUpgradeConfirm(UpgradeDef upgrade, int cost)
        {
            Text(root, "upgrade-text", string.Format(T.upgradeBody, cost, upgrade.name));
            ShowDialog("upgrade");
        }

        // ---------------------------------------------------------------- rooms

        public void RenderSummary(Outcome outcome, int floor, int kills, int shards)
        {
            Text(summary, "summary-title", outcome switch
            {
                Outcome.Clear => T.summaryClear, Outcome.Dead => T.summaryDead, Outcome.Absorbed => T.summaryAbsorbed, _ => T.summaryRetreat,
            });
            Text(summary, "summary-body", string.Format(T.summaryBody, floor, kills, shards));
            VisualElement vfx = summary.Q("summary-vfx");
            Texture2D image = outcome == Outcome.Clear ? stitchVfx : outcome == Outcome.Absorbed ? bossVfx : null;
            vfx.style.backgroundImage = image != null ? new StyleBackground(image) : StyleKeyword.None;
        }

        public void RenderAttic(Profile profile, Func<UpgradeDef, int> cost, Func<UpgradeDef, bool> maxed, Func<UpgradeDef, int> level, Action<string> pick)
        {
            VisualElement container = attic.Q("upgrades");
            container.Clear();
            foreach (UpgradeDef u in db.upgrades)
            {
                bool max = maxed(u);
                string foot = max ? T.upgradeMax : string.Format(T.upgradeCost, cost(u), level(u));
                Card(container, u.icon, null, u.name, u.text, foot, () => pick(u.key), !max && profile.shards >= cost(u));
            }
        }

        public void RenderInventory(Profile profile, int? selected, int turns)
        {
            var (width, height, limit) = Gear.Dimensions(db, profile);
            List<PlacedPiece> placed = Gear.Placed(db, profile);
            Text(inventory, "inventory-header", string.Format(T.inventoryHeader, width, height, placed.Sum(p => p.def.cost), limit));
            VisualElement board = inventory.Q("board");
            board.Clear();
            cells.Clear();
            float size = Mathf.Floor(560f / width) - 4;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = (Button)Slot(slots, "cell");
                    var at = new Vector2Int(x, y);
                    cell.style.width = size;
                    cell.style.height = size;
                    PlacedPiece occupying = placed.FirstOrDefault(p => p.cells.Contains(at));
                    cell.EnableInClassList("filled", occupying != null);
                    cell.EnableInClassList("reinforced", y < profile.rows * 2);
                    Color color = occupying != null ? db.Set(occupying.def.set).color : new Color32(0x30, 0x24, 0x3a, 255);
                    cell.style.backgroundColor = color;
                    cell.userData = color;
                    cell.text = occupying != null && occupying.placement.x == x && occupying.placement.y == y ? "✦" : "";
                    cell.clicked += () => game.ClickCell(at.x, at.y);
                    cell.RegisterCallback<PointerEnterEvent>(_ => game.HoverCell(at));
                    board.Add(cell);
                    cells[at] = cell;
                }
            }
            var stash = inventory.Q<ScrollView>("stash");
            stash.Clear();
            OwnedItem holding = profile.items.FirstOrDefault(i => i.id == selected);
            foreach (OwnedItem item in profile.items)
            {
                PieceDef def = Gear.Def(db, item);
                PlacedPiece placement = placed.FirstOrDefault(p => p.Id == item.id);
                var button = (Button)Slot(pieceButton, "piece");
                button.EnableInClassList("selected", item.id == selected);
                PieceIcon(button.Q("icon"), Gear.Shape(db, item, item.id == selected ? turns : placement?.placement.turns ?? 0), db.Set(def.set).color);
                Text(button, "name", def.name);
                Text(button, "cost", (placement != null ? T.equipped : "") + def.cost);
                string value = def.value < 1 ? $"{Mathf.RoundToInt(def.value * 100)}%" : def.value.ToString("0.##");
                button.tooltip = string.Format(T.pieceTooltip, db.gear.rankNames[def.cost], T.statNames[(int)def.stat], value);
                int id = item.id;
                button.clicked += () => game.SelectPiece(id);
                stash.Add(button);
            }
            VisualElement sets = inventory.Q("sets");
            sets.Clear();
            GearStats stats = Gear.Stats(db, profile);
            foreach (SetDef set in db.gear.sets)
            {
                SetGroup group = stats.groups[set.id];
                var line = (Label)Slot(slots, "set-line");
                line.text = string.Format(T.setLine, set.name, group.size, group.threshold > 0 ? string.Format(T.setOn, group.threshold) : T.setOff, set.bonus);
                line.style.color = Color.Lerp(set.color, Color.white, .1f);
                sets.Add(line);
            }
            inventory.Q<Button>("rotate").SetEnabled(holding != null);
            inventory.Q<Button>("unequip").SetEnabled(placed.Any(p => p.Id == selected));
        }

        void PieceIcon(VisualElement icon, List<Vector2Int> shape, Color color)
        {
            icon.Clear();
            foreach (Vector2Int c in shape)
            {
                VisualElement dot = Slot(slots, "piece-dot");
                dot.style.left = c.x * 7;
                dot.style.top = c.y * 7;
                dot.style.backgroundColor = color;
                icon.Add(dot);
            }
        }

        public void PreviewPiece(Profile profile, OwnedItem item, int turns, Vector2Int? at)
        {
            foreach (Button cell in cells.Values)
            {
                cell.RemoveFromClassList("preview");
                cell.RemoveFromClassList("invalid");
                cell.style.backgroundColor = (Color)cell.userData;
            }
            if (at == null || item == null) return;
            bool valid = Gear.CanPlace(db, profile, item, turns, at.Value.x, at.Value.y);
            Color color = db.Set(Gear.Def(db, item).set).color;
            foreach (Vector2Int c in Gear.Shape(db, item, turns))
            {
                if (!cells.TryGetValue(at.Value + c, out Button cell)) continue;
                cell.AddToClassList(valid ? "preview" : "invalid");
                if (valid) cell.style.backgroundColor = Color.Lerp(color, new Color32(0x25, 0x20, 0x2c, 255), .48f);
                else cell.style.backgroundColor = StyleKeyword.Null;
            }
        }
    }
}
