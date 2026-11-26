"""24차 바느질 타격 파티클 시트 (Blender 5.3) — 당기기로 실이 적을 지나가며 맞힌 자리에서 터지는 실밥·솜·천·단추·바늘 반짝임.

실행:
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b -P tools/render_hit_sparks.py -- C:/Project/TOD [시트 이름 ...]
  아무것도 안 주면 전부. 확인: python tools/preview_hit_sparks.py → Temp/Shots/w24_hitsparks_sheets.png

출력 (Assets/Art/VFX/24, 시트마다 두 장):
  sew_<이름>.png       본체. 흰색·밝은 회색 종이 컷아웃 + 거친 잉크 외곽. 게임에서 색을 곱해 물들인다 (보통 알파 합성).
  sew_<이름>_glow.png  빛. 흰 번짐만 (가산 합성, 본체보다 먼저). 게임에서 색을 곱하고 세기는 데이터(glow, bindGlow).
  형식: 가로 1줄 스트립, 정사각 칸 N장(왼쪽이 0번), 칸 수 = 폭 / 높이 (16차 시트와 같은 규약). 피벗은 모두 가운데.
  art_src/vfx/24/sheets.json   시트별 칸 수, 칸 px, fps, 반복 여부
  art_src/vfx/24/hit_sparks.blend  마지막으로 그린 칸의 편집용 씬

  burst  (256px, 10칸, 한 번)  맞은 자리 가운데 '툭' 터짐: 0번 칸이 가장 큰 종이 별 + 바늘땀 X (프레임 스톱 동안 이 칸이 보인다)
                               → 끊어진 홈질 땀이 방사형으로 퍼지며 가늘어지고 사라짐
  thread (96px, 8칸, 반복)     꼬불꼬불한 실밥 토막 (끝이 풀린 올), 돌면서 꼬임이 바뀜
  fluff  (96px, 8칸, 반복)     솜 뭉치 (덩이 여럿 + 가는 솜털), 천천히 돌며 덩이가 들썩임
  cloth  (96px, 8칸, 반복)     찢어진 천 쪼가리 (지그재그 찢긴 가장자리 + 홈질 한 줄), 낙엽처럼 뒤집히며 펄럭임
  button (96px, 8칸, 반복)     구멍 넷 단추, 세로축으로 돌아 납작해졌다 넓어짐
  glint  (96px, 8칸, 한 번)    바늘 반짝임: 가는 바늘 + 네 갈래 별이 톡 커졌다 돌며 작아짐
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
OUT = ROOT / "Assets/Art/VFX/24"
SRC = ROOT / "art_src/vfx/24"
TMP = SRC / "_frames"
for d in (OUT, SRC, TMP):
    d.mkdir(parents=True, exist_ok=True)

TAU = math.tau


def lin(v):
    return ((v + .055) / 1.055) ** 2.4 if v > .04045 else v / 12.92


def grey(v):
    """sRGB 밝기 v(0~1) → 선형 회색 (꼭짓점 색)."""
    g = lin(v)
    return (g, g, g)


WHITE, LIGHT, MID, SHADE, DARK = grey(1.0), grey(.9), grey(.78), grey(.64), grey(.42)
INK_SRGB = np.array((27 / 255, 21 / 255, 23 / 255), dtype=np.float32)   # 16차 시트와 같은 잉크 (#1b1517)


# ---------------------------------------------------------------- scene (render_element_vfx.py 와 같은 방식)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 12
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
    mat = bpy.data.materials.new("fx24")
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
    """한 칸·한 층의 기하. 꼭짓점마다 (선형 RGB, 알파). 나중에 그린 것이 위. xf = 모든 점에 거는 변환 (돌기, 뒤집기)."""

    def __init__(self, wob=0.0, boil=0):
        self.v, self.f, self.c = [], [], []
        self.z = 0.0
        self.wob, self.boil = wob, boil
        self.xf = None

    def _vert(self, x, y, col, a):
        if self.xf:
            x, y = self.xf(x, y)
        if self.wob:
            s = self.boil * 1.913
            x, y = (x + self.wob * math.sin(x * 4.3 + y * 2.9 + s),
                    y + self.wob * math.sin(x * 3.1 - y * 4.7 + s * 1.37))
        self.v.append((x, y, self.z))
        self.c.append((col[0], col[1], col[2], max(0.0, min(1.0, a))))
        return len(self.v) - 1

    def ribbon(self, pts, widths, col, alpha=1.0, edge=1.0):
        if len(pts) < 2:
            return
        n = len(pts)
        if not isinstance(widths, (list, tuple)):
            widths = [widths] * n
        self.z += 0.001
        ids = []
        for i, (x, y) in enumerate(pts):
            a = pts[max(0, i - 1)]
            b = pts[min(n - 1, i + 1)]
            tx, ty = b[0] - a[0], b[1] - a[1]
            ln = math.hypot(tx, ty) or 1
            nx, ny = -ty / ln, tx / ln
            w = max(0.0, widths[i])
            ids.append((self._vert(x + nx * w, y + ny * w, col, alpha * edge),
                        self._vert(x, y, col, alpha),
                        self._vert(x - nx * w, y - ny * w, col, alpha * edge)))
        for (l0, c0, r0), (l1, c1, r1) in zip(ids, ids[1:]):
            self.f += [(l0, c0, c1, l1), (c0, r0, r1, c1)]

    def blade(self, x0, y0, x1, y1, width, col, alpha=1.0, n=10, bias=.5):
        pts, ws = [], []
        for i in range(n + 1):
            u = i / n
            pts.append((x0 + (x1 - x0) * u, y0 + (y1 - y0) * u))
            prof = (u / bias) if u < bias else ((1 - u) / (1 - bias))
            ws.append(width * max(0.0, prof) ** .8)
        self.ribbon(pts, ws, col, alpha)

    def blob(self, x, y, r, col, alpha=1.0, sy=1.0, rough=.12, seed=0.0, seg=30, soft=False, rot=0.0):
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
        self.blob(x, y, r, col, alpha, sy=sy, rough=0, soft=True, seg=28)

    def poly(self, pts, col, alpha=1.0):
        if len(pts) < 3 or alpha <= 0.003:
            return
        self.z += 0.001
        cx = sum(p[0] for p in pts) / len(pts)
        cy = sum(p[1] for p in pts) / len(pts)
        c = self._vert(cx, cy, col, alpha)
        ring = [self._vert(x, y, col, alpha) for x, y in pts]
        for i in range(len(ring)):
            self.f.append((c, ring[i], ring[(i + 1) % len(ring)]))

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


def clamp(v, a=0.0, b=1.0):
    return max(a, min(b, v))


def seg(t, a, b):
    return clamp((t - a) / (b - a)) if b > a else float(t >= a)


def eo(t, p=3):
    t = clamp(t)
    return 1 - (1 - t) ** p


def rot_xf(ang, sx=1.0, sy=1.0):
    """돌기 + 한 축 눌림(뒤집힘). 점을 (sx, sy) 로 늘린 뒤 ang 만큼 돌린다."""
    c, s = math.cos(ang), math.sin(ang)
    return lambda x, y: (x * sx * c - y * sy * s, x * sx * s + y * sy * c)


def star(L, x, y, r, col, alpha=1.0, rot=0.0, points=4, inner=.22):
    pts = []
    for i in range(points * 2):
        a = rot + i * math.pi / points
        rr = r if i % 2 == 0 else r * inner
        pts.append((x + math.cos(a) * rr, y + math.sin(a) * rr))
    L.poly(pts, col, alpha)


def cross_stitch(L, x, y, s, ang, col, alpha=1.0, w=.05):
    for k in (1, -1):
        a = ang + k * math.pi / 4
        L.blade(x - math.cos(a) * s, y - math.sin(a) * s, x + math.cos(a) * s, y + math.sin(a) * s, w, col, alpha, n=6)


# ================================================================ 그림 함수 (t = 0~1, B = 본체, G = 빛, rng = 시트마다 고정)
def fx_burst(t, B, G, rng):
    """가운데 '툭' 터짐. 0번 칸 = 가장 큰 종이 별 (프레임 스톱 동안 보임) → 홈질 땀이 방사형으로 퍼지며 사라짐."""
    pop = 1 - eo(seg(t, 0, .45), 2)                  # 별 크기 1 → 0
    if pop > .02:
        r = 1.25 * (.5 + .5 * pop) if t > 0 else 1.4
        star(B, 0, 0, r, WHITE, 1, rot=.35 + t * .6, points=6, inner=.42)
        star(B, 0, 0, r * .55, LIGHT, 1, rot=.35 + t * .6 + math.pi / 6, points=6, inner=.5)
        G.glow(0, 0, r * 1.7, WHITE, .75 * pop)
    # 끊어진 홈질: 방사형 짧은 땀 줄 (안쪽에서 바깥으로, 땀마다 틈)
    spokes = 9
    k = eo(seg(t, 0, .9), 2)
    fade = 1 - seg(t, .55, 1)
    for i in range(spokes):
        a = i * TAU / spokes + rng.random() * .25
        r0 = .45 + 1.15 * k
        for j in range(3):
            rr = r0 + j * .26 * (1 + k * .5)
            if rr > 1.9:
                break
            w = .1 * (1 - .5 * k) * (1 - j * .2)
            x0, y0 = math.cos(a) * rr, math.sin(a) * rr
            x1, y1 = math.cos(a) * (rr + .15), math.sin(a) * (rr + .15)
            B.blade(x0, y0, x1, y1, w, WHITE if j == 0 else LIGHT, fade, n=6)
        if t < .5:
            G.glow(math.cos(a) * r0, math.sin(a) * r0, .25, WHITE, .35 * fade)
    # 날아가는 바늘땀 X 넷
    for i in range(4):
        a = i * TAU / 4 + .8
        rr = .7 + 1.0 * k
        if rr < 1.85:
            cross_stitch(B, math.cos(a) * rr, math.sin(a) * rr, .16 * (1 - .4 * k), a + t * 3, LIGHT, fade)


def fx_thread(t, B, G, rng):
    """꼬불꼬불한 실밥 토막: 돌면서 꼬임 위상이 바뀐다 (반복). 끝은 풀린 올 두세 가닥."""
    B.xf = rot_xf(t * TAU + .4)
    ph = t * TAU
    pts, ws = [], []
    n = 22
    for i in range(n + 1):
        u = i / n
        x = -1.35 + 2.7 * u
        y = .32 * math.sin(u * TAU * 1.25 + ph) + .12 * math.sin(u * TAU * 2.6 - ph * 2)
        pts.append((x, y))
        ws.append(.12 * (.75 + .25 * math.sin(math.pi * u)))
    B.ribbon(pts, ws, LIGHT)
    # 꼬임 결: 가는 밝은 사선
    for i in range(2, n - 1, 3):
        x, y = pts[i]
        B.blade(x - .06, y - .09, x + .06, y + .09, .03, WHITE, 1, n=4)
    for end, sgn in ((pts[0], -1), (pts[-1], 1)):
        for k in range(3):
            a = (k - 1) * .5 + (0 if sgn > 0 else math.pi) + .2 * math.sin(ph + k)
            L = .22 + .08 * k
            B.blade(end[0], end[1], end[0] + math.cos(a) * L, end[1] + math.sin(a) * L, .045, MID, 1, n=5, bias=.2)
    B.xf = None
    G.glow(0, 0, 1.7, WHITE, .55)


def fx_fluff(t, B, G, rng):
    """솜 뭉치: 덩이 여럿이 천천히 돌며 들썩이고, 가는 솜털이 삐져나온다 (반복)."""
    B.xf = rot_xf(t * TAU * .5)
    lumps = [(-.45, -.2, .62), (.35, -.28, .58), (0, .3, .66), (.62, .32, .42), (-.62, .38, .4), (.05, -.62, .4)]
    for i, (x, y, r) in enumerate(lumps):   # 아래 그늘
        rr = r * (1 + .06 * math.sin(t * TAU + i * 1.7))
        B.blob(x + .06, y - .08, rr, SHADE, 1, rough=.18, seed=i * 2.1 + t * 3)
    for i, (x, y, r) in enumerate(lumps):
        rr = r * (1 + .06 * math.sin(t * TAU + i * 1.7)) * .9
        B.blob(x, y, rr, LIGHT if i % 2 else WHITE, 1, rough=.2, seed=i * 3.3 + t * 2)
    for i, (x, y, r) in enumerate(lumps[:4]):   # 하이라이트
        B.blob(x - r * .25, y + r * .25, r * .35, WHITE, 1, rough=.1, seed=i)
    for k in range(7):   # 솜털
        a = k * TAU / 7 + .3 * math.sin(t * TAU + k)
        r0, r1 = .9, 1.45 + .15 * math.sin(t * TAU * 2 + k)
        mid = (math.cos(a + .2) * (r0 + r1) / 2, math.sin(a + .2) * (r0 + r1) / 2)
        B.ribbon([(math.cos(a) * r0, math.sin(a) * r0), mid, (math.cos(a + .35) * r1, math.sin(a + .35) * r1)], [.04, .03, .005], MID, .9)
    B.xf = None
    G.glow(0, 0, 1.75, WHITE, .5)


def fx_cloth(t, B, G, rng):
    """찢어진 천 쪼가리: 낙엽처럼 한 축으로 뒤집히며 돈다 (반복). 앞면은 밝고 뒷면은 회색, 홈질 한 줄."""
    flip = math.cos(t * TAU)
    sx = max(.12, abs(flip))
    front = flip >= 0
    B.xf = rot_xf(.5 + t * TAU * .25, sx=sx, sy=1.0)
    # 찢긴 다각형 (아래·오른쪽 가장자리가 지그재그)
    r2 = random.Random(7)
    pts = [(-1.2, .95), (-.2, 1.15), (1.05, .9)]
    for i in range(7):   # 오른쪽 찢긴 변
        u = (i + 1) / 8
        pts.append((1.05 + (r2.random() - .5) * .3 + .1 * (i % 2), .9 - 2.0 * u))
    pts.append((.85, -1.15))
    for i in range(8):   # 아래 찢긴 변
        u = (i + 1) / 9
        pts.append((.85 - 2.0 * u, -1.15 + (r2.random() - .5) * .3 + .12 * (i % 2)))
    pts.append((-1.25, -.9))
    B.poly(pts, LIGHT if front else MID)
    # 천 결 (옅은 가로줄)
    for k in range(4):
        y = .6 - k * .45
        B.ribbon([(-1.05, y), (.75, y + .05)], .025, WHITE if front else LIGHT, .7)
    # 홈질 한 줄 (위쪽 가장자리 따라)
    for k in range(6):
        x0 = -1.0 + k * .33
        B.blade(x0, .72, x0 + .2, .74, .055, DARK, 1, n=5)
    B.xf = None
    G.glow(0, 0, 1.7, WHITE, .45)


def fx_button(t, B, G, rng):
    """구멍 넷 단추: 세로축으로 돌아 납작해졌다 넓어진다 (반복). 테두리 홈 + 구멍 넷 + 구멍을 지나는 실 X."""
    turn = math.cos(t * TAU)
    sx = max(.14, abs(turn))
    front = turn >= 0
    B.xf = rot_xf(.35, sx=sx)
    B.blob(0, 0, 1.25, WHITE if front else MID, 1, rough=.02, seg=40)
    B.blob(0, 0, 1.0, MID if front else SHADE, 1, rough=.01, seg=40)
    B.blob(0, 0, .9, LIGHT if front else MID, 1, rough=.01, seg=40)
    B.blob(-.35, .45, .22, WHITE, .8, rough=0)   # 반짝 자리
    holes = [(-.3, -.3), (.3, -.3), (.3, .3), (-.3, .3)]
    if front:
        B.blade(-.3, -.3, .3, .3, .09, WHITE, 1, n=6)
        B.blade(.3, -.3, -.3, .3, .09, WHITE, 1, n=6)
    for x, y in holes:
        B.blob(x, y, .13, DARK, 1, rough=0, seg=14)
    B.xf = None
    G.glow(0, 0, 1.75, WHITE, .5)


def fx_glint(t, B, G, rng):
    """바늘 반짝임: 가는 바늘이 비스듬히 + 네 갈래 별이 톡 커졌다가 돌며 작아진다 (한 번)."""
    grow = (.6 + .4 * eo(seg(t, 0, .2), 2)) * (1 - seg(t, .3, 1) ** 1.4)   # 0번 칸부터 보인다 (프레임 스톱)
    if grow <= .01:
        return
    a = .75 + t * .8
    L = 1.6 * (.6 + .4 * grow)
    dx, dy = math.cos(a), math.sin(a)
    B.blade(-dx * L, -dy * L, dx * L, dy * L, .08 * grow + .02, WHITE, 1, n=12, bias=.75)   # 바늘
    B.blob(-dx * L * .72, -dy * L * .72, .07 * grow + .02, DARK, 1, rough=0, seg=10)            # 바늘귀
    star(B, 0, 0, 1.25 * grow, WHITE, 1, rot=t * 2.4, points=4, inner=.16)
    star(B, 0, 0, .7 * grow, WHITE, 1, rot=t * 2.4 + math.pi / 4, points=4, inner=.2)
    G.glow(0, 0, 1.9 * grow + .2, WHITE, .85 * grow)


# name: (함수, 칸 수, fps, 칸 px, 반복, 외곽 px, 떨림)
SHEETS = {
    "sew_burst": (fx_burst, 10, 30, 256, False, 3.0, .02),
    "sew_thread": (fx_thread, 8, 14, 96, True, 1.8, .015),
    "sew_fluff": (fx_fluff, 8, 10, 96, True, 1.6, .02),
    "sew_cloth": (fx_cloth, 8, 12, 96, True, 1.8, .015),
    "sew_button": (fx_button, 8, 16, 96, True, 1.8, 0.0),
    "sew_glint": (fx_glint, 8, 24, 96, False, 1.4, 0.0),
}


# ---------------------------------------------------------------- 렌더 + 잉크 후처리 (render_element_vfx.py 와 같은 방식)
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
    return px.reshape(size, size, 4)


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


def inked(px, radius, seed, grain=.05):
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
    rad = radius * (.6 + .8 * smooth_noise(h, w, 7, rng))
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


meta = {name: dict(frames=n, size=size, fps=fps, duration=round(n / fps, 3), loop=loop, pivot=[.5, .5])
        for name, (fn, n, fps, size, loop, ink, wob) in SHEETS.items()}
(SRC / "sheets.json").write_text(json.dumps(meta, indent=1, ensure_ascii=False), encoding="utf-8")
for name, (fn, n, fps, size, loop, ink, wob) in SHEETS.items():
    if ONLY and name not in ONLY and name.replace("sew_", "") not in ONLY:
        continue
    bodies, glows = [], []
    for i in range(n):
        t = i / n if loop else i / (n - 1)
        boil = i if loop else i // 2
        B, G = Layer(wob, boil), Layer(0, boil)
        fn(t, B, G, random.Random(sum(map(ord, name)) * 31))
        body = render_layer(B, size, TMP / f"{name}_b{i:02d}.png")
        glow = render_layer(G, size, TMP / f"{name}_g{i:02d}.png")
        bodies.append(inked(body, ink, seed=boil * 97 + 13))
        glow[..., 3] = np.minimum(glow[..., 3], .7)
        glows.append(glow)
    save(np.concatenate(bodies, axis=1), OUT / f"{name}.png")
    save(np.concatenate(glows, axis=1), OUT / f"{name}_glow.png")
    print("vfx24", name, n, flush=True)

for f in TMP.glob("*.png"):
    f.unlink()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC / "hit_sparks.blend"), compress=True)
