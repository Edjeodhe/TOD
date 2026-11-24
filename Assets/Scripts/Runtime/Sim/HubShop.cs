// 15차 재봉실 허브: 상인 단추의 진열. Unity 객체 무관, 테스트 대상.
// 19차: 조각을 영구로 사지 않는다. 챙긴 조각은 다음 등반 한 번만 들고 간다(등반을 시작하면 ConsumePacked 로 사라짐).
// 20차: '시작 조각 챙기기' = 증강 고르기처럼 무작위 진열 shopStock 개(일기로 풀린 '아버지의 도안'도 섞임)가 카드로 뜨고,
//   그중 packPicks 개까지 파편을 내고 고른다(Take). 고른 조각은 판에 놓지 않은 채 들어오고, 재봉 상자(I)에서 배치한다.
//   한도는 조각 수가 아니라 퍼즐 칸 수: 챙긴 조각들의 칸 합이 Unlocks.StarterCells 를 넘으면 못 고른다.
//   고른 카드를 다시 누르면 되돌리고 파편을 돌려받는다(Return). 진열은 실제 시각으로 shopRefreshHours 마다 새로 뽑는다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace TOD
{
    public enum PackResult { Packed, NotOffered, NoShards, TooBig, PickLimit, Taken }

    public static class HubShop
    {
        public static int Price(GameDatabase db, int kind)
        {
            int[] prices = db.hub?.piecePrices;
            int cost = db.gear.pieces[kind].cost;
            return prices != null && cost >= 0 && cost < prices.Length ? prices[cost] : 0;
        }

        // 조각 하나가 차지하는 퍼즐 칸 수
        public static int Cells(GameDatabase db, int kind) => db.gear.pieces[kind].shape.Length;

        // 챙긴 조각(판에 놓았든 안 놓았든) 칸 합
        public static int PackedCells(GameDatabase db, Profile profile) => profile.items.Where(i => !Mythic.IsMythic(db, i.kind)).Sum(i => Cells(db, i.kind));   // 22차: 신화 도안은 칸 한도와 따로

        public static long RefreshSeconds(GameDatabase db) => db.hub != null && db.hub.shopRefreshHours > 0 ? (long)(db.hub.shopRefreshHours * 3600) : 0;

        // 새 진열까지 남은 초 (0 이하 = 바뀔 때)
        public static long SecondsLeft(GameDatabase db, Profile profile, long now) => RefreshSeconds(db) <= 0 ? 0 : profile.hubStockTime + RefreshSeconds(db) - now;

        // 새로 진열: 챙기지 않은 조각(잠긴 세트 제외) + 풀린 도안 중 shopStock 개를 무작위로. 고른 기록을 비우고 시각을 적는다.
        public static List<int> Pool(GameDatabase db, Profile profile)
        {
            List<int> pool = Gear.Unowned(db, profile);
            return pool;
        }

        public static void Restock(GameDatabase db, Profile profile, Func<float> random, long now)
        {
            List<int> pool = Pool(db, profile);
            profile.hubStock ??= new List<int>();
            profile.hubTaken ??= new List<int>();
            profile.hubStock.Clear();
            profile.hubTaken.Clear();
            int count = Math.Min(db.hub != null ? db.hub.shopStock : 0, pool.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = Math.Min(pool.Count - 1, (int)(random() * pool.Count));
                profile.hubStock.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
            profile.hubStockTime = now;
        }

        // 진열이 비었거나 시간이 지났으면 새로 뽑는다. 바꿨으면 true.
        public static bool Ensure(GameDatabase db, Profile profile, Func<float> random, long now)
        {
            profile.hubStock ??= new List<int>();
            profile.hubTaken ??= new List<int>();
            profile.hubStock.RemoveAll(k => k < 0 || k >= db.gear.pieces.Length);
            bool due = profile.hubStock.Count == 0 || (RefreshSeconds(db) > 0 && SecondsLeft(db, profile, now) <= 0) || profile.hubStockTime > now;
            if (!due) return false;
            Restock(db, profile, random, now);
            return true;
        }

        // 진열 카드 (고른 것도 그대로 보이고 '챙김' 표시)
        public static List<int> Offers(GameDatabase db, Profile profile) => new(profile.hubStock ?? new List<int>());

        public static bool Taken(Profile profile, int kind) => profile.hubTaken != null && profile.hubTaken.Contains(kind);

        public static int Picks(GameDatabase db) => db.hub != null && db.hub.packPicks > 0 ? db.hub.packPicks : 1;

        // 고르기: 파편을 내고 조각을 챙긴다(판에는 아직 안 놓음). 성공하면 Packed 이고 item 에 새 조각.
        public static PackResult Take(GameDatabase db, Profile profile, int kind, out OwnedItem item)
        {
            item = null;
            if (profile.hubStock == null || !profile.hubStock.Contains(kind)) return PackResult.NotOffered;
            if (Taken(profile, kind) || Gear.Owns(profile, kind)) return PackResult.Taken;
            if ((profile.hubTaken?.Count ?? 0) >= Picks(db)) return PackResult.PickLimit;
            if (PackedCells(db, profile) + Cells(db, kind) > Unlocks.StarterCells(db, profile)) return PackResult.TooBig;
            int price = Price(db, kind);
            if (profile.shards < price) return PackResult.NoShards;
            profile.shards -= price;
            item = new OwnedItem { id = profile.nextId++, kind = kind };
            profile.items.Add(item);
            profile.hubTaken ??= new List<int>();
            profile.hubTaken.Add(kind);
            return PackResult.Packed;
        }

        // 되돌리기: 고른 조각을 내려놓고(판에서도 빠짐) 낸 파편을 돌려받는다. 돌려준 파편 수 (없으면 -1).
        public static int Return(GameDatabase db, Profile profile, int kind)
        {
            OwnedItem item = profile.items.Find(i => i.kind == kind);
            if (item == null) return -1;
            profile.items.Remove(item);
            profile.placements.RemoveAll(p => p.id == item.id);
            profile.hubTaken?.Remove(kind);
            int refund = Price(db, kind);
            profile.shards += refund;
            return refund;
        }

        // 등반 시작: 챙긴 조각은 등반 가방(Gear.RunBag)으로 넘어갔으니 영구 프로필에서 지운다. 고른 기록(hubTaken)은 진열이 바뀔 때까지 남는다.
        public static void ConsumePacked(Profile profile)
        {
            profile.items.Clear();
            profile.placements.Clear();
        }
    }
}
