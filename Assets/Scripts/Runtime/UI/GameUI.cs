// UI Toolkit 화면 (웹판 main.js 의 DOM 갱신 부분). 화면 구조는 Assets/UI/*.uxml, 목록 항목은 UXML 템플릿을 복제한다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public enum UiScreen { Lobby, Inventory, Attic, Summary, Combat }

    public partial class GameUI : MonoBehaviour, IWorldText   // 15차 해금 알림, 시작 조각 창: GameUI.Outgame.cs
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
        float shownHp = -1;   // 회복 때 체력 게이지가 차오르는 표시값
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
            Bind("summary-attic", game.GoToAttic);   // 17차: 다락은 걸어 들어가는 방
            Bind("summary-lobby", () => game.ShowScreen(UiScreen.Lobby));
            Bind("inventory-back", game.BackFromRoom);
            Bind("attic-back", game.BackFromRoom);
            Bind("rotate", game.Rotate);
            InitSort();   // 21차 목록 정렬 (GameUI.Board.cs)
            Bind("unequip", game.Unequip);
            root.Q("board").RegisterCallback<PointerLeaveEvent>(_ => { if (!dragging) game.HoverCell(null); });
            // 꾹 눌러 끌기: 판 위 조각·목록 줄을 누른 채 기다리거나 끌면 집어 들고, 놓은 칸에 다시 장착한다.
            inventory.RegisterCallback<PointerDownEvent>(OnPressDown, TrickleDown.TrickleDown);
            inventory.RegisterCallback<PointerMoveEvent>(OnPressMove, TrickleDown.TrickleDown);
            inventory.RegisterCallback<PointerUpEvent>(OnPressUp, TrickleDown.TrickleDown);
            InitOutgame();
            InitBoard();   // 19차 챙기기 판, 모이라의 줄 고르기: GameUI.Board.cs
            InitHub();   // 15차 재봉실 허브: GameUI.Hub.cs
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
            TickInventory();
            TickMoira();   // 19차 모이라 카드 (GameUI.Moira.cs)
            TickFate();    // 20차 운명 읽기 연출 (GameUI.Fate.cs)
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
            Text(root, "attic-shards", profile.shards.ToString());   // 17차: 실궤 창 종이 오른쪽 위
            Text(root, "best-record", $"◇ 최고 기록  {(profile.best > 0 ? string.Format(T.bestRecord, profile.best) : T.bestNone)}");
        }

        public void UpdateHud(RunState r, Combat combat)
        {
            int maxYarn = combat.YarnMax(r);
            int yarn = Mathf.FloorToInt(r.yarn);
            Text(hud, "level", string.Format(T.level, r.level));
            Text(hud, "yarn-value", string.Format(T.ratio, yarn, maxYarn));
            float bonus = combat.StrandBonus(r.strands.Count);
            Text(hud, "thread-count", bonus > 0 ? string.Format(T.threadBonus, r.strands.Count, Mathf.RoundToInt(bonus * 100)) : string.Format(T.threadCount, r.strands.Count));
            Text(hud, "knot-ready", string.Format(T.knotReady, Mathf.Max(1, combat.KnotInterval(r) - r.shotCount)));
            string mood = r.mood == Mood.Frenzy ? T.frenzySuffix : r.mood == Mood.Awakened ? T.awakenSuffix : "";
            Text(hud, "desire-value", string.Format(T.desireValue, Mathf.FloorToInt(r.desire), r.maxDesire, mood));
            Text(hud, "gold", r.gold.ToString());
            Text(hud, "knots", r.knots.Count.ToString());
            // 회복은 게이지가 천천히 차오르고(숫자도 같이 올라감), 피해는 바로 줄어든다.
            if (shownHp < 0 || r.time < .05f) shownHp = r.hp;   // 새 등반은 꽉 찬 상태로 시작
            if (r.hp > shownHp) shownHp = Mathf.MoveTowards(shownHp, r.hp, r.maxHp * Time.unscaledDeltaTime * .22f);   // 15% 회복이 약 0.7초에 걸쳐 차오른다
            else shownHp = r.hp;
            bool filling = shownHp < r.hp - .01f;
            Meter(hud, "hp-bar", shownHp / r.maxHp);
            VisualElement heal = hud.Q("hp-heal");
            heal.style.width = Length.Percent(Mathf.Clamp01(r.hp / r.maxHp) * 100);
            heal.style.opacity = filling ? .6f + .4f * Mathf.Sin(Now * 18) : 0;
            Text(hud, "hp-value", string.Format(T.ratio, Mathf.Round(shownHp), r.maxHp));
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
            string was = Opened;
            Opened = "";
            Show(overlay, false);
            if (was != "") DialogClosed?.Invoke(was);   // 16차 상인 작별 인사 (GameUI.Merchant.cs)
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

        // 재봉 상자 목록 줄: 세트 색이 또렷하도록 진하게 물들이고 조금 어둡게 (밝은 등급 색 이름이 읽히게)
        static Color SetPaperTint(Color c) => Color.Lerp(c, Color.black, .2f);

        // 등급 색 글자: 어두운 진한 색(레전더리 붉은색)은 같은 색조로 밝혀 종이 위에서 읽히게. 회색처럼 채도가 낮은 색은 그대로.
        static Color NameColor(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return s > .3f && v < .85f ? Color.HSVToRGB(h, s, .97f) : c;
        }

        // 카드 띠의 세트 이름: 같은 색조를 밝고 조금 연하게 (검은 테두리 위에서 또렷하게)
        static Color TagColor(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Min(s, .75f), Mathf.Max(v, 1f));
        }

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
                // 16차: 팔림, 금화 부족도 눌러서 상인이 대답하게 (누를 수는 있고 흐리게만)
                Card(container, w.icon, null, w.name, w.text, done ? T.soldOut : string.Format(T.price, cost), () => buy(w.id)).EnableInClassList("ware-dim", done || gold < cost);
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
            foreach (int kind in kinds) PieceOfferCard(container, kind, () => pick(kind));
            ShowDialog("artifact");
        }

        // 조각 하나를 퍼즐 카드로 (쉬어가기 탑 아이템 창, 15차 허브 상인 단추 상점이 같이 쓴다)
        Button PieceOfferCard(VisualElement container, int kind, Action onClick)
        {
            PieceDef def = db.gear.pieces[kind];
            SetDef set = db.Set(def.set);
            string value = def.value < 1 ? $"{Mathf.RoundToInt(def.value * 100)}%" : def.value.ToString("0.##");
            string effect = string.Format(T.pieceEffect, T.statNames[(int)def.stat], value);
            string unique = UniqueText(def);
            if (unique != null) effect += "\n" + unique;
            string text = string.Format(T.pieceShape, def.shape.Length, set.name, set.bonus) + (string.IsNullOrEmpty(def.flavor) ? "" : "\n" + def.flavor);
            // 카드 색은 등급(코스트) 색: 일반 회색 · 레어 하늘색 · 유니크 노란색 · 레전더리 붉은색. 세트 색은 조각 모양에 남긴다.
            Color[] ranks = db.gear.rankColors;
            Color rankColor = ranks != null && def.cost < ranks.Length ? ranks[def.cost] : set.color;
            // 띠: 등급 + 세트 이름(밝힌 세트 색 글씨, 리치 텍스트). 글자 전체에 검은 테두리(.puzzle-tag)를 둘러 어떤 띠 색에서도 읽히게.
            string setTag = $"<color=#{ColorUtility.ToHtmlStringRGB(TagColor(set.color))}>{set.name}</color>";
            Button card = PuzzleCard(container, rankColor, def.icon, $"{db.gear.rankNames[def.cost]}  {setTag}", def.name, effect, text, onClick);
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
            return card;
        }

        // 19차: 모이라(의식)와 단추(들고 갈 수 늘리기)가 같은 확인 창을 쓴다. 비어 있는 문구는 UXML 처음 문구 그대로.
        string upgradeEyebrow0, upgradeTitle0, upgradeButton0;

        public void ShowUpgradeConfirm(string eyebrow, string title, string body, string confirm)
        {
            Label e = root.Q<Label>("upgrade-eyebrow"), t = root.Q<Label>("upgrade-title");
            Button b = root.Q<Button>("confirm-upgrade");
            upgradeEyebrow0 ??= e.text; upgradeTitle0 ??= t.text; upgradeButton0 ??= b.text;
            e.text = string.IsNullOrEmpty(eyebrow) ? upgradeEyebrow0 : eyebrow;
            t.text = string.IsNullOrEmpty(title) ? upgradeTitle0 : title;
            b.text = string.IsNullOrEmpty(confirm) ? upgradeButton0 : confirm;
            Text(root, "upgrade-text", body);
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

        // 19차 모이라 카드 창: GameUI.Moira.cs

        // ---------------------------------------------------------------- 재봉 상자
        // 판 = 칸 버튼(누르기·호버) 위에 조각 그림 층(board-pieces)과 반투명 미리보기 층(board-ghost)을 겹친다.
        // 조각 한 칸 = Slots.uxml piece-tile 복제: 종이·테두리 마스크(k{n}, MaskKey) · 바깥 변 바늘땀 · 매듭 · 아이콘.

        // 장착 바느질: 조각이 툭 놓이고(Drop) → 바늘이 한 땀(StitchPx)씩 종이를 들락날락하며 둘레를 돌고 → 매듭이 톡(Knot)
        const float SewDrop = .16f, SewStitch = .05f, SewKnot = .32f, StitchPx = 18f;
        const float HoldTime = .28f, DragSlop = 14f;   // 꾹 누르기: 이만큼 누르고 있거나 이만큼 끌면 집어 든다
        static readonly Vector2Int[] Normal = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };   // 위·오른·아래·왼 (판 아래로 y 증가)
        static readonly Vector2Int[] Tangent = { new(1, 0), new(0, 1), new(-1, 0), new(0, -1) };  // 시계 방향으로 도는 방향
        static readonly string[] SeamNames = { "seam-t", "seam-r", "seam-b", "seam-l" };

        class Seam
        {
            public VisualElement box, thread;
            public Vector2 origin, from, to;          // 칸의 판 좌표, 바늘땀 시작·끝(판 좌표 px, 바늘 진행 방향)
            public float length, thick;
            public bool horizontal;
            public Vector2Int edgeFrom, edgeTo;       // 격자 꼭짓점 (둘레 순서 잇기용)
        }

        class PieceArt
        {
            public int id;
            public readonly List<VisualElement> tiles = new();
            public readonly List<Seam> seams = new();    // 둘레를 도는 순서
            public readonly List<(VisualElement shine, float x)> shines = new();
            public readonly List<VisualElement> sparkles = new();
            public VisualElement mark;   // 회전 중심 칸의 아이템 아이콘
            public float width, perimeter;
            public Vector2 center;       // 조각 가운데(판 좌표) — 놓일 때·당길 때 이 점을 기준으로 움직인다
            public readonly List<VisualElement> parts = new();   // 함께 움직이는 칸 그림·아이콘
        }

        // 서로 다른 두 조각이 맞닿은 한 칸 길이의 경계 (홈 + 건너 잇는 땀). from → to 가 바느질 방향.
        sealed class Join
        {
            public int a, b;
            public VisualElement box, art;
            public Vector2 from, to;
            public bool vertical;
            public float length;
        }

        readonly List<PieceArt> boardArt = new();
        readonly List<Join> joins = new();
        readonly List<VisualElement> sparks = new();
        Vector2 dropFrom, pendingDrop;   // 끌어 놓은 조각이 손끝 자리 → 칸 자리로 미끄러져 들어가는 시작 어긋남
        int dragTurns, dragBuiltId = -1, dragBuiltTurns = -1;
        float dragBuiltPitch;
        readonly Dictionary<Vector2Int, int> boardIds = new();   // 판 칸 → 그 칸의 조각 id
        int sewId = -1, boardWidth, boardHeight;
        float sewStart, pitch;
        bool holdingPiece;
        // 꾹 눌러 끌기 상태
        int pressId = -1, dragId = -1, pressPointer;
        float pressTime;
        Vector2 pressPos, pointerPos;
        bool dragging;

        Color RankColor(PieceDef def)
        {
            Color[] ranks = db.gear.rankColors;
            return ranks != null && def.cost < ranks.Length ? ranks[def.cost] : db.Set(def.set).color;
        }

        static string Amount(float value) => value < 1 ? $"{Mathf.RoundToInt(value * 100)}%" : value.ToString("0.##");

        // 고유 효과 한 줄: "★ 벼랑 끝 — 체력 30% 이하일 때 당기기 피해 +60%" (없으면 null)
        string UniqueText(PieceDef def)
        {
            if (def.effect == ItemEffect.None || db.gear.effectTexts == null) return null;
            ItemEffectText text = System.Array.Find(db.gear.effectTexts, t => t.effect == def.effect);
            if (text == null) return null;
            return $"★ {text.name} — " + string.Format(text.text, Amount(def.effectValue), Amount(def.effectParam));
        }

        public void RenderInventory(Profile profile, int? selected, int turns, BoardMode mode = null)
        {
            mode ??= new BoardMode();
            boardMode = mode;
            var (width, height, limit) = Gear.Dimensions(db, profile);
            List<PlacedPiece> placed = Gear.Placed(db, profile);
            Text(inventory, "inventory-header", string.Format(T.inventoryHeader, width, height, placed.Sum(p => Unlocks.Cost(db, profile, p)), limit));
            if (game.Run == null) SetOutgameBoardTexts(profile, placed.Count, mode);   // 19차: 챙기기 판, 줄 고르기 판 (GameUI.Board.cs)
            OwnedItem holding = game.ItemFor(selected);
            holdingPiece = holding != null;
            dragTurns = turns;
            HideTip();

            // 칸: 누르기·호버만 맡는 바탕 (조각 그림은 위 층)
            VisualElement board = inventory.Q("board");
            board.Clear();
            cells.Clear();
            pitch = Mathf.Floor(Mathf.Min(560f / width, 640f / height));   // 19차: 가로 줄을 붙여 판이 길어져도 화면 안에
            boardWidth = width;
            boardHeight = height;
            boardIds.Clear();
            foreach (PlacedPiece p in placed) foreach (Vector2Int c in p.cells) boardIds[c] = p.Id;
            board.style.width = pitch * width;
            board.style.height = pitch * height;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = (Button)Slot(slots, "cell");
                    var at = new Vector2Int(x, y);
                    cell.style.width = pitch - 4;
                    cell.style.height = pitch - 4;
                    // 20차: 줄바꿈(flex-wrap)에 맡기지 않고 자리를 정한다 (8칸 너비에서 반올림으로 7칸씩 줄바꿈되던 문제)
                    cell.style.position = Position.Absolute;
                    cell.style.left = x * pitch;
                    cell.style.top = y * pitch;
                    PlacedPiece occupying = placed.FirstOrDefault(p => p.cells.Contains(at));
                    cell.EnableInClassList("filled", occupying != null);
                    cell.text = "";
                    cell.clicked += () => game.ClickCell(at.x, at.y);
                    cell.RegisterCallback<PointerEnterEvent>(e =>
                    {
                        if (dragging) return;
                        game.HoverCell(at);
                        if (occupying != null && !holdingPiece) ShowTip(occupying.def, e.position);
                    });
                    cell.RegisterCallback<PointerMoveEvent>(e => MoveTip(e.position));
                    cell.RegisterCallback<PointerLeaveEvent>(_ => HideTip());
                    board.Add(cell);
                    cells[at] = cell;
                }
            }

            DrawBadges(profile, width, height);   // 21차: 강화한 칸 "+N" (GameUI.Board.cs)

            // 판 위 조각 그림
            VisualElement layer = inventory.Q("board-pieces");
            layer.Clear();
            boardArt.Clear();
            // 같은 세트(같은 색) 조각이 맞닿으면 종이 명암을 달리해 서로 다른 영역으로 보이게 한다 (이웃과 겹치지 않는 명암 고르기)
            var tone = new Dictionary<int, int>();
            foreach (PlacedPiece piece in placed)
            {
                var used = new HashSet<int>();
                foreach (Vector2Int c in piece.cells)
                    for (int d = 0; d < 4; d++)
                        if (boardIds.TryGetValue(c + Normal[d], out int other) && other != piece.Id && tone.TryGetValue(other, out int t)
                            && placed.First(p => p.Id == other).def.set == piece.def.set) used.Add(t);
                tone[piece.Id] = Enumerable.Range(0, 3).First(t => !used.Contains(t) || t == 2);
            }
            foreach (PlacedPiece piece in placed)
            {
                PieceArt art = BuildPiece(layer, piece.def, piece.cells, piece.Id, new Vector2Int(piece.placement.x, piece.placement.y), tone[piece.Id]);
                if (piece.Id == selected) foreach (VisualElement tile in art.tiles) tile.AddToClassList("selected");
                if (piece.Id == dragId) foreach (VisualElement part in art.parts) part.style.opacity = .3f;   // 끌고 있는 조각의 원래 자리는 흐리게
                boardArt.Add(art);
            }
            // 서로 다른 조각이 맞닿은 경계: 어두운 홈 + 건너 잇는 땀 (붙어 있지만 다른 영역)
            float joinWidth = Mathf.Round(pitch / 3f);
            joins.Clear();
            foreach (var (c, id) in boardIds)
            {
                for (int d = 1; d <= 2; d++)
                {
                    if (!boardIds.TryGetValue(c + Normal[d], out int other) || other == id) continue;
                    if (dragging && (id == dragId || other == dragId)) continue;   // 집어 든 조각은 이음이 풀린 채
                    VisualElement box = Slot(slots, "piece-join");
                    bool vertical = d == 1;
                    box.AddToClassList(vertical ? "piece-join-v" : "piece-join-h");
                    VisualElement art = box.Q("join-art");
                    art.style.width = vertical ? joinWidth : pitch;
                    art.style.height = vertical ? pitch : joinWidth;
                    var join = new Join
                    {
                        a = id, b = other, box = box, art = art, vertical = vertical, length = pitch,
                        from = vertical ? new Vector2((c.x + 1) * pitch, c.y * pitch) : new Vector2(c.x * pitch, (c.y + 1) * pitch),
                    };
                    join.to = join.from + (vertical ? new Vector2(0, pitch) : new Vector2(pitch, 0));
                    SetJoin(join, 1, joinWidth);
                    joins.Add(join);
                    layer.Add(box);
                }
            }
            if (boardArt.All(a => a.id != sewId)) sewId = -1;
            ApplySew();
            inventory.Q("board-ghost").Clear();
            inventory.Q("board-ghost").RemoveFromClassList("paper");
            DrawPickLayer(profile, width, height);   // 19차 줄 고르기: 강화한 줄 표시, 확장 가장자리 띠

            // 목록: 아이콘 · 퍼즐 모양 슬롯 · 이름(오른쪽 끝). 아이템은 모두 고유해서 개수는 없다. 장착한 것 먼저, 등급 높은 순.
            var stash = inventory.Q<ScrollView>("stash");
            stash.Clear();
            float rowHeight = Mathf.Round(pitch * 1.3f);
            stash.style.maxHeight = Mathf.Max(pitch * height, (rowHeight + 4) * 4);   // 최소 4줄은 보이게
            stash.style.display = mode.pick != null ? DisplayStyle.None : DisplayStyle.Flex;   // 줄 고르기 중에는 목록 없음
            StashSort sort = game.Profile?.stashSort ?? StashSort.Grade;   // 21차: 오른쪽 위 정렬 버튼 (등반 중에도 실제 프로필의 선택)
            var rows = Gear.SortStash(db, profile.items, i => placed.Any(p => p.Id == i.id), sort);
            DrawSortButton(sort, mode.pick == null);
            if (mode.pick == null && mode.offers != null) rows.AddRange(mode.offers.OrderByDescending(i => Gear.Def(db, i).cost));   // 19차: 단추 진열 (놓으면 삼)
            foreach (OwnedItem item in rows)
            {
                PieceDef def = Gear.Def(db, item);
                PlacedPiece placement = placed.FirstOrDefault(p => p.Id == item.id);
                bool offer = mode.offers != null && mode.offers.Contains(item);
                var button = (Button)Slot(pieceButton, "piece");
                button.AddToClassList($"rank-{def.cost}");
                button.EnableInClassList("selected", item.id == selected);
                button.EnableInClassList("equipped", placement != null);
                button.style.height = rowHeight;
                // 줄 바탕 종이와 퍼즐 모양 = 세트 색, 이름 = 등급 색(잉크 외곽선)
                Color setColor = db.Set(def.set).color;
                button.style.unityBackgroundImageTintColor = SetPaperTint(setColor);
                Icon(button.Q("icon"), def.icon);
                List<Vector2Int> shape = Gear.Shape(db, item, item.id == selected ? turns : placement?.placement.turns ?? 0);
                MiniShape(button.Q("shape"), shape, Color.Lerp(setColor, Color.white, .25f), 16);   // 바탕보다 밝게
                Text(button, "name", def.name);
                button.Q<Label>("name").style.color = NameColor(RankColor(def));
                Text(button, "set", db.Set(def.set).name);   // 이름 위 세트 이름 (세트 색)
                button.Q<Label>("set").style.color = Color.Lerp(setColor, Color.white, .3f);
                Text(button, "state", offer ? string.Format(db.hub.priceFormat, mode.price?.Invoke(item.kind) ?? 0) : placement != null ? T.equipped : "");
                button.EnableInClassList("offer", offer);
                button.EnableInClassList("short", offer && profile.shards < (mode.price?.Invoke(item.kind) ?? 0));
                // 등급 꼬리표: 등급 색 바탕 + 같은 색의 어두운 글씨
                Label rankTag = button.Q<Label>("rank");
                rankTag.text = db.gear.rankNames[def.cost];
                rankTag.style.backgroundColor = RankColor(def);
                rankTag.style.color = def.cost >= 4 ? new Color(1f, .95f, .88f) : Color.Lerp(RankColor(def), Color.black, .72f);
                int id = item.id;
                button.userData = id;   // 꾹 눌러 끌기에서 어떤 아이템인지
                button.clicked += () => game.SelectPiece(id);
                button.RegisterCallback<PointerEnterEvent>(e => ShowTip(def, e.position));
                button.RegisterCallback<PointerMoveEvent>(e => MoveTip(e.position));
                button.RegisterCallback<PointerLeaveEvent>(_ => HideTip());
                stash.Add(button);
            }

            RenderSets(Gear.Stats(db, profile));
            if (dragging)   // 끄는 중 회전(R)해도 손끝 조각·미리보기 유지
            {
                RenderDragPiece();
                game.HoverCell(BoardCell(pointerPos));
            }
            inventory.Q<Button>("rotate").SetEnabled(holding != null);
            inventory.Q<Button>("unequip").SetEnabled(placed.Any(p => p.Id == selected));
            Show(inventory.Q<Button>("rotate"), mode.pick == null);
            Show(inventory.Q<Button>("unequip"), mode.pick == null);
        }

        // 세피리아식 세트 목록: 색 매듭 · 이름 · 연결 수 / 다음 단계 · 단계별 효과(켜진 단계는 밝게). 켜진 세트가 위로.
        void RenderSets(GearStats stats)
        {
            VisualElement list = inventory.Q("set-list");
            list.Clear();
            // 16차: 켜진 세트는 메인 → 부가 순서(Gear.MainFirst), 그 뒤 꺼진 세트
            foreach (SetDef set in db.gear.sets.OrderBy(s => { int i = stats.sets.FindIndex(a => a.def == s); return i < 0 ? 99 : i; })
                .ThenByDescending(s => stats.groups[s.id].tier).ThenByDescending(s => stats.groups[s.id].size))
            {
                SetGroup group = stats.groups[set.id];
                VisualElement entry = Slot(slots, "set-entry");
                entry.EnableInClassList("active", group.tier > 0);
                entry.Q("swatch").style.backgroundColor = set.color;
                // 16차: 메인 속성(판에 조각이 가장 많은 켜진 세트, 모든 공격) / 부가 속성(procCharge 타마다 발동)
                int rank = stats.sets.FindIndex(s => s.def == set);
                string role = rank < 0 ? "" : rank == 0 ? T.setMain : string.Format(T.setSub, set.procCharge);
                Text(entry, "set-name", set.name);
                Text(entry, "set-role", role);   // 이름 아래 한 줄 (이름 옆에 두면 연결 수와 겹친다)
                entry.Q("set-role").style.display = string.IsNullOrEmpty(role) ? DisplayStyle.None : DisplayStyle.Flex;
                entry.Q<Label>("set-role").style.color = rank == 0 ? Color.Lerp(set.color, Color.white, .35f) : new Color(.72f, .65f, .6f);
                int next = set.thresholds.FirstOrDefault(t => t > group.size);
                Text(entry, "set-count", next > 0 ? string.Format(T.setProgress, group.size, next) : string.Format(T.setProgressMax, group.size));
                entry.Q<Label>("set-name").style.color = group.tier > 0 ? Color.Lerp(set.color, Color.white, .25f) : new Color(.66f, .59f, .54f);
                VisualElement tiers = entry.Q("tiers");
                // 12차: 세트가 8개라 켜진 단계 + 다음 단계 하나만 보인다. 판에 하나도 없는 세트는 이름 줄만.
                tiers.style.display = group.size > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                for (int i = 0; i < set.thresholds.Length; i++)
                {
                    if (i > group.tier) break;
                    VisualElement tier = Slot(slots, "set-tier");
                    bool on = i < group.tier;
                    tier.EnableInClassList("on", on);
                    tier.EnableInClassList("next", i == group.tier);
                    Text(tier, "need", set.thresholds[i].ToString());
                    // 단계 효과: "화염 피해 +2, 화상 초당 2"
                    string effect = string.Format(T.pieceEffect, set.bonus, Amount(set.damagePerTier * (i + 1)));
                    if (!string.IsNullOrEmpty(set.effectText)) effect += ", " + string.Format(set.effectText, Amount(set.effectPerTier * (i + 1)));
                    Text(tier, "effect", effect);
                    VisualElement need = tier.Q("need");
                    need.style.backgroundColor = on ? set.color : new Color(0, 0, 0, 0);
                    SetBorder(need, on ? set.color : new Color(.42f, .36f, .33f));
                    tiers.Add(tier);
                }
                list.Add(entry);
            }
        }

        // 조각 그림 한 벌을 판 좌표(칸 목록)로 만든다.
        // pivot = 회전 중심 칸의 판 좌표 (아이콘이 이 칸 슬롯 가운데에 놓인다)
        // tone: 0 그대로 · 1 밝게 · 2 어둡게 (맞닿은 같은 세트 조각끼리 구분)
        PieceArt BuildPiece(VisualElement layer, PieceDef def, List<Vector2Int> cellList, int id, Vector2Int pivot, int tone = 0)
        {
            var art = new PieceArt { id = id };
            var set = new HashSet<Vector2Int>(cellList);
            float inset = Mathf.Round(pitch * .14f);   // 바늘땀: 찢긴 가장자리와 칸 슬롯(slot_frame) 사이
            float thick = Mathf.Max(3, Mathf.Round(pitch * .055f));
            Color fill = db.Set(def.set).color;   // 11차: 판 위 조각 종이 = 세트 색 (등급은 테두리·실·장식이 맡는다)
            if (tone == 1) fill = Color.Lerp(fill, Color.white, .2f);
            else if (tone == 2) fill = Color.Lerp(fill, Color.black, .16f);
            int minX = cellList.Min(c => c.x), maxX = cellList.Max(c => c.x);
            int minY = cellList.Min(c => c.y), maxY = cellList.Max(c => c.y);
            art.width = (maxX - minX + 1) * pitch;
            art.center = new Vector2((minX + maxX + 1) * .5f * pitch, (minY + maxY + 1) * .5f * pitch);
            // 매듭은 맨 위·왼쪽 칸, 아이콘은 회전 중심 칸 슬롯의 정사각형 가운데
            Vector2Int knotCell = cellList.OrderBy(c => c.y).ThenBy(c => c.x).First();
            Vector2 center = new((pivot.x + .5f) * pitch, (pivot.y + .5f) * pitch);
            Vector2Int lastCell = cellList.OrderByDescending(c => c.y).ThenByDescending(c => c.x).First();
            var seams = new List<Seam>();
            foreach (Vector2Int c in cellList)
            {
                VisualElement tile = Slot(slots, "piece-tile");
                tile.AddToClassList($"rank-{def.cost}");
                Vector2 origin = new(c.x * pitch, c.y * pitch);
                tile.style.left = origin.x;
                tile.style.top = origin.y;
                tile.style.width = pitch;
                tile.style.height = pitch;
                int bits = MaskKey(set, c) & 15;
                tile.Q("paper").AddToClassList($"k{MaskKey(set, c)}");
                tile.Q("edge").AddToClassList($"k{MaskKey(set, c)}");
                tile.Q("paper").style.unityBackgroundImageTintColor = fill;
                for (int d = 0; d < 4; d++)
                {
                    VisualElement box = tile.Q(SeamNames[d]);
                    if ((bits & (1 << d)) == 0) { box.style.display = DisplayStyle.None; continue; }
                    Vector2Int n = Normal[d], t = Tangent[d];
                    // 모서리: 이웃이 없으면 안쪽에서 꺾이고(볼록), 이웃과 대각선이 다 있으면 바깥으로 이어진다(오목)
                    float startExt = set.Contains(c - t) ? (set.Contains(c - t + n) ? -inset : 0) : inset;
                    float endExt = set.Contains(c + t) ? (set.Contains(c + t + n) ? -inset : 0) : inset;
                    Vector2 a, b;
                    switch (d)
                    {
                        case 0: a = new(startExt, inset); b = new(pitch - endExt, inset); break;
                        case 1: a = new(pitch - inset, startExt); b = new(pitch - inset, pitch - endExt); break;
                        case 2: a = new(pitch - startExt, pitch - inset); b = new(endExt, pitch - inset); break;
                        default: a = new(inset, pitch - startExt); b = new(inset, endExt); break;
                    }
                    var seam = new Seam
                    {
                        box = box, thread = box.Q("thread"), origin = origin, from = origin + a, to = origin + b,
                        length = Vector2.Distance(a, b), thick = thick, horizontal = d % 2 == 0,
                    };
                    seam.edgeFrom = d switch { 0 => c, 1 => c + new Vector2Int(1, 0), 2 => c + Vector2Int.one, _ => c + new Vector2Int(0, 1) };
                    seam.edgeTo = seam.edgeFrom + t;
                    seam.thread.style.width = seam.horizontal ? seam.length : thick;
                    seam.thread.style.height = seam.horizontal ? thick : seam.length;
                    seams.Add(seam);
                    SetSeam(seam, 1);
                }
                VisualElement knot = tile.Q("knot");
                knot.style.display = c == knotCell ? DisplayStyle.Flex : DisplayStyle.None;
                knot.style.backgroundColor = RankColor(def);   // 종이가 세트 색이므로 매듭은 등급 색
                tile.Q("ornament").style.display = def.cost >= 3 && c == lastCell ? DisplayStyle.Flex : DisplayStyle.None;
                VisualElement sparkle = tile.Q("sparkle");
                bool sparkles = def.cost >= 4 && (c == knotCell || c == lastCell);
                sparkle.style.display = sparkles ? DisplayStyle.Flex : DisplayStyle.None;
                if (sparkles) art.sparkles.Add(sparkle);
                VisualElement shine = tile.Q("shine");
                shine.style.display = def.cost >= 4 ? DisplayStyle.Flex : DisplayStyle.None;
                if (def.cost >= 4) art.shines.Add((shine, (c.x - minX) * pitch));
                tile.style.transformOrigin = new TransformOrigin(art.center.x - origin.x, art.center.y - origin.y);
                art.tiles.Add(tile);
                art.parts.Add(tile);
                layer.Add(tile);
            }
            if (def.icon != null)
            {
                art.mark = Slot(slots, "piece-mark");
                float size = pitch * .5f;   // 슬롯 홈(칸의 56%) 안에 들어가는 크기
                art.mark.style.width = size;
                art.mark.style.height = size;
                art.mark.style.left = center.x - size * .5f;
                art.mark.style.top = center.y - size * .5f;
                Icon(art.mark, def.icon);
                art.mark.style.transformOrigin = new TransformOrigin(art.center.x - (center.x - size * .5f), art.center.y - (center.y - size * .5f));
                art.parts.Add(art.mark);
                layer.Add(art.mark);
            }
            // 세트 이름 꼬리표: 조각 맨 윗줄 칸들의 가운데 위 (세트 색 글씨)
            {
                SetDef setDef = db.Set(def.set);
                VisualElement tag = Slot(slots, "board-set-tag");   // 맨 윗줄 폭의 줄 안에 꼬리표를 가운데 정렬
                Label label = tag.Q<Label>("tag");
                label.text = setDef.name;
                label.style.color = Color.Lerp(setDef.color, Color.white, .3f);
                var top = cellList.Where(c => c.y == minY).ToList();
                float left = top.Min(c => c.x) * pitch, topY = minY * pitch + pitch * .04f;
                tag.style.left = left;
                tag.style.top = topY;
                tag.style.width = (top.Max(c => c.x) - top.Min(c => c.x) + 1) * pitch;
                tag.style.transformOrigin = new TransformOrigin(art.center.x - left, art.center.y - topY);
                art.parts.Add(tag);
                layer.Add(tag);
            }
            // 둘레 순서: 격자 꼭짓점을 따라 시계 방향으로 잇는다 (맨 위 왼쪽 윗변에서 시작)
            var remaining = seams.OrderBy(s => s.edgeFrom.y).ThenBy(s => s.edgeFrom.x).ToList();
            while (remaining.Count > 0)
            {
                Seam current = remaining[0];
                remaining.RemoveAt(0);
                art.seams.Add(current);
                while (true)
                {
                    Vector2Int end = current.edgeTo;
                    int next = remaining.FindIndex(s => s.edgeFrom == end);
                    if (next < 0) break;
                    current = remaining[next];
                    remaining.RemoveAt(next);
                    art.seams.Add(current);
                }
            }
            art.perimeter = art.seams.Sum(sm => sm.length);
            return art;
        }

        // 바늘땀 한 변을 진행 방향으로 f(0~1)만큼 드러낸다. 실 무늬는 제자리에 둔다.
        static void SetSeam(Seam seam, float f)
        {
            float shown = seam.length * Mathf.Clamp01(f);
            Vector2 local = Vector2.Min(seam.from, seam.to) - seam.origin;
            bool forward = seam.horizontal ? seam.to.x >= seam.from.x : seam.to.y >= seam.from.y;
            float offset = forward ? 0 : seam.length - shown;
            seam.box.style.display = shown > .5f ? DisplayStyle.Flex : DisplayStyle.None;
            if (seam.horizontal)
            {
                seam.box.style.left = local.x + offset;
                seam.box.style.top = local.y - seam.thick * .5f;
                seam.box.style.width = shown;
                seam.box.style.height = seam.thick;
                seam.thread.style.left = -offset;
            }
            else
            {
                seam.box.style.left = local.x - seam.thick * .5f;
                seam.box.style.top = local.y + offset;
                seam.box.style.width = seam.thick;
                seam.box.style.height = shown;
                seam.thread.style.top = -offset;
            }
        }

        // 조각을 판에 놓은 직후: 조각이 제자리로 툭 들어앉으며 불투명해지고(끌어 놓았으면 손끝 자리에서 미끄러져 들어감),
        // 이웃 조각과 맞닿았으면 그 경계만 땀땀이 꿰매고, 혼자면 둘레를 한 바퀴 꿰맨 뒤 매듭이 톡 튄다.
        public void Sew(int id)
        {
            sewId = id;
            sewStart = Now;
            dropFrom = pendingDrop;
            pendingDrop = Vector2.zero;
            ApplySew();
        }

        static float EaseOut(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 3);

        // 둘레를 따라 dist 만큼 간 점과 그 자리의 진행 방향
        static (Vector2 point, Vector2 dir) AlongPerimeter(PieceArt art, float dist)
        {
            foreach (Seam seam in art.seams)
            {
                if (dist <= seam.length || seam == art.seams[^1])
                {
                    Vector2 dir = (seam.to - seam.from).normalized;
                    return (Vector2.Lerp(seam.from, seam.to, Mathf.Clamp01(dist / Mathf.Max(1, seam.length))), dir);
                }
                dist -= seam.length;
            }
            return (art.center, Vector2.right);
        }

        // 경계 이음을 from → to 방향으로 f(0~1)만큼 드러낸다. 그림(join-art)은 제자리에 두고 바깥 창만 늘린다.
        void SetJoin(Join join, float f, float width = -1)
        {
            if (width < 0) width = Mathf.Round(pitch / 3f);
            float shown = join.length * Mathf.Clamp01(f);
            Vector2 start = Vector2.Min(join.from, join.to);
            bool forward = join.vertical ? join.to.y >= join.from.y : join.to.x >= join.from.x;
            float offset = forward ? 0 : join.length - shown;
            join.box.style.display = shown > .5f ? DisplayStyle.Flex : DisplayStyle.None;
            if (join.vertical)
            {
                join.box.style.left = start.x - width * .5f;
                join.box.style.top = start.y + offset;
                join.box.style.width = width;
                join.box.style.height = shown;
                join.art.style.left = 0;
                join.art.style.top = -offset;
            }
            else
            {
                join.box.style.left = start.x + offset;
                join.box.style.top = start.y - width * .5f;
                join.box.style.width = shown;
                join.box.style.height = width;
                join.art.style.left = -offset;
                join.art.style.top = 0;
            }
        }

        // 꿰맬 이음들을 바늘이 이어서 지나가는 순서로 (맨 위 왼쪽부터, 다음은 지금 끝점에서 가장 가까운 이음)
        List<Join> SewJoins(int id)
        {
            var left = joins.Where(j => j.a == id || j.b == id).ToList();
            var order = new List<Join>();
            Vector2 at = left.Count > 0 ? left.Select(j => Vector2.Min(j.from, j.to)).OrderBy(v => v.y).ThenBy(v => v.x).First() : Vector2.zero;
            while (left.Count > 0)
            {
                Join next = left.OrderBy(j => Mathf.Min(Vector2.Distance(at, j.from), Vector2.Distance(at, j.to))).First();
                left.Remove(next);
                if (Vector2.Distance(at, next.to) < Vector2.Distance(at, next.from)) (next.from, next.to) = (next.to, next.from);
                order.Add(next);
                at = next.to;
            }
            return order;
        }

        void ApplySew()
        {
            VisualElement needle = inventory.Q("needle");
            PieceArt art = sewId >= 0 ? boardArt.FirstOrDefault(a => a.id == sewId) : null;
            if (art == null || art.seams.Count == 0) { Show(needle, false); HideSparks(); sewId = -1; return; }
            List<Join> sewJoins = SewJoins(art.id);
            bool joining = sewJoins.Count > 0;   // 이웃과 맞닿았으면 그 경계만 꿰맨다
            int stitches = joining ? sewJoins.Count * 2 : Mathf.Max(6, Mathf.RoundToInt(art.perimeter / StitchPx));
            float sewTime = joining ? Mathf.Clamp(stitches * .07f, .35f, 1.1f) : Mathf.Clamp(stitches * SewStitch, .6f, 1.2f);
            float t = Now - sewStart;
            float scale = 1, rotate = 0, opacity = 1, revealed = 0, sewn = 0;
            Vector2 shift = Vector2.zero, knotAt = joining ? sewJoins[^1].to : art.seams[^1].to;
            if (t < SewDrop)
            {
                // 1) 손끝 자리에서 칸 자리로 미끄러져 툭 들어앉으며 반투명이 풀린다
                float u = EaseOut(t / SewDrop);
                shift = Vector2.Lerp(dropFrom, Vector2.zero, u);
                scale = Mathf.Lerp(1.06f, 1f, u);
                rotate = Mathf.Lerp(-3f, 0f, u);
                opacity = Mathf.Lerp(.62f, 1f, u);
                Show(needle, false);
            }
            else if (t < SewDrop + sewTime)
            {
                // 2) 한 땀씩: 바늘이 종이를 뚫고 들어갔다 나오고(번갈아 이쪽·저쪽), 땀이 생길 때마다 조각이 당겨지며 움찔한다
                float p = (t - SewDrop) / sewTime * stitches;
                int k = Mathf.Min(stitches - 1, Mathf.FloorToInt(p));
                float f = p - k;
                float hop = Mathf.Sin(f * Mathf.PI);
                float side = k % 2 == 0 ? 1 : -1;
                Vector2 point, dir;
                float reach;
                if (joining)
                {
                    // 경계 한 칸 = 두 땀. 바늘이 경계를 가로질러 두 조각을 건너 잇는다.
                    sewn = (k + EaseOut(f)) / 2f;
                    int index = Mathf.Min(sewJoins.Count - 1, k / 2);
                    Join join = sewJoins[index];
                    point = Vector2.Lerp(join.from, join.to, Mathf.Clamp01(sewn - index));
                    dir = (join.to - join.from).normalized;
                    reach = pitch * .2f;
                }
                else
                {
                    revealed = (k + EaseOut(f)) * art.perimeter / stitches;
                    (point, dir) = AlongPerimeter(art, revealed);
                    reach = 7f;
                }
                Vector2 inward = new(-dir.y, dir.x);   // 시계 방향으로 도니 오른쪽이 조각 안쪽
                Vector2 tip = point + inward * (hop * reach * side);
                needle.style.left = tip.x;
                needle.style.top = tip.y;
                needle.style.rotate = new Rotate(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + side * hop * (joining ? 40f : 30f));
                needle.style.scale = new Scale(new Vector2(1 - .3f * hop, 1));
                Show(needle, true);
                scale = 1 + .03f * hop;
                rotate = side * .9f * hop;
            }
            else
            {
                // 3) 매듭: 조각이 한 번 부풀었다 가라앉고, 마지막 땀 자리에서 불티가 튄다
                float u = (t - SewDrop - sewTime) / SewKnot;
                revealed = art.perimeter;
                sewn = sewJoins.Count;
                Show(needle, false);
                scale = 1 + .08f * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                Burst(knotAt, Mathf.Clamp01(u));
                if (u >= 1) { sewId = -1; HideSparks(); scale = 1; }
            }
            if (joining && t >= SewDrop) revealed = art.perimeter;   // 이을 때 바깥 둘레는 이미 꿰매진 채, 경계만 꿰맨다
            float walked = 0;
            foreach (Seam seam in art.seams)
            {
                SetSeam(seam, (revealed - walked) / Mathf.Max(1, seam.length));
                walked += seam.length;
            }
            for (int i = 0; i < sewJoins.Count; i++) SetJoin(sewJoins[i], sewn - i);
            foreach (VisualElement part in art.parts)
            {
                part.style.scale = new Scale(new Vector2(scale, scale));
                part.style.rotate = new Rotate(rotate);
                part.style.translate = new Translate(shift.x, shift.y);
                part.style.opacity = opacity;
            }
        }

        // 매듭 불티: 마지막 땀 자리에서 다섯 개가 바깥으로 튀며 사라진다
        void Burst(Vector2 at, float u)
        {
            VisualElement parent = inventory.Q("needle").parent;
            while (sparks.Count < 5) { VisualElement spark = Slot(slots, "sew-spark"); parent.Add(spark); sparks.Add(spark); }
            for (int i = 0; i < sparks.Count; i++)
            {
                float angle = (i * 72 - 90) * Mathf.Deg2Rad;
                float r = EaseOut(u) * pitch * .38f;
                VisualElement spark = sparks[i];
                spark.style.display = DisplayStyle.Flex;
                spark.style.left = at.x + Mathf.Cos(angle) * r;
                spark.style.top = at.y + Mathf.Sin(angle) * r;
                spark.style.opacity = 1 - u;
                spark.style.scale = new Scale(new Vector2(1.2f - u * .6f, 1.2f - u * .6f));
            }
        }

        void HideSparks()
        {
            foreach (VisualElement spark in sparks) spark.style.display = DisplayStyle.None;
        }

        // ---------------------------------------------------------------- 꾹 눌러 끌기
        // 누른 채 끌면 조각 전체가 반투명하게 손끝(회전 중심 칸 가운데)을 따라오고, 판 위에서는 놓일 칸이 옅게 표시된다.
        // 떼면 그 칸에 들어가며 반투명이 풀린다 (못 놓는 자리·판 밖이면 원래대로).

        // 판 좌표의 칸 (판 밖이면 null)
        Vector2Int? BoardCell(Vector2 panelPos)
        {
            VisualElement board = inventory.Q("board");
            if (board.panel == null || pitch <= 0) return null;
            Vector2 local = board.WorldToLocal(panelPos);
            int x = Mathf.FloorToInt(local.x / pitch), y = Mathf.FloorToInt(local.y / pitch);
            if (local.x < 0 || local.y < 0 || x >= boardWidth || y >= boardHeight) return null;
            return new Vector2Int(x, y);
        }

        void OnPressDown(PointerDownEvent e)
        {
            if (e.button != 0 || dragging || boardMode?.pick != null) return;   // 19차: 줄 고르기 중에는 끌기 없음
            pointerPos = pressPos = e.position;
            pressPointer = e.pointerId;
            pressTime = Now;
            pressId = -1;
            Vector2Int? cell = BoardCell(e.position);
            if (cell != null && boardIds.TryGetValue(cell.Value, out int onBoard)) { pressId = onBoard; return; }
            for (var element = e.target as VisualElement; element != null && element != inventory; element = element.parent)
                if (element.ClassListContains("piece-row") && element.userData is int id) { pressId = id; return; }
        }

        void OnPressMove(PointerMoveEvent e)
        {
            pointerPos = e.position;
            if (pressId >= 0 && !dragging && Vector2.Distance(pressPos, pointerPos) > DragSlop) BeginDrag();
            if (!dragging) return;
            MoveDragPiece();
            game.HoverCell(BoardCell(pointerPos));
        }

        void OnPressUp(PointerUpEvent e)
        {
            if (!dragging) { pressId = -1; return; }
            // 끌어서 놓기: 버튼 눌림(클릭)이 따로 일어나지 않게 여기서 멈춘다. 화면은 곧 다시 그려져 포인터 붙잡기도 풀린다.
            e.StopImmediatePropagation();
            inventory.ReleasePointer(e.pointerId);
            int id = dragId;
            Vector2Int? cell = BoardCell(e.position);
            // 놓인 조각이 손끝 자리에서 칸 자리로 미끄러져 들어가도록 어긋남을 기억해 둔다 (Sew 가 씀)
            pendingDrop = cell is Vector2Int at
                ? (Vector2)inventory.Q("board").WorldToLocal(e.position) - new Vector2((at.x + .5f) * pitch, (at.y + .5f) * pitch)
                : Vector2.zero;
            CancelPress();
            game.DropDrag(id, cell);
            pendingDrop = Vector2.zero;   // 못 놓았으면 버린다
        }

        void BeginDrag()
        {
            dragging = true;
            dragId = pressId;
            // 판이 다시 그려지며 눌린 칸이 사라져도 이동·놓기를 계속 받도록 화면 자체가 포인터를 붙잡는다
            inventory.CapturePointer(pressPointer);
            HideTip();
            inventory.Q("board-ghost").AddToClassList("drag");
            Show(inventory.Q("drag-piece"), true);
            game.BeginDrag(dragId);   // 고른 조각으로 삼고 다시 그림 (원래 자리는 흐리게, 손끝 조각도 여기서 만들어짐)
            MoveDragPiece();
            game.HoverCell(BoardCell(pointerPos));
        }

        // 손끝 조각: 회전 중심 칸을 (0,0)에 두고 조각 그림을 한 벌 만든다. 조각·회전·칸 크기가 바뀔 때만 다시 만든다.
        void RenderDragPiece()
        {
            VisualElement holder = inventory.Q("drag-piece");
            if (dragBuiltId == dragId && dragBuiltTurns == dragTurns && Mathf.Approximately(dragBuiltPitch, pitch)) return;
            holder.Clear();
            dragBuiltId = dragId;
            dragBuiltTurns = dragTurns;
            dragBuiltPitch = pitch;
            OwnedItem item = game.ItemFor(dragId);   // 19차: 단추 진열 조각도
            if (item == null) return;
            BuildPiece(holder, Gear.Def(db, item), Gear.Shape(db, item, dragTurns).ToList(), -1, Vector2Int.zero);
        }

        void MoveDragPiece()
        {
            RenderDragPiece();
            VisualElement holder = inventory.Q("drag-piece");
            Vector2 p = inventory.WorldToLocal(pointerPos);
            holder.style.left = p.x - pitch * .5f;
            holder.style.top = p.y - pitch * .5f;
        }

        void CancelPress()
        {
            pressId = -1;
            if (!dragging && dragId < 0) return;
            dragging = false;
            dragId = -1;
            if (inventory == null) return;
            if (inventory.HasPointerCapture(pressPointer)) inventory.ReleasePointer(pressPointer);
            VisualElement holder = inventory.Q("drag-piece");
            holder.Clear();
            dragBuiltId = -1;
            Show(holder, false);
            inventory.Q("board-ghost").RemoveFromClassList("drag");
        }

        // 재봉 상자가 열려 있을 때 매 프레임: 바느질 · 레전더리 빛 줄기와 반짝임
        void TickInventory()
        {
            if (inventory == null || inventory.ClassListContains("hidden")) { CancelPress(); return; }
            TickGrow();   // 20차 칸 강화, 확장 연출 (GameUI.Board.cs)
            if (pressId >= 0 && !dragging && Now - pressTime >= HoldTime) BeginDrag();
            if (sewId >= 0) ApplySew();
            for (int p = 0; p < boardArt.Count; p++)
            {
                PieceArt art = boardArt[p];
                if (art.shines.Count == 0) continue;
                float u = Mathf.Repeat(Now * .55f + p * .37f, 1.8f) / 1.8f;
                float band = -pitch + u * (art.width + pitch * 2);
                foreach (var (shine, x) in art.shines) shine.style.left = band - x;
                for (int i = 0; i < art.sparkles.Count; i++)
                {
                    float s = Mathf.Max(0, Mathf.Sin(Now * 3.1f + i * 2.3f + p));
                    art.sparkles[i].style.opacity = s * s;
                    art.sparkles[i].style.scale = new Scale(new Vector2(.6f + s * .5f, .6f + s * .5f));
                }
            }
        }

        // 종이 마스크 번호 (tools/bake_puzzle_tiles.py, Puzzle.uss): 바깥 변 비트(위1·오른2·아래4·왼8)
        // + 16 × 오목 모서리 비트(왼위1·오른위2·오른아래4·왼아래8: 두 변은 이웃이 있는데 대각선 칸이 빈 곳)
        static int MaskKey(HashSet<Vector2Int> set, Vector2Int c)
        {
            int bits = 0, concave = 0;
            for (int d = 0; d < 4; d++) if (!set.Contains(c + Normal[d])) bits |= 1 << d;
            int[,] corners = { { 0, 3 }, { 0, 1 }, { 2, 1 }, { 2, 3 } };
            for (int i = 0; i < 4; i++)
            {
                int a = corners[i, 0], b = corners[i, 1];
                if ((bits & (1 << a)) == 0 && (bits & (1 << b)) == 0 && !set.Contains(c + Normal[a] + Normal[b])) concave |= 1 << i;
            }
            return bits + 16 * concave;
        }

        // 목록·정보 창·카드의 작은 퍼즐 모양 (판과 같은 종이 마스크를 작게)
        void MiniShape(VisualElement box, List<Vector2Int> shape, Color color, float cellPx)
        {
            box.Clear();
            // 회전 중심 기준 상대 좌표(음수 가능)를 왼쪽 위 0,0 으로 옮긴다
            Vector2Int min = new(shape.Min(c => c.x), shape.Min(c => c.y));
            shape = shape.Select(c => c - min).ToList();
            var set = new HashSet<Vector2Int>(shape);
            box.style.width = (shape.Max(c => c.x) + 1) * cellPx;
            box.style.height = (shape.Max(c => c.y) + 1) * cellPx;
            foreach (Vector2Int c in shape)
            {
                VisualElement dot = Slot(slots, "piece-dot");
                dot.AddToClassList($"k{MaskKey(set, c)}");
                dot.style.left = c.x * cellPx;
                dot.style.top = c.y * cellPx;
                dot.style.width = cellPx;
                dot.style.height = cellPx;
                dot.style.unityBackgroundImageTintColor = color;
                // 칸 나눔 선(1px) + 바깥 잉크 테두리(2px): 같은 세트 색 종이 위에서도 모양이 보이게
                dot.style.borderRightWidth = set.Contains(c + Vector2Int.right) ? 1 : 2;
                dot.style.borderBottomWidth = set.Contains(c + new Vector2Int(0, 1)) ? 1 : 2;
                dot.style.borderLeftWidth = set.Contains(c + Vector2Int.left) ? 0 : 2;
                dot.style.borderTopWidth = set.Contains(c + new Vector2Int(0, -1)) ? 0 : 2;
                box.Add(dot);
            }
        }

        void PieceIcon(VisualElement icon, List<Vector2Int> shape, Color color) => MiniShape(icon, shape, color, 12);

        // 반투명 미리보기: 놓일 자리에 조각 모양 그대로, 못 놓으면 붉게.
        public void PreviewPiece(Profile profile, OwnedItem item, int turns, Vector2Int? at)
        {
            VisualElement ghost = inventory.Q("board-ghost");
            ghost.Clear();
            foreach (Button cell in cells.Values) cell.RemoveFromClassList("preview");
            if (at == null || item == null) return;
            bool valid = Gear.CanPlace(db, profile, item, turns, at.Value.x, at.Value.y);
            ghost.EnableInClassList("invalid", !valid);
            List<Vector2Int> shape = Gear.Shape(db, item, turns).Select(c => c + at.Value).ToList();
            PieceArt art = BuildPiece(ghost, Gear.Def(db, item), shape, -1, at.Value);
            // 끄는 중에는 손끝 조각과 겹쳐 보이므로 놓일 자리를 밝은 상아색(못 놓으면 붉은색) 영역으로 칠한다
            if (!valid) foreach (VisualElement tile in art.tiles) tile.Q("paper").style.unityBackgroundImageTintColor = new Color(.86f, .24f, .26f);
            else if (dragging) foreach (VisualElement tile in art.tiles) tile.Q("paper").style.unityBackgroundImageTintColor = new Color(1f, .96f, .84f);
            foreach (Vector2Int c in shape) if (cells.TryGetValue(c, out Button cell)) cell.AddToClassList("preview");
        }

        // ---------------------------------------------------------------- 아이템 정보 창

        void ShowTip(PieceDef def, Vector2 panelPos)
        {
            VisualElement tip = inventory.Q("piece-tip");
            SetDef set = db.Set(def.set);
            Color rank = RankColor(def);
            Icon(tip.Q("tip-icon"), def.icon);
            Text(tip, "tip-name", def.name);
            tip.Q<Label>("tip-name").style.color = NameColor(rank);
            Text(tip, "tip-set-name", set.name);
            tip.Q<Label>("tip-set-name").style.color = NameColor(Color.Lerp(set.color, Color.white, .25f));
            Text(tip, "tip-rank", db.gear.rankNames[def.cost]);
            tip.Q<Label>("tip-rank").style.color = Color.Lerp(rank, Color.black, .4f);
            MiniShape(tip.Q("tip-shape"), Gear.Rotate(def.shape, 0), set.color, 14);
            Text(tip, "tip-cells", string.Format(T.pieceCells, def.shape.Length));
            Text(tip, "tip-effect", string.Format(T.pieceEffect, T.statNames[(int)def.stat], Amount(def.value)));
            string unique = UniqueText(def);
            Text(tip, "tip-unique", unique ?? "");
            tip.Q("tip-unique").style.display = unique == null ? DisplayStyle.None : DisplayStyle.Flex;
            tip.Q("tip-knot").style.backgroundColor = set.color;
            Text(tip, "tip-set", string.Format(T.tipSet, set.name, set.bonus, string.Join(", ", set.thresholds)));
            Text(tip, "tip-flavor", def.flavor ?? "");
            tip.Q("tip-flavor").style.display = string.IsNullOrEmpty(def.flavor) ? DisplayStyle.None : DisplayStyle.Flex;
            tip.style.unityBackgroundImageTintColor = Color.Lerp(Color.white, rank, .22f);
            Show(tip, true);
            MoveTip(panelPos);
        }

        void MoveTip(Vector2 panelPos)
        {
            VisualElement tip = inventory.Q("piece-tip");
            if (tip.ClassListContains("hidden")) return;
            Vector2 p = inventory.WorldToLocal(panelPos);
            float w = float.IsNaN(tip.layout.width) || tip.layout.width < 1 ? 440 : tip.layout.width;
            float h = float.IsNaN(tip.layout.height) || tip.layout.height < 1 ? 220 : tip.layout.height;
            float x = p.x + 28, y = p.y + 20;
            if (x + w > inventory.layout.width - 8) x = p.x - w - 20;
            if (y + h > inventory.layout.height - 8) y = inventory.layout.height - h - 8;
            tip.style.left = Mathf.Max(8, x);
            tip.style.top = Mathf.Max(8, y);
        }

        void HideTip() => Show(inventory.Q("piece-tip"), false);
    }
}
