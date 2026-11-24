// 15차 재봉실 허브: 상인 단추의 진열. Unity 객체 무관, 테스트 대상.
// 19차: 조각을 영구로 사지 않는다. '시작 조각 챙기기'에서 진열된 조각을 파편으로 사서 재봉 상자 판에 바로 놓고(Pack),
// 그 조각은 다음 등반 한 번만 들고 간다(등반을 시작하면 ConsumePacked 로 사라짐). 판에서 빼면 낸 파편을 돌려준다(Unpack).
// 진열 = 무작위 shopStock 개(등반이 끝날 때마다 새로) + 일기로 풀린 '아버지의 도안'. 가격은 등급별 hub.piecePrices.
using System;
using System.Collections.Generic;
using System.Linq;

namespace TOD
{
    public enum PackResult { Packed, NotOffered, NoShards, Full, Blocked }

    public static class HubShop
    {
        public static int Price(GameDatabase db, int kind)
        {
            int[] prices = db.hub?.piecePrices;
            int cost = db.gear.pieces[kind].cost;
            return prices != null && cost >= 0 && cost < prices.Length ? prices[cost] : 0;
        }

        // 새로 진열: 챙기지 않은 조각(잠긴 세트, 도안 제외) 중 shopStock 개를 무작위로
        public static void Restock(GameDatabase db, Profile profile, Func<float> random)
        {
            List<int> pool = Gear.Unowned(db, profile);
            profile.hubStock ??= new List<int>();
            profile.hubStock.Clear();
            int count = Math.Min(db.hub != null ? db.hub.shopStock : 0, pool.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = Math.Min(pool.Count - 1, (int)(random() * pool.Count));
                profile.hubStock.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
        }

        // 진열이 비었으면 채운다 (처음 실행, 옛 저장). 바꿨으면 true.
        public static bool Ensure(GameDatabase db, Profile profile, Func<float> random)
        {
            profile.hubStock ??= new List<int>();
            profile.hubStock.RemoveAll(k => k < 0 || k >= db.gear.pieces.Length);
            if (profile.hubStock.Count > 0 || Gear.Unowned(db, profile).Count == 0) return false;
            Restock(db, profile, random);
            return true;
        }

        // 진열 전체: 무작위 진열 + 풀린 도안
        public static List<int> Display(GameDatabase db, Profile profile)
        {
            var list = new List<int>(profile.hubStock ?? new List<int>());
            foreach (int k in Diary.OpenPatterns(db, profile)) if (!list.Contains(k)) list.Add(k);
            return list;
        }

        // 아직 챙기지 않아 판에 놓을 수 있는 진열 조각
        public static List<int> Offers(GameDatabase db, Profile profile) => Display(db, profile).Where(k => !Gear.Owns(profile, k)).ToList();

        public static bool Offered(GameDatabase db, Profile profile, int kind) => Offers(db, profile).Contains(kind);

        // 챙기기: 진열 조각을 사서 판 (x, y, turns)에 놓는다. 성공하면 Packed 이고 item 에 새 조각.
        public static PackResult Pack(GameDatabase db, Profile profile, int kind, int x, int y, int turns, out OwnedItem item)
        {
            item = null;
            if (!Offered(db, profile, kind)) return PackResult.NotOffered;
            var probe = new OwnedItem { id = -1, kind = kind };
            if (Unlocks.StarterFull(db, profile, probe)) return PackResult.Full;
            if (!Gear.CanPlace(db, profile, probe, turns, x, y)) return PackResult.Blocked;
            int price = Price(db, kind);
            if (profile.shards < price) return PackResult.NoShards;
            profile.shards -= price;
            item = new OwnedItem { id = profile.nextId++, kind = kind };
            profile.items.Add(item);
            profile.placements.Add(new Placement { id = item.id, x = x, y = y, turns = turns });
            return PackResult.Packed;
        }

        // 챙긴 조각을 판에서 빼면 낸 파편을 돌려준다. 돌려준 파편 수 (없으면 -1).
        public static int Unpack(GameDatabase db, Profile profile, int id)
        {
            OwnedItem item = profile.items.Find(i => i.id == id);
            if (item == null) return -1;
            profile.items.Remove(item);
            profile.placements.RemoveAll(p => p.id == id);
            int refund = Price(db, item.kind);
            profile.shards += refund;
            return refund;
        }

        // 등반 시작: 챙긴 조각은 등반 가방(Gear.RunBag)으로 넘어갔으니 영구 프로필에서 지운다.
        public static void ConsumePacked(Profile profile)
        {
            profile.items.Clear();
            profile.placements.Clear();
        }
    }
}
