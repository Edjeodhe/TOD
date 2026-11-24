"""23차 쉬어가기 NPC 리그 첫 값: art_src/rig/npc_<id>_parts.json(굽기 결과) → Assets/Rig/npc_<id>.json

python tools/npc_rig_seed.py [id ...] [--force]

관절은 굽힌 파츠 그림(tools/bake_npc_parts.py, 배율 .5)에서 하나하나 잰 값이다 (몸 그림의 어깨 둥근 소켓, 목 탭, 꼬리 뿌리 등).
리그 좌표 = 그림 px, 원점 = 발밑 가운데(바닥), 아래 +y, 각도 시계 방향 +(도). 몸 뼈는 몸 그림의 pivot 을 원점(서 있는 몸 = 발밑)에 둔다.
그래서 몸에 붙는 뼈의 자리(at) = 몸 그림에서 잰 점 - 몸 pivot 이다.
팔은 한 벌(arm_upper, arm_lower)을 양쪽이 같이 쓰고 화면 왼쪽 팔은 뒤집는다(flip). 각도: 아래로 곧게 그린 마디가 + 이면 화면 왼쪽으로, - 이면 오른쪽으로 돈다.
움직임: idle(숨쉬기, 흔들림) 반복, hold(말하는 동안 반복), raise(말 걸면 시작 → 최고 → 유지). talkPart = 말하는 동안 바뀌는 그림.
참고 그림(reference) = 캐릭터 시안 (페이지에서 겹쳐 보기).
이 파일은 첫 값만 만든다(이미 있으면 --force). 다듬기는 리깅 페이지(tools/rig_editor/rig.html)에서 하고 거기서 저장한다.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RIG = ROOT / "art_src/rig"


def wave(bone, a1=0, p1=0, a2=0, p2=0, offset=0):
    return {"bone": bone, "offset": offset, "a1": a1, "p1": p1, "a2": a2, "p2": p2}


def shot(bone, peak, settle=None, start=0):
    return {"bone": bone, "start": start, "peak": peak, "settle": peak if settle is None else settle}


def local(parts, part, p):
    """몸 그림의 점 p → 몸 뼈 기준 자리 (pivot 을 뺌)."""
    pv = parts[part]["pivot"]
    return [round(p[0] - pv[0], 1), round(p[1] - pv[1], 1)]


def arms(parts, body, left, right, scale, rests, order, extra=None):
    """양팔: left/right = 몸 그림의 어깨 소켓, rests = ((왼 위, 왼 아래), (오른 위, 오른 아래))."""
    up = parts["arm_upper"]
    up_len = up["end"][1] - up["pivot"][1]
    up_dx = up["end"][0] - up["pivot"][0]
    out = []
    for side, sock, (r_up, r_low) in (("L", left, rests[0]), ("R", right, rests[1])):
        flip = side == "L"
        out.append({"id": f"{side}_up", "parent": body, "part": "arm_upper", "at": local(parts, body, sock), "rest": r_up,
                    "order": order, "scale": scale, "flip": flip})
        low = {"id": f"{side}_low", "parent": f"{side}_up", "part": "arm_lower", "at": [-up_dx if flip else up_dx, up_len], "rest": r_low,
               "order": order + 1, "flip": flip, "snapToEnd": True}
        if extra and side in extra:
            low.update(extra[side])
        out.append(low)
    return out


def palm(parts, part="arm_lower", flip=False):
    p = parts[part]
    dx = p["end"][0] - p["pivot"][0]
    return [-dx if flip else dx, p["end"][1] - p["pivot"][1]]


# ---------------------------------------------------------------- 수녀 재봉사 (축복)
def nun(parts):
    parts["head"]["pivot"] = [86, 178]          # 목 탭 아래 둥근 끝 (두건 안쪽 목)
    body = "body"
    bones = [
        {"id": "root", "parent": None, "part": None, "at": [0, 0], "rest": 0, "order": 0},
        {"id": "body", "parent": "root", "part": "body", "at": [0, 0], "rest": 0, "order": 10},
        {"id": "veil", "parent": "body", "part": "veil", "at": local(parts, body, [136, 14]), "rest": 0, "order": 2, "scale": .78, "shade": .25},
        {"id": "head", "parent": "body", "part": "head", "at": local(parts, body, [136, 30]), "rest": 0, "order": 20, "scale": .62},
    ]
    # 어깨: 마네킹 같은 좁은 어깨 둥근 소켓 (116, 30), (173, 30) 보다 조금 바깥 (넓은 소매가 어깨를 덮게)
    bones += arms(parts, body, [116, 30], [171, 30], .7, ((24, -16), (-16, 10)), 13)
    bones.append({"id": "rosary", "parent": "L_low", "part": "rosary", "at": palm(parts, flip=True), "rest": -8, "order": 16, "scale": .68, "snapToEnd": True})
    idle = {"length": 4.2, "rootY": None, "bones": [
        wave("body", .8), wave("head", 2.2, .2, .6, .1), wave("veil", -1, .35),
        wave("L_up", 1.5, .1), wave("L_low", 2, .2), wave("rosary", 4, .35, 1.2, .1),
        wave("R_up", -1.5, .6), wave("R_low", -2.5, .7),
    ]}
    hold = {"length": 1.6, "bones": [wave("R_low", 5, 0), wave("head", 1.5, .25)]}
    raise_ = {"length": .8, "peakTime": .45, "settleTime": .8, "keepEnd": True, "easeIn": True, "bones": [
        shot("R_up", -62, -56), shot("R_low", -78, -70), shot("head", 6, 4), shot("body", -1.5, -1), shot("L_up", 6, 4)]}
    return {"height": 400, "bones": bones, "idle": idle, "hold": hold, "raise": raise_}


# ---------------------------------------------------------------- 누더기 행상 쥐 (조각 선물)
def rat(parts):
    body = "body"
    bones = [
        {"id": "root", "parent": None, "part": None, "at": [0, 0], "rest": 0, "order": 0},
        {"id": "body", "parent": "root", "part": "body", "at": [0, 0], "rest": 0, "order": 10},
        {"id": "tail", "parent": "body", "part": "tail", "at": local(parts, body, [72, 175]), "rest": 38, "order": 1, "scale": .8},
        {"id": "bundle", "parent": "body", "part": "bundle", "at": local(parts, body, [38, 70]), "rest": -10, "order": 3, "scale": .78},
        {"id": "head", "parent": "body", "part": "head", "at": local(parts, body, [100, 26]), "rest": 0, "order": 20, "scale": .8},
    ]
    # 어깨 둥근 소켓 (49.5, 30), (154.5, 30)
    bones += arms(parts, body, [52, 34], [152, 34], .5, ((10, -38), (-28, -52)), 14, extra={"R": {"talkPart": "arm_piece"}})
    idle = {"length": 2.6, "bones": [
        wave("body", 1.2, 0, .4, .2), wave("head", 3, .15, 1, .4), wave("bundle", -1.5, .3), wave("tail", 6, .1, 2, .5),
        wave("L_up", 2, .1), wave("L_low", 3, .2), wave("R_up", -2, .5), wave("R_low", -4, .6),
    ]}
    hold = {"length": 1.2, "bones": [wave("R_low", 4, 0), wave("head", 2, .3), wave("tail", 5, .2)]}
    raise_ = {"length": .7, "peakTime": .38, "settleTime": .7, "keepEnd": True, "easeIn": True, "bones": [
        shot("R_up", -46, -40), shot("R_low", -48, -42), shot("head", -6, -4), shot("body", 2, 1.5)]}
    return {"height": 380, "bones": bones, "idle": idle, "hold": hold, "raise": raise_}


# ---------------------------------------------------------------- 촛불 유령 (경험치)
def ghost(parts):
    parts["head"]["pivot"] = [78, 182]          # 촛농 목 가운데 (아래로 흘러내린 방울이 아니라)
    parts["arm_upper"]["end"] = [46, 104]       # 늘어진 소매 안, 팔꿈치 자리 (자동으로 찾은 끝은 소매 끝자락)
    parts["arm_wisp"]["end"] = [35, 112]        # 기억 불씨를 받친 손바닥 (자동으로 찾은 끝은 불씨 방울)
    float_h = 34
    body_top = -(parts["body"]["h"] - 6 + float_h)
    body = "body"
    bones = [
        {"id": "root", "parent": None, "part": None, "at": [0, 0], "rest": 0, "order": 0},
        {"id": "body", "parent": "root", "part": "body", "at": [0, body_top], "rest": 0, "order": 10},
        {"id": "head", "parent": "body", "part": "head", "at": local(parts, body, [94, 22]), "rest": 0, "order": 20, "scale": .78},
        {"id": "flame", "parent": "head", "part": "flame", "at": [96 - 78, 12 - 182], "rest": 0, "order": 21, "scale": .9},
    ]
    # 소매가 나오는 로브 어깨 (40, 60), (150, 60)
    bones += arms(parts, body, [42, 62], [148, 62], .62, ((38, 62), (-38, -62)), 12, extra={"L": {"talkPart": "arm_wisp"}})
    idle = {"length": 3.4, "rootY": wave("", 6, 0, 1.5, .3), "rootX": wave("", 2, .25), "bones": [
        wave("body", 1.5, .1), wave("head", 2.5, .3, .8, .1), wave("flame", 5, 0, 3, .2),
        wave("L_up", 3, .2), wave("L_low", 4, .3), wave("R_up", -3, .7), wave("R_low", -4, .8),
    ]}
    for w in (idle["rootY"], idle["rootX"]):
        w.pop("bone")
    hold = {"length": 1.4, "bones": [wave("flame", 8, 0, 4, .1), wave("L_low", 4, 0)]}
    raise_ = {"length": .9, "peakTime": .5, "settleTime": .9, "keepEnd": True, "easeIn": True, "bones": [
        shot("L_up", 30, 26), shot("L_low", 10, 8), shot("head", -5, -3), shot("R_up", -10, -8)]}
    return {"height": 420, "bones": bones, "idle": idle, "hold": hold, "raise": raise_}


# ---------------------------------------------------------------- 단추 눈 인형 (도박)
def doll(parts):
    parts["head"]["pivot"] = [180, 216]         # 목 탭 아래 (자동으로 찾은 아래 끝은 털실 머리카락)
    body = "body"
    bones = [
        {"id": "root", "parent": None, "part": None, "at": [0, 0], "rest": 0, "order": 0},
        {"id": "body", "parent": "root", "part": "body", "at": [0, 0], "rest": 0, "order": 10},
        {"id": "head", "parent": "body", "part": "head", "at": local(parts, body, [102, 22]), "rest": 0, "order": 20, "scale": .62},
    ]
    # 어깨 둥근 소켓 (19.5, 42.5), (187, 42.5): 손바닥이 위로 오게 팔꿈치를 굽혀 든다
    bones += arms(parts, body, [24, 44], [182, 44], .62, ((40, 140), (-40, -140)), 14)
    # 주사위 셋: 머리 위 저글링. 회전 축(juggle_N, 그림 없음)이 몸의 가슴 높이에 있고, 주사위는 축에서 R 위에 매달려
    # 축이 좌우로 크게 흔들리면 손에서 손으로 호를 그린다. 셋은 위상이 1/3 씩 다르다.
    for i, phase in enumerate((0, 1 / 3, 2 / 3)):
        bones.append({"id": f"juggle_{i}", "parent": "body", "part": None, "at": local(parts, body, [102, 30]), "rest": 0, "order": 0})
        bones.append({"id": f"die_{i}", "parent": f"juggle_{i}", "part": "die", "at": [0, -180], "rest": 0, "order": 30, "scale": .56})
    idle = {"length": 1.5, "bones": [
        wave("body", 1.5, 0, .5, .25), wave("head", 4, .1, 1.5, .3),
        wave("L_up", 0, 0, 3, 0), wave("L_low", 0, 0, 6, .05), wave("R_up", 0, 0, -3, .5), wave("R_low", 0, 0, -6, .55),
    ] + [wave(f"juggle_{i}", 62, ph) for i, ph in enumerate((0, 1 / 3, 2 / 3))]
      + [wave(f"die_{i}", 0, 0, 40, ph) for i, ph in enumerate((0, 1 / 3, 2 / 3))]}
    hold = {"length": .75, "bones": [wave("head", 5, 0)]}
    raise_ = {"length": .6, "peakTime": .3, "settleTime": .6, "keepEnd": True, "easeIn": True, "bones": [
        shot("head", 10, 6), shot("L_up", 12, 8), shot("R_up", -12, -8), shot("body", -2, -1)]}
    return {"height": 360, "bones": bones, "idle": idle, "hold": hold, "raise": raise_}


SEEDS = {"nun": nun, "rat": rat, "ghost": ghost, "doll": doll}
# 참고 그림(시안 1024 px): 발밑이 그림 아래에서 얼마나 위인지(px), 리그 높이에 맞춘 배율
REFS = {"nun": (34, .41), "rat": (24, .40), "ghost": (60, .45), "doll": (30, .36)}


def main():
    names = [a for a in sys.argv[1:] if not a.startswith("--")] or list(SEEDS)
    for npc in names:
        out = ROOT / f"Assets/Rig/npc_{npc}.json"
        if out.exists() and "--force" not in sys.argv:
            print("이미 있음 (덮어쓰려면 --force):", out)
            continue
        parts = json.loads((RIG / f"npc_{npc}_parts.json").read_text(encoding="utf-8"))
        rig = SEEDS[npc](parts)
        lift, scale = REFS[npc]
        data = {
            "name": npc,
            "height": rig["height"],
            "rootPos": [0, 0],
            "talkFollow": 6,
            "swapAt": .35,
            "swapAfter": .6,
            "reference": {"file": f"art_src/raw/npc_{npc}.png", "x": 0, "y": round(lift * scale, 1), "scale": scale},
            "parts": parts,
            "bones": rig["bones"],
            "idle": {k: v for k, v in rig["idle"].items() if v is not None},
            "hold": rig["hold"],
            "raise": rig["raise"],
        }
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
        print("wrote", out, len(rig["bones"]), "bones")


if __name__ == "__main__":
    main()
