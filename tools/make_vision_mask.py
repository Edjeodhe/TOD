# 16차: 어둠 보스의 시야 좁힘 마스크. 가운데는 투명, 바깥으로 갈수록 불투명한 흰 원 (색은 코드가 곱한다).
# python tools/make_vision_mask.py → Assets/sprites/fx_vision_mask.png (가져오기는 TOD/Setup/1)
from PIL import Image
import math
N = 512
img = Image.new("RGBA", (N, N), (255, 255, 255, 255))
px = img.load()
for y in range(N):
    for x in range(N):
        r = math.hypot(x + .5 - N / 2, y + .5 - N / 2) / (N / 2)
        t = min(1.0, max(0.0, (r - .55) / .45))
        t = t * t * (3 - 2 * t)
        px[x, y] = (255, 255, 255, int(round(255 * t)))
img.save("Assets/sprites/fx_vision_mask.png")
print("ok")
