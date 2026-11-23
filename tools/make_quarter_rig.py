"""Build the 2.5D SD cutout rig from a 45-degree parts atlas.

python tools/make_quarter_rig.py              # original atlas (art_src/raw/hero_quarter_atlas.png)
python tools/make_quarter_rig.py flat         # flat-style 2.5-head atlas (art_src/raw/hero_flat_atlas.png)

All pin / attach coordinates below were measured on the original atlas. For another atlas drawn in the same
layout, each coordinate is carried over by its relative position inside the piece's bounding box.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SPRITES = ROOT / "assets/sprites"
FLAT = "flat" in sys.argv[1:]
ATLAS = Image.open(ROOT / f"art_src/raw/{'hero_flat_atlas' if FLAT else 'hero_quarter_atlas'}.png").convert("RGB")
# Tight piece boxes (x0, y0, x1, y1) from tools/slice_sheet.py on each atlas.
OLD_BOX = {"head": (84, 16, 588, 475), "torso": (676, 15, 999, 495), "coat": (1078, 15, 1368, 497),
           "uarm": (84, 504, 237, 801), "farm": (290, 497, 485, 808), "hand": (527, 571, 712, 793),
           "thigh": (737, 497, 955, 806), "shin": (1007, 531, 1148, 801), "foot": (1208, 617, 1453, 816),
           "tail": (42, 844, 775, 995), "needle": (833, 869, 1490, 960)}
NEW_BOX = {"head": (57, 7, 624, 504), "torso": (660, 46, 1005, 480), "coat": (1077, 38, 1377, 490),
           "uarm": (79, 543, 236, 767), "farm": (290, 536, 482, 777), "hand": (511, 556, 716, 774),
           "thigh": (741, 541, 952, 776), "shin": (996, 547, 1147, 773), "foot": (1186, 583, 1462, 786),
           "tail": (40, 842, 773, 997), "needle": (829, 866, 1491, 961)}
piece = lambda layer: "tail" if layer.startswith("tail") else layer


def ratio(layer):
    if not FLAT:
        return 1, 1
    o, n = OLD_BOX[piece(layer)], NEW_BOX[piece(layer)]
    return (n[2] - n[0]) / (o[2] - o[0]), (n[3] - n[1]) / (o[3] - o[1])


def remap(layer, xy):
    if not FLAT or not layer:
        return xy
    o, n = OLD_BOX[piece(layer)], NEW_BOX[piece(layer)]
    rx, ry = ratio(layer)
    return (n[0] + (xy[0] - o[0]) * rx, n[1] + (xy[1] - o[1]) * ry)
PARTS = {
    "head": ((85, 18, 590, 480), (352, 435)),
    "torso": ((677, 20, 996, 492), (837, 347)),
    "coat": ((1080, 16, 1375, 492), (1258, 56)),
    "uarm": ((83, 502, 240, 801), (163, 547)),
    "farm": ((288, 500, 480, 801), (398, 537)),
    "hand": ((531, 574, 710, 791), (613, 616)),
    "thigh": ((736, 502, 956, 806), (850, 539)),
    "shin": ((1009, 535, 1146, 801), (1080, 572)),
    "foot": ((1208, 619, 1452, 815), (1286, 660)),
    "tail_base": ((48, 840, 309, 1000), (86, 912)),
    "tail_mid": ((254, 840, 546, 1000), (281, 933)),
    "tail_tip": ((497, 840, 777, 1000), (521, 903)),
    "needle": ((832, 866, 1492, 956), (910, 913)),
}
if FLAT:
    PARTS = {k: ((*map(round, remap(k, b[:2])), *map(round, remap(k, b[2:]))), remap(k, pin)) for k, (b, pin) in PARTS.items()}
origins = {}
scales_y = {"torso": .86}
TAIL_A, TAIL_B = remap("tail", (281, 933)), remap("tail", (521, 903))
tail_joints = {"tail_base": (None, TAIL_A), "tail_mid": (TAIL_A, TAIL_B), "tail_tip": (TAIL_B, None)}
for part, (bounds, _) in PARTS.items():
    bounds = (max(0, bounds[0]), max(0, bounds[1]), min(ATLAS.width, bounds[2]), min(ATLAS.height, bounds[3]))
    channels = np.asarray(ATLAS.crop(bounds), dtype=np.int16)
    red, green, blue = channels.transpose(2, 0, 1)
    dominance = green - np.maximum(red, blue)
    alpha = np.clip((95 - dominance) / 55, 0, 1)
    if part in tail_joints:
        start, end = tail_joints[part]
        yy, xx = np.indices(alpha.shape)
        xx, yy = xx + bounds[0], yy + bounds[1]
        # Rounded overlapping ends cover the cut when the tail bends.
        for pivot, side in ((start, -1), (end, 1)):
            if pivot:
                distance = np.hypot(xx - pivot[0], yy - pivot[1])
                alpha *= np.where((xx - pivot[0]) * side > 0, np.clip(30 - distance, 0, 1), 1)
    pixels = np.stack([red, np.minimum(green, np.maximum(red, blue)), blue, alpha * 255], -1)
    cut = Image.fromarray(pixels.astype("uint8"), "RGBA")
    box = cut.getbbox()
    origins[part] = (bounds[0] + box[0], bounds[1] + box[1])
    image = cut.crop(box)
    if part in scales_y:
        scaled_height = round(image.height * scales_y[part])
        scales_y[part] = scaled_height / image.height
        image = image.resize((image.width, scaled_height), Image.Resampling.LANCZOS)
    image.save(SPRITES / f"hero_quarter_{part}.png", optimize=True)


def local(layer, coords, measured=True):
    """coords: atlas pixel position. measured=True means it was measured on the original atlas."""
    if not layer:
        return list(coords)
    if measured and coords is not PARTS.get(layer, (None, None))[1]:
        coords = remap(layer, coords)
    x, y = origins[layer]
    return [round(coords[0] - x), round((coords[1] - y) * scales_y.get(layer, 1))]


parts = []


def joint(name, image, parent, attach, z, rest=0, scale=1, shade=0):
    parent_image = next((old["layer"] for old in parts if old["id"] == parent), None)
    parts.append(dict(id=name, img=f"hero_quarter_{image}.png" if image else None, layer=image, parent=parent,
                      pivot=local(image, PARTS[image][1], measured=False) if image else [0, 0], attach=local(parent_image, attach) if parent else None,
                      z=z, rest=rest, scale=scale, shade=shade))


joint("hips", None, None, None, -20)
# Flat atlas: legs drawn short, so they are scaled up to show below the coat (2.5 heads, visible lower body).
LEG, FOOT, LEG_DROP = (1.38, 1.22, 14) if FLAT else (1, 1, 0)
joint("torso", "torso", "hips", (0, -8), 0)
joint("head", "head", "torso", (850, 102), 9, rest=-4, scale=1.10 * (.9 if FLAT else 1))
joint("coat", "coat", "hips", (-72, -20), -5, rest=18, scale=.48)
joint("tail_base", "tail_base", "hips", (-86, 3), -15, rest=167, scale=.65)
joint("tail_mid", "tail_mid", "tail_base", (281, 933), -14)
joint("tail_tip", "tail_tip", "tail_mid", (521, 903), -13)
joint("far_thigh", "thigh", "hips", (61, -21 + LEG_DROP), -11, rest=-10, scale=(.49 if FLAT else .43) * LEG, shade=.27)  # 같은 다리 길이: 짧으면 뒷발로 디딜 때 절뚝인다
joint("far_shin", "shin", "far_thigh", (850, 753), -10, rest=12, scale=.9 if FLAT else .88, shade=.27)
joint("far_foot", "foot", "far_shin", (1081, 748), -9, rest=-3, scale=1.12 / FOOT, shade=.27)
joint("near_thigh", "thigh", "hips", (-46, 5 + LEG_DROP), -8, rest=8, scale=.49 * LEG)
joint("near_shin", "shin", "near_thigh", (850, 753), -7, rest=-5, scale=.9)
joint("near_foot", "foot", "near_shin", (1080, 746), -6, rest=-2, scale=1.12 / FOOT)
joint("far_arm", "uarm", "torso", (950, 187), -4, rest=-18, scale=.42, shade=.27)
joint("far_forearm", "farm", "far_arm", (164, 740), -3, rest=-22, scale=.88, shade=.27)
joint("far_hand", "hand", "far_forearm", (399, 724), -2, rest=9, scale=.82, shade=.27)
joint("near_arm", "uarm", "torso", (746, 177), 3, rest=15, scale=.50)
joint("near_forearm", "farm", "near_arm", (164, 740), 4, rest=-30, scale=.93)
joint("near_hand", "hand", "near_forearm", (399, 725), 6, rest=10, scale=.87)
joint("needle", "needle", "near_hand", (620, 690), 5, rest=6, scale=1.09)


def animation(length, poses, *, loop=False, root=None):
    keys = {
        part: [[round(length * i / (len(values)-1), 4), angle] for i, angle in enumerate(values)]
        for part, values in poses.items()
    }
    result = dict(length=length, loop=loop, interp="linear", keys=keys)
    if root:
        result["root"] = [[round(length*i/(len(root)-1), 4), *offset] for i, offset in enumerate(root)]
    return result


anims = {
    "idle": animation(1.55, {
        "hips": [0, -1, 1, 0, 0], "torso": [0, 2, 3, 1, 0], "head": [0, -3, -2, 1, 0],
        "near_arm": [0, -3, -2, 0, 0], "near_forearm": [0, -1, -4, -1, 0],
        "far_arm": [0, 2, 3, 1, 0], "coat": [0, 1, 4, 2, 0],
        "tail_base": [-3, 3, 7, 0, -3], "tail_mid": [2, -6, -2, 7, 2], "tail_tip": [9, 8, -6, -7, 9],
    }, loop=True, root=[[0, 0], [-1, 0], [0, -3], [1, 0], [0, 0]]),
    "run": animation(.55, {
        "hips": [-3, -1, 2, 3, 2, 1, -2, -4, -3],
        "torso": [11, 13, 14, 12, 10, 12, 14, 13, 11],
        "head": [-6, -7, -12, -9, -5, -8, -11, -7, -6],
        "coat": [12, 19, 21, 17, 11, 17, 20, 15, 12],
        "near_thigh": [-26, -11, 9, 26, 23, 10, -17, -32, -26],
        "near_shin": [10, 6, 14, 31, 47, 48, 33, 15, 10],
        "near_foot": [8, 1, -9, -20, -29, -23, -8, 5, 8],
        "far_thigh": [23, 10, -17, -30, -25, -12, 8, 24, 23],
        "far_shin": [46, 44, 31, 14, 9, 5, 12, 28, 46],
        "far_foot": [-28, -22, -7, 5, 8, 0, -8, -18, -28],
        "near_arm": [18, 12, 2, -16, -27, -19, -3, 12, 18],
        "near_forearm": [-16, -14, -19, -26, -36, -32, -23, -17, -16],
        "near_hand": [7, 4, 0, -5, -7, -5, -1, 4, 7],
        "far_arm": [-22, -15, -2, 12, 22, 16, 4, -10, -22],
        "far_forearm": [-24, -22, -19, -15, -14, -18, -24, -25, -24],
        "tail_base": [6, 8, 13, 11, 5, 7, 12, 11, 6],
        "tail_mid": [-10, -15, -14, -5, -4, -12, -14, -8, -10],
        "tail_tip": [-3, -5, -11, -17, -12, -4, -9, -14, -3],
    }, loop=True, root=[[0, -5], [3, -3], [4, -8], [2, -16], [0, -5], [3, -3], [4, -9], [2, -15], [0, -5]]),
    "shoot": animation(.26, {
        "hips": [0, -2, -3, -1, 0], "torso": [-7, 3, 8, 5, 0], "head": [2, -3, -5, -2, 0],
        "near_arm": [-40, -84, -76, -39, 0], "near_forearm": [-46, 16, 7, -12, 0],
        "near_hand": [-8, 1, 7, 3, 0], "needle": [52, 69, 58, 28, 0],
        "far_arm": [10, 29, 28, 13, 0], "far_forearm": [-8, -16, -12, -6, 0],
        "tail_base": [0, -7, -10, -1, 0], "tail_mid": [0, 2, 9, 3, 0], "tail_tip": [0, 2, 5, 14, 0],
    }, root=[[-5, 0], [8, -6], [12, -3], [4, 0], [0, 0]]),
    "pull": animation(.48, {
        "hips": [2, 0, -7, -5, -2, 0], "torso": [12, 14, -12, -17, -9, 0], "head": [-8, -8, 7, 11, 5, 0],
        "near_arm": [-63, -81, -41, -12, -5, 0], "near_forearm": [8, 14, -56, -72, -26, 0],
        "near_hand": [7, 12, -15, -18, -8, 0], "needle": [43, 43, 76, 82, 29, 0],
        "far_arm": [-34, -48, -19, 10, 5, 0], "far_forearm": [0, 5, -42, -41, -17, 0],
        "near_thigh": [-9, -15, -18, -11, -4, 0], "near_shin": [7, 12, 29, 26, 10, 0],
        "far_thigh": [6, 11, 16, 14, 6, 0], "far_shin": [5, 9, 19, 17, 8, 0],
        "coat": [0, 6, -12, -20, -10, 0], "tail_base": [0, 5, -18, -12, 7, 0],
        "tail_mid": [0, 1, 11, 21, -7, 0], "tail_tip": [0, 1, 5, -4, 18, 0],
    }, root=[[5, 1], [10, 6], [-12, 12], [-11, 13], [-5, 4], [0, 0]]),
    "dash": animation(.29, {
        "hips": [-5, -12, -15, -7, 0], "torso": [-11, -22, -21, -13, 0], "head": [7, 15, 16, 9, 0],
        "coat": [-13, -28, -35, -17, 0], "near_arm": [-18, -45, -51, -29, 0],
        "near_forearm": [-18, -26, -34, -22, 0], "far_arm": [10, 24, 31, 18, 0],
        "far_forearm": [-17, -33, -44, -28, 0], "near_thigh": [15, 29, 47, 28, 0],
        "near_shin": [8, 11, 11, 7, 0], "near_foot": [-4, -17, -21, -13, 0],
        "far_thigh": [-13, -23, -26, -10, 0], "far_shin": [19, 31, 39, 19, 0],
        "far_foot": [2, -5, -8, -3, 0], "tail_base": [-5, -15, -25, -15, 0],
        "tail_mid": [3, 8, 20, 14, 0], "tail_tip": [7, 3, -4, 11, 0],
    }, root=[[1, 0], [9, 7], [14, 12], [7, 5], [0, 0]]),
    "hurt": animation(.29, {
        "hips": [-6, -8, 0], "torso": [-22, -16, 0], "head": [14, 9, 0], "near_arm": [21, -10, 0],
        "far_arm": [-32, -18, 0], "near_shin": [14, 22, 0], "far_shin": [8, 14, 0],
        "tail_base": [-23, 10, 0], "tail_mid": [7, -18, 0], "tail_tip": [0, 20, 0],
    }, root=[[-8, 0], [-11, 13], [0, 0]]),
    "die": animation(.55, {
        "hips": [-6, -35, -70], "torso": [-9, -16, -16], "head": [5, 9, 12],
        "near_thigh": [9, 18, 25], "near_shin": [11, 22, 28], "far_thigh": [-5, -11, -8], "far_shin": [5, 13, 25],
        "tail_base": [3, 20, 37], "tail_mid": [-12, -9, -6], "tail_tip": [7, -9, -14],
    }, root=[[0, 5], [-21, 61], [-51, 218]]),
}

for part in parts:
    del part["layer"]

fr = ratio("foot")
sole = [round(63 * fr[0]), round(112 * fr[1])]
feet = [
    {"id": "near_foot", "sole": sole, "depth": 4},
    {"id": "far_foot", "sole": sole, "depth": -54},
]


def rest_root_y():
    """Forward kinematics of the rest pose (same maths as src/rig/runtime.js) → root y that plants the feet."""
    by_id = {p["id"]: p for p in parts}
    def mat(pid):
        p = by_id[pid]
        if not p["parent"]:
            m = np.eye(3)
        else:
            par = by_id[p["parent"]]
            m = mat(p["parent"]) @ np.array([[1, 0, p["attach"][0] - par["pivot"][0]], [0, 1, p["attach"][1] - par["pivot"][1]], [0, 0, 1]])
        a = np.radians(p["rest"])
        s = p["scale"]
        return m @ np.array([[np.cos(a) * s, -np.sin(a) * s, 0], [np.sin(a) * s, np.cos(a) * s, 0], [0, 0, 1]])
    return -max(float((mat(f["id"]) @ np.array([*f["sole"], 1]))[1]) - f["depth"] for f in feet)


root_y = round(rest_root_y()) if FLAT else -237
rig = dict(name="2.5D SD seamstress", stepFps=0, rootPos=[-4, root_y], feet=feet, parts=parts, anims=anims)
print("rootPos", rig["rootPos"], "sole", sole)
(ROOT / "assets/rig/hero-quarter.rig.json").write_text(json.dumps(rig, indent=1) + "\n", encoding="utf-8")
