"""몬스터 파츠 굽기: Codex 시트(art_src/raw/enemy_*_parts.png, 초록 키) → Assets/sprites/enemy_{몬스터}_{파츠}.png

python tools/bake_enemy_parts.py

시트는 파츠를 읽기 순서(줄 → 왼쪽부터)로 그렸고, 팔다리는 관절 끝을 위로 세워 그렸다.
줄은 세로 범위가 겹치는 조각끼리 묶는다(턱처럼 낮게 놓인 조각이 다음 줄로 밀리지 않게).
굽고 나면 파츠마다 크기와 추정 관절(위쪽 둥근 끝의 중심, 그림 px)을 출력한다. 리그 조립(MCP)의 첫 값으로 쓴다.
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw"
OUT = ROOT / "Assets/sprites"
SCALE = .5          # 시트 px → 구운 그림 px

PARTS = {
    "stalker": ["head", "torso", "uarm", "farm", "thigh", "shin"],
    "charger": ["head", "torso", "uarm", "farm", "thigh", "shin", "foot"],
    "shooter": ["head", "jaw", "torso", "arm", "leg"],
    "boss": ["head", "torso", "skirt", "uarm", "farm"],
}


def key_alpha(rgb):
    r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
    dom = g - np.maximum(r, b)
    spill = g > np.maximum(r, b)
    col = np.stack((r, np.where(spill, np.maximum(r, b), g), b), -1).clip(0, 255).astype(np.uint8)
    a = np.clip(1.0 - (dom - 40) / 60.0, 0, 1)
    return col, a


def rows_in_order(found):
    # 세로 범위가 겹치는 조각끼리 한 줄로 묶고, 줄은 위에서 아래로, 줄 안은 왼쪽부터
    found = sorted(found, key=lambda f: f[2][0].start)
    rows = []
    for f in found:
        y0, y1 = f[2][0].start, f[2][0].stop
        for row in rows:
            if y0 < row["y1"] - 20 and y1 > row["y0"] + 20:
                row["items"].append(f)
                row["y0"], row["y1"] = min(row["y0"], y0), max(row["y1"], y1)
                break
        else:
            rows.append({"y0": y0, "y1": y1, "items": [f]})
    rows.sort(key=lambda r: r["y0"])
    return [f for row in rows for f in sorted(row["items"], key=lambda f: f[2][1].start)]


def pieces(path, count, merge=12):
    rgb = np.asarray(Image.open(path).convert("RGB"))
    col, a = key_alpha(rgb)
    mask = a > .16
    lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=merge))
    found = []
    for i, sl in enumerate(ndimage.find_objects(lab)):
        area = int((mask[sl] & (lab[sl] == i + 1)).sum())
        found.append((area, i + 1, sl))
    found = sorted(found, reverse=True)[:count]
    result = []
    for _, li, sl in rows_in_order(found):
        y0, y1 = max(0, sl[0].start - 6), sl[0].stop + 6
        x0, x1 = max(0, sl[1].start - 6), sl[1].stop + 6
        sub_a = a[y0:y1, x0:x1].copy()
        sub_a[lab[y0:y1, x0:x1] != li] = 0
        result.append((col[y0:y1, x0:x1].copy(), sub_a))
    return result


def top_joint(a):
    # 위쪽 둥근 끝: 맨 위 불투명 줄에서 끝 폭의 절반만큼 내려간 곳, 가로는 그 구간의 불투명 중심
    solid = a > .5
    ys = np.where(solid.any(1))[0]
    top = ys[0]
    probe = solid[top: top + max(4, a.shape[0] // 8)]
    xs = np.where(probe.any(0))[0]
    width = xs[-1] - xs[0] + 1
    band = solid[top: top + width]
    cx = float(np.where(band.any(0))[0].mean())
    return round(cx, 1), round(top + width / 2, 1)


info = {}
for name, parts in PARTS.items():
    found = pieces(RAW / f"enemy_{name}_parts.png", len(parts))
    assert len(found) == len(parts), (name, len(found))
    info[name] = {}
    for (col, a), part in zip(found, parts):
        im = Image.fromarray(np.dstack([col, (a * 255).astype(np.uint8)]), "RGBA")
        im = im.resize((max(1, round(im.width * SCALE)), max(1, round(im.height * SCALE))), Image.LANCZOS)
        im.save(OUT / f"enemy_{name}_{part}.png")
        joint = top_joint(np.asarray(im)[..., 3] / 255.0)
        info[name][part] = {"size": im.size, "topJoint": joint}
        print(f"enemy_{name}_{part}", im.size, "top joint", joint)

(ROOT / "art_src/rig").mkdir(parents=True, exist_ok=True)
(ROOT / "art_src/rig/enemy_parts.json").write_text(json.dumps(info, indent=1))
