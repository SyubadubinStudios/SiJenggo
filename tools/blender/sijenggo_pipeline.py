"""
Si Jenggo asset pipeline for Blender (run inside Blender, e.g. through Blender MCP).

    Rodin .glb  ->  normalise (feet on ground, height, facing)  ->  decimate (LOD0/LOD1)
                ->  humanoid armature from mesh landmarks       ->  automatic skin weights
                ->  keyframed fighting animations (actions)     ->  export .glb for ThreeNet

Usage from Blender's Python (MCP execute_blender_code):

    exec(open(r"<repo>/tools/blender/sijenggo_pipeline.py").read())
    build_character("jenggo", r"<repo>/assets/source/rodin/jenggo.glb", r"<repo>/assets/models",
                    height=1.82, style="brawler")

Conventions shared with the C# side (SiJenggo.Presentation/Actors/ModelLibrary.cs):
  * +Z up in Blender (exported as +Y up), character faces -Y in Blender (+Z in glTF).
  * Bone names below are what the engine looks up; action names are the clip names.
  * One clip per key pose family; the game scrubs clip time to match move frame data,
    so the clips are authored as "0 = anticipation, middle = impact, end = recovered".
"""

import math
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

FPS = 30
BONES = [
    # name, parent
    ("root", None), ("hips", "root"), ("spine", "hips"), ("chest", "spine"), ("neck", "chest"), ("head", "neck"),
    ("shoulder.L", "chest"), ("upper_arm.L", "shoulder.L"), ("forearm.L", "upper_arm.L"), ("hand.L", "forearm.L"),
    ("shoulder.R", "chest"), ("upper_arm.R", "shoulder.R"), ("forearm.R", "upper_arm.R"), ("hand.R", "forearm.R"),
    ("thigh.L", "hips"), ("shin.L", "thigh.L"), ("foot.L", "shin.L"),
    ("thigh.R", "hips"), ("shin.R", "thigh.R"), ("foot.R", "shin.R"),
]



def ctx(active, selected=()):
    """Operator context that works when the script runs outside the UI event loop (MCP)."""
    win = bpy.context.window_manager.windows[0]
    area = next((a for a in win.screen.areas if a.type == "VIEW_3D"), win.screen.areas[0])
    sel = list(selected) or [active]
    for o in bpy.context.view_layer.objects:
        o.select_set(o in sel)
    bpy.context.view_layer.objects.active = active
    return bpy.context.temp_override(window=win, area=area, screen=win.screen, active_object=active, object=active,
                                     selected_objects=sel, selected_editable_objects=sel)


# ───────────────────────────── import + normalise ─────────────────────────────

def clear_scene():
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.context.scene.render.fps = FPS


def import_model(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    obj = meshes[0]
    if len(meshes) > 1:
        with ctx(obj, meshes):
            bpy.ops.object.join()
    # Drop the empty parents the importer creates, keeping the world transform.
    with ctx(obj):
        bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    for o in list(bpy.data.objects):
        if o.type == "EMPTY":
            bpy.data.objects.remove(o)
    return obj


def normalise(obj, height):
    """Feet at Z=0, centred on X/Y, scaled to the target height, transforms applied."""
    with ctx(obj):
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    co = np.array([v.co[:] for v in obj.data.vertices])
    lo, hi = co.min(axis=0), co.max(axis=0)
    scale = height / (hi[2] - lo[2])
    centre = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]))
    obj.data.transform(Matrix.Scale(scale, 4) @ Matrix.Translation(-centre))
    obj.data.update()
    return obj


def decimate(obj, target_faces):
    faces = len(obj.data.polygons)
    if faces <= target_faces:
        return obj
    # Evaluate the modifier without operators (works from any context, keeps vertex groups).
    others = [m for m in obj.modifiers]
    for m in others:
        m.show_viewport = False
    mod = obj.modifiers.new("lod", "DECIMATE")
    mod.ratio = target_faces / faces
    mod.use_collapse_triangulate = True
    depsgraph = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph), preserve_all_data_layers=True, depsgraph=depsgraph)
    obj.modifiers.remove(mod)
    for m in others:
        m.show_viewport = True
    old = obj.data
    obj.data = mesh
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return obj


def shrink_textures(obj, size):
    for slot in obj.material_slots:
        if not slot.material or not slot.material.use_nodes:
            continue
        for node in slot.material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image and max(node.image.size) > size:
                node.image.scale(size, size)


# ───────────────────────────── rig ─────────────────────────────

def landmarks(obj):
    """Joint positions estimated from a T/A-pose mesh facing -Y (Blender), feet at Z=0."""
    co = np.array([v.co[:] for v in obj.data.vertices])
    H = co[:, 2].max()
    xmax = np.abs(co[:, 0]).max()

    def side_mean(sign, zlo, zhi, xlo=0.0, xhi=10.0):
        m = (co[:, 2] >= zlo) & (co[:, 2] <= zhi) & (sign * co[:, 0] >= xlo) & (sign * co[:, 0] <= xhi)
        pts = co[m]
        return pts.mean(axis=0) if len(pts) else None

    j = {}
    for s, sign in (("L", 1.0), ("R", -1.0)):
        hand = side_mean(sign, 0.0, H, xmax - 0.06 * H)
        shoulder_band = side_mean(sign, 0.55 * H, 0.9 * H, 0.17 * H, 0.24 * H)
        shoulder_z = shoulder_band[2] if shoulder_band is not None else 0.8 * H
        shoulder = Vector((sign * 0.11 * H, 0.0, shoulder_z + 0.02 * H))
        hand_v = Vector((hand[0], 0.0, hand[2])) if hand is not None else Vector((sign * 0.85 * H, 0, shoulder_z))
        wrist = shoulder.lerp(hand_v, 0.86)
        elbow = shoulder.lerp(wrist, 0.5)
        leg = side_mean(sign, 0.2 * H, 0.35 * H, 0.0)
        leg_x = leg[0] if leg is not None else sign * 0.07 * H
        j["shoulder." + s] = (Vector((sign * 0.03 * H, 0, shoulder.z - 0.01 * H)), shoulder)
        j["upper_arm." + s] = (shoulder, elbow)
        j["forearm." + s] = (elbow, wrist)
        j["hand." + s] = (wrist, hand_v + (hand_v - wrist) * 0.15)
        j["thigh." + s] = (Vector((leg_x, 0, 0.5 * H)), Vector((leg_x, -0.01 * H, 0.27 * H)))
        j["shin." + s] = (Vector((leg_x, -0.01 * H, 0.27 * H)), Vector((leg_x, 0.01 * H, 0.05 * H)))
        j["foot." + s] = (Vector((leg_x, 0.01 * H, 0.05 * H)), Vector((leg_x, -0.09 * H, 0.01 * H)))
    j["root"] = (Vector((0, 0, 0)), Vector((0, 0.1 * H, 0)))
    j["hips"] = (Vector((0, 0, 0.5 * H)), Vector((0, 0, 0.58 * H)))
    j["spine"] = (Vector((0, 0, 0.58 * H)), Vector((0, 0, 0.68 * H)))
    j["chest"] = (Vector((0, 0, 0.68 * H)), Vector((0, 0, 0.8 * H)))
    j["neck"] = (Vector((0, 0, 0.8 * H)), Vector((0, 0, 0.86 * H)))
    j["head"] = (Vector((0, 0, 0.86 * H)), Vector((0, 0, H)))
    return j, H


def build_armature(obj, name):
    joints, H = landmarks(obj)
    arm_data = bpy.data.armatures.new(name + "_rig")
    arm = bpy.data.objects.new(name + "_rig", arm_data)
    bpy.context.collection.objects.link(arm)
    with ctx(arm):
        bpy.ops.object.mode_set(mode="EDIT")
    eb = {}
    for bone, parent in BONES:
        b = arm_data.edit_bones.new(bone)
        b.head, b.tail = joints[bone]
        b.roll = 0.0
        if parent:
            b.parent = eb[parent]
            b.use_connect = False
        b.use_deform = bone != "root"
        eb[bone] = b
    with ctx(arm):
        bpy.ops.object.mode_set(mode="OBJECT")
    return arm, H


def _clear_skin(obj):
    for g in list(obj.vertex_groups):
        obj.vertex_groups.remove(g)
    for m in list(obj.modifiers):
        if m.type == "ARMATURE":
            obj.modifiers.remove(m)
    obj.parent = None


def _heat(obj, arm):
    with ctx(arm, [obj, arm]):
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    weighted = sum(1 for v in obj.data.vertices if len(v.groups))
    return weighted >= 0.9 * len(obj.data.vertices), weighted


def skin(obj, arm):
    """Bone heat on the mesh itself; if the mesh is not watertight, bone heat on a voxel-remeshed
    proxy and transfer the weights back; envelopes only as a last resort."""
    ok, weighted = _heat(obj, arm)
    if ok:
        return "heat"
    print(f"bone heat weighted {weighted}/{len(obj.data.vertices)} vertices, trying a voxel proxy")
    _clear_skin(obj)
    proxy = obj.copy()
    proxy.data = obj.data.copy()
    proxy.name = obj.name + "_proxy"
    bpy.context.collection.objects.link(proxy)
    remesh = proxy.modifiers.new("voxel", "REMESH")
    remesh.mode = "VOXEL"
    remesh.voxel_size = max(proxy.dimensions) * 0.012
    depsgraph = bpy.context.evaluated_depsgraph_get()
    proxy.data = bpy.data.meshes.new_from_object(proxy.evaluated_get(depsgraph), depsgraph=depsgraph)
    proxy.modifiers.clear()
    ok, weighted = _heat(proxy, arm)
    if ok:
        for g in proxy.vertex_groups:
            obj.vertex_groups.new(name=g.name)
        dt = obj.modifiers.new("weights", "DATA_TRANSFER")
        dt.object = proxy
        dt.use_vert_data = True
        dt.data_types_verts = {"VGROUP_WEIGHTS"}
        dt.vert_mapping = "POLYINTERP_NEAREST"
        dt.layers_vgroup_select_src = "ALL"
        dt.layers_vgroup_select_dst = "NAME"
        with ctx(obj):
            bpy.ops.object.modifier_apply(modifier=dt.name)
        bpy.data.objects.remove(proxy)
        obj.parent = arm
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = arm
        return "heat-proxy"
    print("proxy bone heat failed too, using envelopes")
    bpy.data.objects.remove(proxy)
    _clear_skin(obj)
    with ctx(arm, [obj, arm]):
        bpy.ops.object.parent_set(type="ARMATURE_ENVELOPE")
    return "envelope"


# ───────────────────────────── animation ─────────────────────────────

def _world_quat(arm, bone, rotations):
    """Rotations given around armature-space axes, converted into the bone's local frame."""
    q = Quaternion()
    for axis, deg in rotations:
        v = {"X": Vector((1, 0, 0)), "Y": Vector((0, 1, 0)), "Z": Vector((0, 0, 1))}[axis]
        q = Quaternion(v, math.radians(deg)) @ q
    rest = arm.data.bones[bone].matrix_local.to_quaternion()
    return rest.inverted() @ q @ rest


GUARD = {
    "upper_arm.L": [("Y", 70), ("Z", -75)], "forearm.L": [("Y", -120)],
    "upper_arm.R": [("Y", -70), ("Z", 75)], "forearm.R": [("Y", 120)],
    "spine": [("X", 6)], "thigh.L": [("X", -8)], "thigh.R": [("X", 10)], "shin.L": [("X", 12)], "shin.R": [("X", 10)],
}
RELAXED = {"upper_arm.L": [("Y", 72)], "upper_arm.R": [("Y", -72)], "forearm.L": [("Z", -12)], "forearm.R": [("Z", 12)]}


def over(base, **changes):
    """Base pose with some bones replaced (use __ for '.' in bone names)."""
    out = dict(base)
    for k, v in changes.items():
        out[k.replace("__", ".")] = v
    return out


def key_pose(arm, frame, pose, hips_offset=(0, 0, 0), root_rot=()):
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = _world_quat(arm, pb.name, pose.get(pb.name, []))
        pb.keyframe_insert("rotation_quaternion", frame=frame)
    hips = arm.pose.bones["hips"]
    # Offsets are authored in armature space; pose-bone location is in the bone's rest frame.
    hips.location = arm.data.bones["hips"].matrix_local.to_quaternion().inverted() @ Vector(hips_offset)
    hips.keyframe_insert("location", frame=frame)
    root = arm.pose.bones["root"]
    root.rotation_quaternion = _world_quat(arm, "root", list(root_rot))
    root.keyframe_insert("rotation_quaternion", frame=frame)


def make_action(arm, name, keys, loop=False):
    """keys: list of (time_seconds, pose, hips_offset, root_rot)."""
    action = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = action
    for t, pose, *rest in keys:
        hips = rest[0] if len(rest) > 0 else (0, 0, 0)
        root = rest[1] if len(rest) > 1 else ()
        key_pose(arm, 1 + round(t * FPS), pose, hips, root)
    action.use_fake_user = True
    # Keep every clip in the NLA so the exporter writes all of them.
    track = arm.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 1, action)
    strip.name = name
    arm.animation_data.action = None
    return action


def author_clips(arm, H):
    g = GUARD
    bob = 0.015 * H
    P = lambda **kw: over(g, **kw)  # noqa: E731

    make_action(arm, "idle", [(0, g, (0, 0, 0)), (0.5, P(spine=[("X", 9)]), (0, 0, -bob)), (1.0, g, (0, 0, 0))], loop=True)
    stepA = P(thigh__L=[("X", -32)], shin__L=[("X", 20)], thigh__R=[("X", 26)], shin__R=[("X", 35)])
    stepB = P(thigh__L=[("X", 26)], shin__L=[("X", 35)], thigh__R=[("X", -32)], shin__R=[("X", 20)])
    make_action(arm, "walk", [(0, stepA), (0.2, g, (0, 0, bob)), (0.4, stepB), (0.6, g, (0, 0, bob)), (0.8, stepA)], loop=True)

    jab = P(upper_arm__L=[("Z", -88)], forearm__L=[], spine=[("Z", -12), ("X", 8)])
    make_action(arm, "jab", [(0, g), (0.1, jab), (0.2, jab), (0.36, g)])
    cross = P(upper_arm__R=[("Z", 90)], forearm__R=[], spine=[("Z", 22), ("X", 10)], thigh__R=[("X", 18)])
    make_action(arm, "cross", [(0, g), (0.12, cross), (0.22, cross), (0.4, g)])
    hook_wind = P(upper_arm__R=[("Y", -10), ("Z", 10)], forearm__R=[("Z", 80)], spine=[("Z", -20)])
    hook = P(upper_arm__R=[("Y", -10), ("Z", 80)], forearm__R=[("Z", 70)], spine=[("Z", 30)])
    make_action(arm, "hook", [(0, g), (0.1, hook_wind), (0.2, hook), (0.28, hook), (0.46, g)])
    upper_wind = P(upper_arm__R=[("Y", -75), ("Z", 30)], forearm__R=[("Y", 110)], spine=[("X", 18)], thigh__L=[("X", -25)], shin__L=[("X", 40)], thigh__R=[("X", -10)], shin__R=[("X", 40)])
    upper = P(upper_arm__R=[("Z", 90), ("X", -65)], forearm__R=[("Y", 50)], spine=[("X", -12)])
    make_action(arm, "uppercut", [(0, g), (0.15, upper_wind, (0, 0, -0.06 * H)), (0.27, upper, (0, 0, 0.03 * H)), (0.4, upper), (0.6, g)])
    kick_chamber = P(thigh__R=[("X", -80)], shin__R=[("X", 90)], spine=[("X", -8)])
    kick = P(thigh__R=[("X", -88)], shin__R=[("X", 0)], spine=[("X", -18)], upper_arm__L=[("Y", 40), ("Z", -30)])
    make_action(arm, "kick", [(0, g), (0.12, kick_chamber), (0.22, kick), (0.32, kick), (0.52, g)])
    round_k = P(thigh__R=[("X", -70), ("Y", 50)], shin__R=[("X", 10)], spine=[("Z", 40), ("Y", -15)])
    make_action(arm, "roundkick", [(0, g), (0.12, kick_chamber), (0.24, round_k, (0, 0, 0), [("Z", 40)]), (0.34, round_k, (0, 0, 0), [("Z", 70)]), (0.56, g)])
    sweep = P(thigh__L=[("X", -60)], shin__L=[("X", 120)], thigh__R=[("Y", 70)], shin__R=[], spine=[("X", 25)])
    make_action(arm, "sweep", [(0, g), (0.12, sweep, (0, 0, -0.3 * H)), (0.3, sweep, (0, 0, -0.3 * H), [("Z", 160)]), (0.42, sweep, (0, 0, -0.3 * H), [("Z", 300)]), (0.62, g)])
    spin = P(thigh__R=[("X", -70), ("Y", 60)], shin__R=[], upper_arm__L=[("Y", 20)], upper_arm__R=[("Y", -20)], forearm__L=[], forearm__R=[])
    make_action(arm, "spin", [(0, g), (0.1, spin, (0, 0, 0.05 * H)), (0.25, spin, (0, 0, 0.08 * H), [("Z", 180)]), (0.4, spin, (0, 0, 0.08 * H), [("Z", 359)]), (0.55, g)])
    raise_ = P(upper_arm__L=[("Z", -90), ("X", -105)], upper_arm__R=[("Z", 90), ("X", -105)], forearm__L=[], forearm__R=[], spine=[("X", -15)])
    smash = P(upper_arm__L=[("Z", -90), ("X", 30)], upper_arm__R=[("Z", 90), ("X", 30)], forearm__L=[], forearm__R=[], spine=[("X", 35)], thigh__L=[("X", -50)], shin__L=[("X", 80)], thigh__R=[("X", 10)], shin__R=[("X", 70)])
    make_action(arm, "slam", [(0, g), (0.2, raise_, (0, 0, 0.04 * H)), (0.34, smash, (0, 0, -0.22 * H)), (0.5, smash, (0, 0, -0.22 * H)), (0.75, g)])
    slash_wind = P(upper_arm__R=[("Z", 60), ("X", -110)], forearm__R=[], spine=[("Z", -25)])
    slash = P(upper_arm__R=[("Z", 105), ("X", 40)], forearm__R=[], spine=[("Z", 30), ("X", 15)])
    make_action(arm, "slash", [(0, g), (0.14, slash_wind), (0.24, slash), (0.32, slash), (0.5, g)])
    thrust = P(upper_arm__R=[("Z", 88)], forearm__R=[], spine=[("Z", 15), ("X", 18)], thigh__L=[("X", -45)], shin__L=[("X", 40)], thigh__R=[("X", 30)])
    make_action(arm, "thrust", [(0, g), (0.12, thrust, (0, -0.15 * H, -0.06 * H)), (0.26, thrust, (0, -0.15 * H, -0.06 * H)), (0.45, g)])
    grab = P(upper_arm__L=[("Z", -80), ("Y", 15)], upper_arm__R=[("Z", 80), ("Y", -15)], forearm__L=[("Z", -20)], forearm__R=[("Z", 20)], spine=[("X", 12)])
    make_action(arm, "grab", [(0, g), (0.15, grab), (1.0, grab)])
    throw_ = P(upper_arm__L=[("Z", -90), ("X", -100)], upper_arm__R=[("Z", 90), ("X", -100)], forearm__L=[], forearm__R=[], spine=[("X", -20), ("Z", 40)])
    make_action(arm, "throw", [(0, grab), (0.2, over(grab, spine=[("Z", -50)])), (0.35, throw_), (0.6, g)])
    carry = P(upper_arm__L=[("Y", -80), ("Z", -20)], upper_arm__R=[("Y", 80), ("Z", 20)], forearm__L=[("Y", -50)], forearm__R=[("Y", 50)])
    make_action(arm, "carry", [(0, carry), (1.0, carry)], loop=True)
    cast = P(upper_arm__L=[("Y", -50), ("Z", -25)], upper_arm__R=[("Y", 50), ("Z", 25)], forearm__L=[], forearm__R=[], spine=[("X", -20)], head=[("X", -20)])
    make_action(arm, "cast", [(0, g), (0.3, cast, (0, 0, 0.03 * H)), (0.7, cast, (0, 0, 0.03 * H)), (1.0, g)])
    roar = P(upper_arm__L=[("Y", 20), ("Z", 30)], upper_arm__R=[("Y", -20), ("Z", -30)], forearm__L=[("Z", -60)], forearm__R=[("Z", 60)], spine=[("X", -22)], head=[("X", -25)], thigh__L=[("Y", -15)], thigh__R=[("Y", 15)])
    make_action(arm, "roar", [(0, g), (0.3, roar, (0, 0, -0.05 * H)), (0.8, roar, (0, 0, -0.05 * H)), (1.0, g)])
    block = P(upper_arm__L=[("Z", -60), ("Y", 30)], forearm__L=[("Z", -110)], upper_arm__R=[("Z", 60), ("Y", -30)], forearm__R=[("Z", 110)], spine=[("X", -5)])
    make_action(arm, "counter", [(0, g), (0.06, block), (0.3, block), (0.4, cross), (0.6, g)])
    hit = P(spine=[("X", -22)], head=[("X", -25)], upper_arm__L=[("Y", 40)], upper_arm__R=[("Y", -40)])
    make_action(arm, "hit", [(0, g), (0.06, hit, (0, 0.06 * H, 0)), (0.3, g)])
    down = over(RELAXED, spine=[("X", -10)])
    # The root pivots at the feet; with it tipped back, armature -Y points up, so lift the hips along -Y.
    make_action(arm, "knockdown", [(0, hit), (0.25, down, (0, -0.03 * H, 0), [("X", -55)]), (0.45, down, (0, -0.07 * H, 0), [("X", -88)]), (1.0, down, (0, -0.07 * H, 0), [("X", -88)])])
    kneel = P(thigh__L=[("X", -90)], shin__L=[("X", 90)], thigh__R=[("X", 10)], shin__R=[("X", 100)], spine=[("X", 20)])
    make_action(arm, "getup", [(0, down, (0, -0.07 * H, 0), [("X", -88)]), (0.3, kneel, (0, 0, -0.3 * H), [("X", -20)]), (0.6, g)])
    tuck = P(thigh__L=[("X", -70)], shin__L=[("X", 100)], thigh__R=[("X", -40)], shin__R=[("X", 90)])
    make_action(arm, "jump", [(0, g), (0.15, tuck), (0.6, tuck)])
    downed = over(kneel, upper_arm__L=[("Y", 75)], upper_arm__R=[("Y", -75)], spine=[("X", 40)], head=[("X", 20)])
    make_action(arm, "downed", [(0, downed, (0, 0, -0.3 * H)), (1.0, over(downed, spine=[("X", 46)]), (0, 0, -0.31 * H)), (2.0, downed, (0, 0, -0.3 * H))], loop=True)
    win = P(upper_arm__R=[("Y", 85)], forearm__R=[], upper_arm__L=[("Y", 60), ("Z", -20)], spine=[("X", -10)], head=[("X", -15)])
    make_action(arm, "victory", [(0, g), (0.3, win, (0, 0, 0.03 * H)), (0.9, over(win, upper_arm__R=[("Y", 95)]), (0, 0, 0.04 * H)), (1.5, win, (0, 0, 0.03 * H))], loop=True)


# ───────────────────────────── export ─────────────────────────────

def export(obj, arm, path):
    with ctx(arm, [obj, arm]):
        bpy.ops.export_scene.gltf(
            filepath=path,
            export_format="GLB",
            use_selection=True,
            export_yup=True,
            export_skins=True,
            export_animations=True,
            export_animation_mode="NLA_TRACKS",
            export_force_sampling=True,
            export_image_format="JPEG",
            export_apply=False,
        )


def build_character(key, source, out_dir, height=1.8, lod0_faces=14000, lod1_faces=4500, texture=1024):
    import os
    clear_scene()
    obj = import_model(source)
    obj.name = key
    normalise(obj, height)
    shrink_textures(obj, texture)
    decimate(obj, lod0_faces)
    arm, H = build_armature(obj, key)
    method = skin(obj, arm)
    author_clips(arm, H)
    os.makedirs(out_dir, exist_ok=True)
    lod0 = os.path.join(out_dir, key + ".glb")
    export(obj, arm, lod0)
    # LOD1 shares the rig and clips; only the mesh is lighter.
    decimate(obj, lod1_faces)
    export(obj, arm, os.path.join(out_dir, key + "_lod1.glb"))
    print(f"{key}: weights={method} faces={len(obj.data.polygons)} -> {lod0}")
    return obj, arm


def build_prop(key, source, out_dir, size, faces=3000, texture=512, axis="max", long_axis_up=False):
    """Static prop: ground it, scale its largest dimension (or height) to `size`, decimate, export.
    Weapons pass long_axis_up=True so the blade runs along +Z (glTF +Y), which is how the game
    attaches them to the hand bone."""
    import os
    clear_scene()
    obj = import_model(source)
    obj.name = key
    with ctx(obj):
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if long_axis_up:
        co = np.array([v.co[:] for v in obj.data.vertices])
        ext = co.max(axis=0) - co.min(axis=0)
        longest = int(np.argmax(ext))
        if longest == 0:
            obj.data.transform(Matrix.Rotation(math.radians(90), 4, "Y"))
        elif longest == 1:
            obj.data.transform(Matrix.Rotation(math.radians(-90), 4, "X"))
    co = np.array([v.co[:] for v in obj.data.vertices])
    lo, hi = co.min(axis=0), co.max(axis=0)
    extent = (hi[2] - lo[2]) if axis == "height" else (hi - lo).max()
    s = size / extent
    centre = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]))
    obj.data.transform(Matrix.Scale(s, 4) @ Matrix.Translation(-centre))
    shrink_textures(obj, texture)
    decimate(obj, faces)
    os.makedirs(out_dir, exist_ok=True)
    with ctx(obj):
        bpy.ops.export_scene.gltf(filepath=os.path.join(out_dir, key + ".glb"), export_format="GLB", use_selection=True,
                                  export_yup=True, export_image_format="JPEG", export_animations=False)
    print(f"{key}: faces={len(obj.data.polygons)}")
    return obj


def rebuild_clips(arm, H):
    """Re-author every clip on an existing rig (used while tuning poses)."""
    if arm.animation_data:
        for t in list(arm.animation_data.nla_tracks):
            arm.animation_data.nla_tracks.remove(t)
        arm.animation_data.action = None
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    author_clips(arm, H)


def preview(arm, action, frame):
    """Show one frame of one clip in the viewport (mutes the NLA so only that clip plays)."""
    for t in arm.animation_data.nla_tracks:
        t.mute = True
    arm.animation_data.action = bpy.data.actions[action]
    if hasattr(arm.animation_data, "action_slot") and bpy.data.actions[action].slots:
        arm.animation_data.action_slot = bpy.data.actions[action].slots[0]
    bpy.context.scene.frame_set(frame)


def contact_sheet(arm, mesh, shots, spacing=1.2):
    """Frozen copies of the character, one per (clip, frame), laid out along X for review."""
    for o in [o for o in bpy.data.objects if o.name.startswith("sheet_")]:
        bpy.data.objects.remove(o)
    names = []
    for i, (act, fr) in enumerate(shots):
        preview(arm, act, fr)
        bpy.context.view_layer.update()
        a2 = arm.copy()
        a2.name = f"sheet_rig_{i}"
        bpy.context.collection.objects.link(a2)
        a2.animation_data_clear()
        for pb in a2.pose.bones:
            src = arm.pose.bones[pb.name]
            pb.rotation_mode = "QUATERNION"
            pb.rotation_quaternion = src.rotation_quaternion.copy()
            pb.location = src.location.copy()
        m2 = mesh.copy()
        m2.name = f"sheet_mesh_{i}"
        bpy.context.collection.objects.link(m2)
        m2.parent = a2
        for md in m2.modifiers:
            if md.type == "ARMATURE":
                md.object = a2
        a2.location = (i * spacing, 0, 0)
        names.append(m2.name)
    mesh.hide_set(True)
    arm.hide_set(True)
    return names


def clear_sheet(arm, mesh):
    for o in [o for o in bpy.data.objects if o.name.startswith("sheet_")]:
        bpy.data.objects.remove(o)
    mesh.hide_set(False)
    arm.hide_set(False)
