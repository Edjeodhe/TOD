"""속성별·치명타 피해 숫자 그림 글꼴: 기본 아틀라스(흰 글자 + 검은 외곽선)에서 색을 새로 구워 같은 배치의 아틀라스를 만든다.

python tools/bake_element_fonts.py [preview]
  Assets/Art/Generated/damage_font.png (tools/bake_damage_font.py 결과)
  → Assets/Art/Generated/damage_font_<이름>.png   (글자 위치는 art_src/fonts/damage_font.json 그대로)
  → art_src/fonts/element_fonts_preview.png       (검토용: 속성마다 "1234!" + 치명타)

글자 속(밝은 부분) = 위→아래 3색 그라디언트 + 위쪽 광택 띠, 외곽선(어두운 부분) = 속성의 짙은 잉크 색,
바깥 = 속성 색 빛(번짐). 게임에서는 흰색(곱하기 없음)으로 그린다.
Unity 메뉴 TOD/Setup/1. Texture Imports 가 damage_font_*.png 를 같은 JSON 으로 자른다.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "Assets/Art/Generated/damage_font.png"
META = json.loads((ROOT / "art_src/fonts/damage_font.json").read_text(encoding="utf-8"))
OUT = ROOT / "Assets/Art/Generated"


def hexrgb(h):
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255


# 이름: (위, 가운데, 아래 그라디언트, 외곽선 잉크, 바깥 빛)
STYLES = {
    "fire":      ("FFE08A", "FF5A1A", "B8140C", "2E0604", "FF3A10"),
    "ice":       ("FFFFFF", "A8E8FF", "3E9BE0", "0B2340", "6FD3FF"),
    "lightning": ("FFFFF0", "FFE45A", "F2A400", "3A2400", "FFD23F"),
    "light":     ("FFFFFF", "FFF0C2", "E8B84A", "3E2A08", "FFE7A0"),
    "dark":      ("F0DCFF", "B07CF0", "6A2FC0", "16061F", "9E4DF2"),
    "blood":     ("FFD6D6", "FF4A5A", "A80E1E", "260306", "E0182A"),
    "wind":      ("FFFFFF", "C8FFE8", "4CC9A0", "06302A", "8CF2CC"),
    "poison":    ("F2FFB0", "9CFF40", "3E9E14", "0E2A04", "73E626"),
    "crit":      ("FFFFFF", "FFF27A", "FFC21A", "8A0E04", "FFE04A"),   # 치명타: 흰빛 → 노랑 → 주황
}

base = Image.open(BASE).convert("RGBA")
arr = np.asarray(base).astype(np.float32) / 255
rgb, alpha = arr[..., :3], arr[..., 3]
lum = rgb.mean(-1)
fill = np.clip((lum - .25) / .5, 0, 1)        # 1 = 글자 속(흰 부분), 0 = 외곽선(검은 부분)
H, W = alpha.shape
cell_top = META["glyphs"][0]["y"]
cell_h = META["glyphs"][0]["h"]
v = np.clip((np.arange(H, dtype=np.float32) - cell_top) / cell_h, 0, 1)[:, None]   # 글자 칸 안 세로 위치 0(위)~1(아래)


def bake(name, top, mid, bottom, ink, glow):
    t, m, b, k, g = map(hexrgb, (top, mid, bottom, ink, glow))
    grad = np.where(v[..., None] < .5, t + (m - t) * (v[..., None] / .5), m + (b - m) * ((v[..., None] - .5) / .5))
    shine = np.clip(1 - np.abs(v - .2) / .1, 0, 1)[..., None] * .35        # 위쪽 광택 띠
    body = np.clip(grad + shine * (1 - grad), 0, 1)
    color = k + (body - k) * fill[..., None]
    glyph = np.dstack([color, alpha])
    # 바깥 빛: 글자 모양을 넓게 번지게 해 속성 색으로 깔고, 그 위에 글자
    halo_a = np.asarray(Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(4))).astype(np.float32) / 255
    halo = np.dstack([np.broadcast_to(g, (H, W, 3)), halo_a * .7])
    out = halo.copy()
    a1 = glyph[..., 3:4]
    out[..., :3] = glyph[..., :3] * a1 + halo[..., :3] * (1 - a1)
    out[..., 3] = np.clip(glyph[..., 3] + halo[..., 3] * (1 - glyph[..., 3]), 0, 1)
    im = Image.fromarray((out * 255).clip(0, 255).astype(np.uint8), "RGBA")
    im.save(OUT / f"damage_font_{name}.png")
    return im


fonts = {name: bake(name, *style) for name, style in STYLES.items()}
print("baked", ", ".join(fonts))

if "preview" in sys.argv[1:] or True:
    glyph = {g["ch"]: g for g in META["glyphs"]}

    def word(im, text, scale=.5):
        parts = [im.crop((glyph[c]["x"] - 6, glyph[c]["y"] - 6, glyph[c]["x"] + glyph[c]["w"] + 6, glyph[c]["y"] + glyph[c]["h"] + 6)) for c in text]
        parts = [p.resize((round(p.width * scale), round(p.height * scale)), Image.LANCZOS) for p in parts]
        w = sum(p.width for p in parts) - 6 * (len(parts) - 1)
        out = Image.new("RGBA", (w, parts[0].height), (0, 0, 0, 0))
        x = 0
        for p in parts:
            out.alpha_composite(p, (x, 0))
            x += p.width - 6
        return out

    icon_path = ROOT / "Assets/sprites/crit_icon.png"
    icon = Image.open(icon_path).convert("RGBA") if icon_path.exists() else None
    rows = list(fonts.items())
    sheet = Image.new("RGBA", (760, 92 * len(rows) + 20), (34, 26, 38, 255))
    names = {"fire": "화염", "ice": "얼음", "lightning": "번개", "light": "빛", "dark": "어둠", "blood": "피", "wind": "바람", "poison": "독", "crit": "치명타"}
    for i, (name, im) in enumerate(rows):
        y = 10 + i * 92
        w = word(im, "1234", .55)
        sheet.alpha_composite(w, (150, y))
        if name == "crit":
            big = word(im, "256!", .75)
            sheet.alpha_composite(big, (420, y - 12))
            if icon is not None:
                ic = icon.copy(); ic.thumbnail((48, 48))
                sheet.alpha_composite(ic, (404, y - 18))
    sheet.save(ROOT / "art_src/fonts/element_fonts_preview.png")
    print("preview", sheet.size)
