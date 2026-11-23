// 15차 재봉실 허브: 상인 단추의 조각 상점 (기억의 파편으로 아직 없는 조각을 산다). Unity 객체 무관, 테스트 대상.
using System;
using System.Collections.Generic;

namespace TOD
{
    public static class HubShop
    {
        public static int Price(GameDatabase db, int kind)
        {
            int[] prices = db.hub?.piecePrices;
            int cost = db.gear.pieces[kind].cost;
            return prices != null && cost >= 0 && cost < prices.Length ? prices[cost] : 0;
        }

        // 새로 진열: 아직 없는 조각(잠긴 세트 제외) 중 shopStock 개를 무작위로
        public static void Restock(GameDatabase db, Profile profile, Func<float> random)
        {
            List<int> pool = Gear.Unowned(db, profile);
            profile.hubStock.Clear();
            int count = Math.Min(db.hub != null ? db.hub.shopStock : 0, pool.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = Math.Min(pool.Count - 1, (int)(random() * pool.Count));
                profile.hubStock.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
        }

        // 진열이 비었거나 전부 이미 가진 조각(팔림)이고 더 팔 조각이 있으면 다시 채운다. 바꿨으면 true.
        public static bool Ensure(GameDatabase db, Profile profile, Func<float> random)
        {
            profile.hubStock.RemoveAll(k => k < 0 || k >= db.gear.pieces.Length);
            bool allSold = profile.hubStock.TrueForAll(k => Gear.Owns(profile, k));
            if (!allSold || Gear.Unowned(db, profile).Count == 0) return false;
            Restock(db, profile, random);
            return true;
        }

        public static bool Sold(Profile profile, int kind) => Gear.Owns(profile, kind);

        public static bool CanBuy(GameDatabase db, Profile profile, int kind) =>
            profile.hubStock.Contains(kind) && !Sold(profile, kind) && profile.shards >= Price(db, kind);

        // 사면 조각을 재봉 상자에 넣고 파편을 뺀다. 못 사면 null.
        public static PieceDef Buy(GameDatabase db, Profile profile, int kind)
        {
            if (!CanBuy(db, profile, kind)) return null;
            profile.shards -= Price(db, kind);
            return Gear.AddItem(db, profile, kind);
        }
    }
}
