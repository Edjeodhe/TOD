"""모이라 리그: '카드 들기' 자세를 손 자리로 정한다 (두 마디 IK). art_src/rig/moira_rig.json 의 raise 를 고쳐 쓴다.

python tools/moira_rig_ik.py

- RB(오른쪽 둘째 다리) 손 → FAN_HAND. 말하는 동안 RB_low 그림이 카드 넷을 쥔 손(hand_cards)으로 바뀐다.
- RA(오른쪽 앞다리) 손끝 → POINT, 부채를 가리킨다 (밈의 '함정 카드' 자세).
무릎은 BEND 쪽(+1 바깥, -1 안쪽)으로 굽힌다. 최고 자세는 정리 자세의 OVERSHOOT 배.
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PATH = ROOT / "art_src/rig/moira_rig.json"
FAN_HAND = (178, -262)
FAN_TILT = -10
POINT = (112, -262)
OVERSHOOT = 1.12
TARGETS = {"RB": (FAN_HAND, 1), "RA": (POINT, -1)}


def main():
    rig = json.loads(PATH.read_text(encoding="utf-8"))
    by = {b["id"]: b for b in rig["bones"]}
    up, lo = rig["parts"]["leg_upper"], rig["parts"]["leg_lower"]
    body = by["body"]
    raise_ = {bp["bone"]: bp for bp in rig["raise"]["bones"]}

    def world_angle(bid):
        a, b = 0.0, by[bid]
        while b:
            a += b.get("rest", 0)
            b = by.get(b.get("parent")) if b.get("parent") else None
        return a

    body_ang = world_angle("body")
    for leg, (target, bend) in TARGETS.items():
        u, l = by[f"{leg}_up"], by[f"{leg}_low"]
        s = u.get("scale", 1)
        L1 = (up["end"][1] - up["pivot"][1]) * s
        L2 = (lo["end"][1] - lo["pivot"][1]) * s
        sock = (body["at"][0] + u["at"][0], body["at"][1] + u["at"][1])   # 몸이 0° 일 때
        dx, dy = target[0] - sock[0], target[1] - sock[1]
        d = min(math.hypot(dx, dy), L1 + L2 - 1e-3)
        # 아래(0,1)를 v 로 돌리는 각 (시계 +): atan2(-vx, vy)
        base = math.degrees(math.atan2(-dx, dy))
        cos_a = (L1 * L1 + d * d - L2 * L2) / (2 * L1 * d)
        a = math.degrees(math.acos(max(-1, min(1, cos_a))))
        up_world = base + bend * a
        # 무릎 자리 → 손 방향
        kx = sock[0] - L1 * math.sin(math.radians(up_world))
        ky = sock[1] + L1 * math.cos(math.radians(up_world))
        lo_world = math.degrees(math.atan2(-(target[0] - kx), target[1] - ky))
        up_local = up_world - body_ang
        lo_local = lo_world - up_world
        du = ((up_local - u["rest"] + 180) % 360) - 180
        dl = ((lo_local - l["rest"] + 180) % 360) - 180
        raise_[f"{leg}_up"] = {"bone": f"{leg}_up", "start": 0, "peak": round(du * OVERSHOOT, 1), "settle": round(du, 1)}
        raise_[f"{leg}_low"] = {"bone": f"{leg}_low", "start": 0, "peak": round(dl * OVERSHOOT, 1), "settle": round(dl, 1)}
        print(leg, "knee", round(kx), round(ky), "up", round(du, 1), "low", round(dl, 1))
        if leg == "RB" and "fan" in by:
            by["fan"]["rest"] = round(FAN_TILT - lo_world, 1)
    raise_.pop("LA_up", None)
    raise_.pop("LA_low", None)
    rig["raise"]["bones"] = list(raise_.values())
    PATH.write_text(json.dumps(rig, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
