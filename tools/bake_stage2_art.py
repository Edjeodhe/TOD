"""2차 개편 그림 굽기: Codex 시트(art_src/raw) → 투명 PNG (Assets/sprites).

python tools/bake_stage2_art.py

- stat_icons.png (초록 키, 4×2) → stat_*.png        레벨업 스탯 증강 아이콘
- item_icons.png (초록 키, 4×3) → item_*.png        쉬어가기 탑의 아이템(재봉 조각) 아이콘
- tower.png      (초록 키, 1개) → prop_tower.png     쉬어가기 라운드의 탑 (창문처럼 안쪽에 갇힌 키 색 구멍은 빛으로 채운다)
- xp_orb.png     (자홍 키, 1개) → xp_orb.png         경험치 구
- heart_pickup.png (초록 키, 1개) → heart_pickup.png 체력 회복 하트
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw"
OUT = ROOT / "Assets/sprites"

STATS = ["attack", "attack_speed", "move_speed", "health", "reach", "needle_speed", "yarn", "dash"]
ITEMS = ["ember_thimble", "torn_ribbon", "bloody_scissors", "mad_spool",
         "tear_button", "soaked_yarn", "frozen_pincushion", "sleeping_eye",
         "rusty_pin", "tangled_tape", "cracked_watch", "struck_machine"]


def key_alpha(rgb, key):
    r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
    if key == "green":
        dom = g - np.maximum(r, b)
        spill = g > np.maximum(r, b)
        fix = (r, np.where(spill, np.maximum(r, b), g), b)
    else:   # magenta: r·b 가 g 보다 얼마나 큰가
        dom = np.minimum(r, b) - g
        lim = g + 0
        fix = (np.where(dom > 0, np.minimum(r, np.maximum(g, b * .0 + g + 40)), r), g, np.where(dom > 0, np.minimum(b, g + 40), b))
    a = np.clip(1.0 - (dom - 40) / 60.0, 0, 1)
    col = np.stack(fix, -1).clip(0, 255).astype(np.uint8)
    return col, a


def pieces(path, key, count, merge):
    rgb = np.asarray(Image.open(path).convert("RGB"))
    col, a = key_alpha(rgb, key)
    mask = a > .16
    lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=merge))
    found = []
    for i, sl in enumerate(ndimage.find_objects(lab)):
        area = int((mask[sl] & (lab[sl] == i + 1)).sum())
        found.append((area, i + 1, sl))
    found = sorted(found, reverse=True)[:count]
    # 읽기 순서: 줄(중심 y 를 120px 단위로 묶음) → x
    found.sort(key=lambda f: (round(((f[2][0].start + f[2][0].stop) / 2) / 160), f[2][1].start))
    result = []
    for _, li, sl in found:
        y0, y1 = max(0, sl[0].start - 6), sl[0].stop + 6
        x0, x1 = max(0, sl[1].start - 6), sl[1].stop + 6
        sub_a = a[y0:y1, x0:x1].copy()
        sub_a[lab[y0:y1, x0:x1] != li] = 0
        result.append((col[y0:y1, x0:x1].copy(), sub_a))
    return result


def fill_holes(col, a, color, top=.45):
    # 그림 위쪽(top 비율 안)에 갇힌 투명 구멍(키 색으로 그린 창문)만 빛 색으로 채운다. 아래쪽 실 고리 안 틈은 그대로 둔다.
    solid = a > .5
    holes = ndimage.binary_fill_holes(solid) & ~solid
    lab, n = ndimage.label(holes)
    keep = np.zeros_like(holes)
    for i, sl in enumerate(ndimage.find_objects(lab)):
        if (sl[0].start + sl[0].stop) / 2 < top * a.shape[0]:
            keep[sl] |= lab[sl] == i + 1
    keep = ndimage.binary_dilation(keep, iterations=1) & (a < .9) | keep
    col[keep] = color
    a[keep] = 1
    return col, a


def save(col, a, name, max_side):
    im = Image.fromarray(np.dstack([col, (a * 255).astype(np.uint8)]), "RGBA")
    scale = min(1, max_side / max(im.size))
    if scale < 1:
        im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)
    im.save(OUT / f"{name}.png")
    print("wrote", name, im.size)


for (col, a), name in zip(pieces(RAW / "stat_icons.png", "green", 8, 30), STATS):
    save(col, a, f"stat_{name}", 256)
for (col, a), name in zip(pieces(RAW / "item_icons.png", "green", 12, 30), ITEMS):
    save(col, a, f"item_{name}", 256)
(col, a), = pieces(RAW / "tower.png", "green", 1, 40)
col, a = fill_holes(col, a, (214, 238, 228))
save(col, a, "prop_tower", 512)
(col, a), = pieces(RAW / "xp_orb.png", "magenta", 1, 8)
save(col, a, "xp_orb", 128)
# 체력 회복 하트 (초록 키) — 4차 이후 추가
if (RAW / "heart_pickup.png").exists():
    (col, a), = pieces(RAW / "heart_pickup.png", "green", 1, 8)
    save(col, a, "heart_pickup", 128)
