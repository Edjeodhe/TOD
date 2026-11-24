// 22차 신화 등급 = 아버지의 도안 (Unity 오브젝트 무관, 테스트 대상).
// - 층을 처음 돌파해 일기 장이 풀리면 그 장의 도안을 영구히 가진다 (파편 구매 없음, 단추 진열에 없음).
// - 등반마다 하나를 골라 들고 간다 (Profile.mythicPick = 장의 층 번호, 0 = 안 고름). 챙기기 칸 한도와 등급 합 한도에 들지 않는다.
// - 그 층 보스 욕망을 흡수했으면 '붉게 물든 도안'(DiaryEntry.patternTainted), 아니면 보통 도안(patternKind). 가장 최근 선택을 따른다.
// - 판에서는 맞닿은 세트마다 그 세트 조각 1개로 친다 (Gear.Stats).
using System.Collections.Generic;
using System.Linq;

namespace TOD
{
    public static class Mythic
    {
        public const int Rank = 5;

        public static bool IsMythic(GameDatabase db, int kind) =>
            kind >= 0 && kind < db.gear.pieces.Length && (db.gear.pieces[kind].cost >= Rank || Gear.IsPattern(db, kind));

        public static bool IsMythic(PieceDef def) => def != null && def.cost >= Rank;

        // 가진 도안 = 풀린 일기 장 (층 순서)
        public static List<DiaryEntry> Owned(GameDatabase db, Profile profile) =>
            Diary.Unlocked(db, profile).Where(e => e.patternKind >= 0 && e.patternKind < db.gear.pieces.Length).ToList();

        public static bool Tainted(Profile profile, DiaryEntry entry) => Diary.Choice(profile, entry.floor) == DiaryChoice.Absorbed;

        // 그 장의 도안이 지금 어떤 모습인지 (조각 종류)
        public static int KindFor(GameDatabase db, Profile profile, DiaryEntry entry) =>
            Tainted(profile, entry) && entry.patternTainted >= 0 && entry.patternTainted < db.gear.pieces.Length ? entry.patternTainted : entry.patternKind;

        public static DiaryEntry Picked(GameDatabase db, Profile profile) =>
            profile.mythicPick > 0 ? Owned(db, profile).FirstOrDefault(e => e.floor == profile.mythicPick) : null;

        // 고르기/내려놓기: 같은 장을 다시 누르면 내려놓는다. 판의 조각 목록(profile.items)도 맞춘다.
        public static void Toggle(GameDatabase db, Profile profile, int floor)
        {
            profile.mythicPick = profile.mythicPick == floor ? 0 : floor;
            Ensure(db, profile);
        }

        // 영구 프로필의 조각 목록에 고른 도안 하나만 지금 모습으로 둔다 (불러올 때, 재봉실, 등반 시작 전에 부른다).
        // 다른 도안이나 모습이 바뀐 도안은 빼고(배치도), 고른 도안이 없으면 놓지 않은 채로 넣는다.
        public static void Ensure(GameDatabase db, Profile profile)
        {
            DiaryEntry pick = Picked(db, profile);
            if (pick == null) profile.mythicPick = 0;
            int want = pick != null ? KindFor(db, profile, pick) : -1;
            var drop = profile.items.Where(i => IsMythic(db, i.kind) && i.kind != want).Select(i => i.id).ToHashSet();
            profile.items.RemoveAll(i => drop.Contains(i.id));
            profile.placements.RemoveAll(p => drop.Contains(p.id));
            if (want >= 0 && !Gear.Owns(profile, want)) Gear.AddItem(db, profile, want);
        }

        // 등반 가방에 든 붉게 물든 도안의 시작 욕망 합
        public static float DesireStart(GameDatabase db, Profile bag) =>
            bag.items.Where(i => IsMythic(db, i.kind)).Sum(i => db.gear.pieces[i.kind].desireStart);
    }
}
