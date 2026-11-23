"""재봉 상자 퍼즐 칸 그림 굽기 → Assets/UI/Puzzle/*.png

python tools/bake_puzzle_tiles.py

조각 한 칸 = 종이(paper_N) + 잉크 테두리(edge_N). N = 바깥 변 비트(위 1 · 오른쪽 2 · 아래 4 · 왼쪽 8).
바깥 변만 찢긴 가장자리와 잉크 선이 있고, 안쪽 변은 칸 끝까지 이어져 이웃 칸과 한 장처럼 붙는다.
가장자리 흔들림은 변마다 주기 함수라 이웃 칸끼리 이음매가 맞고, 종이 결도 주기 잡음이라 칸끼리 이어진다.
색은 전부 밝은 회색/흰색으로 굽고 USS·코드의 tint 로 등급 색을 입힌다.
mini_N = 종이 + 잉크를 합친 것(목록·정보 창의 작은 모양용).
덤: stitch_h/v(바늘땀 한 땀, 반복), ornament(유니크 금실 십자수), sparkle(레전더리 반짝임), shine(빛 줄기), needle(장착 바늘),
slot_empty(빈 판 칸: 움푹 들어간 슬롯) · slot_frame(조각 칸마다 얹는 슬롯 테두리) · slot_corners(슬롯 모서리 땀, 등급 실 색으로 물들임).
"""
from pathlib import Path
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/UI/Puzzle"
S = 96            # 칸 텍스처 크기
MARGIN = 7.0      # 바깥 변이 칸 끝에서 들어오는 기본 깊이(px) → 조각끼리 사이가 살짝 뜬다
INK = 4.0         # 잉크 선 굵기(px)
rng = np.random.default_rng(7)


def periodic_noise(size, blur):
    """주기 경계 잡음 (FFT 저역 통과) → 칸끼리 이어 붙여도 이음매가 없다."""
    n = rng.standard_normal((size, size))
    fy = np.fft.fftfreq(size)[:, None]
    fx = np.fft.fftfreq(size)[None, :]
    g = np.exp(-(fx ** 2 + fy ** 2) * (blur ** 2))
    out = np.real(np.fft.ifft2(np.fft.fft2(n) * g))
    return (out - out.mean()) / (out.std() + 1e-6)


def grain():
    coarse = periodic_noise(S, 6.0)
    fine = periodic_noise(S, 1.2)
    fiber = periodic_noise(S, 3.0)
    fiber = np.roll(fiber, 0, 0)
    v = .9 + coarse * .035 + fine * .025
    # 가로로 흐르는 섬유 결
    streak = np.repeat(periodic_noise(S, 4.0)[:, :1], S, 1) * .006
    return np.clip(v + streak + fiber * .01, .78, 1.0)


def edge_wobble(side):
    """변 하나의 가장자리 깊이 함수 f(t), t∈[0,1], f(0)=f(1) (정수 주파수 사인 합)."""
    phase = rng.uniform(0, 2 * math.pi, 4)
    amp = [1.6, 1.1, .7, .45]
    freq = [2, 3, 5, 9]

    def f(t):
        return sum(a * np.sin(2 * math.pi * k * t + p) for a, k, p in zip(amp, freq, phase)) - \
               sum(a * math.sin(p) for a, p in zip(amp, phase))  # 끝점 0 → 모서리에서 이웃 변과 맞음
    return f


WOBBLE = [edge_wobble(d) for d in range(4)]


def depth_maps():
    """각 픽셀의 변별 '안쪽 거리 - 가장자리 깊이' (양수면 종이 안)."""
    y, x = np.mgrid[0:S, 0:S].astype(np.float32) + .5
    t_h = x / S
    t_v = y / S
    return [
        y - (MARGIN + WOBBLE[0](t_h)),            # 위
        (S - x) - (MARGIN + WOBBLE[1](t_v)),      # 오른쪽
        (S - y) - (MARGIN + WOBBLE[2](t_h)),      # 아래
        x - (MARGIN + WOBBLE[3](t_v)),            # 왼쪽
    ]


def soft(v, width=1.0):
    return np.clip(v / width + .5, 0, 1)


CORNERS = [(0, 3), (0, 1), (2, 1), (2, 3)]   # 왼위 · 오른위 · 오른아래 · 왼아래 (두 변)


def bake_mask(bits, concave, base, depths):
    inside = np.full((S, S), 1e3, np.float32)
    for d in range(4):
        if bits & (1 << d):
            inside = np.minimum(inside, depths[d])
    # 오목 모서리: 두 변은 이웃이 있는데 대각선 칸이 비면 그 모서리를 두 변의 선으로 잘라 낸다
    for i, (a, b) in enumerate(CORNERS):
        if concave & (1 << i):
            inside = np.minimum(inside, np.maximum(depths[a], depths[b]))
    # 볼록 모서리: 두 바깥 변이 만나는 곳을 둥글게
    y, x = np.mgrid[0:S, 0:S].astype(np.float32) + .5
    r = 9.0
    for a, b, cx, cy in [(0, 3, 0, 0), (0, 1, S, 0), (2, 1, S, S), (2, 3, 0, S)]:
        if bits & (1 << a) and bits & (1 << b):
            ox = cx + (MARGIN + r if cx == 0 else -(MARGIN + r))
            oy = cy + (MARGIN + r if cy == 0 else -(MARGIN + r))
            in_corner = ((x - ox) * (1 if cx == 0 else -1) < 0) & ((y - oy) * (1 if cy == 0 else -1) < 0)
            dist = r - np.hypot(x - ox, y - oy)
            inside = np.where(in_corner, np.minimum(inside, dist), inside)
    alpha = soft(inside, 1.2)
    ink = soft(inside, 1.0) * (1 - soft(inside - INK, 1.4))
    # 잉크는 군데군데 옅어지는 거친 붓 선
    rough = np.clip(.92 + periodic_noise(S, 1.5) * .12, .7, 1)
    ink = ink * rough
    paper = np.stack([base, base, base * .985], -1)
    # 가장자리 쪽은 살짝 그을린 종이
    burn = 1 - .12 * (1 - soft(inside - 10, 8))
    paper = paper * burn[..., None]
    p_img = np.dstack([paper * 255, alpha * 255]).clip(0, 255).astype(np.uint8)
    e_img = np.dstack([np.full((S, S, 3), 255.0), ink * alpha * 255]).clip(0, 255).astype(np.uint8)
    # mini: 종이 위에 검은 잉크를 합성
    col = paper * (1 - ink[..., None]) + np.array([.1, .063, .075]) * ink[..., None]
    m_img = np.dstack([col * 255, alpha * 255]).clip(0, 255).astype(np.uint8)
    return Image.fromarray(p_img, "RGBA"), Image.fromarray(e_img, "RGBA"), Image.fromarray(m_img, "RGBA")


def stitch():
    w, h = 24, 8
    im = Image.new("RGBA", (w * 4, h * 4), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((4 * 4, 1 * 4, 18 * 4, 7 * 4), radius=12, fill=(40, 24, 28, 200))   # 그림자
    d.rounded_rectangle((3 * 4, 0, 17 * 4, 5.5 * 4), radius=10, fill=(255, 255, 255, 255))
    d.line((5 * 4, 1.5 * 4, 15 * 4, 1.5 * 4), fill=(255, 255, 255, 255), width=3)
    im = im.resize((w, h), Image.LANCZOS)
    return im, im.rotate(-90, expand=True)


def star(size, points=4, inner=.28):
    im = Image.new("RGBA", (size * 4, size * 4), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = size * 2
    pts = []
    for i in range(points * 2):
        a = math.pi * i / points - math.pi / 2
        r = c * (1 if i % 2 == 0 else inner)
        pts.append((c + r * math.cos(a), c + r * math.sin(a)))
    d.polygon(pts, fill=(255, 255, 255, 255))
    im = im.filter(ImageFilter.GaussianBlur(2))
    return im.resize((size, size), Image.LANCZOS)


def ornament():
    """유니크: 금실 십자수 두 땀 + 작은 마름모."""
    n = 40
    im = Image.new("RGBA", (n * 4, n * 4), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    k = 4
    for ox, oy in [(6, 22), (18, 22)]:
        d.line(((ox) * k, (oy) * k, (ox + 10) * k, (oy + 10) * k), fill=(255, 255, 255, 255), width=10)
        d.line(((ox + 10) * k, (oy) * k, (ox) * k, (oy + 10) * k), fill=(255, 255, 255, 255), width=10)
    d.polygon([(30 * k, 4 * k), (36 * k, 10 * k), (30 * k, 16 * k), (24 * k, 10 * k)], fill=(255, 255, 255, 255))
    return im.resize((n, n), Image.LANCZOS)


def shine():
    w, h = 48, 96
    a = np.zeros((h, w), np.float32)
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u = (x - w / 2) + (y - h / 2) * -.45
    a = np.exp(-(u / 9) ** 2) * .85 + np.exp(-((u - 14) / 3) ** 2) * .5
    img = np.dstack([np.full((h, w, 3), 255.0), a.clip(0, 1) * 255]).astype(np.uint8)
    return Image.fromarray(img, "RGBA")


def needle():
    w, h = 56, 14
    k = 4
    im = Image.new("RGBA", (w * k, h * k), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # 몸통(왼쪽 귀 → 오른쪽 끝이 바늘 끝)
    d.polygon([(2 * k, 4.5 * k), (40 * k, 5 * k), (55 * k, 7 * k), (40 * k, 9 * k), (2 * k, 9.5 * k)], fill=(26, 16, 19, 255))
    d.polygon([(3 * k, 5.5 * k), (40 * k, 6 * k), (52 * k, 7 * k), (40 * k, 8 * k), (3 * k, 8.5 * k)], fill=(214, 210, 204, 255))
    d.line((6 * k, 6.4 * k, 38 * k, 6.6 * k), fill=(255, 255, 255, 255), width=k)
    d.ellipse((5 * k, 6 * k, 12 * k, 8 * k), fill=(26, 16, 19, 255))   # 바늘귀
    im = im.resize((w, h), Image.LANCZOS)
    return im


def valid_keys():
    """오목 모서리는 그 모서리의 두 변이 모두 안쪽(이웃 있음)일 때만 생긴다."""
    keys = []
    for bits in range(16):
        free = [i for i, (a, b) in enumerate(CORNERS) if not bits & (1 << a) and not bits & (1 << b)]
        for m in range(1 << len(free)):
            concave = sum(1 << free[j] for j in range(len(free)) if m & (1 << j))
            keys.append(bits + 16 * concave)
    return keys


SLOT_A, SLOT_B, SLOT_R = 21.0, 75.0, 8.0   # 조각 칸 슬롯 테두리(96 기준): 바늘땀 안쪽에 들어가는 네모


def box_inside(a, b, r, wobble=.8, seed_blur=3.0):
    """둥근 네모의 안쪽 거리(px, 안이 양수) + 손그림 흔들림."""
    y, x = np.mgrid[0:S, 0:S].astype(np.float32) + .5
    c = (a + b) / 2
    h = (b - a) / 2 - r
    qx, qy = np.abs(x - c) - h, np.abs(y - c) - h
    out = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r
    return -out + periodic_noise(S, seed_blur) * wobble, x, y


def compose(layers):
    rgb = np.zeros((S, S, 3), np.float32)
    a = np.zeros((S, S), np.float32)
    for col, al in layers:
        al = np.clip(al, 0, 1)
        rgb = (rgb * a[..., None] * (1 - al[..., None]) + np.array(col, np.float32) * al[..., None]) / np.maximum(a + al * (1 - a), 1e-4)[..., None]
        a = a + al * (1 - a)
    return Image.fromarray(np.dstack([rgb, a * 255]).clip(0, 255).astype(np.uint8), "RGBA")


INK_RGB, LIGHT_RGB = (26, 16, 19), (255, 247, 228)


def slot_frame(base):
    """조각 칸마다 얹는 슬롯: 종이에 움푹 눌린 네모 홈 + 위·왼쪽 그늘, 아래·오른쪽 밝은 턱 + 거친 잉크 테두리. 가운데는 종이가 비친다."""
    inside, x, y = box_inside(SLOT_A, SLOT_B, SLOT_R)
    a, b = SLOT_A, SLOT_B
    inn = soft(inside, 1.0)
    recess = inn * (.12 + .2 * np.exp(-np.maximum(inside, 0) / 7))
    tl = inn * np.clip(np.exp(-(y - a - 1) / 4.5) + np.exp(-(x - a - 1) / 4.5), 0, 1) * .34
    br = inn * np.clip(np.exp(-(b - y - 1) / 2.2) + np.exp(-(b - x - 1) / 2.2), 0, 1) * .3
    dry = np.clip(.88 + periodic_noise(S, 1.4) * .14, .6, 1)
    ink = soft(1.6 - np.abs(inside), 1.0) * dry * .92
    emboss = soft(-inside - 1.6, 1.0) * (1 - soft(-inside - 4.5, 1.5)) * .2
    return compose([(INK_RGB, recess), (INK_RGB, tl), (LIGHT_RGB, br), (LIGHT_RGB, emboss), (INK_RGB, ink)])


def slot_corners():
    """슬롯 네 모서리의 작은 십자 땀 (흰 실 → 등급 실 색으로 물들임)."""
    k = 4
    im = Image.new("RGBA", (S * k, S * k), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    a, b = SLOT_A + 1, SLOT_B - 1
    for cx, cy in ((a, a), (b, a), (b, b), (a, b)):
        for sx, sy in ((1, 1), (1, -1)):
            p0, p1 = (cx - 4 * sx, cy - 4 * sy), (cx + 4 * sx, cy + 4 * sy)
            d.line([((p0[0] + .7) * k, (p0[1] + 1) * k), ((p1[0] + .7) * k, (p1[1] + 1) * k)], fill=(30, 18, 22, 160), width=int(3.2 * k))
            d.line([(p0[0] * k, p0[1] * k), (p1[0] * k, p1[1] * k)], fill=(255, 255, 255, 255), width=int(2.6 * k))
    return im.resize((S, S), Image.LANCZOS)


def slot_empty(base):
    """빈 판 칸: 칸을 꽉 채운 움푹한 슬롯 (밝은 회색 종이 결로 구워 USS tint 로 어둡게 물들임)."""
    inside, x, y = box_inside(4.0, 92.0, 10.0, wobble=1.0)
    a, b = 4.0, 92.0
    inn = soft(inside, 1.0)
    paper = inn * .96
    tl = inn * np.clip(np.exp(-(y - a - 2) / 7) + np.exp(-(x - a - 2) / 7), 0, 1) * .5
    br = inn * np.clip(np.exp(-(b - y - 2) / 3) + np.exp(-(b - x - 2) / 3), 0, 1) * .35
    vign = inn * (1 - soft(inside - 18, 14)) * .18
    dry = np.clip(.9 + periodic_noise(S, 1.4) * .12, .65, 1)
    ink = soft(2.0 - np.abs(inside - .5), 1.0) * dry
    img = compose([((235, 232, 236), paper), (INK_RGB, vign), (INK_RGB, tl), (LIGHT_RGB, br), (INK_RGB, ink)])
    arr = np.asarray(img).astype(np.float32)
    # 종이 결을 곱해 넣는다
    arr[..., :3] *= np.clip(base[..., None] * 1.06, 0, 1)
    return Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGBA")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    base = grain()
    depths = depth_maps()
    for old in OUT.glob("*_*.png"):
        if old.stem.split("_")[0] in ("paper", "edge", "mini"):
            old.unlink()
    rules = ["/* tools/bake_puzzle_tiles.py 가 만든다. k{n}: n = 바깥 변 비트(위1·오른2·아래4·왼8) + 16 × 오목 모서리 비트(왼위1·오른위2·오른아래4·왼아래8) */"]
    for key in valid_keys():
        bits, concave = key & 15, key >> 4
        p, e, m = bake_mask(bits, concave, base, depths)
        p.save(OUT / f"paper_{key}.png")
        e.save(OUT / f"edge_{key}.png")
        m.save(OUT / f"mini_{key}.png")
        for cls, name in (("tile-paper", "paper"), ("tile-edge", "edge"), ("piece-dot", "mini")):
            rules.append(f'.{cls}.k{key} {{ background-image: url("project://database/Assets/UI/Puzzle/{name}_{key}.png"); }}')
    (ROOT / "Assets/UI/Puzzle.uss").write_text(chr(10).join(rules) + chr(10), encoding="utf-8")
    h, v = stitch()
    h.save(OUT / "stitch_h.png")
    v.save(OUT / "stitch_v.png")
    ornament().save(OUT / "ornament.png")
    star(32).save(OUT / "sparkle.png")
    shine().save(OUT / "shine.png")
    needle().save(OUT / "needle.png")
    for old in ("div_h", "div_v", "divstitch_h", "divstitch_v"):   # 앞서 쓴 칸 나눔(접힌 자국)은 슬롯으로 바뀜
        for f in (OUT / f"{old}.png", OUT / f"{old}.png.meta"):
            if f.exists():
                f.unlink()
    slot_frame(base).save(OUT / "slot_frame.png")
    slot_corners().save(OUT / "slot_corners.png")
    slot_empty(base).save(OUT / "slot_empty.png")
    print("baked", OUT)


if __name__ == "__main__":
    main()
