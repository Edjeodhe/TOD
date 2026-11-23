"""달리기 사이클을 앞/뒤 다리 정확히 반 박자 대칭으로 다시 쓴다.

python tools/symmetric_run.py        → assets/rig/hero-quarter.rig.json 의 run 클립만 교체

통그림 자동 맞춤으로 만든 run 은 박자마다 따로 맞춰져서 좌우 다리·몸 높이가 어긋나 절뚝거렸다.
여기서는 손으로 만든 원래 사이클(tools/make_quarter_rig.py)의 앞다리 곡선 하나를 기준으로
뒷다리 = 같은 곡선을 반 박자 늦춘 것, 몸 들썩임 = 두 걸음이 똑같이 반복되도록 만든다.
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import rigfk  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
RIG = ROOT / "assets/rig/hero-quarter.rig.json"
LENGTH = .55
N = 8  # 한 사이클 키 수 (마지막 키 = 첫 키)

# 앞(near)쪽 한 사이클 곡선 — 원래 손으로 만든 값. 접지(0) → 낮음 → 통과 → 높음 → 반대발 접지 …
NEAR = {
    "thigh": [-26, -11, 9, 26, 23, 10, -17, -32],
    "shin":  [10, 6, 14, 31, 47, 48, 33, 15],
    "foot":  [8, 1, -9, -20, -29, -23, -8, 5],
    "arm":     [18, 12, 2, -16, -27, -19, -3, 12],
    "forearm": [-16, -14, -19, -26, -36, -32, -23, -17],
}
# 몸은 걸음마다(반 사이클) 똑같이 반복
STEP = {
    # 골반·몸통은 고정 기울기: 회전하면 큰 머리까지 지렛대처럼 흔들려 절뚝여 보인다.
    "hips": [0, 0, 0, 0], "torso": [12, 12, 12.5, 12], "head": [-7, -7, -7.5, -7],
    "coat": [12, 19, 21, 16], "near_hand": [7, 4, 0, -4],
    "tail_base": [6, 8, 13, 11], "tail_mid": [-10, -15, -14, -6], "tail_tip": [-3, -6, -12, -16],
}
ROOT_Y = [-5, -3, -8, -16]  # 접지 직후 낮고 → 밀어낼 때 높게
# 3/4 시점이라 뒷다리는 기본 각도가 앞다리와 다르다. 같은 곡선을 그대로 쓰면 뒷발로 디딜 때 몸이 더 꺼져
# 절뚝여 보이므로, 두 걸음의 몸 높이가 같아지도록 수치로 찾은 보정값을 더한다 (rig.html 에서 측정).
FAR_OFFSET = {"thigh": 8, "shin": 4, "foot": -5}
FAR_DEPTH = -23  # 뒷발이 닿는 바닥 높이 (리그 단위, 앞발 = 4)


def half(values):
    """반 박자(4키) 늦춘 곡선"""
    return values[N // 2:] + values[:N // 2]


def track(values):
    t = [round(LENGTH * i / N, 4) for i in range(N + 1)]
    return [[ti, v] for ti, v in zip(t, values + values[:1])]


def build(offset):
    keys = {}
    for part, curve in NEAR.items():
        keys["near_" + part] = track(curve)
        # 팔은 같은 쪽 다리와 반대로 흔든다: 뒤쪽 팔 = 앞쪽 팔 곡선을 반 박자 늦춘 것
        keys["far_" + part] = track([v + offset.get(part, 0) for v in half(curve)])
    for part, curve in STEP.items():
        keys[part] = track(curve + curve)
    # 겉옷 앞자락·치마는 뒷자락 흔들림을 작게 따라간다 (v3 리그에만 있는 파츠)
    ids = {q["id"] for q in rig["parts"]}
    for part, k in (("coat_front_near", .45), ("coat_front_far", .35), ("skirt", -.12)):
        if part in ids:
            keys[part] = [[t, round(v * k, 1)] for t, v in keys["coat"]]
    return keys


root = [[t, 0, y] for t, y in track(ROOT_Y + ROOT_Y)]
rig = json.loads(RIG.read_text(encoding="utf-8"))
far_foot = next(f for f in rig["feet"] if f["id"] == "far_foot")


def asymmetry(c):
    """두 걸음(반 사이클 차이)의 골반 높이 차 제곱 평균"""
    far_foot["depth"] = c[3]
    rig["anims"]["run"] = {"length": LENGTH, "loop": True, "interp": "linear",
                           "keys": build({"thigh": c[0], "shin": c[1], "foot": c[2]}), "root": root}
    n = 32
    h = [rigfk.world(rigfk.grounded(rig, *rigfk.pose(rig, "run", LENGTH * i / n)), "hips")[1] for i in range(n)]
    return sum((h[i] - h[i + n // 2]) ** 2 for i in range(n // 2)) / (n // 2)


# 리그가 바뀌면(하체 파츠 등) 보정값도 달라지므로 매번 다시 찾는다. 기본값에서 출발하는 좌표 하강.
c = [FAR_OFFSET["thigh"], FAR_OFFSET["shin"], FAR_OFFSET["foot"], FAR_DEPTH]
best = asymmetry(c)
for step in (8, 4, 2, 1):
    for _ in range(6):
        improved = False
        for k in range(4):
            for d in (1, -1):
                n = list(c)
                n[k] += d * step * (2 if k == 3 else 1)
                s = asymmetry(n)
                if s < best:
                    best, c, improved = s, n, True
        if not improved:
            break
FAR_OFFSET = {"thigh": c[0], "shin": c[1], "foot": c[2]}
far_foot["depth"] = c[3]
keys = build(FAR_OFFSET)
print("far offset", FAR_OFFSET, "far depth", c[3], "asymmetry rms", round(best ** .5, 2))
# 접지 보정은 35%만 적용 (나머지는 사이클 평균에 고정) → 걸음마다 몸이 크게 들썩이지 않는다.
rig.setdefault("groundDamp", {})["run"] = 0.35
rig["anims"]["run"] = {"length": LENGTH, "loop": True, "interp": "linear", "keys": keys, "root": root}
RIG.write_text(json.dumps(rig, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
print("run rewritten:", ", ".join(sorted(keys)))
