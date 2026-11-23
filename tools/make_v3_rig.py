"""통그림 비율(v3) 파츠 아틀라스 → 리그. 할핀(황동 점)을 자동 검출해 관절을 잡는다.

python tools/make_v3_rig.py            art_src/raw/hero_v3_atlas.png → assets/sprites/hero_v3_*.png + assets/rig/hero-quarter.rig.json
python tools/make_v3_rig.py --preview  조각 번호·할핀 위치를 표시한 확인용 그림만 저장 (scratch/_v3_preview.png)

계층 (겉옷을 따로 걸친 구조):
  hips ─┬─ skirt                       하의 (치마)
        ├─ near/far thigh ─ shin(부츠) ─ foot(부츠)
        ├─ tail_base ─ tail_mid ─ tail_tip
        └─ torso (조끼+스카프, 가는 상체) ─┬─ head
                                           ├─ near/far arm ─ forearm ─ hand ─ (needle)
                                           ├─ coat_shoulders (깃·어깨, 몸통 위에 걸침)
                                           ├─ coat_front_near / coat_front_far (앞자락, 어깨에서 늘어짐)
                                           └─ coat (뒷자락)
기존 클립(idle/run/shoot/pull/hurt/die/dash)은 파츠 id 가 같으므로 그대로 옮긴다.
다음: python tools/symmetric_run.py
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
import rigfk  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
ATLAS = ROOT / "art_src/raw/hero_v3_atlas.png"
SPRITES = ROOT / "assets/sprites"
NAMES = [["head", "torso", "coat_shoulders"],
         ["coat_front_near", "coat_front_far", "coat_back", "skirt"],
         ["uarm", "farm", "hand", "thigh", "shin", "foot"],
         ["tail", "needle"]]
TARGET_HEIGHT = 960      # 조립한 캐릭터 키 (리그 단위) — 게임 화면 크기 유지
# 통그림(대기 포즈) 비율 = 머리 55% · 몸통 30% · 다리 15%. 아틀라스 조각은 같은 배율로 그려져 있으므로
# 구간별 배율로 맞춘다 (측정: 조정 전 머리 36% · 몸통 34% · 다리 34%).
HEAD_SCALE = 1.74
NEW_HEAD_GROW = 1.0   # 목 없는 새 머리 크기 미세 조정 (1 = 이전 머리와 같은 그림 높이)
NECK_SINK = 32        # 머리 할핀을 몸통 목 할핀보다 얼마나 아래(깃 안쪽)에 꽂을지 (몸통 그림 px)
# 레이어 순서 (뒤 -> 앞): 꼬리 · 뒷자락 · 뒤팔 · 뒤 밑단 · 뒷다리 · 앞다리 · 치마 · 몸통(조끼) · 앞 밑단 · 코트 몸판 · 머리 · 앞팔(바늘)
Z = {"tail_base": -15, "tail_mid": -14.9, "tail_tip": -14.8, "coat": -12,
     "far_arm": -11, "far_forearm": -10.9, "far_hand": -10.8, "coat_front_far": -9,
     "far_thigh": -8, "far_shin": -7.9, "far_foot": -7.8, "near_thigh": -7, "near_shin": -6.9, "near_foot": -6.8,
     "skirt": -5, "torso": -3, "coat_front_near": -2, "coat_body": 0, "head": 5,
     "near_arm": 6, "near_forearm": 6.2, "needle": 6.4, "near_hand": 6.6}
LEG_SCALE = .5
ARM_SCALE = .72
FAR_SHADE = .27


def cut_pieces():
    rgb = np.asarray(Image.open(ATLAS).convert("RGB")).astype(np.int16)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    alpha = np.clip((95 - (g - np.maximum(r, b))) / 55, 0, 1)
    mask = alpha > .5
    lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=7))
    sizes = ndimage.sum(mask, lab, range(1, n + 1))
    keep = [i + 1 for i, s in enumerate(sizes) if s > 1500]
    boxes = {i: ndimage.find_objects((lab == i).astype(int))[0] for i in keep}
    centers = {i: ((boxes[i][1].start + boxes[i][1].stop) / 2, (boxes[i][0].start + boxes[i][0].stop) / 2) for i in keep}
    # 줄 나누기: 중심 y 기준으로 4줄
    order = sorted(keep, key=lambda i: centers[i][1])
    rows, cur = [], []
    for i in order:
        if cur and centers[i][1] - np.mean([centers[j][1] for j in cur]) > 110:
            rows.append(cur); cur = []
        cur.append(i)
    rows.append(cur)
    rows = [sorted(row, key=lambda i: centers[i][0]) for row in rows]
    clean = np.dstack([r, np.minimum(g, np.maximum(r, b)), b]).clip(0, 255)
    pieces = {}
    for row_names, row in zip(NAMES, rows):
        if len(row) != len(row_names):
            raise SystemExit(f"row {row_names}: found {len(row)} pieces — run --preview and check the atlas")
        for name, i in zip(row_names, row):
            ys, xs = np.nonzero((lab == i) & mask)
            x0, x1, y0, y1 = xs.min() - 3, xs.max() + 4, ys.min() - 3, ys.max() + 4
            a = np.where(lab[y0:y1, x0:x1] == i, alpha[y0:y1, x0:x1], 0)
            img = np.dstack([clean[y0:y1, x0:x1], a * 255]).astype(np.uint8)
            pieces[name] = dict(img=img, origin=(int(x0), int(y0)), pins=find_pins(img))
    return pieces, rows


REPLACE = ROOT / "art_src/raw/hero_v3b_parts.png"
REPLACE_NAMES = ["head", "coat_body", "coat_hem_near", "coat_hem_far"]


def cut_replacements():
    """교체 파츠 시트(목 없는 머리 · 몸통에 씌우는 코트 몸판 · 허리 아래 밑단 2장) — 한 줄, 왼쪽부터"""
    if not REPLACE.exists():
        return {}
    rgb = np.asarray(Image.open(REPLACE).convert("RGB")).astype(np.int16)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    alpha = np.clip((95 - (g - np.maximum(r, b))) / 55, 0, 1)
    mask = alpha > .5
    lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=7))
    sizes = ndimage.sum(mask, lab, range(1, n + 1))
    keep = sorted([i + 1 for i, sz in enumerate(sizes) if sz > 1500], key=lambda i: ndimage.center_of_mass(mask, lab, i)[1])
    if len(keep) != len(REPLACE_NAMES):
        raise SystemExit(f"replacement sheet: found {len(keep)} pieces, expected {len(REPLACE_NAMES)}")
    clean = np.dstack([r, np.minimum(g, np.maximum(r, b)), b]).clip(0, 255)
    out = {}
    for name, i in zip(REPLACE_NAMES, keep):
        ys, xs = np.nonzero((lab == i) & mask)
        x0, x1, y0, y1 = xs.min() - 3, xs.max() + 4, ys.min() - 3, ys.max() + 4
        a = np.where(lab[y0:y1, x0:x1] == i, alpha[y0:y1, x0:x1], 0)
        img = np.dstack([clean[y0:y1, x0:x1], a * 255]).astype(np.uint8)
        pins = find_pins(img)
        if name == "head":
            img = erase_pins(img)  # 턱 밑 할핀은 보이면 단추처럼 튀므로 그림에서 지운다 (위치만 쓴다)
            img = trim_chin(img, pins)
        out[name] = dict(img=img, origin=(int(x0), int(y0)), pins=pins)
    return out


def trim_chin(img, pins, radius=26):
    """할핀 테두리가 턱선 아래로 튀어나온 혹을, 양옆 턱선을 이어 잘라내고 외곽선을 다시 긋는다"""
    if not pins:
        return img
    img = img.copy()
    px, py = max(pins, key=lambda q: q[1])
    h, w = img.shape[:2]
    xl, xr = int(max(1, px - radius - 10)), int(min(w - 2, px + radius + 10))
    def lowest(x):
        ys = np.nonzero(img[:, x, 3] > 128)[0]
        return int(ys.max()) if len(ys) else None
    yl, yr = lowest(xl - 1), lowest(xr + 1)
    if yl is None or yr is None:
        return img
    outline = np.array([26, 18, 24], np.uint8)
    # 핀 테두리(어두운 원호)가 얼굴 안쪽에 남지 않도록 주변 털색으로 덮는다
    yy, xx = np.indices((h, w))
    dist = np.hypot(xx - px, yy - py)
    disk, ring = dist < radius, (dist >= radius) & (dist < radius + 10)
    light = img[..., :3].astype(int).sum(axis=2) > 360
    fur = img[ring & light & (img[..., 3] > 200), :3]
    if len(fur):
        img[disk & (img[..., 3] > 0), :3] = np.median(fur, axis=0).astype(np.uint8)
    for x in range(xl, xr + 1):
        yb = round(yl + (yr - yl) * (x - xl) / max(1, xr - xl))
        img[yb + 1:, x, 3] = 0
        img[max(0, yb - 8):yb + 1, x, :3] = outline
        img[max(0, yb - 8):yb + 1, x, 3] = 255
    return img


def erase_pins(img):
    """황동 할핀(+테두리)을 주변 색으로 메운다"""
    img = img.copy()
    cr, cg, cb = (img[..., k].astype(int) for k in range(3))
    brass = (cr > 165) & (cg > 105) & (cg < 205) & (cb < 115) & (cr - cb > 70) & (img[..., 3] > 200)
    hole = ndimage.binary_dilation(brass, iterations=5)
    ring = ndimage.binary_dilation(hole, iterations=5) & ~hole & (img[..., 3] > 200)
    lab, n = ndimage.label(hole)
    for i in range(1, n + 1):
        part = lab == i
        near = ndimage.binary_dilation(part, iterations=8) & ring
        cols = img[near, :3].astype(int)
        cols = cols[cols.sum(axis=1) > 240]  # 외곽선(검정)은 빼고 털·천 색만
        if len(cols):
            img[part, :3] = np.median(cols, axis=0).astype(np.uint8)
    return img


def find_pins(img):
    cr, cg, cb = (img[..., k].astype(int) for k in range(3))
    brass = (cr > 165) & (cg > 105) & (cg < 205) & (cb < 115) & (cr - cb > 70) & (img[..., 3] > 200)
    lab, n = ndimage.label(brass)
    pins = []
    for i in range(n):
        area = (lab == i + 1).sum()
        if 60 < area < 3000:
            y, x = ndimage.center_of_mass(brass, lab, i + 1)
            pins.append((float(x), float(y)))
    return pins


def preview(pieces):
    im = Image.open(ATLAS).convert("RGB")
    d = ImageDraw.Draw(im)
    for name, p in pieces.items():
        ox, oy = p["origin"]
        h, w = p["img"].shape[:2]
        d.rectangle([ox, oy, ox + w, oy + h], outline=(255, 0, 0), width=2)
        d.text((ox + 3, oy + 3), name, fill=(255, 255, 0))
        for x, y in p["pins"]:
            d.ellipse([ox + x - 6, oy + y - 6, ox + x + 6, oy + y + 6], outline=(0, 0, 255), width=3)
    out = Path(sys.argv[sys.argv.index("--preview") + 1]) if len(sys.argv) > sys.argv.index("--preview") + 1 else ROOT / "_v3_preview.png"
    im.save(out)
    print("preview:", out)


def top(p, piece):   # 가장 위 할핀 (없으면 위쪽 가운데)
    return min(p, key=lambda q: q[1]) if p else (piece["img"].shape[1] / 2, 12)


def bottom(p, piece):
    return max(p, key=lambda q: q[1]) if p else (piece["img"].shape[1] / 2, piece["img"].shape[0] - 12)


def main():
    pieces, _ = cut_pieces()
    old_head_h = pieces["head"]["img"].shape[0]
    repl = cut_replacements()
    pieces.update(repl)
    if "--preview" in sys.argv:
        return preview(pieces)
    P = {k: v["pins"] for k, v in pieces.items()}
    pin = {}
    pin["neck"] = bottom(P["head"], pieces["head"])
    tp = sorted(P["torso"], key=lambda q: q[1])
    if len(tp) < 4:
        raise SystemExit(f"torso needs 4 pins, found {tp}")
    t_neck, t_waist = tp[0], tp[-1]
    shoulders = sorted(tp[1:-1], key=lambda q: q[0])
    t_near, t_far = shoulders[0], shoulders[-1]
    sk = P["skirt"]
    s_waist = top(sk, pieces["skirt"])
    s_legs = sorted(sorted([q for q in sk if q is not s_waist], key=lambda q: -q[1])[:2], key=lambda q: q[0])
    if len(s_legs) < 2:
        w, h = pieces["skirt"]["img"].shape[1], pieces["skirt"]["img"].shape[0]
        s_legs = [(w * .32, h * .78), (w * .7, h * .74)]
    limb = {k: (top(P[k], pieces[k]), bottom(P[k], pieces[k])) for k in ("uarm", "farm", "thigh", "shin")}
    hand_top = top(P["hand"], pieces["hand"])
    foot_top = top(P["foot"], pieces["foot"])
    tail_pins = sorted(P["tail"], key=lambda q: q[0])

    # 목 없는 머리를 쓰면 몸통 그림의 목 토막(+목 할핀)이 턱 밑으로 비친다 → 잘라내고 몸통 할핀도 지운다
    if "head" in repl:
        timg = erase_pins(pieces["torso"]["img"])
        cut_y = int(min(t_near[1], t_far[1]) - 32)
        timg[:max(0, cut_y), :, 3] = 0
        pieces["torso"]["img"] = timg
    # ---- 스프라이트 저장 (꼬리는 할핀 2·3에서 잘라 3마디) ----
    for name, p in pieces.items():
        if name != "tail":
            Image.fromarray(p["img"], "RGBA").save(SPRITES / f"hero_v3_{name}.png", optimize=True)
    tail = pieces["tail"]["img"]
    th, tw = tail.shape[:2]
    cuts = [q[0] for q in tail_pins[1:3]] if len(tail_pins) >= 3 else [tw * .36, tw * .68]
    ys, xs = np.indices((th, tw))
    joints = [tail_pins[1] if len(tail_pins) >= 3 else (cuts[0], th / 2), tail_pins[2] if len(tail_pins) >= 3 else (cuts[1], th / 2)]
    segs = {"tail_base": (0, cuts[0] + 26), "tail_mid": (cuts[0] - 26, cuts[1] + 26), "tail_tip": (cuts[1] - 26, tw)}
    seg_origin = {}
    for k, (a, b) in segs.items():
        a, b = int(max(0, a)), int(min(tw, b))
        piece = tail[:, a:b].copy()
        # 이음매는 둥근 끝으로 겹치게 (구부릴 때 틈이 안 보이도록)
        for jx, jy in joints:
            d = np.hypot(xs[:, a:b] - jx, ys[:, a:b] - jy)
            side = (xs[:, a:b] - jx)
            if a < jx < b:
                if k == "tail_base" or (k == "tail_mid" and jx == joints[1][0]):
                    piece[..., 3] = np.where((side > 0) & (d > 26), 0, piece[..., 3])
                else:
                    piece[..., 3] = np.where((side < 0) & (d > 26), 0, piece[..., 3])
        Image.fromarray(piece, "RGBA").save(SPRITES / f"hero_v3_{k}.png", optimize=True)
        seg_origin[k] = a

    def loc(name, q):  # 조각 이미지 좌표 (반올림)
        return [round(q[0]), round(q[1])]

    old = rigfk.load()
    parts = []

    def part(pid, img, parent, pivot, attach, z, rest=0, scale=1, shade=0):
        parts.append(dict(id=pid, img=img and f"hero_v3_{img}.png", parent=parent, pivot=pivot, attach=attach,
                          z=z, rest=rest, scale=scale, shade=shade))

    k = 1.0  # 전체 배율 — 아래에서 TARGET_HEIGHT 에 맞춰 다시 계산
    sw = pieces["skirt"]["img"].shape[1]
    part("hips", None, None, [0, 0], None, -20)
    part("skirt", "skirt", "hips", loc("skirt", s_waist), [0, 0], Z["skirt"], scale=k)
    leg_at = lambda q: [round((q[0] - s_waist[0]) * k), round((q[1] - s_waist[1]) * k)]
    for side, q, rest, shade in (("far", s_legs[1], -6, FAR_SHADE), ("near", s_legs[0], 4, 0)):
        part(f"{side}_thigh", "thigh", "hips", loc("thigh", limb["thigh"][0]), leg_at(q), Z[f"{side}_thigh"], rest=rest, scale=k * LEG_SCALE, shade=shade)
        part(f"{side}_shin", "shin", f"{side}_thigh", loc("shin", limb["shin"][0]), loc("thigh", limb["thigh"][1]), Z[f"{side}_shin"], rest=-rest, shade=shade)
        part(f"{side}_foot", "foot", f"{side}_shin", loc("foot", foot_top), loc("shin", limb["shin"][1]), Z[f"{side}_foot"], shade=shade)
    tb = tail_pins[0] if tail_pins else (20, th / 2)
    part("tail_base", "tail_base", "hips", [round(tb[0] - seg_origin["tail_base"]), round(tb[1])], [round(-sw * .3 * k), round(20 * k)], Z["tail_base"], rest=167, scale=k * .8)
    part("tail_mid", "tail_mid", "tail_base", [round(joints[0][0] - seg_origin["tail_mid"]), round(joints[0][1])],
         [round(joints[0][0] - seg_origin["tail_base"]), round(joints[0][1])], Z["tail_mid"])
    part("tail_tip", "tail_tip", "tail_mid", [round(joints[1][0] - seg_origin["tail_tip"]), round(joints[1][1])],
         [round(joints[1][0] - seg_origin["tail_mid"]), round(joints[1][1])], Z["tail_tip"])
    part("torso", "torso", "hips", loc("torso", t_waist), [0, round(-10 * k)], Z["torso"], scale=k)
    # 머리: 목 없는 새 머리면 같은 화면 크기가 되도록 배율 보정
    head_scale = HEAD_SCALE * (old_head_h / pieces["head"]["img"].shape[0] * NEW_HEAD_GROW if "head" in repl else 1)
    head_pin = bottom(pieces["head"]["pins"], pieces["head"]) if "head" in repl else pin["neck"]
    # 목이 없으므로 머리 할핀(턱 밑)을 몸통 목 할핀보다 살짝 아래(깃 안쪽)에 꽂는다
    part("head", "head", "torso", loc("head", head_pin), [round(t_neck[0]), round(t_neck[1] + (NECK_SINK if "head" in repl else 0))],
         Z["head"], rest=-3, scale=round(head_scale, 4))
    if "coat_body" in repl:
        # 코트 몸판: 양 어깨 할핀을 몸통 어깨 할핀에 겹치는 닮음 변환 → 몸통에 그대로 씌워진다
        cb = pieces["coat_body"]
        cpins = sorted(sorted(cb["pins"], key=lambda q: q[1])[:2], key=lambda q: q[0])
        if len(cpins) < 2:
            raise SystemExit(f"coat_body needs 2 shoulder pins, found {cb['pins']}")
        v_src = np.subtract(cpins[1], cpins[0])
        v_dst = np.subtract(t_far, t_near)
        cs = float(np.linalg.norm(v_dst) / np.linalg.norm(v_src))
        rot = float(np.degrees(np.arctan2(v_dst[1], v_dst[0]) - np.arctan2(v_src[1], v_src[0])))
        part("coat_body", "coat_body", "torso", loc("coat_body", cpins[0]), loc("torso", t_near), Z["coat_body"],
             rest=round(rot, 2), scale=round(cs, 4))
        tw_, th_ = pieces["torso"]["img"].shape[1], pieces["torso"]["img"].shape[0]
        for side, name, at in (("near", "coat_hem_near", (tw_ * .3, th_ * .9)), ("far", "coat_hem_far", (tw_ * .78, th_ * .86))):
            hp = pieces[name]
            part(f"coat_front_{side}", name, "torso", loc(name, top(hp["pins"], hp)), [round(at[0]), round(at[1])],
                 Z[f"coat_front_{side}"], rest=4 if side == "near" else -3, scale=round(cs, 4), shade=.12 if side == "far" else 0)
    else:
        cs_ = pieces["coat_shoulders"]
        part("coat_body", "coat_shoulders", "torso", loc("coat_shoulders", top(P["coat_shoulders"], cs_)),
             [round(t_neck[0]), round(t_neck[1] + 6)], Z["coat_body"])
        part("coat_front_near", "coat_front_near", "torso", loc("coat_front_near", top(P["coat_front_near"], pieces["coat_front_near"])),
             loc("torso", t_near), Z["coat_front_near"], rest=4)
        part("coat_front_far", "coat_front_far", "torso", loc("coat_front_far", top(P["coat_front_far"], pieces["coat_front_far"])),
             loc("torso", t_far), Z["coat_front_far"], rest=-3, shade=.12)
    part("coat", "coat_back", "torso", loc("coat_back", top(P["coat_back"], pieces["coat_back"])),
         [round(t_neck[0] - 20), round(t_neck[1] + 30)], Z["coat"], rest=14)
    for side, q, rest, shade in (("far", t_far, -16, FAR_SHADE), ("near", t_near, 12, 0)):
        part(f"{side}_arm", "uarm", "torso", loc("uarm", limb["uarm"][0]), loc("torso", q), Z[f"{side}_arm"], rest=rest, scale=ARM_SCALE, shade=shade)
        part(f"{side}_forearm", "farm", f"{side}_arm", loc("farm", limb["farm"][0]), loc("uarm", limb["uarm"][1]), Z[f"{side}_forearm"], rest=-20, shade=shade)
        part(f"{side}_hand", "hand", f"{side}_forearm", loc("hand", hand_top), loc("farm", limb["farm"][1]), Z[f"{side}_hand"], rest=8, shade=shade)
    hw, hh = pieces["hand"]["img"].shape[1], pieces["hand"]["img"].shape[0]
    nw, nh = pieces["needle"]["img"].shape[1], pieces["needle"]["img"].shape[0]
    # 바늘은 손가락 사이(앞팔 위, 손 아래)에 끼운다
    part("needle", "needle", "near_hand", [round(nw * .2), round(nh / 2)], [round(hw * .55), round(hh * .72)], Z["needle"], rest=6)
    order = ["hips", "torso", "head", "coat", "tail_base", "tail_mid", "tail_tip", "skirt",
             "far_thigh", "far_shin", "far_foot", "near_thigh", "near_shin", "near_foot",
             "far_arm", "far_forearm", "far_hand", "coat_front_far", "coat_body", "coat_front_near",
             "near_arm", "near_forearm", "near_hand", "needle"]
    parts.sort(key=lambda p: order.index(p["id"]))
    fw, fh = pieces["foot"]["img"].shape[1], pieces["foot"]["img"].shape[0]
    sole = [round(fw * .5 - foot_top[0]), round(fh - 6 - foot_top[1])]
    rig = dict(name="2.5D SD seamstress v3", stepFps=0, rootPos=[0, 0],
               feet=[{"id": "near_foot", "sole": sole, "depth": 4}, {"id": "far_foot", "sole": sole, "depth": -40}],
               groundDamp=old.get("groundDamp", {"run": .35}), parts=parts, anims={})
    ids = {p["id"] for p in parts}
    for name, anim in old["anims"].items():
        anim = json.loads(json.dumps(anim))
        anim["keys"] = {pid: tr for pid, tr in anim["keys"].items() if pid in ids}
        rig["anims"][name] = anim
    # 앞자락·치마의 2차 동작: 뒷자락(coat) 흔들림을 따라가되 작게
    for anim in rig["anims"].values():
        coat = anim["keys"].get("coat")
        if coat:
            anim["keys"]["coat_front_near"] = [[t, round(v * .45, 1)] for t, v in coat]
            anim["keys"]["coat_front_far"] = [[t, round(v * .35, 1)] for t, v in coat]
            anim["keys"]["skirt"] = [[t, round(-v * .12, 1)] for t, v in coat]

    # ---- 배율: 조립 키 = TARGET_HEIGHT, 앞발이 바닥(4)에 닿게 ----
    def assembled_height():
        M = rigfk.matrices(rig, *rigfk.pose(rig))
        top_y, sole_y = 0, rigfk.world(M, "near_foot", sole)[1]
        for p in parts:
            if not p["img"]:
                continue
            key = p["img"][len("hero_v3_"):-4]
            h, w = pieces[key]["img"].shape[:2] if key in pieces else (0, 0)
            for x, y in ((0, 0), (w, 0)):
                top_y = min(top_y, rigfk.world(M, p["id"], (x - p["pivot"][0], y - p["pivot"][1]))[1])
        return sole_y - top_y, sole_y
    height, _ = assembled_height()
    scale = TARGET_HEIGHT / height
    for p in parts:
        if p["parent"] == "hips":
            p["scale"] = round(p["scale"] * scale, 4)
            if p["attach"]:
                p["attach"] = [round(v * scale) for v in p["attach"]]
    for _ in range(3):
        M = rigfk.matrices(rig, *rigfk.pose(rig))
        rig["rootPos"][1] = round(rig["rootPos"][1] + 4 - rigfk.world(M, "near_foot", sole)[1], 1)
    M = rigfk.matrices(rig, *rigfk.pose(rig))
    rig["feet"][1]["depth"] = round(rigfk.world(M, "far_foot", sole)[1], 1)
    rigfk.save(rig)
    print("pins: torso", [tuple(map(round, q)) for q in tp], "skirt", [tuple(map(round, q)) for q in sk])
    print("height", round(height), "-> scale", round(scale, 3), "rootPos", rig["rootPos"])


if __name__ == "__main__":
    main()
