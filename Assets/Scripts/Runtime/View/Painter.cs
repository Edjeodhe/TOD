// 웹판 Canvas 그리기 호출을 템플릿 슬롯(SpriteRenderer/LineRenderer) 복제로 옮긴 그리기 도구.
// 매 프레임 Begin() 에서 슬롯을 모두 돌려받고, 그리는 순서대로 sortingOrder 를 매겨 Canvas 의 덧칠 순서를 지킨다.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public enum Layer { Background, Ground, Actors, Effects, Overlay }

    public class Painter : MonoBehaviour
    {
        [Header("Template slots (inactive)")]
        public SpriteRenderer spriteSlot;
        public LineRenderer lineSlot;
        [Header("Materials")]
        public Material spriteAlpha, spriteAdditive, lineAlpha, lineAdditive, lineDashed;
        public Sprite white;

        Pool<SpriteRenderer> sprites;
        Pool<LineRenderer> lines;
        MaterialPropertyBlock block;
        readonly int[] order = new int[5];
        readonly List<Vector3> scratch = new();
        static AnimationCurve blade;
        static readonly int BrightId = Shader.PropertyToID("_Bright"), SatId = Shader.PropertyToID("_Sat"), FillId = Shader.PropertyToID("_Fill"),
            CutId = Shader.PropertyToID("_CutPlane"), ShapeId = Shader.PropertyToID("_Shape"), RingId = Shader.PropertyToID("_Ring");

        void Awake()
        {
            sprites = new Pool<SpriteRenderer>(spriteSlot, transform);
            lines = new Pool<LineRenderer>(lineSlot, transform);
            block = new MaterialPropertyBlock();
            blade ??= new AnimationCurve(new Keyframe(0, 0, 1 / .72f, 1 / .72f), new Keyframe(.72f, 1, 1 / .72f, -1 / .28f), new Keyframe(1, 0, -1 / .28f, -1 / .28f));
        }

        public void Begin()
        {
            sprites.ReleaseAll();
            lines.ReleaseAll();
            System.Array.Clear(order, 0, order.Length);
        }

        static string LayerName(Layer layer) => layer.ToString();

        void Sort(Renderer renderer, Layer layer)
        {
            renderer.sortingLayerName = LayerName(layer);
            renderer.sortingOrder = order[(int)layer]++;
        }

        // 스프라이트를 캔버스 좌표 center 에 widthPx×heightPx 크기로 그린다 (음수 크기 = 뒤집기).
        public SpriteRenderer Sprite(Layer layer, Sprite sprite, Vector2 center, float canvasAngle, float widthPx, float heightPx, Color color, bool additive = false)
        {
            SpriteRenderer sr = sprites.Get();
            sr.sprite = sprite;
            sr.sharedMaterial = additive ? spriteAdditive : spriteAlpha;
            sr.color = color;
            Transform t = sr.transform;
            t.position = Space.Canvas(center);
            t.rotation = Quaternion.Euler(0, 0, Space.Degrees(canvasAngle));
            Rect rect = sprite.rect;
            t.localScale = new Vector3(widthPx / rect.width, heightPx / rect.height, 1);
            Sort(sr, layer);
            block.Clear();
            sr.SetPropertyBlock(block);
            return sr;
        }

        // 필터 흉내: 밝기·채도·단색 덮기·반평면 절단 (월드 법선 n, 거리 d).
        public void Style(Renderer r, float bright = 1, float sat = 1, Color fill = default, float fillAmount = 0, Vector4? cut = null, float shape = 0, float ring = 0)
        {
            block.Clear();
            block.SetFloat(BrightId, bright);
            block.SetFloat(SatId, sat);
            block.SetColor(FillId, new Color(fill.r, fill.g, fill.b, fillAmount));
            block.SetVector(CutId, cut ?? Vector4.zero);
            block.SetFloat(ShapeId, shape);
            block.SetFloat(RingId, ring);
            r.SetPropertyBlock(block);
        }

        // 사각형 (fillRect)
        public SpriteRenderer Rect(Layer layer, float x, float y, float w, float h, Color color, bool additive = false) =>
            Sprite(layer, white, new Vector2(x + w / 2, y + h / 2), 0, w, h, color, additive);

        // 바닥 타원: rx×ry 반지름, width>0 이면 테두리 두께(px), 0 이면 채움
        public SpriteRenderer Ellipse(Layer layer, Vector2 center, float rx, float ry, Color color, float width = 0, bool additive = false)
        {
            float grow = width > 0 ? width / 2 : 0;
            SpriteRenderer sr = Sprite(layer, white, center, 0, (rx + grow) * 2, (ry + grow * ry / Mathf.Max(rx, .01f)) * 2, color, additive);
            Style(sr, shape: width > 0 ? 2 : 1, ring: width > 0 ? Mathf.Clamp01(width / (rx + grow)) : 0);
            return sr;
        }

        public LineRenderer Line(Layer layer, IList<Vector2> canvasPoints, float widthPx, Color color, bool additive = false, bool dashed = false)
        {
            LineRenderer lr = lines.Get();
            lr.sharedMaterial = dashed ? lineDashed : additive ? lineAdditive : lineAlpha;
            lr.textureMode = dashed ? LineTextureMode.Tile : LineTextureMode.Stretch;
            lr.widthCurve = AnimationCurve.Constant(0, 1, 1);
            lr.widthMultiplier = widthPx / Space.Ppu;
            lr.startColor = lr.endColor = color;
            scratch.Clear();
            foreach (Vector2 p in canvasPoints) scratch.Add(Space.Canvas(p));
            lr.positionCount = scratch.Count;
            lr.SetPositions(scratch.ToArray());
            Sort(lr, layer);
            return lr;
        }

        public LineRenderer Line(Layer layer, Vector2 a, Vector2 b, float widthPx, Color color, bool additive = false, bool dashed = false) =>
            Line(layer, new[] { a, b }, widthPx, color, additive, dashed);

        // 끝이 뾰족한 칼날 다각형 (a → 72% 지점에서 가장 넓음 → b)
        public LineRenderer Blade(Layer layer, Vector2 a, Vector2 b, float halfWidthPx, Color color, bool additive)
        {
            Vector2 m = a + (b - a) * .72f;
            LineRenderer lr = Line(layer, new[] { a, m, b }, halfWidthPx * 2, color, additive);
            lr.widthCurve = blade;
            return lr;
        }

        public static Color Alpha(Color c, float a) => new(c.r, c.g, c.b, c.a * a);
    }
}
