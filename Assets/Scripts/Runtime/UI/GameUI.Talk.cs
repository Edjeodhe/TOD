// 26차 아래 대화창 (GameUI 의 나머지 부분). 왼쪽 = 주인공 얼굴(듣는 쪽, 조금 어둡고 작게), 오른쪽 = 말하는 NPC 얼굴, 가운데 아래 = 종이 상자.
// 열면 Opened = "talk" (overlay 의 dialog-talk). 글자는 한 자씩 찍히고, E(또는 상자 누르기)로 다 찍기 → 한 번 더 누르면 닫는다 (GameController.AdvanceTalk).
// 값은 GameDatabase.talk (TalkData.cs), 모양은 UI/Talk.uss.
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        VisualElement talkDialog, talkHero, talkNpc, talkBox, talkTag;
        Label talkText, talkName, talkHint;
        TalkConfig talkCfg;
        string talkLine, talkShown;
        float talkStart, talkCharTime;
        bool talkDone;

        public bool TalkOpen => Opened == "talk";
        public bool TalkTyping => TalkOpen && !talkDone;

        bool EnsureTalk()
        {
            if (talkDialog != null) return true;
            talkDialog = root?.Q("dialog-talk");
            if (talkDialog == null) return false;
            talkHero = talkDialog.Q("talk-hero");
            talkNpc = talkDialog.Q("talk-npc");
            talkBox = talkDialog.Q("talk-box");
            talkTag = talkDialog.Q("talk-tag");
            talkText = talkDialog.Q<Label>("talk-text");
            talkName = talkDialog.Q<Label>("talk-name");
            talkHint = talkDialog.Q<Label>("talk-hint");
            talkBox.RegisterCallback<ClickEvent>(_ => game?.AdvanceTalk());
            return true;
        }

        // speaker = 이름표, face = NPC 얼굴, line = 할 말. defaultCharTime = talk.charTime 이 0 일 때 (npcs.charTime)
        public void ShowTalk(TalkConfig c, float defaultCharTime, string speaker, Sprite face, string line)
        {
            if (c == null || !EnsureTalk()) return;
            talkCfg = c;
            talkCharTime = c.charTime > 0 ? c.charTime : Mathf.Max(.01f, defaultCharTime);
            talkLine = line ?? "";
            talkShown = null;
            talkStart = Time.unscaledTime;
            talkDone = talkLine.Length == 0;
            Icon(talkHero, c.heroPortrait);
            Icon(talkNpc, face);
            if (c.paper != null) talkBox.style.backgroundImage = new StyleBackground(c.paper);
            if (c.nameTag != null) talkTag.style.backgroundImage = new StyleBackground(c.nameTag);
            talkName.text = speaker ?? "";
            Show(talkTag, !string.IsNullOrEmpty(speaker));
            ShowDialog("talk");
            TickTalk();
        }

        public void FinishTalkTyping() => talkDone = true;

        // 매 프레임 (GameController.TickNpcs)
        public void TickTalk()
        {
            if (!TalkOpen || talkCfg == null || talkDialog == null) return;
            TalkConfig c = talkCfg;
            float t = Time.unscaledTime - talkStart;
            if (!talkDone && Mathf.FloorToInt(t / talkCharTime) + 1 >= talkLine.Length) talkDone = true;
            string text = talkDone ? talkLine : Typed(talkLine, Mathf.FloorToInt(t / talkCharTime) + 1);
            if (text != talkShown) { talkText.text = text; talkShown = text; }
            string hint = Settings.Relabel(talkDone ? c.nextHint : c.skipHint);
            if (talkHint.text != hint) talkHint.text = hint;

            // 얼굴이 아래에서 올라옴 (끝으로 갈수록 느리게), 말하는 동안 NPC 얼굴이 들썩임
            float u = c.portraitPop > 0 ? Mathf.Clamp01(t / c.portraitPop) : 1;
            float ease = 1 - (1 - u) * (1 - u) * (1 - u);
            float rise = (1 - ease) * c.portraitRise;
            float bob = talkDone ? 0 : Mathf.Abs(Mathf.Sin(t * c.talkBobSpeed * Mathf.PI)) * c.talkBob;
            talkNpc.style.translate = new Translate(0, rise - bob);
            talkNpc.style.opacity = ease;
            talkHero.style.translate = new Translate(0, rise);
            talkHero.style.opacity = ease;
            float s = c.listenerScale > 0 ? c.listenerScale : 1;
            talkHero.style.scale = new Scale(new Vector3(s, s, 1));
            float d = c.listenerDim > 0 ? c.listenerDim : 1;
            talkHero.style.unityBackgroundImageTintColor = new Color(d, d, d, 1);
        }
    }
}
