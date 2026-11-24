"""22차 칠죄종 이펙트 그림 함수. render_element_vfx.py 가 exec 로 읽어 들인다 (Layer, PAL, 도우미 함수를 그대로 쓴다). 단독 실행 안 함.

죄종 7가지 × (부가 발동 proc, 보스 내려찍기 slam, 보스 탄 bullet, 맵 장식 decor, 결박 폭발 bind) + 바닥 장판 (분노, 시기).
모양 콘셉트:
  분노 wrath    진홍 불길, 쩍쩍 갈라진 땅, 만화식 분노 표시
  나태 sloth    회청 졸음 구름과 안개, 초승달, Zzz
  교만 pride    금관과 보랏빛 후광, 부채꼴 햇살 (눈부심)
  탐욕 greed    빙글 도는 금화, 금실 채찍과 올가미
  식탐 gluttony 주황 이빨 아가리가 콱 무는 모습, 부스러기와 침방울
  색욕 lust     분홍 하트와 장미 꽃잎, 안쪽으로 끌려 드는 실
  시기 envy     초록 눈, 가시 독 덩굴, 독방울
색은 HANDOFF 22차 결정 색을 바탕으로 짙음 → 가운데 → 밝음 → 하이라이트 종이 물감 톤.
무작위 값은 함수 첫머리에서 미리 뽑아 둔다 (칸마다 같은 배치 → 이어지는 움직임).
"""

SIN_HEX = {
    "wrath":    dict(deep="#4a0c10", mid="#9c1f22", light="#d2473a", hi="#f3b48c", glow="#c62e2e"),
    "sloth":    dict(deep="#2a3a4c", mid="#56708a", light="#8ea8bc", hi="#d8e3eb", glow="#6f8fa8"),
    "pride":    dict(deep="#2c1a52", mid="#5a3a9c", light="#8d6cd2", hi="#dccff5", glow="#7b4fc9"),
    "greed":    dict(deep="#5a3c08", mid="#a87818", light="#dcab2e", hi="#f8e6a4", glow="#d9a520"),
    "gluttony": dict(deep="#5c290a", mid="#b0561c", light="#e58a42", hi="#f9d6a8", glow="#e07a2a"),
    "lust":     dict(deep="#5a1033", mid="#a82c68", light="#de609e", hi="#f8c8de", glow="#d9418c"),
    "envy":     dict(deep="#1a3a1a", mid="#377534", light="#68b45f", hi="#c6e8b2", glow="#4fa34a"),
}
SINS = list(SIN_HEX)
for _k, _v in SIN_HEX.items():
    PALETTE_HEX[_k] = _v
    PAL[_k] = {n: lin(hexc(h)) for n, h in _v.items()}
GOLD = PAL["greed"]

TOOTH = lin(hexc("#efe2c4"))
MAW = lin(hexc("#2a0d0a"))
GUM = lin(hexc("#8a2a26"))
CRUMB = lin(hexc("#c8975a"))
CRUMB_L = lin(hexc("#ead0a0"))
SCLERA = lin(hexc("#e4eed2"))
BOWL, BOWL_L = lin(hexc("#4e3b2c"), ), lin(hexc("#86684d"))
LEAF_G, LEAF_H = lin(hexc("#3f6a34")), lin(hexc("#a9cf8a"))


# ---------------------------------------------------------------- 죄종 공용 그림 조각
def rotv(dx, dy, rot):
    c, s = math.cos(rot), math.sin(rot)
    return dx * c - dy * s, dx * s + dy * c


def ellipse_pts(x, y, rx, ry, n=24, rot=0.0):
    out = []
    for i in range(n):
        a = i * TAU / n
        px, py = rotv(math.cos(a) * rx, math.sin(a) * ry, rot)
        out.append((x + px, y + py))
    return out


def partial(pts, f):
    """점 목록의 앞 f(0~1) 만큼 (끝점은 보간)."""
    if f >= 1:
        return pts
    m = (len(pts) - 1) * max(0.0, f)
    i = int(m)
    out = pts[:i + 1]
    if i + 1 < len(pts):
        k = m - i
        a, b = pts[i], pts[i + 1]
        out = out + [(a[0] + (b[0] - a[0]) * k, a[1] + (b[1] - a[1]) * k)]
    return out


def heart_pts(x, y, s, rot=0.0, sy=1.0, n=40):
    """하트 윤곽 (폭 2s, 높이 약 1.8s, 가운데 (x, y)). 0번 점이 위쪽 오목한 곳, n/2 가 아래 뾰족한 끝."""
    out = []
    for i in range(n):
        a = i * TAU / n
        hx = math.sin(a) ** 3
        hy = (13 * math.cos(a) - 5 * math.cos(2 * a) - 2 * math.cos(3 * a) - math.cos(4 * a)) / 16 + .12
        px, py = rotv(hx * s, hy * s, rot)
        out.append((x + px, y + py * sy))
    return out


def heart(L, x, y, s, P, alpha=1.0, rot=0.0, sy=1.0):
    if s < .01 or alpha <= .003:
        return
    L.poly(heart_pts(x, y, s, rot, sy), P["deep"], alpha)
    ox, oy = rotv(0, .05 * s, rot)
    L.poly(heart_pts(x + ox, y + oy * sy, s * .8, rot, sy), P["mid"], alpha)
    ox, oy = rotv(-.08 * s, .12 * s, rot)
    L.poly(heart_pts(x + ox, y + oy * sy, s * .48, rot, sy), P["light"], alpha)
    ox, oy = rotv(-.42 * s, .38 * s, rot)
    L.blob(x + ox, y + oy * sy, .14 * s, P["hi"], alpha, rough=.05, seg=12)


def heart_ring(L, x, y, s, width, col, alpha, sy=1.0, f=1.0):
    """하트 윤곽선. f = 그린 정도 (위 오목한 곳에서 양쪽으로 아래 끝까지)."""
    pts = heart_pts(x, y, s, 0, sy, 48)
    right = pts[:25]
    left = [pts[0]] + pts[47:23:-1]
    for half in (right, left):
        L.ribbon(partial(half, f), width, col, alpha)


def coin(L, x, y, r, spin, P, alpha=1.0):
    """빙글 도는 금화 (spin = 회전 각, 옆으로 납작해짐)."""
    if r < .01 or alpha <= .003:
        return
    sx = max(.14, abs(math.cos(spin)))
    L.poly(ellipse_pts(x, y, r * sx, r, 22), P["deep"], alpha)
    L.poly(ellipse_pts(x, y, r * .82 * sx, r * .82, 22), P["light"], alpha)
    if sx > .3:
        L.poly(ellipse_pts(x + r * .04 * sx, y - r * .04, r * .52 * sx, r * .52, 18), P["mid"], alpha)
        L.blob(x - r * .36 * sx, y + r * .38, r * .15, P["hi"], alpha, rough=0, seg=10)


def flat_coin(L, x, y, r, P, top=None, alpha=1.0):
    """바닥에 누운 금화 (위에서 비스듬히)."""
    L.poly(ellipse_pts(x, y, r, r * .42, 20), P["deep"], alpha)
    L.poly(ellipse_pts(x, y + r * .1, r * .9, r * .34, 20), top or P["light"], alpha)


def crown(L, x, y, w, h, alpha=1.0, gem=None):
    """금관: 밑변 가운데 (x, y), 폭 2w, 높이 h. 뾰족 셋 + 보석."""
    if w < .01 or alpha <= .003:
        return
    out = [(x - w, y), (x + w, y), (x + w * 1.14, y + h), (x + w * .52, y + h * .52), (x, y + h * 1.2),
           (x - w * .52, y + h * .52), (x - w * 1.14, y + h)]
    L.poly(out, GOLD["mid"], alpha)
    inner = [(x - w * .8, y + h * .14), (x + w * .8, y + h * .14), (x + w * .9, y + h * .74), (x + w * .5, y + h * .4),
             (x, y + h * .96), (x - w * .5, y + h * .4), (x - w * .9, y + h * .74)]
    L.poly(inner, GOLD["light"], alpha)
    L.ribbon([(x - w * 1.0, y + h * .1), (x + w * 1.0, y + h * .1)], h * .13, GOLD["deep"], alpha)
    L.ribbon([(x - w * .95, y + h * .1), (x + w * .95, y + h * .1)], h * .05, GOLD["hi"], alpha)
    for px, py in ((x - w * 1.14, y + h), (x, y + h * 1.2), (x + w * 1.14, y + h)):
        L.blob(px, py, w * .13, GOLD["hi"], alpha, rough=0, seg=12)
    g = gem or PAL["pride"]
    L.blob(x, y + h * .45, w * .2, g["mid"], alpha, rough=.05, seg=14)
    L.blob(x - w * .05, y + h * .5, w * .09, g["hi"], alpha, rough=0, seg=10)
    for sx in (-1, 1):
        L.blob(x + sx * w * .62, y + h * .3, w * .1, g["light"], alpha, rough=0, seg=10)


def eye(L, x, y, w, op, P, look=0.0, alpha=1.0, sy=1.0, slit=1.0):
    """아몬드 모양 초록 눈. op = 뜬 정도 (0 감음 ~ 1), look = 눈동자 좌우 (-1~1), slit = 동공 폭 배율."""
    if w < .01 or alpha <= .003:
        return
    n = 18
    h = w * .56 * op
    tf = lambda px, py: (x + px, y + py * sy)
    up = [tf(-w + 2 * w * i / n, h * math.sin(math.pi * i / n) ** .85) for i in range(n + 1)]
    if op < .1:   # 감은 눈: 눈꺼풀 선 + 속눈썹
        L.ribbon(up, [w * .07] * len(up), P["deep"], alpha)
        for k in range(5):
            u = .2 + .6 * k / 4
            px = -w + 2 * w * u
            L.blade(*tf(px, -.02 * w), *tf(px * 1.1, -.22 * w), w * .05, P["deep"], alpha, n=4, bias=.2)
        return
    lo = [tf(w - 2 * w * i / n, -h * .78 * math.sin(math.pi * i / n) ** .85) for i in range(1, n)]
    rim_up = [tf(-w * 1.1 + 2.2 * w * i / n, (h + .09 * w) * math.sin(math.pi * i / n) ** .85) for i in range(n + 1)]
    rim_lo = [tf(w * 1.1 - 2.2 * w * i / n, -(h * .78 + .07 * w) * math.sin(math.pi * i / n) ** .85) for i in range(1, n)]
    L.poly(rim_up + rim_lo, P["deep"], alpha)
    L.poly(up + lo, SCLERA, alpha)
    ri = min(w * .42, h * .74)
    ix = look * w * .3
    L.blob(*tf(ix, 0), ri, P["mid"], alpha, sy=sy, rough=.02, seg=24)
    L.blob(*tf(ix, ri * .1), ri * .72, P["light"], alpha, sy=sy, rough=.02, seg=20)
    L.blade(*tf(ix, -ri * .88), *tf(ix, ri * .88), ri * .26 * slit + .004, INK, alpha, n=8, bias=.5)
    L.blob(*tf(ix - ri * .35, ri * .38), ri * .16, SCLERA, alpha, sy=sy, rough=0, seg=10)
    L.ribbon(up, [w * .06] * len(up), P["deep"], alpha)


def zee(L, x, y, s, col, alpha, width, tilt):
    corners = [(-.5, .5), (.5, .5), (-.5, -.5), (.5, -.5)]
    pts = [(x + px * s, y + py * s) for px, py in (rotv(cx, cy, tilt) for cx, cy in corners)]
    for a, b in zip(pts, pts[1:]):
        L.ribbon([a, b], width, col, alpha)
    for px, py in pts[1:3]:
        L.blob(px, py, width * 1.0, col, alpha, rough=0, seg=10)


def zz(L, P, x, y, s, alpha=1.0, tilt=.2):
    """졸음 Z: 짙은 획 위에 밝은 획."""
    if s < .02 or alpha <= .003:
        return
    zee(L, x, y, s, P["deep"], alpha, s * .2, tilt)
    zee(L, x, y, s, P["hi"], alpha, s * .09, tilt)


def cloud(L, P, x, y, s, alpha=1.0, seed=0.0, flat=1.0):
    """종이를 오려 붙인 졸음 구름 (아래가 짙고 위가 밝다)."""
    if s < .02 or alpha <= .003:
        return
    lobes = [(-.6, -.05, .4), (-.22, .14, .5), (.22, .17, .47), (.6, -.03, .38), (0, -.14, .46)]
    for dx, dy, r in lobes:
        L.blob(x + dx * s, y + (dy - .07) * s * flat, r * s, P["deep"], alpha, sy=flat, rough=.08, seed=seed + dx * 7)
    for dx, dy, r in lobes:
        L.blob(x + dx * s, y + dy * s * flat, r * s * .92, P["mid"], alpha, sy=flat, rough=.08, seed=seed + dy * 9)
    for dx, dy, r in lobes[:4]:
        L.blob(x + dx * s - .04 * s, y + (dy + .1) * s * flat, r * s * .58, P["light"], alpha, sy=flat, rough=.1, seed=seed + dy * 5)
    L.blob(x - .25 * s, y + .32 * s * flat, .15 * s, P["hi"], alpha, sy=flat, rough=.1)


def sleepy_eyes(L, P, x, y, s, alpha=1.0, flat=1.0):
    for sx in (-1, 1):
        L.arc(x + sx * .27 * s, y, .13 * s, math.pi * 1.12, math.pi * 1.88, .04 * s, P["deep"], alpha, sy=flat, n=10, taper=False)


def jaws(L, P, cx, cy, w, h, gap, alpha=1.0, fangs=True):
    """정면에서 본 주황 아가리: 아래턱 → 위턱. gap = 위아래 이빨 사이 벌어짐."""
    if w < .02 or alpha <= .003:
        return
    if gap > .02:
        L.poly(ellipse_pts(cx, cy, w * .86, gap * .5 + h * .2, 26), MAW, alpha)
        L.blob(cx, cy - gap * .25, w * .38, GUM, alpha, sy=.45, rough=.1)   # 혀
    for side in (-1, 1):   # -1 아래턱, 1 위턱
        y0 = cy + side * gap * .5
        dome = [(cx + w * math.cos(a), y0 + side * h * math.sin(a)) for a in (math.pi * i / 16 for i in range(17))]
        L.poly(dome, P["deep"], alpha)
        inner = [(cx + w * .86 * math.cos(a), y0 + side * (h * .78 * math.sin(a) + h * .1)) for a in (math.pi * i / 16 for i in range(17))]
        L.poly(inner, P["mid"], alpha)
        L.blob(cx - w * .3, y0 + side * h * .62, w * .2, P["light"], alpha, sy=.5, rough=.1)
        L.ribbon([(cx - w * .98, y0), (cx + w * .98, y0)], h * .12, GUM, alpha)   # 잇몸
        n = 7
        for k in range(n):
            u = (k + .5) / n
            tx = cx - w * .86 + 1.72 * w * u
            big = fangs and k in (0, n - 1)
            tw = w * (.12 if big else .1)
            th = h * (.62 if big else .36) * (1 - .25 * abs(u - .5))
            L.poly([(tx - tw, y0), (tx + tw, y0), (tx, y0 - side * th)], TOOTH, alpha)


def crumb(L, x, y, r, col, alpha=1.0, seed=0.0):
    if r < .005 or alpha <= .003:
        return
    pts = [(x + math.cos(a) * r * (.7 + .45 * math.sin(seed * 3.1 + k * 2.3)), y + math.sin(a) * r * (.7 + .45 * math.cos(seed * 1.7 + k * 1.9)))
           for k, a in enumerate(i * TAU / 5 + seed for i in range(5))]
    L.poly(pts, col, alpha)


def petal(L, x, y, ang, size, P, alpha=1.0):
    if size < .01 or alpha <= .003:
        return
    dx, dy = math.cos(ang) * size, math.sin(ang) * size
    L.blade(x - dx, y - dy, x + dx, y + dy, size * .6, P["mid"], alpha, n=10, bias=.62)
    L.blade(x - dx * .5, y - dy * .5, x + dx * .75, y + dy * .75, size * .28, P["light"], alpha, n=8, bias=.62)


def vine_path(x0, y0, ang, length, curl, grow, n=16, wave=0.0, ph=0.0):
    """끝으로 갈수록 말려 드는 덩굴 줄기 점들."""
    pts = [(x0, y0)]
    x, y, a = x0, y0, ang
    step = length * max(grow, .001) / n
    for i in range(n):
        u = (i + 1) / n
        a += curl * u * u * 3.2 / n + wave * math.cos(ph + u * 6) / n
        x += math.cos(a) * step
        y += math.sin(a) * step
        pts.append((x, y))
    return pts


def vine(L, P, pts, width, alpha=1.0, thorns=5, leaves=0, sy=1.0):
    """가시 덩굴: 짙은 줄기 + 밝은 결 + 양옆 가시 (+ 잎)."""
    n = len(pts)
    if n < 2 or alpha <= .003:
        return
    ws = [width * (1 - i / (n - 1)) ** .6 + .008 for i in range(n)]
    L.ribbon(pts, ws, P["deep"], alpha)
    L.ribbon(pts, [w * .45 for w in ws], P["mid"], alpha)
    L.ribbon(pts[: max(2, n * 2 // 3)], [w * .15 for w in ws[: max(2, n * 2 // 3)]], P["light"], alpha)
    for k in range(thorns):
        i = 1 + int((n - 3) * (k + .5) / max(1, thorns))
        (x0, y0), (x1, y1) = pts[i - 1], pts[i + 1]
        tx, ty = x1 - x0, y1 - y0
        ln = math.hypot(tx, ty) or 1
        nx, ny = -ty / ln, tx / ln
        side = 1 if k % 2 else -1
        x, y = pts[i]
        w = ws[i]
        L.blade(x, y, x + nx * side * w * 2.6 + tx / ln * w * 1.4, y + (ny * side * w * 2.6 + ty / ln * w * 1.4) * sy, w * .85, P["deep"], alpha, n=4, bias=.12)
    for k in range(leaves):
        i = 1 + int((n - 3) * (k + .3) / max(1, leaves))
        (x0, y0), (x1, y1) = pts[i - 1], pts[i + 1]
        ang = math.atan2(y1 - y0, x1 - x0) + (1.0 if k % 2 else -1.0)
        x, y = pts[i]
        leaf(L, x + math.cos(ang) * width * 1.6, y + math.sin(ang) * width * 1.6 * sy, ang, width * 1.5, P["mid"], P["hi"], alpha)


def crack_paths(rng, n, r0, r1, y=GY):
    paths = []
    for i in range(n):
        ang = i * TAU / n + rng.uniform(-.3, .3)
        ln = rng.uniform(r0, r1)
        jit = [rng.uniform(-.35, .35) for _ in range(6)]
        pts = []
        for k in range(6):
            u = k / 5
            a = ang + (jit[k] if k else 0)
            pts.append((math.cos(a) * ln * u, y + math.sin(a) * ln * u * PY))
        paths.append(pts)
    return paths


def draw_cracks(B, G, P, paths, grow, glow, alpha=1.0):
    """진홍빛으로 달아오른 땅의 틈."""
    if grow <= .01 or alpha <= .003:
        return
    for pts in paths:
        sub = partial(pts, grow)
        n = len(sub)
        ws = [.065 * (1 - i / len(pts)) + .014 for i in range(n)]
        B.ribbon(sub, ws, mixc(P["deep"], INK, .55), alpha)
        B.ribbon(sub, [w * .55 for w in ws], P["light"], alpha * glow)
        B.ribbon(sub, [w * .22 for w in ws], P["hi"], alpha * glow)
        G.ribbon(sub, [w * 3.2 for w in ws], P["glow"], .42 * alpha * glow, edge=0)


def anger_mark(B, P, x, y, s, alpha=1.0, rot=0.0):
    """만화식 분노 표시: 가운데로 휜 네 갈래 굵은 곡선."""
    if s < .02 or alpha <= .003:
        return
    for k in range(4):
        a = rot + math.pi / 4 + k * math.pi / 2
        cx, cy = x + math.cos(a) * s * .66, y + math.sin(a) * s * .66
        B.arc(cx, cy, s * .46, a + math.pi - 1.2, a + math.pi + 1.2, s * .22, P["deep"], alpha, n=16)
        B.arc(cx, cy, s * .46, a + math.pi - 1.0, a + math.pi + 1.0, s * .1, P["hi"], alpha, n=16)


def blink(t, at, width=.07):
    """at 근처에서 잠깐 감기는 눈 (고리 시간)."""
    d = abs(((t - at + .5) % 1) - .5)
    return 1 - .95 * clamp(1 - d / width)


def burst_drops(B, G, P, t, rng_vals, start, cx, cy, spread, up, cols=("light", "hi"), r=.05, g=3.4, floor=None):
    for i, (dx, vx, vy, rr) in enumerate(rng_vals):
        s = seg(t, start, 1) * .85
        if s <= 0:
            continue
        x, y, vx2, vy2 = ballistic(cx + dx, cy, vx * spread, vy * up, s, g)
        if floor is not None and y < floor:
            continue
        drop(B, x, y, vx2, vy2, r * (.7 + .6 * rr), P[cols[i % len(cols)]], 1 - seg(s, .55, .85))


def spiral_strands(t, n, grow, sq, fade, width=.15, turns=3.2, height=2.0, phase=0.0):
    """결박: 바닥에서 적을 감아 오르는 가닥들의 점 목록과 굵기."""
    out = []
    for i in range(n):
        a0 = i * TAU / n + phase
        m = 20
        pts, ws = [], []
        for k in range(m + 1):
            u = k / m * grow
            ang = a0 + u * turns
            rad = (1.0 - .55 * u) * (1 - .35 * sq * u)
            pts.append((math.cos(ang) * rad, GY + u * height + math.sin(ang) * rad * PY * .6))
            ws.append(width * (1 - k / m) ** .6 * (.4 + .6 * fade) + .01)
        out.append((a0, pts, ws))
    return out


# ================================================================ 분노 wrath — 진홍 불길과 갈라진 땅

def slam_wrath(t, B, G, P, rng):
    """분노 분출: 땅이 진홍빛으로 쩍쩍 갈라지고, 틈에서 날 선 불길이 치솟았다 꺼지며 불씨가 튄다."""
    paths = crack_paths(rng, 8, .9, 1.75)
    tng = [(rng.uniform(-.75, .75), rng.random(), rng.random() * TAU) for _ in range(9)]
    emb = [(rng.uniform(0, .25), rng.uniform(-.5, .5), rng.uniform(-1.8, 1.8), rng.uniform(2, 3.6), rng.random()) for _ in range(22)]
    fade = 1 - seg(t, .7, 1)
    B.blob(0, GY, .4 + .95 * eo(seg(t, 0, .25)), mixc(P["deep"], INK, .55), .85 * fade, sy=PY, rough=.14, seed=1)
    draw_cracks(B, G, P, paths, eo(seg(t, 0, .22)), 1 - seg(t, .5, .95), fade)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .08)
    rise = eo(seg(t, .04, .2), 2) * (1 - seg(t, .5, .95)) ** 1.1
    G.glow(0, GY + 1.0 * rise, 1.6 * rise + .2, P["glow"], .55 * rise)
    G.glow(0, GY, 1.5, P["glow"], .4 * rise, sy=PY)
    for x, tall, ph in sorted(tng, key=lambda v: -abs(v[0])):
        h = (1.3 + 1.4 * tall) * (1 - abs(x) * .45) * rise * (.85 + .15 * math.sin(TAU * t * 3 + ph))
        fire_tongue(B, P, x, GY - .05, x * .45, 1, h, .26 * math.sin(TAU * t * 2.5 + ph), .3 + .1 * tall, ph=ph)
    sp = pop(t, .06, .2) * (1 - seg(t, .4, .7))
    if sp > .02:           # 날 선 분노 가시
        for i in range(7):
            a = math.pi * (.12 + .76 * i / 6)
            ln = (1.3 + .5 * (i % 2)) * sp
            x0, y0 = math.cos(a) * .25, GY + .3 + math.sin(a) * .2
            B.blade(x0, y0, x0 + math.cos(a) * ln, y0 + math.sin(a) * ln, .16 * sp, P["deep"], bias=.2)
            B.blade(x0, y0, x0 + math.cos(a) * ln * .85, y0 + math.sin(a) * ln * .85, .08 * sp, P["light"], bias=.2)
    for i, (d, x0, vx, vy, r) in enumerate(emb):
        s = seg(t, d, 1) * .9
        if s <= 0:
            continue
        x, y, vx2, vy2 = ballistic(x0, GY + .3, vx, vy, s, 2.4)
        a = (1 - seg(t, d + .35, 1)) ** 1.2
        drop(B, x, y, vx2, vy2, .03 + .03 * r, P["hi"] if i % 3 == 0 else P["light"], a, .03)
        G.glow(x, y, .14, P["glow"], .35 * a)


def bullet_wrath(t, B, G, P, rng):
    G.glow(-.3, 0, 1.4, P["glow"], .5)
    for i in range(5):
        ph = i / 5
        k = (t + ph) % 1
        fire_tongue(B, P, .1, (i - 2) * .13, -1, (i - 2) * .07, 1.1 + .6 * math.sin(math.pi * k), .28 * math.sin(TAU * (t + ph)), .44, ph=ph * 5)
    rot = TAU * t / 2
    for k in range(6):     # 날 선 분노 가시 고리
        a = rot + k * TAU / 6
        B.blade(.15 + math.cos(a) * .35, math.sin(a) * .35, .15 + math.cos(a) * .9, math.sin(a) * .9, .14, P["deep"], bias=.15)
    B.blob(.15, 0, .55, P["mid"], rough=.12, seed=t * 6)
    B.blob(.2, .02, .38, P["light"], rough=.15, seed=t * 7)
    B.blob(.26, .06, .17, P["hi"], rough=.1)
    for i in range(5):
        k = (t + i / 5) % 1
        B.blob(-.5 - 1.3 * k, math.sin(i * 2.7) * .4 * k, .07 * (1 - k) + .02, P["hi"] if i % 2 else P["light"], 1 - k)


def pool_wrath(t, B, G, P, rng):
    paths = crack_paths(rng, 9, .8, 1.5, y=PG)
    spots = [(rng.uniform(0, TAU), math.sqrt(rng.random()) * 1.25, rng.random(), rng.uniform(.5, 1)) for _ in range(10)]
    pulse = .75 + .25 * math.sin(TAU * t)
    G.glow(0, PG + .3, 1.9, P["glow"], .38 + .08 * math.sin(TAU * t * 2))
    G.glow(0, PG, 1.9, P["glow"], .32, sy=PY)
    B.blob(0, PG, 1.75, mixc(P["deep"], INK, .55), .92, sy=PY, rough=.12, seed=1)
    B.blob(0, PG, 1.4, mixc(P["deep"], INK, .25), .9, sy=PY, rough=.16, seed=2)
    draw_cracks(B, G, P, paths, 1, pulse, 1)
    for sy_, x, ph, tall in sorted(((math.sin(a) * r, math.cos(a) * r, ph, tall) for a, r, ph, tall in spots), reverse=True):
        y = PG + sy_ * PY
        h = (.35 + .5 * tall) * (.65 + .35 * math.sin(TAU * (t + ph)) + .1 * math.sin(TAU * (2 * t + ph * 3)))
        fire_tongue(B, P, x, y, 0, 1, h, .1 * math.sin(TAU * (t + ph * 2)), .2 + .06 * tall, ph=ph * TAU)
    for i in range(10):
        k = (t + (i * .37) % 1) % 1
        x = math.sin(i * 2.3) * 1.3 + .15 * math.sin(TAU * k + i)
        y = PG + math.cos(i * 1.7) * .35 + 1.8 * k
        B.blob(x, y, .035, P["hi"] if i % 2 else P["light"], math.sin(math.pi * k), rough=.1)


def proc_wrath(t, B, G, P, rng):
    """분노 발동: 날 선 진홍 불꽃 가시가 별처럼 터지고, 가운데 분노 표시가 튀어나왔다 사라지며 불똥이 흩어진다."""
    s = pop(t, 0, .2) * (1 - seg(t, .55, 1))
    G.glow(0, 0, 1.8 * s + .2, P["glow"], .58 * bump(t, .1, 1))
    thin = 1 - .6 * seg(t, .5, 1)
    for i in range(10):
        a = .3 * t + i * TAU / 10
        ln = (1.7 if i % 2 == 0 else 1.1) * s
        sw = .2 * (1 if i % 2 else -1)
        x0, y0 = math.cos(a) * .1, math.sin(a) * .1
        tongue(B, x0, y0, math.cos(a), math.sin(a), ln, sw, .3 * thin, P["deep"])
        tongue(B, x0, y0, math.cos(a), math.sin(a), ln * .78, sw * .8, .19 * thin, P["mid"])
        tongue(B, x0, y0, math.cos(a), math.sin(a), ln * .5, sw * .5, .09 * thin, P["hi"])
    ring = seg(t, .05, .6)
    if 0 < ring < 1:
        B.arc(0, 0, .4 + 1.4 * eo(ring), 0, TAU, .07 * (1 - ring) + .01, P["light"], 1 - ring, n=40, taper=False)
    m = pop(t, .08, .26) * (1 - seg(t, .6, .9))
    if m > .02:
        B.blob(0, 0, .8 * m, P["mid"], rough=.25, seed=3)
        anger_mark(B, P, 0, 0, .82 * m)
    k = seg(t, .1, .9)
    if 0 < k < 1:
        for i in range(16):
            a = i * TAU / 16 + math.sin(i * 3.1) * .2
            d = (.5 + 1.3 * eo(k)) * (.75 + .25 * math.sin(i * 1.9))
            drop(B, math.cos(a) * d, math.sin(a) * d, math.cos(a), math.sin(a), .045 * (1 - k) + .01, P["hi"] if i % 2 else P["light"], 1 - k, .15)


def decor_wrath(t, B, G, P, rng):
    """분노의 불씨 자리: 그을려 갈라진 땅의 틈이 진홍빛으로 천천히 맥박치고, 틈에서 작은 불길이 날름거리며 불씨가 오른다."""
    paths = crack_paths(rng, 7, .7, 1.35)
    pulse = .7 + .3 * math.sin(TAU * t)
    G.glow(0, GY + .4, 1.4, P["glow"], .28 + .06 * math.sin(TAU * t))
    G.glow(0, GY, 1.5, P["glow"], .28, sy=PY)
    B.blob(0, GY, 1.35, mixc(P["deep"], INK, .6), .9, sy=PY, rough=.18, seed=2)
    B.blob(.05, GY + .02, .95, mixc(P["deep"], INK, .35), .85, sy=PY, rough=.22, seed=3)
    draw_cracks(B, G, P, paths, 1, pulse, 1)
    for x, y, h, ph in ((-.45, GY + .12, .5, .1), (.4, GY - .1, .42, .6), (.05, GY + .05, .7, .35), (-.8, GY - .05, .3, .8)):
        fl = .9 + .07 * math.sin(TAU * (t * 2 + ph)) + .03 * math.sin(TAU * (t * 3 + ph * 2))
        G.glow(x, y + h * .5, .4, P["glow"], .35)
        fire_tongue(B, P, x, y, 0, 1, h * fl, .03 * math.sin(TAU * (t + ph)), .13 + .05 * h, ph=ph * TAU)
    for i in range(6):
        k = (t + (i * .41) % 1) % 1
        x = math.sin(i * 2.1) * .7 + .12 * math.sin(TAU * k + i)
        B.blob(x, GY + .5 + 1.4 * k, .03, P["hi"], math.sin(math.pi * k), rough=0)


def bind_wrath(t, B, G, P, rng):
    """분노 결박: 발밑 갈라진 틈에서 진홍 불길이 나선으로 휘감아 오르며 조이고, 머리 위에 분노 표시와 함께 불꽃 가시가 터진다."""
    paths = crack_paths(rng, 7, .8, 1.5)
    emb = [(rng.uniform(-.2, .2), rng.uniform(-1.6, 1.6), rng.uniform(.6, 2.2), rng.random()) for _ in range(14)]
    G.glow(0, BC, 1.6, P["glow"], .5 * bump(t, .2, 1))
    fade = 1 - seg(t, .6, .95)
    B.blob(0, GY, .35 + .85 * eo(seg(t, 0, .25)), mixc(P["deep"], INK, .5), .85 * fade, sy=PY, rough=.15, seed=2)
    draw_cracks(B, G, P, paths, eo(seg(t, 0, .2)), fade, fade)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .07, col="light")
    grow = eo(seg(t, 0, .35), 2)
    sq = eo(seg(t, .3, .55))
    for a0, pts, ws in spiral_strands(t, 6, grow, sq, fade, .2, 3.4, 2.1, phase=t * 2):
        B.ribbon(pts, ws, P["deep"], fade)
        B.ribbon(pts, [w * .6 for w in ws], P["mid"], fade)
        B.ribbon(pts, [w * .25 for w in ws], P["hi"], .85 * fade)
        for k in (5, 11, 17):
            if k < len(pts) and grow > .3:
                x, y = pts[k]
                fire_tongue(B, P, x, y, 0, 1, .35 * fade * grow, .05 * math.sin(TAU * t * 3 + k), .1, ph=k)
    s = bump(t, .5, .9)
    if s > .03:
        for i in range(10):
            a = i * TAU / 10 + .2
            ln = (.9 + .5 * (i % 2)) * s
            x0, y0 = math.cos(a) * .15, BC + math.sin(a) * .15
            tongue(B, x0, y0, math.cos(a), math.sin(a), ln, .12 * (1 if i % 2 else -1), .18 * s, P["deep"])
            tongue(B, x0, y0, math.cos(a), math.sin(a), ln * .7, .08 * (1 if i % 2 else -1), .08 * s, P["light"])
    anger_mark(B, P, .1, BC + 1.05, .48 * pop(t, .42, .6) * (1 - seg(t, .82, 1)))
    burst_drops(B, G, P, t, emb, .45, 0, BC + .2, 1, 1, ("light", "hi"), .04, 3, floor=GY)


# ================================================================ 나태 sloth — 회청 졸음 구름, 초승달, Zzz

def slam_sloth(t, B, G, P, rng):
    """졸음 짓누르기: 무거운 회청 구름이 내려앉아 바닥을 짓누르고, 납작하게 퍼지는 안개 물결 위로 Zzz 가 느릿느릿 오른다."""
    puffs = [(rng.uniform(0, TAU), rng.uniform(.7, 1.0), rng.random()) for _ in range(12)]
    down = eo(seg(t, 0, .18), 2)
    squash = eo(seg(t, .14, .3))
    fade = 1 - seg(t, .7, 1)
    cy = 1.3 - (1.3 - (GY + .4)) * down
    flat = 1 - .45 * squash
    sc = .9 + .45 * squash
    G.glow(0, GY + .3, 1.7, P["glow"], .4 * bump(t, .2, 1))
    k = eo(seg(t, .15, .7))
    on = seg(t, .14, .2)
    for i, (a, r, ph) in enumerate(sorted(puffs, key=lambda v: -math.sin(v[0]))):   # 바닥으로 퍼지는 안개 덩이
        rr = (.4 + 1.25 * k) * r
        x, y = math.cos(a) * rr, GY + math.sin(a) * rr * PY
        B.blob(x, y + .08, (.28 + .2 * k) * (1 - .4 * seg(t, .6, 1)), P["mid"] if i % 2 else P["light"], fade * on, sy=.55, rough=.2, seed=i)
    ground_ring(B, G, P, seg(t, .14, .6), .4, 1.85, .07, col="light")
    ca = 1 - seg(t, .45, .75)
    if ca > .01:
        cloud(B, P, 0, cy, .95 * sc, ca, seed=1, flat=flat)
        sleepy_eyes(B, P, 0, cy + .05 * flat, sc, ca, flat)
    for j in range(3):
        k2 = seg(t, .3 + j * .1, .85 + j * .05)
        if 0 < k2 < 1:
            x = .35 + j * .3 + .12 * math.sin(TAU * k2 + j)
            y = GY + .7 + j * .25 + 1.0 * k2
            zz(B, P, x, y, (.22 + .1 * j) * (.6 + .4 * min(1, k2 * 3)), 1 - seg(k2, .6, 1), tilt=.2 + .1 * j)
    for i in range(6):     # 졸음 김 (외곽 없이)
        k3 = seg(t, .2 + i * .04, 1)
        if 0 < k3 < 1:
            a = i * TAU / 6 + k3
            B.blob(math.cos(a) * (.8 + .8 * k3), GY + .2 + .4 * k3 + math.sin(a) * .3, .3 + .3 * k3, P["hi"], .25 * (1 - k3), rough=.35, seed=i, soft=True)


def bullet_sloth(t, B, G, P, rng):
    G.glow(0, 0, 1.3, P["glow"], .4)
    for i in range(5):
        k = (t + i / 5) % 1
        B.blob(-.5 - 1.3 * k, math.sin(i * 2.1) * .3 * k, .3 * (1 - k) + .08, P["light"] if i % 2 else P["mid"], (1 - k) * .9, rough=.3, seed=i)
    for i in range(2):
        k = (t + i / 2) % 1
        zz(B, P, -.7 - 1.0 * k, .5 + .3 * k, .3 * (1 - k * .5), 1 - k, tilt=.3)
    wob = .05 * math.sin(TAU * t)
    cloud(B, P, .1, 0, .78, 1, seed=3, flat=1 - wob)
    sleepy_eyes(B, P, .12, .02, .8, 1, 1 - wob)


def proc_sloth(t, B, G, P, rng):
    """졸음 발동: 회청 초승달이 툭 떠오르고, 둘레로 안개 고리가 퍼지며 Zzz 가 사방으로 흩어진다."""
    s = pop(t, 0, .25) * (1 - seg(t, .6, 1))
    G.glow(0, 0, 1.6 * s + .2, P["glow"], .5 * bump(t, .12, 1))
    ring = seg(t, 0, .7)
    if 0 < ring < 1:
        for i in range(12):
            a = i * TAU / 12 + ring * .6
            r = .45 + 1.25 * eo(ring)
            B.blob(math.cos(a) * r, math.sin(a) * r * .8, .22 * (1 - ring) + .06, P["mid"] if i % 2 else P["light"], (1 - ring) ** .8, rough=.25, seed=i)
    if s > .02:            # 초승달 (가운데가 두꺼운 호)
        B.arc(0, 0, .75 * s, math.pi * .35, math.pi * 1.65, .42 * s, P["deep"], 1, n=30)
        B.arc(-.04 * s, 0, .72 * s, math.pi * .42, math.pi * 1.58, .3 * s, P["light"], 1, n=30)
        B.arc(-.08 * s, .05 * s, .7 * s, math.pi * .6, math.pi * 1.15, .1 * s, P["hi"], 1, n=20)
        B.arc(-.5 * s, .02 * s, .1 * s, math.pi * 1.15, math.pi * 1.85, .035 * s + .005, P["deep"], 1, n=8, taper=False)
    for j in range(6):
        k = seg(t, .1 + j * .04, .8 + j * .03)
        if 0 < k < 1:
            a = math.pi * (.1 + .8 * j / 5)
            d = .55 + 1.2 * eo(k)
            zz(B, P, math.cos(a) * d + .15, math.sin(a) * d * .9 - .1, .24 * (1 - .4 * k) * min(1, k * 5), 1 - seg(k, .6, 1), tilt=.3 * math.sin(j * 1.7))
    for i in range(8):
        k = seg(t, .15, .9)
        if 0 < k < 1:
            a = i * TAU / 8 + .4
            d = .6 + 1.0 * eo(k)
            sparkle(B, math.cos(a) * d, math.sin(a) * d, .07 * math.sin(math.pi * k) + .01, P["hi"], 1, rot=k * 2)


def decor_sloth(t, B, G, P, rng):
    """졸음 베개: 바닥에 낡은 회청 베개가 놓여 있고, 둘레에 졸음 안개가 낮게 깔려 느릿느릿 흐르며 Zzz 가 천천히 떠오른다."""
    G.glow(0, GY + .2, 1.5, P["glow"], .22, sy=PY)
    for i in range(6):     # 뒤쪽 안개
        k = (t * .5 + i / 6) % 1
        B.blob(-1.7 + 3.4 * k, GY + .25 + .08 * math.sin(i * 1.3), .42, P["hi"], .24 * math.sin(math.pi * k), rough=.3, seed=i, soft=True)
    B.blob(0, GY - .02, 1.05, mixc(P["deep"], INK, .4), .55, sy=PY * .6, rough=.2, seed=4)   # 그림자
    B.poly(ellipse_pts(0, GY + .24, .95, .34, 32), P["deep"])
    B.poly(ellipse_pts(-.03, GY + .29, .86, .27, 32), P["mid"])
    B.poly(ellipse_pts(-.14, GY + .35, .58, .13, 24), P["light"])
    for sx in (-1, 1):     # 베개 모서리 술
        B.blade(sx * .88, GY + .26, sx * 1.08, GY + .08, .07, P["deep"], bias=.3)
        B.blade(sx * .88, GY + .26, sx * 1.06, GY + .44, .07, P["deep"], bias=.3)
    B.blob(.02, GY + .31, .06, P["hi"], rough=0, seg=12)
    B.blade(-.3, GY + .3, .32, GY + .28, .012, P["deep"], .8, bias=.5)   # 꿰맨 자국
    for i in range(4):     # 앞쪽 안개
        k = (t * .5 + i / 4 + .12) % 1
        B.blob(-1.7 + 3.4 * k, GY - .12 + .05 * math.sin(i * 2.1), .38, P["hi"], .22 * math.sin(math.pi * k), rough=.3, seed=i + 7, soft=True)
    for j in range(3):
        k = (t + j / 3) % 1
        x = .3 + .3 * k + .1 * math.sin(TAU * k + j)
        y = GY + .7 + 1.5 * k
        zz(B, P, x, y, .14 + .14 * k, math.sin(math.pi * k) ** .6, tilt=.25)


def bind_sloth(t, B, G, P, rng):
    """나태 결박: 졸음 안개 띠가 적을 칭칭 감아 오르며 조이고, 머리 위로 큰 Z 가 툭 튀어나온 뒤 안개가 풀려 퍼진다."""
    G.glow(0, BC, 1.5, P["glow"], .4 * bump(t, .2, 1))
    grow = eo(seg(t, 0, .3), 2)
    fade = 1 - seg(t, .65, 1)
    sq = 1 - .3 * eo(seg(t, .25, .5))
    B.blob(0, GY, .4 + .8 * eo(seg(t, 0, .3)), P["deep"], .6 * fade, sy=PY, rough=.2, seed=4)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .06, col="light")
    for half in (0, 1):
        for k in range(6):
            y = GY + .15 + k * .32 * grow
            r = (.45 + .4 * math.sin(math.pi * (k + .5) / 6)) * sq
            a0 = TAU * t * .8 + k * .9 + half * math.pi
            col = P["mid"] if half == 0 else (P["light"] if k % 2 else P["hi"])
            B.arc(0, y, r, a0, a0 + math.pi * 1.05, (.1 + .015 * k) * fade, col, fade, sy=.34, n=18)
    for j in range(3):     # 띠를 타고 오르는 작은 Z
        k = seg(t, .1 + j * .08, .6 + j * .08)
        if 0 < k < 1:
            a = j * 2.1 + k * 4
            zz(B, P, math.cos(a) * .7 * sq, GY + .3 + 1.6 * k, .16, math.sin(math.pi * k), tilt=.3)
    zk = pop(t, .4, .58) * (1 - seg(t, .82, 1))
    if zk > .02:
        zz(B, P, .2, BC + 1.05, .5 * zk, 1, tilt=.2)
        zz(B, P, .7, BC + 1.5, .3 * zk, 1, tilt=.3)
    for i in range(8):     # 풀려 퍼지는 안개
        k = seg(t, .55, 1)
        if 0 < k < 1:
            a = i * TAU / 8
            B.blob(math.cos(a) * (.5 + 1.1 * k), BC + math.sin(a) * (.4 + .6 * k), .3 + .25 * k, P["hi"], .25 * (1 - k), rough=.3, seed=i, soft=True)


# ================================================================ 교만 pride — 금관과 보랏빛 후광 (눈부심)

def slam_pride(t, B, G, P, rng):
    """교만의 강림: 발밑에서 보랏빛 햇살이 부채처럼 솟고, 그 위에 금관이 번쩍 내려앉으며 후광 고리가 퍼진다."""
    tw = [(rng.uniform(-1.6, 1.6), rng.uniform(0, .4), rng.uniform(.1, .3), rng.random()) for _ in range(14)]
    s = bump(t, .12, .8)
    cy = GY + .95
    G.glow(0, cy, 2.0 * s + .3, P["glow"], .6 * s)
    G.glow(0, GY, 1.7, P["glow"], .4 * s, sy=PY)
    for k, d in enumerate((0, .12)):
        ground_ring(B, G, P, seg(t, d, d + .55), .3, 1.85, .07, col="hi" if k else "light")
    if s > .02:            # 부채꼴 햇살
        for i in range(11):
            a = math.pi * (.04 + .92 * i / 10)
            ln = (1.5 + .55 * (i % 2 == 0)) * s
            B.blade(0, GY + .1, math.cos(a) * ln * 1.15, GY + .1 + math.sin(a) * ln, (.15 if i % 2 == 0 else .1) * s, P["mid"], bias=.12)
            B.blade(0, GY + .1, math.cos(a) * ln, GY + .1 + math.sin(a) * ln * .88, (.06 if i % 2 == 0 else .04) * s, P["hi"], bias=.12)
    c = pop(t, .1, .3) * (1 - seg(t, .7, .95))
    if c > .02:
        B.blob(0, cy + .1, .55 * c, P["deep"], sy=.8, rough=.06)
        crown(B, 0, cy - .2 * c, .55 * c, .62 * c)
        hy = cy + .85 * c
        G.glow(0, hy, .9 * c, P["glow"], .4, sy=.4)
        B.arc(0, hy, .72 * c, 0, TAU, .09 * c, P["light"], 1, sy=.3, n=40, taper=False)
        B.arc(0, hy, .72 * c, math.pi * 1.15, math.pi * 1.85, .035 * c, P["hi"], 1, sy=.3, n=16)
    for x, d, r, ph in tw:  # 떠오르는 반짝임
        k = seg(t, .1 + d, 1)
        if 0 < k < 1:
            sparkle(B, x, GY + .2 + 1.7 * eo(k, 2), r * math.sin(math.pi * k) + .01, P["hi"], 1, rot=k * 2 + ph)


def bullet_pride(t, B, G, P, rng):
    G.glow(0, 0, 1.4, P["glow"], .5)
    for i in range(6):
        k = (t + i / 6) % 1
        sparkle(B, -.5 - 1.3 * k, math.sin(i * 2.1) * .45 * k, .15 * (1 - k) + .02, P["hi"], 1 - k, rot=i)
    rot = TAU * t / 4
    for k in range(8):
        a = rot + k * TAU / 8
        ln = 1.05 if k % 2 == 0 else .7
        B.blade(0, 0, math.cos(a) * ln, math.sin(a) * ln, .17 if k % 2 == 0 else .1, P["light"], bias=.12)
    B.arc(0, 0, .68, 0, math.pi, .07, GOLD["mid"], 1, sy=.38, n=24, taper=False)        # 금 고리 뒤쪽
    B.blob(0, 0, .46, P["deep"], rough=.06)
    B.blob(-.02, .03, .36, P["mid"], rough=.06)
    B.blob(-.1, .12, .14, P["hi"], rough=.05)
    B.arc(0, 0, .68, math.pi, TAU, .08, GOLD["light"], 1, sy=.38, n=24, taper=False)    # 금 고리 앞쪽
    B.arc(0, 0, .68, math.pi * 1.3, math.pi * 1.6, .03, GOLD["hi"], 1, sy=.38, n=8)


def proc_pride(t, B, G, P, rng):
    """교만 발동: 보랏빛 햇살 열두 갈래가 번쩍 펼쳐지고, 가운데 금관이 튀어나오며 후광 고리가 겹겹이 퍼진다."""
    s = pop(t, 0, .2) * (1 - seg(t, .55, 1))
    G.glow(0, 0, 1.9 * s + .2, P["glow"], .6 * bump(t, .1, 1))
    for i in range(12):
        a = .3 * t + i * TAU / 12
        ln = (1.75 if i % 2 == 0 else 1.1) * s
        x0, y0 = math.cos(a) * .15, math.sin(a) * .15
        B.blade(x0, y0, math.cos(a) * ln, math.sin(a) * ln, (.2 if i % 2 == 0 else .12) * s, P["mid"], bias=.15)
        B.blade(x0, y0, math.cos(a) * ln * .8, math.sin(a) * ln * .8, (.08 if i % 2 == 0 else .05) * s, P["hi"], bias=.15)
    for k in range(2):
        r = seg(t, .05 + k * .12, .65 + k * .12)
        if 0 < r < 1:
            B.arc(0, 0, .4 + 1.4 * eo(r), 0, TAU, .07 * (1 - r) + .01, P["light"] if k == 0 else P["hi"], 1 - r, n=44, taper=False)
    c = pop(t, .06, .24) * (1 - seg(t, .62, .9))
    if c > .02:
        B.blob(0, .02, .55 * c, P["deep"], rough=.06)
        crown(B, 0, -.3 * c, .5 * c, .55 * c)
    k = seg(t, .12, .9)
    if 0 < k < 1:
        for i in range(8):
            a = i * TAU / 8 + .2
            d = .6 + 1.1 * eo(k)
            sparkle(B, math.cos(a) * d, math.sin(a) * d, .1 * math.sin(math.pi * k) + .01, P["hi"], 1, rot=k * 3)


def decor_pride(t, B, G, P, rng):
    """왕관 받침: 보랏빛 벨벳 방석 위에 금관이 놓여 있고, 그 위로 후광 고리가 숨 쉬듯 빛나며 반짝임이 천천히 떠오른다."""
    br = .5 + .5 * math.sin(TAU * t)
    G.glow(0, GY + .9, 1.4, P["glow"], .22 + .1 * br)
    G.glow(0, GY, 1.4, P["glow"], .2, sy=PY)
    B.blob(0, GY - .02, 1.05, mixc(P["deep"], INK, .45), .55, sy=PY * .6, rough=.2, seed=4)
    B.poly(ellipse_pts(0, GY + .16, .95, .32, 32), P["deep"])
    B.poly(ellipse_pts(0, GY + .22, .88, .26, 32), P["mid"])
    B.poly(ellipse_pts(-.12, GY + .29, .58, .11, 24), P["light"])
    for sx in (-1, 1):     # 금술
        B.blade(sx * .84, GY + .2, sx * .96, GY - .12, .06, GOLD["mid"], bias=.2)
        B.blob(sx * .96, GY - .14, .07, GOLD["light"], rough=.1)
    crown(B, 0, GY + .3, .48, .55)
    hy = GY + 1.3 + .04 * math.sin(TAU * t)
    G.glow(0, hy, .8, P["glow"], .25 + .15 * br, sy=.5)
    B.arc(0, hy, .55, 0, TAU, .055 + .015 * br, P["light"], .9, sy=.3, n=40, taper=False)
    B.arc(0, hy, .55, math.pi * 1.15, math.pi * 1.85, .025, P["hi"], .9, sy=.3, n=16)
    for i in range(7):
        k = (t + i / 7) % 1
        x = math.sin(i * 2.3) * .9 + .1 * math.sin(TAU * k + i)
        y = GY + .5 + 1.6 * k
        sparkle(B, x, y, .07 * math.sin(math.pi * k) + .005, P["hi"], 1, rot=i)


def bind_pride(t, B, G, P, rng):
    """교만 결박: 둘레에 보랏빛 빛기둥이 솟고, 후광 고리 셋이 위에서 내려와 적을 옭아맨 뒤 머리 위에 금관이 번쩍 씌워진다."""
    G.glow(0, BC, 1.7, P["glow"], .5 * bump(t, .25, 1))
    fade = 1 - seg(t, .7, 1)
    for k, d in enumerate((0, .1)):
        ground_ring(B, G, P, seg(t, d, d + .55), .3, 1.85, .07, col="hi" if k else "light")
    for i in range(9):     # 빛기둥
        ang = i * TAU / 9 + .2
        r = 1.05 + .2 * math.sin(i * 2.1)
        g = bump(t, .12 + i * .015, .85)
        if g < .03:
            continue
        x, y = math.cos(ang) * r, GY + math.sin(ang) * r * PY
        B.blade(x, y - .05, x, y + (.7 + .4 * abs(math.sin(i * 1.3))) * g, .07 * g + .01, P["light"], bias=.15)
    for j in range(3):     # 내려오는 후광 고리
        k = seg(t, j * .08, .35 + j * .08)
        if k <= 0:
            continue
        y = 2.0 - (2.0 - (GY + .5 + j * .55)) * eo(k)
        r = 1.15 - .5 * eo(k)
        G.arc(0, y, r, 0, TAU, .2, P["glow"], .35 * fade, sy=.3, n=40, taper=False, edge=0)
        B.arc(0, y, r, 0, TAU, .085 * fade + .01, P["light"] if j % 2 else P["mid"], fade, sy=.3, n=40, taper=False)
        B.arc(0, y, r, math.pi * 1.2, math.pi * 1.8, .03, P["hi"], fade, sy=.3, n=14)
    c = pop(t, .42, .6) * (1 - seg(t, .85, 1))
    if c > .02:
        for i in range(10):
            a = i * TAU / 10 + .3
            ln = (.8 + .4 * (i % 2)) * c
            B.blade(0, BC + .9, math.cos(a) * ln, BC + .9 + math.sin(a) * ln, .08 * c, P["light"], bias=.15)
        crown(B, 0, BC + .68, .42 * c, .48 * c)


# ================================================================ 탐욕 greed — 금화와 금실

def slam_greed(t, B, G, P, rng):
    """탐욕의 분수: 금화 더미가 번쩍 터지며 금화가 빙글 돌며 솟았다 쏟아지고, 금실 채찍이 바닥을 휘감아 퍼진다."""
    coins = [(rng.uniform(0, .12), rng.uniform(-.4, .4), rng.uniform(-1.6, 1.6), rng.uniform(2.2, 3.8), rng.uniform(0, TAU), rng.uniform(6, 14)) for _ in range(18)]
    whips = [(i * TAU / 6 + rng.uniform(-.3, .3), rng.choice((-1, 1)), rng.uniform(.9, 1.6)) for i in range(6)]
    tw = [(rng.uniform(-1.4, 1.4), rng.uniform(0, 1.6), rng.uniform(.15, .5)) for _ in range(8)]
    fade = 1 - seg(t, .72, 1)
    G.glow(0, GY + .5, 1.7, P["glow"], .5 * bump(t, .1, 1))
    B.blob(0, GY, .4 + .9 * eo(seg(t, 0, .25)), P["deep"], .8 * fade, sy=PY, rough=.16, seed=4)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .07, col="light")
    wk = eo(seg(t, .02, .35))
    for a, side, ln in whips:  # 바닥을 휘감는 금실 채찍
        n = 14
        pts = []
        for k in range(n + 1):
            u = k / n * wk
            ang = a + side * .6 * u
            r = .2 + ln * u
            pts.append((math.cos(ang) * r, GY + math.sin(ang) * r * PY + .05 * math.sin(u * 9 + t * 10)))
        ws = [.06 * (1 - k / n) + .012 for k in range(n + 1)]
        B.ribbon(pts, ws, P["deep"], fade)
        B.ribbon(pts, [w * .5 for w in ws], P["light"], fade)
        B.ribbon(pts, [w * .18 for w in ws], P["hi"], fade)
        G.ribbon(pts, [w * 3 for w in ws], P["glow"], .3 * fade, edge=0)
    pk = pop(t, 0, .15) * (1 - seg(t, .35, .6))
    if pk > .02:           # 터지는 금화 더미
        for i, (dx, dy) in enumerate(((-.3, 0), (.3, 0), (0, .12), (-.15, .25), (.15, .22), (0, .4))):
            coin(B, dx * pk * 1.2, GY + .05 + dy * pk, .2 * pk, .3 + i, P)
    for i, (d, x0, vx, vy, sp0, spin) in enumerate(coins):
        s = seg(t, d, 1) * .95
        if s <= 0:
            continue
        x, y, _, _ = ballistic(x0, GY + .3, vx, vy, s, 3.4)
        if y < GY - .1:
            continue
        a = 1 - seg(s, .7, .95)
        G.glow(x, y, .25, P["glow"], .25 * a)
        coin(B, x, y, .1 + .03 * (i % 3), sp0 + spin * s, P, a)
    for x, y, d in tw:
        k = seg(t, d * .5, d * .5 + .4)
        if 0 < k < 1:
            sparkle(B, x, GY + .3 + y, .12 * math.sin(math.pi * k) + .01, P["hi"], 1, rot=x)


def bullet_greed(t, B, G, P, rng):
    G.glow(0, 0, 1.3, P["glow"], .45)
    for ph in (0, .5):     # 금실 꼬리 두 가닥
        pts = [(-.2 - 1.7 * u, .22 * math.sin(TAU * (u * 1.2 - t) + ph * TAU) * u) for u in (j / 14 for j in range(15))]
        ws = [.07 * (1 - j / 14) + .01 for j in range(15)]
        B.ribbon(pts, ws, P["deep"])
        B.ribbon(pts, [w * .45 for w in ws], P["hi"])
    for i in range(3):
        k = (t + i / 3) % 1
        coin(B, -.6 - 1.2 * k, math.sin(i * 2.3) * .35 * k, .12 * (1 - k) + .04, TAU * k * 2 + i, P, 1 - k)
    coin(B, .12, 0, .62, TAU * t, P)
    sparkle(B, -.08, .3, .14 + .06 * math.sin(TAU * t * 2), P["hi"], 1, rot=.4)


def proc_greed(t, B, G, P, rng):
    """탐욕 발동: 금실 올가미가 적을 휘감아 조이고, 금화가 사방으로 빙글빙글 튀며 가운데 반짝임이 번쩍인다."""
    s = pop(t, 0, .2) * (1 - seg(t, .55, 1))
    G.glow(0, 0, 1.7 * s + .2, P["glow"], .55 * bump(t, .1, 1))
    lk = seg(t, 0, .45)
    la = 1 - seg(t, .45, .75)
    if la > 0:             # 조여 드는 올가미
        r = 1.5 - .9 * eo(lk)
        n = 44
        pts = [(math.cos(i * TAU / n + t * 3) * r * (1 + .05 * math.sin(i * 3 + t * 20)), math.sin(i * TAU / n + t * 3) * r * .85) for i in range(n + 1)]
        G.ribbon(pts, .2, P["glow"], .3 * la, edge=0)
        B.ribbon(pts, .075, P["deep"], la)
        B.ribbon(pts, .038, P["light"], la)
        B.ribbon(pts, .013, P["hi"], la)
        ex, ey = pts[0]
        B.ribbon([(ex, ey), (ex + .5 * la, ey - .7 * la)], .05, P["light"], la)   # 올가미 꼬리
    if s > .02:
        sparkle(B, 0, 0, 1.15 * s, P["light"])
        sparkle(B, 0, 0, .75 * s, P["hi"], rot=math.pi / 4)
        B.blob(0, 0, .22 * s, P["hi"], rough=.05)
    k = seg(t, .08, .9)
    if 0 < k < 1:
        for i in range(12):
            a = i * TAU / 12 + .2 * math.sin(i * 2.3)
            d = (.3 + 1.5 * eo(k)) * (.75 + .25 * math.sin(i * 1.7))
            coin(B, math.cos(a) * d, math.sin(a) * d - .4 * k * k, .15 * (1 - .3 * k), i + k * 12, P, 1 - seg(k, .65, 1))


def decor_greed(t, B, G, P, rng):
    """금화 더미: 바닥에 쌓인 금화 더미에서 반짝임이 번갈아 빛나고, 맨 위 금화 한 닢이 가끔 튀어 올라 빙글 돌다 내려앉는다."""
    G.glow(0, GY + .3, 1.3, P["glow"], .22)
    B.blob(0, GY - .02, 1.05, mixc(P["deep"], INK, .35), .65, sy=PY * .6, rough=.25, seed=3)
    for x, y in ((-.95, -.12), (.9, .12), (.6, -.3), (-.55, .28), (1.15, -.18)):   # 흩어진 금화
        flat_coin(B, x, GY + y, .14, P)
    stacks = [(-.55, .02, 4), (.5, -.05, 3), (-.08, .1, 5), (.18, -.14, 2)]
    for x, y, n in sorted(stacks, key=lambda s: -s[1]):
        for k in range(n):
            cy = GY + y + k * .075
            B.poly(ellipse_pts(x, cy, .22, .09, 20), P["deep"])
            B.poly(ellipse_pts(x, cy + .028, .2, .075, 20), P["light"] if k == n - 1 else P["mid"])
    top = (-.08, GY + .1 + 5 * .075)
    u = seg(t, 0, .55)
    if 0 < u < 1:          # 튀어 오르는 금화
        coin(B, top[0] + .1 * math.sin(math.pi * u), top[1] + .05 + 1.1 * math.sin(math.pi * u), .13, TAU * 2 * u, P)
    else:
        B.poly(ellipse_pts(top[0], top[1], .22, .09, 20), P["deep"])
        B.poly(ellipse_pts(top[0], top[1] + .028, .2, .075, 20), P["light"])
    for i, (x, y) in enumerate(((-.55, .4), (.5, .22), (-.1, .55), (.9, .2), (-.9, -.05))):
        tw = max(0.0, math.sin(TAU * (t + i / 5))) ** 4
        if tw > .05:
            sparkle(B, x, GY + y, .13 * tw + .02, P["hi"], tw, rot=i)


def bind_greed(t, B, G, P, rng):
    """탐욕 결박: 금실 여섯 가닥이 나선으로 감겨 올라 적을 꽁꽁 묶고, 머리 위에서 금화가 터져 쏟아진다."""
    coins = [(rng.uniform(-.2, .2), rng.uniform(-1.6, 1.6), rng.uniform(.8, 2.2), rng.uniform(0, TAU)) for _ in range(12)]
    G.glow(0, BC, 1.6, P["glow"], .45 * bump(t, .2, 1))
    fade = 1 - seg(t, .6, .95)
    B.blob(0, GY, .35 + .8 * eo(seg(t, 0, .25)), P["deep"], .75 * fade, sy=PY, rough=.15, seed=2)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .06, col="light")
    grow = eo(seg(t, 0, .35), 2)
    sq = eo(seg(t, .3, .55))
    for a0, pts, ws in spiral_strands(t, 6, grow, sq, fade, .08, 3.6, 2.0):
        G.ribbon(pts, [w * 3 for w in ws], P["glow"], .25 * fade, edge=0)
        B.ribbon(pts, ws, P["deep"], fade)
        B.ribbon(pts, [w * .5 for w in ws], P["light"], fade)
        B.ribbon(pts, [w * .18 for w in ws], P["hi"], fade)
    s = bump(t, .45, .85)
    if s > .03:
        sparkle(B, 0, BC + .7, 1.0 * s, P["light"])
        sparkle(B, 0, BC + .7, .6 * s, P["hi"], rot=math.pi / 4)
    for i, (dx, vx, vy, sp) in enumerate(coins):
        k = seg(t, .45, 1) * .85
        if k <= 0:
            continue
        x, y, _, _ = ballistic(dx, BC + .7, vx, vy, k, 3.4)
        if y < GY - .1:
            continue
        coin(B, x, y, .12, sp + k * 14, P, 1 - seg(k, .6, .85))


# ================================================================ 식탐 gluttony — 주황 아가리와 부스러기

def slam_gluttony(t, B, G, P, rng):
    """식탐의 한 입: 바닥이 쩍 벌어지며 거대한 주황 아가리가 솟아 콱 다물고, 부스러기와 침방울이 사방으로 튄다."""
    crumbs = [(rng.uniform(-.3, .3), rng.uniform(-1.8, 1.8), rng.uniform(1.2, 3.0), rng.random()) for _ in range(20)]
    drool = [(rng.uniform(-.5, .5), rng.uniform(-.6, .6), rng.uniform(.3, 1.2), rng.random()) for _ in range(6)]
    fade = 1 - seg(t, .72, 1)
    G.glow(0, GY + .6, 1.7, P["glow"], .45 * bump(t, .3, 1))
    B.blob(0, GY, .4 + 1.0 * eo(seg(t, 0, .25)), mixc(P["deep"], INK, .45), .85 * fade, sy=PY, rough=.15, seed=8)
    ground_ring(B, G, P, seg(t, .28, .75), .4, 1.85, .08, col="light")
    rise = eo(seg(t, 0, .2), 2) * (1 - seg(t, .55, .85))
    gap = .95 * (1 - eo(seg(t, .22, .34), 4))
    if rise > .02:
        jaws(B, P, 0, GY + .95 * rise, 1.05 * rise, .6 * rise, gap * rise)
    fl = bump(t, .34, .55)
    if fl > .02:
        G.glow(0, GY + .95, 1.4 * fl + .3, P["glow"], .5 * fl)
        B.blob(0, GY + .95, .5 * fl, P["hi"], fl, sy=.5, rough=.35, seed=2)
    for i, (dx, vx, vy, r) in enumerate(crumbs):
        s = seg(t, .32, 1) * .85
        if s <= 0:
            continue
        x, y, _, _ = ballistic(dx, GY + .95, vx, vy, s, 3.2)
        if y < GY - .15:
            continue
        crumb(B, x, y, .05 + .04 * r, (CRUMB, CRUMB_L, P["light"])[i % 3], 1 - seg(s, .6, .9), seed=i + s * 6)
    burst_drops(B, G, P, t, drool, .34, 0, GY + .9, 1.6, 1.2, ("hi",), .045, 3)


def bullet_gluttony(t, B, G, P, rng):
    G.glow(0, 0, 1.3, P["glow"], .45)
    for i in range(6):
        k = (t + i / 6) % 1
        crumb(B, -.5 - 1.4 * k, math.sin(i * 2.3) * .45 * k, .09 * (1 - k * .5), (CRUMB, CRUMB_L)[i % 2], 1 - k, seed=i + k * 3)
    gap = .5 * abs(math.sin(TAU * t))
    jaws(B, P, .1, 0, .68, .42, gap, fangs=True)


def proc_gluttony(t, B, G, P, rng):
    """식탐 발동: 위아래 주황 이빨 아가리가 쩍 벌렸다가 콱 물고, 문 자리에서 부스러기와 침방울이 사방으로 터진다."""
    s = pop(t, 0, .16) * (1 - seg(t, .55, .8))
    gap = 1.0 * (1 - eo(seg(t, .18, .3), 4))
    G.glow(0, 0, 1.6 * s + .2, P["glow"], .5 * bump(t, .3, 1))
    if s > .02:
        jaws(B, P, 0, 0, .95 * s, .55 * s, gap * s)
    ring = seg(t, .3, .8)
    if 0 < ring < 1:
        B.arc(0, 0, .5 + 1.3 * eo(ring), 0, TAU, .07 * (1 - ring) + .01, P["light"], 1 - ring, n=40, taper=False)
    fl = bump(t, .3, .55)
    if fl > .02:
        for i in range(8):     # 콱 무는 순간의 충격선
            a = i * TAU / 8 + .2
            B.blade(math.cos(a) * .5, math.sin(a) * .5, math.cos(a) * (.5 + .9 * fl), math.sin(a) * (.5 + .9 * fl), .07 * fl, P["hi"], bias=.3)
    k = seg(t, .3, 1)
    if 0 < k < 1:
        for i in range(16):
            a = i * TAU / 16 + math.sin(i * 2.9) * .2
            d = (.3 + 1.4 * eo(k)) * (.7 + .3 * math.sin(i * 1.3))
            if i % 4 == 0:
                drop(B, math.cos(a) * d, math.sin(a) * d - .3 * k * k, math.cos(a), math.sin(a), .05 * (1 - k) + .015, P["hi"], 1 - k, .12)
            else:
                crumb(B, math.cos(a) * d, math.sin(a) * d - .3 * k * k, .07 * (1 - .4 * k), (CRUMB, CRUMB_L, P["light"])[i % 3], 1 - seg(k, .6, 1), seed=i + k * 4)


def decor_gluttony(t, B, G, P, rng):
    """넘치는 죽 그릇: 금 간 그릇에 주황 죽이 넘쳐 보글보글 끓어 거품이 터지고, 김이 오르며 둘레에 부스러기가 흩어져 있다."""
    G.glow(0, GY + .5, 1.3, P["glow"], .22)
    B.blob(0, GY - .02, 1.1, mixc(BOWL, INK, .5), .6, sy=PY * .55, rough=.2, seed=3)
    for i, (x, y, r) in enumerate(((-1.0, -.1, .07), (.95, .05, .06), (.7, -.25, .05), (-.7, -.28, .06), (1.2, -.2, .04), (-1.25, .08, .05), (.35, -.38, .045))):
        crumb(B, x, GY + y, r, (CRUMB, CRUMB_L, P["light"])[i % 3], seed=i * 1.7)
    cy = GY + .62
    bowl = [(math.cos(a) * .82, cy + math.sin(a) * .62) for a in (math.pi + math.pi * i / 20 for i in range(21))]
    B.poly(bowl, BOWL)
    B.poly([(math.cos(a) * .74, cy - .04 + math.sin(a) * .5) for a in (math.pi + math.pi * i / 20 for i in range(21))], BOWL_L)
    B.blade(-.35, cy - .55, -.2, cy - .15, .018, INK, .9, bias=.4)       # 금
    B.blade(-.2, cy - .15, -.28, cy - .02, .014, INK, .9, bias=.4)
    B.poly(ellipse_pts(0, cy, .82, .2, 32), P["deep"])
    B.poly(ellipse_pts(0, cy + .02, .74, .16, 32), P["mid"])
    B.blob(-.25, cy + .06, .25, P["light"], sy=.35, rough=.2, seed=5)
    for x, ln in ((-.62, .32), (.55, .4), (.1, .22)):   # 넘쳐 흐른 죽
        B.blade(x, cy - .02, x + .03, cy - .02 - ln, .07, P["mid"], bias=.2)
        B.blob(x + .03, cy - .02 - ln, .055, P["mid"], rough=.1)
    for i in range(6):     # 보글보글 거품
        k = (t + i / 6 + .07 * i) % 1
        x = math.sin(i * 2.7) * .5
        y = cy + .03 + math.cos(i * 1.9) * .07
        if k < .8:
            rr = .03 + .07 * (k / .8)
            B.blob(x, y + rr * .5, rr, P["light"], rough=.05, seed=i)
            B.blob(x + rr * .35, y + rr * .85, rr * .3, P["hi"], rough=0)
        else:
            kk = seg(k, .8, 1)
            B.arc(x, y + .04, .08 + .1 * kk, 0, TAU, .02 * (1 - kk) + .005, P["hi"], 1 - kk, sy=PY, n=16, taper=False)
    for i in range(4):     # 김
        k = (t + i / 4) % 1
        B.blob((i - 1.5) * .3 + .15 * math.sin(TAU * k + i), cy + .2 + 1.3 * k, .16 + .2 * k, P["hi"], .22 * math.sin(math.pi * k), rough=.3, seed=i, soft=True)


def bind_gluttony(t, B, G, P, rng):
    """식탐 결박: 적 발밑에서 이빨 고리가 솟고 위에서 이빨 고리가 내려와 덥석 물어 조이고, 부스러기와 침방울이 터진다."""
    crumbs = [(rng.uniform(-.3, .3), rng.uniform(-1.7, 1.7), rng.uniform(.6, 2.2), rng.random()) for _ in range(18)]
    G.glow(0, BC, 1.6, P["glow"], .45 * bump(t, .3, 1))
    fade = 1 - seg(t, .62, .9)
    B.blob(0, GY, .4 + .8 * eo(seg(t, 0, .25)), mixc(P["deep"], INK, .4), .8 * fade, sy=PY, rough=.15, seed=2)
    ground_ring(B, G, P, seg(t, .3, .75), .3, 1.8, .07, col="light")
    rise = eo(seg(t, 0, .22), 2)
    close = eo(seg(t, .2, .34), 4)
    R = .95 - .2 * close
    n = 12
    lower_y = GY + .1
    upper_y = 2.1 - (2.1 - (BC + .55)) * rise
    teeth = []
    for half in ("low", "up"):
        for i in range(n):
            a = i * TAU / n
            teeth.append((math.sin(a), half, a))
    for depth, half, a in sorted(teeth, reverse=True):
        x = math.cos(a) * R
        if half == "low":
            by = lower_y + math.sin(a) * R * PY
            th = (.55 + .7 * close) * rise
            tip = by + th
        else:
            by = upper_y + math.sin(a) * R * PY
            th = (.4 + .45 * close) * rise
            tip = by - th
        if rise < .02 or fade <= 0:
            continue
        col = TOOTH if depth < 0 else mixc(TOOTH, INK, .25)
        tw = .13 * rise
        B.poly([(x - tw, by), (x + tw, by), (x, tip)], col, fade)
    if rise > .02 and fade > 0:
        for y, w in ((lower_y, .14), (upper_y, .12)):
            B.arc(0, y, R, math.pi, TAU, w * rise, GUM, fade, sy=PY, n=30, taper=False)   # 앞쪽 잇몸
            B.arc(0, y, R, math.pi * 1.15, math.pi * 1.85, w * .35 * rise, P["light"], fade, sy=PY, n=16)
    fl = bump(t, .34, .55)
    if fl > .02:
        G.glow(0, BC, 1.2 * fl + .3, P["glow"], .5 * fl)
        for i in range(8):
            a = i * TAU / 8 + .3
            B.blade(math.cos(a) * .4, BC + math.sin(a) * .4, math.cos(a) * (.4 + .9 * fl), BC + math.sin(a) * (.4 + .9 * fl), .07 * fl, P["hi"], bias=.3)
    for i, (dx, vx, vy, r) in enumerate(crumbs):
        s = seg(t, .34, 1) * .8
        if s <= 0:
            continue
        x, y, vx2, vy2 = ballistic(dx, BC, vx, vy, s, 3.2)
        if y < GY - .1:
            continue
        if i % 5 == 0:
            drop(B, x, y, vx2, vy2, .045, P["hi"], 1 - seg(s, .55, .8))
        else:
            crumb(B, x, y, .05 + .04 * r, (CRUMB, CRUMB_L, P["light"])[i % 3], 1 - seg(s, .55, .85), seed=i + s * 5)


# ================================================================ 색욕 lust — 분홍 하트와 장미 꽃잎

def slam_lust(t, B, G, P, rng):
    """매혹의 손짓: 바닥에 하트 고리가 퍼지고, 분홍 실이 바깥에서 가운데로 휘감겨 끌려 들며 장미 꽃잎이 소용돌이친다."""
    petals = [(rng.uniform(0, TAU), rng.uniform(.8, 1.8), rng.random(), rng.uniform(.09, .14)) for _ in range(16)]
    fade = 1 - seg(t, .72, 1)
    G.glow(0, GY + .5, 1.7, P["glow"], .45 * bump(t, .15, 1))
    B.blob(0, GY, .4 + .9 * eo(seg(t, 0, .3)), P["deep"], .7 * fade, sy=PY, rough=.16, seed=5)
    for j in range(2):     # 바닥의 하트 고리
        k = seg(t, j * .14, j * .14 + .6)
        if 0 < k < 1:
            s = .45 + 1.25 * eo(k)
            heart_ring(B, 0, GY + .05 * s, s, .065 * (1 - k) + .012, P["light"] if j else P["mid"], (1 - k) ** .7, sy=PY)
            heart_ring(G, 0, GY + .05 * s, s, .22 * (1 - k), P["glow"], .3 * (1 - k), sy=PY)
    k = seg(t, .04, .6)
    if 0 < k < 1:          # 바깥에서 가운데로 감겨 드는 실
        for i in range(7):
            a0 = i * TAU / 7 + t * 2
            rk = 1.9 - 1.3 * eo(k)
            pts = []
            for m in range(15):
                u = m / 14
                ang = a0 + u * 1.3
                r = rk * (1 - .6 * u) + .15
                pts.append((math.cos(ang) * r, GY + .1 + math.sin(ang) * r * PY))
            ws = [.025 + .04 * (m / 14) for m in range(15)]
            al = math.sin(math.pi * k) ** .5
            B.ribbon(pts, ws, P["mid"], al)
            B.ribbon(pts, [w * .4 for w in ws], P["hi"], al)
    c = pop(t, .08, .26) * (1 - seg(t, .62, .85))
    if c > .02:
        beat = 1 + .1 * math.sin(TAU * t * 3)
        G.glow(0, GY + .95, 1.0 * c, P["glow"], .45)
        heart(B, 0, GY + .95, .55 * c * beat, P)
    for a, r0, ph, size in petals:   # 안쪽으로 소용돌이치는 꽃잎
        k2 = seg(t, ph * .2, .75 + ph * .2)
        if 0 < k2 < 1:
            ang = a + k2 * 3
            r = r0 * (1 - .65 * eo(k2))
            petal(B, math.cos(ang) * r, GY + .15 + .7 * k2 * ph + math.sin(ang) * r * PY, ang * 2 + ph * 6, size, P, 1 - seg(k2, .7, 1))


def bullet_lust(t, B, G, P, rng):
    G.glow(0, 0, 1.3, P["glow"], .45)
    for i in range(5):
        k = (t + i / 5) % 1
        petal(B, -.5 - 1.3 * k, math.sin(i * 2.3) * .42 * k, k * 5 + i, .15 * (1 - k * .4), P, 1 - k)
    for i in range(2):
        k = (t + i / 2 + .25) % 1
        heart(B, -.7 - 1.1 * k, (.35 if i else -.35) * k, .16 * (1 - k * .5), P, 1 - k, rot=-math.pi / 2)
    beat = 1 + .1 * math.sin(TAU * t * 2)
    heart(B, .2, 0, .62 * beat, P, rot=-math.pi / 2)


def proc_lust(t, B, G, P, rng):
    """매혹 발동: 분홍 하트가 두근 부풀었다 터지며 장미 꽃잎이 흩날리고, 작은 하트들이 둥글게 퍼진다."""
    beat = pop(t, 0, .18) * (1 + .15 * bump(seg(t, .18, .36), .5, 1))
    hs = .95 * beat * (1 - seg(t, .38, .5))
    G.glow(0, 0, 1.7 * beat + .2, P["glow"], .55 * bump(t, .12, 1))
    for k2 in range(2):
        r = seg(t, .05 + k2 * .15, .6 + k2 * .15)
        if 0 < r < 1:
            heart_ring(B, 0, -.05, .5 + 1.2 * eo(r), .06 * (1 - r) + .01, P["light"] if k2 else P["hi"], 1 - r)
    if hs > .02:
        heart(B, 0, 0, hs, P)
    k = seg(t, .05, .85)
    if 0 < k < 1:
        for i in range(8):
            a = i * TAU / 8 + .2
            d = .55 + 1.25 * eo(k)
            heart(B, math.cos(a) * d, math.sin(a) * d, .17 * (1 - .5 * k) * min(1, k * 6), P, 1 - seg(k, .6, 1), rot=.3 * math.sin(i))
    k = seg(t, .4, 1)
    if 0 < k < 1:
        for i in range(14):
            a = i * TAU / 14 + math.sin(i * 1.7) * .3
            d = (.2 + 1.6 * eo(k)) * (.7 + .3 * math.sin(i * 2.3))
            petal(B, math.cos(a) * d, math.sin(a) * d - .4 * k * k, a + k * 6, .14 * (1 - .3 * k), P, 1 - seg(k, .6, 1))


def decor_lust(t, B, G, P, rng):
    """장미 한 송이: 바닥에 떨어진 분홍 장미 둘레로 꽃잎이 흩어져 있고, 꽃잎이 한 장씩 천천히 떨어져 내려앉으며 작은 하트가 떠오른다."""
    G.glow(0, GY + .35, 1.2, P["glow"], .2)
    B.blob(0, GY - .02, .9, mixc(P["deep"], INK, .5), .5, sy=PY * .6, rough=.2, seed=3)
    for x, y, a in ((-.9, -.1, .4), (.85, .05, 2.2), (.55, -.3, 1.1), (-.5, -.32, 2.8), (1.15, -.2, .2), (-1.2, .1, 1.6)):
        petal(B, x, GY + y, a, .11, P)
    for ang, ln in ((2.6, .55), (.5, .6), (-.4, .4)):   # 잎과 줄기
        x1, y1 = math.cos(ang) * ln, GY + .2 + math.sin(ang) * ln * .5
        B.blade(0, GY + .2, x1, y1, .025, LEAF_G, bias=.5)
        leaf(B, x1, y1, ang, .2, LEAF_G, LEAF_H)
    cy = GY + .4
    B.blob(0, cy, .42, P["deep"], sy=.85, rough=.12, seed=2)
    for i in range(5):     # 바깥 꽃잎
        a = i * TAU / 5 + .3
        B.blob(math.cos(a) * .2, cy + math.sin(a) * .16, .22, P["mid"], rough=.15, seed=i)
    B.blob(0, cy + .04, .24, P["light"], sy=.85, rough=.15, seed=7)
    B.arc(0, cy + .06, .16, .5, 4.6, .035, P["mid"], 1, sy=.8, n=18)          # 안쪽 소용돌이
    B.arc(.02, cy + .07, .08, 2.2, 6.0, .025, P["deep"], 1, sy=.8, n=12)
    B.blob(-.12, cy + .18, .05, P["hi"], rough=0, seg=10)
    for j, (x0, ph) in enumerate(((.55, 0), (-.45, .5))):   # 떨어지는 꽃잎
        k = (t + ph) % 1
        u = seg(k, 0, .75)
        if u < 1:
            y = 1.8 - (1.8 - (GY + .05)) * u
            petal(B, x0 + .25 * math.sin(TAU * u * 1.5 + j), y, TAU * u * 1.2 + j, .11, P, min(1, u * 8))
        else:
            petal(B, x0 + .25 * math.sin(TAU * 1.5 + j), GY + .05, TAU * 1.2 + j, .11, P, 1 - seg(k, .85, 1))
    for j in range(2):     # 떠오르는 하트
        k = (t + j / 2 + .2) % 1
        heart(B, (-.3 if j else .35) + .1 * math.sin(TAU * k), GY + .8 + 1.2 * k, .1, P, math.sin(math.pi * k) ** .7)


def bind_lust(t, B, G, P, rng):
    """색욕 결박: 분홍 리본 실이 감겨 올라 적 둘레에 하트를 그리며 조이고, 하트가 두근 뛰다 꽃잎으로 터져 흩날린다."""
    G.glow(0, BC, 1.6, P["glow"], .45 * bump(t, .25, 1))
    fade = 1 - seg(t, .6, .9)
    B.blob(0, GY, .35 + .8 * eo(seg(t, 0, .25)), P["deep"], .7 * fade, sy=PY, rough=.15, seed=2)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .06, col="light")
    grow = eo(seg(t, 0, .3), 2)
    sq = eo(seg(t, .3, .5))
    for a0, pts, ws in spiral_strands(t, 4, grow, sq, fade, .07, 3.0, 1.8):
        B.ribbon(pts, ws, P["mid"], fade)
        B.ribbon(pts, [w * .4 for w in ws], P["hi"], fade)
    hk = eo(seg(t, .1, .38))
    s = 1.05 * (1 - .22 * sq)
    beat = 1 + .08 * math.sin(TAU * t * 4) * seg(t, .38, .5)
    if fade > 0 and hk > .01:
        heart_ring(G, 0, BC - .05, s * beat, .25, P["glow"], .35 * fade, f=hk)
        heart_ring(B, 0, BC - .05, s * beat, .085, P["deep"], fade, f=hk)
        heart_ring(B, 0, BC - .05, s * beat, .04, P["light"], fade, f=hk)
    c = pop(t, .38, .5) * (1 - seg(t, .55, .65))
    if c > .02:
        heart(B, 0, BC + 1.0, .3 * c, P)
    k = seg(t, .55, 1)
    if 0 < k < 1:
        for i in range(16):
            a = i * TAU / 16 + math.sin(i * 1.3) * .2
            d = (.5 + 1.2 * eo(k)) * (.75 + .25 * math.sin(i * 2.1))
            petal(B, math.cos(a) * d, BC + math.sin(a) * d * .8 - .5 * k * k, a + k * 5, .13, P, 1 - seg(k, .6, 1))
        for i in range(5):
            a = i * TAU / 5 + .5
            heart(B, math.cos(a) * (.4 + 1.0 * k), BC + math.sin(a) * (.4 + .8 * k), .13 * (1 - .4 * k), P, 1 - seg(k, .5, 1))


# ================================================================ 시기 envy — 초록 눈과 가시 독 덩굴

def slam_envy(t, B, G, P, rng):
    """시기의 덩굴: 바닥에 초록 눈이 번쩍 뜨이고, 둘레에서 가시 독 덩굴이 채찍처럼 솟구쳐 휘감기며 독방울이 튄다."""
    vines = [(i * TAU / 7 + rng.uniform(-.2, .2), rng.uniform(.65, 1.05), rng.uniform(1.2, 1.9), rng.choice((-1, 1)), rng.uniform(0, .1)) for i in range(7)]
    drops = [(rng.uniform(-.3, .3), rng.uniform(-1.8, 1.8), rng.uniform(1.2, 3.0), rng.random()) for _ in range(18)]
    fade = 1 - seg(t, .72, 1)
    G.glow(0, GY + .5, 1.7, P["glow"], .45 * bump(t, .15, 1))
    B.blob(0, GY, .4 + .95 * eo(seg(t, 0, .3)), P["deep"], .9 * fade, sy=PY, rough=.15, seed=6)
    B.blob(.05, GY + .02, .3 + .75 * eo(seg(t, 0, .3)), P["mid"], .8 * fade, sy=PY, rough=.2, seed=7)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .07, col="light")
    op = eo(seg(t, .04, .18)) * (1 - seg(t, .6, .8))
    if op > .01:
        eye(B, 0, GY + .02, .62, op, P, look=.5 * math.sin(t * 9), sy=PY * 1.1, slit=1 - .5 * seg(t, .2, .4))
    for depth, ang, r, h, side, d in sorted(((math.sin(v[0]),) + v for v in vines), reverse=True):
        grow = eo(seg(t, d, d + .25), 2) * (1 - seg(t, .62, .92))
        if grow <= .02:
            continue
        bx, by = math.cos(ang) * r, GY + math.sin(ang) * r * PY
        pts = vine_path(bx, by, math.pi / 2 - side * .15 + math.cos(ang) * .35, h, side * 1.7, grow)
        vine(B, P, pts, .11 * (.5 + .5 * grow), 1, thorns=5)
    burst_drops(B, G, P, t, drops, .12, 0, GY + .4, 1, 1, ("light", "hi"), .05, 3.6, floor=GY - .15)


def bullet_envy(t, B, G, P, rng):
    G.glow(0, 0, 1.3, P["glow"], .45)
    for j in range(3):     # 뒤로 끌리는 덩굴 손
        pts = [(-.3 - 1.6 * u, (j - 1) * .3 * u + .15 * math.sin(TAU * (u - t) + j * 2)) for u in (k / 12 for k in range(13))]
        vine(B, P, pts, .08, 1, thorns=3)
    for i in range(3):
        k = (t + i / 3) % 1
        drop(B, -.5 - 1.2 * k, -.3 - .3 * k + math.sin(i * 2) * .2, -1, -.6, .08 * (1 - k) + .03, P["light"], 1 - k)
    B.blob(.15, 0, .62, P["deep"], rough=.08, seed=2)
    B.blob(.12, .03, .52, P["mid"], rough=.08, seed=3)
    eye(B, .18, 0, .5, .95, P, look=.9)


def pool_envy(t, B, G, P, rng):
    """시기의 늪: 초록 독 웅덩이 가장자리에 가시 덩굴이 얽혀 있고, 웅덩이 속 눈 둘이 번갈아 떴다 감기며 거품이 끓는다."""
    bub = [(rng.random(), rng.uniform(0, TAU), math.sqrt(rng.random()) * 1.15, rng.random()) for _ in range(9)]
    G.glow(0, PG + .2, 1.9, P["glow"], .35)
    B.blob(0, PG, 1.75, P["deep"], .92, sy=PY, rough=.14, seed=4)
    B.blob(-.05, PG + .03, 1.4, P["mid"], .9, sy=PY, rough=.18, seed=5)
    B.blob(-.45, PG + .25, .5, P["light"], .5, sy=PY * .5, rough=.3, seed=6)
    for i in range(5):     # 가장자리 덩굴
        a = i * TAU / 5 + .4
        bx, by = math.cos(a) * 1.55, PG + math.sin(a) * 1.55 * PY
        pts = vine_path(bx, by, a + math.pi * .55, .9, 1.4 * (1 if i % 2 else -1), 1, n=12)
        vine(B, P, [(x, PG + (y - PG) * .7) for x, y in pts], .07, 1, thorns=3, leaves=1, sy=.7)
    eye(B, -.5, PG + .05, .38, blink(t, .35, .09), P, look=.6 * math.sin(TAU * t), sy=PY * 1.2)
    eye(B, .55, PG - .12, .3, blink(t, .8, .09), P, look=-.6 * math.sin(TAU * t + 1), sy=PY * 1.2)
    for ph, ang, r, big in bub:
        k = (t + ph) % 1
        x, y = math.cos(ang) * r, PG + math.sin(ang) * r * PY
        if abs(x + .5) < .4 and abs(y - PG) < .25 or abs(x - .55) < .35 and abs(y - PG + .12) < .22:
            continue
        if k < .82:
            rr = .03 + (.08 + .06 * big) * (k / .82)
            B.blob(x, y + rr * .5, rr, P["light"], rough=.05)
            B.blob(x + rr * .35, y + rr * .85, rr * .3, P["hi"], rough=0)
        else:
            kk = seg(k, .82, 1)
            B.arc(x, y + .06, .1 + .15 * kk, 0, TAU, .022 * (1 - kk) + .005, P["hi"], 1 - kk, sy=PY, n=18, taper=False)
    for i in range(5):
        k = (t + i / 5) % 1
        B.blob((i - 2) * .5 + .2 * math.sin(TAU * k + i), PG + .2 + 1.8 * k, .18 + .22 * k, P["light"], .26 * math.sin(math.pi * k), rough=.3, seed=i, soft=True)


def proc_envy(t, B, G, P, rng):
    """시기 발동: 커다란 초록 눈이 번쩍 떠서 노려보고(동공이 가늘어짐), 둘레로 가시 덩굴이 사방으로 뻗으며 독방울이 튄다."""
    s = pop(t, 0, .2) * (1 - seg(t, .6, .9))
    op = eo(seg(t, 0, .16)) * (1 - seg(t, .55, .8))
    G.glow(0, 0, 1.7 * s + .2, P["glow"], .55 * bump(t, .12, 1))
    grow = eo(seg(t, .04, .32), 2) * (1 - seg(t, .6, .95))
    if grow > .02:
        for i in range(8):
            a = i * TAU / 8 + .2
            side = 1 if i % 2 else -1
            pts = vine_path(math.cos(a) * .4, math.sin(a) * .4, a, 1.45, side * 1.3, grow, n=14)
            vine(B, P, pts, .1, 1, thorns=4)
    ring = seg(t, .05, .6)
    if 0 < ring < 1:
        B.arc(0, 0, .5 + 1.3 * eo(ring), 0, TAU, .06 * (1 - ring) + .01, P["light"], 1 - ring, n=40, taper=False)
    if s > .02 and op > .01:
        B.blob(0, 0, .62 * s, P["deep"], sy=.75, rough=.1, seed=2)
        eye(B, 0, 0, .78 * s, op, P, look=.3 * math.sin(t * 12), slit=1 - .6 * seg(t, .15, .35))
    k = seg(t, .2, 1)
    if 0 < k < 1:
        for i in range(12):
            a = i * TAU / 12 + .1 + math.sin(i * 2.1) * .2
            d = (.5 + 1.3 * eo(k)) * (.75 + .25 * math.sin(i * 1.4))
            drop(B, math.cos(a) * d, math.sin(a) * d - .3 * k * k, math.cos(a), math.sin(a), .05 * (1 - k) + .015, P["light"] if i % 3 else P["hi"], 1 - k, .12)


def decor_envy(t, B, G, P, rng):
    """엿보는 덤불: 바닥을 기는 가시 덩굴과 잎 사이에서 초록 눈 하나가 이리저리 엿보다 가끔 깜빡이고, 작은 독 거품이 오른다."""
    G.glow(0, GY + .3, 1.3, P["glow"], .22)
    B.blob(0, GY, 1.2, mixc(P["deep"], INK, .4), .75, sy=PY * .8, rough=.25, seed=3)
    B.blob(.05, GY + .03, .8, P["deep"], .8, sy=PY * .7, rough=.3, seed=4)
    for i, (x0, y0, ang, ln, curl) in enumerate(((-.2, -.05, 3.0, 1.2, -1.2), (.15, .02, .1, 1.3, 1.1), (-.1, .1, 2.2, .9, 1.5),
                                                 (.2, -.12, -.5, .9, -1.4), (0, .05, 1.2, .8, -1.6))):
        pts = vine_path(x0, GY + y0, ang, ln, curl, 1, n=14)
        vine(B, P, [(x, GY + (y - GY) * .55) for x, y in pts], .085, 1, thorns=4, leaves=2, sy=.55)
    for x, ang in ((-.55, 1.9), (.5, 1.2), (.05, 1.6)):   # 위로 솟은 덩굴 끝
        pts = vine_path(x, GY + .1, ang, .65, 1.2 if x < 0 else -1.2, 1, n=10)
        vine(B, P, pts, .06, 1, thorns=2)
    eye(B, 0, GY + .42, .36, blink(t, .5, .07), P, look=.85 * math.sin(TAU * t), sy=.9)
    for i in range(4):
        k = (t + i / 4 + .1 * i) % 1
        x = (i - 1.5) * .45 + .1 * math.sin(TAU * k + i)
        y = GY + .3 + 1.2 * k
        B.blob(x, y, .04 + .03 * (i % 2), P["light"], math.sin(math.pi * k), rough=.05, seed=i)
        B.blob(x + .015, y + .02, .012, P["hi"], math.sin(math.pi * k), rough=0)


def bind_envy(t, B, G, P, rng):
    """시기 결박: 가시 덩굴 다섯 가닥이 나선으로 휘감아 올라 적을 옥죄고, 머리 위에 초록 눈이 번쩍 떠 노려본 뒤 독액이 터진다."""
    drops = [(rng.uniform(-.3, .3), rng.uniform(-1.8, 1.8), rng.uniform(.4, 2.0), rng.random()) for _ in range(18)]
    G.glow(0, BC, 1.6, P["glow"], .45 * bump(t, .2, 1))
    fade = 1 - seg(t, .62, .95)
    B.blob(0, GY, .4 + .95 * eo(seg(t, 0, .3)), P["deep"], .9 * fade, sy=PY, rough=.14, seed=5)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .06, col="light")
    grow = eo(seg(t, 0, .35), 2)
    sq = eo(seg(t, .3, .55))
    for a0, pts, ws in spiral_strands(t, 5, grow, sq, fade, .12, 3.0, 2.0):
        if fade > 0:
            vine(B, P, pts, .12 * (.4 + .6 * fade), fade, thorns=6)
    op = eo(seg(t, .4, .52)) * (1 - seg(t, .78, .9))
    if op > .01:
        G.glow(0, BC + 1.0, .7, P["glow"], .4 * op)
        eye(B, 0, BC + 1.0, .42, op, P, look=0, slit=.5)
    burst_drops(B, G, P, t, drops, .5, 0, BC + .4, 1, 1, ("light", "hi", "mid"), .05, 3.4, floor=GY - .1)
