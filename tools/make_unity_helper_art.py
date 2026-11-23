"""Unity 판에서 쓰는 보조 텍스처를 만든다 (웹판 render.js 의 Canvas 도형을 그림으로 굽기).

python tools/make_unity_helper_art.py  ->  Assets/Art/Generated/*.png

- white / disc / soft / dash / barb / glint / drop : 스프라이트·라인·입자 공용 도형
- bg_dusk / bg_frame / bg_shade : 전투장 배경 (웹판 createBackground 의 그라데이션·테두리·아치·그늘)
  웹 캔버스(1000x590)보다 좌우 50px 넓은 1100x590 으로 그려 16:9 화면의 좌우 여백을 탑 벽으로 채운다.
"""
from pathlib import Path
import math
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/Art/Generated"
OUT.mkdir(parents=True, exist_ok=True)
W, H, PAD = 1100, 590, 50          # 배경 텍스처 크기, 웹 캔버스 대비 좌우 여백
Y_TOP, Y_SCALE, ARENA_W, ARENA_H = 83, .67, 1000, 700
SS = 4                             # 도형 안티에일리어싱용 슈퍼샘플


def hex_rgba(h):
    h = h.lstrip("#")
    r, g, b = (int(h[i:i + 2], 16) for i in (0, 2, 4))
    a = int(h[6:8], 16) if len(h) == 8 else 255
    return r, g, b, a


def save(img, name):
    img.save(OUT / f"{name}.png")
    print("wrote", name, img.size)


def supersampled(size, draw_fn):
    w, h = size
    big = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(big), SS)
    return big.resize((w, h), Image.LANCZOS)


# ---- 공용 도형 ----
save(Image.new("RGBA", (8, 8), (255, 255, 255, 255)), "white")
save(supersampled((128, 128), lambda d, s: d.ellipse((0, 0, 128 * s - 1, 128 * s - 1), fill=(255, 255, 255, 255))), "disc")

y, x = np.mgrid[0:256, 0:256]
r = np.clip(np.hypot(x - 127.5, y - 127.5) / 128, 0, 1)
soft = np.zeros((256, 256, 4), np.uint8)
soft[..., :3] = 255
soft[..., 3] = (np.clip(1 - r, 0, 1) ** 1.6 * 255).astype(np.uint8)
save(Image.fromarray(soft, "RGBA"), "soft")

dash = Image.new("RGBA", (15, 4), (0, 0, 0, 0))          # setLineDash([8,7])
ImageDraw.Draw(dash).rectangle((0, 0, 7, 3), fill=(255, 255, 255, 255))
save(dash, "dash")

save(supersampled((16, 16), lambda d, s: d.polygon([(1 * s, 15 * s), (15 * s, 15 * s), (3 * s, 1 * s)], fill=(255, 255, 255, 255))), "barb")
glint = Image.new("RGBA", (15, 15), (0, 0, 0, 0))
gd = ImageDraw.Draw(glint)
gd.rectangle((0, 7, 14, 7), fill=(255, 255, 255, 255))
gd.rectangle((7, 0, 7, 14), fill=(255, 255, 255, 255))
save(glint, "glint")
save(supersampled((8, 16), lambda d, s: d.ellipse((0, 0, 8 * s - 1, 16 * s - 1), fill=(255, 255, 255, 255))), "drop")

# ---- 배경: 노을 그라데이션 ----
stops = [(0, "#150e17"), (.13, "#110c15"), (.42, "#23132c22"), (.79, "#170e1b00"), (1, "#140d18")]
col = np.zeros((H, 4), np.float32)
for row in range(H):
    t = row / (H - 1)
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t0 <= t <= t1:
            u = (t - t0) / (t1 - t0)
            a, b = np.array(hex_rgba(c0), np.float32), np.array(hex_rgba(c1), np.float32)
            col[row] = a + (b - a) * u
            break
dusk = np.repeat(col[:, None, :], W, axis=1).round().clip(0, 255).astype(np.uint8)
save(Image.fromarray(dusk, "RGBA"), "bg_dusk")


# ---- 배경: 탑 벽 테두리 + 아치 ----
def frame(d, s):
    def P(x, y):
        return ((x + PAD) * s, y * s)

    def rect(x, y, w, h, color, width):
        d.rectangle((*P(x, y), *P(x + w, y + h)), outline=hex_rgba(color), width=round(width * s))

    rect(17, Y_TOP + 11, ARENA_W - 34, ARENA_H * Y_SCALE - 23, "#ad728750", 1.5)
    rect(24, Y_TOP + 16, ARENA_W - 48, ARENA_H * Y_SCALE - 33, "#60455670", 1.5)
    for cx in range(42, ARENA_W, 115):
        pts = [(cx - 24, 88), (cx - 24, 56)]
        for (ax, ay), (qx, qy), (bx, by) in [((cx - 24, 56), (cx - 24, 34), (cx, 21)), ((cx, 21), (cx + 24, 34), (cx + 24, 56))]:
            for i in range(1, 17):
                t = i / 16
                pts.append(((1 - t) ** 2 * ax + 2 * (1 - t) * t * qx + t * t * bx, (1 - t) ** 2 * ay + 2 * (1 - t) * t * qy + t * t * by))
        pts.append((cx + 24, 88))
        poly = [P(px, py) for px, py in pts]
        d.polygon(poly, fill=hex_rgba("#19101d"))
        d.line(poly + [poly[0]], fill=hex_rgba("#60455670"), width=round(1.5 * s))


save(supersampled((W, H), frame), "bg_frame")

# ---- 배경: 가장자리 그늘 (radial 140 -> 550) ----
y, x = np.mgrid[0:H, 0:W]
dist = np.hypot(x - (500 + PAD), y - 310)
t = np.clip((dist - 140) / (550 - 140), 0, 1)
shade = np.zeros((H, W, 4), np.uint8)
shade[..., :3] = hex_rgba("#100917")[:3]
shade[..., 3] = (t * 0x92).astype(np.uint8)
save(Image.fromarray(shade, "RGBA"), "bg_shade")


# ---- 탑 벽 아치 하나 (큰 맵 위쪽 벽을 따라 반복해서 그린다) ----
def arch(d, s):
    w, h = 50, 70
    pts = [(1, h - 1), (1, 35)]
    for (ax, ay), (qx, qy), (bx, by) in [((1, 35), (1, 13), (25, 1)), ((25, 1), (49, 13), (49, 35))]:
        for i in range(1, 17):
            t = i / 16
            pts.append(((1 - t) ** 2 * ax + 2 * (1 - t) * t * qx + t * t * bx, (1 - t) ** 2 * ay + 2 * (1 - t) * t * qy + t * t * by))
    pts.append((49, h - 1))
    poly = [(x * s, y * s) for x, y in pts]
    d.polygon(poly, fill=hex_rgba("#19101d"))
    d.line(poly + [poly[0]], fill=hex_rgba("#60455690"), width=round(1.5 * s))


save(supersampled((50, 70), arch), "arch")


# ---- 계단 (라운드가 끝나면 나타나는 '위로 올라가기', 임시 그림) ----
def stairs(d, s):
    W, Hh = 220, 170
    steps = 5
    for i in range(steps):
        # 뒤(위) 계단부터 그린다: 단마다 윗면(밝음) + 앞면(어두움)
        k = steps - 1 - i
        x0, x1 = 30 + k * 10, 190 - k * 10
        y = 40 + i * 24
        top = [(x0 * s, y * s), (x1 * s, y * s), ((x1 - 8) * s, (y + 9) * s), ((x0 + 8) * s, (y + 9) * s)]
        front = [((x0 + 8) * s, (y + 9) * s), ((x1 - 8) * s, (y + 9) * s), ((x1 - 8) * s, (y + 24) * s), ((x0 + 8) * s, (y + 24) * s)]
        d.polygon(front, fill=hex_rgba("#2a1d2e"), outline=hex_rgba("#120c16"))
        d.polygon(top, fill=hex_rgba("#5a4660"), outline=hex_rgba("#120c16"))
        d.line([top[0], top[1]], fill=hex_rgba("#e6a8bf"), width=round(2 * s))
    # 계단 위로 이어지는 빛 기둥 자리 (반투명 아치)
    d.polygon([(70 * s, 40 * s), (150 * s, 40 * s), (140 * s, 4 * s), (80 * s, 4 * s)], fill=hex_rgba("#cfe9e255"))


save(supersampled((220, 170), stairs), "stairs")


# ---- 아티팩트 제단 (임시 그림) ----
def altar(d, s):
    d.ellipse((20 * s, 96 * s, 140 * s, 124 * s), fill=hex_rgba("#1b1320"))
    d.polygon([(48 * s, 60 * s), (112 * s, 60 * s), (104 * s, 112 * s), (56 * s, 112 * s)], fill=hex_rgba("#3a2a40"), outline=hex_rgba("#120c16"))
    d.ellipse((30 * s, 46 * s, 130 * s, 74 * s), fill=hex_rgba("#6b5270"), outline=hex_rgba("#120c16"), width=round(2 * s))
    d.ellipse((44 * s, 52 * s, 116 * s, 68 * s), outline=hex_rgba("#cfac78"), width=round(2 * s))


save(supersampled((160, 130), altar), "altar")


# ---- 상인 '단추' (쉬어가는 라운드에 50% 확률로 나온다, 임시 그림) ----
def merchant(d, s):
    def S(*pts):
        return [v * s for v in pts]
    d.ellipse(S(22, 150, 118, 166), fill=hex_rgba("#1b1320"))                          # 바닥 그늘
    d.polygon([(30 * s, 156 * s), (110 * s, 156 * s), (94 * s, 70 * s), (46 * s, 70 * s)],
              fill=hex_rgba("#4a3550"), outline=hex_rgba("#120c16"))                    # 망토
    d.polygon([(58 * s, 70 * s), (82 * s, 70 * s), (78 * s, 156 * s), (62 * s, 156 * s)], fill=hex_rgba("#3a2a40"))
    for y in (92, 112, 132):                                                            # 큰 단추 세 개
        d.ellipse(S(64, y, 76, y + 12), fill=hex_rgba("#cfac78"), outline=hex_rgba("#120c16"))
        d.point([(68 * s, (y + 5) * s), (72 * s, (y + 5) * s)], fill=hex_rgba("#120c16"))
    d.ellipse(S(46, 30, 94, 76), fill=hex_rgba("#e9d8c4"), outline=hex_rgba("#120c16"), width=round(2 * s))   # 얼굴
    d.ellipse(S(58, 46, 68, 56), fill=hex_rgba("#120c16"))                              # 단추 눈
    d.ellipse(S(72, 46, 82, 56), fill=hex_rgba("#120c16"))
    d.polygon([(38 * s, 36 * s), (102 * s, 36 * s), (86 * s, 6 * s), (54 * s, 6 * s)],
              fill=hex_rgba("#5a4660"), outline=hex_rgba("#120c16"))                    # 모자
    d.line([(38 * s, 36 * s), (102 * s, 36 * s)], fill=hex_rgba("#e6a8bf"), width=round(2 * s))
    d.rectangle(S(98, 96, 126, 120), fill=hex_rgba("#6b5270"), outline=hex_rgba("#120c16"))  # 짐 보따리
    d.line([(98 * s, 108 * s), (126 * s, 108 * s)], fill=hex_rgba("#cfac78"), width=round(2 * s))


save(supersampled((140, 170), merchant), "merchant")


# ---- 로비 그늘 (웹판 .welcome-shade: 왼쪽에서 오른쪽으로 옅어지는 막 + 아래쪽 그늘) ----
def ramp(t, stops):
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t0 <= t <= t1:
            u = (t - t0) / (t1 - t0) if t1 > t0 else 0
            a, b = np.array(hex_rgba(c0), np.float32), np.array(hex_rgba(c1), np.float32)
            return a + (b - a) * u
    return np.array(hex_rgba(stops[-1][1]), np.float32)


LW, LH = 960, 540
horizontal = np.array([ramp(x / (LW - 1), [(0, "#170f19ed"), (.34, "#1d1320a8"), (.69, "#1d132000"), (1, "#1d132000")]) for x in range(LW)])
vertical = np.array([ramp(1 - y / (LH - 1), [(0, "#150e19a6"), (.46, "#150e1900"), (1, "#150e1900")]) for y in range(LH)])
h = np.repeat(horizontal[None, :, :], LH, axis=0) / 255
v = np.repeat(vertical[:, None, :], LW, axis=1) / 255
alpha = v[..., 3:] + h[..., 3:] * (1 - v[..., 3:])
rgb = (v[..., :3] * v[..., 3:] + h[..., :3] * h[..., 3:] * (1 - v[..., 3:])) / np.maximum(alpha, 1e-6)
lobby = np.concatenate([rgb, alpha], axis=2)
save(Image.fromarray((lobby * 255).round().clip(0, 255).astype(np.uint8), "RGBA"), "lobby_shade")
