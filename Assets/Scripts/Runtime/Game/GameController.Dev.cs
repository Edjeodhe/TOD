// 23차-boss-dev: 개발자 모드가 라운드나 층을 바로 옮길 때 (GameController 의 나머지 부분).
// Ascend 와 같이 열린 창과 상인 진열을 닫고, 시뮬레이션을 바꾼 뒤 새 맵을 깐다.
namespace TOD
{
    public partial class GameController
    {
        public void DevJump(System.Action<RunState> change)
        {
            if (Run == null) return;
            ui.CloseDialog();
            shop = null;
            offered = null;
            change(Run);
            sound.Play("round");
            if (Run.phase != Phase.Victory && Run.outcome == Outcome.None) arena.BeginRound(Run);
        }
    }
}
