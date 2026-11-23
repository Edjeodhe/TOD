// 2D 아핀 행렬 (웹판 DOMMatrix). 잘린 적 조각 배치 등 캔버스 좌표 계산에 쓴다.
using UnityEngine;

namespace TOD
{
    // DOMMatrix 2D: x' = a·x + c·y + e,  y' = b·x + d·y + f
    public struct Affine
    {
        public float a, b, c, d, e, f;
        public static readonly Affine Identity = new() { a = 1, d = 1 };

        public static Affine operator *(Affine m, Affine n) => new()
        {
            a = m.a * n.a + m.c * n.b, b = m.b * n.a + m.d * n.b,
            c = m.a * n.c + m.c * n.d, d = m.b * n.c + m.d * n.d,
            e = m.a * n.e + m.c * n.f + m.e, f = m.b * n.e + m.d * n.f + m.f,
        };

        public Affine Translate(float x, float y) => this * new Affine { a = 1, d = 1, e = x, f = y };
        public Affine Scale(float x, float y) => this * new Affine { a = x, d = y };
        public Affine Rotate(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            return this * new Affine { a = cos, b = sin, c = -sin, d = cos };
        }
        public Vector2 Apply(Vector2 p) => new(a * p.x + c * p.y + e, b * p.x + d * p.y + f);
    }
}
