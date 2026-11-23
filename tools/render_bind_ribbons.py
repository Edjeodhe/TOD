"""결박 당기기 리본 VFX (Blender) — 속성별 실 몸통 · 궤적 꼬리 · 머리 광구.

실행:
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b -P tools/render_bind_ribbons.py -- C:/Project/TOD [fire lightning ...]

출력 (Assets/vfx/bind, 속성마다):
  bind_<style>_thread_core/rim.png  실 몸통. 가로로 이음매 없이 반복(타일), 세로로 FRAMES 장 쌓인 아틀라스(아래가 0번).
                                    런타임: LineRenderer 에 _MainTex_ST = (길이/타일, 1/FRAMES, 흐름, 프레임/FRAMES)
  bind_<style>_trail_core/rim.png   궤적 꼬리. 왼쪽(u=0)이 머리, 오른쪽으로 가늘어지며 사라짐. 세로로 FRAMES 장.
  bind_<style>_head_core/rim.png    머리 광구 한 장 (스프라이트).
  core = 흰 고열 심(런타임에 look.core 로 가산), rim = 회색 외곽(look.color·look.glow 로 곱해 가산).
  art_src/vfx/bind/bind.blend       마지막 상태의 편집용 씬

style: fire(화염) · lightning(번개) · frost(얼음) · plain(금실 = 빛) · dark(어둠) · blood(피) · wind(바람) · poison(독). 모든 프레임은 위상 t = i/FRAMES 로 고리처럼 이어진다.
"""
import math
import random
import sys
from pathlib import Path

import bpy
import numpy as np

ROOT = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
ONLY = sys.argv[sys.argv.index("--") + 2:]
OUT = ROOT / "Assets/vfx/bind"
SRC = ROOT / "art_src/vfx/bind"
TMP = SRC / "_frames"
for d in (OUT, SRC, TMP):
    d.mkdir(parents=True, exist_ok=True)

FRAMES = 8
TAU = math.tau
# 판 크기 (px) — 월드 폭은 항상 4 (x ∈ [-2, 2]), 높이는 비율대로
THREAD = (512, 96)    # y ∈ [-.375, .375]
TRAIL = (512, 128)    # y ∈ [-.5, .5]
HEAD = (128, 128)     # y ∈ [-2, 2]

# ---------------------------------------------------------------- scene (render_flipbooks.py 와 같은 방식)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 12
scene.cycles.use_denoising = False
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
    mat = bpy.data.materials.new("fx")
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
    """한 프레임·한 레이어의 기하. 꼭짓점마다 (밝기, 알파) → 발광 + 투명."""

    def __init__(self):
        self.v, self.f, self.c = [], [], []
        self.z = 0.0

    def _vert(self, x, y, lum, alpha):
        self.v.append((x, y, self.z))
        self.c.append((lum, max(0.0, min(1.0, alpha))))
        return len(self.v) - 1

    def ribbon(self, pts, widths, alphas, lum=1.0, edge=0.0):
        if len(pts) < 2:
            return
        self.z += 0.001
        ids = []
        for i, (x, y) in enumerate(pts):
            a = pts[max(0, i - 1)]
            b = pts[min(len(pts) - 1, i + 1)]
            tx, ty = b[0] - a[0], b[1] - a[1]
            ln = math.hypot(tx, ty) or 1
            nx, ny = -ty / ln, tx / ln
            w, al = widths[i], alphas[i]
            ids.append((self._vert(x + nx * w, y + ny * w, lum, al * edge),
                        self._vert(x, y, lum, al),
                        self._vert(x - nx * w, y - ny * w, lum, al * edge)))
        for (l0, c0, r0), (l1, c1, r1) in zip(ids, ids[1:]):
            self.f += [(l0, c0, c1, l1), (c0, r0, r1, c1)]

    def blade(self, x0, y0, x1, y1, width, alpha, lum=1.0, edge=0.0, n=12, bias=0.35):
        pts, ws, als = [], [], []
        for i in range(n + 1):
            u = i / n
            pts.append((x0 + (x1 - x0) * u, y0 + (y1 - y0) * u))
            prof = (u / bias) if u < bias else ((1 - u) / (1 - bias))
            ws.append(width * max(0.0, prof) ** 0.8)
            als.append(alpha)
        self.ribbon(pts, ws, als, lum, edge)

    def disc(self, x, y, r, alpha, lum=1.0, seg=24):
        self.z += 0.001
        c = self._vert(x, y, lum, alpha)
        ring = [self._vert(x + math.cos(a) * r, y + math.sin(a) * r, lum, 0.0) for a in (i * TAU / seg for i in range(seg))]
        for i in range(seg):
            self.f.append((c, ring[i], ring[(i + 1) % seg]))

    def build(self, name):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.v, [], self.f)
        attr = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        for poly in mesh.polygons:
            for li in poly.loop_indices:
                lum, al = self.c[mesh.loops[li].vertex_index]
                attr.data[li].color = (lum, lum, lum, al)
        mesh.materials.append(MAT)
        obj = bpy.data.objects.new(name, mesh)
        scene.collection.objects.link(obj)


def line(x0, x1, n, fy):
    """x0→x1 를 n 칸으로 나눈 점 (y = fy(x))."""
    return [(x0 + (x1 - x0) * i / n, fy(x0 + (x1 - x0) * i / n)) for i in range(n + 1)]


def flame(layer, x, y, h, sway, width, alpha, n=12):
    """곡선 불꽃 혀: 밑(x, y)에서 h 만큼 솟으며 옆으로 S자로 흔들리고, 끝으로 갈수록 가늘고 옅어진다."""
    pts, ws, als = [], [], []
    for i in range(n + 1):
        u = i / n
        pts.append((x + sway * math.sin(u * math.pi * 1.3) * u, y + h * u))
        ws.append(width * (1 - u) ** .75 * (.75 + .25 * math.sin(math.pi * min(1, u * 2))))
        als.append(alpha * (1 - u ** 2))
    layer.ribbon(pts, ws, als, edge=.15)


def tiled(x, fn):
    """타일 이음매: 가장자리 근처면 한 타일(4) 옆에도 그린다."""
    for dx in (-4, 0, 4):
        if -2.6 <= x + dx <= 2.6:
            fn(x + dx)


def bolt_points(rng, x0, y0, x1, y1, n, amp):
    pts = []
    for i in range(n + 1):
        u = i / n
        off = 0 if i in (0, n) else (rng.random() - .5) * 2 * amp * math.sin(u * math.pi) ** .5
        pts.append((x0 + (x1 - x0) * u, y0 + (y1 - y0) * u + off))
    return pts


# ---------------------------------------------------------------- 실 몸통 (타일, y ∈ [-.375, .375])

def thread_fire(t, core, rim, rng):
    wave = lambda x: .025 * math.sin(TAU * (x / 2 + t))
    pts = line(-2.6, 2.6, 80, wave)
    rim.ribbon(pts, [.13] * len(pts), [.45] * len(pts), edge=0)          # 넓은 불빛
    rim.ribbon(pts, [.06] * len(pts), [.95] * len(pts), edge=.3)
    core.ribbon(pts, [.028] * len(pts), [1] * len(pts), edge=.2)
    # 불길 혀: 위·아래로 번갈아, 밑이 넓고 끝이 휘며 사라지는 곡선 불꽃. 위상마다 자랐다 줄어든다 (고리처럼 이어짐)
    for i in range(20):
        x0 = -2 + 4 * (i + .5) / 20 + (rng.random() - .5) * .1
        side = 1 if i % 2 == 0 else -1
        phase = rng.random()
        grow = .5 + .5 * math.sin(TAU * (t + phase))
        h = (.17 + .15 * grow) * side
        sway = .09 * math.sin(TAU * (t * 2 + phase)) + .05

        def tongue(x):
            flame(rim, x, wave(x), h * 1.15, sway * 1.2, .2, .35)    # 뒤 불빛 (넓고 옅게)
            flame(rim, x, wave(x), h, sway, .14, .85)
            flame(core, x, wave(x), h * .6, sway * .6, .055, .95)
        tiled(x0, tongue)
    # 불씨: 타일 한 칸을 한 바퀴에 지나간다
    for i in range(10):
        x0 = (-2 + 4 * rng.random() + 4 * t + 2) % 4 - 2
        y = (rng.random() - .5) * .5
        tiled(x0, lambda x: rim.disc(x, y, .03 + .02 * rng.random(), .9))


def thread_lightning(t, core, rim, rng):
    frame_rng = random.Random(int(t * 1000) + 7)
    for k, (amp, w) in enumerate(((.17, .022), (.11, .012))):
        pts = bolt_points(frame_rng, -2, 0, 2, 0, 22, amp)
        pts = [(x - 4, y) for x, y in pts[:-1]] + pts + [(x + 4, y) for x, y in pts[1:]]   # 타일 양옆 이어 붙임
        rim.ribbon(pts, [w * 7] * len(pts), [.28] * len(pts), edge=0)
        rim.ribbon(pts, [w * 2.6] * len(pts), [.9] * len(pts), edge=.25)
        core.ribbon(pts, [w] * len(pts), [1] * len(pts), edge=.4)
    # 곁가지
    for _ in range(4):
        x = -1.8 + 3.6 * frame_rng.random()
        side = 1 if frame_rng.random() < .5 else -1
        pts = bolt_points(frame_rng, x, 0, x + .25 + .3 * frame_rng.random(), side * (.2 + .12 * frame_rng.random()), 5, .05)
        rim.ribbon(pts, [.025] * len(pts), [.8] * len(pts), edge=.2)
        core.ribbon(pts, [.008] * len(pts), [1] * len(pts), edge=.4)
    for _ in range(8):
        rim.disc(-2 + 4 * frame_rng.random(), (frame_rng.random() - .5) * .5, .025, 1)


def thread_frost(t, core, rim, rng):
    pts = line(-2.6, 2.6, 40, lambda x: 0)
    rim.ribbon(pts, [.12] * len(pts), [.35] * len(pts), edge=0)
    rim.ribbon(pts, [.045] * len(pts), [.95] * len(pts), edge=.4)
    core.ribbon(pts, [.018] * len(pts), [1] * len(pts), edge=.3)
    # 얼음 결정 가시: 위·아래 번갈아, 비스듬히
    for i in range(14):
        x0 = -2 + 4 * (i + .5) / 14
        side = 1 if i % 2 == 0 else -1
        h = (.12 + .17 * rng.random()) * side
        tip = .06 * (rng.random() - .5)

        def spike(x):
            rim.blade(x, 0, x + tip + .05, h, .05, .95, edge=.45, bias=.25)
            core.blade(x, 0, x + tip * .6 + .03, h * .7, .014, 1, edge=.5, bias=.25)
            rim.blade(x + .1, 0, x + .16, h * .45, .03, .8, edge=.4, bias=.25)
        tiled(x0, spike)
    # 반짝임: 위상마다 깜빡
    for i in range(12):
        x0, y = -2 + 4 * rng.random(), (rng.random() - .5) * .55
        tw = max(0.0, math.sin(TAU * (t + rng.random()))) ** 3
        tiled(x0, lambda x: core.disc(x, y, .04 * tw + .005, tw))


def wisp(layer, x, y, length, curl, width, alpha, t, n=14):
    """연기 한 줄기: (x, y)에서 뒤로 뻗으며 휘감기고, 끝으로 갈수록 가늘고 옅어진다."""
    pts, ws, als = [], [], []
    for i in range(n + 1):
        u = i / n
        pts.append((x - length * u, y + curl * math.sin(math.pi * u * 1.4 + TAU * t) * u))
        ws.append(width * (1 - u * .8))
        als.append(alpha * (1 - u) ** 1.3)
    layer.ribbon(pts, ws, als, edge=0)


def thread_dark(t, core, rim, rng):
    # 어둠: 굵고 흐린 그림자 줄기 + 실을 휘감는 연기 가닥 + 떠다니는 그림자 덩이
    wave = lambda x: .03 * math.sin(TAU * (x / 2 + t))
    pts = line(-2.6, 2.6, 60, wave)
    rim.ribbon(pts, [.22] * len(pts), [.45] * len(pts), edge=0)
    rim.ribbon(pts, [.09] * len(pts), [.85] * len(pts), edge=.2)
    core.ribbon(pts, [.028] * len(pts), [.95] * len(pts), edge=.3)
    for i in range(12):
        x0 = -2 + 4 * (i + .5) / 12
        side = 1 if i % 2 == 0 else -1
        ph = rng.random()
        tiled(x0, lambda x: (wisp(rim, x, wave(x), .7 + .25 * ph, side * (.22 + .1 * ph), .1, .85, t + ph),
                             wisp(core, x, wave(x), .4, side * .1, .022, .8, t + ph)))
    for i in range(10):
        x0 = (-2 + 4 * rng.random() - 4 * t + 2) % 4 - 2
        y = (rng.random() - .5) * .45
        r = .08 + .07 * rng.random()
        tiled(x0, lambda x: rim.disc(x, y, r, .6))


def thread_blood(t, core, rim, rng):
    # 피: 두꺼운 핏줄기가 맥박처럼 굵어졌다 가늘어지고, 아래로 핏방울이 맺혀 떨어진다
    pulse = lambda x: .75 + .25 * math.sin(TAU * (x / 1 - t))
    pts = line(-2.6, 2.6, 80, lambda x: 0)
    rim.ribbon(pts, [.18 * pulse(x) for x, _ in pts], [.45] * len(pts), edge=0)
    rim.ribbon(pts, [.08 * pulse(x) for x, _ in pts], [.95] * len(pts), edge=.35)
    core.ribbon(pts, [.026 * pulse(x) for x, _ in pts], [1] * len(pts), edge=.3)
    for i in range(9):
        x0 = -2 + 4 * (i + .5) / 9 + (rng.random() - .5) * .15
        ph = rng.random()
        k = (t + ph) % 1                       # 맺혔다 떨어지는 진행
        drop = .04 + .22 * k ** 2
        r = .045 + .035 * min(1, k * 2)

        def bead(x):
            rim.blade(x, -.01, x, -drop, .035 * (1 - k * .5), .9, edge=.4, bias=.8)   # 늘어진 물줄기
            rim.disc(x, -drop, r, .95)
            core.disc(x - r * .3, -drop + r * .3, r * .35, .9)                          # 반사광
        tiled(x0, bead)


def thread_wind(t, core, rim, rng):
    # 바람: 가는 심 둘레를 세 가닥 나선이 감아 흐르고, 바람 결 줄무늬가 스쳐 간다
    pts = line(-2.6, 2.6, 60, lambda x: 0)
    rim.ribbon(pts, [.1] * len(pts), [.25] * len(pts), edge=0)
    core.ribbon(pts, [.012] * len(pts), [.8] * len(pts), edge=.3)
    for k in range(3):
        f = lambda x, p=k / 3: .1 * math.sin(TAU * (x + t + p))
        sp = line(-2.6, 2.6, 120, f)
        front = [.5 + .5 * math.cos(TAU * (x + t + k / 3)) for x, _ in sp]        # 앞에 올수록 밝게
        rim.ribbon(sp, [.022 + .012 * a for a in front], [.35 + .6 * a for a in front], edge=.2)
        core.ribbon(sp, [.007] * len(sp), [.2 + .8 * a ** 2 for a in front], edge=.4)
    for i in range(9):
        x0 = (-2 + 4 * rng.random() + 4 * t + 2) % 4 - 2
        y = (rng.random() - .5) * .5
        ln = .25 + .25 * rng.random()
        tiled(x0, lambda x: rim.blade(x - ln, y, x + ln * .2, y, .012, .7, edge=.2, bias=.75))


def thread_poison(t, core, rim, rng):
    # 독: 걸쭉하게 출렁이는 독액 줄기 + 표면에서 솟는 거품 + 아래로 늘어지는 독 방울
    wave = lambda x: .035 * math.sin(TAU * (x * .75 + t))
    pts = line(-2.6, 2.6, 80, wave)
    rim.ribbon(pts, [.14] * len(pts), [.35] * len(pts), edge=0)
    rim.ribbon(pts, [.06] * len(pts), [.9] * len(pts), edge=.3)
    core.ribbon(pts, [.02] * len(pts), [.95] * len(pts), edge=.3)
    for i in range(14):
        x0 = -2 + 4 * rng.random()
        ph = rng.random()
        k = (t + ph) % 1
        r = .025 + .035 * rng.random()
        y = wave(x0) + .03 + .3 * k                                                # 위로 솟는 거품
        a = math.sin(math.pi * k)

        def bubble(x):
            rim.disc(x, y, r, .55 * a)
            core.disc(x + r * .35, y + r * .35, r * .3, .9 * a)                     # 거품 반사광
        tiled(x0, bubble)
    for i in range(5):
        x0 = -2 + 4 * (i + .5) / 5
        k = (t + rng.random()) % 1
        tiled(x0, lambda x: rim.blade(x, wave(x), x, wave(x) - .08 - .14 * k, .03, .85, edge=.4, bias=.8))


def thread_plain(t, core, rim, rng):
    # 금실 두 가닥이 꼬여 흐른다
    for phase in (0, .5):
        f = lambda x, p=phase: .09 * math.sin(TAU * (x / 2 + t + p))
        pts = line(-2.6, 2.6, 80, f)
        rim.ribbon(pts, [.07] * len(pts), [.5] * len(pts), edge=0)
        core.ribbon(pts, [.018] * len(pts), [1] * len(pts), edge=.3)
    for i in range(10):
        x0, y = (-2 + 4 * rng.random() + 4 * t + 2) % 4 - 2, (rng.random() - .5) * .4
        tiled(x0, lambda x: (core.disc(x, y, .035, .9), rim.disc(x, y, .08, .6)))


# ---------------------------------------------------------------- 궤적 꼬리 (x: -2 머리 → +2 꼬리 끝, y ∈ [-.5, .5])

def taper(x):
    u = (x + 2) / 4
    return max(0.0, 1 - u)


def trail_core_streak(core, rim, wobble, width, glow=.22):
    pts = line(-2, 2, 60, wobble)
    rim.ribbon(pts, [glow * taper(x) ** .7 + .01 for x, _ in pts], [.5 * taper(x) ** 1.2 for x, _ in pts], edge=0)
    rim.ribbon(pts, [width * 2.4 * taper(x) ** .8 for x, _ in pts], [.95 * taper(x) for x, _ in pts], edge=.3)
    core.ribbon(pts, [width * taper(x) ** .9 for x, _ in pts], [taper(x) ** .8 for x, _ in pts], edge=.25)


def trail_fire(t, core, rim, rng):
    trail_core_streak(core, rim, lambda x: .05 * math.sin(TAU * (x * .7 - t * 2)) * (x + 2) / 4, .05)
    # 뒤로 휘날리는 불줄기
    for i in range(7):
        x0 = -1.9 + 2.6 * i / 7 + .1 * rng.random()
        side = 1 if i % 2 == 0 else -1
        k = taper(x0)
        curl = side * (.16 + .26 * (1 - k)) * (.7 + .3 * math.sin(TAU * (t + rng.random())))
        x1 = x0 + .7 + .5 * rng.random()
        rim.blade(x0, 0, x1, curl, .09 * k + .02, .85 * k, edge=.15, bias=.3)
        core.blade(x0, 0, x0 + (x1 - x0) * .5, curl * .5, .03 * k + .01, .9 * k, edge=.2, bias=.3)
    for _ in range(16):
        x = -1.6 + 3.4 * rng.random()
        drift = (rng.random() - .5) * .7 * (x + 2) / 4
        rim.disc(x + .25 * t, drift, .035 * taper(x) + .008, .9 * taper(x) ** .5)


def trail_lightning(t, core, rim, rng):
    frame_rng = random.Random(int(t * 1000) + 31)
    pts = bolt_points(frame_rng, -2, 0, 1.6, (frame_rng.random() - .5) * .2, 26, .14)
    rim.ribbon(pts, [.16 * taper(x) ** .7 + .01 for x, _ in pts], [.35 * taper(x) for x, _ in pts], edge=0)
    rim.ribbon(pts, [.055 * taper(x) + .006 for x, _ in pts], [.95 * taper(x) ** .7 for x, _ in pts], edge=.25)
    core.ribbon(pts, [.02 * taper(x) + .003 for x, _ in pts], [taper(x) ** .6 for x, _ in pts], edge=.4)
    for _ in range(5):
        x = -1.6 + 2.6 * frame_rng.random()
        side = 1 if frame_rng.random() < .5 else -1
        k = taper(x)
        b = bolt_points(frame_rng, x, 0, x + .4, side * (.18 + .15 * frame_rng.random()) * k, 5, .05)
        rim.ribbon(b, [.025 * k] * len(b), [.8 * k] * len(b), edge=.2)
        core.ribbon(b, [.008 * k] * len(b), [k] * len(b), edge=.4)
    for _ in range(14):
        x = -1.8 + 3.4 * frame_rng.random()
        rim.disc(x, (frame_rng.random() - .5) * .7 * (x + 2) / 4, .03 * taper(x) + .006, 1)


def trail_frost(t, core, rim, rng):
    trail_core_streak(core, rim, lambda x: 0, .035, glow=.25)
    # 부서져 뒤로 흩어지는 얼음 조각
    for i in range(14):
        x = -1.8 + 3.4 * rng.random()
        k = taper(x)
        y = (rng.random() - .5) * .6 * (1 - k)
        ang = (rng.random() - .5) * .9 + t * .6 * (1 if i % 2 else -1)
        ln = .09 + .09 * k
        dx, dy = math.cos(ang) * ln, math.sin(ang) * ln
        rim.blade(x - dx, y - dy, x + dx, y + dy, .045 * k + .01, .95 * k ** .5, edge=.45)
        core.blade(x - dx * .6, y - dy * .6, x + dx * .6, y + dy * .6, .012 * k + .004, k ** .5, edge=.5)
    for _ in range(10):
        x = -1.9 + 3 * rng.random()
        tw = max(0.0, math.sin(TAU * (t + rng.random()))) ** 2
        core.disc(x, (rng.random() - .5) * .4, .04 * tw * taper(x) + .004, tw)


def trail_plain(t, core, rim, rng):
    for phase in (0, .5):
        f = lambda x, p=phase: .13 * (x + 2) / 4 * math.sin(TAU * (x * .6 - t + p))
        pts = line(-2, 2, 60, f)
        rim.ribbon(pts, [.07 * taper(x) + .005 for x, _ in pts], [.55 * taper(x) for x, _ in pts], edge=0)
        core.ribbon(pts, [.02 * taper(x) + .003 for x, _ in pts], [taper(x) ** .8 for x, _ in pts], edge=.3)
    for _ in range(12):
        x = -1.8 + 3.2 * rng.random()
        core.disc(x, (rng.random() - .5) * .4 * (x + 2) / 4, .035 * taper(x) + .006, taper(x) ** .5)


def trail_dark(t, core, rim, rng):
    trail_core_streak(core, rim, lambda x: .06 * math.sin(TAU * (x * .6 - t)) * (x + 2) / 4, .04, glow=.3)
    for i in range(8):
        x0 = -1.9 + 2.4 * i / 8
        k = taper(x0)
        side = 1 if i % 2 == 0 else -1
        wisp(rim, x0 + .9, 0, -.9 - .5 * rng.random(), side * (.2 + .2 * (1 - k)), .07 * k + .015, .75 * k, t + rng.random())
    for _ in range(12):
        x = -1.6 + 3.4 * rng.random()
        rim.disc(x + .2 * t, (rng.random() - .5) * .6 * (x + 2) / 4, .07 * taper(x) + .02, .5 * taper(x) ** .5)


def trail_blood(t, core, rim, rng):
    trail_core_streak(core, rim, lambda x: 0, .045, glow=.2)
    for _ in range(16):
        x = -1.8 + 3.4 * rng.random()
        k = taper(x)
        y = -(rng.random() * .35) * (1 - k) - .02
        r = .045 * k + .012
        rim.disc(x + .2 * t, y, r, .95 * k ** .4)
        core.disc(x + .2 * t - r * .3, y + r * .3, r * .35, .9 * k ** .4)


def trail_wind(t, core, rim, rng):
    for k in range(3):
        f = lambda x, p=k / 3: .16 * (x + 2) / 4 * math.sin(TAU * (x * .8 - t + p)) + .02 * math.sin(TAU * p)
        pts = line(-2, 2, 80, f)
        rim.ribbon(pts, [.03 * taper(x) + .004 for x, _ in pts], [.8 * taper(x) for x, _ in pts], edge=.2)
        core.ribbon(pts, [.01 * taper(x) + .002 for x, _ in pts], [taper(x) ** .7 for x, _ in pts], edge=.4)
    for _ in range(10):
        x = -1.8 + 3.2 * rng.random()
        y = (rng.random() - .5) * .6 * (x + 2) / 4
        rim.blade(x, y, x + .4, y, .012 * taper(x) + .004, .7 * taper(x), edge=.2, bias=.75)


def trail_poison(t, core, rim, rng):
    trail_core_streak(core, rim, lambda x: .04 * math.sin(TAU * (x * .5 - t)) * (x + 2) / 4, .045, glow=.24)
    for _ in range(18):
        x = -1.8 + 3.4 * rng.random()
        k = taper(x)
        y = (rng.random() - .3) * .5 * (1 - k)
        r = .04 * k + .015
        rim.disc(x + .15 * t, y, r, .6 * k ** .4)
        core.disc(x + .15 * t + r * .35, y + r * .35, r * .3, .9 * k ** .4)


# ---------------------------------------------------------------- 머리 광구 (128×128, x,y ∈ [-2, 2])

def head(style, core, rim, rng):
    rim.disc(0, 0, 1.9, .55)
    rim.disc(0, 0, 1.0, .9)
    core.disc(0, 0, .75, 1)
    if style == "fire":
        for i in range(9):
            a = TAU * i / 9 + rng.random() * .3
            r = 1.3 + .5 * rng.random()
            rim.blade(0, 0, math.cos(a) * r, math.sin(a) * r, .35, .8, edge=.2, bias=.3)
    elif style == "lightning":
        for i in range(7):
            a = TAU * i / 7 + rng.random() * .4
            pts = bolt_points(rng, 0, 0, math.cos(a) * 1.8, math.sin(a) * 1.8, 6, .18)
            rim.ribbon(pts, [.09] * len(pts), [.9] * len(pts), edge=.2)
            core.ribbon(pts, [.03] * len(pts), [1] * len(pts), edge=.4)
    elif style == "frost":
        for i in range(6):
            a = TAU * i / 6
            rim.blade(0, 0, math.cos(a) * 1.85, math.sin(a) * 1.85, .2, .95, edge=.4, bias=.2)
            core.blade(0, 0, math.cos(a) * 1.3, math.sin(a) * 1.3, .07, 1, edge=.5, bias=.2)
            for s in (-1, 1):
                bx, by = math.cos(a) * 1.0, math.sin(a) * 1.0
                rim.blade(bx, by, bx + math.cos(a + s * .8) * .5, by + math.sin(a + s * .8) * .5, .1, .85, edge=.4, bias=.2)
    elif style == "dark":
        for i in range(5):   # 소용돌이 치는 그림자 팔
            a0 = TAU * i / 5
            pts = [(math.cos(a0 + u * 2.2) * (.4 + 1.5 * u), math.sin(a0 + u * 2.2) * (.4 + 1.5 * u)) for u in (j / 16 for j in range(17))]
            rim.ribbon(pts, [.28 * (1 - j / 17) + .03 for j in range(17)], [.85 * (1 - j / 17) for j in range(17)], edge=0)
    elif style == "blood":
        for i in range(8):   # 튀는 핏방울
            a = TAU * i / 8 + rng.random() * .4
            r = 1.1 + .6 * rng.random()
            rim.blade(0, 0, math.cos(a) * r, math.sin(a) * r, .22, .9, edge=.3, bias=.75)
            rim.disc(math.cos(a) * r, math.sin(a) * r, .16, .95)
    elif style == "wind":
        for k in range(3):   # 나선 세 가닥
            pts = [(math.cos(TAU * k / 3 + u * 4.5) * 1.8 * u, math.sin(TAU * k / 3 + u * 4.5) * 1.8 * u) for u in (j / 24 for j in range(25))]
            rim.ribbon(pts, [.12] * 25, [.9 * (1 - j / 25) + .1 for j in range(25)], edge=.2)
            core.ribbon(pts, [.04] * 25, [1 - j / 25 for j in range(25)], edge=.4)
    elif style == "poison":
        for i in range(9):   # 거품 덩이
            a = TAU * rng.random()
            d = 1.0 + .7 * rng.random()
            r = .2 + .25 * rng.random()
            rim.disc(math.cos(a) * d, math.sin(a) * d, r, .8)
            core.disc(math.cos(a) * d + r * .35, math.sin(a) * d + r * .35, r * .3, 1)
    else:
        for i in range(4):
            a = TAU * i / 4 + TAU / 8
            rim.blade(0, 0, math.cos(a) * 1.9, math.sin(a) * 1.9, .22, .9, edge=.2, bias=.15)
            core.blade(0, 0, math.cos(a) * 1.5, math.sin(a) * 1.5, .08, 1, edge=.3, bias=.15)


# ---------------------------------------------------------------- render

def clear_fx():
    for obj in list(scene.collection.objects):
        if obj is not cam:
            mesh = obj.data
            bpy.data.objects.remove(obj)
            bpy.data.meshes.remove(mesh)


def render_layer(layer, size, path):
    clear_fx()
    if layer.v:
        layer.build(path.stem)
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(str(path))
    px = np.empty(size[0] * size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(size[1], size[0], 4)   # 아래→위 행 순서


def save(pixels, path):
    h, w = pixels.shape[:2]
    img = bpy.data.images.new(path.stem, width=w, height=h, alpha=True)
    img.pixels.foreach_set(pixels.ravel())
    img.filepath_raw = str(path)
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


STYLES = {
    "fire": (thread_fire, trail_fire),
    "lightning": (thread_lightning, trail_lightning),
    "frost": (thread_frost, trail_frost),
    "plain": (thread_plain, trail_plain),
    "dark": (thread_dark, trail_dark),
    "blood": (thread_blood, trail_blood),
    "wind": (thread_wind, trail_wind),
    "poison": (thread_poison, trail_poison),
}

for style, (thread_fn, trail_fn) in STYLES.items():
    if ONLY and style not in ONLY:
        continue
    for kind, fn, size in (("thread", thread_fn, THREAD), ("trail", trail_fn, TRAIL)):
        stacks = {"core": [], "rim": []}
        for i in range(FRAMES):
            rng = random.Random(sum(map(ord, style + kind)) * 31)   # 프레임 간 같은 배치 → 이어지는 움직임
            core, rim = Layer(), Layer()
            fn(i / FRAMES, core, rim, rng)
            for lname, layer in (("core", core), ("rim", rim)):
                stacks[lname].append(render_layer(layer, size, TMP / f"{style}_{kind}_{lname}_{i:02d}.png"))
        for lname, frames in stacks.items():
            save(np.concatenate(frames, axis=0), OUT / f"bind_{style}_{kind}_{lname}.png")   # 0번이 맨 아래
    core, rim = Layer(), Layer()
    head(style, core, rim, random.Random(sum(map(ord, style)) * 17))
    for lname, layer in (("core", core), ("rim", rim)):
        save(render_layer(layer, HEAD, TMP / f"{style}_head_{lname}.png"), OUT / f"bind_{style}_head_{lname}.png")
    print("bind ribbons", style)

bpy.ops.wm.save_as_mainfile(filepath=str(SRC / "bind.blend"), compress=True)
