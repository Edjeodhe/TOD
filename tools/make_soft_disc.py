"""부드러운 원 그림 (가운데 흰색 → 가장자리 투명): Assets/Art/Generated/soft_disc.png
모이라의 바닥 그림자(SpriteAlpha, 색을 곱함)와 점성구 빛(SpriteAdditive)에 쓴다. 가장자리가 딱 끊기지 않게 smoothstep 으로 줄인다.

python tools/make_soft_disc.py
"""
from pathlib import Path

import numpy as np
from PIL import Image

N = 256
y, x = np.mgrid[0:N, 0:N]
r = np.hypot(x - (N - 1) / 2, y - (N - 1) / 2) / (N / 2)
t = np.clip(1 - r, 0, 1)
a = t * t * (3 - 2 * t)          # smoothstep
a = a ** 1.3                      # 가운데는 진하게, 가장자리는 더 길게 사라지게
img = np.dstack([np.full((N, N), 255, np.uint8)] * 3 + [(a * 255).astype(np.uint8)])
out = Path(__file__).resolve().parents[1] / "Assets/Art/Generated/soft_disc.png"
Image.fromarray(img, "RGBA").save(out)
print("wrote", out)
