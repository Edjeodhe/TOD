// 큰 맵 배경: 카메라 주변의 바닥 청크만 켜고(템플릿 슬롯 복제·재활용) 컨셉 색을 입힌다.
// 청크 안의 등불 위치는 (라운드 시드, 청크 좌표)로 정해져서 같은 라운드에서는 늘 같은 자리에 있다.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public class BackgroundView : MonoBehaviour
    {
        public ChunkView chunkTemplate;
        public SpriteRenderer moteTemplate, shade;
        public int moteCount;

        GameDatabase db;
        Pool<ChunkView> chunks;
        readonly Dictionary<Vector2Int, ChunkView> live = new();
        readonly List<Vector2Int> stale = new();
        readonly List<SpriteRenderer> motes = new();
        readonly List<Vector3> moteSeeds = new();
        int seed;
        ConceptDef concept;
        MaterialPropertyBlock block;
        static readonly int FillId = Shader.PropertyToID("_Fill"), PurifyId = Shader.PropertyToID("_Purify");

        public void Init(GameDatabase database)
        {
            db = database;
            block = new MaterialPropertyBlock();
            chunks = new Pool<ChunkView>(chunkTemplate, chunkTemplate.transform.parent);
            moteTemplate.gameObject.SetActive(false);
            for (int i = 0; i < moteCount; i++)
            {
                SpriteRenderer mote = Instantiate(moteTemplate, moteTemplate.transform.parent);
                mote.gameObject.SetActive(true);
                motes.Add(mote);
                moteSeeds.Add(new Vector3(i * 463.7f % 1000, i * 241.3f % 1000, 5 + i % 7));
            }
        }

        // 새 라운드 = 새 맵: 청크를 모두 돌려받고 시드를 바꾼다.
        public void BeginRound(int roundSeed)
        {
            seed = roundSeed;
            concept = null;
            chunks.ReleaseAll();
            live.Clear();
        }

        public void Render(RunState r, Rect visibleCanvas, float clock, float zoom)
        {
            MapConfig m = db.map;
            float size = m.chunkSize;
            // 보이는 캔버스 영역 → 전투장 좌표 범위 (청크 하나만큼 여유)
            float ax0 = visibleCanvas.xMin - size, ax1 = visibleCanvas.xMax + size;
            float ay0 = (visibleCanvas.yMin - Space.Top) / Space.Proj - size, ay1 = (visibleCanvas.yMax - Space.Top) / Space.Proj + size;
            int cols = Mathf.CeilToInt(db.arena.width / size), rows = Mathf.CeilToInt(db.arena.height / size);
            int i0 = Mathf.Max(0, Mathf.FloorToInt(ax0 / size)), i1 = Mathf.Min(cols - 1, Mathf.FloorToInt(ax1 / size));
            int j0 = Mathf.Max(0, Mathf.FloorToInt(ay0 / size)), j1 = Mathf.Min(rows - 1, Mathf.FloorToInt(ay1 / size));
            bool recolor = concept != r.concept;
            concept = r.concept;

            stale.Clear();
            foreach (var key in live.Keys) if (key.x < i0 || key.x > i1 || key.y < j0 || key.y > j1) stale.Add(key);
            foreach (var key in stale) { chunks.Release(live[key]); live.Remove(key); }
            for (int i = i0; i <= i1; i++)
            {
                for (int j = j0; j <= j1; j++)
                {
                    var key = new Vector2Int(i, j);
                    if (live.TryGetValue(key, out ChunkView chunk)) { if (recolor) Paint(chunk); continue; }
                    chunk = chunks.Get();
                    live[key] = chunk;
                    Place(chunk, i, j, size);
                    Paint(chunk);
                }
            }

            // 떠다니는 불씨: 카메라 화면 안에서 맴돈다.
            Color tint = concept != null ? concept.lampTint : Color.white;
            RenderPalette pal = db.palette;
            for (int k = 0; k < motes.Count; k++)
            {
                Vector3 s = moteSeeds[k];
                float x = visibleCanvas.xMin + Mathf.Repeat(s.x / 1000 * visibleCanvas.width + Mathf.Sin(clock * .52f + s.x) * 16, visibleCanvas.width);
                float y = visibleCanvas.yMin + Mathf.Repeat(s.y / 1000 * visibleCanvas.height - clock * s.z, visibleCanvas.height);
                SpriteRenderer mote = motes[k];
                mote.transform.position = Space.Canvas(x, y);
                float scale = (.8f + k % 4 * .39f) / mote.sprite.rect.width;
                mote.transform.localScale = new Vector3(scale, scale, 1);
                mote.color = Painter.Alpha(tint, pal.moteAlphaBase + (Mathf.Sin(clock * .93f + s.x) * .5f + .5f) * pal.moteAlphaPulse);
            }
            // 가장자리 그늘은 화면에 붙어 다닌다 (카메라 중심, 줌만큼 커짐).
            if (shade != null)
            {
                Vector3 at = Space.Canvas(visibleCanvas.center);
                shade.transform.position = new Vector3(at.x, at.y, shade.transform.position.z);
                shade.transform.localScale = new Vector3(zoom, zoom, 1);
            }
        }

        void Place(ChunkView chunk, int i, int j, float size)
        {
            // 청크 바닥: 가로 size, 세로 size(바닥 좌표) → 캔버스에서는 세로로 Proj 만큼 눌린다.
            float cx = (i + .5f) * size, cy = (j + .5f) * size;
            chunk.transform.position = Space.Ground(cx, cy);
            SpriteRenderer floor = chunk.floor;
            Vector3 s = floor.transform.localScale;
            floor.size = new Vector2(size / s.x / Space.Ppu, size * Space.Proj / s.y / Space.Ppu);
            var random = new System.Random(seed * 7919 + i * 104729 + j * 1299709);
            for (int k = 0; k < chunk.lamps.Length; k++)
            {
                bool on = k < db.map.lampsPerChunk;
                chunk.lamps[k].gameObject.SetActive(on);
                if (!on) continue;
                float inset = db.map.lampInset;
                float lx = i * size + inset + (float)random.NextDouble() * (size - inset * 2);
                float ly = j * size + inset + (float)random.NextDouble() * (size - inset * 2);
                chunk.lamps[k].position = Space.Ground(lx, ly);
            }
        }

        void Paint(ChunkView chunk)
        {
            ConceptDef c = concept;
            RenderPalette pal = db.palette;
            block.Clear();
            if (c != null) block.SetColor(FillId, c.floorFill);
            block.SetFloat(PurifyId, 1);          // 정화 고리 안쪽 바닥은 SpriteFX 가 되살린다
            chunk.floor.SetPropertyBlock(block);
            Color tint = c != null ? c.lampTint : Color.white;
            foreach (SpriteRenderer glow in chunk.lampGlows) glow.color = Painter.Alpha(tint, pal.lampGlowAlpha);
            foreach (SpriteRenderer flame in chunk.lampFlames) flame.color = Painter.Alpha(tint, pal.lampFlameAlpha);
        }
    }
}
