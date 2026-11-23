"""결박 리본 미리보기: 코어·외곽을 게임처럼 색 입혀 가산 합성한 한 장 (art_src/vfx/bind/preview.png).

python tools/preview_bind_ribbons.py [fire lightning frost plain]
색은 Main 씬 db.bind.looks 와 같은 값(glow, color, core)을 쓴다.
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "Assets/vfx/bind"
FRAMES = 8
LOOKS = {
    "fire": ((1, .35, .05), (1, .55, .12), (1, .93, .6)),
    "lightning": ((1, .8, .2), (1, .96, .6), (1, 1, 1)),
    "frost": ((.35, .8, 1), (.7, .95, 1), (.95, 1, 1)),
    "plain": ((1, .85, .4), (1, .82, .35), (1, .98, .85)),
    "dark": ((.45, .15, .75), (.62, .3, .95), (.9, .75, 1)),
    "blood": ((.8, .05, .1), (.95, .15, .18), (1, .7, .7)),
    "wind": ((.55, .95, .8), (.8, 1, .92), (1, 1, 1)),
    "poison": ((.45, .9, .15), (.65, 1, .3), (.92, 1, .75)),
}


def load(name):
    return np.asarray(Image.open(SRC / name).convert("RGBA"), np.float32) / 255


def tint(img, color, alpha=1.0):
    return img[..., :3] * np.array(color) * img[..., 3:4] * alpha


def compose(style, kind, frame=None):
    glow, color, core = LOOKS[style]
    c, r = load(f"bind_{style}_{kind}_core.png"), load(f"bind_{style}_{kind}_rim.png")
    if frame is not None:
        h = c.shape[0] // FRAMES
        top = c.shape[0] - (frame + 1) * h   # 0번이 맨 아래
        c, r = c[top:top + h], r[top:top + h]
    out = np.full(c.shape[:2] + (3,), (.09, .06, .08), np.float32)
    out += tint(r, glow, .5) + tint(r, color, .9) + tint(c, core, 1)
    return Image.fromarray((out.clip(0, 1) * 255).astype(np.uint8))


styles = sys.argv[1:] or [s for s in LOOKS if (SRC / f"bind_{s}_head_core.png").exists()]
rows = []
for s in styles:
    thread = [compose(s, "thread", f) for f in (0, 3)]
    trail = [compose(s, "trail", f) for f in (0, 4)]
    head = compose(s, "head").resize((128, 128))
    w = 512 + 8 + 512 + 8 + 128
    row = Image.new("RGB", (w, 96 * 2 + 128 * 2 + 24), (20, 14, 18))
    row.paste(thread[0], (0, 0)); row.paste(thread[1], (0, 100))
    row.paste(trail[0], (520, 0)); row.paste(trail[1], (520, 132))
    row.paste(head, (1040, 0))
    rows.append(row)
sheet = Image.new("RGB", (rows[0].width, sum(r.height for r in rows)), (20, 14, 18))
y = 0
for r in rows:
    sheet.paste(r, (0, y)); y += r.height
out = ROOT / "art_src/vfx/bind/preview.png"
sheet.save(out)
print(out)
