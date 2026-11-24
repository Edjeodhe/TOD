"""22차 칠죄종 결박 리본 그림 함수. render_bind_ribbons.py 가 exec 로 읽어 들인다 (Layer, line, flame, tiled, wisp, taper 등을 그대로 쓴다). 단독 실행 안 함.

규칙 (HANDOFF 작업 규칙): 실에 붙는 이펙트는 맨 실을 완전히 덮는다. 몸통 판(y ∈ [-.375, .375])의 가운데 띠를 넓은 빛(rim)이 늘 덮고,
그 위에 죄종 무늬를 얹는다. 심(core)은 가늘게 — 다섯 가닥이 겹쳐도 하얗게 날아가지 않게 core 알파는 .75 안팎으로 낮춘다.
  분노 wrath    날 선 불길 가시 + 쩍 갈라진 균열 무늬 + 불씨
  나태 sloth    느리게 굴러가는 졸음 구름 덩이 + 떠오르는 Z
  교만 pride    꼬인 두 가닥 + 사방으로 뻗는 짧은 햇살 + 네 갈래 별
  탐욕 greed    금실 두 가닥 + 빙글 도는 금화 꿰미
  식탐 gluttony 꿀꺽 삼키듯 부푼 덩이가 흐르는 굵은 줄 + 양옆 이빨 + 침방울
  색욕 lust     땋은 분홍 실 + 흘러가는 하트 + 꽃잎
  시기 envy     가시 덩굴 + 잎 + 독방울
"""

SIN_STYLES = ("wrath", "sloth", "pride", "greed", "gluttony", "lust", "envy")


def heart_pts(x, y, s, rot=0.0, n=28):
    out = []
    for i in range(n):
        a = TAU * i / n
        hx = 16 * math.sin(a) ** 3 / 17
        hy = (13 * math.cos(a) - 5 * math.cos(2 * a) - 2 * math.cos(3 * a) - math.cos(4 * a)) / 17
        c, sn = math.cos(rot), math.sin(rot)
        out.append((x + (hx * c - hy * sn) * s, y + (hx * sn + hy * c) * s))
    return out


def star_pts(x, y, r_out, r_in, points=4, rot=0.0):
    out = []
    for i in range(points * 2):
        a = rot + math.pi * i / points
        r = r_out if i % 2 == 0 else r_in
        out.append((x + math.cos(a) * r, y + math.sin(a) * r))
    return out


def ellipse(x, y, rx, ry, n=20, rot=0.0):
    c, s = math.cos(rot), math.sin(rot)
    return [(x + math.cos(a) * rx * c - math.sin(a) * ry * s, y + math.cos(a) * rx * s + math.sin(a) * ry * c)
            for a in (TAU * i / n for i in range(n))]


def zee(layer, x, y, s, width, alpha):
    pts = [(x - s, y + s), (x + s, y + s), (x - s, y - s), (x + s, y - s)]
    dense = []
    for a, b in zip(pts, pts[1:]):
        for j in range(6):
            u = j / 6
            dense.append((a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u))
    dense.append(pts[-1])
    layer.ribbon(dense, [width] * len(dense), [alpha] * len(dense), edge=.35)


def cover(core, rim, pts, wide=.2, body=.085, heart=.022, wide_a=.5, body_a=.95, core_a=.75, widths=None):
    """맨 실을 덮는 바탕: 넓은 빛 + 몸통 + 가는 심. widths 가 있으면 점마다 배율."""
    k = widths or [1] * len(pts)
    rim.ribbon(pts, [wide * w for w in k], [wide_a] * len(pts), edge=0)
    rim.ribbon(pts, [body * w for w in k], [body_a] * len(pts), edge=.3)
    core.ribbon(pts, [heart * w for w in k], [core_a] * len(pts), edge=.25)


# ---------------------------------------------------------------- 실 몸통

def thread_wrath(t, core, rim, rng):
    wave = lambda x: .03 * math.sin(TAU * (x / 2 + t))
    pts = line(-2.6, 2.6, 80, wave)
    cover(core, rim, pts, wide=.22, body=.09)
    # 날 선 불길 가시: 곡선 혀 대신 뾰족한 칼날이 위아래로 치솟았다 잦아든다
    for i in range(18):
        x0 = -2 + 4 * (i + .5) / 18 + (rng.random() - .5) * .08
        side = 1 if i % 2 == 0 else -1
        ph = rng.random()
        grow = .5 + .5 * math.sin(TAU * (t + ph))
        h = (.16 + .17 * grow) * side
        lean = (rng.random() - .5) * .16

        def spike(x):
            flame(rim, x, wave(x), h * 1.1, lean * 1.5 + .04, .2, .35)
            rim.blade(x - .04, wave(x), x + lean, wave(x) + h, .085, .95, edge=.25, bias=.15)
            core.blade(x - .02, wave(x), x + lean * .5, wave(x) + h * .55, .025, .8, edge=.3, bias=.15)
        tiled(x0, spike)
    # 갈라진 균열: 몸통을 가로지르는 지그재그 금 (심 색으로 달아오름)
    for i in range(6):
        x0 = -2 + 4 * (i + .5) / 6
        crack_rng = random.Random(i * 13 + 5)

        def crack(x):
            pts2 = bolt_points(crack_rng, x - .12, -.06, x + .12, .06, 5, .05)
            core.ribbon(pts2, [.012] * len(pts2), [.7 * (.6 + .4 * math.sin(TAU * (t + i / 6)))] * len(pts2), edge=.4)
        tiled(x0, crack)
    for i in range(12):
        x0 = (-2 + 4 * rng.random() + 4 * t + 2) % 4 - 2
        y = .08 + rng.random() * .26
        y *= 1 if i % 2 else -1
        tiled(x0, lambda x: (rim.disc(x, y, .03 + .015 * rng.random(), .95), core.disc(x, y, .012, .7)))


def thread_sloth(t, core, rim, rng):
    # 느릿한 구름 덩이가 줄을 따라 굴러간다 (한 바퀴에 타일 1/4 만)
    pts = line(-2.6, 2.6, 60, lambda x: .02 * math.sin(TAU * (x / 4 + t)))
    cover(core, rim, pts, wide=.26, body=.1, heart=.014, wide_a=.5, core_a=.4)
    for i in range(11):
        x0 = (-2 + 4 * (i + .5) / 11 + t * 4 / 11 * 1 + 2) % 4 - 2
        r = .1 + .07 * rng.random()
        dy = (rng.random() - .5) * .12
        breathe = 1 + .1 * math.sin(TAU * (t + rng.random()))

        def puff(x):
            for j, (ox, oy, rr) in enumerate(((-.7, -.1, .75), (0, .15, 1), (.7, -.05, .8), (.25, -.2, .6))):
                rim.disc(x + ox * r, dy + oy * r, rr * r * breathe * 1.35, .55)
                rim.disc(x + ox * r, dy + oy * r, rr * r * breathe, .9)
            core.disc(x - .2 * r, dy + .3 * r, r * .35, .3)
        tiled(x0, puff)
    # Z: 위로 느리게 떠오르며 옅어진다
    for i in range(5):
        x0 = -2 + 4 * (i + .3 + .4 * rng.random()) / 5
        ph = rng.random()
        k = (t + ph) % 1
        s = .045 + .035 * k
        a = math.sin(math.pi * k)
        y = .12 + .2 * k

        def zz(x):
            zee(rim, x + .06 * k, y, s, .022, .95 * a)
            zee(core, x + .06 * k, y, s, .007, .7 * a)
        tiled(x0, zz)


def thread_pride(t, core, rim, rng):
    pts = line(-2.6, 2.6, 80, lambda x: 0)
    cover(core, rim, pts, wide=.22, body=.07, heart=.016)
    for phase in (0, .5):
        f = lambda x, p=phase: .07 * math.sin(TAU * (x / 2 + t + p))
        sp = line(-2.6, 2.6, 80, f)
        rim.ribbon(sp, [.035] * len(sp), [.95] * len(sp), edge=.3)
        core.ribbon(sp, [.01] * len(sp), [.7] * len(sp), edge=.3)
    # 짧은 햇살: 줄에서 사방으로 뻗었다 줄어든다
    for i in range(16):
        x0 = -2 + 4 * (i + .5) / 16
        ph = rng.random()
        k = .5 + .5 * math.sin(TAU * (t + ph))
        side = 1 if i % 2 == 0 else -1
        ang = side * (math.pi / 2 + (rng.random() - .5) * .9)
        ln = (.14 + .16 * k)

        def ray(x):
            rim.blade(x, 0, x + math.cos(ang) * ln, math.sin(ang) * ln, .045, .85, edge=.2, bias=.1)
            core.blade(x, 0, x + math.cos(ang) * ln * .6, math.sin(ang) * ln * .6, .012, .65, edge=.3, bias=.1)
        tiled(x0, ray)
    # 네 갈래 별: 줄을 따라 흐르며 반짝인다
    for i in range(7):
        x0 = (-2 + 4 * rng.random() + 4 * t + 2) % 4 - 2
        y = (rng.random() - .5) * .4
        tw = .45 + .55 * max(0.0, math.sin(TAU * (t * 2 + rng.random())))
        s = .06 + .05 * rng.random()

        def star(x):
            rim.poly(star_pts(x, y, s * 1.5 * tw, s * .3, 4, math.pi / 4), .55, edge=.2)
            rim.poly(star_pts(x, y, s * tw, s * .22, 4), .95, edge=.6)
            core.poly(star_pts(x, y, s * .55 * tw, s * .12, 4), .7, edge=.6)
        tiled(x0, star)


def thread_greed(t, core, rim, rng):
    pts = line(-2.6, 2.6, 80, lambda x: 0)
    cover(core, rim, pts, wide=.2, body=.05, heart=.012, wide_a=.45, core_a=.55)
    for phase in (0, .5):
        f = lambda x, p=phase: .06 * math.sin(TAU * (x / 2 + t + p))
        sp = line(-2.6, 2.6, 80, f)
        rim.ribbon(sp, [.03] * len(sp), [.95] * len(sp), edge=.3)
        core.ribbon(sp, [.009] * len(sp), [.6] * len(sp), edge=.3)
    # 금화 꿰미: 줄에 꿰인 금화가 빙글 돌며 흐른다 (가로 폭 = |cos 회전|)
    n = 8
    for i in range(n):
        x0 = (-2 + 4 * (i + .5) / n + 4 * t / n + 2) % 4 - 2
        spin = TAU * (t * 2 + i * .37)
        r = .14 + .02 * (i % 2)
        rx = r * (.18 + .82 * abs(math.cos(spin)))
        dy = .03 * math.sin(TAU * (t + i / n))

        def coin(x):
            rim.disc(x, dy, r * 1.6, .35)
            rim.poly(ellipse(x, dy, rx, r), .98, edge=.85)
            rim.poly(ellipse(x, dy, rx * .7, r * .7), .7, lum=.45, edge=1)       # 테두리 안 오목한 면 (어둡게)
            core.poly(ellipse(x - rx * .25, dy + r * .3, rx * .3, r * .22), .7, edge=.4)   # 반짝 반사
        tiled(x0, coin)
    for i in range(8):
        x0, y = (-2 + 4 * rng.random() + 4 * t + 2) % 4 - 2, (rng.random() - .5) * .5
        tw = max(0.0, math.sin(TAU * (t * 2 + rng.random()))) ** 2
        tiled(x0, lambda x: core.poly(star_pts(x, y, .05 * tw + .005, .01, 4), .8 * tw, edge=.5))


def thread_gluttony(t, core, rim, rng):
    # 꿀꺽: 부푼 덩이가 줄을 따라 손 쪽(-x)으로 넘어간다
    def bulge(x):
        best = 0.0
        for c in (-2, 0, 2):
            d = ((x - c + 4 * t) + 2) % 4 - 2
            best = max(best, math.exp(-(d / .35) ** 2))
        return 1 + .7 * best
    pts = line(-2.6, 2.6, 100, lambda x: 0)
    ws = [bulge(x) for x, _ in pts]
    cover(core, rim, pts, wide=.2, body=.1, heart=.02, core_a=.65, widths=ws)
    # 양옆 이빨: 위는 아래로, 아래는 위로 향한 삼각형 (덩이가 지나면 벌어진다)
    for i in range(22):
        x0 = -2 + 4 * (i + .5) / 22
        side = 1 if i % 2 == 0 else -1

        def tooth(x):
            b = bulge(x)
            base = side * (.1 * b + .02)
            tip = base - side * .085
            rim.poly([(x - .045, base + side * .03), (x + .045, base + side * .03), (x, tip)], .98, lum=1, edge=.9)
            core.poly([(x - .02, base + side * .01), (x + .02, base + side * .01), (x, tip + side * .025)], .55, edge=.6)
        tiled(x0, tooth)
    # 침방울: 아래로 늘어졌다 떨어진다
    for i in range(6):
        x0 = -2 + 4 * (i + .5) / 6 + (rng.random() - .5) * .2
        k = (t + rng.random()) % 1
        drop = .14 + .18 * k ** 2
        r = .03 + .02 * min(1, k * 2)

        def bead(x):
            rim.blade(x, -.12, x, -drop, .025 * (1 - k * .5), .85, edge=.4, bias=.8)
            rim.disc(x, -drop, r, .95)
            core.disc(x - r * .3, -drop + r * .3, r * .35, .65)
        tiled(x0, bead)
    for i in range(10):
        x0 = (-2 + 4 * rng.random() + 4 * t + 2) % 4 - 2
        y = (rng.random() - .5) * .6
        tiled(x0, lambda x: rim.poly(ellipse(x, y, .025, .018, 6, rng.random()), .9, edge=.9))


def thread_lust(t, core, rim, rng):
    pts = line(-2.6, 2.6, 80, lambda x: 0)
    cover(core, rim, pts, wide=.22, body=.06, heart=.014, core_a=.6)
    for k in range(3):    # 땋은 세 가닥
        f = lambda x, p=k / 3: .065 * math.sin(TAU * (x / 1.3 + t + p))
        sp = line(-2.6, 2.6, 120, f)
        front = [.5 + .5 * math.cos(TAU * (x / 1.3 + t + k / 3)) for x, _ in sp]
        rim.ribbon(sp, [.022 + .014 * a for a in front], [.5 + .5 * a for a in front], edge=.3)
        core.ribbon(sp, [.006] * len(sp), [.15 + .55 * a ** 2 for a in front], edge=.4)
    # 하트: 줄을 따라 흐르며 두근거린다
    n = 7
    for i in range(n):
        x0 = (-2 + 4 * (i + .5) / n + 4 * t / n * 2 + 2) % 4 - 2
        beat = 1 + .18 * max(0.0, math.sin(TAU * (t * 2 + i * .3))) ** 4
        s = (.16 + .04 * (i % 3) / 2) * beat
        dy = .05 * math.sin(TAU * (t + i / n)) + (.04 if i % 2 else -.04)
        rot = .25 * math.sin(TAU * (t + i * .2))

        def hrt(x):
            rim.poly(heart_pts(x, dy, s * 1.35, rot), .4, edge=.1)
            rim.poly(heart_pts(x, dy, s, rot), .98, edge=.9)
            core.poly(heart_pts(x - s * .25, dy + s * .3, s * .3, rot), .6, edge=.5)
        tiled(x0, hrt)
    for i in range(10):    # 꽃잎
        x0 = (-2 + 4 * rng.random() + 4 * t + 2) % 4 - 2
        y = (rng.random() - .5) * .62
        rot = rng.random() * TAU + TAU * t
        tiled(x0, lambda x: rim.poly(ellipse(x, y, .035, .016, 10, rot), .9, edge=.7))


def thread_envy(t, core, rim, rng):
    wave = lambda x: .05 * math.sin(TAU * (x / 2 + t))
    pts = line(-2.6, 2.6, 80, wave)
    cover(core, rim, pts, wide=.22, body=.07, heart=.016, core_a=.6)
    # 감아 오르는 덩굴 두 줄기
    for phase in (0, .5):
        f = lambda x, p=phase: wave(x) + .085 * math.sin(TAU * (x / 1.6 + p) + TAU * t)
        sp = line(-2.6, 2.6, 100, f)
        rim.ribbon(sp, [.028] * len(sp), [.95] * len(sp), edge=.3)
        core.ribbon(sp, [.008] * len(sp), [.55] * len(sp), edge=.3)
    # 가시: 덩굴에서 비스듬히 돋음
    for i in range(20):
        x0 = -2 + 4 * (i + .5) / 20
        side = 1 if i % 2 == 0 else -1
        ln = .09 + .07 * rng.random()

        def thorn(x):
            y = wave(x) + side * .06
            rim.poly([(x - .03, y), (x + .03, y), (x + .035, y + side * ln)], .98, edge=.9)
        tiled(x0, thorn)
    # 잎: 흔들리는 잎 몇 장
    for i in range(6):
        x0 = -2 + 4 * (i + .25 + .5 * rng.random()) / 6
        side = 1 if i % 2 == 0 else -1
        rot = side * (.7 + .25 * math.sin(TAU * (t + rng.random())))

        def leaf(x):
            cx, cy = x + math.cos(rot) * .09 * side, wave(x) + side * .13
            rim.poly(ellipse(cx, cy, .1, .04, 16, rot * side), .95, edge=.85)
            core.blade(cx - math.cos(rot * side) * .08, cy - math.sin(rot * side) * .08,
                       cx + math.cos(rot * side) * .08, cy + math.sin(rot * side) * .08, .008, .5, edge=.4)
        tiled(x0, leaf)
    # 독방울: 아래로 맺혀 떨어진다
    for i in range(5):
        x0 = -2 + 4 * (i + .5) / 5
        k = (t + rng.random()) % 1
        r = .03 + .015 * k

        def drop(x):
            y = wave(x) - .08 - .2 * k ** 2
            rim.disc(x, y, r, .95 * (1 - k * .4))
            core.disc(x + r * .3, y + r * .3, r * .35, .6)
        tiled(x0, drop)


# ---------------------------------------------------------------- 궤적 꼬리 (x: -2 머리 → +2 꼬리 끝)

def trail_wrath(t, core, rim, rng):
    trail_core_streak(core, rim, lambda x: .05 * math.sin(TAU * (x * .7 - t * 2)) * (x + 2) / 4, .05, glow=.26)
    for i in range(8):
        x0 = -1.9 + 2.6 * i / 8 + .1 * rng.random()
        side = 1 if i % 2 == 0 else -1
        k = taper(x0)
        curl = side * (.14 + .26 * (1 - k)) * (.7 + .3 * math.sin(TAU * (t + rng.random())))
        x1 = x0 + .5 + .4 * rng.random()
        rim.blade(x0, 0, x1, curl, .08 * k + .02, .9 * k, edge=.2, bias=.15)
        core.blade(x0, 0, x0 + (x1 - x0) * .5, curl * .5, .02 * k + .006, .7 * k, edge=.3, bias=.15)
    for _ in range(16):
        x = -1.6 + 3.4 * rng.random()
        drift = (rng.random() - .5) * .7 * (x + 2) / 4
        rim.disc(x + .25 * t, drift, .035 * taper(x) + .008, .9 * taper(x) ** .5)


def trail_sloth(t, core, rim, rng):
    trail_core_streak(core, rim, lambda x: .03 * math.sin(TAU * (x * .4 - t)) * (x + 2) / 4, .04, glow=.32)
    for i in range(12):
        x = -1.9 + 3.4 * i / 12 + .1 * rng.random()
        k = taper(x)
        r = (.08 + .1 * (1 - k)) * (1 + .1 * math.sin(TAU * (t + rng.random())))
        y = (rng.random() - .5) * .3 * (1 - k)
        rim.disc(x + .15 * t, y, r * 1.3, .45 * k ** .5)
        rim.disc(x + .15 * t, y, r * .8, .8 * k ** .7)
    for i in range(3):
        x = -1.2 + 1.2 * i + .3 * rng.random()
        k = (t + rng.random()) % 1
        zee(rim, x, .12 + .2 * k, .05 + .02 * k, .02, .9 * math.sin(math.pi * k) * taper(x) ** .5)


def trail_pride(t, core, rim, rng):
    trail_core_streak(core, rim, lambda x: 0, .035, glow=.22)
    for _ in range(9):
        x = -1.8 + 3.2 * rng.random()
        k = taper(x)
        y = (rng.random() - .5) * .5 * (x + 2) / 4
        tw = .5 + .5 * max(0.0, math.sin(TAU * (t * 2 + rng.random())))
        s = (.07 * k + .02) * tw
        rim.poly(star_pts(x, y, s * 1.4, s * .3, 4, math.pi / 4), .5 * k ** .5, edge=.2)
        rim.poly(star_pts(x, y, s, s * .22, 4), .95 * k ** .4, edge=.6)
        core.poly(star_pts(x, y, s * .5, s * .1, 4), .7 * k ** .4, edge=.6)


def trail_greed(t, core, rim, rng):
    for phase in (0, .5):
        f = lambda x, p=phase: .1 * (x + 2) / 4 * math.sin(TAU * (x * .6 - t + p))
        pts = line(-2, 2, 60, f)
        rim.ribbon(pts, [.07 * taper(x) + .005 for x, _ in pts], [.55 * taper(x) for x, _ in pts], edge=0)
        rim.ribbon(pts, [.025 * taper(x) + .004 for x, _ in pts], [.95 * taper(x) for x, _ in pts], edge=.3)
        core.ribbon(pts, [.008 * taper(x) + .002 for x, _ in pts], [.65 * taper(x) ** .8 for x, _ in pts], edge=.3)
    for i in range(8):     # 흩날리는 금화
        x = -1.7 + 3.2 * rng.random()
        k = taper(x)
        y = (rng.random() - .5) * .6 * (1 - k)
        spin = TAU * (t * 2 + rng.random())
        r = .07 * k + .025
        rx = r * (.2 + .8 * abs(math.cos(spin)))
        rim.poly(ellipse(x + .2 * t, y, rx, r), .95 * k ** .4, edge=.85)
        core.poly(ellipse(x + .2 * t - rx * .25, y + r * .3, rx * .3, r * .22), .6 * k ** .4, edge=.4)


def trail_gluttony(t, core, rim, rng):
    trail_core_streak(core, rim, lambda x: 0, .05, glow=.22)
    for i in range(14):
        x = -1.9 + 3.2 * i / 14
        k = taper(x)
        side = 1 if i % 2 == 0 else -1
        base = side * (.06 * k + .02)
        tip = base - side * (.06 * k + .015)
        w = .035 * k + .01
        rim.poly([(x - w, base + side * .02), (x + w, base + side * .02), (x, tip)], .95 * k ** .5, edge=.9)
    for _ in range(12):
        x = -1.8 + 3.4 * rng.random()
        k = taper(x)
        y = -(rng.random() * .35) * (1 - k) - .03
        r = .035 * k + .01
        rim.disc(x + .2 * t, y, r, .9 * k ** .4)
        core.disc(x + .2 * t - r * .3, y + r * .3, r * .35, .6 * k ** .4)


def trail_lust(t, core, rim, rng):
    for k3 in range(3):
        f = lambda x, p=k3 / 3: .12 * (x + 2) / 4 * math.sin(TAU * (x * .8 - t + p)) + .02 * math.sin(TAU * p)
        pts = line(-2, 2, 80, f)
        rim.ribbon(pts, [.06 * taper(x) + .004 for x, _ in pts], [.4 * taper(x) for x, _ in pts], edge=0)
        rim.ribbon(pts, [.025 * taper(x) + .004 for x, _ in pts], [.9 * taper(x) for x, _ in pts], edge=.25)
        core.ribbon(pts, [.007 * taper(x) + .002 for x, _ in pts], [.6 * taper(x) ** .7 for x, _ in pts], edge=.4)
    for i in range(7):
        x = -1.7 + 3.0 * rng.random()
        k = taper(x)
        y = (rng.random() - .5) * .5 * (1 - k)
        s = .09 * k + .025
        rot = (rng.random() - .5) * .8 + .3 * math.sin(TAU * t)
        rim.poly(heart_pts(x + .15 * t, y, s, rot), .95 * k ** .4, edge=.9)
        core.poly(heart_pts(x + .15 * t - s * .25, y + s * .3, s * .3, rot), .55 * k ** .4, edge=.5)


def trail_envy(t, core, rim, rng):
    trail_core_streak(core, rim, lambda x: .04 * math.sin(TAU * (x * .5 - t)) * (x + 2) / 4, .04, glow=.24)
    for phase in (0, .5):
        f = lambda x, p=phase: .09 * (x + 2) / 4 * math.sin(TAU * (x * .7 - t + p))
        pts = line(-2, 2, 70, f)
        rim.ribbon(pts, [.022 * taper(x) + .004 for x, _ in pts], [.9 * taper(x) for x, _ in pts], edge=.3)
    for i in range(12):
        x = -1.9 + 3.2 * i / 12
        k = taper(x)
        side = 1 if i % 2 == 0 else -1
        ln = .08 * k + .02
        rim.poly([(x - .025, side * .03), (x + .025, side * .03), (x + .03, side * (.03 + ln))], .95 * k ** .5, edge=.9)
    for _ in range(10):
        x = -1.8 + 3.4 * rng.random()
        k = taper(x)
        y = (rng.random() - .3) * .5 * (1 - k)
        r = .035 * k + .012
        rim.disc(x + .15 * t, y, r, .8 * k ** .4)
        core.disc(x + .15 * t + r * .35, y + r * .35, r * .3, .6 * k ** .4)


# ---------------------------------------------------------------- 머리 광구 (x, y ∈ [-2, 2])

def head_sin(style, core, rim, rng):
    rim.disc(0, 0, 1.9, .5)
    rim.disc(0, 0, 1.0, .9)
    core.disc(0, 0, .7, .8)
    if style == "wrath":
        for i in range(10):
            a = TAU * i / 10 + rng.random() * .25
            r = 1.35 + .5 * rng.random()
            rim.blade(0, 0, math.cos(a) * r, math.sin(a) * r, .3, .9, edge=.2, bias=.12)
            core.blade(0, 0, math.cos(a) * r * .6, math.sin(a) * r * .6, .08, .7, edge=.3, bias=.12)
    elif style == "sloth":
        for i in range(7):
            a = TAU * i / 7
            rim.disc(math.cos(a) * 1.05, math.sin(a) * .9, .55, .75)
        zee(core, .5, .55, .35, .1, .8)
    elif style == "pride":
        rim.poly(star_pts(0, 0, 1.95, .35, 4), .95, edge=.5)
        rim.poly(star_pts(0, 0, 1.4, .3, 4, math.pi / 4), .8, edge=.4)
        core.poly(star_pts(0, 0, 1.2, .2, 4), .75, edge=.5)
    elif style == "greed":
        rim.poly(ellipse(0, 0, 1.25, 1.25, 28), .98, edge=.9)
        rim.poly(ellipse(0, 0, .95, .95, 28), .55, lum=.6, edge=1)
        core.poly(ellipse(-.35, .4, .35, .25, 16), .75, edge=.4)
        for i in range(4):
            a = TAU * i / 4 + TAU / 8
            rim.blade(0, 0, math.cos(a) * 1.95, math.sin(a) * 1.95, .14, .7, edge=.2, bias=.1)
    elif style == "gluttony":
        for i in range(9):
            a = TAU * i / 9
            cx, cy = math.cos(a) * 1.2, math.sin(a) * 1.2
            tx, ty = math.cos(a) * .55, math.sin(a) * .55
            nx, ny = -math.sin(a) * .28, math.cos(a) * .28
            rim.poly([(cx + nx, cy + ny), (cx - nx, cy - ny), (tx, ty)], .95, edge=.9)
    elif style == "lust":
        rim.poly(heart_pts(0, -.1, 1.75), .95, edge=.85)
        core.poly(heart_pts(-.4, .35, .5), .7, edge=.5)
    elif style == "envy":
        for i in range(8):
            a = TAU * i / 8 + rng.random() * .3
            rim.poly([(math.cos(a + .25) * .8, math.sin(a + .25) * .8), (math.cos(a - .25) * .8, math.sin(a - .25) * .8),
                      (math.cos(a) * 1.9, math.sin(a) * 1.9)], .9, edge=.8)
        rim.poly(ellipse(0, 0, .9, .5, 24), .5, lum=.6, edge=1)
        core.poly(ellipse(0, 0, .22, .45, 16), .7, edge=.8)


for _s in SIN_STYLES:
    STYLES[_s] = (globals()[f"thread_{_s}"], globals()[f"trail_{_s}"])
