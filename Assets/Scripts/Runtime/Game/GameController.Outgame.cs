// 15차-outgame-unlock: 세트 해금 알림, 다락 강화 '시작 조각' (GameController 의 나머지 부분).
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

        // 등반 시작 직전: 시작 조각이 판에 없으면 빈자리에 놓는다 (등급 합 한도에 들지 않음).
        void PlaceStarterPiece()
        {
            PieceDef placed = Unlocks.PlaceStarter(db, Profile);
            if (placed == null) return;
            Gear.Save(db, Profile);
            ui.Say(string.Format(db.gear.starterPlaced, placed.name));
        }

        // 개발자 모드: 새 해금이 있으면 그대로 알리고, 없으면 층으로 열리는 세트 전부를 미리보기로 띄운다(기록 안 함).
        public void DevShowUnlocks()
        {
            if (Unlocks.Fresh(db, Profile).Count > 0) { ShowUnlocks(); return; }
            var all = new List<SetDef>();
            foreach (SetDef s in db.gear.sets) if (Unlocks.Floor(db, s.id) > 0) all.Add(s);
            ui.ShowUnlockNotice(all, _ => sound.Play(db.gear.unlockSound));
        }

        void RenderStarterRow() => ui.RenderStarter(Profile, OpenStarterPicker);

        public void OpenStarterPicker()
        {
            if (Profile.starter <= 0) return;
            ui.ShowStarterPicker(Profile, PickStarter);
        }

        // kind < 0 → 고르지 않음
        void PickStarter(int kind)
        {
            // 예전 시작 조각이 판에 있으면 한도 계산이 바뀌므로, 넘치게 되면 그 조각을 판에서 내린다
            OwnedItem old = Unlocks.StarterItem(Profile);
            Profile.starterKind = kind;
            if (old != null && old.kind != kind)
            {
                Placement at = Profile.placements.Find(p => p.id == old.id);
                if (at != null && !Gear.CanPlace(db, Profile, old, at.turns, at.x, at.y)) Profile.placements.Remove(at);
            }
            Gear.Save(db, Profile);
            ui.CloseDialog();
            RenderStarterRow();
            sound.Play("upgrade");
        }
    }
}
