"""Slice Codex-generated sheets into game textures.

python tools/bake_codex_art.py vfx    art_src/raw/vfx_sheet.png -> assets/vfx/fx_*.png (black -> alpha, additive)
python tools/bake_codex_art.py dodge  art_src/raw/hero_dodge.png -> assets/sprites/hero_dodge_{1,2,3}.png + hero_dodge.json
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
VFX_NAMES = ["chaos", "despair", "frustration", "snip", "sigil", "yank"]


def bake_vfx():
    sheet = np.asarray(Image.open(ROOT / "art_src/raw/vfx_sheet.png").convert("RGB")).astype(np.float32) / 255
    h, w = sheet.shape[:2]
    cw, ch = w // 3, h // 2
    for i, name in enumerate(VFX_NAMES):
        cell = sheet[(i // 3) * ch:(i // 3 + 1) * ch, (i % 3) * cw:(i % 3 + 1) * cw]
        # Additive art on black: alpha is the brightest channel, colour is un-premultiplied.
        alpha = cell.max(axis=2)
        alpha = np.clip((alpha - .035) / .965, 0, 1)
        color = np.where(alpha[..., None] > 1e-3, cell / np.maximum(cell.max(axis=2, keepdims=True), 1e-3), 0)
        rgba = np.dstack([color, alpha])
        ys, xs = np.nonzero(alpha > .02)
        cx, cy = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
        half = max(xs.max() - cx, cx - xs.min(), ys.max() - cy, cy - ys.min()) + 6
        box = (int(cx - half), int(cy - half), int(cx + half), int(cy + half))
        image = Image.fromarray((rgba * 255).astype(np.uint8), "RGBA").crop(box).resize((256, 256), Image.LANCZOS)
        image.save(ROOT / f"assets/vfx/fx_{name}.png")
        print("fx", name, box)


def bake_dodge():
    rgb = np.asarray(Image.open(ROOT / (sys.argv[2] if len(sys.argv) > 2 else "art_src/raw/hero_dodge.png")).convert("RGB")).astype(np.int16)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    green = (g - np.maximum(r, b)).clip(0, 255)
    alpha = 1 - np.clip((green - 40) / 90, 0, 1)
    mask = alpha > .5
    labels, count = ndimage.label(ndimage.binary_closing(mask, iterations=6))
    sizes = ndimage.sum(mask, labels, range(1, count + 1))
    keep = sorted([i + 1 for i, s in enumerate(sizes) if s > 4000],
                  key=lambda i: ndimage.center_of_mass(mask, labels, i)[1])
    assert len(keep) == 3, f"expected 3 figures, found {len(keep)}"
    # Remove green spill from anti-aliased edges.
    spill = np.minimum(g, np.maximum(r, b))
    clean = np.dstack([r, spill, b]).clip(0, 255)
    rgba = np.dstack([clean, (alpha * 255).astype(np.int16)]).astype(np.uint8)
    meta = {"frames": []}
    for n, label in enumerate(keep, 1):
        region = ndimage.binary_dilation(labels == label, iterations=4)
        ys, xs = np.nonzero(region & mask)
        x0, x1, y0, y1 = xs.min() - 4, xs.max() + 5, ys.min() - 4, ys.max() + 5
        piece = rgba[y0:y1, x0:x1].copy()
        piece[..., 3] = np.where(region[y0:y1, x0:x1], piece[..., 3], 0)
        Image.fromarray(piece, "RGBA").save(ROOT / f"assets/sprites/hero_dodge_{n}.png")
        # Foot anchor: centre of the lowest 6% of opaque rows.
        local = piece[..., 3] > 128
        rows = np.nonzero(local.any(axis=1))[0]
        floor = rows.max()
        band = local[int(floor - (floor - rows.min()) * .06):floor + 1]
        fx = float(np.nonzero(band.any(axis=0))[0].mean())
        meta["frames"].append({"img": f"hero_dodge_{n}.png", "w": int(x1 - x0), "h": int(y1 - y0),
                               "foot": [round(fx, 1), int(floor)]})
        print("dodge", n, (x0, y0, x1, y1), meta["frames"][-1]["foot"])
    (ROOT / "assets/sprites/hero_dodge.json").write_text(json.dumps(meta, indent=1))


if __name__ == "__main__":
    {"vfx": bake_vfx, "dodge": bake_dodge}[sys.argv[1]]()
