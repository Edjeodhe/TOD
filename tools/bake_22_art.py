"""22차 그림 굽기 (sin-art): 칠죄종 조각 아이콘, 신화 도안, 죄종 몬스터 파츠, 죄종 정적 소품

python tools/bake_22_art.py [icons] [patterns] [enemies] [decor]   (인자 없으면 전부)

- 아이콘: art_src/raw/sin_icons_<a..d>.png → Assets/sprites/item_<죄종>_<1~5>.png (긴 변 256, 16차 이전 item_* 와 같음)
- 도안: art_src/raw/sin_patterns.png → item_pattern_6, 7.png. 1~7 모두 붉게 물든 item_pattern_<n>_tainted.png (색만 입힘, 모양과 크기 그대로)
- 몬스터: art_src/raw/enemy_<종류>_parts_<죄종>.png → Assets/sprites/enemy_<종류>_<파츠>_<죄종>.png
  가로세로 비율이 원래 파츠와 10% 안이면 16차처럼 원래 파츠 칸에 꼭 맞춘다.
  더 다르면(왕관, 부푼 배처럼 새 디자인이 튀어나옴) 찌그러뜨리지 않고 같은 배율로 줄인 뒤 관절 끝(팔다리와 몸통은 위, 머리는 아래)을 원래 자리에 맞추고,
  튀어나온 만큼 캔버스를 넓힌다. 그때의 피벗을 art_src/raw/sin_parts_fit.json 에 적고 TOD/Setup/16. Element Art 가 그 피벗으로 가져온다(PPU 는 원래 파츠 그대로).
- 소품: art_src/raw/decor_<죄종>.png (2×2 칸) → Assets/sprites/decor_<죄종>_<이름>.png (긴 변 320)
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw"
OUT = ROOT / "Assets/sprites"
FIT = RAW / "sin_parts_fit.json"

sys.path.insert(0, str(Path(__file__).resolve().parent))
from bake_16_art import key_alpha, pieces, PARTS, tight  # noqa: E402
from make_sin_prompts import ICON_SHEETS, DECOR, KEY  # noqa: E402

SINS = ["wrath", "sloth", "envy", "greed", "gluttony", "lust", "pride"]


def save(col, a, path, max_side):
    im = Image.fromarray(np.dstack([col, (a * 255).astype(np.uint8)]), "RGBA")
    s = min(1, max_side / max(im.size))
    if s < 1:
        im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
    pad = Image.new("RGBA", (im.width + 12, im.height + 12), (0, 0, 0, 0))   # 가장자리 6px 여백 (2차 아이콘과 같음)
    pad.paste(im, (6, 6))
    if max(pad.size) > max_side:
        pad = pad.resize((round(pad.width * max_side / max(pad.size)), round(pad.height * max_side / max(pad.size))), Image.LANCZOS)
    pad.save(path)
    return pad.size


# ---------------------------------------------------------------- 아이콘
def grid_cells(path, key, cols, rows):
    """떨어진 조각(연기, 꿈 구름)도 한 아이콘으로: 덩어리마다 무게중심이 든 칸에 모은다. 칸마다 (col, a) 를 꼭 맞게 자른다."""
    rgb = np.asarray(Image.open(path).convert("RGB"))
    col, a = key_alpha(rgb, key)
    a = np.where(a > .16, a, 0)
    h, w = a.shape
    lab, n = ndimage.label(a > .16)
    cells = [np.zeros((h, w), bool) for _ in range(cols * rows)]
    for i, sl in enumerate(ndimage.find_objects(lab)):
        m = lab[sl] == i + 1
        if m.sum() < 30:
            continue
        ys, xs = np.nonzero(m)
        cy, cx = ys.mean() + sl[0].start, xs.mean() + sl[1].start
        if rows == 1:
            r = 0
        else:
            r = min(rows - 1, int(cy / (h / rows)))
        c = min(cols - 1, int(cx / (w / cols)))
        cells[r * cols + c][sl] |= m
    out = []
    for m in cells:
        ys, xs = np.nonzero(m)
        if len(ys) == 0:
            out.append(None)
            continue
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        out.append((col[y0:y1, x0:x1].copy(), np.where(m, a, 0)[y0:y1, x0:x1]))
    return out


def bake_icons():
    for k, sins in ICON_SHEETS.items():
        src = RAW / f"sin_icons_{k}.png"
        if not src.exists():
            continue
        found = grid_cells(src, KEY.get(sins[0], "green"), 5, len(sins))
        if any(f is None for f in found):
            print("SKIP (empty cell)", src.name)
            continue
        for i, (col, a) in enumerate(found):
            s, idx = sins[i // 5], i % 5 + 1
            print("wrote", f"item_{s}_{idx}", save(col, a, OUT / f"item_{s}_{idx}.png", 256))


# ---------------------------------------------------------------- 도안
def taint(path_in, path_out, seed):
    """붉게 물든 도안: 밝기는 살리고 색을 핏빛으로 끌어내리고, 실루엣 안에 핏자국 얼룩과 흘러내린 자국. 검은 외곽선과 금속 반짝임은 남긴다."""
    im = np.asarray(Image.open(path_in).convert("RGBA")).astype(np.float32) / 255
    rgb, a = im[..., :3], im[..., 3]
    h, w = a.shape
    lum = rgb @ np.array([.299, .587, .114])
    blood = np.array([.62, .07, .09])
    dark = np.array([.24, .02, .04])
    tinted = dark + (blood - dark) * np.clip(lum * 1.35, 0, 1)[..., None]
    tinted = tinted + np.clip(lum - .72, 0, 1)[..., None] * np.array([1.1, .75, .45])   # 금속 반짝임은 붉은 금빛으로 남김
    edge = np.clip(1 - ndimage.distance_transform_edt(a > .5) / (.18 * max(h, w)), 0, 1)   # 가장자리부터 스며든 피
    mix = .42 + .4 * edge
    out = rgb * (1 - mix[..., None]) + tinted * mix[..., None]
    out = np.where((lum < .14)[..., None], rgb, out)   # 잉크 외곽선 그대로

    rng = np.random.default_rng(seed)
    inside = ndimage.binary_erosion(a > .6, iterations=6)
    stain = np.zeros((h, w), np.float32)
    yy, xx = np.mgrid[0:h, 0:w]
    ys, xs = np.nonzero(inside)
    for _ in range(9):   # 번진 핏자국
        i = rng.integers(len(ys))
        cy, cx = ys[i], xs[i]
        r = rng.uniform(.06, .14) * max(h, w)
        noise = ndimage.gaussian_filter(rng.random((h, w)).astype(np.float32), 6) * 2.2
        d = np.hypot(yy - cy, xx - cx) / r + noise - 1.1
        stain = np.maximum(stain, np.clip(1 - d, 0, 1) ** .6)
    for _ in range(5):   # 위에서 흘러내린 자국
        i = rng.integers(len(ys))
        cx, cy = xs[i], ys[i]
        length = rng.uniform(.15, .35) * h
        width = rng.uniform(2.5, 5)
        drip = (np.abs(xx - cx) < width * (1 - np.clip((yy - cy) / length, 0, 1) * .4)) & (yy >= cy) & (yy < cy + length)
        bulb = np.hypot(yy - (cy + length), xx - cx) < width * 1.6
        stain = np.maximum(stain, (drip | bulb).astype(np.float32))
    for _ in range(26):   # 튄 핏방울
        i = rng.integers(len(ys))
        stain = np.maximum(stain, (np.hypot(yy - ys[i], xx - xs[i]) < rng.uniform(1.5, 4.5)).astype(np.float32))
    stain = ndimage.gaussian_filter(stain, 1.0) * inside
    wet = np.array([.30, .01, .03])
    out = out * (1 - stain[..., None] * .88) + wet * stain[..., None] * .88
    rim = np.clip(ndimage.gaussian_filter(stain, 2.5) - stain, 0, 1)   # 마른 가장자리는 조금 더 어둡게
    out = out * (1 - rim[..., None] * .5)
    Image.fromarray((np.dstack([np.clip(out, 0, 1), a]) * 255).astype(np.uint8), "RGBA").save(path_out)


def bake_patterns():
    src = RAW / "sin_patterns.png"
    if src.exists():
        found = grid_cells(src, "green", 2, 1)
        for i, (col, a) in enumerate(f for f in found if f):
            print("wrote", f"item_pattern_{6 + i}", save(col, a, OUT / f"item_pattern_{6 + i}.png", 256))
    for n in range(1, 8):
        p = OUT / f"item_pattern_{n}.png"
        if p.exists():
            taint(p, OUT / f"item_pattern_{n}_tainted.png", 22 + n)
            print("wrote", f"item_pattern_{n}_tainted")


# ---------------------------------------------------------------- 몬스터 파츠
LENGTH = {"uarm", "farm", "thigh", "shin", "arm", "leg", "torso", "skirt"}   # 위 끝이 관절, 세로 길이를 맞춤
BOTTOM = {"head"}                                                           # 아래 끝(목)이 관절


def end_center(a, top, frac=.15):
    ys, xs = np.nonzero(a > .4)
    h = ys.max() - ys.min() + 1
    sel = ys <= ys.min() + h * frac if top else ys >= ys.max() - h * frac
    return xs[sel].mean()


def fit_part(col, a, base, part):
    """새 파츠(col, a)를 원래 파츠 캔버스에 맞춘다. 반환: RGBA 그림, 피벗(없으면 None = 원래와 같음)."""
    by0, by1, bx0, bx1 = tight(base)
    bw, bh = bx1 - bx0, by1 - by0
    nh, nw = a.shape
    im = Image.fromarray(np.dstack([col, (a * 255).astype(np.uint8)]), "RGBA")
    ratio = (nw / nh) / (bw / bh)
    if .9 < ratio < 1.1:   # 16차와 같이 칸에 꼭 맞춤
        canvas = Image.new("RGBA", base.size, (0, 0, 0, 0))
        canvas.paste(im.resize((bw, bh), Image.LANCZOS), (bx0, by0))
        return canvas, None, ratio
    ba = np.asarray(base)[..., 3] / 255
    if part in LENGTH:
        s = bh / nh
    else:
        s = max(bw / nw, bh / nh)
    sw, sh = max(1, round(nw * s)), max(1, round(nh * s))
    im = im.resize((sw, sh), Image.LANCZOS)
    sa = np.asarray(im)[..., 3] / 255
    top = part not in BOTTOM
    # 관절 끝의 가운데 x 와 끝 y 를 원래 파츠에 맞춘다
    ox = bx0 + end_center(ba[by0:by1, bx0:bx1], top) - end_center(sa, top)
    oy = by0 if top else by1 - sh
    ox = round(ox)
    W, H = base.size
    L = max(0, 9 - ox)
    T = max(0, 9 - oy)
    R = max(0, ox + sw + 9 - W)
    B = max(0, oy + sh + 9 - H)
    canvas = Image.new("RGBA", (W + L + R, H + T + B), (0, 0, 0, 0))
    canvas.paste(im, (ox + L, oy + T), im)
    if L == T == R == B == 0:
        return canvas, None, ratio
    # 원래 피벗 (0.5, 0.5) = 원래 캔버스 가운데. Unity 피벗 y 는 아래에서 잰다
    pivot = [(L + W / 2) / canvas.width, (B + H / 2) / canvas.height]
    return canvas, pivot, ratio


def bake_enemies():
    fit = {i["name"]: i for i in json.loads(FIT.read_text(encoding="utf-8"))["items"]} if FIT.exists() else {}
    for t, parts in PARTS.items():
        for s in SINS:
            src = RAW / f"enemy_{t}_parts_{s}.png"
            if not src.exists():
                continue
            for merge in (12, 6, 3):   # 파츠끼리 가까우면 덜 붙여 본다
                found = pieces(src, KEY.get(s, "green"), len(parts), merge)
                if len(found) == len(parts):
                    break
            if len(found) != len(parts):
                print("SKIP (part count)", src.name, len(found), "!=", len(parts))
                continue
            notes = []
            for (col, a), part in zip(found, parts):
                base = Image.open(OUT / f"enemy_{t}_{part}.png").convert("RGBA")
                canvas, pivot, ratio = fit_part(col, a, base, part)
                name = f"enemy_{t}_{part}_{s}"
                canvas.save(OUT / f"{name}.png")
                if pivot:
                    fit[name] = {"name": name, "pivot": [round(pivot[0], 4), round(pivot[1], 4)]}
                    notes.append(f"{part} {ratio:.2f} fit")
                else:
                    fit.pop(name, None)
                    if abs(ratio - 1) > .03:
                        notes.append(f"{part} {ratio:.2f}")
            print("wrote", t, s, ", ".join(notes))
    # Unity JsonUtility 로 읽는 형식: {"items": [{"name", "pivot": [x, y]}]}
    FIT.write_text(json.dumps({"items": sorted(fit.values(), key=lambda i: i["name"])}, indent=1), encoding="utf-8")


# ---------------------------------------------------------------- 소품
def bake_decor():
    for s, (names, _, _) in DECOR.items():
        src = RAW / f"decor_{s}.png"
        if not src.exists():
            continue
        rgb = np.asarray(Image.open(src).convert("RGB"))
        col, a = key_alpha(rgb, KEY.get(s, "green"))
        a = np.where(a > .16, a, 0)
        h, w = a.shape
        i = 0
        for r in range(2):
            for c in range(2):
                y0, y1, x0, x1 = r * h // 2, (r + 1) * h // 2, c * w // 2, (c + 1) * w // 2
                sub = a[y0:y1, x0:x1]
                lab, n = ndimage.label(sub > .16)
                if n == 0:
                    continue
                sizes = ndimage.sum(sub > .16, lab, range(1, n + 1))
                keep = np.isin(lab, 1 + np.nonzero(sizes >= 40)[0])   # 작은 키 찌꺼기 빼기
                # 이웃 칸에서 넘어온 작은 조각: 큰 덩어리(가장 큰 것의 15% 이상)에서 40px 넘게 떨어진 작은 덩어리는 뺀다
                big = np.isin(lab, 1 + np.nonzero(sizes >= sizes.max() * .15)[0])
                near = ndimage.binary_dilation(big, iterations=40)
                for j in np.nonzero((sizes >= 40) & (sizes < sizes.max() * .15))[0]:
                    m = lab == j + 1
                    if not (m & near).any():
                        keep &= ~m
                ys, xs = np.nonzero(keep)
                by0, by1 = max(0, ys.min() - 4), min(sub.shape[0], ys.max() + 5)
                bx0, bx1 = max(0, xs.min() - 4), min(sub.shape[1], xs.max() + 5)
                sa = np.where(keep, sub, 0)[by0:by1, bx0:bx1]
                im = Image.fromarray(np.dstack([col[y0 + by0:y0 + by1, x0 + bx0:x0 + bx1], (sa * 255).astype(np.uint8)]), "RGBA")
                k = min(1, 320 / max(im.size))
                im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
                im.save(OUT / f"decor_{s}_{names[i]}.png")
                i += 1
        print("wrote decor", s, i)


if __name__ == "__main__":
    what = set(sys.argv[1:]) or {"icons", "patterns", "enemies", "decor"}
    if "icons" in what:
        bake_icons()
    if "patterns" in what:
        bake_patterns()
    if "enemies" in what:
        bake_enemies()
    if "decor" in what:
        bake_decor()
