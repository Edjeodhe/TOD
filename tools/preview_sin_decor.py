"""22차 미리보기: 죄종 정적 소품 7 × 4 (파일 이름 표시) → Temp/Shots/sin22_decor.png"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from make_sin_prompts import DECOR  # noqa: E402

C = 260
sheet = Image.new("RGBA", (C * 4, C * len(DECOR)), (52, 46, 58, 255))
d = ImageDraw.Draw(sheet)
for r, (s, (names, _, _)) in enumerate(DECOR.items()):
    for i, n in enumerate(names):
        p = ROOT / f"Assets/sprites/decor_{s}_{n}.png"
        if p.exists():
            im = Image.open(p).convert("RGBA")
            im.thumbnail((C - 30, C - 30))
            sheet.alpha_composite(im, (i * C + (C - im.width) // 2, r * C + 22 + (C - 22 - im.height) // 2))
        d.text((i * C + 6, r * C + 4), f"{s}_{n}", fill=(255, 255, 255, 255))
sheet.save(ROOT / "Temp/Shots/sin22_decor.png")
print("ok")
