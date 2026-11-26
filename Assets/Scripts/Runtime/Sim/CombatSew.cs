// 24차-hit-sparks: 당기기로 맞힌 적 자리에 바느질 타격 파티클 Fx 하나 (조각은 뷰가 seed 로 그림, ArenaRenderer.Sew.cs).
using UnityEngine;

namespace TOD
{
    public partial class Combat
    {
        // hits = 이 당기기에서 이 적이 맞은 횟수, binding = 결박(죄종 색으로 물듦), awayAngle = 주인공 반대쪽 (캔버스 각도)
        void SpawnSewSparks(RunState r, Enemy e, int hits, bool binding, Element sin, float awayAngle)
        {
            SewSparkConfig s = X.sparks;
            if (s == null || s.time <= 0) return;
            Fx fx = Spawn(r, FxKind.Sew, e.x, e.y, new FxSpec { time = s.time, alpha = 1 });
            fx.pop = Mathf.Max(1, hits);
            fx.bound = binding;
            fx.angle = awayAngle;
            fx.lift = EnemyHeight(e) * .5f;
            fx.seed = r.random() * 10000;
            fx.size = e.IsBoss ? X.bossScale : 1;
            fx.element = sin;
            if (binding) fx.color = db.Tone(sin).color;
        }
    }
}
