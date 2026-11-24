"""21차 증강 연출 (Blender 5.3, 2판): 파란 아크 몇 줄기가 캐릭터 발밑 바닥에 살짝 감겨 붙었다가 노이즈 디졸브로 사라진다.
(1판의 금빛 실 나선 감기는 사용자 요청 "너무 과함"으로 바꿈. 1판은 커밋 전에 덮어써서 남아 있지 않음)

실행:
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b -P tools/render_augment_growth.py -- C:/Project/TOD

출력 (Assets/Art/VFX/16, render_element_vfx.py 와 같은 형식: 가로 1줄 스트립, 정사각 칸, 본체 + _glow):
  growth_back.png / growth_back_glow.png     발밑 고리의 뒤쪽 반 (캐릭터 뒤 Ground 층)
  growth_front.png / growth_front_glow.png   발밑 고리의 앞쪽 반과 작은 불티 (캐릭터 앞 Effects 층)
  피벗은 가운데(.5, .5). 발(바닥점)은 칸 높이의 30% 아래(y = -1.2).
  칸 수 20. 게임은 fps 0 = 효과 시간에 맞춰 한 번 재생 (TOD/Setup/16. Element Art 가 visual.growthBack/Front 에 연결).
디졸브는 셰이더에서: 노이즈 > 문턱값이면 보이고, 문턱값 바로 위 띠는 밝은 하늘색으로 빛난다. 잉크 외곽 없음(가볍게).
"""
import math
import random
import sys
from pathlib import Path

import bpy
import numpy as np

ROOT = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
OUT = ROOT / "Assets/Art/VFX/16"
SRC = ROOT / "art_src/vfx/21"
TMP = SRC / "_frames"
for d in (OUT, SRC, TMP):
    d.mkdir(parents=True, exist_ok=True)

TAU = math.tau
PY = .67           # 바닥 원 납작함
GY = -1.2          # 발
H = 2.35           # 발 → 머리 위
FRAMES = 20
SIZE = 256


def hexc(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (1, 3, 5))


def lin(c):
    return tuple(((v + .055) / 1.055) ** 2.4 if v > .04045 else v / 12.92 for v in c)


INK_SRGB = np.array(hexc("#1b1517"), dtype=np.float32)
BLUE = {k: lin(hexc(v)) for k, v in dict(deep="#1d3f7a", mid="#3f86e0", light="#8cc8ff", hi="#e8f6ff", glow="#3d8cff").items()}
EMBER = lin(hexc("#d6efff"))       # 디졸브 가장자리
EMBER_HOT = lin(hexc("#ffffff"))

# ---------------------------------------------------------------- scene
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 48
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


def make_material(name, dissolve):
    """꼭짓점 색 발광 + (dissolve 면) 높이와 노이즈로 문턱값 아래를 지우고 가장자리를 불씨 색으로."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    N, L = nt.nodes.new, nt.links.new
    out = N("ShaderNodeOutputMaterial")
    attr = N("ShaderNodeVertexColor"); attr.layer_name = "Col"
    emit = N("ShaderNodeEmission"); emit.inputs["Strength"].default_value = 1.0
    clear = N("ShaderNodeBsdfTransparent")
    mix = N("ShaderNodeMixShader")
    L(clear.outputs[0], mix.inputs[1]); L(emit.outputs[0], mix.inputs[2]); L(mix.outputs[0], out.inputs[0])
    if not dissolve:
        L(attr.outputs["Color"], emit.inputs["Color"]); L(attr.outputs["Alpha"], mix.inputs["Fac"])
        return mat, None
    coord = N("ShaderNodeTexCoord")
    sep = N("ShaderNodeSeparateXYZ"); L(coord.outputs["Object"], sep.inputs[0])
    noise = N("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 5.0; noise.inputs["Detail"].default_value = 4.0
    L(coord.outputs["Object"], noise.inputs["Vector"])

    def math_node(op, a, b):
        m = N("ShaderNodeMath"); m.operation = op
        for i, v in enumerate((a, b)):
            if isinstance(v, (int, float)): m.inputs[i].default_value = v
            else: L(v, m.inputs[i])
        return m.outputs[0]

    h = math_node("MULTIPLY", math_node("ADD", sep.outputs["Y"], -GY), .12)          # 바닥은 거의 평평: 노이즈가 주로 정한다
    d = math_node("ADD", h, math_node("MULTIPLY", noise.outputs["Fac"], .9))
    thr = N("ShaderNodeValue"); thr.name = "threshold"; thr.outputs[0].default_value = -1
    sub = math_node("SUBTRACT", d, thr.outputs[0])
    vis = math_node("GREATER_THAN", sub, 0.0)
    edge = math_node("MULTIPLY", vis, math_node("LESS_THAN", sub, .06))
    hot = math_node("MULTIPLY", vis, math_node("LESS_THAN", sub, .02))
    col1 = N("ShaderNodeMix"); col1.data_type = "RGBA"
    L(edge, col1.inputs["Factor"]); L(attr.outputs["Color"], col1.inputs["A"]); col1.inputs["B"].default_value = (*EMBER, 1)
    col2 = N("ShaderNodeMix"); col2.data_type = "RGBA"
    L(hot, col2.inputs["Factor"]); L(col1.outputs["Result"], col2.inputs["A"]); col2.inputs["B"].default_value = (*EMBER_HOT, 1)
    L(col2.outputs["Result"], emit.inputs["Color"])
    L(math_node("MULTIPLY", attr.outputs["Alpha"], vis), mix.inputs["Fac"])
    return mat, thr


MAT_D, THR_D = make_material("growth_dissolve", True)
MAT_P, _ = make_material("growth_plain", False)


class Layer:
    """한 프레임 한 층의 기하 (꼭짓점 색 + 알파). mat 별로 따로 모은다."""

    def __init__(self):
        self.parts = {}
        self.z = 0.0

    def _p(self, mat):
        return self.parts.setdefault(mat.name, (mat, [], [], []))

    def _v(self, mat, x, y, col, a):
        _, v, f, c = self._p(mat)
        v.append((x, y, self.z)); c.append((*col, max(0.0, min(1.0, a))))
        return len(v) - 1

    def ribbon(self, pts, widths, col, alphas=1.0, mat=MAT_D):
        if len(pts) < 2:
            return
        n = len(pts)
        widths = widths if isinstance(widths, list) else [widths] * n
        alphas = alphas if isinstance(alphas, list) else [alphas] * n
        self.z += .001
        ids = []
        for i, (x, y) in enumerate(pts):
            a, b = pts[max(0, i - 1)], pts[min(n - 1, i + 1)]
            tx, ty = b[0] - a[0], b[1] - a[1]
            ln = math.hypot(tx, ty) or 1
            nx, ny = -ty / ln, tx / ln
            w = max(0.0, widths[i])
            ids.append((self._v(mat, x + nx * w, y + ny * w, col, alphas[i]), self._v(mat, x, y, col, alphas[i]),
                        self._v(mat, x - nx * w, y - ny * w, col, alphas[i])))
        f = self._p(mat)[2]
        for (l0, c0, r0), (l1, c1, r1) in zip(ids, ids[1:]):
            f += [(l0, c0, c1, l1), (c0, r0, r1, c1)]

    def blob(self, x, y, r, col, alpha=1.0, sy=1.0, soft=False, seg=28, mat=MAT_D):
        if r <= .002 or alpha <= .003:
            return
        self.z += .001
        c = self._v(mat, x, y, col, alpha)
        ring = [self._v(mat, x + math.cos(i * TAU / seg) * r, y + math.sin(i * TAU / seg) * r * sy, col, 0 if soft else alpha) for i in range(seg)]
        f = self._p(mat)[2]
        for i in range(seg):
            f.append((c, ring[i], ring[(i + 1) % seg]))

    def glow(self, x, y, r, col, alpha, sy=1.0, mat=MAT_D):
        self.blob(x, y, r, col, alpha, sy=sy, soft=True, seg=32, mat=mat)

    def build(self, name):
        for key, (mat, v, f, c) in self.parts.items():
            if not v:
                continue
            mesh = bpy.data.meshes.new(f"{name}_{key}")
            mesh.from_pydata(v, [], f)
            attr = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
            for poly in mesh.polygons:
                for li in poly.loop_indices:
                    attr.data[li].color = c[mesh.loops[li].vertex_index]
            mesh.materials.append(mat)
            obj = bpy.data.objects.new(mesh.name, mesh)
            scene.collection.objects.link(obj)


def clamp(v, a=0.0, b=1.0):
    return max(a, min(b, v))


def seg(t, a, b):
    return clamp((t - a) / (b - a))


def eo(t, p=3):
    return 1 - (1 - t) ** p


# ---------------------------------------------------------------- 한 칸
ARCS = [(.15, 1.9, .86), (2.35, 1.7, .8), (4.25, 1.6, .9)]   # (시작 각, 길이(라디안), 반지름)


def arc_point(ang, r):
    return math.cos(ang) * r, GY + math.sin(ang) * r * PY      # sin < 0 = 앞(아래쪽)


def frame(t, back, front, back_g, front_g, rng):
    """바닥에 붙는 파란 아크: 0~.3 미끄러지듯 그려짐, .3~.55 살짝 돌며 빛남, .5~ 디졸브."""
    draw = eo(seg(t, 0, .3), 2.5)
    crawl = .35 * eo(seg(t, 0, 1), 1.5)                  # 천천히 한쪽으로 돈다
    bright = 1 + .25 * math.sin(math.pi * seg(t, .2, .55))
    # 발밑 옅은 빛 웅덩이
    pool = seg(t, 0, .2) * (1 - seg(t, .6, 1))
    back_g.glow(0, GY, 1.0, BLUE["glow"], .22 * pool, sy=PY)
    for k, (a0, length, r) in enumerate(ARCS):
        a0 += crawl * (1 if k % 2 == 0 else -.6)
        n = 40
        span = length * draw
        if span < .03:
            continue
        runs = {True: [], False: []}
        cur, side = [], None
        for i in range(n + 1):
            u = i / n
            ang = a0 + span * u
            x, y = arc_point(ang, r * (1 + .03 * math.sin(u * 9 + k)))
            front_side = math.sin(ang) < 0
            if side is not None and front_side != side:
                cur.append((x, y, u)); runs[side].append(cur); cur = [cur[-1]]
            side = front_side
            cur.append((x, y, u))
        if cur:
            runs[side].append(cur)
        for is_front, rs in runs.items():
            Lb, G = (front, front_g) if is_front else (back, back_g)
            for run in rs:
                if len(run) < 2:
                    continue
                pts = [(x, y) for x, y, _ in run]
                prof = [math.sin(math.pi * clamp(u)) ** .7 for _, _, u in run]     # 양 끝이 가늘다
                Lb.ribbon(pts, [.03 * p for p in prof], BLUE["mid"], [min(1, .85 * bright) * (.4 + .6 * p) for p in prof])
                Lb.ribbon(pts, [.012 * p for p in prof], BLUE["hi"], [min(1, .9 * bright) * p for p in prof])
                G.ribbon(pts, [.13 * p for p in prof], BLUE["glow"], .4 * bright)
        # 아크 머리 반짝 (그려지는 동안)
        if draw < 1:
            x, y = arc_point(a0 + span, r)
            G = front_g if math.sin(a0 + span) < 0 else back_g
            G.glow(x, y, .2, BLUE["light"], .6)
    # 작은 불티 몇 개가 살짝 떠올랐다 사라짐 (디졸브 안 받음)
    r2 = random.Random(5)
    for i in range(12):
        born = r2.uniform(.4, .8)
        k = (t - born) / .25
        if not 0 <= k <= 1:
            continue
        ang = r2.uniform(0, TAU)
        x, y = arc_point(ang, r2.uniform(.7, .95))
        y += .35 * eo(k, 2)
        a = math.sin(math.pi * k)
        Lb = front if math.sin(ang) < 0 else back
        Lb.blob(x, y, .02, BLUE["hi"], a, mat=MAT_P)
        (front_g if math.sin(ang) < 0 else back_g).glow(x, y, .08, BLUE["glow"], .5 * a, mat=MAT_P)


# ---------------------------------------------------------------- 렌더 + 잉크
def clear_fx():
    for obj in list(scene.collection.objects):
        if obj is not cam:
            mesh = obj.data
            bpy.data.objects.remove(obj)
            bpy.data.meshes.remove(mesh)


def render(layer, path):
    clear_fx()
    layer.build(path.stem)
    if len(scene.collection.objects) <= 1:
        return np.zeros((SIZE, SIZE, 4), np.float32)
    scene.render.resolution_x = scene.render.resolution_y = SIZE
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(str(path))
    px = np.empty(SIZE * SIZE * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(SIZE, SIZE, 4)


def dilate(m):
    p = np.pad(m, 1)
    return p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]


def inked(px, radius=1.6):
    """실 둘레에 얇은 잉크 외곽 (게임의 종이 컷아웃 결)."""
    a = px[..., 3]
    m = a > .3
    ring = m.copy()
    for _ in range(int(math.ceil(radius))):
        ring = dilate(ring)
    ink = (ring & ~m).astype(np.float32) * .85
    oa = a + ink * (1 - a)
    rgb = (px[..., :3] * a[..., None] + INK_SRGB * (ink * (1 - a))[..., None]) / np.maximum(oa, 1e-4)[..., None]
    return np.dstack([np.clip(rgb, 0, 1), oa]).astype(np.float32)


def save(pixels, path):
    h, w = pixels.shape[:2]
    img = bpy.data.images.new(path.stem, width=w, height=h, alpha=True)
    img.pixels.foreach_set(pixels.ravel())
    img.filepath_raw = str(path)
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


sheets = {n: [] for n in ("growth_back", "growth_back_glow", "growth_front", "growth_front_glow")}
for i in range(FRAMES):
    t = i / (FRAMES - 1)
    THR_D.outputs[0].default_value = -.3 + 1.5 * eo(seg(t, .5, .95), 1.3)   # 디졸브 문턱값: 노이즈 따라 조각조각 사라짐
    layers = [Layer() for _ in range(4)]
    frame(t, layers[0], layers[2], layers[1], layers[3], random.Random(3))   # 층 순서: 뒤, 뒤 빛, 앞, 앞 빛
    for (name, out), layer in zip(sheets.items(), layers):
        px = render(layer, TMP / f"{name}_{i:02d}.png")
        if name.endswith("_glow"):
            px[..., 3] = np.minimum(px[..., 3] * .9, .6)
        # 2판: 잉크 외곽 없음 (가벼운 빛)
        out.append(px)
    print("growth frame", i, flush=True)
for name, frames in sheets.items():
    save(np.concatenate(frames, axis=1), OUT / f"{name}.png")
for f in TMP.glob("*.png"):
    f.unlink()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC / "augment_growth.blend"), compress=True)
print("growth done", FRAMES)
