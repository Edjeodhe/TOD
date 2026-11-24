// 23차-boss-dev: 전투 HUD 의 층, 라운드 표시와 기억의 파편 보유 수 (GameUI 의 나머지 부분).
// - 타이머 오른쪽 "3층 2라운드" (Main.uxml timer > floor-round). 보스 HUD 가 뜨면 보스 이름 옆(boss-floor)에 같은 글.
// - 일시정지 버튼 아래 파편 수 (hud-shards). 수가 오르면 bump 클래스로 잠깐 커졌다 돌아온다 (Main.uss transition).
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        const float ShardBumpTime = .14f;
        int hudShards = -1;
        float shardBumpUntil;

        void UpdateFloorRound(RunState r, bool bossShown)
        {
            string format = r.roundKind switch
            {
                RoundKind.Rest when !string.IsNullOrEmpty(T.hudFloorRest) => T.hudFloorRest,
                RoundKind.Boss when !string.IsNullOrEmpty(T.hudFloorBoss) => T.hudFloorBoss,
                _ => T.hudFloorRound,
            };
            string text = string.IsNullOrEmpty(format) ? "" : string.Format(format, r.floor, r.round);
            Text(hud, bossShown ? "boss-floor" : "floor-round", text);
        }

        void UpdateHudShards(Profile profile)
        {
            VisualElement box = root.Q("hud-shards");
            if (box == null) return;
            Text(box, "hud-shards-count", profile.shards.ToString());
            if (hudShards >= 0 && profile.shards > hudShards) shardBumpUntil = Time.unscaledTime + ShardBumpTime;
            hudShards = profile.shards;
            box.EnableInClassList("bump", Time.unscaledTime < shardBumpUntil);
        }
    }
}
