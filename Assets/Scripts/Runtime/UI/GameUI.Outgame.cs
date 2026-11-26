// 15차-outgame-unlock: 세트 해금 알림(세트 색 종이 띠 + 이름 + 반짝임). 17차: 다락 방 이름, 방 전환 어둠, 실궤 창.
// 구조는 Main.uxml(unlock-notice, room-fade, attic-shards), 모양과 움직임은 Outgame.uss 전환(transition)이다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        const float UnlockHold = 3.2f;     // 한 세트 알림이 떠 있는 시간 (클릭하면 넘김)
        const float UnlockFade = .45f;     // 반짝임이 사그라드는 시점
        const float UnlockLeave = .4f;     // 띠가 사라지는 시간

        VisualElement unlockBox;
        readonly Queue<SetDef> unlockQueue = new();
        Action<SetDef> unlockShown;
        IVisualElementScheduledItem unlockNext, unlockFadeItem;
        bool unlockShowing;

        void InitOutgame()
        {
            unlockBox = root.Q("unlock-notice");
            unlockBox?.RegisterCallback<ClickEvent>(_ => NextUnlock());
        }

        // ---------------------------------------------------------------- 해금 알림

        public bool UnlockShowing => unlockShowing;

        // 새로 열린 세트들을 차례로 띄운다. 하나가 뜰 때마다 onShow(효과음 등).
        public void ShowUnlockNotice(IEnumerable<SetDef> sets, Action<SetDef> onShow)
        {
            if (unlockBox == null) return;
            foreach (SetDef s in sets) unlockQueue.Enqueue(s);
            unlockShown = onShow;
            if (!unlockShowing) NextUnlock();
        }

        void NextUnlock()
        {
            unlockNext?.Pause();
            unlockFadeItem?.Pause();
            if (unlockQueue.Count == 0)
            {
                unlockShowing = false;
                unlockBox.RemoveFromClassList("show");
                unlockBox.AddToClassList("leave");
                unlockNext = unlockBox.schedule.Execute(() => { if (!unlockShowing) Show(unlockBox, false); }).StartingIn((long)(UnlockLeave * 1000));
                return;
            }
            unlockShowing = true;
            SetDef set = unlockQueue.Dequeue();
            Color ink = Color.Lerp(set.color, Color.black, .7f);
            unlockBox.Q("unlock-ribbon").style.unityBackgroundImageTintColor = Color.Lerp(set.color, Color.white, .35f);
            unlockBox.Q("unlock-flash").style.unityBackgroundImageTintColor = set.color;
            Text(unlockBox, "unlock-name", set.name);
            unlockBox.Q<Label>("unlock-name").style.color = ink;
            // 1단계 효과 (세트 패널과 같은 문구): "어둠 피해 +2, 저주 10%"
            string effect = string.Format(T.pieceEffect, set.bonus, Amount(set.damagePerTier));
            if (!string.IsNullOrEmpty(set.effectText)) effect += ", " + string.Format(set.effectText, Amount(set.effectPerTier), Amount(set.extra));
            Text(unlockBox, "unlock-text", effect);
            foreach (Label spark in unlockBox.Query<Label>(className: "unlock-spark").ToList()) spark.style.color = Color.Lerp(set.color, Color.white, .5f);

            // 처음 상태로 되돌린 뒤 다음 프레임에 show → USS 전환이 다시 돈다
            unlockBox.RemoveFromClassList("show");
            unlockBox.RemoveFromClassList("fade");
            unlockBox.RemoveFromClassList("leave");
            Show(unlockBox, true);
            unlockBox.schedule.Execute(() => unlockBox.AddToClassList("show")).StartingIn(30);
            unlockFadeItem = unlockBox.schedule.Execute(() => unlockBox.AddToClassList("fade")).StartingIn((long)((UnlockFade + 1f) * 1000));
            unlockNext = unlockBox.schedule.Execute(NextUnlock).StartingIn((long)(UnlockHold * 1000));
            unlockShown?.Invoke(set);
        }

        // ---------------------------------------------------------------- 17차 다락 방, 실궤 창

        VisualElement roomFade;
        IVisualElementScheduledItem fadeMid, fadeEnd;

        // 다락 방의 실궤에서 연 강화 창: 방이 뒤에 비치게 배경 그림을 걷고 옅은 그늘만 깐다 (Outgame.uss .attic.over-room)
        public void SetAtticOverRoom(bool over) => attic?.EnableInClassList("over-room", over);

        // 위 띠 방 이름과 아래 안내를 지금 방 것으로
        public void SetHubRoom(HubRoom room)
        {
            if (room == null || lobby == null) return;
            if (!string.IsNullOrEmpty(room.title)) lobby.Q<Label>(className: "brand-name").text = room.title;
            if (!string.IsNullOrEmpty(room.subtitle)) lobby.Q<Label>(className: "brand-sub").text = room.subtitle;
            hubRoomHint = room.hint ?? "";
            Text(root, "hub-hint", Settings.Relabel(hubRoomHint));   // 24차: 바꾼 키 이름
        }

        // 화면이 half 초 동안 어두워졌다가 → mid → half 초 동안 밝아진다. 어두운 판이 없으면 바로 mid.
        public void FadeRoom(float half, Action mid)
        {
            roomFade ??= root.Q("room-fade");
            if (roomFade == null || half <= 0) { mid(); return; }
            fadeMid?.Pause();
            fadeEnd?.Pause();
            roomFade.style.transitionDuration = new List<TimeValue> { new(half, TimeUnit.Second) };
            Show(roomFade, true);
            roomFade.schedule.Execute(() => roomFade.AddToClassList("on")).StartingIn(16);
            fadeMid = roomFade.schedule.Execute(() =>
            {
                mid();
                roomFade.RemoveFromClassList("on");
                fadeEnd = roomFade.schedule.Execute(() => Show(roomFade, false)).StartingIn((long)(half * 1000) + 30);
            }).StartingIn((long)(half * 1000) + 30);
        }
    }
}
