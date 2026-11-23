// 바닥 청크 템플릿 (MCP로 만든 Arena/Background/Chunks/ChunkTemplate). BackgroundView 가 복제·배치한다.
using UnityEngine;

namespace TOD
{
    public class ChunkView : MonoBehaviour
    {
        public SpriteRenderer floor;
        public Transform[] lamps;                 // 등불 묶음 (Glow·Flame·Base 를 자식으로 둔다)
        public SpriteRenderer[] lampGlows, lampFlames;
    }
}
