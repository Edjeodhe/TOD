// 영구 저장 프로필과 재봉 상자 계산 (웹판 gear.js).
// 17차: Profile 은 영구 저장만 맡는다(파편, 강화, 일기, 시작 조각 items 와 등반 전 배치 placements).
// 등반은 Gear.RunBag 이 만든 임시 Profile(RunState.bag)을 쓰고, 귀환하면 버린다.
// 19차: 시작 조각은 영구가 아니다. 상인 단추에게 파편을 내고 '챙긴' 조각(items, 모두 판 위)은 다음 등반 한 번만 들고 가고 등반을 시작하면 사라진다.
// 모이라 강화는 위치를 고른다: 확장 = 판 가장자리에 줄 하나(extraCols/extraRows), 가로·세로 줄 강화 = 고른 줄 번호(rowLines/colLines).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOD
{
    [Serializable] public class OwnedItem { public int id, kind; }
    [Serializable] public class Placement { public int id, x, y, turns; }
    public enum StashSort { Grade, Set, Acquired }   // 21차 재봉 상자 목록 정렬: 등급순(기본), 속성순, 획득순

    [Serializable]
    public class Profile
    {
        public const int CurrentVersion = 4;   // 22차: 칠죄종으로 조각을 모두 새로 만들어 옛 조각 번호를 버린다
        public int version = CurrentVersion;
        public int shards, best, capacity;
        public int expansion;                     // 19차: 확장한 횟수 (한 번 = 줄 하나). 등급 합 한도도 이만큼 는다
        public int extraCols, extraRows;          // 19차: 확장으로 붙인 세로 줄(폭), 가로 줄(높이) 수
        public List<int> rowLines = new();        // 19차: 강화한 가로 줄 번호 (위에서 0)
        public List<int> colLines = new();        // 19차: 강화한 세로 줄 번호 (왼쪽에서 0)
        public int rows, cols;                    // (19차부터 안 씀) 옛 저장의 가로/세로 줄 강화 단계. 불러올 때 rowLines/colLines 로 옮긴다
        public List<OwnedItem> items = new();
        public List<Placement> placements = new();
        public StashSort stashSort;               // 21차: 재봉 상자 목록 정렬 기준 (마지막으로 고른 것)
        public int nextId;
        // 15차-outgame-unlock: 알림을 본 해금 세트 id, 다락 '시작 조각' 강화 단계와 고른 조각 종류
        public List<string> seenUnlocks = new();
        public int starter, starterKind = -1;   // starter = 17차 '시작 조각 선택' 단계(들고 갈 칸 수, 19차부터 상인 단추). starterKind 는 15차 흔적(안 씀)
        // 15차 아버지의 일기 (Diary.cs): 층 번호 - 1 자리에 보스 욕망 선택(0 아직, 1 흡수, 2 거절), 읽은 조각의 층 번호, 알린 조각 수
        public List<int> floorChoices = new();
        public List<int> diaryRead = new();
        public int diaryNoticed;
        // 19차 모이라의 '운명 읽기': 다음 등반 1층 속성 (0 = 아직 안 읽음, 아니면 Element + 1). 등반을 시작하면 쓰고 비운다
        public int fate;
        // 15차 재봉실 허브: 상인 단추 진열 (조각 종류, HubShop.cs). 19차: 다음 등반에 챙길 수 있는 조각
        public List<int> hubStock = new();
        // 20차: 진열을 뽑은 실제 시각(유닉스 초)과 이 진열에서 고른 조각 종류. hub.shopRefreshHours 가 지나면 새로 뽑고 비운다
        public long hubStockTime;
        public List<int> hubTaken = new();
        // 22차 신화: 다음 등반에 들고 갈 아버지의 도안 (일기 장의 층 번호, 0 = 안 고름). Mythic.cs
        public int mythicPick;
        // 23차 쉬어가기 NPC: 만난 NPC(NpcKind 번호), 이번 등반 사이에 재봉실 선물을 이미 준 NPC(등반이 끝나면 비움), 다음 등반에 걸 재봉실 축복
        public List<int> npcMet = new();
        public List<int> npcGifted = new();
        public List<NpcBoon> npcBoons = new();
    }

    public class PlacedPiece
    {
        public Placement placement;
        public PieceDef def;
        public List<Vector2Int> cells;
        public int Id => placement.id;
    }

    public class SetGroup { public int size, threshold, tier; }

    // 켜진 세트 (단계 1 이상). 16차: 메인 세트(판에 놓인 조각 수가 가장 많은 것)가 맨 앞.
    // count = 판에 놓인 그 세트 조각 수, size = 가장 큰 연결 덩어리 조각 수, order = gear.sets 순서
    public class ActiveSet { public SetDef def; public int tier, size, count, order; }

    public class GearStats
    {
        public readonly Dictionary<StatKind, float> stats = new();
        public readonly Dictionary<string, SetGroup> groups = new();
        public readonly List<ActiveSet> sets = new();
        public readonly List<PieceDef> effects = new();   // 장착한 조각의 고유 효과
        public float Effect(ItemEffect kind) { float v = 0; foreach (PieceDef d in effects) if (d.effect == kind) v += d.effectValue; return v; }
        public float Param(ItemEffect kind) { foreach (PieceDef d in effects) if (d.effect == kind) return d.effectParam; return 0; }
        public bool Has(ItemEffect kind) { foreach (PieceDef d in effects) if (d.effect == kind) return true; return false; }
        public float this[StatKind kind] => stats.TryGetValue(kind, out float v) ? v : 0;
    }

    public static class Gear
    {
        // 21차 재봉 상자 목록 순서: 판에 놓인 조각이 항상 먼저, 그 안에서 고른 기준.
        // 등급순 = 높은 등급 → 세트 → 획득, 속성순 = 세트 목록 순서(화염, 얼음, 번개, 빛, 어둠, 피, 바람, 독) → 높은 등급 → 획득, 획득순 = 얻은 순서(items 순서)
        public static List<OwnedItem> SortStash(GameDatabase db, IList<OwnedItem> items, Func<OwnedItem, bool> placed, StashSort sort)
        {
            int SetIndex(OwnedItem i) { int k = Array.FindIndex(db.gear.sets, s => s.id == Def(db, i).set); return k < 0 ? int.MaxValue : k; }
            int Got(OwnedItem i) => items.IndexOf(i);
            var first = items.OrderBy(i => placed(i) ? 0 : 1);
            return (sort switch
            {
                StashSort.Set => first.ThenBy(SetIndex).ThenByDescending(i => Def(db, i).cost).ThenBy(Got),
                StashSort.Acquired => first.ThenBy(Got),
                _ => first.ThenByDescending(i => Def(db, i).cost).ThenBy(SetIndex).ThenBy(Got),
            }).ToList();
        }

        public static Profile Load(GameDatabase db)
        {
            try
            {
                string json = PlayerPrefs.GetString(db.gear.saveKey, "");
                if (!string.IsNullOrEmpty(json))
                {
                    Profile saved = JsonUtility.FromJson<Profile>(json);
                    if (saved is { version: >= 1 and <= Profile.CurrentVersion } && saved.items != null && saved.placements != null)
                    {
                        Profile loaded = DropInvalidPlacements(db, Dedupe(db, Migrate(db, saved)));
                        Mythic.Ensure(db, loaded);
                        return loaded;
                    }
                }
            }
            catch (Exception error) { Debug.LogWarning($"[TOD] profile reset: {error.Message}"); }
            return new Profile { nextId = 1 };
        }

        // 옛 저장 옮기기. 17차 이전(version 1)과 17~18차(version 2)의 조각은 영구 시작 조각이었다 → 19차에는 모두 버린다(돌려주는 파편 없음).
        // 강화: 가로/세로 줄 단계 n → 위(왼쪽)부터 2n 줄, 확장 단계 e → 오른쪽과 아래에 줄 e 개씩 (확장 횟수 2e).
        static Profile Migrate(GameDatabase db, Profile profile)
        {
            if (profile.version >= Profile.CurrentVersion) return profile;
            profile.items.Clear();
            profile.placements.Clear();
            profile.hubStock?.Clear();
            profile.hubTaken?.Clear();
            profile.fate = 0;
            profile.rowLines ??= new List<int>();
            profile.colLines ??= new List<int>();
            if (profile.extraCols == 0 && profile.extraRows == 0 && profile.expansion > 0)
            {
                profile.extraCols = profile.extraRows = profile.expansion;
                profile.expansion *= 2;
            }
            var (width, height, _) = Dimensions(db, profile);
            for (int y = 0; y < Math.Min(profile.rows * 2, height); y++) if (!profile.rowLines.Contains(y)) profile.rowLines.Add(y);
            for (int x = 0; x < Math.Min(profile.cols * 2, width); x++) if (!profile.colLines.Contains(x)) profile.colLines.Add(x);
            profile.rows = profile.cols = 0;
            profile.version = Profile.CurrentVersion;
            return profile;
        }

        // 등반용 임시 가방: 강화 단계는 영구 프로필과 같고, 조각은 챙긴 시작 조각 전부(판에 놓은 것은 같은 자리, 20차: 안 놓은 것은 가방 목록에).
        // 등반 중 얻은 조각과 배치는 여기에만 쌓이고, 귀환하면 버린다. 파편, 최고 층, 일기는 영구 프로필(RunState.profile)에 쓴다.
        public static Profile RunBag(GameDatabase db, Profile profile)
        {
            var bag = new Profile
            {
                shards = 0, best = profile.best, expansion = profile.expansion, extraCols = profile.extraCols, extraRows = profile.extraRows,
                rowLines = new List<int>(profile.rowLines ?? new List<int>()), colLines = new List<int>(profile.colLines ?? new List<int>()),
                capacity = profile.capacity, starter = profile.starter, nextId = profile.nextId,
                seenUnlocks = new List<string>(profile.seenUnlocks ?? new List<string>()),
            };
            foreach (OwnedItem item in profile.items) bag.items.Add(new OwnedItem { id = item.id, kind = item.kind });
            foreach (Placement p in profile.placements)
                if (profile.items.Exists(i => i.id == p.id)) bag.placements.Add(new Placement { id = p.id, x = p.x, y = p.y, turns = p.turns });
            return bag;
        }

        // 17차 아버지의 도안: 일기로만 풀리는 전용 시작 조각 (탑 보상, 상점 무작위 진열에는 나오지 않는다)
        public static bool IsPattern(GameDatabase db, int kind) => kind >= 0 && db.diary?.entries != null && System.Array.Exists(db.diary.entries, e => e.patternKind == kind || e.patternTainted == kind);

        public static void Save(GameDatabase db, Profile profile)
        {
            PlayerPrefs.SetString(db.gear.saveKey, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        public static PieceDef Def(GameDatabase db, OwnedItem item) => db.gear.pieces[item.kind];

        public static (int width, int height, int limit) Dimensions(GameDatabase db, Profile p) =>
            (db.gear.baseColumns + p.extraCols, db.gear.baseRows + p.extraRows, db.gear.baseCostLimit + p.expansion * db.gear.costLimitStep);

        // 19차: 강화한 줄 위의 칸인가 (가로 줄 + 세로 줄, 둘 다면 2)
        public static int Reinforced(Profile p, Vector2Int c) =>
            (p.rowLines != null && p.rowLines.Contains(c.y) ? 1 : 0) + (p.colLines != null && p.colLines.Contains(c.x) ? 1 : 0);

        // 아이템은 모두 고유하다: 같은 종류는 한 번만 가진다 (개수 표시 없음).
        public static bool Owns(Profile profile, int kind) => profile != null && profile.items.Any(i => i.kind == kind);

        // 아직 갖지 않은 종류 (쉬어가기 탑 선택지·상점 조각이 여기서 뽑는다). 15차: 잠긴 세트 조각은 빠진다 (Unlocks). 17차: 아버지의 도안도 빠진다.
        public static List<int> Unowned(GameDatabase db, Profile profile) =>
            Enumerable.Range(0, db.gear.pieces.Length).Where(k => !Owns(profile, k) && Unlocks.KindOpen(db, profile, k) && !IsPattern(db, k)).ToList();

        // 저장된 프로필에 같은 종류가 여럿이면 처음 것만 남긴다 (빠진 것의 배치도 지움).
        static Profile Dedupe(GameDatabase db, Profile profile)
        {
            var seen = new HashSet<int>();
            var removed = new HashSet<int>();
            profile.items.RemoveAll(i =>
            {
                bool drop = i.kind < 0 || i.kind >= db.gear.pieces.Length || !seen.Add(i.kind);
                if (drop) removed.Add(i.id);
                return drop;
            });
            profile.placements.RemoveAll(p => removed.Contains(p.id));
            return profile;
        }

        // 23차 레어도 가중치: 그 조각 등급(cost)의 gear.rankWeights. 표가 없거나 칸이 모자라면 1.
        public static float Weight(GameDatabase db, int kind)
        {
            float[] w = db.gear.rankWeights;
            int cost = db.gear.pieces[kind].cost;
            return w != null && cost >= 0 && cost < w.Length ? Mathf.Max(0, w[cost]) : 1;
        }

        // 후보 중 하나를 레어도 가중치로 뽑는다. 가중치 0 인 조각은 빠진다. 뽑을 것이 없으면 -1.
        public static int PickWeighted(GameDatabase db, IList<int> kinds, Func<float> random)
        {
            if (kinds == null) return -1;
            float total = kinds.Sum(k => Weight(db, k));
            if (total <= 0) return -1;
            float roll = random() * total;
            int last = -1;
            foreach (int k in kinds)
            {
                float w = Weight(db, k);
                if (w <= 0) continue;
                last = k;
                if ((roll -= w) < 0) return k;
            }
            return last;   // 부동소수 오차로 끝까지 왔을 때
        }

        // 중복 없이 count 개 (탑 진열, 상인 단추 진열)
        public static List<int> PickWeighted(GameDatabase db, IEnumerable<int> kinds, int count, Func<float> random)
        {
            var pool = new List<int>(kinds);
            var picked = new List<int>();
            while (picked.Count < count)
            {
                int k = PickWeighted(db, pool, random);
                if (k < 0) break;
                picked.Add(k);
                pool.Remove(k);
            }
            return picked;
        }

        // 종류를 주면 그 종류(이미 가졌으면 그대로 그 정의만 돌려줌), 안 주면 아직 없는 종류 중 하나를 레어도 가중치로. 모두 가졌으면 null.
        // random 을 안 주면 UnityEngine.Random (등반 중에는 r.random 을 넘겨 시드대로 굴린다).
        public static PieceDef AddItem(GameDatabase db, Profile profile, int kind = -1, Func<float> random = null)
        {
            if (kind < 0)
            {
                kind = PickWeighted(db, Unowned(db, profile), random ?? (() => UnityEngine.Random.value));
                if (kind < 0) return null;
            }
            if (Owns(profile, kind)) return db.gear.pieces[kind];
            var item = new OwnedItem { id = profile.nextId++, kind = kind };
            profile.items.Add(item);
            return Def(db, item);
        }

        // 회전 중심 = 데이터의 첫 칸(shape[0]). 돌려도 이 칸은 제자리에 있고(배치 x,y = 이 칸의 판 좌표),
        // 나머지 칸은 이 칸 기준 상대 좌표로 시계 방향 90°씩 돈다. 판 위 아이콘도 이 칸 슬롯 가운데에 놓인다.
        public static List<Vector2Int> Shape(GameDatabase db, OwnedItem item, int turns) => Rotate(Def(db, item).shape, turns);

        public static List<Vector2Int> Rotate(Vector2Int[] shape, int turns)
        {
            Vector2Int pivot = shape[0];
            List<Vector2Int> cells = shape.Select(c => c - pivot).ToList();
            for (int step = 0; step < ((turns % 4) + 4) % 4; step++) cells = cells.Select(c => new Vector2Int(-c.y, c.x)).ToList();
            return cells;
        }

        // 불러온 배치 중 판 밖·겹침·등급 합 초과인 것은 뺀다 (조각 모양·회전 기준이 바뀐 옛 저장 대비).
        static Profile DropInvalidPlacements(GameDatabase db, Profile profile)
        {
            var (width, height, limit) = Dimensions(db, profile);
            var used = new HashSet<Vector2Int>();
            int cost = 0;
            profile.placements.RemoveAll(p =>
            {
                OwnedItem item = profile.items.Find(i => i.id == p.id);
                if (item == null) return true;
                List<Vector2Int> cells = Shape(db, item, p.turns).Select(c => c + new Vector2Int(p.x, p.y)).ToList();
                bool bad = cost + Unlocks.Cost(db, profile, item) > limit || cells.Any(c => c.x < 0 || c.y < 0 || c.x >= width || c.y >= height || used.Contains(c));
                if (bad) return true;
                cost += Unlocks.Cost(db, profile, item);
                foreach (Vector2Int c in cells) used.Add(c);
                return false;
            });
            return profile;
        }

        public static List<PlacedPiece> Placed(GameDatabase db, Profile profile)
        {
            var list = new List<PlacedPiece>();
            foreach (Placement placement in profile.placements)
            {
                OwnedItem item = profile.items.Find(i => i.id == placement.id);
                if (item == null) continue;
                list.Add(new PlacedPiece
                {
                    placement = placement, def = Def(db, item),
                    cells = Shape(db, item, placement.turns).Select(c => new Vector2Int(c.x + placement.x, c.y + placement.y)).ToList(),
                });
            }
            return list;
        }

        public static bool CanPlace(GameDatabase db, Profile profile, OwnedItem item, int turns, int x, int y)
        {
            var (width, height, limit) = Dimensions(db, profile);
            List<PlacedPiece> occupied = Placed(db, profile).Where(p => p.Id != item.id).ToList();
            if (occupied.Sum(p => Unlocks.Cost(db, profile, p)) + Unlocks.Cost(db, profile, item) > limit) return false;   // 시작 조각은 0
            foreach (Vector2Int c in Shape(db, item, turns))
            {
                int col = x + c.x, row = y + c.y;
                if (col < 0 || row < 0 || col >= width || row >= height) return false;
                if (occupied.Any(p => p.cells.Contains(new Vector2Int(col, row)))) return false;
            }
            return true;
        }

        public static GearStats Stats(GameDatabase db, Profile profile)
        {
            var result = new GearStats();
            foreach (StatKind kind in Enum.GetValues(typeof(StatKind))) result.stats[kind] = 0;
            List<PlacedPiece> pieces = Placed(db, profile);
            foreach (PlacedPiece piece in pieces)
            {
                // 19차: 고른 가로 줄 + 고른 세로 줄 위 칸 비율만큼 +rowBonus. 둘 다 걸친 칸은 둘 다 받는다.
                float reinforced = piece.cells.Sum(c => Reinforced(profile, c)) / (float)piece.cells.Count;
                result.stats[piece.def.stat] += piece.def.value * (1 + reinforced * db.gear.rowBonus);
            }
            foreach (SetDef set in db.gear.sets)
            {
                // 22차 신화: 이 세트 조각에 맞닿은 도안은 이 세트 조각 1개로 친다 (맞닿은 세트마다)
                List<PlacedPiece> own = pieces.Where(p => p.def.set == set.id && !Mythic.IsMythic(p.def)).ToList();
                List<PlacedPiece> wild = pieces.Where(p => Mythic.IsMythic(p.def) && own.Any(o => Touches(o, p))).ToList();
                var remaining = new HashSet<PlacedPiece>(own.Concat(wild));
                int size = 0;
                while (remaining.Count > 0)
                {
                    PlacedPiece first = remaining.First();
                    var cluster = new List<PlacedPiece> { first };
                    remaining.Remove(first);
                    for (int i = 0; i < cluster.Count; i++)
                    {
                        foreach (PlacedPiece other in remaining.ToList())
                        {
                            bool adjacent = Touches(cluster[i], other);
                            if (adjacent) { cluster.Add(other); remaining.Remove(other); }
                        }
                    }
                    size = Mathf.Max(size, cluster.Count);
                }
                int[] tiers = set.thresholds.Where(t => t <= size).ToArray();
                result.groups[set.id] = new SetGroup { size = size, threshold = tiers.Length > 0 ? tiers[^1] : 0, tier = tiers.Length };
                // 12차: 세트는 능력치 대신 속성(피해 + 부가 효과)을 준다 → Combat 이 r.sets 로 읽는다
                if (tiers.Length > 0)
                    result.sets.Add(new ActiveSet { def = set, tier = tiers.Length, size = size, count = own.Count + wild.Count, order = Array.IndexOf(db.gear.sets, set) });
            }
            // 장착한 조각의 고유 효과 (같은 효과가 여럿이면 값을 더한다)
            foreach (PlacedPiece piece in pieces)
                if (piece.def.effect != ItemEffect.None) result.effects.Add(piece.def);
            result.sets.Sort(MainFirst);
            return result;
        }

        static bool Touches(PlacedPiece a, PlacedPiece b) => a.cells.Any(p => b.cells.Any(q => Mathf.Abs(p.x - q.x) + Mathf.Abs(p.y - q.y) == 1));

        // 16차 메인 세트 순서: 판에 놓인 조각 수 → 단계 → 연결 조각 수 → 세트 순서
        public static int MainFirst(ActiveSet a, ActiveSet b)
        {
            int c = b.count.CompareTo(a.count);
            if (c == 0) c = b.tier.CompareTo(a.tier);
            if (c == 0) c = b.size.CompareTo(a.size);
            if (c == 0) c = a.order.CompareTo(b.order);
            return c;
        }

        // 메인 세트(sets[0])가 실의 속성(모습)이 된다. 바늘·당기기·결박 폭발이 모두 이 속성이다.
        public static Element ThreadElement(GearStats gear) => gear.sets.Count > 0 ? gear.sets[0].def.element : Element.Plain;
    }
}
