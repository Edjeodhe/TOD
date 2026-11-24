// 23차 종이 인형 리그 (쉬어가기 NPC 4종). 리깅 페이지(tools/rig_editor/rig.html)의 계산을 C# 으로 그대로 옮긴 것이다 (MoiraRig 와 같은 규칙, 모이라 전용 점성구, 카드는 뺌).
//   데이터 = 페이지가 저장한 Assets/Rig/npc_<id>.json (TextAsset, 그대로 읽음). 그림 = 페이지가 쓰는 Assets/sprites/npc_<id>_*.png 그대로.
//   그림이 있는 뼈마다 partTemplate(SpriteRenderer, MCP 로 만든 템플릿)을 한 번 복제하고(템플릿 슬롯 복제), 매 프레임 페이지와 같은 순서로 계산한다:
//     pose   : idle 반복 + hold(말하기 반복) × 말 비중 + raise(말하기 시작 → 최고 → 유지) × 말 비중
//     worlds : 뼈 i = 부모 · 이동(at) · 회전(rest + 동작) · 배율(scale) (뿌리 = 이동(rootPos + 동작 root))
//     그리기 : 뼈 변환 · artScale · flip(pivot 세로축 거울) · 파츠 rot · 파츠 scale · 이동(-pivot), 순서(order) 오름차순
//   말하는 동안 talkPart 가 있는 뼈는 그 그림으로 바뀐다 (말 비중 > swapAt 이고 raise 최고 시점 × swapAfter 뒤).
//   에디터에서는 0.5초마다 파일을 직접 다시 읽어, 페이지에서 저장하면 플레이 중에도 바로 바뀐다. 페이지 계산을 바꾸면 이 파일도 같이 바꾼다.
// 좌표: 그림 px, 아래 +y, 각도 시계 방향 +(도). 리그 원점 = 발밑(바닥). Unity 로 옮길 때 y 와 회전 방향을 뒤집는다.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace TOD
{
    public class CutoutRig : MonoBehaviour
    {
        [Header("리깅 페이지 데이터 (그대로)")]
        public TextAsset rigJson;             // Assets/Rig/npc_<id>.json
        public Sprite[] sprites;              // 파츠 그림 (이름 = JSON parts.file 의 파일 이름)
        public SpriteRenderer partTemplate;   // 뼈마다 복제할 렌더러 템플릿 (꺼 둠)
        public SortingGroup group;            // 그리기 층과 순서 (전투장 = Ground 층 위, 재봉실 = 시설과 같은 층)

        [Header("색 (방 빛에 맞춤)")]
        public Color tint = Color.white;
        [Range(0, 2)] public float saturation = 1, brightness = 1;

        public bool Talking { get; set; }
        public float TalkWeight { get; private set; }
        public float height { get { Load(); return rigHeight; } }   // 리그 높이 (그림 px). 데이터 height 에 맞춰 배율을 정한다
        float rigHeight = 400;

        class Part { public Sprite sprite; public Vector2 pivot; public float rot, scale = 1; }
        class Bone
        {
            public string id, parent, part, talkPart;
            public int parentIndex = -1, order;
            public Vector2 at;
            public float rest, scale = 1, artScale = 1, shade;
            public bool flip;
            public SpriteRenderer sr;
        }
        class Wave { public int bone; public float offset, a1, p1, a2, p2; public float At(float u) => offset + a1 * Mathf.Sin(2 * Mathf.PI * (u + p1)) + a2 * Mathf.Sin(4 * Mathf.PI * (u + p2)); }
        class Loop { public float length = 1; public Wave rootX, rootY; public readonly List<Wave> bones = new(); }
        class Shot { public float length, peakTime, settleTime; public bool keepEnd, easeIn; public readonly List<(int bone, float start, float peak, float settle)> bones = new(); }

        readonly Dictionary<string, Part> parts = new();
        readonly List<Bone> bones = new();
        Loop idle, hold;
        Shot raise;
        Vector2 rootPos;
        float swapAt = .35f, swapAfter = .8f, talkFollow = 6;
        string loadedText;
        float time, talkStart = -99, reloadCheck;
        bool wasTalking;
        float[] ang;
        Matrix[] world;
        MaterialPropertyBlock block;
        static readonly int BrightId = Shader.PropertyToID("_Bright"), SatId = Shader.PropertyToID("_Sat");

        void Load()
        {
            string text = rigJson != null ? rigJson.text : null;
#if UNITY_EDITOR
            string path = rigJson != null ? UnityEditor.AssetDatabase.GetAssetPath(rigJson) : null;
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) text = System.IO.File.ReadAllText(path);
#endif
            if (string.IsNullOrEmpty(text) || text == loadedText) return;
            loadedText = text;
            object root = MiniJson.Parse(text);
            foreach (Bone b in bones) if (b.sr != null) Destroy(b.sr.gameObject);
            bones.Clear();
            parts.Clear();
            rigHeight = MiniJson.F(root, "height", 400);
            float[] rp = MiniJson.V(root, "rootPos");
            rootPos = rp != null ? new Vector2(rp[0], rp[1]) : Vector2.zero;
            swapAt = MiniJson.F(root, "swapAt", .35f);
            swapAfter = MiniJson.F(root, "swapAfter", .8f);
            talkFollow = MiniJson.F(root, "talkFollow", 6);

            var byName = (sprites ?? new Sprite[0]).Where(s => s != null).GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());
            foreach (var pair in MiniJson.O(root, "parts") ?? new Dictionary<string, object>())
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(MiniJson.S(pair.Value, "file") ?? "");
                if (!byName.TryGetValue(name, out Sprite sprite)) { Debug.LogWarning($"[TOD] {this.name} 리그 그림 없음: {name} (CutoutRig.sprites 에 넣어 주세요)"); continue; }
                float[] pv = MiniJson.V(pair.Value, "pivot");
                parts[pair.Key] = new Part
                {
                    sprite = sprite, pivot = pv != null ? new Vector2(pv[0], pv[1]) : Vector2.zero,
                    rot = MiniJson.F(pair.Value, "rot"), scale = MiniJson.F(pair.Value, "scale", 1),
                };
            }
            foreach (object o in MiniJson.A(root, "bones") ?? new List<object>())
            {
                float[] at = MiniJson.V(o, "at");
                var b = new Bone
                {
                    id = MiniJson.S(o, "id"), parent = MiniJson.S(o, "parent"), part = MiniJson.S(o, "part"), talkPart = MiniJson.S(o, "talkPart"),
                    at = at != null ? new Vector2(at[0], at[1]) : Vector2.zero, rest = MiniJson.F(o, "rest"), order = (int)MiniJson.F(o, "order"),
                    scale = MiniJson.F(o, "scale", 1), artScale = MiniJson.F(o, "artScale", 1), flip = MiniJson.B(o, "flip"), shade = MiniJson.F(o, "shade"),
                };
                b.parentIndex = string.IsNullOrEmpty(b.parent) ? -1 : bones.FindIndex(x => x.id == b.parent);
                bones.Add(b);
            }
            idle = ReadLoop(MiniJson.O(root, "idle"));
            hold = ReadLoop(MiniJson.O(root, "hold"));
            raise = ReadShot(MiniJson.O(root, "raise"));

            partTemplate.gameObject.SetActive(false);
            foreach (Bone b in bones)
            {
                if ((b.part == null || !parts.ContainsKey(b.part)) && (b.talkPart == null || !parts.ContainsKey(b.talkPart))) continue;
                b.sr = Instantiate(partTemplate, transform);
                b.sr.name = b.id;
                b.sr.sortingOrder = b.order;
                b.sr.gameObject.SetActive(true);
            }
            ang = new float[bones.Count];
            world = new Matrix[bones.Count];
        }

        int BoneIndex(string id) => bones.FindIndex(b => b.id == id);

        Loop ReadLoop(Dictionary<string, object> j)
        {
            var l = new Loop();
            if (j == null) return l;
            l.length = MiniJson.F(j, "length", 1);
            l.rootX = ReadWave(MiniJson.O(j, "rootX"), -1);
            l.rootY = ReadWave(MiniJson.O(j, "rootY"), -1);
            foreach (object o in MiniJson.A(j, "bones") ?? new List<object>())
            {
                int i = BoneIndex(MiniJson.S(o, "bone"));
                if (i >= 0) l.bones.Add(ReadWave(o as Dictionary<string, object>, i));
            }
            return l;
        }

        static Wave ReadWave(Dictionary<string, object> j, int bone) => j == null ? null : new Wave
        {
            bone = bone, offset = MiniJson.F(j, "offset"), a1 = MiniJson.F(j, "a1"), p1 = MiniJson.F(j, "p1"), a2 = MiniJson.F(j, "a2"), p2 = MiniJson.F(j, "p2"),
        };

        Shot ReadShot(Dictionary<string, object> j)
        {
            if (j == null) return null;
            var s = new Shot
            {
                length = MiniJson.F(j, "length"), peakTime = MiniJson.F(j, "peakTime"), settleTime = MiniJson.F(j, "settleTime"),
                keepEnd = MiniJson.B(j, "keepEnd"), easeIn = MiniJson.B(j, "easeIn"),
            };
            foreach (object o in MiniJson.A(j, "bones") ?? new List<object>())
            {
                int i = BoneIndex(MiniJson.S(o, "bone"));
                if (i >= 0) s.bones.Add((i, MiniJson.F(o, "start"), MiniJson.F(o, "peak"), MiniJson.F(o, "settle")));
            }
            return s;
        }

        static float ShotValue(Shot m, float t, float a, float b, float c)
        {
            if (m.peakTime > 0 && t < m.peakTime) { float u = t / m.peakTime; if (m.easeIn) u = 1 - (1 - u) * (1 - u); return a + (b - a) * u; }
            if (t < m.settleTime) return b + (c - b) * (m.settleTime > m.peakTime ? (t - m.peakTime) / (m.settleTime - m.peakTime) : 1);
            if (m.keepEnd || m.length <= m.settleTime) return c;
            return c + (0 - c) * Mathf.Clamp01((t - m.settleTime) / (m.length - m.settleTime));
        }

        struct Matrix
        {
            public float a, b, c, d, e, f;
            public static Matrix operator *(Matrix m, Matrix n) => new()
            {
                a = m.a * n.a + m.c * n.b, b = m.b * n.a + m.d * n.b, c = m.a * n.c + m.c * n.d, d = m.b * n.c + m.d * n.d,
                e = m.a * n.e + m.c * n.f + m.e, f = m.b * n.e + m.d * n.f + m.f,
            };
            public static Matrix T(float x, float y) => new() { a = 1, d = 1, e = x, f = y };
            public static Matrix R(float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new() { a = c, b = s, c = -s, d = c }; }
            public static Matrix S(float x, float y) => new() { a = x, d = y };
            public Vector2 Apply(Vector2 p) => new(a * p.x + c * p.y + e, b * p.x + d * p.y + f);
        }

        // dt 만큼 진행해 자세를 계산하고 렌더러에 적용한다. highlight = 가까이 왔을 때 밝기 배율.
        public void Animate(float dt, float highlight)
        {
#if UNITY_EDITOR
            reloadCheck -= dt;
            if (reloadCheck <= 0) { reloadCheck = .5f; Load(); }
#else
            if (loadedText == null) Load();
#endif
            if (bones.Count == 0) return;
            time += dt;
            if (Talking && !wasTalking) talkStart = time;
            wasTalking = Talking;
            TalkWeight += ((Talking ? 1 : 0) - TalkWeight) * Mathf.Min(1, dt * talkFollow);
            float talkT = time - talkStart, talkW = TalkWeight;

            Vector2 rootOffset = Vector2.zero;
            for (int i = 0; i < ang.Length; i++) ang[i] = 0;
            void AddLoop(Loop m, float w)
            {
                if (m == null || w <= 0) return;
                float u = m.length > 0 ? Mathf.Repeat(time, m.length) / m.length : 0;
                if (m.rootX != null) rootOffset.x += m.rootX.At(u) * w;
                if (m.rootY != null) rootOffset.y += m.rootY.At(u) * w;
                foreach (Wave bw in m.bones) ang[bw.bone] += bw.At(u) * w;
            }
            AddLoop(idle, 1);
            AddLoop(hold, talkW);
            if (raise != null && talkW > 0)
            {
                float tt = Mathf.Min(talkT, raise.length);
                foreach (var (bone, start, peak, settle) in raise.bones) ang[bone] += ShotValue(raise, tt, start, peak, settle) * talkW;
            }

            for (int i = 0; i < bones.Count; i++)
            {
                Bone b = bones[i];
                Matrix m = b.parentIndex < 0 ? Matrix.T(rootPos.x + rootOffset.x, rootPos.y + rootOffset.y) : world[b.parentIndex] * Matrix.T(b.at.x, b.at.y);
                m = m * Matrix.R(b.rest + ang[i]);
                if (b.scale != 1) m = m * Matrix.S(b.scale, b.scale);
                world[i] = m;
            }

            bool raised = raise == null || talkT >= raise.peakTime * swapAfter;
            bool showTalk = talkW > swapAt && (raised || !Talking);
            float ppu = Space.Ppu > 0 ? Space.Ppu : 100;
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < bones.Count; i++)
            {
                Bone b = bones[i];
                if (b.sr == null) continue;
                string key = showTalk && b.talkPart != null && parts.ContainsKey(b.talkPart) ? b.talkPart : b.part;
                if (key == null || !parts.TryGetValue(key, out Part part)) { b.sr.enabled = false; continue; }
                b.sr.enabled = true;
                Place(b.sr, DrawMatrix(world[i], b, part), part.sprite, ppu);
                b.sr.color = tint * (b.shade > 0 ? Color.Lerp(Color.white, new Color(.16f, .12f, .2f), b.shade) : Color.white);
                b.sr.GetPropertyBlock(block);
                block.SetFloat(BrightId, highlight * brightness);
                block.SetFloat(SatId, saturation);
                b.sr.SetPropertyBlock(block);
            }
        }

        static Matrix DrawMatrix(Matrix bone, Bone b, Part part)
        {
            Matrix m = bone;
            if (b.artScale != 1) m = m * Matrix.S(b.artScale, b.artScale);
            if (b.flip) m = m * Matrix.S(-1, 1);
            if (part.rot != 0) m = m * Matrix.R(part.rot);
            if (part.scale != 1) m = m * Matrix.S(part.scale, part.scale);
            return m * Matrix.T(-part.pivot.x, -part.pivot.y);
        }

        static void Place(SpriteRenderer sr, Matrix m, Sprite sprite, float ppu)
        {
            sr.sprite = sprite;
            Rect rect = sprite.rect;
            Vector2 pivotImg = new(sprite.pivot.x, rect.height - sprite.pivot.y);
            Vector2 p = m.Apply(pivotImg);
            float la = m.a, lb = -m.b, lc = -m.c, ld = m.d;
            float sx = Mathf.Sqrt(la * la + lb * lb);
            float det = la * ld - lb * lc;
            float sy = sx > 1e-6f ? det / sx : 0;
            float angle = Mathf.Atan2(lb, la) * Mathf.Rad2Deg;
            float unit = sprite.pixelsPerUnit / ppu;
            Transform t = sr.transform;
            t.localPosition = new Vector3(p.x / ppu, -p.y / ppu, 0);
            t.localRotation = Quaternion.Euler(0, 0, angle);
            t.localScale = new Vector3(sx * unit, sy * unit, 1);
        }
    }
}
