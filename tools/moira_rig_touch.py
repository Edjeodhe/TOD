"""모이라 리그: 22차 '구슬 만지기' 자세 (미래 보기). 두 앞다리(RA, LA)의 손을 점성구 유리 양옆에 얹는다 (두 마디 IK).
Assets/Rig/moira_rig.json 의 touch(시작 → 최고 → 유지)와 touchLoop(만지는 동안 손끝이 쓰다듬듯 조금씩)를 고쳐 쓴다.

python tools/moira_rig_touch.py
python tools/preview_moira_rig.py Temp/moira_touch.png --touch --bones

- 점성구 유리 가운데 = ball 뼈 + (0, -78) 그림 px (moira_ball.png 의 유리 원, 반지름 약 67). 미래 보기 때 구슬은 LIFT 만큼 떠오른다.
- 손(아랫다리 end) → 유리 양옆 HANDS. 무릎은 BEND 쪽(+1 바깥, -1 안쪽)으로 굽힌다. 최고 자세는 정리 자세의 OVERSHOOT 배.
- 머리는 HEAD 도만큼 구슬 쪽으로 숙인다.
값을 다듬은 뒤에는 리깅 페이지(moira.html)에서 '구슬 만지기'로 보고 손봐도 된다(저장하면 Unity 가 바로 읽음).
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PATH = ROOT / "Assets/Rig/moira_rig.json"
LIFT = 22                      # 미래 보기 때 구슬이 떠오르는 높이 (그림 px, 데이터 hub.fateShow.orbLift 와 맞춤)
GLASS = (0, -78)               # ball 뼈 원점 → 유리 가운데
RADIUS = 67
HANDS = {"RA": ((RADIUS * .86, 18), 1), "LA": ((-RADIUS * .86, 18), -1)}   # 유리 가운데에서 손 자리, 무릎 굽힘
OVERSHOOT = 1.08
HEAD = 6


def main():
    rig = json.loads(PATH.read_text(encoding="utf-8"))
    by = {b["id"]: b for b in rig["bones"]}
    up, lo = rig["parts"]["leg_upper"], rig["parts"]["leg_lower"]
    body, ball = by["body"], by["ball"]
    center = (ball["at"][0] + GLASS[0], ball["at"][1] + GLASS[1] - LIFT)

    def world_angle(bid):
        a, b = 0.0, by[bid]
        while b:
            a += b.get("rest", 0)
            b = by.get(b.get("parent")) if b.get("parent") else None
        return a

    body_ang = world_angle("body")
    bones = [{"bone": "head", "start": 0, "peak": HEAD * 1.3, "settle": HEAD}]
    for leg, (off, bend) in HANDS.items():
        target = (center[0] + off[0], center[1] + off[1])
        u, l = by[f"{leg}_up"], by[f"{leg}_low"]
        s = u.get("scale", 1)
        L1 = (up["end"][1] - up["pivot"][1]) * s
        L2 = (lo["end"][1] - lo["pivot"][1]) * s
        sock = (body["at"][0] + u["at"][0], body["at"][1] + u["at"][1])
        dx, dy = target[0] - sock[0], target[1] - sock[1]
        d = max(1e-3, min(math.hypot(dx, dy), L1 + L2 - 1e-3))
        base = math.degrees(math.atan2(-dx, dy))
        cos_a = (L1 * L1 + d * d - L2 * L2) / (2 * L1 * d)
        a = math.degrees(math.acos(max(-1, min(1, cos_a))))
        up_world = base + bend * a
        kx = sock[0] - L1 * math.sin(math.radians(up_world))
        ky = sock[1] + L1 * math.cos(math.radians(up_world))
        lo_world = math.degrees(math.atan2(-(target[0] - kx), target[1] - ky))
        du = (((up_world - body_ang) - u["rest"] + 180) % 360) - 180
        dl = (((lo_world - up_world) - l["rest"] + 180) % 360) - 180
        bones.append({"bone": f"{leg}_up", "start": 0, "peak": round(du * OVERSHOOT, 1), "settle": round(du, 1)})
        bones.append({"bone": f"{leg}_low", "start": 0, "peak": round(dl * OVERSHOOT, 1), "settle": round(dl, 1)})
        print(leg, "target", [round(v) for v in target], "knee", round(kx), round(ky), "up", round(du, 1), "low", round(dl, 1))
    rig["touch"] = {"length": .9, "peakTime": .5, "settleTime": .9, "keepEnd": True, "easeIn": True, "bones": bones}
    rig.setdefault("touchLoop", {"length": 2.6, "bones": [
        {"bone": "RA_low", "offset": 0, "a1": 2.2, "p1": 0, "a2": 0, "p2": 0},
        {"bone": "LA_low", "offset": 0, "a1": -2.2, "p1": .5, "a2": 0, "p2": 0},
        {"bone": "head", "offset": 0, "a1": 1.2, "p1": .25, "a2": 0, "p2": 0},
    ]})
    PATH.write_text(json.dumps(rig, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
