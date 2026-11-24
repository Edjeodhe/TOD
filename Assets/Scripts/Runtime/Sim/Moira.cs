// 19차 다락의 모이라: 강화 4종(인벤토리 확장, 가로 줄 강화, 세로 줄 강화, 욕망 최대치)의 계산. Unity 객체 무관, 테스트 대상.
// 확장, 가로 줄, 세로 줄은 재봉 상자 판에서 위치를 고른다(pick):
//   확장 = 판 가장자리 하나(0 위, 1 오른쪽, 2 아래, 3 왼쪽)에 줄 하나를 붙인다. 위나 왼쪽이면 놓인 조각과 강화한 줄 번호가 한 칸씩 밀린다.
//   가로 줄 = 줄 번호(위에서 0), 세로 줄 = 줄 번호(왼쪽에서 0). 한 번에 한 줄.
// 시작 조각 들고 갈 수(starter)는 상인 단추로 옮겼지만 값 계산은 여기서 같이 한다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace TOD
{
    public static class Moira
    {
        public const string Expansion = "expansion", Rows = "rows", Cols = "cols", Capacity = "capacity";
        public const int Top = 0, Right = 1, Bottom = 2, Left = 3;

        // 모이라가 맡는 강화 (데이터 upgrades 중 이 key 들, 데이터 순서대로 보여 준다)
        public static readonly string[] Keys = { Expansion, Rows, Cols, Capacity };

        public static bool IsMoira(UpgradeDef u) => u != null && Array.IndexOf(Keys, u.key) >= 0;

        // 판에서 위치를 골라야 하는 강화
        public static bool NeedsPick(string key) => key is Expansion or Rows or Cols;

        public static int Level(Profile p, string key) => key switch
        {
            Expansion => p.expansion,
            Rows => p.rowLines?.Count ?? 0,
            Cols => p.colLines?.Count ?? 0,
            Unlocks.StarterKey => p.starter,
            _ => p.capacity,
        };

        public static int Cost(Profile p, UpgradeDef u) => u.price + u.priceStep * Level(p, u.key);

        // 최대 단계. 가로/세로 줄은 판의 줄 수가 한도이고, 데이터 max 가 0 이상이면 그것과 더 작은 쪽.
        public static int Max(GameDatabase db, Profile p, UpgradeDef u)
        {
            var (width, height, _) = Gear.Dimensions(db, p);
            int lines = u.key == Rows ? height : u.key == Cols ? width : -1;
            if (lines < 0) return u.max;
            return u.max >= 0 ? Math.Min(u.max, lines) : lines;
        }

        public static bool Maxed(GameDatabase db, Profile p, UpgradeDef u) => Level(p, u.key) >= Max(db, p, u);

        // 판에서 고른 위치가 쓸 수 있는가 (가로/세로 줄: 판 안이고 아직 강화 안 한 줄, 확장: 네 변 중 하나)
        public static bool CanPick(GameDatabase db, Profile p, string key, int pick)
        {
            var (width, height, _) = Gear.Dimensions(db, p);
            return key switch
            {
                Rows => pick >= 0 && pick < height && !(p.rowLines?.Contains(pick) ?? false),
                Cols => pick >= 0 && pick < width && !(p.colLines?.Contains(pick) ?? false),
                Expansion => pick is >= Top and <= Left,
                _ => true,
            };
        }

        // 파편을 내고 강화한다. 못 하면 false (단계 끝, 파편 부족, 고른 위치가 안 됨).
        public static bool Apply(GameDatabase db, Profile p, UpgradeDef u, int pick = -1)
        {
            if (u == null || Maxed(db, p, u) || p.shards < Cost(p, u)) return false;
            if (NeedsPick(u.key) && !CanPick(db, p, u.key, pick)) return false;
            p.shards -= Cost(p, u);
            switch (u.key)
            {
                case Expansion: Expand(p, pick); break;
                case Rows: (p.rowLines ??= new List<int>()).Add(pick); p.rowLines.Sort(); break;
                case Cols: (p.colLines ??= new List<int>()).Add(pick); p.colLines.Sort(); break;
                case Unlocks.StarterKey: p.starter++; break;
                default: p.capacity++; break;
            }
            return true;
        }

        // 판 가장자리에 줄 하나를 붙인다. 위/왼쪽이면 놓인 조각과 강화한 줄이 한 칸씩 밀린다.
        public static void Expand(Profile p, int side)
        {
            p.rowLines ??= new List<int>();
            p.colLines ??= new List<int>();
            p.expansion++;
            switch (side)
            {
                case Top:
                    p.extraRows++;
                    foreach (Placement pl in p.placements) pl.y++;
                    for (int i = 0; i < p.rowLines.Count; i++) p.rowLines[i]++;
                    break;
                case Bottom: p.extraRows++; break;
                case Left:
                    p.extraCols++;
                    foreach (Placement pl in p.placements) pl.x++;
                    for (int i = 0; i < p.colLines.Count; i++) p.colLines[i]++;
                    break;
                default: p.extraCols++; break;
            }
        }

        // 데이터에서 모이라 강화만 (순서 유지)
        public static IEnumerable<UpgradeDef> Upgrades(GameDatabase db) => (db.upgrades ?? new UpgradeDef[0]).Where(IsMoira);
    }
}
