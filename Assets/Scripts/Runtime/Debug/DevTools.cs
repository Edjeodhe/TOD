// 개발자 모드: F1 로 여닫는 디버그 패널 (Main.uxml dev-panel). 에디터와 개발 빌드에서만 켜진다.
// 버튼 이름 → 동작은 아래 Bind 에서 연결한다. 지급량 같은 값은 인스펙터(MCP)에서 넣는다.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TOD
{
    public class DevTools : MonoBehaviour
    {
        public GameController game;
        public UIDocument document;
        public VisualTreeAsset setButton;     // Assets/UI/DevSetButton.uxml
        [Header("지급량")]
        public int goldAmount;
        public int shardAmount;
        public float desireStep;

        // 23차: 쉬어가기 NPC 바로 부르기. 23-npc 세션이 연결한다: id(blessing 축복 수녀, gift 행상 쥐, xp 촛불 유령, gamble 단추 인형) → 화면에 띄울 결과 문구.
        // 연결 전이면 버튼이 "연결 전" 문구만 띄운다.
        public static System.Func<string, string> SummonNpc;
        static readonly string[] NpcIds = { "blessing", "gift", "xp", "gamble" };
        const int LegendaryRank = 4;   // PieceDef.cost: 1 일반 … 4 레전더리, 5 신화(Mythic.Rank)

        VisualElement panel;
        Label status;
        Button god, freeze, ignoreLocks;
        float statusUntil;
        string flash;

        GameDatabase Db => game.db;
        RunState Run => game.Run;
        Profile Profile => game.Profile;
        Profile Bag => Run != null ? Run.bag : Profile;   // 23차: 등반 중에는 등반 가방, 아니면 영구 프로필(시작 조각)

        void Start()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) { enabled = false; return; }
            VisualElement root = document.rootVisualElement;
            panel = root.Q("dev-panel");
            if (panel == null) { enabled = false; return; }
            status = panel.Q<Label>("dev-status");
            god = panel.Q<Button>("dev-god");
            freeze = panel.Q<Button>("dev-freeze");
            ignoreLocks = panel.Q<Button>("dev-ignore-locks");

            Bind("dev-god", () => { if (Run != null) Run.devGod = !Run.devGod; });
            Bind("dev-heal", () => { if (Run != null) Run.hp = Run.maxHp; });
            Bind("dev-desire-max", () => { if (Run != null) game.Combat.DevMaxDesire(Run); });
            Bind("dev-level", () => { if (Run != null) game.Combat.DevLevelUp(Run); });
            Bind("dev-yarn", () => { if (Run != null) Run.yarn = game.Combat.YarnMax(Run); });
            Bind("dev-gold", () => { if (Run != null) Run.gold += goldAmount; });
            Bind("dev-shards", () => { Profile.shards += shardAmount; game.DevGearChanged(); });
            Button shardsButton = panel.Q<Button>("dev-shards");
            if (shardsButton != null) shardsButton.text = $"파편 +{shardAmount}";
            Bind("dev-desire-up", () => { if (Run != null) game.Combat.AdjustDesire(Run, desireStep); });
            Bind("dev-desire-down", () => { if (Run != null) game.Combat.AdjustDesire(Run, -desireStep); });
            foreach (string type in new[] { "stalker", "charger", "shooter", "boss" })
                Bind($"dev-spawn-{type}", () => { if (Run != null) game.Combat.DevSpawn(Run, type); });
            Bind("dev-bind", () => Say(Run != null && game.Combat.DevBind(Run) ? "가장 가까운 적에게 결박 실을 걸었습니다 (우클릭)" : "적이 없습니다"));
            Bind("dev-kill", () => { if (Run != null) game.Combat.DevKillAll(Run); });
            Bind("dev-end-round", () => { if (Run != null) { game.Combat.DevEndRound(Run); Say("현재 라운드 클리어"); } });
            // 23차 진행: 보스 바로 클리어(처치 흐름 그대로), 다음 라운드, 다음 층, 쉬어가기, 지정 층
            Bind("dev-boss-clear", BossClear);
            Bind("dev-next-round", () => Jump(r => game.Combat.Ascend(r), "다음 라운드로"));
            Bind("dev-next-floor", () =>
            {
                if (Run != null && Run.floor >= Db.arena.finalFloor) { Say("마지막 층입니다"); return; }
                Jump(r => game.Combat.DevGoToFloor(r, r.floor + 1), "다음 층 1라운드로");
            });
            Bind("dev-rest", () => Jump(r => game.Combat.DevGoToRound(r, RoundKind.Rest), "쉬어가기 라운드로"));
            Bind("dev-legendary", () => GiveRank(false));
            Bind("dev-mythic", () => GiveRank(true));
            foreach (string id in NpcIds)
                Bind($"dev-npc-{id}", () => Say(SummonNpc == null ? "쉬어가기 NPC 연결 전입니다 (23-npc)" : SummonNpc(id) ?? $"NPC 부름: {id}"));
            Bind("dev-freeze", () => { if (Run != null) Run.devFreeze = !Run.devFreeze; });
            Bind("dev-all-items", GiveAll);
            Bind("dev-clear-board", () => { Bag.placements.Clear(); game.DevGearChanged(); });
            Bind("dev-clear-items", () => { Bag.placements.Clear(); Bag.items.Clear(); game.DevGearChanged(); });
            // 15차 해금: 잠금 무시(저장 안 함), 최고 층 조절, 알림 다시 보기
            Bind("dev-ignore-locks", () => { Unlocks.ignoreLocks = !Unlocks.ignoreLocks; Say(Unlocks.ignoreLocks ? "잠긴 세트도 나옵니다" : "세트 잠금을 다시 지킵니다"); });
            Bind("dev-best-up", () => { Profile.best++; game.DevGearChanged(); Say($"최고 층 {Profile.best} (로비로 돌아오면 해금 알림)"); });
            Bind("dev-best-reset", () => { Profile.best = 0; Profile.seenUnlocks.Clear(); game.DevGearChanged(); Say("최고 층과 해금 알림 기록을 지웠습니다"); });
            Bind("dev-unlock-notice", game.DevShowUnlocks);

            // 16차: 층 속성 바꾸기 (몬스터, 바닥 색조, 장식, 암시 문구가 함께 바뀐다)
            SinButtons("dev-floor-elements", (element, name) =>
            {
                if (Run == null) { Say("등반 중에만 바꿀 수 있습니다"); return; }
                game.Combat.DevSetFloorElement(Run, element);
                game.arena.BeginRound(Run);   // 바닥 색조, 속성 장식 다시 깔기
                Say($"층 속성: {name}");
            });
            // 23차: 죄종 층으로 바로 가기 (새 맵 1라운드), 미래 보기 구슬을 죄종마다
            SinButtons("dev-sin-floors", (element, name) => { game.DevGoToSin(element); Say($"{name} 층 1라운드로"); });
            // 23차-boss7: 죄종 보스 바로 싸우기, 2페이즈 전환 바로 보기
            SinButtons("dev-sin-bosses", (element, name) =>
            {
                game.DevGoToSin(element);
                if (Run != null) game.DevJump(r => game.Combat.DevGoToRound(r, RoundKind.Boss));
                Say($"{name} 보스");
            });
            Bind("dev-boss-phase", () => Say(Run != null && game.Combat.DevBossPhase(Run) ? "보스 2페이즈 전환" : "1페이즈 칠죄종 보스가 없습니다"));
            SinButtons("dev-fate-sins", (element, name) => Say(game.DevShowFate(element) ?? $"미래 보기: {name}"));

            VisualElement floorRow = panel.Q("dev-floors");
            if (floorRow != null)
            {
                floorRow.Clear();
                for (int f = 1; f <= Db.arena.finalFloor; f++)
                {
                    var button = (Button)setButton.Instantiate().Q("set");
                    button.Q("dot").AddToClassList("dev-floor-dot");
                    button.Q<Label>("label").text = $"{f}층";
                    int floor = f;
                    button.clicked += () => Jump(r => game.Combat.DevGoToFloor(r, floor), $"{floor}층 1라운드로");
                    floorRow.Add(button);
                }
            }

            VisualElement sets = panel.Q("dev-sets");
            sets.Clear();
            foreach (SetDef set in Db.gear.sets)
            {
                var button = (Button)setButton.Instantiate().Q("set");
                button.Q("dot").style.backgroundColor = set.color;
                button.Q<Label>("label").text = set.name + " +1";
                string id = set.id;
                button.clicked += () => AddSetPiece(id);
                sets.Add(button);
            }
        }

        void Bind(string name, System.Action action)
        {
            Button button = panel.Q<Button>(name);
            if (button != null) button.clicked += action;
        }

        void Say(string text) { flash = text; statusUntil = Time.unscaledTime + 2.5f; }

        // 죄종마다 버튼 하나 (색 점 + 세트 이름, DevSetButton.uxml 복제). click(죄종, 이름)
        void SinButtons(string container, System.Action<Element, string> click)
        {
            VisualElement row = panel.Q(container);
            if (row == null || Db.elements?.defs == null) return;
            row.Clear();
            foreach (ElementDef def in Db.elements.defs)
            {
                SetDef owner = Db.gear.sets.FirstOrDefault(s => s.element == def.element);
                var button = (Button)setButton.Instantiate().Q("set");
                button.Q("dot").style.backgroundColor = owner != null ? owner.color : Db.Tone(def.element).color;
                string name = owner != null ? owner.name : def.element.ToString();
                button.Q<Label>("label").text = name;
                Element element = def.element;
                button.clicked += () => click(element, name);
                row.Add(button);
            }
        }

        void Update()
        {
            Keyboard k = Keyboard.current;
            if (k != null && k.f1Key.wasPressedThisFrame) panel.EnableInClassList("hidden", !panel.ClassListContains("hidden"));
            if (panel.ClassListContains("hidden")) return;
            god.EnableInClassList("on", Run != null && Run.devGod);
            freeze.EnableInClassList("on", Run != null && Run.devFreeze);
            ignoreLocks?.EnableInClassList("on", Unlocks.ignoreLocks);
            if (Time.unscaledTime < statusUntil) { status.text = flash; return; }
            GearStats gear = Run?.gearStats ?? Gear.Stats(Db, Profile);
            string sets = gear.sets.Count == 0 ? "없음" : string.Join(", ", gear.sets.Select(s => $"{s.def.name} {s.tier}단계"));
            string run = Run == null ? "등반 전" : $"{Run.floor}층 {Run.round}R, 체력 {Mathf.Round(Run.hp)}/{Run.maxHp}, 욕망 {Mathf.Round(Run.desire)}, 적 {Run.enemies.Count}";
            status.text = $"{run}\n세트: {sets}, 조각 {Bag.items.Count}개 (판 {Bag.placements.Count}), 파편 {Profile.shards}";
        }

        // 23차: 라운드, 층 옮기기는 GameController.DevJump 가 창을 닫고 새 맵을 깐다.
        void Jump(System.Action<RunState> change, string message)
        {
            if (Run == null) { Say("등반 중에만 쓸 수 있습니다"); return; }
            game.DevJump(change);
            Say(message);
        }

        // 보스 바로 클리어: 보스와 싸우는 중이 아니면 보스 라운드로 옮긴 뒤, 실제 처치와 같은 흐름(처치 연출 → 기억의 파편 → 정화 → 계단, 욕망 창)으로 쓰러뜨린다.
        void BossClear()
        {
            if (Run == null) { Say("등반 중에만 쓸 수 있습니다"); return; }
            if (Run.roundKind != RoundKind.Boss || Run.phase != Phase.Combat || !Run.enemies.Any(e => e.IsBoss))
                game.DevJump(r => game.Combat.DevGoToRound(r, RoundKind.Boss));
            game.Combat.DevEndRound(Run);
            Say("보스 처치");
        }

        // 레전더리 조각, 신화 도안 하나 받기 (아직 없는 종류 중 무작위, 판에는 놓지 않음 → I 재봉 상자에서 놓기)
        void GiveRank(bool mythic)
        {
            if (mythic && Run == null) { Say("신화 도안은 등반 중에만 받습니다 (재봉실에서는 일기에서 고름)"); return; }
            Profile bag = Bag;
            List<int> kinds = mythic
                ? (Db.diary?.entries ?? new DiaryEntry[0]).SelectMany(e => new[] { e.patternKind, e.patternTainted })
                    .Where(k => k >= 0 && k < Db.gear.pieces.Length && !Gear.Owns(bag, k)).Distinct().ToList()
                : Enumerable.Range(0, Db.gear.pieces.Length)
                    .Where(k => Db.gear.pieces[k].cost == LegendaryRank && !Gear.IsPattern(Db, k) && !Gear.Owns(bag, k)).ToList();
            if (kinds.Count == 0) { Say("더 받을 조각이 없습니다"); return; }
            PieceDef def = Gear.AddItem(Db, bag, kinds[Random.Range(0, kinds.Count)]);
            game.DevGearChanged();
            Say($"{def.name} 받음 (I 재봉 상자)");
        }

        // 모든 종류의 조각을 하나씩 (판에는 놓지 않음)
        void GiveAll()
        {
            int given = 0;
            for (int kind = 0; kind < Db.gear.pieces.Length; kind++)
                if (Unlocks.KindOpen(Db, Profile, kind) && !Gear.IsPattern(Db, kind)) { Gear.AddItem(Db, Bag, kind); given++; }   // 잠긴 세트는 '잠금 무시'일 때만
            game.DevGearChanged();
            Say($"조각 {given}종을 하나씩 지급했습니다");
        }

        // 그 세트 조각 하나를 지급하고, 같은 세트 조각과 맞닿는 자리에 바로 놓는다 (세트 단계가 하나씩 오르게)
        void AddSetPiece(string setId)
        {
            int[] kinds = Enumerable.Range(0, Db.gear.pieces.Length).Where(k => Db.gear.pieces[k].set == setId).OrderBy(k => Db.gear.pieces[k].cost).ToArray();
            if (kinds.Length == 0) return;
            if (!Unlocks.IsOpen(Db, Profile, setId)) { Say($"{Db.Set(setId).name} 세트는 아직 잠겨 있습니다 (잠금 무시로 지급)"); return; }
            Profile bag = Bag;
            int owned = bag.items.Count(i => Db.gear.pieces[i.kind].set == setId);
            PieceDef def = Gear.AddItem(Db, bag, kinds[owned % kinds.Length]);
            OwnedItem item = bag.items[^1];
            var (width, height, _) = Gear.Dimensions(Db, bag);
            var mine = Gear.Placed(Db, bag).Where(p => p.def.set == setId).SelectMany(p => p.cells).ToHashSet();
            (int x, int y, int turns, int score)? best = null;
            for (int turns = 0; turns < 4; turns++)
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        if (!Gear.CanPlace(Db, bag, item, turns, x, y)) continue;
                        int touch = Gear.Shape(Db, item, turns).Select(c => c + new Vector2Int(x, y))
                            .Count(c => mine.Contains(c + Vector2Int.up) || mine.Contains(c + Vector2Int.down) || mine.Contains(c + Vector2Int.left) || mine.Contains(c + Vector2Int.right));
                        if (best == null || touch > best.Value.score) best = (x, y, turns, touch);
                    }
            if (best is { } b) bag.placements.Add(new Placement { id = item.id, x = b.x, y = b.y, turns = b.turns });
            game.DevGearChanged();
            Say(best != null ? $"{def.name} 장착" : $"{def.name} 지급 (판에 자리가 없음)");
        }
    }
}
