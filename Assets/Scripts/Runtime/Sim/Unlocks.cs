// 15차 아웃게임: 층 돌파로 세트가 열리는 표, 해금 알림 기록. 17차: 시작 조각 들고 갈 수.
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

        // ---------------------------------------------------------------- 시작 조각 (17차)
        // 시작 조각 = Profile.items. 19차: 상인 단추의 '시작 조각 챙기기'에서 파편으로 산 조각(다음 등반 한 번만).
        // 20차: 한도는 퍼즐 칸 수. 챙길 칸 = gear.starterSlotsBase + '퍼즐 칸 추가'(key starter) 단계 × gear.starterSlotsStep.
        //   예: 5칸이면 레전더리(4칸) 하나 + 일반(1칸) 하나. 넘는 조각은 HubShop.Take 가 막는다.

        public static int StarterCells(GameDatabase db, Profile profile) =>
            Math.Max(0, db.gear.starterSlotsBase + (profile?.starter ?? 0) * db.gear.starterSlotsStep);

        // 등급 합 한도에 드는 값 (17차: 시작 조각도 그대로 든다)
        public static int Cost(GameDatabase db, Profile profile, OwnedItem item) => Mythic.IsMythic(Gear.Def(db, item)) ? 0 : Gear.Def(db, item).cost;   // 22차: 신화 도안은 등급 합에 안 듦

        public static int Cost(GameDatabase db, Profile profile, PlacedPiece piece) => Mythic.IsMythic(piece.def) ? 0 : piece.def.cost;
    }
}
