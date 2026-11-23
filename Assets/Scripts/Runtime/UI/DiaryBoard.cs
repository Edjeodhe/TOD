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
            Label last = root.Q<Label>("diary-page-last");
            last.text = Diary.LastLine(Db, P, entry);
            last.style.color = Diary.Choice(P, entry.floor) switch
            {
                DiaryChoice.Absorbed => D.absorbedInk, DiaryChoice.Refused => D.refusedInk, _ => D.undecidedInk,
            };
            root.Q("diary-page").style.rotate = new Rotate(new Angle(entry.tilt * .3f));
            Show(reader, true);
            if (wasNew)
            {
                Diary.MarkRead(P, entry);
                Gear.Save(Db, P);
                Render();
            }
            game.sound.Play(wasNew ? "upgrade" : "loot");
        }

        void CloseReader() => Show(reader, false);

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
