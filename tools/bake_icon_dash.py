"""25차 머리 위 대시 칸 스프라이트 시트: Codex 그림(초록 키, 왼쪽 = 꽉 찬 칸, 오른쪽 = 쓰는 순간 터짐)
→ Assets/sprites/icon_dash_sheet.png (정사각 CELL px 칸 가로 띠, 16칸)

python tools/bake_icon_dash.py      # + Temp/Shots/icon_dash_sheet_preview.png (어두운 바탕에 크게)

칸 0~10 = 차오름 (0 빈 칸 → 10 꽉 찬 칸): 빈 칸 위에 꽉 찬 그림을 왼쪽부터 드러내고, 차오르는 끝에 밝은 선.
칸 11~15 = 쓰는 순간: 하얗게 번쩍 → 터짐 → 터짐이 흩어지며 빈 칸 → 빈 칸.
빈 칸은 꽉 찬 그림을 잿빛 자두색으로 눌러서 만든다 (실루엣이 꼭 같게).
Unity: Sprite Multiple, ProjectSetup.SliceFlipbook 로 정사각 칸 자르기 → db.hud.dashHead.fill = 0~10, use = 11~15.
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

from bake_hub_art import key_green

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "art_src/raw/icon_dash_sheet.png"
OUT = ROOT / "Assets/sprites/icon_dash_sheet.png"
PREVIEW = ROOT / "Temp/Shots/icon_dash_sheet_preview.png"
CELL = 192
ICON_W = 150          # 꽉 찬 아이콘 가로 (칸 안)
FILL_STEPS = 11
TONE = .68          # 25차: 사용자 "너무 밝아" → 색만 이만큼 어둡게 (검은 외곽은 그대로 검음)


def half(col, a, side):
    """왼쪽/오른쪽 반에서 그림 전부 (RGBA float, 0~1)"""
    w = a.shape[1] // 2
    sl = slice(0, w) if side == 0 else slice(w, None)
    c, al = col[:, sl].astype(np.float32) / 255, a[:, sl]
    mask = al > .3
    lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=6))
    sizes = ndimage.sum(mask, lab, range(1, n + 1))
    keep = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 40])
    al = np.where(ndimage.binary_dilation(keep, iterations=3), al, 0)
    ys, xs = np.nonzero(al > .05)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    return np.dstack([c[y0:y1, x0:x1], al[y0:y1, x0:x1]])


def body_box(img):
    """가장 큰 덩어리(화살 몸통)의 상자"""
    mask = img[..., 3] > .5
    lab, n = ndimage.label(mask)
    big = np.argmax(ndimage.sum(mask, lab, range(1, n + 1))) + 1
    ys, xs = np.nonzero(lab == big)
    return xs.min(), xs.max() + 1, ys.min(), ys.max() + 1


def place(img, scale, center, size=CELL):
    """img 를 scale 배로 줄여 칸의 (가운데 + 0) 에 img 의 center(px) 가 오게 놓는다"""
    h, w = img.shape[:2]
    im = Image.fromarray((img * 255).clip(0, 255).astype(np.uint8), "RGBA").resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
    out = Image.new("RGBA", (size, size))
    cx, cy = center[0] * scale, center[1] * scale
    out.paste(im, (round(size / 2 - cx), round(size / 2 - cy)))   # 빈 칸에 붙이기 (음수 자리도 됨, 넘치면 잘림)
    return np.asarray(out).astype(np.float32) / 255


def over(top, bottom):
    a = top[..., 3:4] + bottom[..., 3:4] * (1 - top[..., 3:4])
    c = (top[..., :3] * top[..., 3:4] + bottom[..., :3] * bottom[..., 3:4] * (1 - top[..., 3:4])) / np.maximum(a, 1e-4)
    return np.dstack([c, a])


def fade(img, k):
    out = img.copy()
    out[..., 3] *= k
    return out


def main():
    rgb = np.asarray(Image.open(SRC).convert("RGB"))
    col, a = key_green(rgb)
    full_src, burst_src = half(col, a, 0), half(col, a, 1)
    bx0, bx1, by0, by1 = body_box(full_src)
    scale = ICON_W / (bx1 - bx0)
    full = place(full_src, scale, ((bx0 + bx1) / 2, (by0 + by1) / 2))
    full[..., :3] *= TONE
    cx0, cx1, cy0, cy1 = body_box(burst_src)
    burst = place(burst_src, scale, ((cx0 + cx1) / 2, (cy0 + cy1) / 2))
    burst[..., :3] *= TONE

    # 빈 칸: 밝기만 남겨 잿빛 자두색으로 누르고, 바깥 빛(옅은 알파)은 거의 지움
    lum = full[..., :3] @ np.array([.3, .55, .15], np.float32)
    empty_rgb = np.stack([lum * .42 + .05, lum * .36 + .04, lum * .46 + .07], -1)
    empty = np.dstack([empty_rgb, np.where(full[..., 3] > .6, full[..., 3], full[..., 3] * .25)])
    empty[..., 3] *= .92

    ys, xs = np.nonzero(full[..., 3] > .5)
    left, right = xs.min(), xs.max()
    xx = np.arange(CELL, dtype=np.float32)[None, :]
    frames = []
    for k in range(FILL_STEPS):
        f = k / (FILL_STEPS - 1)
        if f >= 1:
            frames.append(full)
            continue
        edge = left + (right - left) * f
        m = np.clip((edge - xx) / 4 + .5, 0, 1)[..., None]                       # 4px 부드러운 끝
        frame = np.dstack([full[..., :3] * m + empty[..., :3] * (1 - m), full[..., 3:4] * m + empty[..., 3:4] * (1 - m)])
        if 0 < f:
            line = np.clip(1 - np.abs(xx - edge) / 3, 0, 1)[..., None] * (full[..., 3:4] > .6)   # 차오르는 끝의 밝은 선
            frame[..., :3] = frame[..., :3] * (1 - line * .6) + np.array([.6, .78, .8], np.float32) * line * .6
        frames.append(frame)

    white = full.copy()
    white[..., :3] = white[..., :3] * .7 + .22
    pop = place(white, 1.12, (CELL / 2, CELL / 2))
    frames.append(pop)                                                             # 11 번쩍
    frames.append(over(burst, fade(empty, .0)))                                    # 12 터짐
    frames.append(over(fade(place(burst, 1.08, (CELL / 2, CELL / 2)), .7), fade(empty, .5)))   # 13
    frames.append(over(fade(place(burst, 1.16, (CELL / 2, CELL / 2)), .35), empty))            # 14
    frames.append(empty)                                                           # 15 빈 칸

    sheet = np.concatenate(frames, 1)
    Image.fromarray((sheet * 255).clip(0, 255).astype(np.uint8), "RGBA").save(OUT)
    print("wrote", OUT, sheet.shape[1], "x", sheet.shape[0], len(frames), "frames")

    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    bg = Image.new("RGBA", (sheet.shape[1], CELL), (40, 30, 42, 255))
    bg.alpha_composite(Image.open(OUT))
    bg.resize((sheet.shape[1] // 2, CELL // 2), Image.LANCZOS).save(PREVIEW)


if __name__ == "__main__":
    main()
