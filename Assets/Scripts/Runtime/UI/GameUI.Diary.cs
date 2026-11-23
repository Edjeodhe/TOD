// 15차 아버지의 일기: GameUI 에서 게시판을 여닫는 입구. 그리기는 DiaryBoard (Game/Diary UIDocument).
// 허브 게시판 시설의 E → ShowDiary(). 열린 동안 Opened == "diary" 라서 Enter 등반 시작과 E 상호작용이 막힌다.
namespace TOD
{
    public partial class GameUI
    {
        public DiaryBoard diary;

        public bool DiaryOpen => Opened == "diary";

        public void ShowDiary()
        {
            if (diary == null || Opened != "") return;
            Opened = "diary";
            diary.Show();
            if (!diary.IsOpen) Opened = "";
        }

        public void HideDiary()
        {
            if (diary != null) diary.Hide();
            if (Opened == "diary") Opened = "";
        }
    }
}
