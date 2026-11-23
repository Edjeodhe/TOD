// 실 속성 입자 (웹판 render.js particle/updateParticles). 종류별 ParticleSystem 은 MCP로 만들고 설정한다
// (중력·크기 변화·투명도 변화는 각 시스템의 모듈 값). 여기서는 Emit 만 한다.
using UnityEngine;

namespace TOD
{
    public enum ParticleKind { Ember, Drop, Spark, Glint }

    public class ParticleFx : MonoBehaviour
    {
        public ParticleSystem ember, drop, spark, glint;
        public int maxParticles;

        ParticleSystem System(ParticleKind kind) => kind switch
        {
            ParticleKind.Ember => ember, ParticleKind.Drop => drop, ParticleKind.Spark => spark, _ => glint,
        };

        int Count() => ember.particleCount + drop.particleCount + spark.particleCount + glint.particleCount;

        // 캔버스 px 좌표·속도(px/s, 아래로 +)
        public void Emit(ParticleKind kind, Vector2 canvas, Vector2 velocity, float life, Color color, float size)
        {
            if (Count() > maxParticles) return;
            var p = new ParticleSystem.EmitParams
            {
                position = Space.Canvas(canvas),
                velocity = new Vector3(velocity.x / Space.Ppu, -velocity.y / Space.Ppu, 0),
                startLifetime = life,
                startColor = color,
                startSize = size * 2 / Space.Ppu,
                applyShapeToPosition = false,
            };
            System(kind).Emit(p, 1);
        }

        // 히트스톱·일시 정지 동안 입자도 멈춘다 (웹판은 게임 시간으로 입자를 움직였다).
        public void SetRunning(bool running)
        {
            foreach (ParticleSystem ps in new[] { ember, drop, spark, glint })
            {
                ParticleSystem.MainModule main = ps.main;
                main.simulationSpeed = running ? 1 : 0;
            }
        }

        public void Clear()
        {
            foreach (ParticleSystem ps in new[] { ember, drop, spark, glint }) ps.Clear();
        }
    }
}
