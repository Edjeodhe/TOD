"""23차 모이라 카드 아이콘: art_src/raw/upgrade_box23.png (초록 키, 가로 2개)
→ Assets/sprites/icon_upgrade_expansion.png (인벤토리 확장, 재봉 상자 + 큰 '+'), icon_upgrade_lines.png (인벤토리 칸 강화, 금실로 꿰맨 칸)

python tools/bake_upgrade_box23.py
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw/upgrade_box23.png"
OUT = ROOT / "Assets/sprites"
NAMES = ["expansion", "lines"]
MAX_SIDE = 360   # 옛 카드 아이콘(icon_shard 227×361, icon_yarn 318×303)과 비슷한 크기

rgb = np.asarray(Image.open(RAW).convert("RGB"))
r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
dom = g - np.maximum(r, b)
a = np.clip(1.0 - (dom - 40) / 60.0, 0, 1)
col = np.stack((r, np.where(g > np.maximum(r, b), np.maximum(r, b), g), b), -1).clip(0, 255).astype(np.uint8)   # 초록 번짐 걷기
mask = a > .16
# 왼쪽 절반 = 확장, 오른쪽 절반 = 칸 강화. 떨어진 '+', 날아가는 조각, 반짝이도 같은 쪽에 붙인다 (작은 티끌만 버림)
lab, n = ndimage.label(mask)
sizes = ndimage.sum(mask, lab, range(1, n + 1))
centers = ndimage.center_of_mass(mask, lab, range(1, n + 1))
half = mask.shape[1] / 2
for side, name in enumerate(NAMES):
    keep = np.zeros_like(mask)
    for i in range(n):
        if sizes[i] >= 60 and (centers[i][1] < half) == (side == 0): keep |= lab == i + 1
    ys, xs = np.nonzero(keep)
    y0, y1 = max(0, ys.min() - 4), ys.max() + 5
    x0, x1 = max(0, xs.min() - 4), xs.max() + 5
    sub = a[y0:y1, x0:x1].copy()
    sub[~ndimage.binary_dilation(keep, iterations=2)[y0:y1, x0:x1]] = 0
    im = Image.fromarray(np.dstack([col[y0:y1, x0:x1], (sub * 255).astype(np.uint8)]), "RGBA")
    s = MAX_SIDE / max(im.size)
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    im.save(OUT / f"icon_upgrade_{name}.png")
    print("wrote", name, im.size)
