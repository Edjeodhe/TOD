"""몬스터 리그 미리보기: art_src/rig/enemy_rigs.json 의 뼈대·동작을 MonsterRig 와 같은 계산으로 그려 본다.

python tools/preview_enemy_rig.py <몬스터> <동작> <출력.png> [프레임 수]
  동작: rest | idle | walk | melee | shot | dash | charge | strike | fire | brake | hurt

json 은 씬 조립(MCP)의 첫 값일 뿐이며, 조립한 뒤에는 씬(Arena/Enemies/Rigs)이 원본이다.
각도는 캔버스 기준(시계 방향 +, 도), 위치는 그림 px(아래 +y), 원점은 발밑.
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SPEC = json.loads((ROOT / "art_src/rig/enemy_rigs.json").read_text(encoding="utf-8"))


def wave(w, u):
    return w.get("offset", 0) + w.get("a1", 0) * math.sin(2 * math.pi * (u + w.get("p1", 0))) \
        + w.get("a2", 0) * math.sin(4 * math.pi * (u + w.get("p2", 0)))


def loop_pose(m, t, pose, weight=1):
    u = (t % m["length"]) / m["length"] if m.get("length") else 0
    if "rootX" in m: pose["root"][0] += wave(m["rootX"], u) * weight
    if "rootY" in m: pose["root"][1] += wave(m["rootY"], u) * weight
    for w in m.get("bones", []):
        pose["ang"][w["bone"]] = pose["ang"].get(w["bone"], 0) + wave(w, u) * weight


def shot_value(m, t, a, b, c):
    peak, settle, length = m.get("peakTime", 0), m.get("settleTime", 0), m["length"]
    if peak > 0 and t < peak:
        u = t / peak
        if m.get("easeIn"): u = 1 - (1 - u) ** 2
        return a + (b - a) * u
    if t < settle:
        return b + (c - b) * ((t - peak) / (settle - peak) if settle > peak else 1)
    if m.get("keepEnd") or length <= settle: return c
    return c + (0 - c) * min(1, max(0, (t - settle) / (length - settle)))


def vec(v):
    return [v["x"], v["y"]] if isinstance(v, dict) else (v or [0, 0])


def shot_pose(m, t, pose):
    t = min(max(t, 0), m["length"])
    for i, k in enumerate("xy"):
        pose["root"][i] += shot_value(m, t, vec(m.get("rootStart"))[i], vec(m.get("rootPeak"))[i], vec(m.get("rootSettle"))[i])
    for b in m.get("bones", []):
        pose["ang"][b["bone"]] = pose["ang"].get(b["bone"], 0) + shot_value(m, t, b.get("start", 0), b.get("peak", 0), b.get("settle", 0))


def mat_mul(m, n):
    a, b, c, d, e, f = m
    A, B, C, D, E, F = n
    return (a * A + c * B, b * A + d * B, a * C + c * D, b * C + d * D, a * E + c * F + e, b * E + d * F + f)


def rot(deg):
    r = math.radians(deg)
    return (math.cos(r), math.sin(r), -math.sin(r), math.cos(r), 0, 0)


def tr(x, y):
    return (1, 0, 0, 1, x, y)


def bone_world(spec, pose, origin, scale, lift=0.0):
    world = {}
    for b in spec["bones"]:
        parent = world.get(b.get("parent"), tr(*origin))
        at = b.get("at", [0, 0])
        if b.get("parent") is None:
            at = [spec["rootPos"][0] + pose["root"][0], spec["rootPos"][1] + pose["root"][1] + lift]
        m = mat_mul(parent, tr(at[0] * scale, at[1] * scale))
        world[b["id"]] = mat_mul(m, rot(b.get("rest", 0) + pose["ang"].get(b["id"], 0)))
    return world


def render(spec, pose, canvas, origin, scale=1.0):
    world = bone_world(spec, pose, origin, scale)
    follow = spec.get("groundFollow", 0)
    if spec.get("feet") and follow > 0:
        # MonsterRig.Ground 와 같은 계산: 가장 낮은 발바닥을 바닥에 맞춘다
        pivots = {b["id"]: b["pivot"] for b in spec["bones"] if b.get("part")}
        lowest = -1e9
        for f in spec["feet"]:
            m = world[f["bone"]]
            px, py = pivots[f["bone"]]
            x, y = (f["sole"][0] - px) * scale, (f["sole"][1] - py) * scale
            wy = m[1] * x + m[3] * y + m[5]
            lowest = max(lowest, (wy - origin[1]) / scale - f.get("depth", 0))
        lift = max(-spec.get("maxGroundCorrection", 60), min(spec.get("maxGroundCorrection", 60), -lowest * follow))
        world = bone_world(spec, pose, origin, scale, lift)
    draws = [(b.get("order", 0), b, world[b["id"]]) for b in spec["bones"] if b.get("part")]
    for _, b, m in sorted(draws, key=lambda x: x[0]):
        im = Image.open(ROOT / f"Assets/sprites/enemy_{spec['name']}_{b['part']}.png").convert("RGBA")
        if b.get("shade"):
            dark = Image.new("RGBA", im.size, (22, 19, 27, 255))
            mixed = Image.blend(im, dark, b["shade"])
            mixed.putalpha(im.getchannel("A"))
            im = mixed
        px, py = b["pivot"]
        # 그림 좌표 p → 화면: M · (p - pivot)·scale. PIL 은 역변환을 받는다.
        full = mat_mul(m, mat_mul((scale, 0, 0, scale, 0, 0), tr(-px, -py)))
        a, bb, c, d, e, f = full
        det = a * d - bb * c
        inv = (d / det, -bb / det, -c / det, a / det, (c * f - d * e) / det, (bb * e - a * f) / det)
        layer = im.transform(canvas.size, Image.AFFINE, (inv[0], inv[2], inv[4], inv[1], inv[3], inv[5]), Image.BICUBIC)
        canvas.alpha_composite(layer)
    return world


def main():
    name, motion, out = sys.argv[1], sys.argv[2], sys.argv[3]
    frames = int(sys.argv[4]) if len(sys.argv) > 4 else (1 if motion == "rest" else 8)
    spec = SPEC[name]
    spec["name"] = name
    scale = 150 / max(300, spec.get("height", 300)) * 1.6
    cell_w, cell_h = 300, 330
    sheet = Image.new("RGBA", (cell_w * frames, cell_h), (58, 54, 66, 255))
    draw = ImageDraw.Draw(sheet)
    m = spec.get(motion, {})
    for k in range(frames):
        pose = {"root": [0, 0], "ang": {}}
        t = 0
        if motion in ("idle", "walk"):
            t = m["length"] * k / frames
            loop_pose(m, t, pose)
        elif motion != "rest":
            t = m["length"] * k / max(1, frames - 1)
            shot_pose(m, t, pose)
        cell = Image.new("RGBA", (cell_w, cell_h), (0, 0, 0, 0))
        origin = (cell_w / 2, cell_h - 30)
        render(spec, pose, cell, origin, scale)
        if motion == "rest":
            box = cell.getchannel("A").point(lambda v: 255 if v > 40 else 0).getbbox()
            print(name, "rest height (그림 px)", round((origin[1] - box[1]) / scale), "width", round((box[2] - box[0]) / scale))
        sheet.alpha_composite(cell, (k * cell_w, 0))
        gy = cell_h - 30
        draw.line([(k * cell_w, gy), (k * cell_w + cell_w, gy)], fill=(140, 200, 190, 255))
        ht = spec.get("height", 0) * scale
        draw.line([(k * cell_w + 10, gy - ht), (k * cell_w + 40, gy - ht)], fill=(255, 220, 0, 255))
        draw.text((k * cell_w + 4, 4), f"{motion} t={t:.2f}", fill=(255, 255, 255, 255))
    sheet.save(out)


if __name__ == "__main__":
    main()
