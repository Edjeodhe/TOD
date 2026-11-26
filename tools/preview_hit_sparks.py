"""24차 바느질 타격 파티클 시트 미리보기 — 어두운 바닥 위에 빛(가산) → 본체(알파) 순서로 합성해 칸을 늘어놓는다.
줄마다 시트 하나, 왼쪽은 흰색 그대로, 오른쪽은 색을 곱한 모습(분노 진홍 = 결박 때 물드는 모습 예).

실행: python tools/preview_hit_sparks.py
출력: Temp/Shots/w24_hitsparks_sheets.png
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SHEETS = ROOT / "Assets/Art/VFX/24"
META = json.loads((ROOT / "art_src/vfx/24/sheets.json").read_text(encoding="utf-8"))
OUT = ROOT / "Temp/Shots"
OUT.mkdir(parents=True, exist_ok=True)
CELL = 96
BG = np.array([0x3a, 0x33, 0x2e], dtype=np.float32) / 255
TINTS = [np.array([1, 1, 1], np.float32), np.array([.98, .93, .82], np.float32), np.array([.86, .25, .22], np.float32)]


def load(path):
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.float32) / 255


rows = []
for name, m in META.items():
    body, glow = load(SHEETS / f"{name}.png"), load(SHEETS / f"{name}_glow.png")
    n, s = m["frames"], m["size"]
    row = []
    for tint in TINTS:
        for i in range(n):
            b = np.asarray(Image.fromarray((body[:, i * s:(i + 1) * s] * 255).astype(np.uint8)).resize((CELL, CELL), Image.LANCZOS), np.float32) / 255
            g = np.asarray(Image.fromarray((glow[:, i * s:(i + 1) * s] * 255).astype(np.uint8)).resize((CELL, CELL), Image.LANCZOS), np.float32) / 255
            c = np.broadcast_to(BG, (CELL, CELL, 3)).copy()
            c = np.clip(c + g[..., :3] * tint * g[..., 3:] * .6, 0, 1)
            c = c * (1 - b[..., 3:]) + b[..., :3] * tint * b[..., 3:]
            row.append(c)
        row.append(np.broadcast_to(BG * .6, (CELL, 16, 3)))
    rows.append(np.concatenate(row, axis=1))
W = max(r.shape[1] for r in rows)
rows = [np.pad(r, ((0, 4), (0, W - r.shape[1]), (0, 0))) for r in rows]
img = np.concatenate(rows, axis=0)
Image.fromarray((img * 255).astype(np.uint8)).save(OUT / "w24_hitsparks_sheets.png")
print(OUT / "w24_hitsparks_sheets.png")
