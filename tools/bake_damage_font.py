"""피해 숫자 그림 글꼴 굽기: Codex 시트 → 글자 아틀라스 PNG + 글자 위치 JSON.

python tools/bake_damage_font.py
  art_src/raw/damage_digits.png (초록 키, 두 줄: 0~6 / 7 8 9 + - !)
  → Assets/Art/Generated/damage_font.png   (가로 한 줄 아틀라스, 글자 사이 여백)
  → art_src/fonts/damage_font.json         (글자별 x, y, w, h — 위에서 아래 기준)
그 뒤 Unity 메뉴 TOD/Setup/1. Texture Imports 가 JSON 대로 글자 스프라이트(damage_font_<코드>)를 잘라 낸다.

글자 크기 비율은 원본 그대로 둔다(+, - 는 숫자보다 작다). 줄마다 숫자의 위·아래를 기준선으로 같은 높이 칸에 담아
위아래 위치가 맞게 나온다.
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "art_src/raw/damage_digits.png"
OUT_PNG = ROOT / "Assets/Art/Generated/damage_font.png"
OUT_JSON = ROOT / "art_src/fonts/damage_font.json"
CHARS = "0123456789+-!"
CELL_H = 128          # 아틀라스 칸 높이 (px)
PAD = 10              # 글자 사이 여백 (밉맵 번짐 방지)

rgb = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
dom = g - np.maximum(r, b)
alpha = np.clip(1 - (dom - 40) / 60, 0, 1)
g2 = np.where(g > np.maximum(r, b), np.maximum(r, b), g)
col = np.stack([r, g2, b], -1).clip(0, 255).astype(np.uint8)

mask = alpha > .16
lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=20))   # ! 의 점까지 한 글자로
pieces = []
for i, sl in enumerate(ndimage.find_objects(lab)):
    area = int((mask[sl] & (lab[sl] == i + 1)).sum())
    if area > 400:
        pieces.append((sl, i + 1, area))
assert len(pieces) >= len(CHARS), f"글자 {len(pieces)}개 찾음 (필요 {len(CHARS)})"
pieces = sorted(pieces, key=lambda p: -p[2])[:len(CHARS)]
# 읽기 순서: 두 줄로 나눈 뒤 x 순
cy = np.array([(p[0][0].start + p[0][0].stop) / 2 for p in pieces])
split = (cy.min() + cy.max()) / 2
rows = [sorted([p for p in pieces if (p[0][0].start + p[0][0].stop) / 2 < split], key=lambda p: p[0][1].start),
        sorted([p for p in pieces if (p[0][0].start + p[0][0].stop) / 2 >= split], key=lambda p: p[0][1].start)]
ordered = rows[0] + rows[1]
assert len(rows[0]) == 7 and len(rows[1]) == 6, f"줄 나눔이 예상과 다름: {len(rows[0])}, {len(rows[1])}"

glyphs = []
for row in rows:
    digits = [p for p in row if (p[0][0].stop - p[0][0].start) > .6 * max(q[0][0].stop - q[0][0].start for q in row)]
    top = min(p[0][0].start for p in digits)
    bottom = max(p[0][0].stop for p in digits)
    for sl, li, _ in row:
        x0, x1 = sl[1].start, sl[1].stop
        a = alpha[top:bottom, x0:x1].copy()
        a[lab[top:bottom, x0:x1] != li] = 0
        rgba = np.dstack([col[top:bottom, x0:x1], (a * 255).astype(np.uint8)])
        glyphs.append(Image.fromarray(rgba, "RGBA"))

# 줄마다 숫자 높이를 CELL_H 로 맞춘다 (가로세로 비율 유지)
glyphs = [im.resize((max(1, round(im.width * CELL_H / im.height)), CELL_H), Image.LANCZOS) for im in glyphs]
width = sum(im.width for im in glyphs) + PAD * (len(glyphs) + 1)
height = CELL_H + PAD * 2
atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
meta, x = [], PAD
for ch, im in zip(CHARS, glyphs):
    atlas.alpha_composite(im, (x, PAD))
    meta.append({"ch": ch, "x": x, "y": PAD, "w": im.width, "h": im.height})
    x += im.width + PAD
OUT_PNG.parent.mkdir(parents=True, exist_ok=True)
OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
atlas.save(OUT_PNG)
OUT_JSON.write_text(json.dumps({"width": width, "height": height, "glyphs": meta}, ensure_ascii=False, indent=1), encoding="utf-8")
print("atlas", atlas.size, "glyphs", "".join(m["ch"] for m in meta))
