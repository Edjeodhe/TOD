// 19차 모이라 카드 창 (UiScreen.Attic, 다락 방이 뒤에 비침). 하스스톤 '발견'처럼 고른다:
//   모이라가 카드 넷을 쥔 손을 들면(cardDelay) 카드가 그 손(MoiraRig.CardPoint)에서 한 장씩 튀어나와 호를 그리며 자리로 날아가고,
//   도착하면 뒤집혀 앞면(이름, 그림, 설명, 가격)이 보인다. 그 뒤 둥실 떠 있고, 마우스를 올리면 떠올라 커지며 빛난다.
//   누르면 그 카드가 가운데로 나오며 번쩍이고 나머지는 모이라 손으로 돌아간다(판을 여는 카드). 확인 창을 여는 카드는 제자리에서 번쩍인다.
//   못 고르는 카드(파편 부족, 최대)는 흐리고, 누르면 흔들린다. Esc/돌아가기 = 모든 카드가 손으로 돌아간 뒤 닫힌다.
// 구조는 Main.uxml(attic: moira-line, moira-cards, moira-hint), 카드 한 장은 MoiraCard.uxml 복제, 모양은 Moira.uss.
// 움직임은 USS 전환 대신 매 프레임 계산한다(TickMoira): 위치, 회전, 크기, 뒤집기를 카드마다 따로 섞기 위해.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    // 모이라 카드 한 장의 내용 (GameController 가 채움)
    public class MoiraCardInfo
    {
        public string key;
        public Sprite icon;
        public string name, text, foot;
        public string level;                  // 카드 위 단계 꼬리표 (비우면 숨김)
        public bool enabled;
        public bool opensBoard;               // 판을 여는 카드 (나머지 카드가 손으로 돌아감)
        public Action pick, locked;           // 누름 / 못 고르는 카드를 누름
    }

    public partial class GameUI
    {
        public VisualTreeAsset moiraCard;     // MoiraCard.uxml

        const float FlipTime = .22f, ReturnTime = .32f, ChooseTime = .42f, ShakeTime = .35f;

        class DealtCard
        {
            public MoiraCardInfo info;
            public VisualElement root, inner, face, back, glow;
            public Vector2 from, slot;
            public float slotRot, startRot, dealAt, hover, shakeAt = -9, chooseAt = -9, returnAt = -9, phase;
            public bool hovered;
        }

        readonly List<DealtCard> moiraCards = new();
        Func<Vector2> moiraHand;              // 모이라 손 자리 (패널 px)
        Action moiraClosed;
        bool moiraLeaving;
        float moiraLeaveAt;
        DealtCard chosen;

        public bool MoiraCardsBusy => moiraLeaving || (chosen != null && chosen.info.opensBoard);

        // 카드를 나눠 준다. deal = 손에서 새로 날려 보냄(false 면 같은 자리에 내용만 바꿈, 확인 창 뒤 갱신).
        public void ShowMoiraCards(IList<MoiraCardInfo> infos, string line, int shards, Func<Vector2> hand, bool deal)
        {
            HubConfig h = db.hub;
            moiraHand = hand;
            if (!string.IsNullOrEmpty(h.moiraEyebrow)) Text(attic, "attic-eyebrow", h.moiraEyebrow);
            if (!string.IsNullOrEmpty(h.moiraTitle)) Text(attic, "attic-title", h.moiraTitle);
            Text(attic, "attic-shards", shards.ToString());
            if (line != null) SayMoira(line);
            Text(attic, "moira-hint", h.cardHint ?? "");
            VisualElement layer = attic.Q("moira-cards");
            if (!deal && moiraCards.Count == infos.Count)
            {
                for (int i = 0; i < infos.Count; i++) Fill(moiraCards[i], infos[i]);
                chosen = null;
                return;
            }
            layer.Clear();
            moiraCards.Clear();
            moiraLeaving = false;
            chosen = null;
            float start = Now + Mathf.Max(0, h.cardDelay);
            Vector2 size = CardSize();
            for (int i = 0; i < infos.Count; i++)
            {
                var card = new DealtCard { info = infos[i], phase = i * 1.7f };
                card.root = Slot(moiraCard, "moira-card");
                card.inner = card.root.Q("card-inner");
                card.face = card.root.Q("card-face");
                card.back = card.root.Q("card-back");
                card.glow = card.root.Q("card-glow");
                if (h.cardFront != null) card.face.style.backgroundImage = new StyleBackground(h.cardFront);
                if (h.cardBack != null) card.back.style.backgroundImage = new StyleBackground(h.cardBack);
                card.root.style.width = size.x;
                card.root.style.height = size.y;
                card.dealAt = start + i * h.cardStagger;
                card.startRot = (i - (infos.Count - 1) / 2f) * 12 - 20;
                int index = i;
                card.root.RegisterCallback<PointerEnterEvent>(_ => moiraCards[index].hovered = true);
                card.root.RegisterCallback<PointerLeaveEvent>(_ => moiraCards[index].hovered = false);
                ((Button)card.root).clicked += () => Pick(moiraCards[index]);
                Fill(card, infos[i]);
                layer.Add(card.root);
                moiraCards.Add(card);
            }
            Layout();
        }

        Vector2 CardSize() => new(220, 330);

        void Fill(DealtCard card, MoiraCardInfo info)
        {
            card.info = info;
            Icon(card.root.Q("card-icon"), info.icon);
            Text(card.root, "card-name", info.name ?? "");
            Text(card.root, "card-text", info.text ?? "");
            Text(card.root, "card-foot", info.foot ?? "");
            Text(card.root, "card-level", info.level ?? "");
            card.root.Q("card-level").style.display = string.IsNullOrEmpty(info.level) ? DisplayStyle.None : DisplayStyle.Flex;
            card.root.EnableInClassList("locked", !info.enabled);
        }

        // 카드 자리: 화면 아래쪽 가운데에 살짝 부채꼴 (가운데가 조금 높다)
        void Layout()
        {
            float w = attic.layout.width, h = attic.layout.height;
            if (float.IsNaN(w) || w < 10) { w = 1920; h = 1080; }
            int n = moiraCards.Count;
            float gap = CardSize().x + 34;
            for (int i = 0; i < n; i++)
            {
                float k = i - (n - 1) / 2f;
                moiraCards[i].slot = new Vector2(w / 2 + k * gap, h * .64f + k * k * 10);
                moiraCards[i].slotRot = k * 3.5f;
            }
        }

        public void SayMoira(string line)
        {
            Label label = attic.Q<Label>("moira-line");
            if (label == null) return;
            label.text = line ?? "";
            label.RemoveFromClassList("show");
            if (string.IsNullOrEmpty(line)) return;   // 26차: 대화창으로 이미 말했으면 카드 창 위 줄은 비운다
            label.schedule.Execute(() => label.AddToClassList("show")).StartingIn(30);
        }

        void Pick(DealtCard card)
        {
            if (moiraLeaving || chosen != null || Now < card.dealAt + db.hub.cardFly + FlipTime) return;   // 아직 날아오는 중
            if (!card.info.enabled)
            {
                card.shakeAt = Now;
                card.info.locked?.Invoke();
                return;
            }
            chosen = card;
            card.chooseAt = Now;
            if (card.info.opensBoard)
            {
                foreach (DealtCard other in moiraCards) if (other != card) other.returnAt = Now;
                attic.schedule.Execute(() => { chosen = null; card.info.pick?.Invoke(); }).StartingIn((long)(ChooseTime * 1000));
            }
            else
            {
                attic.schedule.Execute(() => card.info.pick?.Invoke()).StartingIn((long)(ChooseTime * .6f * 1000));
            }
        }

        // 돌아가기: 카드가 모두 손으로 돌아간 뒤 closed
        public void LeaveMoira(Action closed)
        {
            if (moiraCards.Count == 0) { closed?.Invoke(); return; }
            if (moiraLeaving) return;
            moiraLeaving = true;
            moiraLeaveAt = Now;
            moiraClosed = closed;
            foreach (DealtCard c in moiraCards) c.returnAt = Now + UnityEngine.Random.value * .08f;
        }

        // 판을 열고 돌아왔을 때 등: 손에서 다시 나눌 수 있게 비운다
        public void ClearMoiraCards()
        {
            attic?.Q("moira-cards")?.Clear();
            moiraCards.Clear();
            moiraLeaving = false;
            chosen = null;
        }

        static float EaseOutBack(float u) { const float c = 1.5f; u -= 1; return 1 + (c + 1) * u * u * u + c * u * u; }
        static float EaseOutQuad(float u) => 1 - (1 - u) * (1 - u);

        void TickMoira()
        {
            TickVessel();   // 22차 욕망 최대치 강화 연출 (GameUI.Vessel.cs)
            TickStamp();    // 22차 인벤토리 확장 알림 (GameUI.Stamp.cs)
            if (attic == null || moiraCards.Count == 0 || attic.ClassListContains("hidden")) return;
            Layout();
            Vector2 hand = moiraHand?.Invoke() ?? new Vector2(attic.layout.width / 2, attic.layout.height * .3f);
            float fly = Mathf.Max(.05f, db.hub.cardFly);
            float dt = Time.unscaledDeltaTime;
            Vector2 size = CardSize();
            bool allBack = true;
            foreach (DealtCard c in moiraCards)
            {
                c.from = hand;
                float u = Mathf.Clamp01((Now - c.dealAt) / fly);
                // 손 → 자리: 호를 그리며 (위로 솟았다가) 살짝 지나쳤다 돌아옴
                Vector2 pos = Vector2.LerpUnclamped(c.from, c.slot, EaseOutBack(u)) + new Vector2(0, -Mathf.Sin(Mathf.PI * u) * 120);
                float rot = Mathf.Lerp(c.startRot, c.slotRot, EaseOutQuad(u));
                float scale = Mathf.Lerp(.2f, 1, EaseOutQuad(u));
                float alpha = Now < c.dealAt ? 0 : 1;
                // 뒤집기: 도착 뒤 FlipTime 동안 가로 폭이 0 까지 줄었다가 앞면으로 다시 펴진다
                float f = Mathf.Clamp01((Now - c.dealAt - fly) / FlipTime);
                bool faceUp = f >= .5f;
                float squash = Mathf.Abs(Mathf.Cos(Mathf.PI * f));
                // 둥실 + 마우스 올림
                bool ready = f >= 1 && !moiraLeaving && chosen == null;
                c.hover = Mathf.MoveTowards(c.hover, ready && c.hovered && c.info.enabled ? 1 : 0, dt * 7);
                if (f >= 1) pos.y += Mathf.Sin(Now * 1.6f + c.phase) * 5;
                pos.y -= c.hover * 34;
                rot = Mathf.Lerp(rot, 0, c.hover);
                scale *= 1 + c.hover * .1f;
                // 못 고르는 카드 흔들기
                float sk = Now - c.shakeAt;
                if (sk < ShakeTime) pos.x += Mathf.Sin(sk * 60) * 10 * (1 - sk / ShakeTime);
                // 고른 카드: 가운데 앞으로 (판을 여는 카드) / 제자리에서 번쩍
                float ch = c == chosen ? Mathf.Clamp01((Now - c.chooseAt) / ChooseTime) : 0;
                if (ch > 0)
                {
                    float e = EaseOutQuad(ch);
                    if (c.info.opensBoard) pos = Vector2.Lerp(pos, new Vector2(attic.layout.width / 2, attic.layout.height * .5f), e);
                    scale *= 1 + (c.info.opensBoard ? .3f : .12f) * Mathf.Sin(Mathf.PI * Mathf.Min(1, e * 1.2f) * .5f);
                    rot = Mathf.Lerp(rot, 0, e);
                }
                // 손으로 돌아가기
                float r = c.returnAt > 0 ? Mathf.Clamp01((Now - c.returnAt) / ReturnTime) : 0;
                if (r > 0)
                {
                    float e = EaseOutQuad(r);
                    pos = Vector2.Lerp(pos, c.from, e);
                    scale = Mathf.Lerp(scale, .2f, e);
                    rot = Mathf.Lerp(rot, c.startRot, e);
                    alpha = 1 - e;
                    faceUp = r < .35f && faceUp;
                    squash = faceUp ? 1 : squash;
                }
                if (r < 1) allBack = false;

                c.root.style.left = pos.x - size.x / 2;
                c.root.style.top = pos.y - size.y / 2;
                c.root.style.rotate = new Rotate(rot);
                c.root.style.scale = new Scale(new Vector2(scale, scale));
                c.root.style.opacity = alpha;
                c.inner.style.scale = new Scale(new Vector2(Mathf.Max(.02f, squash), 1));
                c.face.style.display = faceUp ? DisplayStyle.Flex : DisplayStyle.None;
                c.back.style.display = faceUp ? DisplayStyle.None : DisplayStyle.Flex;
                c.glow.style.opacity = Mathf.Max(c.hover, ch > 0 ? 1 - ch * .4f : 0) * (c.info.enabled ? 1 : 0);
                c.root.pickingMode = r > 0 ? PickingMode.Ignore : PickingMode.Position;
            }
            if (moiraLeaving && allBack && Now - moiraLeaveAt > ReturnTime)
            {
                moiraLeaving = false;
                Action done = moiraClosed;
                moiraClosed = null;
                ClearMoiraCards();
                done?.Invoke();
            }
        }

        // 모이라 손 자리(월드) → 패널 px
        public Vector2 WorldToPanel(Vector3 world)
        {
            if (root.panel == null) return Vector2.zero;
            Vector3 screen = worldCamera.WorldToScreenPoint(world);
            return RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
        }
    }
}
