"""16차 속성 이펙트 시트 (Blender 5.3) — 보스 속성 공격, 부가 세트 발동, 움직이는 맵 장식, 결박 폭발.

실행:
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b -P tools/render_element_vfx.py -- C:/Project/TOD [그룹|시트 이름 ...]
  그룹: boss(내려찍기·탄·장판) proc(부가 발동) decor(맵 장식) bind(결박 폭발). 아무것도 안 주면 전부.

출력 (Assets/Art/VFX/16, 시트마다 두 장):
  <name>.png       본체. 속성 색을 칠한 종이 컷아웃 + 거친 잉크 외곽. 보통 알파 합성(Blend SrcAlpha OneMinusSrcAlpha), 흰색 틴트로 그린다.
  <name>_glow.png  빛. 속성 색 번짐만 담은 가산 합성(Blend SrcAlpha One)용. 본체보다 먼저(아래에) 그린다. 세기를 낮춰 두어 겹쳐도 하얗게 날아가지 않는다.
  형식: 가로 1줄 스트립, 정사각 칸 N장(왼쪽이 0번). 칸 수 = 폭 / 높이 (기존 Assets/vfx/flip 과 같은 규약).
  art_src/vfx/16/sheets.json  시트별 칸 수, 칸 크기, fps, 반복 여부, 피벗
  art_src/vfx/16/element_vfx.blend  마지막으로 그린 칸의 편집용 씬 (낱장 프레임 _frames 는 끝나면 지운다)

좌표: 한 칸은 월드 x, y ∈ [-2, 2]. 바닥에 붙는 이펙트는 바닥점이 y = -1.2 (피벗 (0.5, 0.2)), 바닥 원은 PROJECTION_Y .67 로 납작하게 미리 구웠다.
탄(bullet)은 +x 가 날아가는 방향이다(런타임에서 각도만큼 회전).
"""
import json
import math
import random
import sys
from pathlib import Path

import bpy
import numpy as np

ROOT = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
ONLY = sys.argv[sys.argv.index("--") + 2:]
OUT = ROOT / "Assets/Art/VFX/16"
SRC = ROOT / "art_src/vfx/16"
TMP = SRC / "_frames"
for d in (OUT, SRC, TMP):
    d.mkdir(parents=True, exist_ok=True)

TAU = math.tau
PY = .67          # 바닥 원 납작함 (게임 PROJECTION_Y)
GY = -1.2         # 바닥점 (피벗 y = (GY + 2) / 4 = .2)
PIVOT_GROUND = (.5, .2)
PIVOT_CENTER = (.5, .5)


# ---------------------------------------------------------------- 색 (sRGB 로 적고 선형으로 바꿔 꼭짓점 색에 넣는다)
def hexc(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (1, 3, 5))


def lin(c):
    return tuple(((v + .055) / 1.055) ** 2.4 if v > .04045 else v / 12.92 for v in c)


def mixc(a, b, k):
    return tuple(x + (y - x) * k for x, y in zip(a, b))


INK_SRGB = np.array(hexc("#1b1517"), dtype=np.float32)
INK = lin(hexc("#1b1517"))

# 속성 색: 게임 bind.looks 의 색조를 따르되 채도를 낮춘 종이 물감 톤 (deep → mid → light → hi), glow = 가산 빛
PALETTE_HEX = {
    "fire":      dict(deep="#6a2318", mid="#b04f2a", light="#d98a3e", hi="#eec98a", glow="#c8682e"),
    "ice":       dict(deep="#304f66", mid="#6993a8", light="#a6c6d0", hi="#e2ece8", glow="#6aa6bf"),
    "lightning": dict(deep="#5a4d26", mid="#b99a3e", light="#e0c96e", hi="#f4ecc2", glow="#c9aa45"),
    "light":     dict(deep="#73592f", mid="#bf9c55", light="#e0c88a", hi="#f4ead0", glow="#c9a75a"),
    "dark":      dict(deep="#1d1426", mid="#3d2950", light="#694c86", hi="#9c84b6", glow="#55327a"),
    "blood":     dict(deep="#470f15", mid="#862024", light="#b3453e", hi="#d98d82", glow="#8e2424"),
    "wind":      dict(deep="#3b6456", mid="#79a293", light="#b2cfc1", hi="#e0ece4", glow="#78b49c"),
    "poison":    dict(deep="#30461a", mid="#64832c", light="#97b24c", hi="#d0dd90", glow="#7aa232"),
}
PAL = {k: {n: lin(hexc(h)) for n, h in v.items()} for k, v in PALETTE_HEX.items()}
ELEMENTS = list(PAL)


# ---------------------------------------------------------------- scene (render_flipbooks.py 와 같은 방식 + 색 꼭짓점)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 10
scene.cycles.use_denoising = False
scene.cycles.transparent_max_bounces = 256
scene.cycles.max_bounces = 256
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.view_settings.view_transform = "Standard"
scene.world = bpy.data.worlds.new("void")
cam_data = bpy.data.cameras.new("cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = 4.0
cam = bpy.data.objects.new("cam", cam_data)
cam.location = (0, 0, 10)
scene.collection.objects.link(cam)
scene.camera = cam


def make_material():
    mat = bpy.data.materials.new("fx16")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Strength"].default_value = 1.0
    clear = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(attr.outputs["Color"], emit.inputs["Color"])
    nt.links.new(attr.outputs["Alpha"], mix.inputs["Fac"])
    nt.links.new(clear.outputs[0], mix.inputs[1])
    nt.links.new(emit.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    return mat


MAT = make_material()


class Layer:
    """한 프레임·한 레이어의 기하. 꼭짓점마다 (선형 RGB, 알파). 나중에 그린 것이 위에 온다.
    wob: 손그림 떨림(월드 단위). boil 이 바뀌면 떨림 모양이 바뀐다 (종이 컷아웃이 들썩이는 느낌)."""

    def __init__(self, wob=0.0, boil=0):
        self.v, self.f, self.c = [], [], []
        self.z = 0.0
        self.wob, self.boil = wob, boil

    def _vert(self, x, y, col, a):
        if self.wob:
            s = self.boil * 1.913
            x, y = (x + self.wob * math.sin(x * 4.3 + y * 2.9 + s),
                    y + self.wob * math.sin(x * 3.1 - y * 4.7 + s * 1.37))
        self.v.append((x, y, self.z))
        self.c.append((col[0], col[1], col[2], max(0.0, min(1.0, a))))
        return len(self.v) - 1

    def ribbon(self, pts, widths, col, alphas=1.0, edge=1.0):
        if len(pts) < 2:
            return
        n = len(pts)
        if not isinstance(widths, (list, tuple)):
            widths = [widths] * n
        if not isinstance(alphas, (list, tuple)):
            alphas = [alphas] * n
        self.z += 0.001
        ids = []
        for i, (x, y) in enumerate(pts):
            a = pts[max(0, i - 1)]
            b = pts[min(n - 1, i + 1)]
            tx, ty = b[0] - a[0], b[1] - a[1]
            ln = math.hypot(tx, ty) or 1
            nx, ny = -ty / ln, tx / ln
            w, al = max(0.0, widths[i]), alphas[i]
            ids.append((self._vert(x + nx * w, y + ny * w, col, al * edge),
                        self._vert(x, y, col, al),
                        self._vert(x - nx * w, y - ny * w, col, al * edge)))
        for (l0, c0, r0), (l1, c1, r1) in zip(ids, ids[1:]):
            self.f += [(l0, c0, c1, l1), (c0, r0, r1, c1)]

    def blade(self, x0, y0, x1, y1, width, col, alpha=1.0, edge=1.0, n=12, bias=.5):
        """양 끝이 뾰족한 칼날. bias = 가장 두꺼운 지점(0~1)."""
        pts, ws = [], []
        for i in range(n + 1):
            u = i / n
            pts.append((x0 + (x1 - x0) * u, y0 + (y1 - y0) * u))
            prof = (u / bias) if u < bias else ((1 - u) / (1 - bias))
            ws.append(width * max(0.0, prof) ** .8)
        self.ribbon(pts, ws, col, alpha, edge)

    def blob(self, x, y, r, col, alpha=1.0, sy=1.0, rough=.12, seed=0.0, seg=34, soft=False, rot=0.0):
        """울퉁불퉁한 종이 조각 원. soft=True 면 가운데만 진하고 바깥으로 빠지는 빛 원."""
        if r <= 0.002 or alpha <= 0.003:
            return
        self.z += 0.001
        c = self._vert(x, y, col, alpha)
        ring = []
        for i in range(seg):
            ang = i * TAU / seg + rot
            rr = r * (1 + rough * (math.sin(ang * 3 + seed) * .6 + math.sin(ang * 5 + seed * 2.3) * .4))
            ring.append(self._vert(x + math.cos(ang) * rr, y + math.sin(ang) * rr * sy, col, 0.0 if soft else alpha))
        for i in range(seg):
            self.f.append((c, ring[i], ring[(i + 1) % seg]))

    def glow(self, x, y, r, col, alpha, sy=1.0):
        self.blob(x, y, r, col, alpha, sy=sy, rough=0, soft=True, seg=32)

    def poly(self, pts, col, alpha=1.0):
        """별 모양(가운데에서 보이는) 다각형."""
        if len(pts) < 3 or alpha <= 0.003:
            return
        self.z += 0.001
        cx = sum(p[0] for p in pts) / len(pts)
        cy = sum(p[1] for p in pts) / len(pts)
        c = self._vert(cx, cy, col, alpha)
        ring = [self._vert(x, y, col, alpha) for x, y in pts]
        for i in range(len(ring)):
            self.f.append((c, ring[i], ring[(i + 1) % len(ring)]))

    def arc(self, cx, cy, r, a0, a1, width, col, alpha=1.0, sy=1.0, n=28, taper=True, edge=1.0):
        pts, ws = [], []
        for i in range(n + 1):
            u = i / n
            ang = a0 + (a1 - a0) * u
            pts.append((cx + math.cos(ang) * r, cy + math.sin(ang) * r * sy))
            ws.append(width * (math.sin(math.pi * u) ** .6 if taper else 1))
        self.ribbon(pts, ws, col, alpha, edge)

    def build(self, name):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.v, [], self.f)
        attr = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        cols = self.c
        for poly in mesh.polygons:
            for li in poly.loop_indices:
                attr.data[li].color = cols[mesh.loops[li].vertex_index]
        mesh.materials.append(MAT)
        obj = bpy.data.objects.new(name, mesh)
        scene.collection.objects.link(obj)


# ---------------------------------------------------------------- 수학 도우미
def clamp(v, a=0.0, b=1.0):
    return max(a, min(b, v))


def eo(t, p=3):
    t = clamp(t)
    return 1 - (1 - t) ** p


def seg(t, a, b):
    return clamp((t - a) / (b - a)) if b > a else float(t >= a)


def bump(t, peak, end):
    if t <= peak:
        return seg(t, 0, peak) ** .6
    return (1 - seg(t, peak, end)) ** 2.2


def pop(t, a, b):
    """a~b 동안 0 → 1 로 튀어나왔다가(오버슈트) 자리잡기."""
    k = seg(t, a, b)
    return 1 + 2.2 * (k - 1) ** 3 + 1.2 * (k - 1) ** 2 if k > 0 else 0.0


def wave01(x):
    return .5 + .5 * math.sin(TAU * x)


def bolt(rng, x0, y0, x1, y1, n, amp):
    pts = []
    for i in range(n + 1):
        u = i / n
        off = 0 if i in (0, n) else (rng.random() - .5) * 2 * amp * math.sin(u * math.pi) ** .5
        dx, dy = x1 - x0, y1 - y0
        ln = math.hypot(dx, dy) or 1
        pts.append((x0 + dx * u - dy / ln * off, y0 + dy * u + dx / ln * off))
    return pts


# ---------------------------------------------------------------- 공용 그림 조각
def tongue(L, x, y, dx, dy, length, sway, width, col, alpha=1.0, ph=0.0, n=12):
    """방향 (dx, dy) 로 솟는 불꽃 혀. 밑이 둥글고 넓고, 끝은 S자로 휘며 뾰족하다."""
    nx, ny = -dy, dx
    pts, ws = [], []
    for i in range(n + 1):
        u = i / n
        s = sway * math.sin(u * math.pi * 1.3 + ph) * u
        pts.append((x + dx * length * u + nx * s, y + dy * length * u + ny * s))
        ws.append(width * (1 - u) ** .8 * (.62 + .38 * math.sin(math.pi * min(1, u * 2.2))))
    L.ribbon(pts, ws, col, alpha)


def fire_tongue(B, P, x, y, dx, dy, length, sway, width, alpha=1.0, ph=0.0):
    """종이를 겹겹이 오려 붙인 불꽃 (짙은 → 밝은 순서로 작아진다)."""
    if length < .02:
        return
    tongue(B, x, y, dx, dy, length, sway, width, P["deep"], alpha, ph)
    tongue(B, x, y, dx, dy, length * .84, sway * .9, width * .76, P["mid"], alpha, ph)
    tongue(B, x, y, dx, dy, length * .6, sway * .7, width * .5, P["light"], alpha, ph)
    tongue(B, x, y, dx, dy, length * .34, sway * .5, width * .26, P["hi"], alpha, ph)


def sparkle(L, x, y, r, col, alpha=1.0, rot=0.0):
    """4갈래 반짝임."""
    for k in range(2):
        a = rot + k * math.pi / 2
        dx, dy = math.cos(a) * r, math.sin(a) * r
        L.blade(x - dx, y - dy, x + dx, y + dy, r * .22, col, alpha, n=8)


def drop(L, x, y, vx, vy, r, col, alpha=1.0, stretch=.08):
    """날아가는 물방울: 머리는 둥글고 뒤로 꼬리가 늘어진다."""
    sp = math.hypot(vx, vy) or 1
    tl = r + stretch * sp
    L.blade(x - vx / sp * tl, y - vy / sp * tl, x, y, r * .85, col, alpha, n=8, bias=.85)
    L.blob(x, y, r, col, alpha, rough=.05, seg=14)


def leaf(L, x, y, ang, size, col, vein, alpha=1.0):
    dx, dy = math.cos(ang) * size, math.sin(ang) * size
    L.blade(x - dx, y - dy, x + dx, y + dy, size * .42, col, alpha, n=10)
    L.blade(x - dx * .8, y - dy * .8, x + dx * .7, y + dy * .7, size * .06, vein, alpha, n=4)


def shard(L, x, y, ang, length, width, col, alpha=1.0):
    dx, dy = math.cos(ang), math.sin(ang)
    L.blade(x - dx * length, y - dy * length, x, y, width, col, alpha, n=6, bias=.75)


def ground_ring(B, G, P, t, r0, r1, width, a=1.0, y=GY, col="mid"):
    k = eo(t, 2.5)
    if 0 < t < 1:
        B.arc(0, y, r0 + (r1 - r0) * k, 0, TAU, width * (1 - t) + .01, P[col], a * (1 - t) ** .7, sy=PY, n=48, taper=False)
        G.arc(0, y, r0 + (r1 - r0) * k, 0, TAU, width * 3 * (1 - t) + .02, P["glow"], .35 * a * (1 - t), sy=PY, n=48, taper=False, edge=0)


def ballistic(x0, y0, vx, vy, s, g=3.2):
    return x0 + vx * s, y0 + vy * s - g * s * s, vx, vy - 2 * g * s


# ================================================================ 1. 보스 속성 공격 — 내려찍기 (바닥 기준, 16칸)

def slam_fire(t, B, G, P, rng):
    """화염 불기둥: 발밑 그을음 → 불길 혀가 솟구쳐 기둥이 되었다가 잦아들고, 불씨가 튄다."""
    B.blob(0, GY, .45 + .8 * eo(seg(t, 0, .3)), mixc(P["deep"], INK, .45), .7 * (1 - seg(t, .7, 1)), sy=PY, rough=.1, seed=1)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.7, .07)
    rise = eo(seg(t, 0, .16), 2) * (1 - seg(t, .55, 1)) ** 1.1
    G.glow(0, GY + .9 * rise, 1.5 * rise + .2, P["glow"], .5 * rise)
    G.glow(0, GY, 1.4, P["glow"], .4 * rise, sy=PY)
    tongues = [(-.55, .55), (.55, .6), (-.3, .9), (.3, .95), (-.12, 1.0), (.12, .98), (0, 1.0)]
    for i, (x, tall) in enumerate(tongues):
        ph = rng.random() * TAU
        h = (1.4 + 1.2 * tall) * rise * (.86 + .14 * math.sin(TAU * t * 3 + ph))
        sway = .22 * math.sin(TAU * t * 2 + ph)
        fire_tongue(B, P, x, GY - .05, x * .25, 1, h, sway, .32 + .1 * tall, ph=ph)
    for i in range(20):
        d = rng.uniform(.05, .3)
        s = seg(t, d, 1) * .9
        if s <= 0:
            continue
        x, y, vx, vy = ballistic(rng.uniform(-.4, .4), GY + rng.uniform(0, .6), rng.uniform(-1.6, 1.6), rng.uniform(1.8, 3.4), s, 2.2)
        a = (1 - seg(t, d + .3, 1)) ** 1.2
        r = .03 + .03 * rng.random()
        drop(B, x, y, vx, vy, r, P["light"] if i % 3 else P["hi"], a, .03)
        G.glow(x, y, r * 4, P["glow"], .35 * a)


def slam_ice(t, B, G, P, rng):
    """얼음 서리 파편: 바닥이 하얗게 얼고, 둘레에서 얼음 가시가 솟았다가 부서져 흩어진다."""
    fade = 1 - seg(t, .72, 1)
    B.blob(0, GY, .3 + .95 * eo(seg(t, 0, .3)), P["mid"], .6 * fade, sy=PY, rough=.12, seed=3)
    B.blob(-.05, GY + .02, .2 + .6 * eo(seg(t, 0, .3)), P["light"], .55 * fade, sy=PY, rough=.14, seed=5)
    ground_ring(B, G, P, seg(t, 0, .45), .2, 1.8, .06, col="mid")
    G.glow(0, GY + .3, 1.6, P["glow"], .45 * bump(t, .12, 1))
    spikes = []
    for i in range(11):
        ang = i * TAU / 11 + rng.uniform(-.2, .2)
        r = rng.uniform(.45, 1.25)
        spikes.append((math.sin(ang), ang, r, rng.uniform(.45, 1.1), rng.uniform(0, .12)))
    for _, ang, r, h, d in sorted(spikes, reverse=True):   # 먼 것부터
        bx, by = math.cos(ang) * r, GY + math.sin(ang) * r * PY
        g = pop(t, d, d + .16) * (1 - seg(t, .62, .85))
        if g <= .01:
            continue
        tx, ty = bx + math.cos(ang) * .35 * h * g, by + h * g
        B.blade(bx - .02, by - .04, tx, ty, .16 * g, P["deep"], bias=.22)
        B.blade(bx, by - .02, tx, ty, .11 * g, P["mid"], bias=.22)
        B.blade(bx + .03, by, tx * .7 + bx * .3, ty * .7 + by * .3, .05 * g, P["hi"], bias=.25)
    for i in range(22):    # 부서진 조각
        d = .55 + rng.uniform(0, .12) if i < 14 else rng.uniform(0, .08)
        s = seg(t, d, 1) * .8
        if s <= 0:
            continue
        x, y, vx, vy = ballistic(rng.uniform(-1, 1), GY + rng.uniform(.1, .8), rng.uniform(-2.2, 2.2), rng.uniform(.6, 2.6), s, 3.4)
        a = 1 - seg(s, .45, .8)
        shard(B, x, y, math.atan2(vy, vx), .14 + .05 * rng.random(), .07, P["mid"] if i % 2 else P["light"], a)
    for i in range(7):     # 서리 김 (외곽 없이 옅게)
        k = seg(t, .1 + i * .04, 1)
        ang = i * TAU / 7
        B.blob(math.cos(ang) * (.6 + .9 * k), GY + .15 + .3 * k + math.sin(ang) * .3, .25 + .3 * k, P["hi"], .27 * (1 - k), rough=.3, seed=i, soft=True)


def slam_lightning(t, B, G, P, rng):
    """번개 낙뢰: 하늘에서 지그재그 줄기가 내리꽂히며 깜빡이고, 바닥을 따라 전기가 튄다."""
    frng = random.Random(int(t * 1000) * 7 + 3)
    strike = 1 - seg(t, .05, .5)
    B.blob(0, GY, .3 + .7 * eo(seg(t, 0, .25)), mixc(P["deep"], INK, .5), .6 * (1 - seg(t, .45, .8)), sy=PY, rough=.15, seed=2)
    ground_ring(B, G, P, seg(t, .02, .45), .2, 1.8, .06, col="light")
    if strike > 0 and not (t > .25 and int(t * 15) % 3 == 2):
        top = (frng.uniform(-.3, .3), 2.1)
        main = bolt(frng, top[0], top[1], 0, GY + .1, 14, .35)
        G.ribbon(main, .5 * strike, P["glow"], .45 * strike, edge=0)
        B.ribbon(main, .13 * strike + .03, P["deep"])
        B.ribbon(main, .08 * strike + .02, P["light"])
        B.ribbon(main, .035 * strike + .01, P["hi"])
        for _ in range(3):
            k = frng.randint(3, 9)
            x, y = main[k]
            side = frng.choice((-1, 1))
            br = bolt(frng, x, y, x + side * frng.uniform(.4, .8), y - frng.uniform(.3, .7), 5, .12)
            B.ribbon(br, .05 * strike + .01, P["deep"])
            B.ribbon(br, .025 * strike + .005, P["light"])
    flash = bump(t, .06, .35)
    if flash > .02:
        B.blob(0, GY + .15, .55 * flash, P["light"], flash, sy=.8, rough=.35, seed=frng.random() * 6)
        B.blob(0, GY + .15, .3 * flash, P["hi"], flash, sy=.8, rough=.3, seed=frng.random() * 6)
        G.glow(0, GY + .3, 1.8 * flash + .3, P["glow"], .55 * flash)
    arcs = 1 - seg(t, .3, .85)
    for i in range(6):     # 바닥을 따라 튀는 전기
        if arcs <= 0 or frng.random() < .3:
            continue
        ang = i * TAU / 6 + frng.uniform(-.3, .3)
        r = (.6 + .9 * eo(seg(t, .08, .6))) * frng.uniform(.7, 1)
        pts = bolt(frng, 0, GY, math.cos(ang) * r, GY + math.sin(ang) * r * PY, 6, .12)
        B.ribbon(pts, .045 * arcs + .01, P["deep"])
        B.ribbon(pts, .02 * arcs + .005, P["hi"])
        G.ribbon(pts, .15, P["glow"], .3 * arcs, edge=0)
    for i in range(12):
        s = seg(t, .05, .6)
        if s <= 0 or s >= 1:
            continue
        ang = rng.uniform(.2, math.pi - .2)
        d = rng.uniform(.6, 1.7) * eo(s, 2)
        shard(B, math.cos(ang) * d, GY + math.sin(ang) * d * .8, ang, .18 * (1 - s) + .04, .035, P["light"], 1 - s)


def slam_light(t, B, G, P, rng):
    """빛 십자 섬광: 바닥에서 십자 광채가 번쩍 서고, 빛의 고리가 퍼지며 빛 기둥이 솟는다."""
    cy = GY + .65
    s = bump(t, .12, .75)
    G.glow(0, cy, 1.9 * s + .2, P["glow"], .5 * s)
    G.glow(0, GY, 1.6, P["glow"], .35 * s, sy=PY)
    for k, d in enumerate((0, .14)):
        ground_ring(B, G, P, seg(t, d, d + .55), .3, 1.85, .07, col="hi" if k else "light")
    for i in range(9):     # 바닥 고리에서 솟는 빛 기둥
        ang = i * TAU / 9 + .2
        r = 1.0 + .25 * math.sin(i * 2.1)
        g = bump(t, .18 + i * .015, .9)
        if g < .03:
            continue
        x, y = math.cos(ang) * r, GY + math.sin(ang) * r * PY
        B.blade(x, y - .05, x, y + (.6 + .5 * rng.random()) * g, .05 * g + .01, P["light"], bias=.15)
    if s > .02:
        L = 1.9 * s
        B.blade(0, cy - L * .75, 0, cy + L, .2 * s, P["mid"], n=16)
        B.blade(-L * .95, cy, L * .95, cy, .17 * s, P["mid"], n=16)
        B.blade(0, cy - L * .7, 0, cy + L * .95, .12 * s, P["light"], n=16)
        B.blade(-L * .9, cy, L * .9, cy, .1 * s, P["light"], n=16)
        for a in (.785, -.785, 2.356, -2.356):
            B.blade(0, cy, math.cos(a) * L * .45, cy + math.sin(a) * L * .45, .07 * s, P["light"], bias=.2)
        B.blade(0, cy - L * .55, 0, cy + L * .8, .05 * s, P["hi"], n=16)
        B.blade(-L * .7, cy, L * .7, cy, .045 * s, P["hi"], n=16)
        B.blob(0, cy, .22 * s, P["hi"], 1, rough=.1)
    for i in range(14):
        k = seg(t, .1 + rng.random() * .2, 1)
        if k <= 0 or k >= 1:
            continue
        x = rng.uniform(-1.5, 1.5)
        y = GY + rng.uniform(0, .4) + 1.6 * eo(k, 2)
        sparkle(B, x, y, .09 * math.sin(math.pi * k) + .01, P["hi"], 1, rot=k * 2)


def slam_dark(t, B, G, P, rng):
    """어둠 촉수 연기: 발밑에 그림자 웅덩이가 번지고, 가장자리에서 연기 촉수가 휘감아 솟는다."""
    fade = 1 - seg(t, .75, 1)
    B.blob(0, GY, .4 + .95 * eo(seg(t, 0, .3)), mixc(P["deep"], INK, .3), .85 * fade, sy=PY, rough=.16, seed=7)
    B.blob(0, GY, .3 + .6 * eo(seg(t, 0, .3)), P["mid"], .55 * fade, sy=PY, rough=.2, seed=8)
    G.glow(0, GY + .2, 1.6, P["glow"], .35 * bump(t, .2, 1))
    arms = []
    for i in range(7):
        ang = i * TAU / 7 + rng.uniform(-.2, .2)
        arms.append((math.sin(ang), ang, rng.uniform(.5, 1.0), rng.uniform(1.0, 1.9), rng.uniform(0, .15), rng.choice((-1, 1))))
    for _, ang, r, h, d, side in sorted(arms, reverse=True):
        grow = eo(seg(t, d, d + .3), 2) * (1 - seg(t, .62, .95))
        if grow <= .02:
            continue
        bx, by = math.cos(ang) * r, GY + math.sin(ang) * r * PY
        pts, ws = [], []
        for k in range(17):
            u = k / 16
            curl = side * (.35 * math.sin(u * math.pi * 1.4 + t * 4) * u + .45 * u ** 3)
            pts.append((bx + curl + math.cos(ang) * .2 * u, by + h * grow * u))
            ws.append(.17 * (1 - u) ** .7 * grow + .01)
        B.ribbon(pts, ws, P["deep"])
        B.ribbon([(x + side * .03, y) for x, y in pts], [w * .55 for w in ws], P["mid"])
        B.ribbon([(x - side * .05, y) for x, y in pts], [w * .16 for w in ws], P["hi"], [.8 * (1 - k / 16) for k in range(17)])
    for i in range(10):    # 솟는 연기 덩이
        k = seg(t, .05 + i * .05, 1)
        if k <= 0 or k >= 1:
            continue
        x = rng.uniform(-1.1, 1.1) + .2 * math.sin(k * 3 + i)
        B.blob(x, GY + .2 + 1.5 * k, .14 + .22 * k, P["mid"] if i % 2 else P["light"], .7 * (1 - k) ** 1.2, rough=.3, seed=i)


def slam_blood(t, B, G, P, rng):
    """피 핏물 파도: 붉은 웅덩이가 터지며 양옆으로 핏물 파도가 말려 나가고, 핏방울이 흩뿌려진다."""
    fade = 1 - seg(t, .75, 1)
    B.blob(0, GY, .3 + 1.1 * eo(seg(t, 0, .4)), P["deep"], .9 * fade, sy=PY, rough=.14, seed=9)
    B.blob(-.1, GY + .03, .2 + .9 * eo(seg(t, 0, .4)), P["mid"], .8 * fade, sy=PY, rough=.3, seed=10)
    B.blob(-.25, GY + .08, .15 + .3 * eo(seg(t, 0, .4)), P["hi"], .5 * fade, sy=PY * .6, rough=.3, seed=11)
    G.glow(0, GY + .3, 1.5, P["glow"], .3 * bump(t, .2, 1))
    col = eo(seg(t, 0, .18)) * (1 - seg(t, .3, .6))
    if col > .02:          # 가운데 솟는 핏기둥
        B.blade(0, GY - .1, 0, GY + 1.4 * col, .35 * col, P["deep"], bias=.25)
        B.blade(0, GY - .05, .05, GY + 1.1 * col, .22 * col, P["mid"], bias=.25)
        B.blob(0, GY + 1.4 * col, .2 * col, P["mid"], rough=.3)
    for side in (-1, 1):   # 말려 나가는 파도 둘 (뒤 파도는 작게)
        for k2, (lag, scale) in enumerate(((0, 1), (.12, .7))):
            tt = seg(t, .08 + lag, 1)
            H = 1.05 * scale * bump(tt, .25, 1)
            if H < .03:
                continue
            x = side * (.3 + 1.25 * eo(tt, 2))
            pts, ws = [], []
            for k in range(19):
                u = k / 18
                px = x - side * .75 * (1 - u) + side * .22 * math.sin(math.pi * u) * u
                py = GY + H * math.sin(math.pi * min(1, u * 1.1) * .92) - (u > .8) * (u - .8) * H * 1.6
                pts.append((px, py))
                ws.append((.2 * (1 - u) ** .5 + .03) * scale)
            B.ribbon(pts, ws, P["deep"])
            B.ribbon([(px + side * .02, py + .02) for px, py in pts], [w * .6 for w in ws], P["mid"])
            B.ribbon([(px + side * .04, py + .05) for px, py in pts[4:]], [w * .2 for w in ws[4:]], P["hi"], .8)
            for j in range(3):    # 물마루에서 튀는 방울
                s = seg(tt, .2 + j * .1, .9)
                if 0 < s < 1:
                    x2, y2, vx, vy = ballistic(pts[-4][0], pts[-4][1], side * (.8 + j * .4), 1.2 + j * .3, s * .7, 3)
                    drop(B, x2, y2, vx, vy, .05 * scale, P["mid"], 1 - s)
    for i in range(16):
        s = seg(t, rng.uniform(0, .15), 1) * .85
        if s <= 0:
            continue
        x, y, vx, vy = ballistic(rng.uniform(-.2, .2), GY + .3, rng.uniform(-2, 2), rng.uniform(1.5, 3.2), s, 3.4)
        if y < GY - .2:
            continue
        drop(B, x, y, vx, vy, .04 + .04 * rng.random(), P["mid"] if i % 2 else P["light"], 1 - seg(s, .6, .85))


def slam_wind(t, B, G, P, rng):
    """바람 회오리: 바닥 먼지 고리에서 회오리가 비틀며 솟고, 잎과 먼지가 감겨 오른다."""
    grow = eo(seg(t, 0, .3), 2)
    fade = 1 - seg(t, .7, 1)
    rot = TAU * t * 2.2
    G.glow(0, GY + .9, 1.3, P["glow"], .3 * grow * fade)
    for i in range(9):     # 바닥 먼지
        ang = i * TAU / 9
        k = seg(t, 0, .7)
        r = .5 + 1.1 * eo(k)
        B.blob(math.cos(ang + k) * r, GY + math.sin(ang + k) * r * PY, .18 + .14 * k, P["light"], .27 * (1 - k), rough=.35, seed=i, soft=True)
    ground_ring(B, G, P, seg(t, 0, .45), .3, 1.7, .05, col="light")
    for k in range(9):     # 회오리 고리 (뒤쪽 반 먼저, 앞쪽 반 나중)
        y = GY + .05 + k * .3 * grow
        r = (.22 + k * .13) * (.7 + .3 * grow)
        a0 = rot * (1 + k * .08) + k * .9
        B.arc(0, y, r, a0 + math.pi * .1, a0 + math.pi * 1.0, .05 + .012 * k, P["mid"], fade, sy=.32, n=20)
    for k in range(9):
        y = GY + .05 + k * .3 * grow
        r = (.22 + k * .13) * (.7 + .3 * grow)
        a0 = rot * (1 + k * .08) + k * .9 + math.pi
        B.arc(0, y, r, a0, a0 + math.pi * 1.1, .045 + .012 * k, P["light"] if k % 2 else P["hi"], fade, sy=.32, n=20)
    for i in range(10):    # 감겨 오르는 잎
        ph = rng.random()
        k = (ph + t * .9) % 1
        ang = ph * TAU + TAU * t * 2.5
        h = k * 2.4 * grow
        r = .3 + h * .4
        x, y = math.cos(ang) * r, GY + h + math.sin(ang) * r * .32
        leaf(B, x, y, ang * 1.3, .1, P["deep"] if i % 2 else P["mid"], P["hi"], fade * math.sin(math.pi * k) ** .3)
    for i in range(8):     # 스치는 바람 결
        y = GY + .3 + i * .22
        x = math.sin(i * 2.3 + t * 6) * .8
        B.blade(x - .5, y, x + .5, y + .05, .025, P["hi"], .8 * grow * fade, bias=.6)


def slam_poison(t, B, G, P, rng):
    """독 독액 튀김: 독 덩이가 터져 사방으로 독액이 튀고, 바닥 웅덩이에 거품이 끓고 독기가 오른다."""
    fade = 1 - seg(t, .75, 1)
    B.blob(0, GY, .35 + 1.05 * eo(seg(t, 0, .35)), P["deep"], .9 * fade, sy=PY, rough=.15, seed=12)
    B.blob(.05, GY + .02, .25 + .75 * eo(seg(t, 0, .35)), P["mid"], .85 * fade, sy=PY, rough=.18, seed=13)
    G.glow(0, GY + .4, 1.6, P["glow"], .4 * bump(t, .15, 1))
    sp = bump(t, .1, .45)
    if sp > .03:           # 튀는 순간의 가시 별
        pts = []
        for i in range(18):
            ang = i * TAU / 18
            r = (.95 if i % 2 == 0 else .4) * sp * (1 + .25 * math.sin(i * 7.1))
            pts.append((math.cos(ang) * r, GY + .35 + math.sin(ang) * r * .8))
        B.poly(pts, P["mid"])
        B.blob(0, GY + .35, .35 * sp, P["light"], rough=.3)
    for i in range(22):
        s = seg(t, rng.uniform(.03, .12), 1) * .9
        if s <= 0:
            continue
        x, y, vx, vy = ballistic(rng.uniform(-.2, .2), GY + .35, rng.uniform(-2.4, 2.4), rng.uniform(1.2, 3.4), s, 3.6)
        if y < GY - .15:
            continue
        drop(B, x, y, vx, vy, .045 + .04 * rng.random(), P["light"] if i % 3 else P["hi"], 1 - seg(s, .6, .85))
    for i in range(7):     # 거품
        bx, by = rng.uniform(-.9, .9), GY + rng.uniform(-.25, .2)
        k = seg(t, .3 + i * .05, .3 + i * .05 + .35)
        if 0 < k < 1:
            r = .05 + .13 * k
            if k < .85:
                B.blob(bx, by + r * .6, r, P["light"], fade, rough=.05)
                B.blob(bx + r * .35, by + r * .95, r * .3, P["hi"], fade, rough=0)
            else:
                B.arc(bx, by + r * .6, r * 1.3, 0, TAU, .02, P["hi"], fade, n=20, taper=False)
    for i in range(6):
        k = seg(t, .2 + i * .07, 1)
        if 0 < k < 1:
            B.blob(rng.uniform(-1, 1), GY + .3 + 1.4 * k, .2 + .25 * k, P["light"], .27 * (1 - k), rough=.3, seed=i, soft=True)


# ================================================================ 1b. 보스 탄 (반복 8칸, +x 진행, 128px)

def bullet_fire(t, B, G, P, rng):
    G.glow(-.3, 0, 1.4, P["glow"], .45)
    for i in range(5):
        ph = i / 5
        k = (t + ph) % 1
        fire_tongue(B, P, .1, (i - 2) * .12, -1, (i - 2) * .05, 1.1 + .6 * math.sin(math.pi * k), .25 * math.sin(TAU * (t + ph)), .42, ph=ph * 5)
    B.blob(.15, 0, .55, P["mid"], rough=.12, seed=t * 6)
    B.blob(.2, .02, .38, P["light"], rough=.15, seed=t * 7)
    B.blob(.26, .06, .18, P["hi"], rough=.1)
    for i in range(5):
        k = (t + i / 5) % 1
        B.blob(-.5 - 1.3 * k, (rng.random() - .5) * .8 * k, .07 * (1 - k) + .02, P["light"], 1 - k)


def bullet_ice(t, B, G, P, rng):
    G.glow(-.2, 0, 1.3, P["glow"], .4)
    for i in range(7):
        k = (t + i / 7) % 1
        x = -.4 - 1.4 * k
        y = (rng.random() - .5) * .9 * k
        sparkle(B, x, y, .12 * (1 - k) + .02, P["hi"] if i % 2 else P["light"], 1 - k, rot=k * 3)
    B.blob(-.6, 0, .35, P["hi"], .25, rough=.3)
    tip, back = 1.3, -.75
    B.poly([(tip, 0), (.2, .36), (back, .2), (back - .2, 0), (back, -.22), (.2, -.36)], P["deep"])
    B.poly([(tip - .1, 0), (.2, .28), (back + .05, .12), (back + .1, -.14), (.2, -.26)], P["mid"])
    B.poly([(tip - .2, .02), (.2, .22), (-.3, .1), (.1, .04)], P["light"])
    B.blade(-.4, .1, tip - .3, .03, .04, P["hi"], bias=.7)


def bullet_lightning(t, B, G, P, rng):
    frng = random.Random(int(t * 100) * 13 + 5)
    G.glow(0, 0, 1.4, P["glow"], .5)
    trail = bolt(frng, .2, 0, -1.9, frng.uniform(-.3, .3), 9, .3)
    B.ribbon(trail, [.1 * (1 - k / 9) + .01 for k in range(10)], P["deep"])
    B.ribbon(trail, [.05 * (1 - k / 9) + .005 for k in range(10)], P["light"])
    B.blob(0, 0, .55, P["deep"], rough=.3, seed=frng.random() * 9)
    B.blob(0, 0, .42, P["light"], rough=.3, seed=frng.random() * 9)
    B.blob(.05, .05, .2, P["hi"], rough=.2)
    for _ in range(4):
        ang = frng.uniform(0, TAU)
        pts = bolt(frng, math.cos(ang) * .35, math.sin(ang) * .35, math.cos(ang) * frng.uniform(.8, 1.2), math.sin(ang) * frng.uniform(.8, 1.2), 4, .15)
        B.ribbon(pts, .045, P["deep"])
        B.ribbon(pts, .02, P["hi"])


def bullet_light(t, B, G, P, rng):
    G.glow(0, 0, 1.4, P["glow"], .45)
    for i in range(6):
        k = (t + i / 6) % 1
        sparkle(B, -.5 - 1.3 * k, math.sin(i * 2.1) * .4 * k, .14 * (1 - k) + .02, P["hi"], 1 - k, rot=i)
    rot = TAU * t / 4
    B.blob(0, 0, .42, P["mid"], rough=.06)
    for k in range(4):
        a = rot + k * math.pi / 2
        B.blade(0, 0, math.cos(a) * 1.05, math.sin(a) * 1.05, .2, P["light"], bias=.12)
    for k in range(4):
        a = rot + math.pi / 4 + k * math.pi / 2
        B.blade(0, 0, math.cos(a) * .6, math.sin(a) * .6, .12, P["light"], bias=.12)
    B.blob(0, 0, .25, P["hi"], rough=.05)


def bullet_dark(t, B, G, P, rng):
    G.glow(0, 0, 1.2, P["glow"], .35)
    for i in range(5):
        k = (t + i / 5) % 1
        side = 1 if i % 2 else -1
        pts = [(-.2 - 1.6 * u, side * (.15 + .3 * math.sin(math.pi * u * 1.5 + TAU * t)) * u) for u in (j / 12 for j in range(13))]
        B.ribbon(pts, [.16 * (1 - j / 12) + .01 for j in range(13)], P["deep"], 1)
        B.blob(-.6 - 1.2 * k, side * .3 * k, .18 * (1 - k) + .03, P["mid"], (1 - k) * .8, rough=.3, seed=i)
    B.blob(0, 0, .6, P["deep"], rough=.22, seed=t * TAU)
    B.blob(.05, .03, .42, P["mid"], rough=.25, seed=t * TAU + 1)
    B.arc(0, 0, .5, TAU * t, TAU * t + 2.2, .05, P["hi"], .9, n=16)
    B.blob(.1, .08, .12, P["hi"], rough=.1)


def bullet_blood(t, B, G, P, rng):
    G.glow(-.2, 0, 1.2, P["glow"], .3)
    for i in range(4):
        k = (t + i / 4) % 1
        drop(B, -.6 - 1.2 * k, (math.sin(i * 1.7) * .3 - .2) * k, -1, -.3, .1 * (1 - k) + .03, P["mid"], 1 - k)
    wob = .06 * math.sin(TAU * t)
    B.blade(-1.3, 0, .55, 0, .48 + wob, P["deep"], bias=.85)
    B.blob(.45, 0, .5 + wob, P["deep"], rough=.06)
    B.blade(-1.0, .03, .5, .02, .34, P["mid"], bias=.85)
    B.blob(.45, .02, .36, P["mid"], rough=.06)
    B.blob(.6, .18, .12, P["hi"], rough=.1)


def bullet_wind(t, B, G, P, rng):
    G.glow(0, 0, 1.2, P["glow"], .3)
    rot = -TAU * t
    for k in range(3):
        a = rot + k * TAU / 3
        B.arc(0, 0, .75, a, a + 2.0, .2, P["mid"], n=20)
        B.arc(0, 0, .7, a + .3, a + 1.9, .09, P["light"], n=16)
        B.arc(0, 0, .62, a + .6, a + 1.7, .03, P["hi"], n=12)
    B.blob(0, 0, .22, P["light"], rough=.2)
    for i in range(6):
        k = (t + i / 6) % 1
        y = (i - 2.5) * .22
        x = -.6 - 1.2 * k
        B.blade(x - .5, y, x + .2, y, .035, P["hi"], 1 - k, bias=.75)


def bullet_poison(t, B, G, P, rng):
    G.glow(0, 0, 1.3, P["glow"], .4)
    for i in range(4):
        k = (t + i / 4) % 1
        drop(B, -.5 - 1.3 * k, -.25 - .3 * k + math.sin(i * 2) * .2, -1, -.6, .09 * (1 - k) + .03, P["light"], 1 - k)
    sq = .08 * math.sin(TAU * t)
    B.blob(0, 0, .62, P["deep"], sy=1 - sq, rough=.15, seed=t * TAU)
    B.blob(.05, .02, .48, P["mid"], sy=1 - sq, rough=.2, seed=t * TAU + 2)
    for i in range(4):
        k = (t + i / 4) % 1
        ang = i * 1.7
        r = .06 + .1 * math.sin(math.pi * k)
        B.blob(math.cos(ang) * .3, math.sin(ang) * .3, r, P["light"], rough=.05)
        B.blob(math.cos(ang) * .3 + r * .3, math.sin(ang) * .3 + r * .3, r * .3, P["hi"], rough=0)


# ================================================================ 1c. 바닥 장판 (화염·독, 반복 12칸, 가운데 피벗)
PG = -.6   # 장판 가운데 y (피벗 (0.5, 0.35))


def pool_fire(t, B, G, P, rng):
    G.glow(0, PG + .3, 1.9, P["glow"], .4 + .08 * math.sin(TAU * t * 2))
    G.glow(0, PG, 1.9, P["glow"], .35, sy=PY)
    B.blob(0, PG, 1.75, mixc(P["deep"], INK, .4), .9, sy=PY, rough=.12, seed=1)
    B.blob(0, PG, 1.5, P["deep"], .9, sy=PY, rough=.15, seed=2)
    B.blob(.1, PG + .05, 1.0, P["mid"], .6, sy=PY * .9, rough=.25, seed=3)
    spots = []
    for i in range(11):
        ang = rng.uniform(0, TAU)
        r = math.sqrt(rng.random()) * 1.3
        spots.append((math.sin(ang) * r, math.cos(ang) * r, rng.random(), rng.uniform(.55, 1)))
    for sy_, x, ph, tall in sorted(spots, reverse=True):
        y = PG + sy_ * PY
        h = (.35 + .45 * tall) * (.65 + .35 * math.sin(TAU * (t + ph)) + .1 * math.sin(TAU * (2 * t + ph * 3)))
        fire_tongue(B, P, x, y, 0, 1, h, .1 * math.sin(TAU * (t + ph * 2)), .2 + .06 * tall, ph=ph * TAU)
    for i in range(10):
        ph = rng.random()
        k = (t + ph) % 1
        x = rng.uniform(-1.3, 1.3) + .15 * math.sin(TAU * k + i)
        y = PG + rng.uniform(-.4, .3) + 1.8 * k
        B.blob(x, y, .035, P["hi"] if i % 2 else P["light"], math.sin(math.pi * k), rough=.1)


def pool_poison(t, B, G, P, rng):
    G.glow(0, PG + .2, 1.9, P["glow"], .35)
    B.blob(0, PG, 1.75, P["deep"], .92, sy=PY, rough=.14, seed=4)
    B.blob(-.05, PG + .03, 1.4, P["mid"], .9, sy=PY, rough=.18, seed=5)
    B.blob(-.4, PG + .25, .55, P["light"], .5, sy=PY * .5, rough=.3, seed=6)
    B.blob(.5, PG - .1, .35, P["light"], .4, sy=PY * .5, rough=.3, seed=7)
    for i in range(12):
        ph = rng.random()
        k = (t * 2 + ph) % 1 if i % 3 == 0 else (t + ph) % 1
        ang = rng.uniform(0, TAU)
        r = math.sqrt(rng.random()) * 1.2
        x, y = math.cos(ang) * r, PG + math.sin(ang) * r * PY
        if k < .82:
            rr = .04 + .14 * (k / .82) * (.6 + .4 * rng.random())
            B.blob(x, y + rr * .5, rr, P["light"], rough=.05)
            B.blob(x + rr * .35, y + rr * .85, rr * .3, P["hi"], rough=0)
        else:
            kk = seg(k, .82, 1)
            B.arc(x, y + .08, .1 + .18 * kk, 0, TAU, .025 * (1 - kk) + .005, P["hi"], 1 - kk, sy=PY, n=20, taper=False)
            for j in range(4):
                a = j * TAU / 4 + .5
                B.blob(x + math.cos(a) * .2 * kk, y + .1 + .25 * kk * math.sin(math.pi * kk) + math.sin(a) * .1 * kk, .03, P["light"], 1 - kk)
    for i in range(6):
        k = (t + i / 6) % 1
        x = (i - 2.5) * .45 + .2 * math.sin(TAU * k + i)
        B.blob(x, PG + .2 + 1.9 * k, .18 + .25 * k, P["light"], .27 * math.sin(math.pi * k), rough=.3, seed=i, soft=True)


# ================================================================ 2. 부가 세트 발동 (12칸 0.5초, 적 몸 가운데 피벗) — 날카롭고 화려하게

def proc_fire(t, B, G, P, rng):
    """불꽃 개화: 날 선 불꽃 가시 8개가 별처럼 터지며 돌고, 불똥이 사방으로 흩어진다."""
    s = pop(t, 0, .22) * (1 - seg(t, .55, 1))
    G.glow(0, 0, 1.8 * s + .2, P["glow"], .55 * bump(t, .1, 1))
    rot = .6 * t
    for i in range(8):
        a = rot + i * TAU / 8
        L = (1.65 if i % 2 == 0 else 1.05) * s
        thin = 1 - .6 * seg(t, .5, 1)
        tongue(B, math.cos(a) * .1, math.sin(a) * .1, math.cos(a), math.sin(a), L, .18 * (1 if i % 2 else -1), .3 * thin, P["deep"])
        tongue(B, math.cos(a) * .1, math.sin(a) * .1, math.cos(a), math.sin(a), L * .8, .15 * (1 if i % 2 else -1), .2 * thin, P["mid"])
        tongue(B, math.cos(a) * .1, math.sin(a) * .1, math.cos(a), math.sin(a), L * .55, .1 * (1 if i % 2 else -1), .1 * thin, P["hi"])
    if s > .05:
        B.blob(0, 0, .35 * s, P["light"], rough=.3, seed=t * 9)
        B.blob(0, 0, .18 * s, P["hi"], rough=.2)
    k = seg(t, 0, .6)
    if 0 < k < 1:
        B.arc(0, 0, .3 + 1.5 * eo(k), 0, TAU, .06 * (1 - k) + .01, P["light"], 1 - k, n=40, taper=False)
    for i in range(16):
        k = seg(t, .08, 1)
        ang = i * TAU / 16 + rng.uniform(-.15, .15)
        d = (.4 + rng.uniform(.8, 1.5)) * eo(k, 2.5)
        if 0 < k < 1:
            shard(B, math.cos(ang) * d, math.sin(ang) * d, ang, .25 * (1 - k) + .05, .05, P["hi"] if i % 2 else P["light"], 1 - k ** 1.5)


def proc_ice(t, B, G, P, rng):
    """눈꽃 결정: 여섯 갈래 결정이 탁 펼쳐졌다가 산산조각 나 흩어진다."""
    g = pop(t, 0, .2)
    G.glow(0, 0, 1.7 * g + .2, P["glow"], .5 * bump(t, .12, 1))
    shatter = seg(t, .5, 1)
    if shatter <= 0:
        for i in range(6):
            a = i * TAU / 6 + math.pi / 2
            dx, dy = math.cos(a), math.sin(a)
            L = 1.55 * g
            B.blade(0, 0, dx * L, dy * L, .16 * g, P["deep"], bias=.15)
            B.blade(0, 0, dx * L, dy * L, .1 * g, P["mid"], bias=.15)
            for f, bl in ((.45, .45), (.72, .32)):
                bx, by = dx * L * f, dy * L * f
                for sgn in (-1, 1):
                    b2 = a + sgn * .9
                    B.blade(bx, by, bx + math.cos(b2) * bl * g, by + math.sin(b2) * bl * g, .07 * g, P["mid"], bias=.2)
            B.blade(0, 0, dx * L * .8, dy * L * .8, .035 * g, P["hi"], bias=.2)
        B.poly([(math.cos(a) * .3 * g, math.sin(a) * .3 * g) for a in (i * TAU / 6 for i in range(6))], P["light"])
        B.blob(0, 0, .12 * g, P["hi"], rough=0)
    else:
        for i in range(24):
            a = i * TAU / 24 + rng.uniform(-.1, .1)
            d = rng.uniform(.3, 1.4) + 1.0 * eo(shatter, 2)
            spin = a + shatter * rng.uniform(-6, 6)
            L = .2 * (1 - shatter) + .06
            x, y = math.cos(a) * d, math.sin(a) * d
            B.blade(x - math.cos(spin) * L, y - math.sin(spin) * L, x + math.cos(spin) * L, y + math.sin(spin) * L, .07, P["mid"] if i % 2 else P["light"], 1 - shatter ** 1.3)
    k = seg(t, .45, .8)
    if 0 < k < 1:
        B.arc(0, 0, .4 + 1.4 * eo(k), 0, TAU, .05 * (1 - k) + .01, P["hi"], 1 - k, n=40, taper=False)


def proc_lightning(t, B, G, P, rng):
    """정전기 방출: 가운데에서 지그재그 번개 가지가 사방으로 뻗고, 끝끼리 전기 고리로 이어지며 깜빡인다."""
    frng = random.Random(int(t * 1000) * 11 + 1)
    s = bump(t, .08, .85)
    G.glow(0, 0, 1.9 * s + .2, P["glow"], .6 * s)
    flash = bump(t, .05, .3)
    if flash > .03:
        B.blob(0, 0, .5 * flash, P["light"], rough=.4, seed=frng.random() * 9)
        B.blob(0, 0, .28 * flash, P["hi"], rough=.3, seed=frng.random() * 9)
    ends = []
    for i in range(7):
        if s < .05:
            break
        a = i * TAU / 7 + frng.uniform(-.25, .25)
        r = (1.1 + .6 * frng.random()) * eo(seg(t, 0, .2))
        ex, ey = math.cos(a) * r, math.sin(a) * r
        ends.append((ex, ey))
        if frng.random() < .2 and t > .3:
            continue
        pts = bolt(frng, 0, 0, ex, ey, 7, .22)
        B.ribbon(pts, .11 * s + .03, P["deep"])
        B.ribbon(pts, .06 * s + .018, P["light"])
        B.ribbon(pts, .022 * s + .007, P["hi"])
        for _ in range(2):
            k = frng.randint(2, 5)
            x, y = pts[k]
            b = a + frng.choice((-1, 1)) * frng.uniform(.5, 1.0)
            br = bolt(frng, x, y, x + math.cos(b) * .4, y + math.sin(b) * .4, 3, .1)
            B.ribbon(br, .03 * s + .008, P["light"])
    ring = seg(t, .18, .7)
    if 0 < ring < 1 and len(ends) > 2:
        for (x0, y0), (x1, y1) in zip(ends, ends[1:] + ends[:1]):
            if frng.random() < .4:
                continue
            pts = bolt(frng, x0, y0, x1, y1, 5, .15)
            B.ribbon(pts, .035 * (1 - ring) + .01, P["light"], 1 - ring)
    for i in range(10):
        k = seg(t, .1, .9)
        a = frng.uniform(0, TAU)
        d = .5 + 1.3 * k
        if 0 < k < 1:
            B.blob(math.cos(a) * d, math.sin(a) * d, .04, P["hi"], 1 - k, rough=0)


def proc_light(t, B, G, P, rng):
    """성광: 여덟 갈래 빛살 별이 번쩍이며 돌고, 무늬 새긴 빛 고리가 퍼지며 반짝임이 흩어진다."""
    s = pop(t, 0, .2) * (1 - seg(t, .5, 1)) ** 1.3
    G.glow(0, 0, 1.9 * s + .3, P["glow"], .55 * bump(t, .1, 1))
    rot = .5 * t
    if s > .03:
        for i in range(4):
            a = rot + i * math.pi / 2
            B.blade(0, 0, math.cos(a) * 1.85 * s, math.sin(a) * 1.85 * s, .2 * s, P["mid"], bias=.08)
            B.blade(0, 0, math.cos(a) * 1.7 * s, math.sin(a) * 1.7 * s, .12 * s, P["light"], bias=.08)
            B.blade(0, 0, math.cos(a) * 1.4 * s, math.sin(a) * 1.4 * s, .045 * s, P["hi"], bias=.08)
            a2 = a + math.pi / 4
            B.blade(0, 0, math.cos(a2) * 1.0 * s, math.sin(a2) * 1.0 * s, .12 * s, P["light"], bias=.1)
        B.blob(0, 0, .3 * s, P["hi"], rough=.05)
    k = seg(t, .05, .75)
    if 0 < k < 1:
        r = .5 + 1.2 * eo(k)
        for j in range(12):    # 끊긴 무늬 고리
            a0 = j * TAU / 12 - rot * 2
            B.arc(0, 0, r, a0, a0 + TAU / 12 * .7, .07 * (1 - k) + .015, P["light"], 1 - k, n=6)
    for i in range(12):
        k = seg(t, .1, 1)
        a = i * TAU / 12 + .26
        d = .6 + 1.2 * eo(k, 2)
        if 0 < k < 1:
            sparkle(B, math.cos(a) * d, math.sin(a) * d, .14 * math.sin(math.pi * k) + .02, P["hi"], 1, rot=k * 3)


def proc_dark(t, B, G, P, rng):
    """어둠 수렴: 그림자 가시가 한 점으로 빨려 들었다가, 검은 구멍이 터지며 보랏빛 가시 고리가 뻗는다."""
    G.glow(0, 0, 1.6, P["glow"], .45 * bump(t, .35, 1))
    if t < .35:
        k = eo(seg(t, 0, .35), 2)
        for i in range(10):
            a = i * TAU / 10 + .3
            r0 = 1.8 - 1.4 * k
            B.blade(math.cos(a) * r0, math.sin(a) * r0, math.cos(a) * (r0 + .5), math.sin(a) * (r0 + .5), .1, P["mid"], bias=.15)
            B.blade(math.cos(a) * r0, math.sin(a) * r0, math.cos(a) * (r0 + .3), math.sin(a) * (r0 + .3), .04, P["hi"], bias=.15)
        B.blob(0, 0, .2 + .15 * k, P["deep"], rough=.3, seed=t * 9)
    else:
        k = seg(t, .35, 1)
        v = eo(seg(t, .35, .55)) * (1 - seg(t, .7, 1))
        B.blob(0, 0, .95 * v, P["light"], rough=.25, seed=3)
        B.blob(0, 0, .8 * v, mixc(P["deep"], INK, .5), rough=.25, seed=4)
        for i in range(12):
            a = i * TAU / 12 + rng.uniform(-.1, .1)
            L = (1.0 + .7 * (i % 2)) * eo(seg(k, 0, .35))
            th = 1 - seg(k, .4, 1)
            if th <= 0:
                continue
            r0 = .5 * v
            B.blade(math.cos(a) * r0, math.sin(a) * r0, math.cos(a) * (r0 + L), math.sin(a) * (r0 + L), .16 * th, P["deep"], bias=.2)
            B.blade(math.cos(a) * r0, math.sin(a) * r0, math.cos(a) * (r0 + L * .85), math.sin(a) * (r0 + L * .85), .07 * th, P["light"], bias=.2)
        if k < .8:
            B.arc(0, 0, .5 + 1.4 * eo(k), 0, TAU, .05 * (1 - k), P["hi"], 1 - k, n=40, taper=False)


def proc_blood(t, B, G, P, rng):
    """핏빛 할퀴기: 굵은 초승달 할퀴기 세 줄이 차례로 크게 그어지고, 가운데 핏물이 터지며 베인 결 양옆으로 핏방울이 뿌려진다."""
    G.glow(0, 0, 1.8, P["glow"], .45 * bump(t, .15, 1))
    fade = 1 - seg(t, .6, 1)
    for j in range(3):
        d = j * .07
        head = eo(seg(t, d, d + .2), 3)
        tail = eo(seg(t, d + .22, d + .75), 2)
        off = (j - 1) * .45
        if head - tail < .01 or fade <= 0:
            continue
        cx, cy = -1.7 + off * .8, 1.7 - off * .2
        a0, a1 = math.radians(-95), math.radians(-20)
        for col, wmul in ((P["deep"], 1), (P["mid"], .6), (P["hi"], .2)):
            pts, ws = [], []
            for k in range(29):
                u = k / 28
                ang = a0 + (a1 - a0) * (tail + (head - tail) * u)
                pts.append((cx + math.cos(ang) * 2.4, cy + math.sin(ang) * 2.4))
                ws.append(.24 * wmul * u ** .7 * (1 - u) ** .25 * 1.6)
            B.ribbon(pts, ws, col, fade)
            if wmul == 1:
                G.ribbon(pts, [w * 2.5 for w in ws], P["glow"], .3 * fade, edge=0)
    sp = bump(t, .18, .55)
    if sp > .03:
        pts = []
        for i in range(16):
            a = i * TAU / 16
            rr = (.75 if i % 2 == 0 else .35) * sp * (1 + .3 * math.sin(i * 4.1))
            pts.append((math.cos(a) * rr, math.sin(a) * rr))
        B.poly(pts, P["deep"])
        B.blob(0, 0, .3 * sp, P["mid"], rough=.3, seed=2)
    for i in range(22):
        k = seg(t, .15 + rng.uniform(0, .08), 1)
        if k <= 0 or k >= 1:
            continue
        a = math.radians(45) + (math.pi if i % 2 else 0) + rng.uniform(-.6, .6)
        d = rng.uniform(.4, 1.8) * eo(k, 2)
        x, y = math.cos(a) * d, math.sin(a) * d - .5 * k * k
        drop(B, x, y, math.cos(a), math.sin(a) - k, .05 + .05 * rng.random(), P["mid"] if i % 3 else P["light"], 1 - k ** 1.5, .15)


def proc_wind(t, B, G, P, rng):
    """바람 칼날: 초승달 칼날 셋이 빠르게 돌며 퍼지고, 칼바람 결이 스친다."""
    G.glow(0, 0, 1.5, P["glow"], .35 * bump(t, .15, 1))
    rot = -TAU * 1.4 * eo(t, 1.6)
    r = .35 + 1.15 * eo(seg(t, 0, .7), 2)
    fade = 1 - seg(t, .6, 1)
    for k in range(3):
        a = rot + k * TAU / 3
        B.arc(0, 0, r, a, a + 2.1, .26 * fade + .02, P["deep"], fade, n=22)
        B.arc(0, 0, r * .97, a + .25, a + 2.0, .14 * fade + .01, P["light"], fade, n=18)
        B.arc(0, 0, r * .93, a + .6, a + 1.9, .05 * fade + .005, P["hi"], fade, n=14)
        B.arc(0, 0, r * .7, a + .9, a + 2.3, .03, P["hi"], fade * .8, n=12)
    for i in range(8):
        k = seg(t, .05 + i * .03, .7 + i * .03)
        if 0 < k < 1:
            y = (i - 3.5) * .4
            x = -1.8 + 3.6 * eo(k)
            B.blade(x - .7, y, x, y + .05, .04, P["hi"], 1 - k, bias=.8)
    for i in range(8):
        k = seg(t, .1, 1)
        a = i * TAU / 8 + rot * .5
        d = .4 + 1.2 * k
        if 0 < k < 1:
            leaf(B, math.cos(a) * d, math.sin(a) * d, a * 2, .09, P["mid"], P["hi"], 1 - k)


def proc_poison(t, B, G, P, rng):
    """독 포자 폭발: 가시 돋친 독 덩이가 팍 터져 꼬리 달린 독방울이 사방으로 뻗고, 거품이 터진다."""
    G.glow(0, 0, 1.7, P["glow"], .5 * bump(t, .12, 1))
    s = pop(t, 0, .18) * (1 - seg(t, .4, .75))
    if s > .03:
        pts = []
        for i in range(24):
            a = i * TAU / 24
            rr = (1.25 if i % 2 == 0 else .55) * s * (1 + .2 * math.sin(i * 5.3))
            pts.append((math.cos(a) * rr, math.sin(a) * rr))
        B.poly(pts, P["deep"])
        B.poly([(x * .78, y * .78) for x, y in pts], P["mid"])
        B.blob(0, 0, .45 * s, P["light"], rough=.3, seed=2)
        B.blob(.12, .12, .15 * s, P["hi"], rough=.1)
    for i in range(14):
        k = seg(t, .12, 1)
        if 0 < k < 1:
            a = i * TAU / 14 + .1
            d = .5 + 1.3 * eo(k, 2)
            drop(B, math.cos(a) * d, math.sin(a) * d, math.cos(a) * 3, math.sin(a) * 3, .07 * (1 - k * .5), P["light"] if i % 2 else P["mid"], 1 - k ** 1.4, .12)
    for i in range(6):
        k = seg(t, .3 + i * .05, .65 + i * .05)
        a = i * 1.9
        x, y = math.cos(a) * .8, math.sin(a) * .8
        if 0 < k < .8:
            B.blob(x, y, .08 + .12 * k, P["light"], rough=.05)
            B.blob(x + .05, y + .05, .04, P["hi"], rough=0)
        elif .8 <= k < 1:
            kk = seg(k, .8, 1)
            B.arc(x, y, .25 + .1 * kk, 0, TAU, .03 * (1 - kk), P["hi"], 1 - kk, n=16, taper=False)


# ================================================================ 3. 움직이는 맵 장식 (반복 16칸 10fps, 바닥 피벗)
WAX, WAX_D = lin(hexc("#cbbd9c")), lin(hexc("#8a7c63"))
IRON, IRON_L = lin(hexc("#3d3936")), lin(hexc("#6e6860"))
COPPER = lin(hexc("#9a6a42"))


def decor_fire(t, B, G, P, rng):
    """촛불 무리: 촛농이 흘러내린 초 다섯 자루에서 불꽃이 일렁이고, 불씨가 오른다."""
    G.glow(0, GY + 1.2, 1.7, P["glow"], .35 + .02 * math.sin(TAU * t * 2))
    G.glow(0, GY, 1.4, P["glow"], .3, sy=PY)
    B.blob(0, GY, 1.0, mixc(WAX_D, INK, .3), .9, sy=PY * .8, rough=.25, seed=3)   # 녹아 고인 촛농
    B.blob(-.1, GY + .03, .75, WAX_D, .9, sy=PY * .7, rough=.3, seed=4)
    candles = [(-.6, .55, .13, .1), (.55, .7, .15, .3), (-.2, 1.25, .17, .5), (.2, .95, .16, .7), (-.85, .35, .11, .9)]
    for x, h, w, ph in sorted(candles, key=lambda c: -c[1]):
        y0 = GY + (.08 if x in (-.2, .2) else -.05)
        B.poly([(x - w, y0), (x + w, y0), (x + w, y0 + h), (x + w * .3, y0 + h + .03), (x - w, y0 + h - .02)], WAX)
        B.blade(x + w * .6, y0 + h, x + w * .6, y0 + h * .4, .04, WAX_D, bias=.2)   # 흘러내린 촛농
        B.blade(x - w * .5, y0 + h, x - w * .5, y0 + h * .65, .035, WAX_D, bias=.2)
        B.blob(x - w * .45, y0 + h * .4, .04, WAX, rough=.1)
        B.blade(x, y0 + h - .02, x + .01, y0 + h + .1, .02, INK, bias=.5)          # 심지
        fl = .92 + .05 * math.sin(TAU * (t * 2 + ph)) + .025 * math.sin(TAU * (t * 3 + ph * 2))   # 21차: 덜 일렁이게 (.12/.06 → .05/.025)
        sway = .025 * math.sin(TAU * (t + ph)) + .012 * math.sin(TAU * (t * 2 + ph * 3))
        G.glow(x, y0 + h + .25, .45, P["glow"], .4)
        fire_tongue(B, P, x, y0 + h + .03, 0, 1, .5 * fl, sway, .14, ph=ph * TAU)
    for i in range(5):
        ph = rng.random()
        k = (t + ph) % 1
        x = rng.uniform(-.6, .6) + .12 * math.sin(TAU * k + i)
        B.blob(x, GY + 1.4 + 1.0 * k, .03, P["hi"], math.sin(math.pi * k), rough=0)


def decor_ice(t, B, G, P, rng):
    """눈발과 서리 김: 언 바닥에 서리 결정이 돋고, 눈송이가 흩날려 내리며 바닥에 김이 서린다."""
    G.glow(0, GY + .2, 1.6, P["glow"], .25, sy=PY)
    B.blob(0, GY, 1.25, P["mid"], .5, sy=PY, rough=.12, seed=1)
    B.blob(-.05, GY + .02, .95, P["light"], .55, sy=PY, rough=.14, seed=2)
    B.blob(-.3, GY + .06, .45, P["hi"], .5, sy=PY * .5, rough=.2, seed=3)
    for i in range(7):     # 서리 결정
        ang = rng.uniform(0, TAU)
        r = math.sqrt(rng.random()) * 1.0
        x, y = math.cos(ang) * r, GY + math.sin(ang) * r * PY
        h = rng.uniform(.15, .35)
        B.blade(x, y, x - .05, y + h, .07, P["mid"], bias=.2)
        B.blade(x + .05, y, x + .14, y + h * .6, .05, P["mid"], bias=.2)
        B.blade(x, y, x - .03, y + h * .7, .025, P["hi"], bias=.2)
    for i in range(5):     # 서리 김 (옅게, 외곽 없이)
        k = (t + i / 5) % 1
        x = -1.5 + 3.0 * ((i * .37 + t * .5) % 1)
        B.blob(x, GY + .1 + .2 * k, .3 + .15 * k, P["hi"], .27 * math.sin(math.pi * k), rough=.35, seed=i, soft=True)
    for i in range(16):    # 눈송이
        ph = rng.random()
        k = (t + ph) % 1
        x = rng.uniform(-1.8, 1.8) + .2 * math.sin(TAU * (k * 1 + ph))
        y = 2.1 - 3.2 * k
        if y < GY + rng.uniform(-.3, .3):
            continue
        r = rng.uniform(.04, .08)
        if i % 3 == 0:
            sparkle(B, x, y, r * 1.6, P["hi"], 1, rot=TAU * k)
        else:
            B.blob(x, y, r, P["hi"], 1, rough=.15, seed=i)


def decor_lightning(t, B, G, P, rng):
    """번개 맞은 잔해: 부서진 철판과 구리 막대 더미에서 전기 불꽃이 튀며 깜빡인다."""
    i = int(round(t * 16))
    frng = random.Random(i * 31 + 7)
    on = i % 4 != 3
    G.glow(0, GY + .5, 1.3, P["glow"], (.35 if on else .15))
    B.blob(0, GY, 1.2, mixc(IRON, INK, .4), .85, sy=PY * .7, rough=.3, seed=2)   # 그을음
    plates = [(-.7, .05, -.4, .5, .35), (.6, .0, .5, .45, .3), (-.1, .1, .2, .6, .4), (.15, -.05, -.25, .55, .28)]
    for x, y, rot, w, h in plates:
        c, s = math.cos(rot), math.sin(rot)
        pts = [(-w, 0), (w, 0), (w * .8, h), (-w * .6, h * 1.1)]
        B.poly([(x + px * c - py * s, GY + y + px * s + py * c) for px, py in pts], IRON if rot < 0 else IRON_L)
        B.blade(x - w * .5 * c, GY + y + .05, x + w * .5 * c, GY + y + h * .5, .02, INK, .8)   # 금
    B.blade(.25, GY + .1, .45, GY + 1.25, .09, COPPER, bias=.5)        # 구리 막대
    B.blade(-.35, GY + .1, -.55, GY + .95, .07, COPPER, bias=.5)
    B.blob(.45, GY + 1.25, .08, IRON_L, rough=.1)
    tips = [(.45, GY + 1.25), (-.55, GY + .95), (-.7, GY + .45), (.6, GY + .4), (0, GY + .65)]
    if on:
        for _ in range(frng.randint(2, 3)):
            a, b = frng.sample(tips, 2)
            pts = bolt(frng, a[0], a[1], b[0], b[1], 7, .2)
            G.ribbon(pts, .18, P["glow"], .35, edge=0)
            B.ribbon(pts, .05, P["deep"])
            B.ribbon(pts, .028, P["light"])
            B.ribbon(pts, .01, P["hi"])
        x, y = tips[frng.randrange(len(tips))]
        B.blob(x, y, .1, P["light"], rough=.4, seed=frng.random() * 9)
        B.blob(x, y, .05, P["hi"], rough=.2)
    for j in range(8):     # 튀는 불똥 (위상 고리)
        ph = rng.random()
        k = (t * 2 + ph) % 1
        x0, y0 = tips[j % len(tips)]
        x, y, vx, vy = ballistic(x0, y0, rng.uniform(-1.2, 1.2), rng.uniform(.8, 1.8), k * .5, 3)
        shard(B, x, y, math.atan2(vy, vx), .08, .025, P["hi"], 1 - k)


def decor_light(t, B, G, P, rng):
    """떠도는 빛 알갱이: 바닥의 옅은 빛 웅덩이에서 빛 알갱이가 반짝이며 천천히 떠오른다."""
    G.glow(0, GY, 1.6, P["glow"], .3 + .05 * math.sin(TAU * t), sy=PY)
    G.glow(0, GY + .8, 1.2, P["glow"], .18)
    for i in range(14):
        ph = rng.random()
        k = (t + ph) % 1
        x = rng.uniform(-1.3, 1.3) * (1 - .3 * k) + .2 * math.sin(TAU * (k + ph * 2))
        y = GY + .05 + 2.8 * k
        tw = .55 + .45 * math.sin(TAU * (t * 2 + ph * 3))
        a = math.sin(math.pi * k) ** .6
        r = (.05 + .05 * rng.random()) * tw
        G.glow(x, y, r * 5, P["glow"], .3 * a)
        if i % 3 == 0:
            sparkle(B, x, y, r * 2.2, P["hi"], a, rot=TAU * k * .5)
        else:
            B.blob(x, y, r, P["hi"] if i % 2 else P["light"], a, rough=.1, seed=i)


def decor_dark(t, B, G, P, rng):
    """깔리는 연기: 검보랏빛 연기 덩이가 바닥을 따라 굴러가며 번지고, 가는 연기가 휘감아 오른다."""
    G.glow(0, GY, 1.5, P["glow"], .2, sy=PY)
    blobs = []
    for i in range(14):
        ph = i / 14 + rng.uniform(0, .05)
        k = (t * .5 + ph) % 1          # 왼쪽 → 오른쪽으로 굴러감
        x = -1.9 + 3.8 * k
        y = GY + rng.uniform(-.45, .45) * PY
        r = (.3 + .25 * rng.random()) * math.sin(math.pi * k) ** .5
        blobs.append((y, x, r, i, k))
    for y, x, r, i, k in sorted(blobs, reverse=True):
        a = math.sin(math.pi * k) ** .7
        B.blob(x, y + r * .3, r, P["deep"], .9 * a, sy=.7, rough=.3, seed=i + t * 2)
        B.blob(x - r * .15, y + r * .5, r * .65, P["mid"], .8 * a, sy=.7, rough=.35, seed=i + 3)
        B.blob(x - r * .3, y + r * .7, r * .25, P["light"], .6 * a, sy=.7, rough=.3, seed=i + 5)
    for i in range(4):
        k = (t + i / 4) % 1
        x0 = (i - 1.5) * .8
        pts = [(x0 + .25 * math.sin(u * 4 + TAU * t + i) * u, GY + .2 + (.6 + 1.4 * k) * u) for u in (j / 12 for j in range(13))]
        B.ribbon(pts, [.07 * (1 - j / 12) + .01 for j in range(13)], P["mid"], [.7 * math.sin(math.pi * k) * (1 - j / 13) for j in range(13)])


def decor_blood(t, B, G, P, rng):
    """핏방울 웅덩이: 위에서 핏방울이 똑똑 떨어져 웅덩이에 왕관처럼 튀고, 물결이 번진다."""
    G.glow(0, GY, 1.4, P["glow"], .2, sy=PY)
    B.blob(0, GY, 1.3, P["deep"], .95, sy=PY, rough=.2, seed=1)
    B.blob(-.05, GY + .02, 1.05, P["mid"], .9, sy=PY, rough=.22, seed=2)
    B.blob(-.35, GY + .15, .4, P["light"], .7, sy=PY * .45, rough=.3, seed=3)
    B.blob(.3, GY - .12, .2, P["hi"], .5, sy=PY * .4, rough=.3, seed=4)
    for j, (dx, ph) in enumerate(((-.35, 0), (.4, .5))):
        k = (t + ph) % 1
        hit = .45
        x, y = dx, GY + .05
        if k < hit:             # 떨어지는 방울
            u = k / hit
            yy = 2.2 - (2.2 - y) * u * u
            drop(B, x, yy, 0, -1, .07, P["mid"], 1, .12 * u)
        else:                   # 튀는 왕관 + 물결
            u = seg(k, hit, 1)
            for ring in range(2):
                rr = seg(u, ring * .2, 1)
                if 0 < rr < 1:
                    B.arc(x, y, .1 + .6 * eo(rr), 0, TAU, .03 * (1 - rr) + .005, P["light"], 1 - rr, sy=PY, n=28, taper=False)
            cu = seg(u, 0, .45)
            if 0 < cu < 1:
                for c in range(5):
                    a = math.pi * (c + .5) / 5
                    xx, yy, vx, vy = ballistic(x, y, math.cos(a) * .9, .9 + .5 * math.sin(a), cu * .45, 3)
                    drop(B, xx, yy, vx, vy, .035, P["mid"], 1 - cu, .05)


def decor_wind(t, B, G, P, rng):
    """소용돌이 먼지와 잎: 작은 회오리가 돌며 먼지를 말아 올리고 잎이 감겨 돈다."""
    DUST = lin(hexc("#a3988a"))
    rot = TAU * t * 2
    G.glow(0, GY + .7, 1.1, P["glow"], .15)
    for i in range(8):
        ang = i * TAU / 8 + rot * .3
        r = .55 + .15 * math.sin(i * 2.1 + TAU * t)
        B.blob(math.cos(ang) * r, GY + math.sin(ang) * r * PY, .2, DUST, .27, rough=.35, seed=i, soft=True)
    for k in range(7):
        y = GY + .1 + k * .3
        r = .2 + k * .12
        a0 = rot * (1 + k * .1) + k * 1.1
        B.arc(0, y, r, a0, a0 + math.pi * 1.2, .035 + .006 * k, P["mid"] if k % 2 else P["light"], .85, sy=.3, n=18)
        B.arc(0, y, r * .9, a0 + math.pi, a0 + math.pi * 1.8, .025, P["hi"], .7, sy=.3, n=12)
    for i in range(7):
        ph = i / 7
        k = (t + ph) % 1
        ang = ph * TAU + rot * 1.2
        h = .1 + k * 2.0
        r = .3 + h * .35
        x, y = math.cos(ang) * r, GY + h + math.sin(ang) * r * .3
        a = math.sin(math.pi * k) ** .4
        col = (P["deep"], P["mid"], lin(hexc("#8a6a3e")))[i % 3]
        leaf(B, x, y, ang * 1.5 + k * 4, .1, col, P["hi"], a)
    for i in range(6):
        ph = rng.random()
        k = (t + ph) % 1
        ang = ph * TAU + rot
        r = .4 + .3 * k
        B.blob(math.cos(ang) * r, GY + .2 + 1.4 * k, .04, DUST, .8 * math.sin(math.pi * k), rough=.2)


def decor_poison(t, B, G, P, rng):
    """끓는 거품 웅덩이: 독 웅덩이에서 거품이 부풀었다 터지고, 독기가 피어오른다."""
    G.glow(0, GY + .1, 1.5, P["glow"], .3, sy=PY)
    B.blob(0, GY, 1.35, P["deep"], .95, sy=PY, rough=.18, seed=5)
    B.blob(-.05, GY + .02, 1.1, P["mid"], .9, sy=PY, rough=.22, seed=6)
    B.blob(-.35, GY + .14, .45, P["light"], .5, sy=PY * .45, rough=.3, seed=7)
    for i in range(9):
        ph = rng.random()
        k = (t + ph) % 1
        ang = rng.uniform(0, TAU)
        r = math.sqrt(rng.random()) * .95
        x, y = math.cos(ang) * r, GY + math.sin(ang) * r * PY
        big = .1 + .1 * rng.random()
        if k < .8:
            rr = .03 + big * (k / .8)
            B.blob(x, y + rr * .5, rr, P["light"], rough=.05, seed=i)
            B.blob(x + rr * .35, y + rr * .85, rr * .3, P["hi"], rough=0)
        else:
            kk = seg(k, .8, 1)
            B.arc(x, y + .05, big + .15 * kk, 0, TAU, .025 * (1 - kk) + .005, P["hi"], 1 - kk, sy=PY, n=20, taper=False)
            for j in range(4):
                a = j * TAU / 4 + .4
                B.blob(x + math.cos(a) * .18 * kk, y + .08 + .2 * math.sin(math.pi * kk) + math.sin(a) * .08 * kk, .025, P["light"], 1 - kk)
    for i in range(5):
        k = (t + i / 5) % 1
        x = (i - 2) * .45 + .2 * math.sin(TAU * k + i)
        B.blob(x, GY + .2 + 1.8 * k, .16 + .22 * k, P["light"], .27 * math.sin(math.pi * k), rough=.3, seed=i, soft=True)


# ================================================================ 4. 결박 폭발 (14칸 30fps, 바닥 피벗) — 어둠·피·바람·독
BC = GY + 1.0   # 결박된 적 몸 가운데 높이


def bind_dark(t, B, G, P, rng):
    """어둠 결박: 바닥 그림자에서 촉수 여섯이 나선으로 솟아 적을 휘감아 조이고, 보랏빛 가시가 터진다."""
    G.glow(0, BC, 1.6, P["glow"], .45 * bump(t, .2, 1))
    fade = 1 - seg(t, .55, .95)
    B.blob(0, GY, .35 + .8 * eo(seg(t, 0, .25)), mixc(P["deep"], INK, .3), .8 * fade, sy=PY, rough=.15, seed=2)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .06, col="light")
    grow = eo(seg(t, 0, .35), 2)
    sq = eo(seg(t, .3, .55))          # 조이기
    for i in range(6):
        a0 = i * TAU / 6
        n = 20
        pts, ws = [], []
        for k in range(n + 1):
            u = k / n * grow
            ang = a0 + u * 3.2
            rad = (1.0 - .55 * u) * (1 - .35 * sq * u)
            pts.append((math.cos(ang) * rad, GY + u * 2.0 + math.sin(ang) * rad * PY * .6))
            ws.append(.15 * (1 - k / n) ** .6 * (.4 + .6 * fade) + .01)
        front = [math.sin(a0 + k / n * grow * 3.2) < 0 for k in range(n + 1)]
        B.ribbon(pts, ws, P["deep"] if front[n // 2] else mixc(P["deep"], INK, .4), fade)
        B.ribbon(pts, [w * .45 for w in ws], P["mid"], fade)
        B.ribbon(pts, [w * .15 for w in ws], P["hi"], .8 * fade)
    s = bump(t, .5, .9)
    if s > .03:
        for i in range(10):
            a = i * TAU / 10 + .2
            L = (.9 + .5 * (i % 2)) * s
            B.blade(math.cos(a) * .2, BC + math.sin(a) * .2, math.cos(a) * L, BC + math.sin(a) * L, .1 * s, P["light"], bias=.15)
        B.blob(0, BC, .3 * s, mixc(P["deep"], INK, .5), rough=.3)
    for i in range(10):
        k = seg(t, .4, 1)
        if 0 < k < 1:
            a = rng.uniform(0, TAU)
            d = .4 + 1.3 * eo(k)
            B.blob(math.cos(a) * d, BC + math.sin(a) * d * .8, .05, P["hi"], 1 - k, rough=0)


def bind_blood(t, B, G, P, rng):
    """피 결박: 발밑에서 핏빛 가시 왕관이 솟구치고, 가운데 핏줄기가 터져 핏방울이 쏟아진다."""
    G.glow(0, BC, 1.5, P["glow"], .4 * bump(t, .15, 1))
    fade = 1 - seg(t, .75, 1)
    B.blob(0, GY, .35 + 1.0 * eo(seg(t, 0, .3)), P["deep"], .95 * fade, sy=PY, rough=.14, seed=3)
    B.blob(-.1, GY + .03, .25 + .85 * eo(seg(t, 0, .3)), P["mid"], .9 * fade, sy=PY, rough=.3, seed=4)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .06, col="light")
    spikes = []
    for i in range(10):
        ang = i * TAU / 10 + rng.uniform(-.15, .15)
        r = rng.uniform(.5, .9)
        spikes.append((math.sin(ang), ang, r, rng.uniform(.9, 1.7), rng.uniform(0, .08)))
    for _, ang, r, h, d in sorted(spikes, reverse=True):
        g = pop(t, d, d + .15) * (1 - seg(t, .6, .9))
        if g < .02:
            continue
        bx, by = math.cos(ang) * r, GY + math.sin(ang) * r * PY
        tx, ty = bx * (1 - .35 * g), by + h * g
        B.blade(bx * 1.08, by - .05, tx, ty, .16 * g, P["deep"], bias=.2)
        B.blade(bx * 1.02, by, tx, ty, .1 * g, P["mid"], bias=.2)
        B.blade(bx, by + .05, tx * .9 + bx * .1, ty - .1 * g, .035 * g, P["hi"], .8, bias=.25)
    col = eo(seg(t, .05, .25)) * (1 - seg(t, .45, .75))
    if col > .02:
        B.blade(0, GY, 0, GY + 2.0 * col, .3 * col, P["deep"], bias=.3)
        B.blade(.03, GY, .03, GY + 1.7 * col, .18 * col, P["mid"], bias=.3)
        B.blob(0, GY + 2.0 * col, .2 * col, P["mid"], rough=.3)
    for i in range(20):
        s = seg(t, .15 + rng.uniform(0, .1), 1) * .8
        if s <= 0:
            continue
        x, y, vx, vy = ballistic(rng.uniform(-.15, .15), GY + 1.7, rng.uniform(-1.8, 1.8), rng.uniform(.5, 2.2), s, 3.4)
        if y < GY:
            continue
        drop(B, x, y, vx, vy, .045 + .035 * rng.random(), P["mid"] if i % 3 else P["light"], 1 - seg(s, .55, .8))


def bind_wind(t, B, G, P, rng):
    """바람 결박: 적 둘레로 회오리 고리가 빠르게 감겨 오르며 조이고, 바람 칼날이 엇갈려 벤다."""
    G.glow(0, BC, 1.4, P["glow"], .35 * bump(t, .2, 1))
    grow = eo(seg(t, 0, .25), 2)
    fade = 1 - seg(t, .7, 1)
    sq = 1 - .35 * eo(seg(t, .25, .55))
    rot = TAU * t * 3
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .05, col="light")
    for i in range(8):
        ang = i * TAU / 8
        k = seg(t, 0, .7)
        r = .5 + 1.2 * eo(k)
        B.blob(math.cos(ang + k) * r, GY + math.sin(ang + k) * r * PY, .18 + .14 * k, lin(hexc("#a3988a")), .27 * (1 - k), rough=.35, seed=i, soft=True)
    for half in (0, 1):
        for k in range(8):
            y = GY + .05 + k * .27 * grow
            r = (.35 + .5 * math.sin(math.pi * (k + .5) / 8)) * sq
            a0 = rot * (1 + k * .1) + k * .8 + half * math.pi
            col = P["mid"] if half == 0 else (P["light"] if k % 2 else P["hi"])
            B.arc(0, y, r, a0, a0 + math.pi * (1.0 if half == 0 else 1.1), .05 + .01 * k, col, fade, sy=.32, n=18)
    for j in range(2):     # 엇갈려 베는 칼바람
        k = seg(t, .3 + j * .12, .6 + j * .12)
        if 0 < k < 1:
            a = math.radians(30 if j == 0 else 150)
            L = 1.7
            cx, cy = 0, BC
            hx = cx - math.cos(a) * L + 2 * math.cos(a) * L * eo(k)
            hy = cy - math.sin(a) * L + 2 * math.sin(a) * L * eo(k)
            tx = cx - math.cos(a) * L + 2 * math.cos(a) * L * eo(seg(k, .25, 1))
            ty = cy - math.sin(a) * L + 2 * math.sin(a) * L * eo(seg(k, .25, 1))
            B.blade(tx, ty, hx, hy, .12, P["mid"], bias=.8)
            B.blade(tx, ty, hx, hy, .05, P["hi"], bias=.8)
    for i in range(8):
        ph = rng.random()
        k = seg(t, ph * .3, 1)
        ang = ph * TAU + rot * 1.3
        h = k * 2.4
        r = (.4 + .3 * k) * sq
        if 0 < k < 1:
            leaf(B, math.cos(ang) * r, GY + h + math.sin(ang) * r * .3, ang * 2, .1, P["deep"] if i % 2 else P["mid"], P["hi"], 1 - k ** 2)


def bind_poison(t, B, G, P, rng):
    """독 결박: 발밑 독 웅덩이에서 독액 기둥이 솟구쳐 적을 삼키고, 꼭대기에서 왕관처럼 터져 흩뿌려진다."""
    G.glow(0, BC, 1.6, P["glow"], .45 * bump(t, .2, 1))
    fade = 1 - seg(t, .75, 1)
    B.blob(0, GY, .4 + .95 * eo(seg(t, 0, .3)), P["deep"], .95 * fade, sy=PY, rough=.14, seed=5)
    B.blob(.05, GY + .03, .3 + .85 * eo(seg(t, 0, .3)), P["mid"], .9 * fade, sy=PY, rough=.3, seed=6)
    ground_ring(B, G, P, seg(t, 0, .5), .3, 1.8, .06, col="light")
    col = eo(seg(t, 0, .25), 2) * (1 - seg(t, .5, .8))
    if col > .02:
        H = 2.2 * col
        for k in range(9):     # 덩이가 쌓인 독액 기둥
            u = k / 8
            y = GY + H * u
            r = (.48 - .2 * u) * (.85 + .15 * math.sin(k * 2.3 + t * 12)) * col ** .5
            B.blob(math.sin(k * 1.7 + t * 8) * .06, y, r, P["deep"], rough=.15, seed=k)
        for k in range(9):
            u = k / 8
            y = GY + H * u
            r = (.34 - .15 * u) * col ** .5
            B.blob(math.sin(k * 1.7 + t * 8) * .06 - .04, y + .03, r, P["mid"], rough=.2, seed=k + 9)
        B.blade(-.12, GY + .1, -.1, GY + H * .9, .06 * col, P["light"], .9, bias=.5)
        for i in range(8):     # 꼭대기 왕관
            a = math.pi * (i + .5) / 8
            B.blade(0, GY + H, math.cos(a) * .7 * col, GY + H + math.sin(a) * .5 * col, .1 * col, P["mid"], bias=.3)
            B.blob(math.cos(a) * .7 * col, GY + H + math.sin(a) * .5 * col, .07 * col, P["light"], rough=.1)
    for i in range(22):
        s = seg(t, .3 + rng.uniform(0, .1), 1) * .8
        if s <= 0:
            continue
        x, y, vx, vy = ballistic(rng.uniform(-.3, .3), GY + 2.0, rng.uniform(-2, 2), rng.uniform(.4, 1.8), s, 3.4)
        if y < GY - .1:
            continue
        drop(B, x, y, vx, vy, .05 + .04 * rng.random(), P["light"] if i % 3 else P["hi"], 1 - seg(s, .55, .8))
    for i in range(6):
        k = seg(t, .1 + i * .05, .45 + i * .05)
        a = i * TAU / 6 + .3
        x, y = math.cos(a) * .9, GY + math.sin(a) * .9 * PY
        if 0 < k < .8:
            r = .05 + .12 * k
            B.blob(x, y + r * .5, r, P["light"], fade, rough=.05)
            B.blob(x + r * .35, y + r * .85, r * .3, P["hi"], fade, rough=0)
        elif .8 <= k < 1:
            kk = seg(k, .8, 1)
            B.arc(x, y + .1, .18 + .1 * kk, 0, TAU, .025 * (1 - kk), P["hi"], 1 - kk, n=16, taper=False)


# ================================================================ 시트 표
# name: (그룹, 함수, 속성, 칸 수, fps, 칸 px, 반복, 피벗, 외곽 px)
SHEETS = {}


def sheet(name, group, fn, el, frames, fps, size, loop, pivot, ink=2.6, wob=.02, still=False):
    """still: 칸마다 외곽과 모양이 들썩이지 않는다 (21차: 맵 장식이 너무 일렁거림 → 초와 웅덩이는 가만히, 불꽃과 알갱이만 움직임)"""
    SHEETS[name] = dict(group=group, fn=fn, element=el, frames=frames, fps=fps, size=size, loop=loop, pivot=pivot, ink=ink, wob=wob, still=still)


for el in ELEMENTS:
    sheet(f"boss_{el}_slam", "boss", globals()[f"slam_{el}"], el, 16, 24, 256, False, PIVOT_GROUND)
    sheet(f"boss_{el}_bullet", "boss", globals()[f"bullet_{el}"], el, 8, 16, 128, True, PIVOT_CENTER, ink=1.6, wob=.025)
sheet("boss_fire_pool", "boss", pool_fire, "fire", 12, 12, 256, True, (.5, .35))
sheet("boss_poison_pool", "boss", pool_poison, "poison", 12, 12, 256, True, (.5, .35))
for el in ELEMENTS:
    sheet(f"proc_{el}", "proc", globals()[f"proc_{el}"], el, 12, 24, 256, False, PIVOT_CENTER, ink=2.2)
for el in ELEMENTS:
    sheet(f"decor_{el}_loop", "decor", globals()[f"decor_{el}"], el, 16, 10, 256, True, PIVOT_GROUND, ink=2.4, wob=.015, still=True)
for el in ("dark", "blood", "wind", "poison"):
    sheet(f"bind_{el}_burst", "bind", globals()[f"bind_{el}"], el, 14, 30, 256, False, PIVOT_GROUND)


# ---------------------------------------------------------------- 렌더 + 잉크 후처리
def clear_fx():
    for obj in list(scene.collection.objects):
        if obj is not cam:
            mesh = obj.data
            bpy.data.objects.remove(obj)
            bpy.data.meshes.remove(mesh)


def render_layer(layer, size, path):
    if not layer.v:
        return np.zeros((size, size, 4), dtype=np.float32)
    clear_fx()
    layer.build(path.stem)
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(str(path))
    px = np.empty(size * size * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(size, size, 4)   # 아래→위 행 순서, sRGB 값, 곧은 알파


def smooth_noise(h, w, cell, rng):
    gh, gw = h // cell + 2, w // cell + 2
    g = rng.random((gh, gw)).astype(np.float32)
    ys, xs = np.arange(h) / cell, np.arange(w) / cell
    y0, x0 = ys.astype(int), xs.astype(int)
    fy, fx = (ys - y0)[:, None], (xs - x0)[None, :]
    fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
    a, b = g[y0][:, x0], g[y0][:, x0 + 1]
    c, d = g[y0 + 1][:, x0], g[y0 + 1][:, x0 + 1]
    return a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy


def dilate(m, cross):
    p = np.pad(m, 1)
    r = p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    if not cross:
        r = r | p[:-2, :-2] | p[:-2, 2:] | p[2:, :-2] | p[2:, 2:]
    return r


def inked(px, radius, seed, grain=.06):
    """거친 잉크 외곽: 알파 .3 넘는 실루엣 둘레에 굵기가 들쭉날쭉한 검은 선을 깔고, 종이 결 얼룩을 살짝 얹는다."""
    h, w = px.shape[:2]
    a = px[..., 3]
    m = a > .3
    R = int(math.ceil(radius * 1.5)) + 1
    dist = np.where(m, 0, R + 2).astype(np.float32)
    cur = m
    for k in range(1, R + 1):
        cur = dilate(cur, k % 2 == 1)
        dist[cur & (dist > k)] = k
    rng = np.random.default_rng(seed)
    rad = radius * (.5 + 1.0 * smooth_noise(h, w, 9, rng))
    ink = np.clip(rad - dist + .5, 0, 1)
    oa = a + ink * (1 - a)
    rgb = px[..., :3]
    out = (rgb * a[..., None] + INK_SRGB[None, None, :] * (ink * (1 - a))[..., None]) / np.maximum(oa, 1e-4)[..., None]
    out *= (1 - grain * smooth_noise(h, w, 3, rng))[..., None]
    return np.dstack([np.clip(out, 0, 1), oa]).astype(np.float32)


def save(pixels, path):
    h, w = pixels.shape[:2]
    img = bpy.data.images.new(path.stem, width=w, height=h, alpha=True)
    img.pixels.foreach_set(pixels.ravel())
    img.filepath_raw = str(path)
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


# 메타는 시트 표에서 바로 나오므로 매번 전부 쓴다 (여러 프로세스로 나눠 돌려도 같은 내용)
meta = {name: dict(group=s["group"], element=s["element"], frames=s["frames"], size=s["size"], fps=s["fps"],
                   duration=round(s["frames"] / s["fps"], 3), loop=s["loop"], pivot=list(s["pivot"]))
        for name, s in SHEETS.items()}
(SRC / "sheets.json").write_text(json.dumps(meta, indent=1, ensure_ascii=False), encoding="utf-8")
for name, s in SHEETS.items():
    if ONLY and name not in ONLY and s["group"] not in ONLY:
        continue
    P = PAL[s["element"]]
    n, size = s["frames"], s["size"]
    bodies, glows = [], []
    for i in range(n):
        t = i / n if s["loop"] else i / (n - 1)
        boil = 0 if s["still"] else i if s["loop"] else i // 2   # 반복은 칸마다, 터짐은 두 칸마다(12fps 들썩임), still 은 고정
        B, G = Layer(s["wob"], boil), Layer(0, boil)
        fn = s["fn"]
        fn(t, B, G, P, random.Random(sum(map(ord, name)) * 31))
        body = render_layer(B, size, TMP / f"{name}_b{i:02d}.png")
        glow = render_layer(G, size, TMP / f"{name}_g{i:02d}.png")
        bodies.append(inked(body, s["ink"] * size / 256, seed=boil * 97 + 13))
        glow[..., 3] = np.minimum(glow[..., 3] * .9, .6)    # 겹쳐도 하얗게 날아가지 않게 상한
        glows.append(glow)
    save(np.concatenate(bodies, axis=1), OUT / f"{name}.png")
    save(np.concatenate(glows, axis=1), OUT / f"{name}_glow.png")
    print("vfx16", name, n, flush=True)

for f in TMP.glob("*.png"):   # 낱장 프레임은 시트에 다 들어갔으니 지운다
    f.unlink()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC / "element_vfx.blend"), compress=True)
