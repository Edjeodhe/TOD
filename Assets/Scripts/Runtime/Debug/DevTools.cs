// 개발자 모드: F1 로 여닫는 디버그 패널 (Main.uxml dev-panel). 에디터와 개발 빌드에서만 켜진다.
// 버튼 이름 → 동작은 아래 Bind 에서 연결한다. 지급량 같은 값은 인스펙터(MCP)에서 넣는다.
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

        VisualElement panel;
        Label status;
        Button god, freeze;
        float statusUntil;
        string flash;

        GameDatabase Db => game.db;
        RunState Run => game.Run;
        Profile Profile => game.Profile;

        void Start()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) { enabled = false; return; }
            VisualElement root = document.rootVisualElement;
            panel = root.Q("dev-panel");
            if (panel == null) { enabled = false; return; }
            status = panel.Q<Label>("dev-status");
            god = panel.Q<Button>("dev-god");
            freeze = panel.Q<Button>("dev-freeze");

            Bind("dev-god", () => { if (Run != null) Run.devGod = !Run.devGod; });
            Bind("dev-heal", () => { if (Run != null) Run.hp = Run.maxHp; });
            Bind("dev-level", () => { if (Run != null) game.Combat.DevLevelUp(Run); });
            Bind("dev-yarn", () => { if (Run != null) Run.yarn = game.Combat.YarnMax(Run); });
            Bind("dev-gold", () => { if (Run != null) Run.gold += goldAmount; });
            Bind("dev-shards", () => { Profile.shards += shardAmount; game.DevGearChanged(); });
            Bind("dev-desire-up", () => { if (Run != null) game.Combat.AdjustDesire(Run, desireStep); });
            Bind("dev-desire-down", () => { if (Run != null) game.Combat.AdjustDesire(Run, -desireStep); });
            foreach (string type in new[] { "stalker", "charger", "shooter", "boss" })
                Bind($"dev-spawn-{type}", () => { if (Run != null) game.Combat.DevSpawn(Run, type); });
            Bind("dev-bind", () => Say(Run != null && game.Combat.DevBind(Run) ? "가장 가까운 적에게 결박 실을 걸었습니다 (우클릭)" : "적이 없습니다"));
            Bind("dev-kill", () => { if (Run != null) game.Combat.DevKillAll(Run); });
            Bind("dev-end-round", () => { if (Run != null) game.Combat.DevEndRound(Run); });
            Bind("dev-freeze", () => { if (Run != null) Run.devFreeze = !Run.devFreeze; });
            Bind("dev-all-items", GiveAll);
            Bind("dev-clear-board", () => { Profile.placements.Clear(); game.DevGearChanged(); });
            Bind("dev-clear-items", () => { Profile.placements.Clear(); Profile.items.Clear(); game.DevGearChanged(); });

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

        void Update()
        {
            Keyboard k = Keyboard.current;
            if (k != null && k.f1Key.wasPressedThisFrame) panel.EnableInClassList("hidden", !panel.ClassListContains("hidden"));
            if (panel.ClassListContains("hidden")) return;
            god.EnableInClassList("on", Run != null && Run.devGod);
            freeze.EnableInClassList("on", Run != null && Run.devFreeze);
            if (Time.unscaledTime < statusUntil) { status.text = flash; return; }
            GearStats gear = Run?.gearStats ?? Gear.Stats(Db, Profile);
            string sets = gear.sets.Count == 0 ? "없음" : string.Join(", ", gear.sets.Select(s => $"{s.def.name} {s.tier}단계"));
            string run = Run == null ? "등반 전" : $"{Run.floor}층 {Run.round}R, 체력 {Mathf.Round(Run.hp)}/{Run.maxHp}, 욕망 {Mathf.Round(Run.desire)}, 적 {Run.enemies.Count}";
            status.text = $"{run}\n세트: {sets}, 조각 {Profile.items.Count}개 (판 {Profile.placements.Count})";
        }

        // 모든 종류의 조각을 하나씩 (판에는 놓지 않음)
        void GiveAll()
        {
            for (int kind = 0; kind < Db.gear.pieces.Length; kind++) Gear.AddItem(Db, Profile, kind);
            game.DevGearChanged();
            Say($"조각 {Db.gear.pieces.Length}종을 하나씩 지급했습니다");
        }

        // 그 세트 조각 하나를 지급하고, 같은 세트 조각과 맞닿는 자리에 바로 놓는다 (세트 단계가 하나씩 오르게)
        void AddSetPiece(string setId)
        {
            int[] kinds = Enumerable.Range(0, Db.gear.pieces.Length).Where(k => Db.gear.pieces[k].set == setId).OrderBy(k => Db.gear.pieces[k].cost).ToArray();
            if (kinds.Length == 0) return;
            int owned = Profile.items.Count(i => Db.gear.pieces[i.kind].set == setId);
            PieceDef def = Gear.AddItem(Db, Profile, kinds[owned % kinds.Length]);
            OwnedItem item = Profile.items[^1];
            var (width, height, _) = Gear.Dimensions(Db, Profile);
            var mine = Gear.Placed(Db, Profile).Where(p => p.def.set == setId).SelectMany(p => p.cells).ToHashSet();
            (int x, int y, int turns, int score)? best = null;
            for (int turns = 0; turns < 4; turns++)
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        if (!Gear.CanPlace(Db, Profile, item, turns, x, y)) continue;
                        int touch = Gear.Shape(Db, item, turns).Select(c => c + new Vector2Int(x, y))
                            .Count(c => mine.Contains(c + Vector2Int.up) || mine.Contains(c + Vector2Int.down) || mine.Contains(c + Vector2Int.left) || mine.Contains(c + Vector2Int.right));
                        if (best == null || touch > best.Value.score) best = (x, y, turns, touch);
                    }
            if (best is { } b) Profile.placements.Add(new Placement { id = item.id, x = b.x, y = b.y, turns = b.turns });
            game.DevGearChanged();
            Say(best != null ? $"{def.name} 장착" : $"{def.name} 지급 (판에 자리가 없음)");
        }
    }
}
