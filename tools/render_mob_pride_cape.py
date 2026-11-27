"""25차 교만 차저 돌진 꼬리 다시 뽑기: 2D 리본(23차 mobdash_pride) 대신 Blender 3D 벨벳 망토.

  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b -P tools/render_mob_pride_cape.py -- C:/Project/TOD
  python tools/bake_mob_pride_cape.py        # 낱장 → Assets/Art/VFX/16/mob_pride_dash.png + _glow.png (잉크 외곽, 빛)

사용자: "이펙트 구림, 블렌더로 자연스럽게, 톤이 너무 밝아 명도 낮춰서".
- 칸 = 월드 x, y ∈ [-2, 2] (render_element_vfx.py 와 같음), 가운데 = 적 몸, +x = 돌진 방향, 망토는 −x 로 끌린다.
- 망토: 어깨(x = -.12, 폭 .5)에 매달린 사다리꼴 천(꼬리 폭 1.15, 길이 1.8), 꼬리로 갈수록 처지고 넓어진다.
  물결 = 꼬리 쪽으로 커지는 진행파(옆 펄럭임 z + 위아래 y), 8칸에 한 바퀴라 끊김 없이 반복.
- 재질: 어두운 자두색 벨벳(Sheen) + 가장자리 낮은 채도의 금테. 빛은 어둡게(위 왼쪽 따뜻한 약한 키, 오른쪽 차가운 채움).
출력: art_src/vfx/24/pride_cape/frame_00~07.png (256px, 투명 바탕), art_src/vfx/24/pride_cape.blend
"""
import math
import sys
from pathlib import Path

import bpy

ROOT = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
OUT = ROOT / "art_src/vfx/24/pride_cape"
OUT.mkdir(parents=True, exist_ok=True)
FRAMES, SIZE = 8, 256
TAU = math.tau


def srgb(h):
    c = [int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    return tuple(((v + .055) / 1.055) ** 2.4 if v > .04045 else v / 12.92 for v in c) + (1.0,)


# ---------------------------------------------------------------- 장면
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = scene.render.resolution_y = SIZE
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.view_settings.exposure = -0.35
scene.world = bpy.data.worlds.new("w")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs[0].default_value = srgb("#140f1a")
scene.world.node_tree.nodes["Background"].inputs[1].default_value = .35

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = 4
cam.location = (0, 0, 10)
scene.collection.objects.link(cam)
scene.camera = cam


def light(name, kind, energy, color, rot, size=None):
    d = bpy.data.lights.new(name, kind)
    d.energy = energy
    d.color = color[:3]
    if size is not None:
        d.angle = size
    o = bpy.data.objects.new(name, d)
    o.rotation_euler = rot
    scene.collection.objects.link(o)


light("key", "SUN", 2.2, (1.0, .86, .7), (math.radians(-35), math.radians(-30), 0), size=math.radians(25))
light("fill", "SUN", .6, (.55, .6, 1.0), (math.radians(30), math.radians(45), 0), size=math.radians(40))
light("rim", "SUN", 1.4, (.9, .7, 1.0), (math.radians(70), math.radians(-60), math.radians(180)), size=math.radians(20))

# ---------------------------------------------------------------- 재질: 벨벳 + 금테 (UV v 가장자리, u 꼬리 끝)
mat = bpy.data.materials.new("velvet")
mat.use_nodes = True
nt = mat.node_tree
nt.nodes.clear()
out = nt.nodes.new("ShaderNodeOutputMaterial")
vel = nt.nodes.new("ShaderNodeBsdfPrincipled")
vel.inputs["Base Color"].default_value = srgb("#3a2258")
vel.inputs["Roughness"].default_value = .8
vel.inputs["Sheen Weight"].default_value = 1.0
vel.inputs["Sheen Roughness"].default_value = .45
vel.inputs["Sheen Tint"].default_value = srgb("#7a5aa8")
gold = nt.nodes.new("ShaderNodeBsdfPrincipled")
gold.inputs["Base Color"].default_value = srgb("#7a5a2a")
gold.inputs["Metallic"].default_value = .85
gold.inputs["Roughness"].default_value = .42
uv = nt.nodes.new("ShaderNodeTexCoord")
sep = nt.nodes.new("ShaderNodeSeparateXYZ")
nt.links.new(uv.outputs["UV"], sep.inputs[0])


def edge_mask(sock, lo, hi):
    """lo 아래 또는 hi 위 = 1"""
    a = nt.nodes.new("ShaderNodeMath"); a.operation = "LESS_THAN"; a.inputs[1].default_value = lo
    b = nt.nodes.new("ShaderNodeMath"); b.operation = "GREATER_THAN"; b.inputs[1].default_value = hi
    nt.links.new(sock, a.inputs[0]); nt.links.new(sock, b.inputs[0])
    m = nt.nodes.new("ShaderNodeMath"); m.operation = "MAXIMUM"
    nt.links.new(a.outputs[0], m.inputs[0]); nt.links.new(b.outputs[0], m.inputs[1])
    return m.outputs[0]


trim_v = edge_mask(sep.outputs["Y"], .07, .93)
trim_u = nt.nodes.new("ShaderNodeMath"); trim_u.operation = "GREATER_THAN"; trim_u.inputs[1].default_value = .955
nt.links.new(sep.outputs["X"], trim_u.inputs[0])
trim = nt.nodes.new("ShaderNodeMath"); trim.operation = "MAXIMUM"
nt.links.new(trim_v, trim.inputs[0]); nt.links.new(trim_u.outputs[0], trim.inputs[1])
mix = nt.nodes.new("ShaderNodeMixShader")
nt.links.new(trim.outputs[0], mix.inputs[0])
nt.links.new(vel.outputs[0], mix.inputs[1])
nt.links.new(gold.outputs[0], mix.inputs[2])
nt.links.new(mix.outputs[0], out.inputs[0])

# ---------------------------------------------------------------- 망토 그물 (u = 어깨 0 → 꼬리 1, v = 아래 0 → 위 1)
NU, NV = 64, 28
mesh = bpy.data.meshes.new("cape")
verts, faces, uvs = [], [], []
for i in range(NU + 1):
    for j in range(NV + 1):
        verts.append((0, 0, 0))
for i in range(NU):
    for j in range(NV):
        a = i * (NV + 1) + j
        faces.append((a, a + NV + 1, a + NV + 2, a + 1))
mesh.from_pydata(verts, [], faces)
uvl = mesh.uv_layers.new(name="UV")
for poly in mesh.polygons:
    for li in poly.loop_indices:
        vi = mesh.loops[li].vertex_index
        i, j = divmod(vi, NV + 1)
        uvl.data[li].uv = (i / NU, j / NV)
for p in mesh.polygons:
    p.use_smooth = True
cape = bpy.data.objects.new("cape", mesh)
cape.data.materials.append(mat)
scene.collection.objects.link(cape)
sub = cape.modifiers.new("smooth", "SUBSURF")
sub.levels = sub.render_levels = 1


def shape(t):
    """t ∈ [0, 1) 한 바퀴. 어깨에서 좁게 모인 천이 꼬리로 갈수록 넓어지고, 길이 방향 주름 결 + 꼬리로 커지는 진행파.
    밑단은 가운데가 더 긴 둥근 곡선이고 너울거린다."""
    co = []
    for i in range(NU + 1):
        u = i / NU
        half = .1 + .52 * u ** .75                  # 어깨 폭 .2 → 꼬리 폭 1.24
        droop = -.04 - .22 * u * u                  # 꼬리 쪽이 처짐
        amp = .03 + .2 * u ** 1.25                  # 진행파 세기
        ph = TAU * (u * 1.2 - t)
        for j in range(NV + 1):
            v = j / NV
            s = v * 2 - 1
            length = 1.9 * (1 - .14 * s * s) * (1 + .035 * math.sin(TAU * (t + s * .6)))   # 밑단: 가운데가 길고 너울
            x = -.04 - length * u
            pleat = math.sin(TAU * (v * 2.4) + .5) * (.025 + .07 * u ** .7)                  # 길이 방향 주름 결 (어깨에서 갈라져 꼬리로 퍼짐)
            flap = amp * math.cos(ph + s * 1.1)                                                # 옆 펄럭임
            y = droop + amp * .9 * math.sin(ph) + s * half + pleat * .25
            z = flap * 1.7 + pleat * 2.2 + .1 * u * s * math.sin(ph * .5 + .4)
            co.append((x + .04 * u * math.sin(ph + s * 1.3), y, z))
    return co


for f in range(FRAMES):
    co = shape(f / FRAMES)
    for vtx, c in zip(mesh.vertices, co):
        vtx.co = c
    mesh.update()
    scene.render.filepath = str(OUT / f"frame_{f:02d}.png")
    bpy.ops.render.render(write_still=True)
    print("cape frame", f, flush=True)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT.parent / "pride_cape.blend"), compress=True)
