// 영구 저장 프로필과 재봉 상자 계산 (웹판 gear.js).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOD
{
    [Serializable] public class OwnedItem { public int id, kind; }
    [Serializable] public class Placement { public int id, x, y, turns; }

    [Serializable]
    public class Profile
    {
        public int version = 1;
        public int shards, best, expansion, rows, capacity;
        public List<OwnedItem> items = new();
        public List<Placement> placements = new();
        public int nextId;
    }

    public class PlacedPiece
    {
        public Placement placement;
        public PieceDef def;
        public List<Vector2Int> cells;
        public int Id => placement.id;
    }

    public class SetGroup { public int size, threshold, tier; }

    public class GearStats
    {
        public readonly Dictionary<StatKind, float> stats = new();
        public readonly Dictionary<string, SetGroup> groups = new();
        public float this[StatKind kind] => stats.TryGetValue(kind, out float v) ? v : 0;
    }

    public static class Gear
    {
        public static Profile Load(GameDatabase db)
        {
            try
            {
                string json = PlayerPrefs.GetString(db.gear.saveKey, "");
                if (!string.IsNullOrEmpty(json))
                {
                    Profile saved = JsonUtility.FromJson<Profile>(json);
                    if (saved is { version: 1 } && saved.items != null && saved.placements != null) return saved;
                }
            }
            catch (Exception error) { Debug.LogWarning($"[TOD] profile reset: {error.Message}"); }
            var profile = new Profile();
            foreach (int kind in db.gear.startingKinds) profile.items.Add(new OwnedItem { id = ++profile.nextId, kind = kind });
            profile.nextId++;
            return profile;
        }

        public static void Save(GameDatabase db, Profile profile)
        {
            PlayerPrefs.SetString(db.gear.saveKey, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        public static PieceDef Def(GameDatabase db, OwnedItem item) => db.gear.pieces[item.kind];

        public static (int width, int height, int limit) Dimensions(GameDatabase db, Profile p) =>
            (db.gear.baseColumns + p.expansion, db.gear.baseRows + p.expansion, db.gear.baseCostLimit + p.expansion * db.gear.costLimitStep);

        public static PieceDef AddItem(GameDatabase db, Profile profile, int kind = -1)
        {
            if (kind < 0) kind = UnityEngine.Random.Range(0, db.gear.pieces.Length);
            var item = new OwnedItem { id = profile.nextId++, kind = kind };
            profile.items.Add(item);
            return Def(db, item);
        }

        public static List<Vector2Int> Shape(GameDatabase db, OwnedItem item, int turns)
        {
            List<Vector2Int> shape = Def(db, item).shape.ToList();
            for (int step = 0; step < turns; step++)
            {
                int bottom = shape.Max(c => c.y);
                shape = shape.Select(c => new Vector2Int(bottom - c.y, c.x)).ToList();
            }
            return shape;
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
            if (occupied.Sum(p => p.def.cost) + Def(db, item).cost > limit) return false;
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
                float reinforced = piece.cells.Count(c => c.y < profile.rows * 2) / (float)piece.cells.Count;
                result.stats[piece.def.stat] += piece.def.value * (1 + reinforced * db.gear.rowBonus);
            }
            foreach (SetDef set in db.gear.sets)
            {
                var remaining = new HashSet<PlacedPiece>(pieces.Where(p => p.def.set == set.id));
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
                            bool adjacent = cluster[i].cells.Any(a => other.cells.Any(b => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1));
                            if (adjacent) { cluster.Add(other); remaining.Remove(other); }
                        }
                    }
                    size = Mathf.Max(size, cluster.Count);
                }
                int[] tiers = set.thresholds.Where(t => t <= size).ToArray();
                result.groups[set.id] = new SetGroup { size = size, threshold = tiers.Length > 0 ? tiers[^1] : 0, tier = tiers.Length };
                result.stats[set.stat] += tiers.Length * set.perTier;
            }
            return result;
        }

        // 재봉 상자에서 가장 높은 단계의 세트가 실의 속성이 된다 (동률이면 연결 크기).
        public static Element ThreadElement(GearStats gear)
        {
            Element best = Element.Plain;
            int score = 0;
            foreach ((string id, Element element) in new[] { ("chaos", Element.Chaos), ("despair", Element.Despair), ("frustration", Element.Frustration) })
            {
                if (!gear.groups.TryGetValue(id, out SetGroup group) || group.tier == 0) continue;
                int value = group.tier * 100 + group.size;
                if (value > score) { best = element; score = value; }
            }
            return best;
        }
    }
}
