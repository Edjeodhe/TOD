"""22차 미리보기: 죄종 조각 아이콘 35 + 도안 7 (보통, 붉게 물든) → Temp/Shots/sin22_icons.png, sin22_patterns.png"""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
S = ROOT / "Assets/sprites"
SINS = ["wrath", "sloth", "envy", "greed", "gluttony", "lust", "pride"]
COLORS = {"wrath": "#C62E2E", "sloth": "#6F8FA8", "envy": "#4FA34A", "greed": "#D9A520", "gluttony": "#E07A2A", "lust": "#D9418C", "pride": "#7B4FC9"}
C = 200


def tile(sheet, im, x, y):
    im = im.copy()
    im.thumbnail((C - 16, C - 16))
    sheet.alpha_composite(im, (x + (C - im.width) // 2, y + (C - im.height) // 2))


sheet = Image.new("RGBA", (C * 5 + 120, C * 7), (52, 46, 58, 255))
d = ImageDraw.Draw(sheet)
for r, s in enumerate(SINS):
    d.rectangle([0, r * C, 110, r * C + C - 1], fill=COLORS[s])
    d.text((8, r * C + 8), s, fill=(255, 255, 255, 255))
    for i in range(5):
        p = S / f"item_{s}_{i + 1}.png"
        if p.exists():
            tile(sheet, Image.open(p).convert("RGBA"), 120 + i * C, r * C)
sheet.save(ROOT / "Temp/Shots/sin22_icons.png")
pat = Image.new("RGBA", (C * 7, C * 2), (52, 46, 58, 255))
for n in range(1, 8):
    for r, suf in enumerate(["", "_tainted"]):
        p = S / f"item_pattern_{n}{suf}.png"
        if p.exists():
            tile(pat, Image.open(p).convert("RGBA"), (n - 1) * C, r * C)
pat.save(ROOT / "Temp/Shots/sin22_patterns.png")
print("ok")
