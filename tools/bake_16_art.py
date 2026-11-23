"""16차 그림 굽기: 속성 몬스터 파츠 + 상인 자세

python tools/bake_16_art.py

- 속성 몬스터: art_src/raw/enemy_<종류>_parts_<속성>.png → Assets/sprites/enemy_<종류>_<파츠>_<속성>.png
  기존 파츠(Assets/sprites/enemy_<종류>_<파츠>.png)와 **같은 픽셀 크기**로 맞춘다. 그래서 리그의 관절 위치와 피벗을 그대로 쓰고 스프라이트만 바꿔 끼우면 된다.
  바람, 독은 자홍 키, 나머지는 초록 키.
- 상인: art_src/raw/merchant_poses.png(3×2 칸, 초록 키) → Assets/sprites/merchant_<자세>.png (기본, 인사, 말하기, 구매 기쁨, 거절, 작별)
  모든 자세를 같은 배율로 줄이고, 칸 안의 발 위치(그림 아래 끝)를 맞춰 아래 가운데 피벗으로 쓸 수 있게 한다.
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw"
OUT = ROOT / "Assets/sprites"

from scipy import ndimage  # noqa: E402

PARTS = {
    "stalker": ["head", "torso", "uarm", "farm", "thigh", "shin"],
    "charger": ["head", "torso", "uarm", "farm", "thigh", "shin", "foot"],
    "shooter": ["head", "jaw", "torso", "arm", "leg"],
    "boss": ["head", "torso", "skirt", "uarm", "farm"],
}
ELEMENTS = ["fire", "ice", "lightning", "light", "dark", "blood", "wind", "poison"]
MAGENTA = {"wind", "poison"}
POSES = ["idle", "greet", "talk", "happy", "refuse", "farewell"]


def key_alpha(rgb, key):
    r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
    if key == "green":
        dom = g - np.maximum(r, b)
        spill = g > np.maximum(r, b)
        fix = (r, np.where(spill, np.maximum(r, b), g), b)
    else:
        dom = np.minimum(r, b) - g
        fix = (np.where(dom > 0, np.minimum(r, g + 40), r), g, np.where(dom > 0, np.minimum(b, g + 40), b))
    a = np.clip(1.0 - (dom - 40) / 60.0, 0, 1)
    return np.stack(fix, -1).clip(0, 255).astype(np.uint8), a


def rows_in_order(found):
    found = sorted(found, key=lambda f: f[2][0].start)
    rows = []
    for f in found:
        y0, y1 = f[2][0].start, f[2][0].stop
        for row in rows:
            if y0 < row["y1"] - 20 and y1 > row["y0"] + 20:
                row["items"].append(f)
                row["y0"], row["y1"] = min(row["y0"], y0), max(row["y1"], y1)
                break
        else:
            rows.append({"y0": y0, "y1": y1, "items": [f]})
    rows.sort(key=lambda r: r["y0"])
    return [f for row in rows for f in sorted(row["items"], key=lambda f: f[2][1].start)]


def pieces(path, key, count, merge=12):
    rgb = np.asarray(Image.open(path).convert("RGB"))
    col, a = key_alpha(rgb, key)
    mask = a > .16
    lab, _ = ndimage.label(ndimage.binary_dilation(mask, iterations=merge))
    found = []
    for i, sl in enumerate(ndimage.find_objects(lab)):
        found.append((int((mask[sl] & (lab[sl] == i + 1)).sum()), i + 1, sl))
    found = sorted(found, reverse=True)[:count]
    out = []
    for _, li, sl in rows_in_order(found):
        sub_a = a[sl].copy()
        sub_a[lab[sl] != li] = 0
        ys, xs = np.nonzero(sub_a > .16)
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        out.append((col[sl][y0:y1, x0:x1].copy(), sub_a[y0:y1, x0:x1]))
    return out


def tight(im):
    a = np.asarray(im)[..., 3]
    ys, xs = np.nonzero(a > 40)
    return ys.min(), ys.max() + 1, xs.min(), xs.max() + 1


def bake_enemies():
    for t, parts in PARTS.items():
        for e in ELEMENTS:
            src = RAW / f"enemy_{t}_parts_{e}.png"
            if not src.exists():
                continue
            found = pieces(src, "magenta" if e in MAGENTA else "green", len(parts))
            if len(found) != len(parts):
                print("SKIP (part count)", src.name, len(found), "!=", len(parts))
                continue
            for (col, a), part in zip(found, parts):
                base = Image.open(OUT / f"enemy_{t}_{part}.png").convert("RGBA")
                by0, by1, bx0, bx1 = tight(base)
                im = Image.fromarray(np.dstack([col, (a * 255).astype(np.uint8)]), "RGBA")
                im = im.resize((bx1 - bx0, by1 - by0), Image.LANCZOS)
                canvas = Image.new("RGBA", base.size, (0, 0, 0, 0))
                canvas.paste(im, (bx0, by0))
                canvas.save(OUT / f"enemy_{t}_{part}_{e}.png")
            print("wrote", t, e)


def bake_merchant():
    src = RAW / "merchant_poses.png"
    if not src.exists():
        return
    rgb = np.asarray(Image.open(src).convert("RGB"))
    col, a = key_alpha(rgb, "green")
    a = np.where(a > .16, a, 0)
    h, w = a.shape
    cells = []
    for r in range(2):
        for c in range(3):
            y0, y1, x0, x1 = r * h // 2, (r + 1) * h // 2, c * w // 3, (c + 1) * w // 3
            sub = a[y0:y1, x0:x1]
            ys, xs = np.nonzero(sub > .16)
            cells.append((col[y0:y1, x0:x1], sub, ys.min(), ys.max() + 1, xs.min(), xs.max() + 1))
    # 같은 배율, 같은 캔버스: 가장 큰 자세 기준. 발(아래 끝)은 캔버스 아래, 몸 가운데는 캔버스 가운데
    W = max(c[5] - c[4] for c in cells) + 16
    H = max(c[3] - c[2] for c in cells) + 16
    scale = min(1.0, 512 / H)
    for (cc, ca, y0, y1, x0, x1), pose in zip(cells, POSES):
        im = Image.fromarray(np.dstack([cc[y0:y1, x0:x1], (ca[y0:y1, x0:x1] * 255).astype(np.uint8)]), "RGBA")
        canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        canvas.paste(im, ((W - (x1 - x0)) // 2, H - 8 - (y1 - y0)))
        canvas = canvas.resize((round(W * scale), round(H * scale)), Image.LANCZOS)
        canvas.save(OUT / f"merchant_{pose}.png")
        print("wrote merchant", pose, canvas.size)


DECOR = {
    "fire": ["brazier", "charred_mannequin", "ember_pile", "scorched_machine"],
    "ice": ["frozen_mannequin", "icicle_candle", "snow_yarn", "ice_crystals"],
    "lightning": ["spool_coil", "sparking_lamp", "copper_tangle", "struck_crack"],
    "light": ["glass_shards", "candle_altar", "gilded_mirror", "praying_statue"],
    "dark": ["shadow_crack", "thorn_cage", "void_doll", "dark_obelisk"],
    "blood": ["blood_pool", "rag_rack", "thread_cocoon", "bleeding_cushion"],
    "wind": ["pinwheel_stand", "torn_sail", "leaf_pile", "weathervane"],
    "poison": ["toxic_puddle", "mushroom_cluster", "cracked_vials", "rotting_stump"],
}


def bake_decor():
    """2×2 칸마다 그 안의 그림 전부를 한 장으로. 긴 변 320px (12차 장식과 같은 크기)."""
    for e, names in DECOR.items():
        src = RAW / f"decor_{e}.png"
        if not src.exists():
            continue
        rgb = np.asarray(Image.open(src).convert("RGB"))
        col, a = key_alpha(rgb, "magenta" if e in MAGENTA else "green")
        a = np.where(a > .16, a, 0)
        h, w = a.shape
        i = 0
        for r in range(2):
            for c in range(2):
                y0, y1, x0, x1 = r * h // 2, (r + 1) * h // 2, c * w // 2, (c + 1) * w // 2
                sub = a[y0:y1, x0:x1]
                ys, xs = np.nonzero(sub > .16)
                if len(ys) == 0:
                    continue
                by0, by1 = max(0, ys.min() - 4), min(sub.shape[0], ys.max() + 5)
                bx0, bx1 = max(0, xs.min() - 4), min(sub.shape[1], xs.max() + 5)
                im = Image.fromarray(np.dstack([col[y0 + by0:y0 + by1, x0 + bx0:x0 + bx1],
                                                (sub[by0:by1, bx0:bx1] * 255).astype(np.uint8)]), "RGBA")
                s = min(1, 320 / max(im.size))
                im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
                im.save(OUT / f"decor_{e}_{names[i]}.png")
                i += 1
        print("wrote decor", e, i)


if __name__ == "__main__":
    bake_enemies()
    bake_merchant()
    bake_decor()
