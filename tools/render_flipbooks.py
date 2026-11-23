"""Blender 플립북 VFX 렌더러 — 프레임마다 형태가 실제로 변하는 시퀀스 이펙트.

실행:
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b -P tools/render_flipbooks.py -- C:/Project/TOD

출력 (이펙트마다 두 장, 가로 1줄 스트립):
  assets/vfx/flip/<name>_core.png  흰색 고열 코어 (런타임에서 그대로 가산)
  assets/vfx/flip/<name>_rim.png   회색조 외곽/파편 (런타임에서 실 속성 색으로 곱한 뒤 가산)
  src/game/flipbooks.js            프레임 수/크기/재생 시간 메타데이터 (오프라인 빌드용 JS 모듈)
  art_src/vfx/flip/flipbooks.blend  마지막 프레임 상태의 편집용 씬

모든 이펙트는 +x 방향을 '진행 방향'으로 그린다. 게임에서 각도만큼 회전시켜 쓴다.
"""
import json
import math
import random
import sys
from pathlib import Path

import bpy
import numpy as np

ROOT = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
OUT = ROOT / "assets/vfx/flip"
SRC = ROOT / "art_src/vfx/flip"   # 편집용 .blend 와 낱장 프레임은 Unity Assets 밖에 둔다
SRC.mkdir(parents=True, exist_ok=True)
TMP = SRC / "_frames"
OUT.mkdir(parents=True, exist_ok=True)
TMP.mkdir(parents=True, exist_ok=True)

SIZE = 256          # 프레임 한 칸 px
ORTHO = 4.0         # 프레임이 덮는 월드 폭 (x,y ∈ [-2, 2])


# ---------------------------------------------------------------- scene
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 12
scene.cycles.use_denoising = False
scene.render.resolution_x = SIZE
scene.render.resolution_y = SIZE
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.view_settings.view_transform = "Standard"
scene.world = bpy.data.worlds.new("void")
cam_data = bpy.data.cameras.new("cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = ORTHO
cam = bpy.data.objects.new("cam", cam_data)
cam.location = (0, 0, 10)
scene.collection.objects.link(cam)
scene.camera = cam


def make_material():
    """색 속성(RGB=밝기, A=불투명도)으로 발광 + 투명을 섞는 재질. 가장자리가 부드럽게 빠진다."""
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
    """한 프레임·한 레이어의 기하를 하나의 메시로 모은다."""

    def __init__(self):
        self.v, self.f, self.c = [], [], []  # c: 꼭짓점별 (밝기, 알파)
        self.z = 0.0

    def _vert(self, x, y, lum, alpha):
        self.v.append((x, y, self.z))
        self.c.append((lum, max(0.0, min(1.0, alpha))))
        return len(self.v) - 1

    def ribbon(self, pts, widths, alphas, lum=1.0, edge=0.0):
        """폴리라인 → 가운데 밝고 가장자리 빠지는 띠. widths=반폭, alphas=가운데 알파."""
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
            ids.append((
                self._vert(x + nx * w, y + ny * w, lum, al * edge),
                self._vert(x, y, lum, al),
                self._vert(x - nx * w, y - ny * w, lum, al * edge),
            ))
        for (l0, c0, r0), (l1, c1, r1) in zip(ids, ids[1:]):
            self.f += [(l0, c0, c1, l1), (c0, r0, r1, c1)]

    def blade(self, x0, y0, x1, y1, width, alpha, lum=1.0, edge=0.0, n=14, bias=0.5):
        """양 끝이 뾰족한 칼날. bias = 가장 두꺼운 지점(0~1)."""
        pts, ws, als = [], [], []
        for i in range(n + 1):
            u = i / n
            pts.append((x0 + (x1 - x0) * u, y0 + (y1 - y0) * u))
            prof = (u / bias) if u < bias else ((1 - u) / (1 - bias))
            ws.append(width * max(0.0, prof) ** 0.8)
            als.append(alpha)
        self.ribbon(pts, ws, als, lum, edge)

    def disc(self, x, y, r, alpha, lum=1.0, seg=28, sy=1.0):
        """중심 밝고 바깥으로 0이 되는 원형 광원."""
        self.z += 0.001
        c = self._vert(x, y, lum, alpha)
        ring = [self._vert(x + math.cos(a) * r, y + math.sin(a) * r * sy, lum, 0.0)
                for a in (i * math.tau / seg for i in range(seg))]
        for i in range(seg):
            self.f.append((c, ring[i], ring[(i + 1) % seg]))

    def shard(self, x, y, ang, length, width, alpha, lum=1.0):
        """날아가는 파편: 진행 방향으로 늘어난 날카로운 삼각 조각 (속도 번짐 포함)."""
        dx, dy = math.cos(ang), math.sin(ang)
        self.blade(x - dx * length, y - dy * length, x, y, width, alpha, lum, edge=0.35, n=6, bias=0.8)

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
        return obj


# ---------------------------------------------------------------- easing
def clamp(v, a=0.0, b=1.0):
    return max(a, min(b, v))


def eo(t, p=3):   # ease out
    t = clamp(t)
    return 1 - (1 - t) ** p


def seg(t, a, b):  # t 를 [a,b] 구간의 0~1 로
    return clamp((t - a) / (b - a)) if b > a else float(t >= a)


def bump(t, peak, end):  # peak 에서 1, 0/end 에서 0 — 날카로운 섬광 곡선
    if t <= peak:
        return seg(t, 0, peak) ** 0.6
    return (1 - seg(t, peak, end)) ** 2.2


# ---------------------------------------------------------------- effects
# 각 함수: (t ∈ [0,1], core: Layer, rim: Layer, rng) → None

def puncture(t, core, rim, rng):
    """바늘이 박히는 순간: 앞으로 찌르는 섬광 + 천 섬유/파편이 진행 방향으로 튀어나감."""
    # 찌르기 섬광 (짧고 뾰족, 앞으로 뻗었다 사라짐)
    s = bump(t, 0.08, 0.4)
    if s > 0.01:
        core.blade(-0.35, 0, 0.25 + 1.35 * eo(t / 0.4), 0, 0.11 * s, 1.0 * s, edge=0.25, bias=0.3)
        core.blade(-0.15, -0.45 * s, -0.15, 0.45 * s, 0.05 * s, 0.8 * s, edge=0.2)  # 십자 반짝
    core.disc(0, 0, 0.22 + 0.25 * eo(t), (1 - t) ** 2.5, 1.0)
    rim.disc(0, 0, 0.55 + 0.6 * eo(t), 0.55 * (1 - t) ** 1.6, 0.9)
    # 파편: 대부분 진행 방향 원뿔, 일부는 반동으로 뒤로
    for i in range(18):
        back = i % 5 == 0
        ang = (math.pi if back else 0) + rng.uniform(-1.0, 1.0) * (0.45 if back else 1.15)
        spd = rng.uniform(0.9, 1.9) * (0.6 if back else 1)
        d = spd * eo(t, 2.6)
        x, y = math.cos(ang) * d, math.sin(ang) * d
        smear = 0.08 + 0.5 * (1 - eo(t, 2)) * spd
        a = (1 - seg(t, 0.25 + rng.random() * 0.3, 1.0)) ** 1.2
        rim.shard(x, y, ang, smear, 0.035 + rng.random() * 0.03, a, 1.0)
        if i % 3 == 0 and a > 0.05:
            core.shard(x, y, ang, smear * 0.6, 0.012, a * 0.9)
    # 풀린 섬유: 휘감기며 날아가는 짧은 실오라기
    for i in range(5):
        base = rng.uniform(-1.1, 1.1)
        curl = rng.choice((-1, 1)) * rng.uniform(1.5, 3.0)
        reach = 0.4 + 1.1 * eo(t, 2)
        pts, ws, als = [], [], []
        fade = (1 - seg(t, 0.3, 1.0)) ** 1.5
        for k in range(12):
            u = k / 11
            ang = base + curl * u * 0.5 * (0.3 + t)
            r = 0.15 + reach * u
            pts.append((math.cos(ang) * r, math.sin(ang) * r))
            ws.append(0.022 * math.sin(math.pi * u) + 0.004)
            als.append(fade * math.sin(math.pi * u) ** 0.5)
        rim.ribbon(pts, ws, als, 1.0, edge=0.2)


def slash(t, core, rim, rng):
    """그웬 풍 초승달 베기: 날카로운 선두가 휘두르며 지나가고 꼬리가 따라 사라짐 + 관통 절단선."""
    def crescent(layer, cx, cy, radius, a0, a1, head, tail, width, alpha, edge):
        if head - tail < 0.01:
            return
        pts, ws, als = [], [], []
        n = 28
        for k in range(n + 1):
            u = k / n
            ang = a0 + (a1 - a0) * (tail + (head - tail) * u)
            pts.append((cx + math.cos(ang) * radius, cy + math.sin(ang) * radius))
            ws.append(width * (u ** 0.9) * (1 - u) ** 0.25 * 1.6)   # 선두 쪽이 두껍고 끝은 뾰족
            als.append(alpha * (0.35 + 0.65 * u))
        layer.ribbon(pts, ws, als, 1.0, edge)

    head = eo(seg(t, 0.0, 0.32), 3)
    tail = eo(seg(t, 0.1, 0.62), 2)
    fade = 1 - seg(t, 0.45, 1.0) ** 1.4
    a0, a1 = math.radians(-78), math.radians(78)
    crescent(core, -1.15, 0, 1.45, a0, a1, head, tail, 0.12, fade, 0.2)
    crescent(rim, -1.15, 0, 1.45, a0, a1, head, tail, 0.36, fade, 0.0)
    # 두 번째 잔상 베기 (살짝 안쪽, 늦게)
    h2 = eo(seg(t, 0.1, 0.42), 3)
    t2 = eo(seg(t, 0.2, 0.7), 2)
    crescent(core, -1.35, 0.1, 1.3, a0 + 0.2, a1 - 0.1, h2, t2, 0.035, fade * 0.7, 0.1)
    crescent(rim, -1.35, 0.1, 1.3, a0 + 0.2, a1 - 0.1, h2, t2, 0.12, fade * 0.6, 0.0)
    # 관통 절단선: 실 방향으로 화면을 가르는 얇은 칼선
    s = bump(t, 0.12, 0.55)
    if s > 0.01:
        L = 0.4 + 1.6 * eo(seg(t, 0.0, 0.2))
        core.blade(-L, 0, L, 0, 0.03 * s + 0.004, s, edge=0.3, n=20)
        rim.blade(-L * 1.05, 0, L * 1.05, 0, 0.11 * s, 0.7 * s, n=20)
    core.disc(0, 0, 0.3 * bump(t, 0.12, 0.5) + 0.01, bump(t, 0.12, 0.5))
    # 칼선 방향으로 튀는 불꽃
    for i in range(12):
        side = 1 if i % 2 else -1
        ang = (0 if side > 0 else math.pi) + rng.uniform(-0.35, 0.35)
        d = rng.uniform(0.5, 1.8) * eo(seg(t, 0.1, 0.8), 2.5)
        a = (1 - seg(t, 0.2, 0.85)) * (t > 0.1)
        rim.shard(math.cos(ang) * d, math.sin(ang) * d, ang, 0.1 + 0.35 * (1 - t), 0.025, a)


def snip(t, core, rim, rng):
    """가위질: 두 날이 X자로 교차하며 닫히는 순간 별 섬광, 이후 날이 부서져 흩어짐."""
    close = eo(seg(t, 0.0, 0.3), 2.2)
    theta = math.radians(42 - 30 * close)
    fade = 1 - seg(t, 0.4, 0.85) ** 1.3
    L = 1.25 + 0.35 * eo(seg(t, 0.3, 0.6))
    for sgn in (1, -1):
        a = sgn * theta
        dx, dy = math.cos(a), math.sin(a)
        if fade > 0.01:
            core.blade(-dx * L, -dy * L, dx * L, dy * L, 0.05, fade, edge=0.2, n=18, bias=0.62)
            rim.blade(-dx * L, -dy * L, dx * L, dy * L, 0.17, fade * 0.8, n=18, bias=0.62)
    # 닫히는 순간 4각 별 섬광
    s = bump(t, 0.3, 0.62)
    if s > 0.01:
        core.blade(-1.7 * s, 0, 1.7 * s, 0, 0.07 * s, s, edge=0.2)
        core.blade(0, -1.0 * s, 0, 1.0 * s, 0.06 * s, s, edge=0.2)
        for a in (math.pi / 4, -math.pi / 4):
            r = 0.55 * s
            core.blade(-math.cos(a) * r, -math.sin(a) * r, math.cos(a) * r, math.sin(a) * r, 0.03 * s, 0.8 * s)
        core.disc(0, 0, 0.45 * s, s)
        rim.disc(0, 0, 1.2 * s, 0.6 * s)
    # 날 파편
    if t > 0.32:
        u = seg(t, 0.32, 1.0)
        for i in range(14):
            ang = i * math.tau / 14 + rng.uniform(-0.2, 0.2)
            d = (0.3 + rng.uniform(0.5, 1.4)) * eo(u, 2.5)
            a = (1 - u) ** 1.4
            rim.shard(math.cos(ang) * d, math.sin(ang) * d, ang, 0.08 + 0.3 * (1 - u), 0.04, a)
            if i % 2 == 0:
                core.shard(math.cos(ang) * d, math.sin(ang) * d, ang, 0.06 + 0.15 * (1 - u), 0.012, a)


def flash(t, core, rim, rng):
    """렌즈 섬광: 순식간에 터지고 길게 빠지는 가로 스트릭 + 별."""
    s = bump(t, 0.12, 1.0)
    if s < 0.01:
        return
    core.blade(-1.95 * s, 0, 1.95 * s, 0, 0.05 * s + 0.005, s, edge=0.15, n=24)
    core.blade(0, -0.9 * s, 0, 0.9 * s, 0.04 * s, s * 0.9, edge=0.15)
    for a in (0.62, -0.62, math.pi - 0.62, math.pi + 0.62):
        core.blade(0, 0, math.cos(a) * 0.6 * s, math.sin(a) * 0.6 * s, 0.025 * s, 0.7 * s, bias=0.15)
    core.disc(0, 0, 0.35 * s, s)
    rim.disc(0, 0, 1.3 * s, 0.75 * s, sy=0.55)
    rim.blade(-2.0 * s, 0, 2.0 * s, 0, 0.18 * s, 0.6 * s, n=24)


def muzzle(t, core, rim, rng):
    """바늘 발사: 손에서 앞으로 뻗는 속도선 원뿔 + 반동 링 파편."""
    for i in range(11):
        ang = (i - 5) * 0.11 + rng.uniform(-0.03, 0.03)
        reach = rng.uniform(1.0, 1.85) * (1 - abs(i - 5) * 0.06)
        head = 0.1 + reach * eo(seg(t, 0, 0.45), 3)
        tail = 0.1 + reach * eo(seg(t, 0.15, 0.9), 2)
        if head - tail < 0.02:
            continue
        dx, dy = math.cos(ang), math.sin(ang)
        lay = core if i % 4 == 1 else rim
        w = 0.03 if lay is core else 0.08
        lay.blade(dx * tail, dy * tail, dx * head, dy * head, w, 1 - seg(t, 0.4, 1.0), edge=0.2, bias=0.85)
    s = bump(t, 0.1, 0.6)
    core.disc(0.05, 0, 0.3 * s + 0.01, s)
    rim.disc(0.05, 0, 0.7 * s + 0.01, 0.7 * s, sy=0.7)
    for i in range(8):   # 뒤로 튀는 반동 파편
        ang = math.pi + rng.uniform(-1.2, 1.2)
        d = rng.uniform(0.25, 0.7) * eo(t, 2)
        rim.shard(math.cos(ang) * d, math.sin(ang) * d, ang, 0.05 + 0.15 * (1 - t), 0.02, (1 - t) ** 1.5)


def thunk(t, core, rim, rng):
    """바닥에 박힘: 둔탁한 먼지/돌 조각이 낮게 퍼짐 (위에서 본 시점, 런타임에서 납작하게)."""
    s = bump(t, 0.06, 0.5)
    core.disc(0, 0, 0.25 * s + 0.01, s)
    rim.disc(0, 0, 0.9 * eo(t) + 0.2, 0.45 * (1 - t) ** 2)
    for i in range(16):
        ang = i * math.tau / 16 + rng.uniform(-0.25, 0.25)
        d = rng.uniform(0.4, 1.4) * eo(t, 3)
        rim.shard(math.cos(ang) * d, math.sin(ang) * d, ang, 0.06 + 0.25 * (1 - t), 0.04, (1 - seg(t, 0.2, 1.0)) ** 1.3, 0.75)


EFFECTS = {
    #  이름        함수       프레임  재생(초)
    "puncture": (puncture, 12, 0.30),
    "slash":    (slash,    14, 0.36),
    "snip":     (snip,     14, 0.36),
    "flash":    (flash,    8,  0.18),
    "muzzle":   (muzzle,   8,  0.17),
    "thunk":    (thunk,    10, 0.26),
}


def clear_fx():
    for obj in list(scene.collection.objects):
        if obj is not cam:
            mesh = obj.data
            bpy.data.objects.remove(obj)
            bpy.data.meshes.remove(mesh)


def render_to(path):
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def load_pixels(path):
    img = bpy.data.images.load(str(path))
    px = np.empty(SIZE * SIZE * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(SIZE, SIZE, 4)   # 아래→위 행 순서 (Blender 규약)


def save_strip(frames, path):
    strip = np.concatenate(frames, axis=1)
    h, w = strip.shape[:2]
    img = bpy.data.images.new(path.stem, width=w, height=h, alpha=True)
    img.pixels.foreach_set(strip.ravel())
    img.filepath_raw = str(path)
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


meta = {}
only = [a for a in sys.argv[sys.argv.index("--") + 2:]]
for name, (fn, count, duration) in EFFECTS.items():
    if only and name not in only:
        continue
    layers = {"core": [], "rim": []}
    for i in range(count):
        t = i / (count - 1)
        rng = random.Random(sum(map(ord, name)) * 31)   # 프레임 간 같은 파편 → 연속 운동
        core, rim = Layer(), Layer()
        fn(t, core, rim, rng)
        for lname, layer in (("core", core), ("rim", rim)):
            clear_fx()
            if layer.v:
                layer.build(f"{name}_{lname}")
            path = TMP / f"{name}_{lname}_{i:02d}.png"
            render_to(path)
            layers[lname].append(load_pixels(path))
    for lname, frames in layers.items():
        save_strip(frames, OUT / f"{name}_{lname}.png")
    meta[name] = {"frames": count, "size": SIZE, "duration": duration}
    print("flipbook", name, count)

# 메타데이터: 기존 항목과 병합
meta_js = ROOT / "web/src/game/flipbooks.js"
if meta_js.exists() and only:
    old = meta_js.read_text(encoding="utf-8")
    try:
        prev = json.loads(old[old.index("{"):old.rindex("}") + 1])
        prev.update(meta)
        meta = prev
    except ValueError:
        pass
meta_js.write_text(
    "// Generated by tools/render_flipbooks.py (Blender). Re-render instead of editing.\n"
    f"export const FLIPBOOKS = {json.dumps(meta, indent=1)};\n", encoding="utf-8")
bpy.ops.wm.save_as_mainfile(filepath=str(SRC / "flipbooks.blend"), compress=True)
