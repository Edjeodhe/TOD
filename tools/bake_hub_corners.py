"""24차 재봉실 구석 가구 + 새 재봉실 배경 굽기: Codex 시트(art_src/raw) → Assets/sprites.

python tools/bake_hub_corners.py

- hub_corners.png (초록 키, 위 3칸 + 아래 2칸): 수녀 기도대, 촛불 유령 장식장, 단추 수레 / 쥐 좌판, 인형 장난감 구석
  → hub_corner_nun / hub_corner_ghost / hub_corner_cart / hub_corner_rat / hub_corner_doll (방 빛 비네트, 긴 변 512)
- hub_room3.png (불투명): 구석 가구 자리에 맞춰 다시 그린 재봉실 → Assets/sprites/hub_room3.png (데이터 hub.backdrop)
"""
import numpy as np
from PIL import Image
from scipy import ndimage

from bake_hub_art import OUT, RAW, key_green, save, vignette

CORNERS = {0: "nun", 1: "ghost", 2: "cart", 3: "rat", 4: "doll"}   # 칸 번호 (3열 × 2행, 5번 칸은 비움)


def cells(path, cols=3, rows=2, merge=24):
    """칸마다 무게중심이 그 칸에 있는 그림 덩어리 전부를 한 장으로 (빈 칸은 건너뜀)."""
    rgb = np.asarray(Image.open(path).convert("RGB"))
    col, a = key_green(rgb)
    a = np.where(a > .16, a, 0)
    mask = a > .16
    lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=merge))
    h, w = a.shape
    owner = np.zeros(n + 1, int) - 1
    for i, sl in enumerate(ndimage.find_objects(lab)):
        ys, xs = np.nonzero(mask[sl] & (lab[sl] == i + 1))
        if len(ys) < 400:   # 티끌
            continue
        cy, cx = sl[0].start + ys.mean(), sl[1].start + xs.mean()
        owner[i + 1] = int(cy * rows // h) * cols + int(cx * cols // w)
    out = {}
    for cell in range(cols * rows):
        keep = mask & (owner[lab] == cell)
        ys, xs = np.nonzero(keep)
        if len(ys) == 0:
            continue
        y0, y1 = max(0, ys.min() - 4), ys.max() + 2
        x0, x1 = max(0, xs.min() - 4), xs.max() + 5
        out[cell] = (col[y0:y1, x0:x1].copy(), np.where(keep[y0:y1, x0:x1], a[y0:y1, x0:x1], 0))
    return out


if (RAW / "hub_corners.png").exists():
    found = cells(RAW / "hub_corners.png")
    for cell, name in CORNERS.items():
        if cell not in found:
            print("missing", name)
            continue
        col, a = found[cell]
        save(vignette(col, a, rim=.3, bottom=.35, side=.18), a, f"hub_corner_{name}", 512)
if (RAW / "hub_room3.png").exists():
    Image.open(RAW / "hub_room3.png").convert("RGB").save(OUT / "hub_room3.png")
    print("wrote hub_room3")
