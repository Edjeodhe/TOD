"""20차 모이라 점성구 다시 굽기: art_src/raw/moira_ball2.png (초록 키) →
  Assets/sprites/moira_ball.png      리그용. 옛 그림과 같은 캔버스(136×149)와 같은 자리(아래 가운데 피벗)에 맞춘다 → 리그 값은 그대로.
  Assets/sprites/moira_ball_big.png  운명 읽기 연출용 큰 그림 (긴 변 512)
  Assets/sprites/moira_ball_glass.png 같은 캔버스의 유리만 흑백 (20차: 속성 색으로 물들여 위에 덮음. 받침 자리는 비움)

python tools/bake_moira_ball.py

굽는 동안 가장자리를 조금 더 어둡게(비네트) 하고, 받침은 아래로 갈수록 어둡게 해 탁자에 놓인 무게를 준다.
"""
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw/moira_ball2.png"
SMALL = ROOT / "Assets/sprites/moira_ball.png"
BIG = ROOT / "Assets/sprites/moira_ball_big.png"


def key_green(rgb):
    r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
    dom = g - np.maximum(r, b)
    spill = g > np.maximum(r, b)
    col = np.stack((r, np.where(spill, np.maximum(r, b), g), b), -1).clip(0, 255)
    a = np.clip(1.0 - (dom - 40) / 60.0, 0, 1)
    return col, a


def shade(col, a):
    """구의 가장자리를 더 어둡게, 받침(아래 20%)은 아래로 갈수록 어둡게."""
    h, w = a.shape
    ys, xs = np.nonzero(a > .5)
    top, bottom = ys.min(), ys.max()
    # 구 = 위쪽 78% 부분의 가로 폭으로 원을 잡는다
    cut = top + int((bottom - top) * .78)
    sphere = a[top:cut] > .5
    sx = np.nonzero(sphere.any(0))[0]
    cx, r = (sx.min() + sx.max()) / 2, (sx.max() - sx.min()) / 2
    cy = top + r
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / max(r, 1)
    vignette = 1 - .32 * np.clip((d - .45) / .55, 0, 1) ** 1.6
    stand = np.clip((yy - (top + (bottom - top) * .72)) / ((bottom - top) * .28), 0, 1)
    ground = 1 - .35 * stand
    return col * (vignette * ground)[..., None]


def fit(col, a, size, box, bottom_center):
    """그림(알파 영역)을 box(w, h) 안에 비율을 지켜 넣고, 아래 가운데를 bottom_center 에 맞춘 size 캔버스."""
    ys, xs = np.nonzero(a > .05)
    col, a = col[ys.min():ys.max() + 1, xs.min():xs.max() + 1], a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    im = Image.fromarray(np.dstack([col.clip(0, 255).astype(np.uint8), (a * 255).astype(np.uint8)]), "RGBA")
    s = min(box[0] / im.width, box[1] / im.height)
    im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    out.paste(im, (round(bottom_center[0] - im.width / 2), bottom_center[1] - im.height + 1), im)
    return out


def glass_layer(col, a):
    """유리(구 안이면서 놋쇠 받침의 갈색이 아닌 곳)만 남기고 밝기를 흑백으로. 안개가 가장 밝은 곳 = 흰색."""
    h, w = a.shape
    ys, xs = np.nonzero(a > .5)
    top, bottom = ys.min(), ys.max()
    cut = top + int((bottom - top) * .78)
    sx = np.nonzero((a[top:cut] > .5).any(0))[0]
    cx, r = (sx.min() + sx.max()) / 2, (sx.max() - sx.min()) / 2
    cy = top + r
    yy, xx = np.mgrid[0:h, 0:w]
    inside = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) <= r * 1.01
    rr, gg, bb = col[..., 0], col[..., 1], col[..., 2]
    brass = (rr > bb + 12) & (rr > gg - 4)
    keep = inside & ~brass & (a > .05)
    lum = .3 * rr + .55 * gg + .15 * bb + .35 * bb   # 보랏빛 안개가 밝게 남도록 파랑을 조금 더
    top_l = np.percentile(lum[keep], 99.5) if keep.any() else 255
    g = np.clip(lum / max(top_l, 1) * 255, 0, 255)
    gray = np.stack([g, g, g], -1)
    return gray, np.where(keep, a, 0)


def main():
    rgb = np.asarray(Image.open(RAW).convert("RGB"))
    col, a = key_green(rgb)
    a = np.where(a > .16, a, 0)
    col = shade(col, a)
    old = Image.open(SMALL) if SMALL.exists() else None
    size = old.size if old else (136, 149)
    if old:
        oa = np.asarray(old)[..., 3]
        ys, xs = np.nonzero(oa > 20)
        box, bc = (xs.max() - xs.min() + 1, ys.max() - ys.min() + 1), (round((xs.min() + xs.max() + 1) / 2), int(ys.max()))
    else:
        box, bc = (132, 145), (68, 146)
    fit(col, a, size, box, bc).save(SMALL)
    ys, xs = np.nonzero(a > .05)
    w, h = xs.max() - xs.min() + 1, ys.max() - ys.min() + 1
    s = 512 / max(w, h)
    big = (round(w * s) + 8, round(h * s) + 8)
    fit(col, a, big, (big[0] - 8, big[1] - 8), (big[0] // 2, big[1] - 5)).save(BIG)
    gcol, ga = glass_layer(col, a)
    # 큰 그림과 같은 자리: 같은 잘라내기(원래 알파 영역)로 맞추려고 알파 영역 밖은 아주 작은 값으로 테두리를 둔다
    ga_box = np.where(a > .05, np.where(ga > .1, ga, .06), 0)   # 자리 맞춤용 .06 은 아래에서 지움
    glass = fit(gcol, ga_box, big, (big[0] - 8, big[1] - 8), (big[0] // 2, big[1] - 5))
    ar = np.asarray(glass).copy()
    ar[..., 3] = np.where(ar[..., 3] <= 24, 0, ar[..., 3])
    Image.fromarray(ar, "RGBA").save(ROOT / "Assets/sprites/moira_ball_glass.png")
    print("small", size, box, bc, "big", big)


if __name__ == "__main__":
    main()
