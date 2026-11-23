// 전투장 배경 (웹판 createBackground + 떠다니는 불씨). 바닥·그라데이션·테두리·그늘 오브젝트는 MCP로 배치하고,
// 여기서는 층마다 바뀌는 등불 색과 불씨 움직임만 맡는다.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public class BackgroundView : MonoBehaviour
    {
        public SpriteRenderer[] lampGlows, lampFlames;
        public SpriteRenderer moteTemplate;
        public int moteCount;

        readonly List<SpriteRenderer> motes = new();
        readonly List<Vector3> moteSeeds = new();   // x, y, drift
        readonly List<float> moteSizes = new();
        Color tint = Color.clear;

        public void Init(float arenaHeight)
        {
            moteTemplate.gameObject.SetActive(false);
            for (int i = 0; i < moteCount; i++)
            {
                SpriteRenderer mote = Instantiate(moteTemplate, moteTemplate.transform.parent);
                mote.gameObject.SetActive(true);
                motes.Add(mote);
                moteSeeds.Add(new Vector3(i * 463.7f % Space.ArenaWidth, i * 241.3f % arenaHeight, 5 + i % 7));
                moteSizes.Add(.8f + i % 4 * .39f);
            }
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        public void Render(Color floorTint, float clock, RenderPalette palette, float arenaHeight)
        {
            if (floorTint != tint)
            {
                tint = floorTint;
                foreach (SpriteRenderer glow in lampGlows) glow.color = Painter.Alpha(tint, palette.lampGlowAlpha);
                foreach (SpriteRenderer flame in lampFlames) flame.color = Painter.Alpha(tint, palette.lampFlameAlpha);
            }
            for (int i = 0; i < motes.Count; i++)
            {
                Vector3 seed = moteSeeds[i];
                float x = seed.x + Mathf.Sin(clock * .52f + seed.x) * 16;
                float y = (seed.y + arenaHeight - clock * seed.z % arenaHeight) % arenaHeight;
                SpriteRenderer mote = motes[i];
                mote.transform.position = Space.Ground(x, y);
                float size = moteSizes[i] / mote.sprite.rect.width;
                mote.transform.localScale = new Vector3(size, size, 1);
                mote.color = Painter.Alpha(tint, palette.moteAlphaBase + (Mathf.Sin(clock * .93f + seed.x) * .5f + .5f) * palette.moteAlphaPulse);
            }
        }
    }
}
