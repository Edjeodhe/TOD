"""피해 숫자 기본 아틀라스를 주아체(Assets/Fonts/Jua/Jua-Regular.ttf)로 굽는다 (14차: 게임 글꼴 전체 주아).

python tools/bake_damage_font_jua.py
  → Assets/Art/Generated/damage_font.png   (흰 글자 + 검은 외곽선, 가로 한 줄, 글자 사이 여백)
  → art_src/fonts/damage_font.json         (글자별 x, y, w, h — 위에서 아래 기준)
이어서 python tools/bake_element_fonts.py 로 속성·치명타 색을 굽고, Unity 메뉴 TOD/Setup/1 로 자른다.
옛 Codex 손글씨 숫자는 tools/bake_damage_font.py (art_src/raw/damage_digits.png) 에 그대로 남아 있다.
"""
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
FONT = ROOT / "Assets/Fonts/Jua/Jua-Regular.ttf"
OUT_PNG = ROOT / "Assets/Art/Generated/damage_font.png"
OUT_JSON = ROOT / "art_src/fonts/damage_font.json"
CHARS = "0123456789+-!"
SIZE = 116            # 글꼴 크기 (px)
STROKE = 9            # 검은 외곽선 두께 (px)
PAD = 10              # 글자 사이 여백 (밉맵 번짐 방지)

font = ImageFont.truetype(str(FONT), SIZE)
# 모든 글자를 같은 세로 칸에 담는다: 숫자 0 의 위·아래(외곽선 포함)를 기준선으로
x0, top, x1, bottom = font.getbbox("0", stroke_width=STROKE)
cell_h = bottom - top
glyphs = []
for ch in CHARS:
    l, t, r, b = font.getbbox(ch, stroke_width=STROKE)
    w = r - l
    im = Image.new("RGBA", (w, cell_h), (0, 0, 0, 0))
    ImageDraw.Draw(im).text((-l, -top), ch, font=font, fill=(255, 255, 255, 255), stroke_width=STROKE, stroke_fill=(0, 0, 0, 255))
    glyphs.append(im)

width = sum(im.width for im in glyphs) + PAD * (len(glyphs) + 1)
height = cell_h + PAD * 2
atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
meta, x = [], PAD
for ch, im in zip(CHARS, glyphs):
    atlas.alpha_composite(im, (x, PAD))
    meta.append({"ch": ch, "x": x, "y": PAD, "w": im.width, "h": im.height})
    x += im.width + PAD
OUT_PNG.parent.mkdir(parents=True, exist_ok=True)
atlas.save(OUT_PNG)
OUT_JSON.write_text(json.dumps({"width": width, "height": height, "glyphs": meta}, ensure_ascii=False, indent=1), encoding="utf-8")
print("jua atlas", atlas.size, "cell", cell_h)
