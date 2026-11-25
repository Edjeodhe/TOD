"""23차 일반 몬스터 공격 이펙트 (칠죄종). render_element_vfx.py 가 render_element_vfx_sins.py 다음에 exec 로 읽는다 (Layer, PAL, 도우미를 그대로 씀). 단독 실행 안 함.

시트 (그룹 'mob', Assets/Art/VFX/16/mob_<죄종>_<종류>.png + _glow):
  slash  스토커 근접 휘두름. 12칸 256px, 한 번, 가운데 피벗 = 적 몸 가운데. +x = 휘두르는 쪽(주인공 쪽).
         휘두른 호의 반지름 ≈ 칸 폭의 35~40% → 그리기 크기 ≈ 근접 거리 / .37.
  shot   슈터 탄. 8칸 128px, 반복, 가운데 피벗. +x = 날아가는 쪽, 꼬리는 −x. 몸통 반지름 ≈ 칸 폭의 13%.
         보스 탄(boss_<죄종>_bullet)과 겹치지 않는 작은 모양.
  dash   차저 돌진 꼬리. 8칸 256px, 반복, 가운데 피벗 = 적 몸 가운데. +x = 돌진 방향, 꼬리가 −x 칸 끝까지.
모양:
  분노 wrath    불꽃 발톱 세 줄 / 불꽃 창끝 / 불길 자국과 불씨
  나태 sloth    축 처진 무거운 회청 안개 호 / 늘어지는 안개 방울 / 가라앉는 안개 둑과 Z
  교만 pride    금테 두른 보랏빛 칼날과 햇살 부채 / 금테 보석 / 금테 벨벳 망토와 반짝임
  탐욕 greed    금박 조각이 흩날리는 호와 금화 / 빙글 도는 금화와 금박 / 쏟아지는 금화
  식탐 gluttony 앞에서 콱 무는 아가리 / 빙글 도는 닭다리 / 기름 얼룩과 부스러기
  색욕 lust     하트로 말리는 분홍 리본 채찍 / 하트 화살 / 분홍 리본 자락과 꽃잎
  시기 envy     가시 덩굴 채찍과 곁눈질 / 독 가시 / 끌리는 가시 덩굴과 독방울
"""

MEAT = dict(deep=lin(hexc("#4a2210")), mid=lin(hexc("#9a4e22")), light=lin(hexc("#d48444")), hi=lin(hexc("#f4c890")))


# ---------------------------------------------------------------- 공용
def arc_pts(r, a0, a1, n=22, cx=0.0, cy=0.0, sy=1.0, sag=0.0):
    """a0 → a1 호 위의 점 (sag: 호 가운데가 아래로 처지는 양)."""
    out = []
    for i in range(n + 1):
        u = i / n
        a = a0 + (a1 - a0) * u
        out.append((cx + math.cos(a) * r, cy + math.sin(a) * r * sy - sag * math.sin(math.pi * u)))
    return out


def crescent(L, pts, width, col, alpha=1.0, head=.72, edge=1.0):
    """초승달 칼자국: 꼬리(0번)는 가늘고 머리 쪽 head 지점이 가장 굵고 끝은 뾰족."""
    n = len(pts)
    if n < 2 or alpha <= .003:
        return
    ws = []
    for i in range(n):
        u = i / (n - 1)
        prof = (u / head) ** 1.4 if u < head else ((1 - u) / (1 - head)) ** .7
        ws.append(width * max(0.0, prof) + .004)
    L.ribbon(pts, ws, col, alpha, edge)


def swing(t, a0, a1, lead=.3, lag=.6, power=3):
    """휘두르기: (꼬리 각, 머리 각). 머리는 lead 까지 a0 → a1, 꼬리는 늦게 따라온다."""
    h = a0 + (a1 - a0) * eo(seg(t, 0, lead), power)
    tl = a0 + (a1 - a0) * .7 * eo(seg(t, lead * .35, lag), 2)   # 꼬리는 70% 까지만: 호가 한 점으로 뭉치지 않고 펼친 채 사라진다
    return tl, h


def flake(L, x, y, s, ang, col, alpha=1.0, sk=.55):
    """금박 조각: 비스듬한 마름모."""
    if s < .005 or alpha <= .003:
        return
    pts = [rotv(s, 0, ang), rotv(0, s * sk, ang), rotv(-s * .8, 0, ang), rotv(0, -s * sk * .7, ang)]
    L.poly([(x + px, y + py) for px, py in pts], col, alpha)


def drumstick(L, x, y, s, rot, alpha=1.0):
    """닭다리: 동그란 살 + 뼈 (뼈 끝 혹 둘). s = 살 반지름."""
    bx, by = rotv(-s * 2.1, 0, rot)
    mx, my = rotv(-s * .5, 0, rot)
    L.blade(x + mx, y + my, x + bx, y + by, s * .34, TOOTH, alpha, n=6, bias=.5)
    L.ribbon([(x + mx, y + my), (x + bx, y + by)], s * .2, TOOTH, alpha)
    for side in (-1, 1):
        kx, ky = rotv(-s * 2.15, side * s * .22, rot)
        L.blob(x + kx, y + ky, s * .26, TOOTH, alpha, rough=.05, seg=12)
    L.blob(x, y, s, MEAT["deep"], alpha, rough=.1, seed=rot)
    ox, oy = rotv(s * .05, s * .06, rot)
    L.blob(x + ox, y + oy, s * .82, MEAT["mid"], alpha, rough=.1, seed=rot + 1)
    ox, oy = rotv(-s * .1, s * .3, rot)
    L.blob(x + ox, y + oy, s * .42, MEAT["light"], alpha, rough=.1, seed=rot + 2)
    ox, oy = rotv(-s * .25, s * .45, rot)
    L.blob(x + ox, y + oy, s * .14, MEAT["hi"], alpha, rough=0, seg=10)


def gem(L, x, y, s, P, alpha=1.0, rot=0.0):
    """앞쪽(+x)이 뾰족한 보석: 짙은 외곽 → 면 → 하이라이트, 금테."""
    if s < .01 or alpha <= .003:
        return
    tf = lambda px, py: (x + rotv(px, py, rot)[0], y + rotv(px, py, rot)[1])
    outer = [tf(s * 1.25, 0), tf(s * .1, s * .78), tf(-s * .75, s * .5), tf(-s * .95, 0), tf(-s * .75, -s * .5), tf(s * .1, -s * .78)]
    rim = [tf(px * 1.16, py * 1.16) for px, py in ((s * 1.25, 0), (s * .1, s * .78), (-s * .75, s * .5), (-s * .95, 0), (-s * .75, -s * .5), (s * .1, -s * .78))]
    L.poly(rim, GOLD["mid"], alpha)
    L.poly(outer, P["deep"], alpha)
    L.poly([tf(s * 1.1, 0), tf(s * .1, s * .66), tf(-s * .3, 0), tf(s * .1, -s * .66)], P["mid"], alpha)
    L.poly([tf(s * .1, s * .66), tf(-s * .65, s * .42), tf(-s * .3, 0)], P["light"], alpha)
    L.poly([tf(s * .75, s * .08), tf(s * .15, s * .46), tf(-s * .05, s * .12)], P["hi"], alpha)


def mob_glow_line(G, pts, width, col, alpha):
    G.ribbon(pts, [width] * len(pts), col, alpha, edge=0)


# ================================================================ slash — 스토커 근접 (가운데 피벗, +x 가 주인공 쪽)
A0, A1 = 1.25, -1.25    # 위에서 아래로 휘두름


def mobslash_wrath(t, B, G, P, rng):
    """불꽃 발톱: 진홍 발톱 자국 세 줄이 위에서 아래로 할퀴고, 자국마다 불길 혀가 솟고 불씨가 튄다."""
    emb = [(rng.uniform(-.9, .9), rng.uniform(.6, 1.4), rng.uniform(-.4, .9), rng.random()) for _ in range(9)]
    fade = 1 - seg(t, .55, 1)
    G.glow(.6, 0, 1.3, P["glow"], .5 * bump(t, .2, 1))
    for k, (r, d) in enumerate(((1.05, 0.0), (1.35, .05), (1.65, .1))):
        tl, h = swing(clamp(t - d), A0 + .1 * k, A1 - .1 * k, .26, .55)
        if h == tl:
            continue
        pts = arc_pts(r, tl, h, 22, cx=-.35)
        crescent(B, pts, .2, P["deep"], fade)
        crescent(B, pts, .13, P["mid"], fade)
        crescent(B, pts, .07, P["light"], fade)
        crescent(B, pts, .025, P["hi"], fade)
        mob_glow_line(G, pts, .35, P["glow"], .35 * fade)
        # 자국 위로 솟는 불길 혀 (호 바깥쪽)
        for j in range(3):
            u = .25 + .3 * j
            a = tl + (h - tl) * u
            fl = .32 * bump(seg(t, d + .08 + .05 * j, .9), .25, 1)
            fire_tongue(B, P, -.35 + math.cos(a) * r, math.sin(a) * r, math.cos(a), math.sin(a), fl, .08 * math.sin(t * 20 + j), .12, fade, ph=j)
    for x0, vx, vy, rr in emb:
        s = seg(t, .2, 1) * .8
        if s <= 0:
            continue
        x, y, vx2, vy2 = ballistic(1.1, x0 * .8, vx, vy * .6 + x0 * .4, s, 2.0)
        drop(B, x, y, vx2, vy2, .035 + .03 * rr, P["hi"] if rr > .5 else P["light"], 1 - seg(s, .45, .8))


def mobslash_sloth(t, B, G, P, rng):
    """나태: 무거운 회청 안개 덩이가 축 처진 호를 그리며 느릿느릿 내리치고, 안개가 남아 가라앉으며 Z 가 떠오른다."""
    puffs = [(rng.random(), rng.uniform(-.25, .25), rng.random()) for _ in range(12)]
    fade = 1 - seg(t, .6, 1)
    # 천천히 출발해 무겁게 내려앉는 휘두름
    k = seg(t, 0, .5)
    k = k * k * (3 - 2 * k)
    h = A0 + (A1 - A0) * k
    tl = A0 + (A1 - A0) * .7 * seg(t, .15, .65) ** 1.5
    G.glow(.6, -.3, 1.3, P["glow"], .45 * bump(t, .35, 1))
    if h != tl:
        pts = arc_pts(1.3, tl, h, 18, cx=-.3, sag=.35 * k)
        crescent(B, pts, .32, P["deep"], fade * .95, head=.6)
        crescent(B, pts, .24, P["mid"], fade * .95, head=.6)
        crescent(B, pts, .1, P["light"], fade * .8, head=.6)
        # 호를 따라 남는 안개 덩이 (아래로 가라앉음)
        for u, off, rr in puffs:
            a = tl + (h - tl) * u
            sink = .5 * seg(t, .3, 1) * (.5 + rr)
            x, y = -.3 + math.cos(a) * (1.3 + off), math.sin(a) * (1.3 + off) - .35 * k * math.sin(math.pi * u) - sink
            B.blob(x, y, (.13 + .1 * rr) * (1 + .6 * seg(t, .3, 1)), P["light"] if rr > .6 else P["mid"], fade * .75, rough=.3, seed=u * 9)
    # 내려친 끝 자리의 묵직한 안개 구름
    c = pop(t, .38, .58) * (1 - seg(t, .7, 1))
    if c > .02:
        cloud(B, P, .5, -1.05, .55 * c, 1, seed=2, flat=.7)
        sleepy_eyes(B, P, .5, -1.05, .55 * c, 1, .7)
    zt = seg(t, .45, 1)
    if 0 < zt < 1:
        zz(B, P, .4 + .3 * zt, -.3 + .9 * zt, .28 + .1 * zt, 1 - zt ** 2, tilt=.25)


def mobslash_pride(t, B, G, P, rng):
    """교만: 금테 두른 날렵한 보랏빛 칼날이 휘둘리고, 끝에서 보랏빛 햇살 부채와 네 갈래 반짝임이 번쩍인다."""
    fade = 1 - seg(t, .5, .95)
    tl, h = swing(t, A0, A1, .22, .5, 4)
    G.glow(.6, 0, 1.3, P["glow"], .5 * bump(t, .2, 1))
    if h != tl:
        pts = arc_pts(1.45, tl, h, 24, cx=-.35)
        outer = arc_pts(1.6, tl, h, 24, cx=-.35)
        crescent(B, outer, .08, GOLD["deep"], fade, head=.8)
        crescent(B, outer, .04, GOLD["light"], fade, head=.8)
        crescent(B, pts, .17, P["deep"], fade, head=.8)
        crescent(B, pts, .1, P["mid"], fade, head=.8)
        crescent(B, pts, .04, P["hi"], fade, head=.8)
        mob_glow_line(G, pts, .3, P["glow"], .35 * fade)
    # 햇살 부채 (앞쪽으로 펼쳐짐)
    fan = seg(t, .18, .7)
    if 0 < fan < 1:
        for j in range(7):
            a = -.9 + 1.8 * j / 6
            ln = (.9 if j % 2 == 0 else .6) * eo(fan) + .1
            x0, y0 = .35 + math.cos(a) * .35, math.sin(a) * .35
            B.blade(x0, y0, x0 + math.cos(a) * ln, y0 + math.sin(a) * ln, .09 if j % 2 == 0 else .06, P["light"], 1 - fan, bias=.15)
            G.blade(x0, y0, x0 + math.cos(a) * ln * 1.2, y0 + math.sin(a) * ln * 1.2, .2, P["glow"], .35 * (1 - fan), bias=.15)
    for j, (x, y, at) in enumerate(((1.25, .75, .2), (1.55, -.2, .3), (1.05, -.95, .38))):
        s = bump(seg(t, at, at + .4), .3, 1)
        sparkle(B, x, y, .22 * s, GOLD["hi"] if j % 2 else P["hi"], s, rot=j * .5)
    c = pop(t, .25, .45) * (1 - seg(t, .7, .95))
    if c > .02:
        crown(B, .95, .25, .26 * c, .3 * c, 1)


def mobslash_greed(t, B, G, P, rng):
    """탐욕: 금박 조각을 흩뿌리며 금빛 호를 긋고, 휘두른 끝에서 금화가 빙글빙글 튀어나간다."""
    fl = [(rng.random(), rng.uniform(-.18, .18), rng.uniform(0, TAU), rng.random()) for _ in range(22)]
    coins = [(rng.uniform(-.8, .8), rng.uniform(.5, 1.2), rng.uniform(.3, 1.1), rng.random()) for _ in range(5)]
    fade = 1 - seg(t, .55, 1)
    tl, h = swing(t, A0, A1, .28, .58)
    G.glow(.6, 0, 1.3, P["glow"], .5 * bump(t, .25, 1))
    if h != tl:
        pts = arc_pts(1.35, tl, h, 22, cx=-.35)
        crescent(B, pts, .2, P["deep"], fade)
        crescent(B, pts, .13, P["mid"], fade)
        crescent(B, pts, .07, P["light"], fade)
        crescent(B, pts, .025, P["hi"], fade)
        mob_glow_line(G, pts, .32, P["glow"], .35 * fade)
        for u, off, ang, rr in fl:
            a = tl + (h - tl) * u
            drift = .35 * seg(t, .2, 1) * rr
            x = -.35 + math.cos(a) * (1.35 + off + drift)
            y = math.sin(a) * (1.35 + off + drift) - .3 * seg(t, .3, 1) * rr
            flake(B, x, y, .09 + .07 * rr, ang + t * 8 * (rr - .5), P["hi"] if rr > .6 else P["light"], fade)
    for x0, vx, vy, rr in coins:
        s = seg(t, .22, 1) * .85
        if s <= 0:
            continue
        x, y, _, _ = ballistic(1.0, x0 * .9, vx, vy * x0 * 1.2, s, 2.6)
        coin(B, x, y, .1 + .05 * rr, TAU * s * 3 + rr * 5, P, 1 - seg(s, .55, .85))


def mobslash_gluttony(t, B, G, P, rng):
    """식탐: 주인공 쪽 바로 앞에서 주황 아가리가 쩍 벌렸다 콱 물고, 부스러기와 침방울이 튄다."""
    cr = [(rng.uniform(-.3, .3), rng.uniform(-1, 1), rng.uniform(.2, 1.2), rng.random()) for _ in range(10)]
    s = pop(t, 0, .18) * (1 - seg(t, .62, .85))
    gap = 1.1 * (1 - eo(seg(t, .26, .36), 4)) + .05 * bump(seg(t, .36, .5), .3, 1)
    G.glow(.6, 0, 1.3, P["glow"], .5 * bump(t, .35, 1))
    if s > .02:
        jaws(B, P, .8, 0, 1.0 * s, .58 * s, gap * s)
    # 콱 물 때 퍼지는 충격 고리와 앞쪽 줄
    ring = seg(t, .34, .75)
    if 0 < ring < 1:
        B.arc(.8, 0, .5 + .6 * eo(ring), 0, TAU, .06 * (1 - ring) + .01, P["light"], 1 - ring, sy=.7, n=40, taper=False)
        for j in range(5):
            a = -.8 + 1.6 * j / 4
            x0 = .8 + math.cos(a) * (.7 + .3 * eo(ring))
            B.blade(x0, math.sin(a) * (.7 + .3 * eo(ring)), x0 + math.cos(a) * .3, math.sin(a) * (.7 + .3 * eo(ring)) + math.sin(a) * .3,
                    .05, P["hi"], 1 - ring, bias=.3)
    for dx, vx, vy, rr in cr:
        k = seg(t, .34, 1) * .8
        if k <= 0:
            continue
        x, y, vx2, vy2 = ballistic(.8 + dx, 0, vx * (1 if rr > .3 else -.6), vy * 1.2, k, 2.8)
        if rr > .55:
            crumb(B, x, y, .07 + .04 * rr, CRUMB if rr > .75 else CRUMB_L, 1 - seg(k, .5, .8), seed=rr * 9 + k * 6)
        else:
            drop(B, x, y, vx2, vy2, .045, P["hi"], 1 - seg(k, .5, .8))


def mobslash_lust(t, B, G, P, rng):
    """색욕: 분홍 리본 채찍이 호를 그리며 휘어 끝이 하트로 말리고, 꽃잎과 작은 하트가 흩날린다."""
    pet = [(rng.random(), rng.uniform(-.4, .4), rng.uniform(0, TAU), rng.random()) for _ in range(7)]
    fade = 1 - seg(t, .6, 1)
    tl, h = swing(t, A0, A1, .3, .62)
    G.glow(.6, 0, 1.3, P["glow"], .5 * bump(t, .3, 1))
    if h != tl:
        for ph, col, w in ((0, P["deep"], .07), (math.pi, P["mid"], .07), (0, P["light"], .03)):
            pts = []
            n = 24
            for i in range(n + 1):
                u = i / n
                a = tl + (h - tl) * u
                r = 1.35 + .1 * math.sin(u * 9 + ph + t * 6)
                pts.append((-.35 + math.cos(a) * r, math.sin(a) * r))
            ws = [w * (.3 + .7 * math.sin(math.pi * min(1, (i / n) * 1.2)) ** .5) + .008 for i in range(n + 1)]
            B.ribbon(pts, ws, col, fade)
        mob_glow_line(G, arc_pts(1.35, tl, h, 22, cx=-.35), .3, P["glow"], .3 * fade)
    # 채찍 끝이 하트로 말림
    hk = seg(t, .25, .55)
    if hk > 0:
        hx, hy = -.35 + math.cos(A1) * 1.35, math.sin(A1) * 1.35
        heart_ring(B, hx + .35, hy + .05, .35, .06, P["deep"], fade, f=eo(hk))
        heart_ring(B, hx + .35, hy + .05, .35, .025, P["hi"], fade, f=eo(hk))
    hb = pop(t, .3, .5) * (1 - seg(t, .6, .9))
    if hb > .02:
        heart(B, 1.15, .1, .42 * hb, P)
    for u, off, ang, rr in pet:
        k = seg(t, .2 + .2 * u, 1)
        if k <= 0:
            continue
        a = A0 + (A1 - A0) * u
        x = -.35 + math.cos(a) * (1.4 + off) + .5 * k
        y = math.sin(a) * (1.4 + off) - .6 * k * k + .2 * math.sin(k * 6 + rr * 5)
        if rr > .7:
            heart(B, x, y, .1, P, 1 - k)
        else:
            petal(B, x, y, ang + k * 4, .1, P, 1 - k)


def mobslash_envy(t, B, G, P, rng):
    """시기: 가시 독 덩굴이 채찍처럼 호를 그리며 휘감아 치고, 곁눈질하는 초록 눈이 번뜩이며 독방울이 튄다."""
    drops = [(rng.random(), rng.uniform(.3, 1.0), rng.uniform(-.2, .8), rng.random()) for _ in range(7)]
    fade = 1 - seg(t, .58, 1)
    tl, h = swing(t, A0, A1, .3, .62)
    G.glow(.6, 0, 1.3, P["glow"], .5 * bump(t, .3, 1))
    if h != tl:
        pts = []
        n = 18
        for i in range(n + 1):
            u = i / n
            a = tl + (h - tl) * u
            r = 1.3 + .12 * math.sin(u * 7 + t * 5)
            pts.append((-.3 + math.cos(a) * r, math.sin(a) * r))
        vine(B, P, pts, .13, fade, thorns=6, leaves=2)
        mob_glow_line(G, pts, .32, P["glow"], .35 * fade)
    # 곁눈질 눈 (휘두르는 동안 떠서 주인공 쪽을 흘겨봄)
    op = bump(seg(t, .05, .75), .3, 1)
    if op > .05:
        eye(B, .1, .05, .42, min(1, op * 1.3) * .75, P, look=.9, alpha=min(1, op * 2), slit=.6)
    for u, vx, vy, rr in drops:
        k = seg(t, .25 + .2 * u, 1) * .8
        if k <= 0:
            continue
        a = A0 + (A1 - A0) * u
        x, y, vx2, vy2 = ballistic(-.3 + math.cos(a) * 1.4, math.sin(a) * 1.4, vx, vy, k, 3.0)
        drop(B, x, y, vx2, vy2, .05 + .03 * rr, P["light"] if rr > .4 else P["hi"], 1 - seg(k, .5, .8))


# ================================================================ shot — 슈터 탄 (가운데 피벗, +x 진행, 128px)

def mobshot_wrath(t, B, G, P, rng):
    """불꽃 창끝: 앞이 뾰족한 진홍 불덩이, 뒤로 불길이 길게 날리고 불씨가 떨어진다."""
    G.glow(-.2, 0, 1.3, P["glow"], .5)
    for i in range(3):
        ph = i / 3
        fire_tongue(B, P, .15, (i - 1) * .14, -1, (i - 1) * .05, 1.2 + .45 * math.sin(TAU * (t + ph)), .25 * math.sin(TAU * (t + ph)), .42, ph=ph * 4)
    B.blade(-.4, 0, 1.15, 0, .5, P["deep"], bias=.35)
    B.blade(-.25, .02, .95, .01, .34, P["mid"], bias=.35)
    B.blade(-.05, .05, .75, .03, .18, P["light"], bias=.4)
    B.blob(.25, .1, .08, P["hi"], rough=0, seg=10)
    for i in range(4):
        k = (t + i / 4) % 1
        B.blob(-.7 - 1.1 * k, math.sin(i * 2.4) * .35 * k - .2 * k * k, .06 * (1 - k) + .02, P["hi"] if i % 2 else P["light"], 1 - k)


def mobshot_sloth(t, B, G, P, rng):
    """축 늘어진 안개 방울: 무겁게 처진 회청 방울이 출렁이고, 뒤로 안개 덩이가 가라앉으며 떨어진다."""
    G.glow(0, -.1, 1.25, P["glow"], .4)
    for i in range(4):
        k = (t + i / 4) % 1
        B.blob(-.55 - 1.2 * k, -.15 - .5 * k * k + math.sin(i * 2.2) * .15, .26 * (1 - k * .6) + .05, P["light"] if i % 2 else P["mid"], (1 - k) * .85, rough=.3, seed=i)
    wob = .08 * math.sin(TAU * t)
    # 아래로 처진 방울 (위는 둥글고 아래로 묵직하게 늘어짐)
    B.blob(.1, -.05, .62, P["deep"], sy=.85 + wob, rough=.12, seed=1)
    B.blob(.05, -.22, .5, P["deep"], sy=.8, rough=.1, seed=2)
    B.blob(.1, 0, .5, P["mid"], sy=.85 + wob, rough=.12, seed=3)
    B.blob(.02, .14, .26, P["light"], rough=.12, seed=4)
    B.blob(-.06, .26, .1, P["hi"], rough=0, seg=10)
    sleepy_eyes(B, P, .16, -.02, .7, 1, .9)
    zz(B, P, -.35, .62 + .08 * math.sin(TAU * t), .26, .9, tilt=.3)


def mobshot_pride(t, B, G, P, rng):
    """금테 보석: 앞이 뾰족한 보랏빛 보석이 금테를 두르고 날며, 뒤로 네 갈래 반짝임이 흩어진다."""
    G.glow(0, 0, 1.3, P["glow"], .5)
    for i in range(4):
        k = (t + i / 4) % 1
        sparkle(B, -.6 - 1.2 * k, math.sin(i * 2.5) * .4 * k, .2 * (1 - k) + .04, GOLD["hi"] if i % 2 else P["hi"], 1 - k, rot=i + k * 2)
    for j in (-1, 1):   # 뒤로 끌리는 금빛 두 줄
        pts = [(-.5 - 1.2 * u, j * (.18 + .15 * u)) for u in (k / 8 for k in range(9))]
        B.ribbon(pts, [.045 * (1 - k / 8) + .01 for k in range(9)], GOLD["light"], .9)
    gem(B, .15, 0, .58, P, 1, rot=.12 * math.sin(TAU * t))
    sparkle(B, .55, .35, .16 + .08 * math.sin(TAU * t * 2), P["hi"], 1, rot=.3)


def mobshot_greed(t, B, G, P, rng):
    """빙글 도는 금화: 금화가 옆으로 돌며 날고, 뒤로 금박 조각이 팔랑팔랑 흩어진다."""
    fl = [(rng.uniform(-.4, .4), rng.uniform(0, TAU), rng.random()) for _ in range(6)]
    G.glow(0, 0, 1.25, P["glow"], .45)
    for i, (oy, ang, rr) in enumerate(fl):
        k = (t + i / 6) % 1
        flake(B, -.5 - 1.3 * k, oy * k * 1.6 - .2 * k * k, .14 * (1 - k * .5), ang + TAU * k * (1 + rr), P["hi"] if i % 2 else P["light"], 1 - k)
    B.blade(-.35, 0, -1.5, 0, .14, P["glow"], .5, bias=.1)
    coin(B, .15, 0, .6, TAU * t, P)
    sparkle(B, .3, .42, .14 + .06 * math.sin(TAU * t * 2), P["hi"], 1, rot=.4)


def mobshot_gluttony(t, B, G, P, rng):
    """닭다리: 노릇한 닭다리가 빙글 돌며 날고, 뒤로 부스러기와 기름방울이 떨어진다."""
    G.glow(0, 0, 1.25, P["glow"], .45)
    for i in range(5):
        k = (t + i / 5) % 1
        if i % 2:
            drop(B, -.5 - 1.2 * k, math.sin(i * 2.3) * .3 - .5 * k * k, -1, -.6, .06 * (1 - k) + .03, P["light"], 1 - k)
        else:
            crumb(B, -.5 - 1.3 * k, math.sin(i * 2.3) * .35 * k - .3 * k * k, .1 * (1 - k * .5), (CRUMB, CRUMB_L)[i % 2], 1 - k, seed=i + k * 3)
    drumstick(B, .35, 0, .38, -TAU * t + .4)


def mobshot_lust(t, B, G, P, rng):
    """하트 화살: 끝이 하트인 분홍 화살, 뒤에 깃털 꽃잎, 날아온 자리에 작은 하트가 남는다."""
    G.glow(0, 0, 1.25, P["glow"], .45)
    for i in range(3):
        k = (t + i / 3) % 1
        heart(B, -.9 - .9 * k, math.sin(i * 2.4) * .3 * k + .1 * math.sin(TAU * (k + i)), .13 * (1 - k * .5), P, 1 - k, rot=-math.pi / 2)
    B.ribbon([(-1.25, 0), (.55, 0)], .055, P["deep"])
    B.ribbon([(-1.2, 0), (.5, 0)], .025, P["hi"])
    for j in (-1, 1):   # 깃털 꽃잎
        for m in range(2):
            x = -1.15 + m * .25
            petal(B, x - .12, j * .16, j * .75 + math.pi, .2, P, 1)
    flap = .06 * math.sin(TAU * t * 2)
    heart(B, .72, 0, .48 + flap, P, rot=-math.pi / 2)


def mobshot_envy(t, B, G, P, rng):
    """독 가시: 갈고리 가시가 달린 초록 가시 화살, 끝에서 독이 맺혀 뒤로 뚝뚝 떨어진다."""
    G.glow(0, 0, 1.25, P["glow"], .45)
    for i in range(4):
        k = (t + i / 4) % 1
        drop(B, -.4 - 1.2 * k, -.15 - .6 * k * k + math.sin(i * 2) * .15, -1, -.8, .08 * (1 - k) + .03, P["light"] if i % 2 else P["hi"], 1 - k)
    sway = .06 * math.sin(TAU * t)
    pts = [(-1.3 + 2.5 * u, sway * math.sin(u * 5) + .12 * u * u) for u in (k / 12 for k in range(13))]
    ws = [.08 + .22 * (k / 12) ** 1.5 for k in range(13)]
    ws[-1] = .02
    B.ribbon(pts, ws, P["deep"])
    B.ribbon(pts, [w * .5 for w in ws], P["mid"])
    B.blade(.6, .1, 1.35, .08, .2, P["deep"], bias=.1)          # 날 선 끝
    B.blade(.65, .12, 1.2, .1, .1, P["light"], bias=.1)
    for j, x in enumerate((-.8, -.3, .2)):                       # 갈고리 가시
        side = 1 if j % 2 else -1
        B.blade(x, side * .05, x - .3, side * .32, .07, P["deep"], bias=.15)
    leaf(B, -1.0, .25, 2.4, .2, P["mid"], P["hi"])
    B.blob(1.3, -.05, .07 + .02 * math.sin(TAU * t), P["hi"], rough=0, seg=12)


# ================================================================ dash — 차저 돌진 꼬리 (가운데 피벗 = 적 몸, +x 돌진, 꼬리 −x)

def mobdash_wrath(t, B, G, P, rng):
    """불길 자국: 몸 뒤로 진홍 불길 혀가 길게 휘날리고, 불씨가 흩어지며 앞에는 달아오른 가시 바람."""
    emb = [(rng.random(), rng.uniform(-.7, .7), rng.random()) for _ in range(8)]
    G.glow(-.75, 0, 1.15, P["glow"], .5)
    for i in range(5):
        ph = i / 5
        y0 = (i - 2) * .2
        ln = 1.4 + .5 * math.sin(TAU * (t + ph)) + (.3 if i == 2 else 0)
        fire_tongue(B, P, -.2, y0 * .7, -1, y0 * .25, ln, .3 * math.sin(TAU * (t + ph * 1.3)), .5 - .06 * abs(i - 2), ph=ph * 6)
    for u, oy, rr in emb:
        k = (t + u) % 1
        B.blob(-.6 - 1.4 * k, oy * (1 + k) + .3 * k * k, .06 * (1 - k) + .02, P["hi"] if rr > .5 else P["light"], 1 - k)
    for j in (-1, 1):   # 앞쪽 가시 바람
        B.arc(.2, 0, .95, j * .35, j * 1.25, .07, P["light"], .7, n=12)


def mobdash_sloth(t, B, G, P, rng):
    """가라앉는 안개 둑: 몸 뒤로 무거운 회청 안개 덩이가 굴러 늘어지며 바닥 쪽으로 처지고, Z 가 떠오른다."""
    G.glow(-.75, -.3, 1.15, P["glow"], .45)
    for i in range(7):
        k = (t + i / 7) % 1
        x = -.3 - 1.6 * k
        y = -.1 - .55 * k * k + .15 * math.sin(i * 2.7)
        r = .3 + .35 * k
        a = (1 - k) ** .8 * .95
        B.blob(x, y - .05, r, P["deep"], a, sy=.75, rough=.25, seed=i)
        B.blob(x + .03, y + .03, r * .82, P["mid"], a, sy=.75, rough=.25, seed=i + 3)
        B.blob(x - .05, y + .12, r * .4, P["light"], a * .9, sy=.75, rough=.2, seed=i + 5)
    for i in range(2):
        k = (t + i / 2) % 1
        zz(B, P, -.7 - .8 * k, .5 + .5 * k, .25 + .1 * k, (1 - k) * min(1, k * 4), tilt=.3)


def mobdash_pride(t, B, G, P, rng):
    """금테 벨벳 망토: 몸 뒤로 보랏빛 망토 자락이 물결치며 펄럭이고, 금테와 반짝임이 따라온다."""
    G.glow(-.75, 0, 1.15, P["glow"], .45)
    n = 16
    for layer, (col, wk) in enumerate(((P["deep"], 1.0), (P["mid"], .8), (P["light"], .35))):
        pts, ws = [], []
        for i in range(n + 1):
            u = i / n
            pts.append((-.2 - 1.75 * u, .12 * math.sin(TAU * (u * 1.3 - t)) * (u + .2) + .05 * layer))
            ws.append((.22 + .45 * u ** .8) * wk)
        B.ribbon(pts, ws, col)
        if layer == 0:
            for side in (-1, 1):    # 금테 두 줄
                edge = [(x - .02, y + side * w) for (x, y), w in zip(pts, ws)]
                B.ribbon(edge, [.05] * len(edge), GOLD["mid"])
                B.ribbon(edge, [.02] * len(edge), GOLD["hi"])
    for i in range(4):
        k = (t + i / 4) % 1
        sparkle(B, -.6 - 1.3 * k, math.sin(i * 2.6) * .7 * k, .18 * (1 - k) + .04, GOLD["hi"] if i % 2 else P["hi"], 1 - k, rot=i + k * 3)


def mobdash_greed(t, B, G, P, rng):
    """쏟아지는 금화: 몸 뒤로 금화가 줄줄이 쏟아져 빙글빙글 떨어지고, 금빛 줄과 금박이 따라온다."""
    cs = [(rng.random(), rng.uniform(-.45, .45), rng.random()) for _ in range(9)]
    G.glow(-.75, -.1, 1.15, P["glow"], .45)
    for j in (-.22, 0, .22):
        B.blade(-.2, j, -1.6 + abs(j) * 1.5, j * 1.4, .07, P["light"], .8, bias=.1)
    for u, oy, rr in cs:
        k = (t + u) % 1
        x = -.35 - 1.5 * k
        y = oy - 1.1 * k * k
        if rr > .35:
            coin(B, x, y, .14 + .06 * rr, TAU * (k * 2 + rr), P, 1 - k ** 2)
        else:
            flake(B, x, y, .1, TAU * k * 2 + rr * 9, P["hi"], 1 - k)


def mobdash_gluttony(t, B, G, P, rng):
    """기름 얼룩과 부스러기: 몸 뒤로 주황 기름 자국이 끈적하게 늘어지고, 부스러기와 침방울이 떨어진다."""
    cs = [(rng.random(), rng.uniform(-.45, .45), rng.random()) for _ in range(10)]
    G.glow(-.75, 0, 1.15, P["glow"], .45)
    for j, oy in enumerate((-.18, .14)):
        pts = [(-.2 - 1.6 * u, oy + .06 * math.sin(TAU * (u * 2 - t) + j * 2) - .15 * u * u) for u in (k / 12 for k in range(13))]
        ws = [(.16 - .1 * (k / 12)) * (.8 + .4 * math.sin(k * 1.9 + t * TAU + j)) for k in range(13)]
        B.ribbon(pts, ws, P["deep"], .9)
        B.ribbon(pts, [w * .55 for w in ws], P["light"], .9)
        B.ribbon(pts[:8], [w * .2 for w in ws[:8]], P["hi"], .9)
    for u, oy, rr in cs:
        k = (t + u) % 1
        x, y = -.4 - 1.4 * k, oy - .9 * k * k
        if rr > .5:
            crumb(B, x, y, .08 + .05 * rr, CRUMB if rr > .75 else CRUMB_L, 1 - k, seed=rr * 7 + k * 4)
        else:
            drop(B, x, y, -1, -1.4 * k, .05 + .02 * rr, P["hi"], 1 - k)
    bone = (t + .4) % 1
    drumstick(B, -.5 - 1.2 * bone, .5 - .8 * bone * bone, .16, TAU * bone * 1.5, 1 - bone ** 2)


def mobdash_lust(t, B, G, P, rng):
    """분홍 리본 자락: 몸 뒤로 분홍 리본 세 가닥이 물결치며 휘날리고, 꽃잎과 작은 하트가 흩어진다."""
    G.glow(-.75, 0, 1.15, P["glow"], .45)
    for j, (oy, col) in enumerate(((-.25, P["deep"]), (0, P["mid"]), (.25, P["light"]))):
        pts = [(-.2 - 1.75 * u, oy * (1 + u) + .2 * math.sin(TAU * (u * 1.4 - t) + j * 2) * u) for u in (k / 14 for k in range(15))]
        ws = [.09 * (1 - .5 * k / 14) for k in range(15)]
        B.ribbon(pts, ws, col)
        B.ribbon(pts, [w * .3 for w in ws], P["hi"], .8)
    for i in range(5):
        k = (t + i / 5) % 1
        x, y = -.6 - 1.3 * k, math.sin(i * 2.3) * .6 * k + .1 * math.sin(TAU * (k * 2 + i * .3))
        if i % 2:
            heart(B, x, y, .12 * (1 - k * .4), P, 1 - k)
        else:
            petal(B, x, y, k * 6 + i, .14 * (1 - k * .4), P, 1 - k)


def mobdash_envy(t, B, G, P, rng):
    """끌리는 가시 덩굴: 몸 뒤로 가시 덩굴 세 줄이 휘청이며 끌리고, 독방울과 잎이 떨어진다."""
    G.glow(-.75, 0, 1.15, P["glow"], .45)
    for j in range(3):
        pts = [(-.2 - 1.75 * u, (j - 1) * .3 * (u + .3) + .14 * math.sin(TAU * (u * 1.2 - t) + j * 2)) for u in (k / 14 for k in range(15))]
        vine(B, P, pts, .1, 1, thorns=4, leaves=1 if j == 1 else 0)
    for i in range(4):
        k = (t + i / 4) % 1
        drop(B, -.5 - 1.2 * k, math.sin(i * 2.1) * .4 - .6 * k * k, -1, -1, .07 * (1 - k) + .03, P["light"] if i % 2 else P["hi"], 1 - k)
