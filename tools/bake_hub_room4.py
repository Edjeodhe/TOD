"""24차 넓은 재봉실 배경: 가운데 한 장 + 양옆 이어 그리기 두 장 → Assets/sprites/hub_room4.png (3072x1024).

  python tools/bake_hub_room4.py pads      # 이어 그리기 입력 그림 (art_src/raw/hub_room4_{left,right}_in.png)
  python tools/bake_hub_room4.py           # 합치기 → Assets/sprites/hub_room4.png + Temp/Shots/hub_room4_preview.jpg

- 가운데 C = art_src/raw/hub_room4_center.png (1536x1024, 옆 비네트 없음). 캔버스 500~1500 px 자리.
- 왼쪽 L = art_src/raw/hub_room4_left.png: 오른쪽 반이 C 의 왼쪽 반을 다시 그린 것, 왼쪽 반이 새로 그린 수녀 감실 날개 (캔버스 0~500).
- 오른쪽 R = art_src/raw/hub_room4_right.png: 왼쪽 반이 C 의 오른쪽 반, 오른쪽 반이 촛불 유령 제단 날개 (캔버스 1500~2000).
이어 붙이는 곳: 겹치는 반쪽을 위상 상관으로 맞춰(가로·세로 이동) 옮기고, 겹친 곳의 색 평균·편차를 C 에 맞춘 뒤,
C 가장자리 안쪽 SEAM px 구간에서 부드럽게 섞는다. 마지막에 방 양 끝만 어둡게 (비네트).
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw"
OUT = ROOT / "Assets/sprites/hub_room4.png"
SHOTS = ROOT / "Temp/Shots"
W, H, HALF = 1536, 1024, 768
SEAM = (12, 120)        # C 가장자리에서 이만큼 안쪽 구간에서 섞음 (px)
END_DARK = (.07, .62)   # 양 끝 비네트: 너비 비율, 가장 끝에서 남는 밝기


def load(name):
    return np.asarray(Image.open(RAW / name).convert("RGB").resize((W, H), Image.LANCZOS)).astype(np.float32)


def pads():
    c = load("hub_room4_center.png")
    grey = np.full((H, HALF, 3), 128, np.float32)
    left = np.concatenate([grey, c[:, :HALF]], 1)
    right = np.concatenate([c[:, HALF:], grey], 1)
    for name, im in (("hub_room4_left_in.png", left), ("hub_room4_right_in.png", right)):
        Image.fromarray(im.astype(np.uint8)).save(RAW / name)
        print("wrote", RAW / name)


def shift_of(a, b):
    """b 를 얼마나 옮기면 a 에 맞는지 (dy, dx). 밝기 위상 상관."""
    ga, gb = a.mean(2), b.mean(2)
    ga, gb = ga - ga.mean(), gb - gb.mean()
    win = np.outer(np.hanning(ga.shape[0]), np.hanning(ga.shape[1]))
    fa, fb = np.fft.fft2(ga * win), np.fft.fft2(gb * win)
    r = fa * np.conj(fb)
    r /= np.abs(r) + 1e-6
    c = np.fft.ifft2(r).real
    dy, dx = np.unravel_index(np.argmax(c), c.shape)
    if dy > c.shape[0] // 2: dy -= c.shape[0]
    if dx > c.shape[1] // 2: dx -= c.shape[1]
    return int(dy), int(dx)


def move(im, dy, dx):
    out = np.roll(im, (dy, dx), (0, 1))
    if dy > 0: out[:dy] = out[dy:dy + 1]
    if dy < 0: out[dy:] = out[dy - 1:dy]
    return out


def match(im, ref_part, part):
    """part(im 의 겹친 곳)의 채널별 평균·편차를 ref_part 에 맞추는 변환을 im 전체에."""
    m, s = part.reshape(-1, 3).mean(0), part.reshape(-1, 3).std(0) + 1e-3
    rm, rs = ref_part.reshape(-1, 3).mean(0), ref_part.reshape(-1, 3).std(0)
    return (im - m) / s * rs + rm


def compose():
    c, l, r = load("hub_room4_center.png"), load("hub_room4_left.png"), load("hub_room4_right.png")
    dl = shift_of(c[:, :HALF], l[:, HALF:])
    dr = shift_of(c[:, HALF:], r[:, :HALF])
    print("shift left", dl, "right", dr)
    l, r = move(l, *dl), move(r, *dr)
    l = match(l, c[:, :HALF], l[:, HALF:])
    r = match(r, c[:, HALF:], r[:, :HALF])
    # 놓기 (그림 px): L 0..1536, R 1536..3072, 그 위에 C 768..2304 를 가장자리만 섞어서
    full = np.zeros((H, W * 2, 3), np.float32)
    full[:, 0:W] = l
    full[:, W:2 * W] = r
    cpos = HALF
    a, b = SEAM
    ramp = np.clip((np.arange(W) - a) / (b - a), 0, 1)
    wc = np.minimum(ramp, ramp[::-1])
    region = full[:, cpos:cpos + W]
    full[:, cpos:cpos + W] = region * (1 - wc[None, :, None]) + c * wc[None, :, None]
    # 양 끝 비네트
    x = np.arange(W * 2) / (W * 2)
    e, k = END_DARK
    edge = np.clip(np.minimum(x, 1 - x) / e, 0, 1)
    shade = k + (1 - k) * (edge ** 1.4)
    full *= shade[None, :, None]
    full = full.clip(0, 255).astype(np.uint8)
    Image.fromarray(full).save(OUT)
    SHOTS.mkdir(parents=True, exist_ok=True)
    prev = Image.fromarray(full).resize((W, H // 2), Image.LANCZOS)
    prev.save(SHOTS / "hub_room4_preview.jpg", quality=90)
    print("wrote", OUT, full.shape)


if __name__ == "__main__":
    pads() if sys.argv[1:] == ["pads"] else compose()
