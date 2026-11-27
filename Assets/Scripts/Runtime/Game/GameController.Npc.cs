// 23차 쉬어가기 NPC 4종과 재봉실 NPC (GameController 의 나머지 부분). 판정은 Sim/CombatNpc.cs, 그리기는 ArenaRenderer.Npc.cs 와 HubView,
// 말풍선은 GameUI.Npc.cs. 데이터는 db.npcs (NpcData.cs).
using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TOD
{
    public partial class GameController
    {
        string npcLine;
        float npcLineAt = -99, npcTalkUntil = -99;
        bool npcNear;
        NpcKind npcLineKind;

        NpcConfig NpcCfg => db.npcs;

        void InitNpcs()
        {
            ui.DialogClosed += id => { if (id == "talk") { npcTalkUntil = Clock; talkAfter = null; } };   // 26차 대화창을 닫으면 말하기 자세도 끝
            if (hub == null) return;
            hub.stationVisible = s => !HubView.IsNpc(s) || (Profile.npcMet != null && Profile.npcMet.Contains((int)HubView.NpcOf(s)));
            // 개발자 모드 'NPC 불러오기' (DevTools 의 id: blessing, gift, xp, gamble)
            DevTools.SummonNpc = id =>
            {
                NpcKind kind = id switch { "gift" => NpcKind.Gift, "xp" => NpcKind.Memory, "gamble" => NpcKind.Gamble, _ => NpcKind.Blessing };
                NpcDef def = NpcCfg?.Get(kind);
                if (def == null) return "NPC 데이터가 없습니다";
                return DevSummonNpc(kind) ? $"{def.name}을(를) 옆에 불렀습니다" : "등반 중에만 부를 수 있습니다";
            };
        }

        void NpcSay(NpcKind kind, string line, bool talk = true)
        {
            if (string.IsNullOrEmpty(line)) return;
            npcLine = line;
            npcLineKind = kind;
            npcLineAt = Clock;
            if (talk) npcTalkUntil = Clock + (NpcCfg != null ? NpcCfg.talkHold : 2);
            if (!string.IsNullOrEmpty(NpcCfg?.talkSound)) sound.Play(NpcCfg.talkSound, .8f);
        }

        // 26차: E 로 말을 걸면 아래 대화창 (얼굴이 없으면 예전처럼 머리 위 말풍선). 창이 열린 동안 NPC 는 말하기 자세
        void NpcTalk(NpcKind kind, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            NpcDef def = NpcCfg?.Get(kind);
            if (def == null || db.talk == null || def.portrait == null) { NpcSay(kind, line); return; }
            npcLine = null;
            npcLineKind = kind;
            npcTalkUntil = float.MaxValue;
            if (!string.IsNullOrEmpty(NpcCfg.talkSound)) sound.Play(NpcCfg.talkSound, .8f);
            OpenTalk(def.name, def.portrait, line, null);
        }

        // 26차 대화창 공용. after = E 로 끝까지 읽고 닫았을 때 이어서 할 일 (상인 = 상점, 모이라 = 카드 창). Esc 로 닫으면 안 한다
        Action talkAfter;

        bool OpenTalk(string speaker, Sprite face, string line, Action after)
        {
            if (db.talk == null || face == null || string.IsNullOrEmpty(line)) return false;
            talkAfter = after;
            ui.ShowTalk(db.talk, NpcCfg != null && NpcCfg.charTime > 0 ? NpcCfg.charTime : .04f, speaker, face, line);
            return true;
        }

        // 대화창에서 E (또는 상자 누르기): 찍히는 중이면 다 찍고, 다 찍혔으면 닫고 이어서 할 일
        public void AdvanceTalk()
        {
            if (!ui.TalkOpen) return;
            if (ui.TalkTyping) { ui.FinishTalkTyping(); return; }
            Action after = talkAfter;
            ui.CloseDialog();   // DialogClosed 가 talkAfter 를 비운다
            after?.Invoke();
        }

        void NpcNotice(NpcTalkResult result)
        {
            NpcConfig c = NpcCfg;
            if (!string.IsNullOrEmpty(result.notice)) ui.Say(result.notice);
            if (result.rewarded && c != null)
            {
                string id = result.bad ? c.badSound : c.goodSound;
                if (!string.IsNullOrEmpty(id)) sound.Play(id);
            }
        }

        // 쉬어가기 NPC 에게 E
        void TalkNpcRun()
        {
            if (Run == null) return;
            NpcDef def = Combat.NpcHere(Run);
            if (def == null) return;
            NpcTalkResult result = Combat.TalkNpc(Run);
            NpcTalk(def.kind, result.line);
            NpcNotice(result);
            if (result.first) { Gear.Save(db, Profile); if (!string.IsNullOrEmpty(NpcCfg.metNotice)) ui.Say(string.Format(NpcCfg.metNotice, def.name)); }
        }

        // 재봉실 NPC 에게 E: 등반마다 한 번 무작위 템이나 다음 등반 축복
        void TalkNpcHub(NpcKind kind)
        {
            NpcTalkResult result = Combat.HubGift(Profile, kind, () => Random.value);
            NpcTalk(kind, result.line);
            NpcNotice(result);
            if (result.rewarded) { Gear.Save(db, Profile); ui.UpdateMeta(Profile); }
        }

        // 등반 시작: 재봉실 NPC 가 걸어 둔 축복을 건다
        void ApplyNpcBoons()
        {
            string names = Combat.ApplyBoons(Run);
            if (string.IsNullOrEmpty(names)) return;
            Gear.Save(db, Profile);
            if (!string.IsNullOrEmpty(NpcCfg?.boonNotice)) ui.Say(string.Format(NpcCfg.boonNotice, names));
        }

        // 매 프레임: 다가오면 혼잣말, 말하기 자세, 말풍선 자리
        void TickNpcs(float dt)
        {
            ui.TickTalk();   // 26차 아래 대화창 글자 찍기, 얼굴 움직임
            NpcConfig c = NpcCfg;
            if (c == null) return;
            bool talking = Clock < npcTalkUntil;
            Vector2? head = null;
            if (Run != null && screen == UiScreen.Combat)
            {
                NpcDef def = Combat.NpcHere(Run);
                bool near = def != null && !runInventory && Combat.Nearest(Run) == Interactable.Npc;
                if (near && !npcNear && Opened == "") { string line = Combat.NpcApproach(Run); if (line != null) NpcSay(def.kind, line, false); }
                npcNear = near;
                if (arena.NpcRig != null) arena.NpcRig.Talking = talking && def != null && npcLineKind == def.kind;
                if (def != null && npcLineKind == def.kind) head = arena.NpcHead;
            }
            else if (InHub && hub != null)
            {
                npcNear = false;
                hub.npcTalking = talking ? npcLineKind : (NpcKind?)null;
                NpcDef def = c.Get(npcLineKind);
                if (def != null) head = hub.NpcHead(npcLineKind, def.bubbleLift);
            }
            if (hub != null && hubRoot != null && hubRoot.activeSelf) hub.AnimateNpcs(dt);

            string shown = null;
            int visible = 0;
            if (npcLine != null)
            {
                float charTime = c.charTime > 0 ? c.charTime : .04f;
                float typed = npcLine.Length * charTime;
                if (Clock < npcLineAt + typed + c.lineHold) { shown = npcLine; visible = Mathf.FloorToInt((Clock - npcLineAt) / charTime) + 1; }
                else npcLine = null;
            }
            ui.UpdateNpcBubble(shown, visible, head, c.Get(npcLineKind)?.name);
        }

        // 개발자 모드: 지금 라운드에 그 NPC 를 부른다 (등반 중)
        public bool DevSummonNpc(NpcKind kind)
        {
            if (Run == null) return false;
            Combat.DevSummonNpc(Run, kind);
            return true;
        }

        // 개발자 모드: 재봉실 NPC 모두 만난 것으로 / 선물 다시 받기
        public void DevMeetAllNpcs()
        {
            Profile.npcMet ??= new System.Collections.Generic.List<int>();
            foreach (NpcKind k in System.Enum.GetValues(typeof(NpcKind))) if (!Profile.npcMet.Contains((int)k)) Profile.npcMet.Add((int)k);
            Profile.npcGifted?.Clear();
            Gear.Save(db, Profile);
        }
    }
}
