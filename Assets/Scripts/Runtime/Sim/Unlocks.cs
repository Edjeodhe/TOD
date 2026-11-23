// 15차 아웃게임: 층 돌파로 세트가 열리는 표, 해금 알림 기록, 다락 강화 '시작 조각'.
// 데이터는 GameDatabase(gear.setUnlockFloor, upgrades 의 key "starter")에 있고, 여기는 계산만 한다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOD
{
    // 세트 해금 한 줄: profile.best(처음 돌파한 가장 높은 층)가 floor 이상이면 열린다. 표에 없는 세트는 처음부터 열림.
    [Serializable] public class SetUnlock { public string set; public int floor; }

    public static class Unlocks
    {
        public const string StarterKey = "starter";   // 다락 강화 key

        // 개발자 모드 "잠금 무시" (저장하지 않음). 켜면 잠긴 세트 조각도 탑, 상점, 지급에 나온다.
        public static bool ignoreLocks;

        public static int Floor(GameDatabase db, string setId)
        {
            SetUnlock entry = db.gear.setUnlockFloor?.FirstOrDefault(u => u.set == setId);
            return entry?.floor ?? 0;
        }

        // 실제 해금 여부 (개발자 토글과 무관)
        public static bool Earned(GameDatabase db, Profile profile, string setId) => (profile?.best ?? 0) >= Floor(db, setId);

        public static bool IsOpen(GameDatabase db, Profile profile, string setId) => ignoreLocks || Earned(db, profile, setId);

        public static bool KindOpen(GameDatabase db, Profile profile, int kind) => IsOpen(db, profile, db.gear.pieces[kind].set);

        // 열렸지만 아직 알림을 보지 않은 세트 (처음부터 열린 세트는 알림 없음). gear.sets 순서.
        public static List<SetDef> Fresh(GameDatabase db, Profile profile)
        {
            var seen = profile.seenUnlocks ?? new List<string>();
            return db.gear.sets.Where(s => Floor(db, s.id) > 0 && Earned(db, profile, s.id) && !seen.Contains(s.id)).ToList();
        }

        public static void MarkSeen(Profile profile, IEnumerable<SetDef> sets)
        {
            profile.seenUnlocks ??= new List<string>();
            foreach (SetDef s in sets) if (!profile.seenUnlocks.Contains(s.id)) profile.seenUnlocks.Add(s.id);
        }

        // ---------------------------------------------------------------- 시작 조각

        public static bool IsStarter(Profile profile, OwnedItem item) => profile != null && item != null && profile.starter > 0 && item.kind == profile.starterKind;

        // 등급 합 한도에 드는 값: 시작 조각은 0
        public static int Cost(GameDatabase db, Profile profile, OwnedItem item) => IsStarter(profile, item) ? 0 : Gear.Def(db, item).cost;

        public static int Cost(GameDatabase db, Profile profile, PlacedPiece piece)
        {
            OwnedItem item = profile.items.Find(i => i.id == piece.Id);
            return item != null ? Cost(db, profile, item) : piece.def.cost;
        }

        public static OwnedItem StarterItem(Profile profile) =>
            profile == null || profile.starter <= 0 ? null : profile.items.Find(i => i.kind == profile.starterKind);

        // 그 조각을 놓을 수 있는 자리 중 같은 세트 조각과 가장 많이 맞닿는 곳 (같으면 위 왼쪽 먼저). 없으면 null.
        public static (int x, int y, int turns)? BestSpot(GameDatabase db, Profile profile, OwnedItem item)
        {
            var (width, height, _) = Gear.Dimensions(db, profile);
            string setId = Gear.Def(db, item).set;
            var mine = Gear.Placed(db, profile).Where(p => p.def.set == setId && p.Id != item.id).SelectMany(p => p.cells).ToHashSet();
            (int x, int y, int turns, int score)? best = null;
            for (int turns = 0; turns < 4; turns++)
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        if (!Gear.CanPlace(db, profile, item, turns, x, y)) continue;
                        int touch = Gear.Shape(db, item, turns).Select(c => c + new Vector2Int(x, y))
                            .Count(c => mine.Contains(c + Vector2Int.up) || mine.Contains(c + Vector2Int.down) || mine.Contains(c + Vector2Int.left) || mine.Contains(c + Vector2Int.right));
                        if (best == null || touch > best.Value.score) best = (x, y, turns, touch);
                    }
            return best is { } b ? (b.x, b.y, b.turns) : null;
        }

        // 등반 시작: 시작 조각이 판에 없으면 빈자리에 놓는다. 놓았으면 그 조각 정의, 아니면 null.
        public static PieceDef PlaceStarter(GameDatabase db, Profile profile)
        {
            OwnedItem item = StarterItem(profile);
            if (item == null || profile.placements.Any(p => p.id == item.id)) return null;
            if (BestSpot(db, profile, item) is not { } spot) return null;
            profile.placements.Add(new Placement { id = item.id, x = spot.x, y = spot.y, turns = spot.turns });
            return Gear.Def(db, item);
        }
    }
}
