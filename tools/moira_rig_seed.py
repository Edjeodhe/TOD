"""모이라 리그 첫 값: art_src/rig/moira_parts.json(굽기 결과) → art_src/rig/moira_rig.json

python tools/moira_rig_seed.py [--force]

참고 그림(hub_attic_moira.png, 통그림)에서 눈으로 잰 관절 자리(원본 1024 px 좌표)로 다리마다 소켓 → 무릎 → 손끝을 맞춘다.
리그 좌표 = 그림 px, 원점 = 탁자 다리 아래 가운데(바닥), 아래 +y. 원본 → 리그 = (원본 - (512, 920)) × K.
다리 8개는 윗다리/아랫다리 그림 한 벌을 같이 쓰고, 왼쪽은 뒤집는다(flip). 다리마다 배율 = 소켓~무릎 거리 / 윗다리 길이.
이 파일은 첫 값만 만든다. 뒤의 다듬기는 리깅 페이지(tools/rig_editor/moira.html)에서 하고 거기서 저장한다.
"""
import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RIG = ROOT / "art_src/rig"
K = .515


def rig(p):
    return [(p[0] - 512) * K, (p[1] - 920) * K]


def ang(v):
    """세로 아래(0, 1)를 v 방향으로 돌리는 각도 (시계 방향 +, 아래 +y 화면)."""
    return math.degrees(math.atan2(-v[0], v[1]))


def main():
    out = RIG / "moira_rig.json"
    if out.exists() and "--force" not in sys.argv:
        print("이미 있음 (덮어쓰려면 --force):", out)
        return
    parts = json.loads((RIG / "moira_parts.json").read_text(encoding="utf-8"))
    up, lo = parts["leg_upper"], parts["leg_lower"]
    up_len = up["end"][1] - up["pivot"][1]
    body_pivot = [4, -270]
    head_scale, ball_scale = .8, .76
    bones = [
        {"id": "seat", "parent": None, "part": None, "at": [0, 0], "rest": 0, "order": 0},
        {"id": "table", "parent": "seat", "part": "table", "at": [0, 0], "rest": 0, "order": 20},
        {"id": "ball", "parent": "seat", "part": "ball", "at": [-6, -158], "rest": 0, "order": 22, "scale": ball_scale},
        {"id": "body", "parent": "seat", "part": "body", "at": body_pivot, "rest": 0, "order": 10},
        {"id": "head", "parent": "body", "part": "head", "at": [0, 8], "rest": 0, "order": 14, "scale": head_scale},
    ]
    # 다리: 이름, 소켓(몸 그림 기준, 회전 중심에서), 무릎/손끝(원본 px), 그림 순서
    legs = [
        ("A", [62, 70], (650, 345), (596, 428), 24),    # 앞다리: 점성구 위 손
        ("B", [78, 100], (792, 420), (700, 520), 25),   # 둘째: 실을 쥔 손 (오른쪽이 카드 부채를 든다)
        ("C", [86, 135], (850, 560), (795, 640), 26),   # 셋째: 탁자 가장자리에 얹은 손
        ("D", [70, 40], (884, 372), (893, 700), 4),     # 뒷다리: 몸 뒤로 뻗어 바닥까지
    ]
    for name, socket, knee, tip, order in legs:
        for side in ("R", "L"):
            s = 1 if side == "R" else -1
            sock_local = [socket[0] * s, socket[1]]
            sock = [body_pivot[0] + sock_local[0], body_pivot[1] + sock_local[1]]
            k = rig(knee) if s > 0 else rig((1024 - knee[0], knee[1]))
            t = rig(tip) if s > 0 else rig((1024 - tip[0], tip[1]))
            v1 = [k[0] - sock[0], k[1] - sock[1]]
            v2 = [t[0] - k[0], t[1] - k[1]]
            a1 = ang(v1)
            a2 = ang(v2) - a1
            scale = math.hypot(*v1) / up_len
            bones.append({"id": f"{side}{name}_up", "parent": "body", "part": "leg_upper", "at": sock_local, "rest": round(a1, 1),
                          "order": order if name == "D" else order - 4, "scale": round(scale, 3), "flip": s < 0,
                          "shade": .35 if name == "D" else 0})
            bones.append({"id": f"{side}{name}_low", "parent": f"{side}{name}_up", "part": "leg_lower",
                          "at": [0, up_len], "rest": round(((a2 + 180) % 360) - 180, 1), "order": order + 1 if name == "D" else order,
                          "flip": s < 0, "snapToEnd": True, "shade": .35 if name == "D" else 0})
    lo_reach = lo["end"][1] - lo["pivot"][1]
    bones.append({"id": "fan", "parent": "RB_low", "part": "fan", "at": [0, lo_reach], "rest": 0, "order": 30, "scale": 1.6, "snapToEnd": True})

    def wave(bone, a1=0, p1=0, a2=0, p2=0, offset=0):
        return {"bone": bone, "offset": offset, "a1": a1, "p1": p1, "a2": a2, "p2": p2}

    idle = {"length": 3.2, "rootX": wave("")["offset"] and None, "rootY": None, "bones": [
        wave("body", 1.2), wave("head", 3, .25, .8, .1),
        wave("RA_up", 3, 0), wave("RA_low", 7, .1, 2, .3), wave("LA_up", -3, .5), wave("LA_low", -7, .6, -2, .8),
        wave("RB_up", 2, .2), wave("RB_low", 3, .3), wave("LB_up", -2, .7), wave("LB_low", -3, .8),
        wave("RC_low", 1.5, .4), wave("LC_low", -1.5, .9),
        wave("RD_up", 1.5, .15), wave("LD_up", -1.5, .65),
    ]}
    hold = {"length": 2.0, "bones": [wave("RB_low", 2.5, 0), wave("LA_low", 0, 0, 5, 0), wave("head", 2, .2)]}
    raise_ = {"length": .7, "peakTime": .38, "settleTime": .7, "keepEnd": True, "easeIn": True, "bones": [
        {"bone": "RB_up", "start": 0, "peak": -62, "settle": -55},
        {"bone": "RB_low", "start": 0, "peak": 70, "settle": 62},
        {"bone": "LA_up", "start": 0, "peak": 28, "settle": 22},
        {"bone": "LA_low", "start": 0, "peak": -40, "settle": -34},
        {"bone": "head", "start": 0, "peak": 7, "settle": 5},
    ]}
    data = {
        "height": 430,
        "rootPos": [0, 0],
        "fanBone": "fan",
        "talkFollow": 6,
        "reference": {"file": "Assets/sprites/hub_attic_moira.png", "x": 0, "y": 0, "scale": .66},
        "parts": parts,
        "bones": bones,
        "idle": {k: v for k, v in idle.items() if v is not None},
        "hold": hold,
        "raise": raise_,
    }
    out.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print("wrote", out, len(bones), "bones")


if __name__ == "__main__":
    main()
