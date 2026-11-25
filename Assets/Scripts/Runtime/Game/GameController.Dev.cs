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

        // 죄종 층으로 바로 가기: 지금 층 번호 그대로 그 죄종의 새 맵 1라운드 (등반 전이면 등반부터 시작)
        public void DevGoToSin(Element sin)
        {
            if (Run == null) StartRun();
            if (Run == null) return;
            DevJump(r => Combat.DevGoToFloor(r, r.floor, sin));
        }

        // 미래 보기 구슬 미리 보기: 재봉실 모이라 앞에서 그 죄종의 구슬 연출을 튼다. 파편을 내지 않고, 읽어 둔 운명(Profile.fate)도 바꾸지 않는다.
        public string DevShowFate(Element sin)
        {
            if (Run != null) return "재봉실에서만 볼 수 있습니다 (등반 중)";
            if (ui.FateShowing) return "지금 미래 보기가 끝난 뒤에 누르세요 (Esc 건너뛰기)";
            ElementDef def = db.elements?.Def(sin);
            if (def == null) return "죄종 정의가 없습니다";
            if (hub != null && !hub.InAttic) GoToAttic();   // 다락 방으로 먼저 (방 밖이면 ShowScreen(Attic)이 걸어 들어가기만 하고 창을 안 연다)
            if (screen != UiScreen.Attic) TalkToMoira();    // 모이라 창 = 미래 보기 연출(자막, 안내)이 그려지는 층
            moiraLine = FateSpeech(def);
            ShowFate(false, def);
            return null;
        }
    }
}
