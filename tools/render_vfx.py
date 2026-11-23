"""Generate the editable Blender scenes and transparent battle sprites."""
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
ART = ROOT / "assets/vfx"
ART.mkdir(parents=True, exist_ok=True)
random.seed(34)

bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, color, strength):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (*color, 1)
    emission.inputs["Strength"].default_value = strength
    mat.node_tree.links.new(emission.outputs[0], output.inputs[0])
    return mat


rose = material("Rose thread", (.76, .06, .28), 1.9)
white = material("Bright edge", (1, .56, .72), 3.9)
gold = material("Brass sparks", (.94, .40, .116), 3.1)
teal = material("Dash silk", (.118, .74, .58), 2.4)
violet = material("Desire fracture", (.40, .074, .66), 2.0)
# Dodge silhouettes, ribbons and swirls share one sky-blue family.
sky = material("Dodge sky", (.08, .45, 1.0), 1.25)
frost = material("Dodge frost edge", (.40, .78, 1.0), 2.3)
# White-hot textures are colorized at runtime per thread element.
silk = material("Tintable silk", (1, 1, 1), 2.2)
core = material("Tintable core", (1, 1, 1), 5.0)


def curve(scene, name, points, thickness, mat):
    path = bpy.data.curves.new(name, "CURVE")
    path.dimensions = "3D"
    path.resolution_u = 12
    path.bevel_depth = thickness
    path.bevel_resolution = 3
    poly = path.splines.new("POLY")
    poly.points.add(len(points) - 1)
    for vertex, (x, y, z, width) in zip(poly.points, points):
        vertex.co = (x, y, z, 1)
        vertex.radius = width
    obj = bpy.data.objects.new(name, path)
    scene.collection.objects.link(obj)
    path.materials.append(mat)


def arc(scene, name, radius, start, end, height, thickness, mat, ratio=1, xwave=0):
    points = []
    for i in range(57):
        f = i / 56
        angle = start + (end - start) * f
        r = radius * (1 + xwave * math.sin(angle * 7))
        width = max(.035, math.sin(f * math.pi)**.56)
        points.append((r * math.cos(angle), r * ratio * math.sin(angle), height, width))
    curve(scene, name, points, thickness, mat)


def shards(scene, count, lower, upper, colors):
    meshes = {}
    for i in range(count):
        angle = random.uniform(0, math.tau)
        distance = random.uniform(lower, upper)
        d = Vector((math.cos(angle), math.sin(angle), .01))
        n = Vector((-d.y, d.x, .02))
        p = d * distance
        length = random.uniform(.13, .53)
        points = [p+d*length, p+n*length*.12, p-d*length*.33, p-n*length*.12, p+Vector((0,0,.06))]
        mat = random.choice(colors)
        vertices, faces = meshes.setdefault(mat, ([], []))
        base = len(vertices)
        vertices.extend(points)
        faces.extend([(base+j, base+(j+1)%4, base+4) for j in range(4)])
    for mat, (vertices, faces) in meshes.items():
        mesh = bpy.data.meshes.new(mat.name + " splinters")
        mesh.from_pydata(vertices, [], faces)
        obj = bpy.data.objects.new(mesh.name, mesh)
        scene.collection.objects.link(obj)
        obj.data.materials.append(mat)


def pull(scene):
    for i in range(9):
        angle = i * math.tau / 9 + random.uniform(-.18,.18)
        arc(scene, "Whipping thread", 1.21+i*.094, angle, angle+.92, i*.016,
            .025+i*.002, [rose,white,gold][i%3], .89, .05)
    for i in range(15):
        angle = i * math.tau/15
        points = [(math.cos(angle+f*.6)*f*2.4, math.sin(angle+f*.6)*f*2.4, 0, 1-f*.95)
                  for f in (j/20 for j in range(21))]
        curve(scene, "Impact splinter", points, .028, white if i%3 else gold)
    shards(scene, 36, .47, 2.35, [rose,white,gold])


def dash(scene):
    for i in range(17):
        height = (i-8)*.13
        extent = random.uniform(1.34,2.63)
        points = [(-extent + (3.0+extent)*f,
                   height + math.sin(f*math.tau + i*.27) * (.18+f*.12), i*.017,
                   math.sin(math.pi*f)**.7+.01)
                  for f in (j/40 for j in range(41))]
        curve(scene, "Woven dash ribbon", points, .051 if i%3 else .022, [sky,frost,sky][i%3])
    shards(scene, 22, .7, 2.7, [sky,frost])


def boss(scene):
    for i in range(14):
        f = i * math.tau / 7
        arc(scene, "Broken desire halo", 1.55+(i//7)*.74, f, f+.83, .12,
            .05 if i>=7 else .03, rose if i%2 else violet, 1, .03)
        points = [(math.cos(f+math.sin(t*30)*.052)*t*2.43, math.sin(f+math.sin(t*30)*.052)*t*2.43,
                   .1, max(.08,math.sin(t*math.pi))) for t in (.3+j*.71/36 for j in range(37))]
        curve(scene, "Radial crack", points, .047, white if i%2 else violet)
    shards(scene, 45, .76, 2.43, [violet,rose,white])


def needle(scene):
    for i in range(7):
        a = i*math.tau/7
        curve(scene, "Needle spark", [(0,0,0,1), (.83*math.cos(a),.83*math.sin(a),0,.58),
                                      (2.07*math.cos(a+.23),2.07*math.sin(a+.23),.04,.02)],
              .07, [white,gold,rose][i%3])
    shards(scene, 18, .57, 1.86, [white,gold])


def stitch(scene):
    for radius in (1.3, 1.85, 2.3):
        for i in range(5):
            start = i*math.tau/5 + radius
            arc(scene, "Memory filament", radius, start, start+1.05, .14, .033, teal if i%3 else gold)
    shards(scene, 36, .66, 2.25, [teal,white,gold])


def streak(scene):
    # Comet trail for a flying needle: head at +x, frayed silk strands behind it.
    for i in range(11):
        lane = (i-5)*.05
        length = random.uniform(2.2, 3.1)
        points = [(2.9 - length*f, lane*(1+f*2.4) + math.sin(f*9+i)*.04*f, i*.01,
                   (1-f)**1.4 + .02) for f in (j/36 for j in range(37))]
        curve(scene, "Needle comet strand", points, .05 if i == 5 else .018, core if i == 5 else silk)
    for i in range(14):
        x = random.uniform(-2.8, 2.2)
        y = random.uniform(-.34, .34) * (1 + (2.4-x)*.25)
        s = random.uniform(.05, .14)
        curve(scene, "Comet sparkle h", [(x-s,y,.1,0.2),(x,y,.1,1),(x+s,y,.1,0.2)], .02, core)
        curve(scene, "Comet sparkle v", [(x,y-s,.1,0.2),(x,y,.1,1),(x,y+s,.1,0.2)], .02, core)


def launch(scene):
    # Muzzle cone fired along +x with a stitched ring at the hand.
    for i in range(13):
        a = (i-6)*.085
        reach = random.uniform(1.6, 2.9) * (1 - abs(i-6)*.05)
        points = [(math.cos(a)*reach*f - .4, math.sin(a)*reach*f, .02, max(.03, math.sin(math.pi*min(1,f*1.3))**.6))
                  for f in (j/24 for j in range(25))]
        curve(scene, "Launch ray", points, .045 if i%3 == 0 else .022, core if i%3 == 0 else silk)
    arc(scene, "Launch ring", .62, 0, math.tau, .05, .028, silk, 1.0)
    for i in range(12):
        a = i*math.tau/12
        curve(scene, "Launch stitch", [(.5*math.cos(a), .5*math.sin(a), .1, 1), (.78*math.cos(a+.08), .78*math.sin(a+.08), .1, .4)],
              .03, core)


def dodge(scene):
    # Crescent gusts curling around the body for the dodge launch.
    for i in range(8):
        start = math.pi*.35 + i*.07
        arc(scene, "Dodge gust", 1.05+i*.19, start, start+math.pi*(.9-i*.05), i*.02,
            .05 if i%2 else .028, frost if i%3 == 0 else sky, .78, .02)
    shards(scene, 26, .9, 2.6, [sky, frost])


def shock(scene):
    # Thin snap ring with stitches for the pull release.
    arc(scene, "Snap ring", 2.2, 0, math.tau, 0, .04, core, 1.0, .01)
    arc(scene, "Snap ring inner", 1.75, .3, math.tau-.3, .02, .02, silk, 1.0, .03)
    for i in range(24):
        a = i*math.tau/24
        curve(scene, "Snap stitch", [(2.0*math.cos(a), 2.0*math.sin(a), .05, .6), (2.45*math.cos(a+.05), 2.45*math.sin(a+.05), .05, 1)],
              .028, core if i%2 else silk)


def scene_for(name, builder):
    scene = bpy.data.scenes.new(name)
    scene.world = bpy.data.worlds.new(name + " world")
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 8
    scene.render.resolution_x = 384
    scene.render.resolution_y = 384
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"
    camera = bpy.data.cameras.new("Sprite orthographic")
    camera.type = "ORTHO"
    camera.ortho_scale = 6.4
    obj = bpy.data.objects.new("Sprite camera", camera)
    obj.location = (0,0,9)
    scene.collection.objects.link(obj)
    scene.camera = obj
    scene.render.filepath = "//" + name + ".png"
    builder(scene)
    return scene


scenes = [scene_for(name, builder) for name, builder in
          [("pull", pull), ("dash", dash), ("boss", boss), ("needle", needle), ("stitch", stitch),
           ("streak", streak), ("launch", launch), ("dodge", dodge), ("shock", shock)]]
bpy.context.window.scene = scenes[0]
for s in tuple(bpy.data.scenes):
    if s not in scenes:
        bpy.data.scenes.remove(s)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / "art_src/vfx/tower-vfx.blend"), compress=True)
for scene in scenes:
    bpy.ops.render.render(scene=scene.name, write_still=True)
