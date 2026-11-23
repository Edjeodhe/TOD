// 15차-outgame-unlock: 세트 해금 알림(세트 색 종이 띠 + 이름 + 반짝임)과 다락 '시작 조각' 줄, 고르는 창.
// 구조는 Main.uxml(unlock-notice, attic-starter, dialog-starter), 모양과 움직임은 Outgame.uss 전환(transition)이다.
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
            root.Q<Button>("starter-pick")?.RegisterCallback<ClickEvent>(_ => game.OpenStarterPicker());
            root.Q<Button>("close-starter")?.RegisterCallback<ClickEvent>(_ => CloseDialog());
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
            if (!string.IsNullOrEmpty(set.effectText)) effect += ", " + string.Format(set.effectText, Amount(set.effectPerTier));
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

        // ---------------------------------------------------------------- 시작 조각

        // 다락 아래 줄: 강화를 샀으면 보이고, 고른 조각 아이콘과 이름(없으면 '아직 고르지 않음')
        public void RenderStarter(Profile profile, Action pick)
        {
            VisualElement row = attic.Q("attic-starter");
            if (row == null) return;
            Show(row, profile.starter > 0);
            OwnedItem item = Unlocks.StarterItem(profile);
            PieceDef def = item != null ? Gear.Def(db, item) : null;
            Icon(row.Q("starter-icon"), def?.icon);
            Show(row.Q("starter-icon"), def != null);
            Text(row, "starter-name", def?.name ?? "");
            if (def != null) row.Q<Label>("starter-name").style.color = NameColor(RankColor(def));
            Show(row.Q("starter-name"), def != null);
            Show(row.Q("starter-none"), def == null);
        }

        // 가진 조각 목록(재봉 상자 줄 템플릿 PieceButton.uxml 복제)에서 하나를 고른다. 지금 시작 조각은 선택 표시.
        public void ShowStarterPicker(Profile profile, Action<int> pick)
        {
            var list = root.Q<ScrollView>("starter-list");
            list.Clear();
            Button none = root.Q<Button>("starter-clear");
            none.clickable = new Clickable(() => pick(-1));
            none.SetEnabled(Unlocks.StarterItem(profile) != null);
            foreach (OwnedItem item in profile.items.OrderByDescending(i => Gear.Def(db, i).cost).ThenBy(i => Gear.Def(db, i).set))
            {
                PieceDef def = Gear.Def(db, item);
                var button = (Button)Slot(pieceButton, "piece");
                button.AddToClassList($"rank-{def.cost}");
                button.AddToClassList("starter-row");
                button.EnableInClassList("selected", item.kind == profile.starterKind);
                Color setColor = db.Set(def.set).color;
                button.style.unityBackgroundImageTintColor = SetPaperTint(setColor);
                Icon(button.Q("icon"), def.icon);
                MiniShape(button.Q("shape"), Gear.Shape(db, item, 0), Color.Lerp(setColor, Color.white, .25f), 14);
                Text(button, "name", def.name);
                button.Q<Label>("name").style.color = NameColor(RankColor(def));
                Text(button, "set", db.Set(def.set).name);
                button.Q<Label>("set").style.color = Color.Lerp(setColor, Color.white, .3f);
                Text(button, "state", "");
                Label rankTag = button.Q<Label>("rank");
                rankTag.text = db.gear.rankNames[def.cost];
                rankTag.style.backgroundColor = RankColor(def);
                rankTag.style.color = def.cost >= 4 ? new Color(1f, .95f, .88f) : Color.Lerp(RankColor(def), Color.black, .72f);
                int kind = item.kind;
                button.clicked += () => pick(kind);
                list.Add(button);
            }
            ShowDialog("starter");
        }
    }
}
