"""Builds one hero end to end: mesh, skeleton, skin, clips, and (optionally) the FBX for Unity.

From Blender's Scripting tab or the MCP:   import bt_build; bt_build.build('eli')
Headless, everything + export:             blender -b -P unity/art/blender/bt_build.py -- all
"""
import importlib
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else '/Users/daniel/Documents/BajoTerra/unity/art/blender'
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bt_lib, bt_face, bt_slugs, bt_slug_models, bt_heads, bt_weapons, bt_rig, bt_chars  # noqa: E402

OUT = os.path.normpath(os.path.join(HERE, '..', '..', 'BajoTerraClient', 'Assets', 'Resources', 'Models'))
NAMES = ('eli', 'trixie', 'kord', 'pronto')


def reload_all():
    for m in (bt_lib, bt_face, bt_slugs, bt_slug_models, bt_heads, bt_weapons, bt_rig, bt_chars):   # dependencies first
        importlib.reload(m)


def build(name, offset=(0, 0, 0), animate=True, suffix=''):
    """Builds `name` in the current scene. suffix keeps several heroes apart in one preview scene."""
    reload_all()
    bt_lib.SUFFIX = suffix
    col = bt_lib.collection('Bajoterra')
    body, J, S = bt_chars.build_character(name, col)
    arm = bt_rig.build_armature('Armature', J, col)
    body.name = 'Body'
    body.parent = arm
    mod = body.modifiers.new('Armature', 'ARMATURE')
    mod.object = arm
    rig = bt_rig.Rig(arm, J)
    info = {}
    if animate:
        clips = bt_rig.Clips(rig, S, J)
        info = bt_rig.author_all(rig, clips)
        info['_crouch_lean'] = clips.crouch_lean
        info['_crouch_err'] = round(clips.crouch_err, 3)
    arm.location = Vector(offset)
    bt_lib.SUFFIX = ''
    return arm, body, rig, info


def export(name):
    bpy.context.scene.render.fps = bt_rig.FPS
    os.makedirs(OUT, exist_ok=True)
    arm, body, rig, info = build(name)
    path = os.path.join(OUT, name + '.fbx')
    bt_rig.export_fbx(path, [arm, body])
    return path, info


SLUG_OUT = os.path.join(OUT, 'Slugs')


def export_slug(kind):
    """One of the fifteen fired slugs (bt_slug_models) to Resources/Models/Slugs/<id>.fbx."""
    return bt_slug_models.export(kind, SLUG_OUT)


if __name__ == '__main__' and '--' in sys.argv:
    which = sys.argv[sys.argv.index('--') + 1:]
    for n in (NAMES if which == ['all'] else which):
        if n == 'slugs':
            for kind in bt_slug_models.IDS:
                print('exported', export_slug(kind))
            continue
        if n.startswith('slug:'):
            print('exported', export_slug(n[5:]))
            continue
        bt_lib.clear_scene()
        print('exported', export(n))
