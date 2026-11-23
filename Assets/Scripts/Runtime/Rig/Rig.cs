// 종이 인형 리그 런타임 (웹판 src/rig/runtime.js). 좌표는 캔버스(아래로 +y) 기준이며 뷰가 Unity 좌표로 바꾼다.
using System;
using System.Collections.Generic;
using System.Linq;
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

    public class RigPart
    {
        public string id, img, parent;
        public Vector2 pivot, attach;
        public float z, rest, scale = 1, shade;
        public bool hidden;
        public int index, parentIndex = -1;
    }

    public class RigClip
    {
        public float length;
        public bool loop = true;
        public bool linear;
        public float[][][] keys;          // [partIndex][key][t, deg]
        public float[][] root;            // [key][t, x, y]
    }

    public class RigFoot { public int part; public Vector2 sole; public float depth; }

    public class RigPose
    {
        public float[] ang;
        public Vector2 root;
        public RigPose(int count) { ang = new float[count]; }
    }

    public class Rig
    {
        public RigPart[] parts;
        public Dictionary<string, RigPart> byId;
        public Dictionary<string, RigClip> clips = new();
        public RigFoot[] feet = Array.Empty<RigFoot>();
        public Dictionary<string, float> groundDamp = new();
        public Vector2 rootPos;
        public float stepFps;
        public int[] drawOrder;          // z 오름차순 파츠 인덱스
        readonly Dictionary<string, float> groundMean = new();
        int[] visitOrder;                // 부모가 먼저 오는 순서

        public static Rig Load(string json)
        {
            var data = (Dictionary<string, object>)MiniJson.Parse(json);
            var rig = new Rig();
            var parts = (List<object>)data["parts"];
            rig.parts = parts.Select((o, i) =>
            {
                var p = (Dictionary<string, object>)o;
                return new RigPart
                {
                    index = i, id = (string)p["id"], img = p.TryGetValue("img", out object img) ? img as string : null,
                    parent = p.TryGetValue("parent", out object parent) ? parent as string : null,
                    pivot = Vec(p, "pivot"), attach = Vec(p, "attach"), z = Num(p, "z"), rest = Num(p, "rest"),
                    scale = p.ContainsKey("scale") && p["scale"] != null ? Num(p, "scale") : 1, shade = Num(p, "shade"),
                    hidden = p.TryGetValue("hidden", out object h) && h is true,
                };
            }).ToArray();
            rig.byId = rig.parts.ToDictionary(p => p.id);
            foreach (RigPart p in rig.parts) if (p.parent != null) p.parentIndex = rig.byId[p.parent].index;
            rig.rootPos = Vec(data, "rootPos");
            rig.stepFps = Num(data, "stepFps");
            if (data.TryGetValue("feet", out object feet) && feet is List<object> footList)
            {
                rig.feet = footList.Cast<Dictionary<string, object>>()
                    .Select(f => new RigFoot { part = rig.byId[(string)f["id"]].index, sole = Vec(f, "sole"), depth = Num(f, "depth") }).ToArray();
            }
            if (data.TryGetValue("groundDamp", out object damp) && damp is Dictionary<string, object> dampMap)
            {
                foreach (var (clip, value) in dampMap) rig.groundDamp[clip] = Convert.ToSingle(value);
            }
            foreach (var (name, value) in (Dictionary<string, object>)data["anims"])
            {
                var anim = (Dictionary<string, object>)value;
                var clip = new RigClip
                {
                    length = Num(anim, "length"), loop = !(anim.TryGetValue("loop", out object loop) && loop is false),
                    linear = anim.TryGetValue("interp", out object interp) && interp as string == "linear",
                    keys = new float[rig.parts.Length][][],
                };
                if (anim.TryGetValue("keys", out object keys) && keys is Dictionary<string, object> keyMap)
                {
                    foreach (var (partId, track) in keyMap)
                    {
                        if (rig.byId.TryGetValue(partId, out RigPart part)) clip.keys[part.index] = Track(track);
                    }
                }
                if (anim.TryGetValue("root", out object root)) clip.root = Track(root);
                rig.clips[name] = clip;
            }
            rig.drawOrder = rig.parts.OrderBy(p => p.z).ThenBy(p => p.index).Select(p => p.index).ToArray();
            var order = new List<int>();
            void Visit(RigPart p) { if (order.Contains(p.index)) return; if (p.parentIndex >= 0) Visit(rig.parts[p.parentIndex]); order.Add(p.index); }
            foreach (RigPart p in rig.parts) Visit(p);
            rig.visitOrder = order.ToArray();
            return rig;
        }

        static float Num(Dictionary<string, object> o, string key) => o.TryGetValue(key, out object v) && v != null ? Convert.ToSingle(v) : 0;
        static Vector2 Vec(Dictionary<string, object> o, string key)
        {
            if (!o.TryGetValue(key, out object v) || v is not List<object> list || list.Count < 2) return Vector2.zero;
            return new Vector2(Convert.ToSingle(list[0]), Convert.ToSingle(list[1]));
        }
        static float[][] Track(object value) => ((List<object>)value).Select(k => ((List<object>)k).Select(Convert.ToSingle).ToArray()).ToArray();

        static float[] Sample(float[][] track, float t, bool linear, float[] fallback)
        {
            if (track == null || track.Length == 0) return fallback;
            if (track.Length == 1 || t <= track[0][0]) return track[0];
            for (int i = 0; i < track.Length - 1; i++)
            {
                float[] a = track[i], b = track[i + 1];
                if (t < b[0])
                {
                    if (!linear) return a;
                    float u = (t - a[0]) / (b[0] - a[0]);
                    var result = new float[a.Length];
                    result[0] = t;
                    for (int k = 1; k < a.Length; k++) result[k] = a[k] + (b[k] - a[k]) * u;
                    return result;
                }
            }
            return track[^1];
        }

        public static float ClipTime(RigClip clip, float t)
        {
            if (clip == null) return 0;
            return !clip.loop ? Mathf.Min(t, clip.length) : ((t % clip.length) + clip.length) % clip.length;
        }

        public RigPose Pose(string clipName, float t, float? stepFpsOverride = null)
        {
            clips.TryGetValue(clipName, out RigClip clip);
            var pose = new RigPose(parts.Length);
            float tt = ClipTime(clip, t);
            float step = stepFpsOverride ?? stepFps;
            if (step > 0) tt = Mathf.Floor(tt * step) / step;
            for (int i = 0; i < parts.Length; i++)
            {
                float[] v = clip != null ? Sample(clip.keys[i], tt, clip.linear, null) : null;
                pose.ang[i] = parts[i].rest + (v != null ? v[1] : 0);
            }
            float[] r = clip != null ? Sample(clip.root, tt, clip.linear, null) : null;
            if (r != null) pose.root = new Vector2(r[1], r[2]);
            return pose;
        }

        public Affine[] Matrices(RigPose pose, Affine @base)
        {
            var m = new Affine[parts.Length];
            foreach (int i in visitOrder)
            {
                RigPart p = parts[i];
                Affine local;
                if (p.parentIndex < 0) local = @base.Translate(rootPos.x + pose.root.x, rootPos.y + pose.root.y);
                else
                {
                    RigPart parent = parts[p.parentIndex];
                    local = m[p.parentIndex].Translate(p.attach.x - parent.pivot.x, p.attach.y - parent.pivot.y);
                }
                local = local.Rotate(pose.ang[i]);
                if (p.scale != 1) local = local.Scale(p.scale, p.scale);
                m[i] = local;
            }
            return m;
        }

        float GroundOffset(Affine[] m, Affine @base)
        {
            float best = float.NegativeInfinity;
            foreach (RigFoot foot in feet)
            {
                float soleY = m[foot.part].Apply(foot.sole).y;
                best = Mathf.Max(best, (soleY - @base.f) / @base.d - foot.depth);
            }
            return best;
        }

        float GroundMean(string clipName)
        {
            if (groundMean.TryGetValue(clipName, out float mean)) return mean;
            RigClip clip = clips[clipName];
            float sum = 0;
            for (int i = 0; i < 16; i++) sum += GroundOffset(Matrices(Pose(clipName, clip.length * i / 16, 0), Affine.Identity), Affine.Identity);
            return groundMean[clipName] = sum / 16;
        }

        // 발이 바닥에 닿도록 몸 높이를 보정한다 (clip 의 groundDamp 만큼만, 사이클 평균 기준).
        public Affine[] GroundedMatrices(RigPose pose, Affine @base, string clipName)
        {
            Affine[] m = Matrices(pose, @base);
            if (feet.Length == 0) return m;
            float offset = GroundOffset(m, @base);
            if (clipName != null && groundDamp.TryGetValue(clipName, out float damp) && clips.ContainsKey(clipName))
            {
                float mean = GroundMean(clipName);
                offset = mean + (offset - mean) * damp;
            }
            float correction = Mathf.Clamp(offset, -46, 46);
            return Mathf.Abs(correction) < .01f ? m : Matrices(pose, @base.Translate(0, -correction));
        }

        public int Index(string id) => byId.TryGetValue(id, out RigPart p) ? p.index : -1;
    }
}
