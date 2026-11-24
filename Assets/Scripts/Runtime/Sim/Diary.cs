// 15차 아버지의 일기 판정 (Unity 오브젝트 무관). 저장은 Profile.floorChoices / diaryRead / diaryNoticed.
using System.Collections.Generic;
using System.Linq;

namespace TOD
{
    public enum DiaryChoice { None = 0, Absorbed = 1, Refused = 2 }

    public static class Diary
    {
        // 풀린 조각 = profile.best(처음 돌파한 최고 층) 이하 층의 조각, 층 순서
        public static List<DiaryEntry> Unlocked(GameDatabase db, Profile profile) =>
            All(db).Where(e => IsUnlocked(profile, e)).ToList();

        public static IEnumerable<DiaryEntry> All(GameDatabase db) =>
            (db.diary?.entries ?? new DiaryEntry[0]).OrderBy(e => e.floor);

        public static bool IsUnlocked(Profile profile, DiaryEntry entry) => entry.floor <= profile.best;

        public static bool IsNew(Profile profile, DiaryEntry entry) => IsUnlocked(profile, entry) && !profile.diaryRead.Contains(entry.floor);

        // 처음 읽으면 true: 17차 보상(기억의 파편 readShards)을 준다. 도안은 '읽은 쪽지'로 판정하므로 따로 줄 것이 없다(PatternOpen).
        public static bool MarkRead(Profile profile, DiaryEntry entry)
        {
            if (profile.diaryRead.Contains(entry.floor)) return false;
            profile.diaryRead.Add(entry.floor);
            profile.shards += System.Math.Max(0, entry.readShards);
            return true;
        }

        // 17차 아버지의 도안: 그 도안을 주는 쪽지를 읽었으면 상인 단추에게서 살 수 있다.
        public static bool PatternOpen(GameDatabase db, Profile profile, int kind) =>
            All(db).Any(e => e.patternKind == kind && kind >= 0 && profile.diaryRead.Contains(e.floor));

        // 풀린(쪽지를 읽은) 도안 종류, 층 순서
        public static List<int> OpenPatterns(GameDatabase db, Profile profile) =>
            All(db).Where(e => e.patternKind >= 0 && e.patternKind < db.gear.pieces.Length && profile.diaryRead.Contains(e.floor)).Select(e => e.patternKind).ToList();

        // 보스 욕망 흡수/거절 기록. 같은 층을 다시 깨면 가장 최근 선택으로 덮는다.
        public static void RecordChoice(Profile profile, int floor, bool absorbed)
        {
            if (floor < 1) return;
            while (profile.floorChoices.Count < floor) profile.floorChoices.Add((int)DiaryChoice.None);
            profile.floorChoices[floor - 1] = (int)(absorbed ? DiaryChoice.Absorbed : DiaryChoice.Refused);
        }

        public static DiaryChoice Choice(Profile profile, int floor) =>
            floor >= 1 && floor <= profile.floorChoices.Count ? (DiaryChoice)profile.floorChoices[floor - 1] : DiaryChoice.None;

        // 일기 마지막 줄: 흡수/거절에 따라 달라지고, 아직 고르지 않았으면 번진 줄
        public static string LastLine(GameDatabase db, Profile profile, DiaryEntry entry) => Choice(profile, entry.floor) switch
        {
            DiaryChoice.Absorbed => entry.absorbedLine,
            DiaryChoice.Refused => entry.refusedLine,
            _ => db.diary.undecidedLine,
        };

        // 아직 알리지 않은 새 조각 수 (알리면 diaryNoticed 를 올린다)
        public static int PendingNotice(GameDatabase db, Profile profile) => System.Math.Max(0, Unlocked(db, profile).Count - profile.diaryNoticed);

        public static int TakeNotice(GameDatabase db, Profile profile)
        {
            int count = PendingNotice(db, profile);
            profile.diaryNoticed = Unlocked(db, profile).Count;
            return count;
        }
    }
}
