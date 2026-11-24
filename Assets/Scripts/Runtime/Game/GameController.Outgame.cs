// 15차-outgame-unlock: 세트 해금 알림 (GameController 의 나머지 부분). 17차: 시작 조각은 재봉 상자 판에서 고른다(GameController.cs).
using System.Collections.Generic;

namespace TOD
{
    public partial class GameController
    {
        // 로비(재봉실 허브)로 돌아올 때: 새로 열린 세트가 있으면 하나씩 알린다. 본 해금은 Profile 에 남겨 다시 뜨지 않는다.
        void ShowUnlocks()
        {
            List<SetDef> fresh = Unlocks.Fresh(db, Profile);
            if (fresh.Count == 0) return;
            Unlocks.MarkSeen(Profile, fresh);
            Gear.Save(db, Profile);
            ui.ShowUnlockNotice(fresh, _ => sound.Play(db.gear.unlockSound));
        }

        // 개발자 모드: 새 해금이 있으면 그대로 알리고, 없으면 층으로 열리는 세트 전부를 미리보기로 띄운다(기록 안 함).
        public void DevShowUnlocks()
        {
            if (Unlocks.Fresh(db, Profile).Count > 0) { ShowUnlocks(); return; }
            var all = new List<SetDef>();
            foreach (SetDef s in db.gear.sets) if (Unlocks.Floor(db, s.id) > 0) all.Add(s);
            ui.ShowUnlockNotice(all, _ => sound.Play(db.gear.unlockSound));
        }
    }
}
