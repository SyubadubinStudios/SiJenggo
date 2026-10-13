"""Renders a contact sheet of clips from an exported model (headless review of animation poses).

    blender --background --factory-startup --python tools/blender/preview_clips.py -- bima out.png idle:20 walk:8 victory:30
"""
import math
import os
import sys

import bpy
from mathutils import Vector

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
exec(open(os.path.join(REPO, "tools", "blender", "sijenggo_pipeline.py"), encoding="utf-8").read())  # noqa: S102

args = sys.argv[sys.argv.index("--") + 1:]
key, out, shots = args[0], args[1], [(s.split(":")[0], int(s.split(":")[1])) for s in args[2:]]
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.join(REPO, "assets", "models", key + ".glb"))
for o in [o for o in bpy.data.objects if o.type == "MESH" and not any(m.type == "ARMATURE" for m in o.modifiers)]:
    bpy.data.objects.remove(o)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
mesh = next(o for o in bpy.data.objects if o.type == "MESH")
arm.animation_data_create()
H = mesh.dimensions.z
spacing = H * 0.75
names = contact_sheet(arm, mesh, shots, spacing)
mesh.hide_render = True
arm.hide_render = True
scene = bpy.context.scene
cam_data = bpy.data.cameras.new("cam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = max(len(shots) * spacing, H * 1.3) * 1.05
cam = bpy.data.objects.new("cam", cam_data)
scene.collection.objects.link(cam)
cx = (len(shots) - 1) * spacing / 2
cam.location = Vector((cx + 1.5, -9, H * 0.6 + 0.8))
direction = Vector((cx, 0, H * 0.6)) - cam.location
cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
scene.camera = cam
light = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
light.rotation_euler = (math.radians(50), 0, math.radians(30))
scene.collection.objects.link(light)
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.color_type = "TEXTURE"
scene.render.resolution_x = 220 * len(shots)
scene.render.resolution_y = int(scene.render.resolution_x * H * 1.35 / cam_data.ortho_scale)
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print("wrote", out)
