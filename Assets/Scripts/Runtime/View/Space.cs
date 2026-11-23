// 전투장 좌표 ↔ 웹 캔버스 px ↔ Unity 월드 변환, 템플릿 슬롯 풀.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public static class Space
    {
        // GameController.Start 에서 GameDatabase.arena 값으로 채운다.
        public static float Ppu = 1, Top, Proj = 1, CanvasHeight = 1, ArenaWidth = 1;

        public static void Configure(ArenaConfig a)
        {
            Ppu = a.pixelsPerUnit; Top = a.screenTop; Proj = a.projectionY; CanvasHeight = a.canvasHeight; ArenaWidth = a.width;
        }

        // 캔버스 px(아래로 +y) → 월드
        public static Vector3 Canvas(float cx, float cy) => new(cx / Ppu, -cy / Ppu, 0);
        public static Vector3 Canvas(Vector2 c) => Canvas(c.x, c.y);
        public static float ProjectY(float y) => Top + Proj * y;
        // 전투장 바닥 좌표(+ 바닥에서 띄운 높이 px) → 월드
        public static Vector3 Ground(float x, float y, float lift = 0) => Canvas(x, ProjectY(y) - lift);
        public static Vector2 ToCanvas(float x, float y) => new(x, ProjectY(y));
        public static Vector2 WorldToArena(Vector3 w) => new(w.x * Ppu, (-w.y * Ppu - Top) / Proj);
        // 캔버스 각도(라디안, 시계방향 +) → Unity z 회전(도)
        public static float Degrees(float canvasRadians) => -canvasRadians * Mathf.Rad2Deg;
    }

    // MCP로 만든 비활성 템플릿을 복제해 쓰는 슬롯 풀 (HANDOFF 규칙의 '템플릿 슬롯 복제').
    public class Pool<T> where T : Component
    {
        readonly T template;
        readonly Transform parent;
        readonly Stack<T> free = new();
        public readonly List<T> active = new();

        public Pool(T template, Transform parent)
        {
            this.template = template;
            this.parent = parent;
            template.gameObject.SetActive(false);
        }

        public T Get()
        {
            T item = free.Count > 0 ? free.Pop() : Object.Instantiate(template, parent);
            item.gameObject.SetActive(true);
            active.Add(item);
            return item;
        }

        public void Release(T item)
        {
            if (!active.Remove(item)) return;
            item.gameObject.SetActive(false);
            free.Push(item);
        }

        public void ReleaseAll()
        {
            foreach (T item in active) { item.gameObject.SetActive(false); free.Push(item); }
            active.Clear();
        }
    }
}
