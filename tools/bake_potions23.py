"""23차 포션 아이콘: art_src/raw/potions23.png (초록 키, 가로 3개) → Assets/sprites/potion_heal|pure|yarn.png

python tools/bake_potions23.py
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw/potions23.png"
OUT = ROOT / "Assets/sprites"
NAMES = ["heal", "pure", "yarn"]

rgb = np.asarray(Image.open(RAW).convert("RGB"))
r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
dom = g - np.maximum(r, b)
a = np.clip(1.0 - (dom - 40) / 60.0, 0, 1)
col = np.stack((r, np.where(g > np.maximum(r, b), np.maximum(r, b), g), b), -1).clip(0, 255).astype(np.uint8)
mask = a > .16
lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=12))
found = sorted(((int((lab[sl] == i + 1).sum()), i + 1, sl) for i, sl in enumerate(ndimage.find_objects(lab))), reverse=True)[:3]
found.sort(key=lambda f: f[2][1].start)
for (_, li, sl), name in zip(found, NAMES):
    y0, y1 = max(0, sl[0].start - 4), sl[0].stop + 4
    x0, x1 = max(0, sl[1].start - 4), sl[1].stop + 4
    sub = a[y0:y1, x0:x1].copy()
    sub[lab[y0:y1, x0:x1] != li] = 0
    im = Image.fromarray(np.dstack([col[y0:y1, x0:x1], (sub * 255).astype(np.uint8)]), "RGBA")
    # 정사각 캔버스 가운데 (칸 아이콘으로 쓰기 쉽게)
    side = max(im.size)
    sq = Image.new("RGBA", (side, side))
    sq.paste(im, ((side - im.width) // 2, (side - im.height) // 2))
    sq.resize((256, 256), Image.LANCZOS).save(OUT / f"potion_{name}.png")
    print("wrote", name, im.size)
