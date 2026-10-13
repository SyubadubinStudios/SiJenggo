"""
Per-character animation sets for Si Jenggo (PLAN M7 item 2).

The base pipeline (sijenggo_pipeline.py) gives every rig the same 25 clips. This script opens the
exported models, replaces a few of those clips with signature versions and adds new ones, then
re-exports LOD0 and LOD1. It needs no MCP: run Blender headless from the repo root:

    blender --background --factory-startup --python tools/blender/character_styles.py
    blender --background --factory-startup --python tools/blender/character_styles.py -- bima rani

Signature clips per style (clip names are what ActorAnimator looks up; new names fall back on the C#
side: gadget -> jab, taunt -> roar, intro -> roar):
  brawler (Jenggo)  idle: hunched heavy bob · victory: double-biceps flex · taunt: beckon
  silat   (Bima)    idle/walk: low kuda-kuda stance with open hands · victory: sembah salute
  agent   (Rani)    idle: hand at the earpiece · gadget: wrist-device aim · victory: hands on hips
  sarung  (Ucup)    idle: tasbih at the chest · spin: arms-wide sarong whirl · victory: doa, palms up
  bosses            intro: one of power / tide / mystic / logger / volcano / temple / wings

Pose axes (armature space, character faces -Y): upper_arm.L +Y lowers the arm, -Z swings it forward;
a forearm turned inward with Z (L: -, R: +) then lifted with Y (L: +, R: -) brings the hands together at
the chest. Review poses with tools/blender/preview_clips.py before committing.
"""

import math
import os
import sys

import bpy

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
exec(open(os.path.join(REPO, "tools", "blender", "sijenggo_pipeline.py"), encoding="utf-8").read())  # noqa: S102

MODELS = os.path.join(REPO, "assets", "models")

STYLES = {
    "jenggo": "brawler", "bima": "silat", "rani": "agent", "ucup": "sarung",
    "bang_kodir": "boss_power", "kapten_badai": "boss_tide", "nyi_angin_laut": "boss_mystic",
    "raja_penebang": "boss_logger", "jagal_merapi": "boss_volcano", "patih_abadi": "boss_temple",
    "jenderal_garuda": "boss_wings",
}


def ctx_bg(active, selected=()):
    """Background-mode variant of ctx(): there is no 3D view, so only override the selection."""
    sel = list(selected) or [active]
    for o in bpy.context.scene.objects:
        if o is not None:
            o.select_set(o in sel)
    bpy.context.view_layer.objects.active = active
    return bpy.context.temp_override(active_object=active, object=active, selected_objects=sel, selected_editable_objects=sel)


def replace_action(arm, name, keys, loop=False):
    """Drops an existing clip of that name (action + its NLA track) and authors the new one."""
    old = bpy.data.actions.get(name)
    if arm.animation_data:
        for t in list(arm.animation_data.nla_tracks):
            if t.name == name or any(s.action == old for s in t.strips):
                arm.animation_data.nla_tracks.remove(t)
    if old is not None:
        bpy.data.actions.remove(old)
    return make_action(arm, name, keys, loop)


def style_brawler(arm, H):
    g = GUARD
    P = lambda **kw: over(g, **kw)  # noqa: E731
    hunch = P(spine=[("X", 16)], chest=[("X", 6)], head=[("X", -12)], shoulder__L=[("Y", 8)], shoulder__R=[("Y", -8)])
    roll = over(hunch, spine=[("X", 18), ("Z", 6)], shoulder__L=[("Y", 2)], shoulder__R=[("Y", -14)])
    replace_action(arm, "idle", [(0, hunch, (0, 0, -0.03 * H)), (0.6, roll, (0, 0, -0.05 * H)), (1.2, hunch, (0, 0, -0.03 * H))], loop=True)
    flex = over(RELAXED, upper_arm__L=[("Y", -5)], forearm__L=[("Y", -105)], upper_arm__R=[("Y", 5)], forearm__R=[("Y", 105)], spine=[("X", -8)], head=[("X", -10)])
    flex2 = over(flex, chest=[("X", -6)], forearm__L=[("Y", -120)], forearm__R=[("Y", 120)])
    replace_action(arm, "victory", [(0, g), (0.3, flex, (0, 0, 0.02 * H)), (0.8, flex2, (0, 0, 0.03 * H)), (1.4, flex, (0, 0, 0.02 * H))], loop=True)
    reach = P(upper_arm__R=[("Z", 80)], forearm__R=[("Y", 20)], spine=[("X", -6)], head=[("X", -10)])
    curl = over(reach, forearm__R=[("Y", 95)])
    replace_action(arm, "taunt", [(0, g), (0.2, reach), (0.4, curl), (0.6, reach), (0.8, curl), (1.1, g)])


def style_silat(arm, H):
    # Kuda-kuda: wide, low horse stance; lead hand open and forward, rear hand guarding the chest.
    stance = {
        "thigh.L": [("X", -28), ("Y", -14)], "shin.L": [("X", 42)], "thigh.R": [("X", -18), ("Y", 16)], "shin.R": [("X", 46)],
        "spine": [("X", 10), ("Z", -14)], "head": [("Z", 12)],
        "upper_arm.L": [("Y", 30), ("Z", -68)], "forearm.L": [("Y", -35)], "hand.L": [("X", -30)],
        "upper_arm.R": [("Y", -62), ("Z", 58)], "forearm.R": [("Y", 128)],
    }
    flow = over(stance, **{"upper_arm.L": [("Y", 42), ("Z", -55)], "forearm.L": [("Y", -60)], "upper_arm.R": [("Y", -52), ("Z", 70)], "forearm.R": [("Y", 112)], "spine": [("X", 10), ("Z", -8)]})
    low = (0, 0, -0.1 * H)
    replace_action(arm, "idle", [(0, stance, low), (0.8, flow, (0, 0, -0.11 * H)), (1.6, stance, low)], loop=True)
    stepA = over(stance, **{"thigh.L": [("X", -45), ("Y", -10)], "shin.L": [("X", 55)], "thigh.R": [("X", 12), ("Y", 14)], "shin.R": [("X", 50)]})
    stepB = over(stance, **{"thigh.L": [("X", 10), ("Y", -14)], "shin.L": [("X", 50)], "thigh.R": [("X", -42), ("Y", 12)], "shin.R": [("X", 55)]})
    replace_action(arm, "walk", [(0, stepA, low), (0.22, stance, (0, 0, -0.08 * H)), (0.44, stepB, low), (0.66, stance, (0, 0, -0.08 * H)), (0.88, stepA, low)], loop=True)
    # Sembah: palms together in front of the chest, a small bow.
    sembah = over(RELAXED, upper_arm__L=[("Y", 62), ("Z", -40)], forearm__L=[("Z", -95), ("Y", 70)], hand__L=[("Y", 60)],
                  upper_arm__R=[("Y", -62), ("Z", 40)], forearm__R=[("Z", 95), ("Y", -70)], hand__R=[("Y", -60)])
    bow = over(sembah, spine=[("X", 22)], head=[("X", 18)])
    replace_action(arm, "victory", [(0, GUARD), (0.4, sembah), (1.0, bow, (0, 0, -0.02 * H)), (1.8, sembah)], loop=True)


def style_agent(arm, H):
    g = GUARD
    ear = over(g, **{"upper_arm.R": [("Y", -35), ("Z", 30), ("X", -40)], "forearm.R": [("Y", 150)], "head": [("Z", -10), ("Y", 6)], "thigh.R": [("X", 4), ("Y", 8)]})
    listen = over(ear, head=[("Z", -16), ("Y", 10)], spine=[("Z", 8), ("X", 4)])
    replace_action(arm, "idle", [(0, ear), (0.9, listen, (0, 0, -0.01 * H)), (1.8, ear)], loop=True)
    aim_wind = over(g, **{"upper_arm.R": [("Z", 70)], "forearm.R": [("Y", 70)], "spine": [("Z", -10)]})
    aim = over(g, **{"upper_arm.R": [("Z", 90), ("X", -8)], "forearm.R": [("Y", 8)], "hand.R": [("X", -35)], "upper_arm.L": [("Y", 30), ("Z", -80)], "forearm.L": [("Y", -40)], "spine": [("Z", 22), ("X", 6)], "thigh.L": [("X", -20)], "shin.L": [("X", 20)]})
    make_action(arm, "gadget", [(0, g), (0.1, aim_wind), (0.2, aim), (0.38, aim), (0.55, g)]) if "gadget" not in bpy.data.actions else replace_action(arm, "gadget", [(0, g), (0.1, aim_wind), (0.2, aim), (0.38, aim), (0.55, g)])
    hips = over(RELAXED, upper_arm__L=[("Y", 50), ("Z", 10)], forearm__L=[("Z", -100), ("Y", -30)], upper_arm__R=[("Y", -50), ("Z", -10)], forearm__R=[("Z", 100), ("Y", 30)], thigh__R=[("Y", 10)], spine=[("Z", 6)], head=[("Z", -8)])
    replace_action(arm, "victory", [(0, g), (0.35, hips), (1.2, over(hips, spine=[("Z", -4)], head=[("Z", 8)])), (2.0, hips)], loop=True)


def style_sarung(arm, H):
    calm = over(RELAXED, **{"upper_arm.R": [("Y", -55), ("Z", 45)], "forearm.R": [("Y", 118)], "spine": [("X", 4)], "head": [("X", 6)]})
    count = over(calm, **{"hand.R": [("X", -25)], "head": [("X", 10)]})
    replace_action(arm, "idle", [(0, calm), (0.7, count, (0, 0, -0.008 * H)), (1.4, calm)], loop=True)
    wide = over(RELAXED, upper_arm__L=[("Y", 8)], upper_arm__R=[("Y", -8)], forearm__L=[], forearm__R=[], spine=[("X", -6)], head=[("X", -10)])
    replace_action(arm, "spin", [(0, GUARD), (0.08, wide, (0, 0, 0.02 * H)), (0.2, wide, (0, 0, 0.03 * H), [("Z", 140)]), (0.32, wide, (0, 0, 0.03 * H), [("Z", 280)]), (0.42, wide, (0, 0, 0.02 * H), [("Z", 359)]), (0.58, GUARD)])
    doa = over(RELAXED, upper_arm__L=[("Y", 65), ("Z", -45)], forearm__L=[("Z", -60), ("Y", 60)], hand__L=[("X", -30)], upper_arm__R=[("Y", -65), ("Z", 45)], forearm__R=[("Z", 60), ("Y", -60)], hand__R=[("X", -30)], head=[("X", 14)])
    replace_action(arm, "victory", [(0, GUARD), (0.5, doa), (1.6, over(doa, head=[("X", 20)])), (2.4, doa)], loop=True)


def style_boss(arm, H, kind):
    g = GUARD
    P = lambda **kw: over(g, **kw)  # noqa: E731
    roar = P(upper_arm__L=[("Y", 20), ("Z", 30)], upper_arm__R=[("Y", -20), ("Z", -30)], forearm__L=[("Z", -60)], forearm__R=[("Z", 60)], spine=[("X", -22)], head=[("X", -25)], thigh__L=[("Y", -15)], thigh__R=[("Y", 15)])
    raise_ = P(upper_arm__L=[("Z", -90), ("X", -105)], upper_arm__R=[("Z", 90), ("X", -105)], forearm__L=[], forearm__R=[], spine=[("X", -15)])
    if kind == "boss_power":
        poundL = P(upper_arm__L=[("Y", 40), ("Z", -60)], forearm__L=[("Y", -140)], spine=[("X", -10)])
        poundR = P(upper_arm__R=[("Y", -40), ("Z", 60)], forearm__R=[("Y", 140)], spine=[("X", -10)])
        keys = [(0, g), (0.3, poundL), (0.55, poundR), (0.8, poundL), (1.05, poundR), (1.5, roar, (0, 0, -0.05 * H)), (2.4, roar, (0, 0, -0.05 * H)), (2.8, g)]
    elif kind == "boss_tide":
        keys = [(0, g), (0.5, P(spine=[("X", -10), ("Z", 25)], upper_arm__R=[("Z", 60), ("X", -60)], forearm__R=[])), (1.0, raise_, (0, 0, 0.03 * H)), (1.6, raise_, (0, 0, 0.03 * H)), (2.0, roar, (0, 0, -0.05 * H)), (2.8, g)]
    elif kind == "boss_mystic":
        wide = over(RELAXED, upper_arm__L=[("Y", -20), ("X", -20)], upper_arm__R=[("Y", 20), ("X", -20)], forearm__L=[], forearm__R=[], spine=[("X", -14)], head=[("X", -20)])
        keys = [(0, g), (0.6, wide, (0, 0, 0.06 * H)), (1.4, over(wide, spine=[("X", -20)]), (0, 0, 0.12 * H)), (2.2, wide, (0, 0, 0.08 * H)), (2.8, g)]
    elif kind == "boss_logger":
        shoulder = P(upper_arm__R=[("Y", -30), ("Z", 30), ("X", -90)], forearm__R=[("Y", 120)], spine=[("Z", -12)])
        keys = [(0, g), (0.5, shoulder), (1.2, shoulder, (0, 0, -0.02 * H)), (1.6, raise_, (0, 0, 0.03 * H)), (2.1, roar, (0, 0, -0.05 * H)), (2.8, g)]
    elif kind == "boss_volcano":
        smash = P(upper_arm__L=[("Z", -90), ("X", 30)], upper_arm__R=[("Z", 90), ("X", 30)], forearm__L=[], forearm__R=[], spine=[("X", 35)], thigh__L=[("X", -50)], shin__L=[("X", 80)], thigh__R=[("X", 10)], shin__R=[("X", 70)])
        keys = [(0, g), (0.5, raise_, (0, 0, 0.04 * H)), (0.8, smash, (0, 0, -0.22 * H)), (1.4, smash, (0, 0, -0.22 * H)), (1.9, roar, (0, 0, -0.05 * H)), (2.8, g)]
    elif kind == "boss_temple":
        bow = over(RELAXED, spine=[("X", 30)], head=[("X", 20)], upper_arm__R=[("Y", -60), ("Z", 40)], forearm__R=[("Y", 110)])
        draw = P(upper_arm__R=[("Z", 60), ("X", -110)], forearm__R=[], spine=[("Z", -25)])
        keys = [(0, RELAXED), (0.8, bow), (1.4, bow), (1.9, draw, (0, 0, -0.03 * H)), (2.4, draw, (0, 0, -0.03 * H)), (2.8, g)]
    else:  # boss_wings
        folded = over(RELAXED, upper_arm__L=[("Y", 55), ("Z", -45)], forearm__L=[("Z", -100)], upper_arm__R=[("Y", -55), ("Z", 45)], forearm__R=[("Z", 100)], head=[("X", -8)])
        wings = over(RELAXED, upper_arm__L=[("Y", -10), ("Z", 15)], upper_arm__R=[("Y", 10), ("Z", -15)], forearm__L=[("Y", -10)], forearm__R=[("Y", 10)], spine=[("X", -14)], head=[("X", -18)])
        keys = [(0, folded), (0.9, folded), (1.4, wings), (2.2, wings), (2.8, g)]
    if "intro" in bpy.data.actions:
        replace_action(arm, "intro", keys)
    else:
        make_action(arm, "intro", keys)


def restyle(key):
    style = STYLES[key]
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.context.scene.render.fps = FPS
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, key + ".glb"))
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    mesh = next(o for o in bpy.data.objects if o.type == "MESH" and o.parent == arm or any(m.type == "ARMATURE" for m in getattr(o, "modifiers", [])))
    arm.animation_data_create()
    arm.animation_data.action = None
    H = mesh.dimensions.z
    {"brawler": style_brawler, "silat": style_silat, "agent": style_agent, "sarung": style_sarung}.get(style, lambda a, h: style_boss(a, h, style))(arm, H)
    for a in bpy.data.actions:
        a.use_fake_user = True
    for lod, path in (("", os.path.join(MODELS, key + ".glb")), ("_lod1", os.path.join(MODELS, key + "_lod1.glb"))):
        if lod:
            # LOD1 keeps its lighter mesh: swap the mesh data in from the existing LOD1 file.
            before = set(bpy.data.objects)
            bpy.ops.import_scene.gltf(filepath=path)
            new = [o for o in bpy.data.objects if o not in before]
            lod_mesh = next(o for o in new if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers))
            mesh.data = lod_mesh.data
            for o in new:
                if o.type == "MESH" and o is lod_mesh:
                    o.data = bpy.data.meshes.new("discard")
                bpy.data.objects.remove(o, do_unlink=True)
            bpy.context.view_layer.update()
            arm.animation_data.action = None
        with ctx_bg(arm, [mesh, arm]):
            bpy.ops.export_scene.gltf(
                filepath=path, export_format="GLB", use_selection=True, export_yup=True, export_skins=True,
                export_animations=True, export_animation_mode="NLA_TRACKS", export_force_sampling=True,
                export_image_format="JPEG", export_apply=False)
    print(f"{key}: style={style} clips={sorted(a.name for a in bpy.data.actions)}")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(STYLES)
    for k in argv:
        restyle(k)
