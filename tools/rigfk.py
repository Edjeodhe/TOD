"""리그 순운동학 (src/rig/runtime.js 와 같은 계산) — 오프라인 도구에서 발 접지·대칭을 계산할 때 쓴다."""
import json
import math
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
RIG_PATH = ROOT / "assets/rig/hero-quarter.rig.json"


def load(path=RIG_PATH):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def save(rig, path=RIG_PATH):
    Path(path).write_text(json.dumps(rig, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")


def sample(track, t, interp="linear"):
    if not track:
        return None
    if len(track) == 1 or t <= track[0][0]:
        return track[0][1:]
    for a, b in zip(track, track[1:]):
        if t < b[0]:
            if interp == "step":
                return a[1:]
            u = (t - a[0]) / (b[0] - a[0])
            return [x + (y - x) * u for x, y in zip(a[1:], b[1:])]
    return track[-1][1:]


def pose(rig, clip=None, t=0.0):
    anim = rig["anims"].get(clip) if clip else None
    if anim:
        t = min(t, anim["length"]) if anim.get("loop") is False else t % anim["length"]
    interp = (anim or {}).get("interp", "step")
    ang = {}
    for p in rig["parts"]:
        v = sample(anim["keys"].get(p["id"]), t, interp) if anim else None
        ang[p["id"]] = (p.get("rest") or 0) + (v[0] if v else 0)
    r = sample(anim.get("root"), t, interp) if anim else None
    return ang, (r if r else [0, 0])


def matrices(rig, ang, root, base=np.eye(3)):
    by = {p["id"]: p for p in rig["parts"]}
    out = {}

    def visit(pid):
        if pid in out:
            return out[pid]
        p = by[pid]
        if not p.get("parent"):
            o = rig.get("rootPos", [0, 0])
            m = base @ translate(o[0] + root[0], o[1] + root[1])
        else:
            par = by[p["parent"]]
            m = visit(par["id"]) @ translate(p["attach"][0] - par["pivot"][0], p["attach"][1] - par["pivot"][1])
        m = m @ rotate(ang[pid]) @ scale(p.get("scale") or 1)
        out[pid] = m
        return m

    for p in rig["parts"]:
        visit(p["id"])
    return out


def translate(x, y):
    return np.array([[1, 0, x], [0, 1, y], [0, 0, 1.0]])


def rotate(deg):
    a = math.radians(deg)
    return np.array([[math.cos(a), -math.sin(a), 0], [math.sin(a), math.cos(a), 0], [0, 0, 1.0]])


def scale(s):
    return np.array([[s, 0, 0], [0, s, 0], [0, 0, 1.0]])


def world(M, pid, local=(0, 0)):
    v = M[pid] @ np.array([local[0], local[1], 1.0])
    return float(v[0]), float(v[1])


def ground_offset(rig, M):
    return max(world(M, f["id"], f["sole"])[1] - f["depth"] for f in rig["feet"])


def grounded(rig, ang, root):
    M = matrices(rig, ang, root)
    off = max(-46, min(46, ground_offset(rig, M)))
    return matrices(rig, ang, root, translate(0, -off)) if abs(off) > .01 else M
