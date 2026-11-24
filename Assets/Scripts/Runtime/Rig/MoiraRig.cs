// 19차 다락의 예견가 모이라(거미) 리그. 리깅 페이지(tools/rig_editor/moira.html)의 계산을 C# 으로 그대로 옮긴 것이다.
//   데이터 = 페이지가 저장한 Assets/Rig/moira_rig.json (TextAsset, 그대로 읽음). 그림 = 페이지가 쓰는 Assets/sprites/moira_*.png 그대로.
//   씬에 뼈를 구워 두지 않는다. 그림이 있는 뼈마다 partTemplate(SpriteRenderer, MCP 로 만든 템플릿)을 한 번 복제하고(템플릿 슬롯 복제),
//   매 프레임 페이지와 같은 순서로 계산해 그 렌더러의 자리/회전/크기를 정한다:
//     pose   : idle 반복 + hold 반복 × 말 비중 + raise(시작 → 최고 → 유지) × 말 비중
//     worlds : 뼈 i = 부모 · 이동(at) · 회전(rest + 동작) · 배율(scale) (뿌리 = 이동(rootPos + 동작 root))
//     그리기 : 뼈 변환 · artScale · flip(pivot 세로축 거울) · 파츠 rot · 파츠 scale · 이동(-pivot), 순서(order) 오름차순
//     (22차) + touchLoop 반복 × 만지기 비중 + touch(시작 → 최고 → 유지) × 만지기 비중. 만지는 만큼 말(카드 들기) 비중은 빠진다.
//   말하는 동안 talkPart 가 있는 뼈는 그 그림으로 바뀐다(카드 넷을 쥔 손). 바뀌는 때 = 말 비중 > swapAt 이고 raise 최고 시점 × swapAfter 뒤.
//   페이지에서 저장하면 에디터에서는 바로 다시 읽는다(플레이 중에도). 페이지 계산을 바꾸면 이 파일도 같이 바꾼다.
// 좌표: 그림 px, 아래 +y, 각도 시계 방향 +(도). Unity 로 옮길 때 y 와 회전 방향을 뒤집는다.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOD
{
    public class MoiraRig : MonoBehaviour
    {
        [Header("리깅 페이지 데이터 (그대로)")]
        public TextAsset rigJson;             // Assets/Rig/moira_rig.json
        public Sprite[] sprites;              // 파츠 그림 (이름 = JSON parts.file 의 파일 이름)
        public SpriteRenderer partTemplate;   // 뼈마다 복제할 렌더러 템플릿 (꺼 둠)

        [Header("점성구")]
        public string ballBone = "ball";
        public float ballPulse = .12f, ballSpeed = 1.3f;

        [Header("방에 어울리게 (방 조명에 맞춘 색, 바닥 그림자, 점성구 빛)")]
        public Color tint = Color.white;      // 모든 파츠에 곱하는 색 (방의 보랏빛 어둠)
        [Range(0, 2)] public float saturation = 1, brightness = 1;   // SpriteAlpha _Sat, _Bright
        public SpriteRenderer shadow;         // 탁자 밑 바닥 그림자 (흰 사각 + _Shape 1 = 부드러운 타원)
        public Vector2 shadowCenter, shadowSize;   // 그림 px (리그 원점 = 탁자 다리 아래 가운데)
        public Color shadowColor = new(0, 0, 0, .6f);
        public SpriteRenderer glow;           // 점성구 빛 (더하기 재질 + _Shape 1)
        public Vector2 glowOffset, glowSize;  // 점성구 뼈 원점에서 (그림 px), 크기 (그림 px)
        public Color glowColor = new(.6f, .45f, .9f, .35f);

        public bool Talking { get; set; }
        public float TalkWeight { get; private set; }
        public float height { get { Load(); return rigHeight; } }   // 리그 높이 (그림 px). 시설 height 에 맞춰 배율을 정한다
        float rigHeight = 430;
        public Vector3 CardPoint { get; private set; }
        // 20차 운명 읽기: 점성구 그림의 월드 가운데, 크기, 그림. BallOut 이면 탁자 위 점성구와 빛을 감춘다(UI 의 구가 대신 날아감).
        public Vector3 BallPoint { get; private set; }
        public Vector3 BallSize { get; private set; }
        public Sprite BallSprite { get; private set; }
        public bool BallOut { get; set; }

        [Header("22차 미래 보기: 고해상도 점성구 (FateOrbView 가 재질 값을 넣음)")]
        public SpriteRenderer fateOrb;        // Touching 동안 탁자 점성구 대신 그리는 큰 그림(moira_ball_big) 렌더러. 순서는 점성구 뼈와 같아 앞다리 손이 구슬 앞에 온다
        public bool Touching { get; set; }    // 앉아서 두 앞다리로 점성구를 감싸 만진다 (리깅 페이지 '구슬 만지기')
        public float TouchWeight { get; private set; }
        public bool FateOrbOn { get; set; }   // 탁자 점성구를 fateOrb 로 바꿔 그린다
        public float BallLift { get; set; }   // 점성구가 떠오른 높이 (그림 px)
        public float Motion { get; set; } = 1; // 반복 동작(숨쉬기, 손 흔들림, 점성구 맥박)이 흐르는 빠르기. 0 이면 지금 자세 그대로 멈춘다 (미래 보기 영상 동안)

        // ---------------------------------------------------------------- JSON → 리그
        class Part { public Sprite sprite; public Vector2 pivot; public float rot, scale = 1; public Vector2? cardPoint; }
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
        Loop idle, hold, touchLoop;
        Shot raise, touch;
        Vector2 rootPos;
        float swapAt = .35f, swapAfter = .8f, talkFollow = 6;
        string cardBone;
        string loadedText;
        float time, loopTime, talkStart = -99, touchStart = -99, reloadCheck;
        bool wasTalking, wasTouching;
        float[] ang;
        Matrix[] world;
        MaterialPropertyBlock block;
        static readonly int BrightId = Shader.PropertyToID("_Bright"), SatId = Shader.PropertyToID("_Sat"), ShapeId = Shader.PropertyToID("_Shape");

        void Load()
        {
            string text = rigJson != null ? rigJson.text : null;
#if UNITY_EDITOR
            // 에디터: 파일을 바로 읽는다 (리깅 페이지가 저장한 뒤 Unity 가 다시 가져오기 전에도 반영)
            string path = rigJson != null ? UnityEditor.AssetDatabase.GetAssetPath(rigJson) : null;
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) text = System.IO.File.ReadAllText(path);
#endif
            if (string.IsNullOrEmpty(text) || text == loadedText) return;
            loadedText = text;
            object root = MiniJson.Parse(text);
            foreach (Bone b in bones) if (b.sr != null) Destroy(b.sr.gameObject);
            bones.Clear();
            parts.Clear();
            rigHeight = MiniJson.F(root, "height", 430);
            float[] rp = MiniJson.V(root, "rootPos");
            rootPos = rp != null ? new Vector2(rp[0], rp[1]) : Vector2.zero;
            swapAt = MiniJson.F(root, "swapAt", .35f);
            swapAfter = MiniJson.F(root, "swapAfter", .8f);
            talkFollow = MiniJson.F(root, "talkFollow", 6);
            cardBone = MiniJson.S(root, "cardBone");

            var byName = (sprites ?? new Sprite[0]).Where(s => s != null).GroupBy(s => s.name).ToDictionary(g => g.Key, g => g.First());
            Dictionary<string, object> partsJson = MiniJson.O(root, "parts") ?? new Dictionary<string, object>();
            foreach (var pair in partsJson)
            {
                string file = MiniJson.S(pair.Value, "file") ?? "";
                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                if (!byName.TryGetValue(name, out Sprite sprite)) { Debug.LogWarning($"[TOD] 모이라 그림 없음: {name} (MoiraRig.sprites 에 넣어 주세요)"); continue; }
                float[] pv = MiniJson.V(pair.Value, "pivot");
                float[] cp = MiniJson.V(pair.Value, "cardPoint");
                parts[pair.Key] = new Part
                {
                    sprite = sprite, pivot = pv != null ? new Vector2(pv[0], pv[1]) : Vector2.zero,
                    rot = MiniJson.F(pair.Value, "rot"), scale = MiniJson.F(pair.Value, "scale", 1),
                    cardPoint = cp != null ? new Vector2(cp[0], cp[1]) : null,
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
            touchLoop = ReadLoop(MiniJson.O(root, "touchLoop"));
            touch = ReadShot(MiniJson.O(root, "touch"));

            // 그림이 있는 뼈마다 템플릿 복제 (order 가 그리기 순서)
            partTemplate.gameObject.SetActive(false);
            foreach (Bone b in bones)
            {
                if ((b.part == null || !parts.ContainsKey(b.part)) && (b.talkPart == null || !parts.ContainsKey(b.talkPart))) continue;
                b.sr = Instantiate(partTemplate, transform);
                b.sr.name = b.id;
                b.sr.sortingOrder = b.order;
                b.sr.gameObject.SetActive(true);
                b.sr.color = tint * (b.shade > 0 ? Color.Lerp(Color.white, new Color(.16f, .12f, .2f), b.shade) : Color.white);   // 방 색 × 뒤쪽 다리 그늘
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

        // 페이지 shotValue 와 같다
        static float ShotValue(Shot m, float t, float a, float b, float c)
        {
            if (m.peakTime > 0 && t < m.peakTime) { float u = t / m.peakTime; if (m.easeIn) u = 1 - (1 - u) * (1 - u); return a + (b - a) * u; }
            if (t < m.settleTime) return b + (c - b) * (m.settleTime > m.peakTime ? (t - m.peakTime) / (m.settleTime - m.peakTime) : 1);
            if (m.keepEnd || m.length <= m.settleTime) return c;
            return c + (0 - c) * Mathf.Clamp01((t - m.settleTime) / (m.length - m.settleTime));
        }

        // ---------------------------------------------------------------- 2×3 행렬 (페이지와 같은 [a b c d e f]: x' = a x + c y + e, y' = b x + d y + f)
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

        // ---------------------------------------------------------------- 매 프레임
        // dt 만큼 진행해 자세를 계산하고 렌더러에 적용한다. highlight = 가까이 왔을 때 밝기 배율.
        public void Animate(float dt, float highlight)
        {
#if UNITY_EDITOR
            reloadCheck -= dt;
            if (reloadCheck <= 0) { reloadCheck = .5f; Load(); }   // 리깅 페이지에서 저장하면 바로 반영
#else
            if (loadedText == null) Load();
#endif
            if (bones.Count == 0) return;
            time += dt;
            loopTime += dt * Mathf.Clamp01(Motion);
            if (Talking && !wasTalking) talkStart = time;
            wasTalking = Talking;
            TalkWeight += ((Talking ? 1 : 0) - TalkWeight) * Mathf.Min(1, dt * talkFollow);
            float talkT = time - talkStart;
            if (Touching && !wasTouching) touchStart = time;
            wasTouching = Touching;
            TouchWeight += ((Touching ? 1 : 0) - TouchWeight) * Mathf.Min(1, dt * talkFollow);
            float touchT = time - touchStart;
            float talkW = TalkWeight * (1 - TouchWeight);   // 만지는 만큼 카드 들기는 빠진다

            // pose
            Vector2 rootOffset = Vector2.zero;
            for (int i = 0; i < ang.Length; i++) ang[i] = 0;
            void AddLoop(Loop m, float w)
            {
                if (m == null || w <= 0) return;
                float u = m.length > 0 ? Mathf.Repeat(loopTime, m.length) / m.length : 0;
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
            AddLoop(touchLoop, TouchWeight);
            if (touch != null && TouchWeight > 0)
            {
                float tt = Mathf.Min(touchT, touch.length);
                foreach (var (bone, start, peak, settle) in touch.bones) ang[bone] += ShotValue(touch, tt, start, peak, settle) * TouchWeight;
            }

            // worlds
            for (int i = 0; i < bones.Count; i++)
            {
                Bone b = bones[i];
                Matrix m = b.parentIndex < 0 ? Matrix.T(rootPos.x + rootOffset.x, rootPos.y + rootOffset.y) : world[b.parentIndex] * Matrix.T(b.at.x, b.at.y);
                m = m * Matrix.R(b.rest + ang[i]);
                if (b.scale != 1) m = m * Matrix.S(b.scale, b.scale);
                world[i] = m;
            }

            // 그리기 (말할 때 그림 바꾸기 포함)
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
                Matrix bw = b.id == ballBone && BallLift != 0 ? world[i] * Matrix.T(0, -BallLift) : world[i];   // 22차: 떠오른 점성구
                Matrix m = DrawMatrix(bw, b, part);
                Place(b.sr, m, part.sprite, ppu);
                if (b.id == cardBone && b.talkPart != null && parts.TryGetValue(b.talkPart, out Part tp) && tp.cardPoint is Vector2 cp)
                {
                    Vector2 p = DrawMatrix(world[i], b, tp).Apply(cp);
                    CardPoint = transform.TransformPoint(new Vector3(p.x / ppu, -p.y / ppu, 0));
                }
                b.sr.color = tint * (b.shade > 0 ? Color.Lerp(Color.white, new Color(.16f, .12f, .2f), b.shade) : Color.white);   // 방 색 × 뒤쪽 다리 그늘 (인스펙터 값이 바로 반영되게 매 프레임)
                float bright = highlight * brightness;
                if (b.id == ballBone) bright *= 1 + ballPulse * Pulse;
                b.sr.GetPropertyBlock(block);
                block.SetFloat(BrightId, bright);
                block.SetFloat(SatId, saturation);
                b.sr.SetPropertyBlock(block);
                if (b.id == ballBone)
                {
                    Bounds bb = b.sr.bounds;
                    BallPoint = bb.center;
                    BallSize = bb.size;
                    BallSprite = part.sprite;
                    if (BallOut) b.sr.enabled = false;   // 20차 운명 읽기: 점성구가 화면 가운데로 나가 있는 동안
                    PlaceFateOrb(b, m, ppu);
                }
            }
            PlaceShadowAndGlow(ppu);
            if (BallOut && glow != null) glow.enabled = false;
        }

        float Pulse => .5f + .5f * Mathf.Sin(loopTime * ballSpeed * 2 * Mathf.PI);

        // 22차 미래 보기: 탁자 점성구(작은 그림)를 같은 자리, 같은 크기의 큰 그림(fateOrb.sprite)으로 바꿔 그린다. 확대해도 선명하고, 재질이 유리 안에 영상을 그린다.
        void PlaceFateOrb(Bone b, Matrix small, float ppu)
        {
            if (fateOrb == null) return;
            fateOrb.enabled = FateOrbOn && fateOrb.sprite != null && b.sr.sprite != null;
            if (!fateOrb.enabled) return;
            b.sr.enabled = false;
            fateOrb.sortingOrder = b.order;
            float k = b.sr.sprite.rect.width / fateOrb.sprite.rect.width;   // 큰 그림 1px = 작은 그림 k px
            Place(fateOrb, small * Matrix.S(k, k), fateOrb.sprite, ppu);
            fateOrb.color = tint;   // 방 색 (영상은 셰이더가 이 색을 곱하지 않는다)
            Bounds bb = fateOrb.bounds;
            BallPoint = bb.center;
            BallSize = bb.size;
        }

        // 바닥 그림자(탁자 밑 부드러운 타원)와 점성구 빛(점성구 둘레 보랏빛 더하기)
        void PlaceShadowAndGlow(float ppu)
        {
            if (shadow != null && shadowSize.x > 0)
            {
                shadow.enabled = true;
                shadow.color = shadowColor;
                Place(shadow, new Vector2(shadowCenter.x, shadowCenter.y), shadowSize, ppu);
            }
            int ball = BoneIndex(ballBone);
            if (glow != null && glowSize.x > 0 && ball >= 0)
            {
                Vector2 at = world[ball].Apply(glowOffset + new Vector2(0, -BallLift));
                Color c = glowColor;
                c.a *= .75f + .25f * Pulse;
                glow.enabled = true;
                glow.color = c;
                Place(glow, at, glowSize * (1 + .04f * Pulse), ppu);
            }
        }

        // 흰 사각 스프라이트를 그림 px 의 가운데·크기로 (_Shape 1 이면 부드러운 타원)
        void Place(SpriteRenderer sr, Vector2 center, Vector2 size, float ppu)
        {
            block ??= new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            block.SetFloat(ShapeId, sr.sprite != null && sr.sprite.name == "white" ? 1 : 0);   // 흰 사각이면 셰이더가 타원으로, soft_disc 면 그림 그대로
            sr.SetPropertyBlock(block);
            Rect rect = sr.sprite != null ? sr.sprite.rect : new Rect(0, 0, 1, 1);
            float unit = sr.sprite != null ? sr.sprite.pixelsPerUnit / ppu : 1;
            sr.transform.localPosition = new Vector3(center.x / ppu, -center.y / ppu, 0);
            sr.transform.localRotation = Quaternion.identity;
            sr.transform.localScale = new Vector3(size.x / rect.width * unit, size.y / rect.height * unit, 1);
        }

        // 페이지 그리기와 같은 순서: 뼈 · artScale · flip · 파츠 rot · 파츠 scale · 이동(-pivot)
        static Matrix DrawMatrix(Matrix bone, Bone b, Part part)
        {
            Matrix m = bone;
            if (b.artScale != 1) m = m * Matrix.S(b.artScale, b.artScale);
            if (b.flip) m = m * Matrix.S(-1, 1);
            if (part.rot != 0) m = m * Matrix.R(part.rot);
            if (part.scale != 1) m = m * Matrix.S(part.scale, part.scale);
            return m * Matrix.T(-part.pivot.x, -part.pivot.y);
        }

        // 그림(왼쪽 위 원점, 아래 +y) 변환 m → 스프라이트 렌더러의 로컬 자리/회전/크기 (Unity 는 위 +y, 반시계 +)
        void Place(SpriteRenderer sr, Matrix m, Sprite sprite, float ppu)
        {
            sr.sprite = sprite;
            Rect rect = sprite.rect;
            Vector2 pivotImg = new(sprite.pivot.x, rect.height - sprite.pivot.y);   // 스프라이트 피벗을 그림 좌표로
            Vector2 p = m.Apply(pivotImg);
            // y 를 뒤집은 선형부: [[a, -c], [-b, d]]
            float la = m.a, lb = -m.b, lc = -m.c, ld = m.d;
            float sx = Mathf.Sqrt(la * la + lb * lb);
            float det = la * ld - lb * lc;
            float sy = sx > 1e-6f ? det / sx : 0;
            float angle = Mathf.Atan2(lb, la) * Mathf.Rad2Deg;
            float unit = sprite.pixelsPerUnit / ppu;   // 그림 1px = 리그 1px
            Transform t = sr.transform;
            t.localPosition = new Vector3(p.x / ppu, -p.y / ppu, 0);
            t.localRotation = Quaternion.Euler(0, 0, angle);
            t.localScale = new Vector3(sx * unit, sy * unit, 1);
        }
    }
}
