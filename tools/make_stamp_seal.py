# 22차 인벤토리 확장 알림의 붉은 인주 도장 자국 (Codex 한도로 파이썬으로 그림). 
# 출력: Assets/UI/Paper/stamp_seal.png (512, 투명 배경). 실행: python tools/make_stamp_seal.py
import math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 1024
random.seed(7); np.random.seed(7)
m = Image.new("L", (S, S), 0)
d = ImageDraw.Draw(m)
c = S / 2

def wobbly_ring(r, w, n=180, jit=6):
    pts_o, pts_i = [], []
    for i in range(n):
        a = 2 * math.pi * i / n
        j = math.sin(a * 3 + 1.3) * jit + math.sin(a * 7) * jit * .4
        pts_o.append((c + (r + j) * math.cos(a), c + (r + j) * math.sin(a)))
        pts_i.append((c + (r - w + j * .7) * math.cos(a), c + (r - w + j * .7) * math.sin(a)))
    d.polygon(pts_o, fill=255)
    d.polygon(pts_i, fill=0)

wobbly_ring(400, 46)
wobbly_ring(330, 16, jit=4)

# 2x2 칸 (재봉 상자 칸): 굵은 테두리 네모 넷, 오른쪽 아래 칸은 새로 생긴 칸이라 꽉 채움
cell, gap = 150, 26
g0 = c - cell - gap / 2
for iy in range(2):
    for ix in range(2):
        x0, y0 = g0 + ix * (cell + gap), g0 + iy * (cell + gap)
        box = Image.new("L", (cell + 40, cell + 40), 0)
        bd = ImageDraw.Draw(box)
        bd.rectangle([20, 20, 20 + cell, 20 + cell], fill=255)
        if not (ix == 1 and iy == 1):
            bd.rectangle([20 + 24, 20 + 24, cell - 4, cell - 4], fill=0)
        box = box.rotate(random.uniform(-2.5, 2.5), resample=Image.BICUBIC)
        m.paste(255, (int(x0) - 20, int(y0) - 20), box)

# 홈질 땀 고리: 안쪽 고리와 칸 사이를 도는 짧은 땀 (바느질)
n = 30
for i in range(n):
    a0 = 2 * math.pi * i / n + .05
    a1 = a0 + 2 * math.pi / n * .55
    r = 268
    pts = [(c + r * math.cos(a0 + (a1 - a0) * k / 6), c + r * math.sin(a0 + (a1 - a0) * k / 6)) for k in range(7)]
    d.line(pts, fill=255, width=15, joint="curve")

# 인주 결: 눌림이 고르지 않아 군데군데 빠지고 가장자리가 번짐
a = np.array(m, dtype=np.float32) / 255
lo = np.random.rand(S // 32, S // 32).astype(np.float32)
lo = np.array(Image.fromarray((lo * 255).astype(np.uint8)).resize((S, S), Image.BICUBIC), dtype=np.float32) / 255
hi = np.random.rand(S, S).astype(np.float32)
hi = np.array(Image.fromarray((hi * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.1)), dtype=np.float32) / 255
press = np.clip(0.6 + 0.6 * (lo - 0.5), 0, 1)           # 넓은 눌림 차이
grain = (hi > 0.5 - 0.42 * press).astype(np.float32)       # 종이 결 사이로 빠진 점
alpha = a * grain
soft = np.array(Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.4)), dtype=np.float32) / 255
alpha = np.clip(soft * (0.8 + 0.3 * press), 0, 1)
# 가장자리 작은 인주 방울
dots = Image.new("L", (S, S), 0); dd = ImageDraw.Draw(dots)
for _ in range(26):
    ang = random.uniform(0, 2 * math.pi); r = random.uniform(410, 470); s = random.uniform(3, 11)
    x, y = c + r * math.cos(ang), c + r * math.sin(ang)
    dd.ellipse([x - s, y - s, x + s, y + s], fill=random.randint(140, 230))
alpha = np.maximum(alpha, np.array(dots.filter(ImageFilter.GaussianBlur(1)), dtype=np.float32) / 255)

# 색: 주홍 ~ 진홍, 진한 곳이 더 어둡게
col_a = np.array([214, 64, 48], np.float32); col_b = np.array([150, 22, 32], np.float32)
mix = np.clip(lo * .7 + .15, 0, 1)[..., None]
rgb = col_a * (1 - mix) + col_b * mix
out = np.dstack([rgb, alpha * 255]).astype(np.uint8)
img = Image.fromarray(out, "RGBA").rotate(-8, resample=Image.BICUBIC).resize((512, 512), Image.LANCZOS)
img.save("Assets/UI/Paper/stamp_seal.png")
print("saved", img.size)
