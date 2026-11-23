"""16차 속성 이펙트 시트 미리보기 — 게임 바닥색 위에 빛(가산) → 본체(알파) 순서로 합성해 칸을 늘어놓는다.

실행: python tools/preview_element_vfx.py [그룹|시트 이름 ...]
출력: Temp/Shots/vfx16_<그룹>.png  (한 줄 = 한 시트, 왼쪽부터 0번 칸)
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SHEETS = ROOT / "Assets/Art/VFX/16"
META = json.loads((ROOT / "art_src/vfx/16/sheets.json").read_text(encoding="utf-8"))
OUT = ROOT / "Temp/Shots"
OUT.mkdir(parents=True, exist_ok=True)
CELL = 128
BG = np.array([0x3a, 0x33, 0x2e], dtype=np.float32) / 255     # 어두운 종이 바닥
BG2 = np.array([0x8c, 0x80, 0x6c], dtype=np.float32) / 255    # 밝은 바닥 (정화 뒤)

only = sys.argv[1:]
groups = {}
for name, m in META.items():
    if only and name not in only and m["group"] not in only:
        continue
    if (SHEETS / f"{name}.png").exists():
        groups.setdefault(m["group"], []).append(name)


def composite(body, glow, bg):
    h, w = body.shape[:2]
    out = np.ones((h, w, 3), dtype=np.float32) * bg
    out = out + glow[..., :3] * glow[..., 3:4]
    out = np.clip(out, 0, 1)
    out = body[..., :3] * body[..., 3:4] + out * (1 - body[..., 3:4])
    return out


for group, names in groups.items():
    rows = []
    for name in names:
        m = META[name]
        body = np.asarray(Image.open(SHEETS / f"{name}.png").convert("RGBA"), dtype=np.float32) / 255
        glow = np.asarray(Image.open(SHEETS / f"{name}_glow.png").convert("RGBA"), dtype=np.float32) / 255
        bg = BG2 if name.endswith("_light") else BG
        img = Image.fromarray((composite(body, glow, BG) * 255).astype(np.uint8))
        n, size = m["frames"], m["size"]
        scale = CELL / size
        img = img.resize((int(n * size * scale), CELL), Image.LANCZOS)
        row = Image.new("RGB", (16 * CELL + 150, CELL), (24, 20, 18))
        row.paste(img, (150, 0))
        d = ImageDraw.Draw(row)
        d.text((6, 6), name, fill=(230, 220, 200))
        d.text((6, 22), f"{n}f {m['fps']}fps {'loop' if m['loop'] else ''}", fill=(170, 160, 150))
        piv = m["pivot"]
        for i in range(n):   # 피벗 표시
            x = 150 + int((i + piv[0]) * CELL)
            y = int((1 - piv[1]) * CELL)
            d.line((x - 3, y, x + 3, y), fill=(90, 200, 255))
        rows.append(row)
    sheet = Image.new("RGB", (rows[0].width, CELL * len(rows)))
    for i, r in enumerate(rows):
        sheet.paste(r, (0, i * CELL))
    path = OUT / f"vfx16_{group}.png"
    sheet.save(path)
    print(path)
