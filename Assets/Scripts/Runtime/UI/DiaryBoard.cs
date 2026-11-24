// 15차 아버지의 일기 게시판 (Game/Diary UIDocument). 쪽지는 DiaryNote.uxml 템플릿을 복제하고, 문구는 GameDatabase.diary 데이터.
// 여닫기는 GameUI.ShowDiary() / HideDiary() 로 한다 (허브 게시판 E, 임시 로비 버튼). 판정은 Sim/Diary.cs.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TOD
{
    public class DiaryBoard : MonoBehaviour
    {
        public UIDocument document;
        public VisualTreeAsset noteTemplate;
        public GameController game;

        VisualElement root, board, notes, reader, toast, lobbyButton, lobbyDot, lobbyHost;
        Label toastText;
        readonly List<(VisualElement note, float tilt, float phase)> fresh = new();   // 새 쪽지: 반짝임 3개 + 살랑임
        float toastStart = -99, toastUntil = -99;
        bool built;

        GameDatabase Db => game.db;
        DiaryConfig D => game.db.diary;
        Profile P => game.Profile;
        public bool IsOpen => built && !board.ClassListContains("hidden");

        void Build()
        {
            if (built || document == null || game == null || game.db == null) return;
            root = document.rootVisualElement;
            board = root.Q("diary-board");
            notes = root.Q("diary-notes");
            reader = root.Q("diary-reader");
            toast = root.Q("diary-toast");
            toastText = root.Q<Label>("diary-toast-text");
            lobbyButton = root.Q("diary-lobby-button");
            lobbyDot = root.Q("diary-lobby-new");
            root.Q<Label>("diary-title").text = D.boardTitle;
            root.Q<Label>("diary-subtitle").text = D.boardSubtitle;
            root.Q<Label>("diary-hint").text = D.readHint;
            root.Q<Label>("diary-lobby-label").text = D.lobbyButton;
            root.Q<Button>("diary-close").text = D.closeText;
            root.Q<Button>("diary-page-close").text = D.closeText;
            root.Q<Button>("diary-close").clicked += () => game.ui.HideDiary();
            root.Q<Button>("diary-page-close").clicked += CloseReader;
            root.Q<Button>("diary-lobby-button").clicked += () => game.ui.ShowDiary();
            // 게시판 바깥(어두운 곳)을 누르면 닫는다, 읽기 중이면 읽기만 닫는다
            board.RegisterCallback<PointerDownEvent>(e => { if (e.target == board) game.ui.HideDiary(); });
            reader.RegisterCallback<PointerDownEvent>(e => { if (e.target == reader) CloseReader(); });
            built = true;
        }

        // ---------------------------------------------------------------- open / close

        public void Show()
        {
            Build();
            if (!built || P == null) return;
            Render();
            Show(reader, false);
            Show(board, true);
            game.sound.Play("loot");
        }

        public void Hide()
        {
            if (!built) return;
            Show(reader, false);
            Show(board, false);
        }

        void Render()
        {
            notes.Clear();
            fresh.Clear();
            foreach (DiaryEntry entry in Diary.All(Db))
            {
                TemplateContainer tree = noteTemplate.Instantiate();
                VisualElement note = tree.Q("note");
                note.RemoveFromHierarchy();
                note.style.rotate = new Rotate(new Angle(entry.tilt));
                bool open = Diary.IsUnlocked(P, entry);
                note.EnableInClassList("locked", !open);
                note.EnableInClassList("fresh", Diary.IsNew(P, entry));
                if (open)
                {
                    note.Q<Label>("note-title").text = entry.title;
                    note.Q<Label>("note-preview").text = string.Join("\n", (entry.lines ?? new string[0]).Take(2)) + " ...";
                    Ink(note.Q<Label>("note-preview"), entry);
                    Stain(note.Q("paper"), entry, .45f, (D.bloodMaxStains + 1) / 2, 11);
                    note.Q<Label>("note-new").text = D.newTag;
                    DiaryEntry captured = entry;
                    ((Button)note).clicked += () => Read(captured);
                }
                else
                {
                    note.Q<Label>("note-title").text = "";
                    note.Q<Label>("note-preview").text = string.Format(D.lockedText, entry.floor);
                    note.pickingMode = PickingMode.Ignore;
                }
                if (Diary.IsNew(P, entry)) fresh.Add((note, entry.tilt, entry.floor * 1.7f));
                notes.Add(note);
            }
        }

        void Read(DiaryEntry entry)
        {
            bool wasNew = Diary.IsNew(P, entry);
            root.Q<Label>("diary-page-title").text = entry.title;
            root.Q<Label>("diary-page-body").text = string.Join("\n", entry.lines ?? new string[0]);
            Ink(root.Q<Label>("diary-page-body"), entry);
            Stain(root.Q("diary-page"), entry, 1, D.bloodMaxStains, 29);
            Label last = root.Q<Label>("diary-page-last");
            last.text = Diary.LastLine(Db, P, entry);
            last.style.color = Diary.Choice(P, entry.floor) switch
            {
                DiaryChoice.Absorbed => D.absorbedInk, DiaryChoice.Refused => D.refusedInk, _ => D.undecidedInk,
            };
            root.Q("diary-page").style.rotate = new Rotate(new Angle(entry.tilt * .3f));
            // 17차 보상 줄: "기억의 파편 3 · 아버지의 도안: ..." (데이터 rewardLine)
            PieceDef pattern = entry.patternKind >= 0 && entry.patternKind < Db.gear.pieces.Length ? Db.gear.pieces[entry.patternKind] : null;
            Label reward = root.Q<Label>("diary-page-reward");
            if (reward != null)
            {
                reward.text = entry.readShards > 0 || pattern != null ? string.Format(D.rewardLine ?? "", entry.readShards, pattern?.name ?? "") : "";
                Show(reward, reward.text != "");
            }
            Show(reader, true);
            if (wasNew)
            {
                Diary.MarkRead(P, entry);
                Gear.Save(Db, P);
                Render();
                game.ui.UpdateMeta(P);
                var said = new List<string>();
                if (entry.readShards > 0) said.Add(string.Format(D.rewardShards, entry.readShards));
                if (pattern != null) said.Add(string.Format(D.rewardPattern, pattern.name));
                if (said.Count > 0) game.ui.Say(string.Join("  ", said));
            }
            game.sound.Play(wasNew ? "upgrade" : "loot");
        }

        void CloseReader() => Show(reader, false);

        // ---------------------------------------------------------------- 18차 핏자국, 핏빛 잉크

        // 본문 잉크: 데이터 ink 의 알파가 0 이면 USS 기본색
        static void Ink(Label label, DiaryEntry entry)
        {
            if (label == null) return;
            label.style.color = entry.ink.a > 0 ? new StyleColor(entry.ink) : new StyleColor(StyleKeyword.Null);
        }

        // 쪽지 위에 핏자국을 흩뿌린다. 자리, 크기, 각도는 층 번호로 정해져서 열 때마다 같다.
        // 핏자국 층(diary-blood)은 종이 맨 아래(글자 밑)에 깔린다.
        void Stain(VisualElement paper, DiaryEntry entry, float scale, int maxStains, int salt)
        {
            if (paper == null) return;
            paper.Q("diary-blood")?.RemoveFromHierarchy();
            Sprite[] stains = D.bloodStains;
            int count = stains == null || stains.Length == 0 ? 0 : Mathf.RoundToInt(entry.blood * maxStains);
            if (count <= 0) return;
            var layer = new VisualElement { name = "diary-blood", pickingMode = PickingMode.Ignore };
            layer.AddToClassList("diary-blood");
            var rng = new System.Random(entry.floor * 97 + salt);
            float alpha = Mathf.Lerp(D.bloodAlpha.x, D.bloodAlpha.y, entry.blood);
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = stains[rng.Next(stains.Length)];
                if (sprite == null) continue;
                float size = Mathf.Lerp(D.bloodSize.x, D.bloodSize.y, (float)rng.NextDouble()) * scale;
                float aspect = sprite.rect.width / Mathf.Max(1, sprite.rect.height);
                var stain = new VisualElement { pickingMode = PickingMode.Ignore };
                stain.AddToClassList("diary-stain");
                stain.style.backgroundImage = new StyleBackground(sprite);
                stain.style.width = size * Mathf.Min(1, aspect);
                stain.style.height = size / Mathf.Max(1, aspect);
                stain.style.left = Length.Percent((float)rng.NextDouble() * 88 - 6);
                stain.style.top = Length.Percent((float)rng.NextDouble() * 86 - 4);
                stain.style.rotate = new Rotate(new Angle(((float)rng.NextDouble() - .5f) * 60));   // 흐른 자국이 위로 가지 않게 ±30도만
                Color tint = D.bloodTint.a > 0 ? D.bloodTint : Color.white;
                tint.a *= alpha * Mathf.Lerp(.75f, 1, (float)rng.NextDouble());
                stain.style.unityBackgroundImageTintColor = tint;
                layer.Add(stain);
            }
            paper.Insert(0, layer);
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            Build();
            if (!built || P == null) return;
            float now = Time.unscaledTime;

            // 다른 대화 상자가 Opened 를 바꿨으면 게시판도 닫는다
            if (IsOpen && game.ui.Opened != "diary") Hide();
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (!reader.ClassListContains("hidden")) CloseReader(); else game.ui.HideDiary();
            }

            // 새 조각 알림: 등반이 끝나 결과 화면이나 허브로 돌아왔을 때 한 번
            if (game.Run == null && Diary.PendingNotice(Db, P) > 0)
            {
                int count = Diary.TakeNotice(Db, P);
                Gear.Save(Db, P);
                toastText.text = string.Format(D.noticeText, count);
                toastStart = now;
                toastUntil = now + D.noticeDuration;
                Show(toast, true);
                game.sound.Play("upgrade");
            }
            if (!toast.ClassListContains("hidden"))
            {
                float t = now - toastStart, left = toastUntil - now;
                float pop = Mathf.Clamp01(t / .25f);
                float scale = Mathf.Lerp(.7f, 1f, 1 - (1 - pop) * (1 - pop)) + Mathf.Sin(Mathf.Clamp01(t / .5f) * Mathf.PI) * .06f;
                toast.style.scale = new Scale(new Vector3(scale, scale, 1));
                toast.style.opacity = Mathf.Clamp01(left / .4f) * pop;
                toast.style.translate = new Translate(0, (1 - pop) * -30);
                if (left <= 0) Show(toast, false);
            }

            // 임시 로비 버튼: 허브 전, 로비가 보일 때만 (새 조각이 있으면 반짝)
            lobbyHost ??= game.ui.document != null ? game.ui.document.rootVisualElement.Q("lobby") : null;
            bool lobbyVisible = lobbyHost != null && !lobbyHost.ClassListContains("hidden");
            Show(lobbyButton, D.showLobbyButton && game.Run == null && lobbyVisible && !IsOpen);
            bool anyNew = Diary.Unlocked(Db, P).Any(e => Diary.IsNew(P, e));
            Show(lobbyDot, anyNew);
            if (anyNew) Twinkle(lobbyDot, now, 0);

            // 새 쪽지 반짝임
            foreach (var (note, tilt, phase) in fresh)
            {
                Twinkle(note.Q("spark-a"), now, phase);
                Twinkle(note.Q("spark-b"), now, phase + 1.9f);
                Twinkle(note.Q("spark-c"), now, phase + 3.1f);
                // 핀에 매달린 종이가 바람에 살랑이듯
                note.style.rotate = new Rotate(new Angle(tilt + Mathf.Sin(now * 1.8f + phase) * 1.6f));
            }
        }

        static void Twinkle(VisualElement e, float now, float phase)
        {
            if (e == null) return;
            float s = .55f + .45f * Mathf.Abs(Mathf.Sin(now * 2.6f + phase));
            e.style.scale = new Scale(new Vector3(s, s, 1));
            e.style.rotate = new Rotate(new Angle(now * 40 + phase * 30));
            e.style.opacity = .35f + .65f * s;
        }

        static void Show(VisualElement e, bool visible) => e?.EnableInClassList("hidden", !visible);
    }
}
