"""웹 리그 키프레임(art_src/rig/hero-quarter.rig.json, 웹 rig.html 에서 만든 원본)을 Unity C# 동작 파라미터로 한 번 변환한다.

python tools/fit_hero_motion.py  ->  Temp/hero_motion.json (MCP 로 씬 HeroRig 에 넣는 값)

- 반복 동작(idle, run): 뼈마다 offset + a1·sin(2π(u+p1)) + a2·sin(4π(u+p2)), u = t/length
- 한 번 동작(shoot, pull, dash, hurt, die): 시작 자세(0초) → 최고 자세(peak) → 정리 자세(settle) → 0 (keepEnd 면 마지막 자세 유지)
"""
import json
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
rig = json.loads((ROOT / "art_src/rig/hero-quarter.rig.json").read_text(encoding="utf-8"))

ONE_SHOT = {   # 이름: (peak, settle, keepEnd, easeIn)
    "shoot": (0.065, 0.13, False, False),
    "pull": (0.192, 0.288, False, False),
    "dash": (0.145, 0.2175, False, True),
    "hurt": (0.0, 0.145, False, False),
    "die": (0.275, 0.55, True, False),
}


def sample(track, t):
    ts = [k[0] for k in track]
    return [float(np.interp(t, ts, [k[i] for k in track])) for i in range(1, len(track[0]))]


def fit_loop(track, length):
    ts = np.array([k[0] for k in track[:-1]]) / length       # 마지막 키 = 첫 키 반복
    vs = np.array([k[1] for k in track[:-1]])
    # 키 사이를 촘촘히 채워(선형 보간) 맞춘다
    u = np.linspace(0, 1, 64, endpoint=False)
    v = np.interp(u, np.append(ts, 1), np.append(vs, vs[0]))
    A = np.stack([np.ones_like(u), np.sin(2 * np.pi * u), np.cos(2 * np.pi * u), np.sin(4 * np.pi * u), np.cos(4 * np.pi * u)], 1)
    c, *_ = np.linalg.lstsq(A, v, rcond=None)
    a1, a2 = np.hypot(c[1], c[2]), np.hypot(c[3], c[4])
    p1 = np.arctan2(c[2], c[1]) / (2 * np.pi)           # sin(x + φ) = sin x cos φ + cos x sin φ
    p2 = np.arctan2(c[4], c[3]) / (4 * np.pi)
    err = float(np.abs(A @ c - v).max())
    return {"offset": round(float(c[0]), 3), "a1": round(float(a1), 3), "p1": round(float(p1), 4), "a2": round(float(a2), 3), "p2": round(float(p2), 4)}, err


out = {"loops": {}, "shots": {}}
worst = []
for name in ("idle", "run"):
    a = rig["anims"][name]
    bones = []
    for bone, track in a["keys"].items():
        w, err = fit_loop(track, a["length"])
        w["bone"] = bone
        bones.append(w)
        worst.append((err, name, bone))
    root = a.get("root") or [[0, 0, 0], [a["length"], 0, 0]]
    rx, ex = fit_loop([[k[0], k[1]] for k in root], a["length"])
    ry, ey = fit_loop([[k[0], k[2]] for k in root], a["length"])
    worst += [(ex, name, "rootX"), (ey, name, "rootY")]
    out["loops"][name] = {"length": a["length"], "bones": bones, "rootX": rx, "rootY": ry}

for name, (peak, settle, keep, ease) in ONE_SHOT.items():
    a = rig["anims"][name]
    bones = []
    for bone, track in a["keys"].items():
        s0, p, st = sample(track, 0)[0], sample(track, peak)[0], sample(track, settle)[0]
        bones.append({"bone": bone, "start": round(s0, 2), "peak": round(p, 2), "settle": round(st, 2)})
    root = a.get("root") or [[0, 0, 0]]
    out["shots"][name] = {
        "length": a["length"], "peakTime": peak, "settleTime": settle, "keepEnd": keep, "easeIn": ease,
        "rootStart": [round(v, 2) for v in sample(root, 0)], "rootPeak": [round(v, 2) for v in sample(root, peak)],
        "rootSettle": [round(v, 2) for v in sample(root, settle)], "bones": bones,
    }

out["rig"] = {k: rig[k] for k in ("rootPos", "feet", "groundDamp")}
out["parts"] = rig["parts"]
(ROOT / "Temp").mkdir(exist_ok=True)
(ROOT / "Temp/hero_motion.json").write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
worst.sort(reverse=True)
print("largest loop fit errors (deg):", [(round(e, 1), n, b) for e, n, b in worst[:6]])
