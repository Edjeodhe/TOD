"""21차 피해 숫자 그림 글꼴: Codex 로 그린 글자 시트(0~9 + - !) → 흐린 검은 외곽을 두른 아틀라스.

python tools/bake_number_font.py [a|b|23_a|23_b|23_c|23_d]      (기본 a)
  art_src/raw/number21_<판>.png  (Codex, tools/codex_prompts/number21_<판>.txt, 참고 art_src/raw/number21_ref.png = 주아 배치)
      a = 두툼한 펠트 + 안쪽 홈질,  b = 찢은 양피지
  23차: "23_b" 처럼 차수를 붙이면 art_src/raw/number23_b.png (같은 배치: 윗줄 0~6,아랫줄 7 8 9 + - !)
      23_a 만화, 23_b 붓 잉크 (사용자가 고름, 지금 게임에 들어간 것), 23_c 금박 필사본, 23_d 십자수
  → Assets/Art/Generated/number_font.png        흰 글자 (게임에서 피해 색을 곱해 물들인다 = visual.damageFont)
  → Assets/Art/Generated/number_font_crit.png   치명타: 흰빛 → 노랑 → 주황 (= visual.critFont)
  → art_src/fonts/number_font.json              글자별 x, y, w, h (위에서 아래 기준, 두 아틀라스 공용)
  → art_src/fonts/number_font_preview.png       검토용
Unity 메뉴 TOD/Setup/1. Texture Imports 가 number_font*.png 를 이 JSON 으로 자른다 (number_font_<글자 코드>).
속성 피해 숫자는 옛 damage_font_<속성>.png (bake_element_fonts.py) 그대로.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
VARIANT = (sys.argv[1:] or ["a"])[0]
SRC = ROOT / (f"art_src/raw/number{VARIANT}.png" if "_" in VARIANT else f"art_src/raw/number21_{VARIANT}.png")
OUT = ROOT / "Assets/Art/Generated"
OUT_JSON = ROOT / "art_src/fonts/number_font.json"
CHARS = "0123456789+-!"
DIGIT_H = 104        # 숫자 높이 (아틀라스 px)
CORE, CORE_BLUR = 4, 2.0     # 외곽 심: 글자 바로 둘레는 짙게
HAZE, HAZE_BLUR, HAZE_ALPHA = 10, 5.5, .55   # 바깥 번짐
MARGIN = HAZE + 12
PAD = 6

# ---------------------------------------------------------------- 초록 키 + 글자 나누기 (bake_damage_font.py 와 같은 방식)
rgb = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32)
r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
dom = g - np.maximum(r, b)
alpha = np.clip(1 - (dom - 40) / 60, 0, 1)
g2 = np.where(g > np.maximum(r, b), np.maximum(r, b), g)
col = np.stack([r, g2, b], -1).clip(0, 255) / 255
mask = alpha > .16
DILATE = 10 if "_" in VARIANT else 20   # 23차 시트는 글자 사이가 좁아 20이면 이웃 글자가 붙는다 (4~12 모두 13자)
lab, _ = ndimage.label(ndimage.binary_dilation(mask, iterations=DILATE))   # ! 의 점까지 한 글자로
pieces = [(sl, i + 1, int((mask[sl] & (lab[sl] == i + 1)).sum())) for i, sl in enumerate(ndimage.find_objects(lab))]
pieces = sorted([p for p in pieces if p[2] > 400], key=lambda p: -p[2])[:len(CHARS)]
assert len(pieces) == len(CHARS), f"글자 {len(pieces)}개 (필요 {len(CHARS)})"
cy = np.array([(p[0][0].start + p[0][0].stop) / 2 for p in pieces])
split = (cy.min() + cy.max()) / 2
rows = [sorted([p for p in pieces if (p[0][0].start + p[0][0].stop) / 2 < split], key=lambda p: p[0][1].start),
        sorted([p for p in pieces if (p[0][0].start + p[0][0].stop) / 2 >= split], key=lambda p: p[0][1].start)]
assert len(rows[0]) == 7 and len(rows[1]) == 6, f"줄 나눔 {len(rows[0])}, {len(rows[1])}"

# 줄마다 숫자 위·아래를 기준선으로 같은 칸 높이에 (+, - 는 그 칸 안 원래 자리)
raw = []
for row in rows:
    tall = max(q[0][0].stop - q[0][0].start for q in row)
    digits = [p for p in row if (p[0][0].stop - p[0][0].start) > .6 * tall]
    top = min(p[0][0].start for p in digits)
    bottom = max(p[0][0].stop for p in digits)
    for sl, li, _ in row:
        x0, x1 = sl[1].start, sl[1].stop
        a = alpha[top:bottom, x0:x1].copy()
        a[lab[top:bottom, x0:x1] != li] = 0
        raw.append(np.dstack([col[top:bottom, x0:x1], a]).astype(np.float32))


def resize(px, scale):
    h, w = px.shape[:2]
    size = (max(1, round(w * scale)), max(1, round(h * scale)))
    premul = np.dstack([px[..., :3] * px[..., 3:4], px[..., 3]])
    out = np.asarray(Image.fromarray((premul * 255).astype(np.uint8), "RGBA").resize(size, Image.LANCZOS)).astype(np.float32) / 255
    a = out[..., 3:4]
    return np.dstack([out[..., :3] / np.maximum(a, 1e-4), out[..., 3]])


def blurred(m, grow, blur):
    im = Image.fromarray((m * 255).astype(np.uint8))
    if grow:
        im = im.filter(ImageFilter.MaxFilter(grow * 2 + 1))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(blur))).astype(np.float32) / 255


def outlined(px, fill_tint):
    """글자 그림 + 흐린 검은 외곽 (짙은 심 + 옅은 번짐). fill_tint(h, w) = 글자에 곱할 색."""
    h, w = px.shape[:2]
    big = np.zeros((h + MARGIN * 2, w + MARGIN * 2, 4), np.float32)
    big[MARGIN:MARGIN + h, MARGIN:MARGIN + w] = px
    a = big[..., 3]
    ink = np.maximum(np.clip(blurred(a, CORE, CORE_BLUR) * 1.3, 0, 1), blurred(a, HAZE, HAZE_BLUR) * HAZE_ALPHA)
    oa = a + ink * (1 - a)
    rgb = big[..., :3] * fill_tint(*a.shape) * (a / np.maximum(oa, 1e-4))[..., None]   # 검은 바탕 위에 글자
    return np.dstack([np.clip(rgb, 0, 1), oa])


def white(h, w):
    return np.ones((h, w, 3), np.float32)


# 치명타 색 (위 → 가운데 → 아래). 21차 = 흰빛 → 노랑 → 주황, 23차 붓 잉크 = 피빛 붉은 붓글씨 (밝은 핏빛 → 진홍 → 검붉음)
CRIT = ((255, 175, 160), (235, 45, 45), (135, 8, 22)) if "_" in VARIANT else ((255, 255, 250), (255, 232, 100), (255, 165, 30))


def crit(h, w):
    v = np.clip((np.arange(h, dtype=np.float32) - MARGIN) / (h - MARGIN * 2), 0, 1)[:, None, None]
    t, m, b = (np.array(c, np.float32) / 255 for c in CRIT)
    return np.broadcast_to(np.where(v < .45, t + (m - t) * (v / .45), m + (b - m) * ((v - .45) / .55)), (h, w, 3)).astype(np.float32)


scale = DIGIT_H / raw[0].shape[0]
glyph_src = [resize(px, scale) for px in raw]


def atlas(tint):
    glyphs = [outlined(px, tint) for px in glyph_src]
    cell_h = max(g.shape[0] for g in glyphs)
    width = sum(g.shape[1] for g in glyphs) + PAD * (len(glyphs) + 1)
    height = cell_h + PAD * 2
    out = np.zeros((height, width, 4), np.float32)
    meta, x = [], PAD
    for ch, gl in zip(CHARS, glyphs):
        out[PAD:PAD + gl.shape[0], x:x + gl.shape[1]] = gl
        meta.append({"ch": ch, "x": x, "y": PAD, "w": gl.shape[1], "h": cell_h})
        x += gl.shape[1] + PAD
    # 크로마키 초록이 붓 가장자리에 남지 않게: 초록이 빨강·파랑보다 세면 그 둘 중 큰 값으로 누른다
    rb = np.maximum(out[..., 0], out[..., 2])
    out[..., 1] = np.minimum(out[..., 1], rb)
    return Image.fromarray((out * 255).round().clip(0, 255).astype(np.uint8), "RGBA"), meta, width, height


plain, meta, width, height = atlas(white)
gold, _, _, _ = atlas(crit)
plain.save(OUT / "number_font.png")
gold.save(OUT / "number_font_crit.png")
OUT_JSON.write_text(json.dumps({"width": width, "height": height, "glyphs": meta}, ensure_ascii=False, indent=1), encoding="utf-8")

# ---------------------------------------------------------------- 검토용
by = {m["ch"]: m for m in meta}


def word(im, text, s=.5, tint=(1, 1, 1)):
    parts = [im.crop((by[c]["x"], by[c]["y"], by[c]["x"] + by[c]["w"], by[c]["y"] + by[c]["h"])) for c in text]
    parts = [p.resize((round(p.width * s), round(p.height * s)), Image.LANCZOS) for p in parts]
    over = round(MARGIN * 2 * s * .85)
    o = Image.new("RGBA", (sum(p.width for p in parts) - over * (len(parts) - 1), parts[0].height), (0, 0, 0, 0))
    x = 0
    for p in parts:
        a = np.asarray(p).astype(np.float32)
        a[..., :3] *= np.array(tint, np.float32)
        o.alpha_composite(Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA"), (x, 0))
        x += p.width - over
    return o


sheet = Image.new("RGBA", (760, 120), (58, 44, 52, 255))
sheet.alpha_composite(word(plain, "1234"), (20, 22))
sheet.alpha_composite(word(plain, "56", tint=(1, .86, .55)), (250, 22))
sheet.alpha_composite(word(plain, "+7", tint=(.6, 1, .6)), (380, 22))
sheet.alpha_composite(word(gold, "256!", .62), (510, 10))
sheet.save(ROOT / f"art_src/fonts/number_font_preview_{VARIANT}.png")
print("number font", VARIANT, plain.size)
