"""Skeleton, procedural animation and FBX export for the Bajoterra characters.

Skeleton (same names on every character, so the Unity side finds bones by name):
Root > Hips > Spine > Chest > Neck > Head ; Chest > UpperArm.L/R > LowerArm.L/R > Hand.L/R ; Hips > UpperLeg.L/R >
LowerLeg.L/R > Foot.L/R ; Hand.R > Chamber, Muzzle (locators on the blaster). `.L` is the character's left (+X), the
blaster sits in the right hand (-X). The rest pose IS the aiming pose (both hands on the blaster), so the clips only
add motion on top of it and the Unity side only has to tilt Spine/Chest to aim.

Animation is authored in code. Every pose is a set of rotations expressed in world-aligned axes (+X side, -Y forward,
+Z up; +X rotation leans forward / swings legs back, +Y leans left, +Z turns left) that is converted to the bone's own
axes, plus a two-bone IK that plants the feet, so strides, the crouch and the roll are exact on any proportions.
"""
import math

import bpy
from mathutils import Euler, Matrix, Vector

FPS = 30
V = Vector


def E(x=0.0, y=0.0, z=0.0):
    """World-aligned rotation (degrees) as a 3x3 matrix."""
    return Euler((math.radians(x), math.radians(y), math.radians(z)), 'XYZ').to_matrix()


I3 = Matrix.Identity(3)


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


# --------------------------------------------------------------------------------------------------- armature

LEG = ('UpperLeg', 'LowerLeg', 'Foot')


def bone_table(J):
    t = [
        ('Root', V((0, 0, 0)), V((0, 0, .12)), None, False),
        ('Hips', J['hips'], J['waist'], 'Root', True),
        ('Spine', J['waist'], J['chest'], 'Hips', True),
        ('Chest', J['chest'], J['neck'], 'Spine', True),
        ('Neck', J['neck'], J['head'], 'Chest', True),
        ('Head', J['head'], J['head_top'], 'Neck', True),
    ]
    for s in 'LR':
        wrist, elbow = J['wrist' + s], J['elbow' + s]
        t += [
            ('UpperArm.' + s, J['shoulder' + s], elbow, 'Chest', True),
            ('LowerArm.' + s, elbow, wrist, 'UpperArm.' + s, True),
            ('Hand.' + s, wrist, wrist + (wrist - elbow).normalized() * J['hand_len'], 'LowerArm.' + s, True),
            ('UpperLeg.' + s, J['hip' + s], J['knee' + s], 'Hips', True),
            ('LowerLeg.' + s, J['knee' + s], J['ankle' + s], 'UpperLeg.' + s, True),
            ('Foot.' + s, J['ankle' + s], J['toe' + s], 'LowerLeg.' + s, True),
        ]
    t += [
        ('Chamber', J['chamber'], J['chamber'] + V((0, 0, .03)), 'Hand.R', False),
        ('Muzzle', J['muzzle'], J['muzzle'] + V((0, -.03, 0)), 'Hand.R', False),
    ]
    return t


def build_armature(name, J, collection):
    arm = bpy.data.armatures.new(name)
    obj = bpy.data.objects.new(name, arm)
    collection.objects.link(obj)
    view = bpy.context.view_layer
    view.objects.active = obj
    for o in view.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    edit = arm.edit_bones
    for bname, head, tail, parent, deform in bone_table(J):
        b = edit.new(bname)
        b.head, b.tail = head, tail
        b.use_deform = deform
        if parent:
            b.parent = edit[parent]
        b.use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    return obj


# --------------------------------------------------------------------------------------------------- pose solver


def ik2(h, t, l1, l2, pole):
    """Knee position for a two-bone chain from hip h to ankle target t; the knee bends towards `pole`."""
    d = t - h
    dist = max(1e-5, d.length)
    dist_c = min(max(dist, abs(l1 - l2) + 1e-4), l1 + l2 - 1e-4)
    n = d / dist
    a = (l1 * l1 - l2 * l2 + dist_c * dist_c) / (2 * dist_c)
    hgt = math.sqrt(max(l1 * l1 - a * a, 0.0))
    pv = pole - n * pole.dot(n)
    pv = pv.normalized() if pv.length > 1e-6 else V((0, -1, 0))
    return h + n * a + pv * hgt


def swing(a, b):
    """Minimal rotation (3x3) taking direction a to direction b."""
    return a.normalized().rotation_difference(b.normalized()).to_matrix()


class Rig:
    def __init__(self, arm_obj, J):
        self.obj = arm_obj
        self.J = J
        bones = arm_obj.data.bones
        self.rest = {b.name: b.matrix_local.to_3x3() for b in bones}
        self.head = {b.name: b.head_local.copy() for b in bones}
        self.tail = {b.name: b.tail_local.copy() for b in bones}
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in bones}
        self.order = [b.name for b in bones]  # parents before children
        self.len = {n: (self.tail[n] - self.head[n]).length for n in self.order}
        for pb in arm_obj.pose.bones:
            pb.rotation_mode = 'QUATERNION'

    # ---- forward kinematics (positions only, used for calibration and the roll's ground contact)
    def positions(self, D, hips_t):
        pos = {}
        for n in self.order:
            p = self.parent[n]
            if p is None:
                pos[n] = self.head[n].copy()
            elif n == 'Hips':
                pos[n] = self.head[n] + hips_t
            else:
                pos[n] = pos[p] + D[p] @ (self.head[n] - self.head[p])
        return pos

    def point(self, bone, rest_point, D, pos):
        return pos[bone] + D[bone] @ (rest_point - self.head[bone])

    def solve(self, P):
        """P: pose dict -> (D world deltas per bone, hips translation)."""
        J = self.J
        D = {'Root': I3}
        t = V(P.get('hips_t', (0, 0, 0)))
        D['Hips'] = E(*P.get('hips_r', (0, 0, 0)))
        D['Spine'] = D['Hips'] @ E(*P.get('spine_r', (0, 0, 0)))
        D['Chest'] = D['Spine'] @ E(*P.get('chest_r', (0, 0, 0)))
        D['Neck'] = D['Chest'] @ E(*P.get('neck_r', (0, 0, 0)))
        D['Head'] = D['Neck'] @ E(*P.get('head_r', (0, 0, 0)))
        for s in 'LR':
            D['UpperArm.' + s] = D['Chest'] @ E(*P.get('uarm_r' + s, (0, 0, 0)))
            D['LowerArm.' + s] = D['UpperArm.' + s] @ E(*P.get('larm_r' + s, (0, 0, 0)))
            D['Hand.' + s] = D['LowerArm.' + s] @ E(*P.get('hand_r' + s, (0, 0, 0)))
        D['Chamber'] = D['Hand.R']
        D['Muzzle'] = D['Hand.R']
        pivot = self.head['Hips']
        for s in 'LR':
            hip_rest = self.head['UpperLeg.' + s]
            hip = pivot + t + D['Hips'] @ (hip_rest - pivot)
            if 'foot_local' + s in P:   # offset from the hips, turning with them (tuck, roll)
                tgt = pivot + t + D['Hips'] @ V(P['foot_local' + s])
            else:                       # target given in the world (planted feet)
                tgt = V(P['foot' + s])
            l1, l2 = self.len['UpperLeg.' + s], self.len['LowerLeg.' + s]
            knee = ik2(hip, tgt, l1, l2, V(P.get('pole' + s, (0, -1, .1))))
            up_rest = self.tail['UpperLeg.' + s] - self.head['UpperLeg.' + s]
            lo_rest = self.tail['LowerLeg.' + s] - self.head['LowerLeg.' + s]
            D['UpperLeg.' + s] = swing(up_rest, knee - hip)
            D['LowerLeg.' + s] = swing(lo_rest, tgt - knee)
            D['Foot.' + s] = E(*P.get('foot_r' + s, (0, 0, 0)))
        return D, t

    def apply(self, P, frame):
        D, t = self.solve(P)
        for n in self.order:
            pb = self.obj.pose.bones[n]
            rest = self.rest[n]
            p = self.parent[n]
            dloc = D[n] if p is None else D[p].inverted() @ D[n]
            basis = rest.inverted() @ dloc @ rest
            pb.rotation_quaternion = basis.to_quaternion()
            pb.keyframe_insert('rotation_quaternion', frame=frame)
            if n == 'Hips':
                pb.location = rest.inverted() @ t
                pb.keyframe_insert('location', frame=frame)


# --------------------------------------------------------------------------------------------------- clips


class Clips:
    """Builds the clips for one character from its proportions."""

    def __init__(self, rig, S, J):
        self.rig, self.S, self.J = rig, S, J
        self.hip_z = J['hips'].z
        self.ankle_z = J['ankleL'].z
        self.leg = J['hips'].z - self.ankle_z          # hip-to-ground when straight
        self.k = self.leg / 0.84                       # scale relative to Eli's legs
        self.hx = abs(J['ankleL'].x)
        self.crouch_lean = None
        self.crouch_drop = None
        self._calibrate_crouch()

    def ankle(self, s):
        a = self.J['ankle' + s]
        return V((a.x, a.y, a.z))

    # ---- poses ------------------------------------------------------------------------------------------
    def stand(self, breathe=0.0, sway=0.0):
        P = dict(hips_t=(0, 0, -0.012 * self.k), spine_r=(1.0 + breathe, 0, 0), chest_r=(0.5 + breathe * 1.4, 0, 0),
                 head_r=(-0.8, 0, 0), hips_r=(0, sway, 0))
        for s in 'LR':
            a = self.ankle(s)
            P['foot' + s] = V((a.x * 1.12, a.y, a.z))
        return P

    def crouch_pose(self, lean=None, drop=None):
        lean = self.crouch_lean if lean is None else lean
        drop = self.crouch_drop if drop is None else drop
        P = dict(hips_t=(0, 0.0, -drop), hips_r=(0.4 * lean, 0, 0), spine_r=(0.35 * lean, 0, 0),
                 chest_r=(0.3 * lean, 0, 0), neck_r=(-0.25 * lean, 0, 0), head_r=(-0.25 * lean, 0, 0))
        comp = -0.97 * 1.05 * lean          # arms lift back up so the blaster keeps pointing forward
        for s in 'LR':
            a = self.ankle(s)
            sx = 1 if s == 'L' else -1
            P['foot' + s] = V((a.x * 1.45, a.y - 0.03 * self.k, a.z))
            P['pole' + s] = V((sx * 0.25, -1, 0.35))
            P['uarm_r' + s] = (comp, 0, 0)
        return P

    def head_top_z(self, P):
        D, t = self.rig.solve(P)
        pos = self.rig.positions(D, t)
        return self.rig.point('Head', self.J['head_top'], D, pos).z + (self.S['hair_extra'])

    def _calibrate_crouch(self):
        """Pick the hip drop and torso lean so the crouched head sits at 60 % of the standing height (the server's
        crouch hitbox), with the thighs still reachable."""
        H = self.S['height']
        target = 0.6 * H
        drop = (self.leg * 0.60)
        best = None
        for lean in range(10, 80, 1):
            z = self.head_top_z(self.crouch_pose(lean, drop))
            if best is None or abs(z - target) < best[0]:
                best = (abs(z - target), lean)
        self.crouch_drop, self.crouch_lean = drop, float(best[1])
        self.crouch_err = best[0]

    # ---- gait -------------------------------------------------------------------------------------------
    def gait(self, phase, direction, crouch):
        """Locomotion pose. phase: radians (a full cycle = two steps). direction: (right, forward) unit vector.
        crouch: 0 standing, 1 crouched."""
        rt, fw = direction
        u = V((-rt, -fw, 0.0))                       # world direction of travel
        base = self.crouch_pose() if crouch else self.stand()
        P = dict(base)
        k = self.k
        stride = (0.52 if not crouch else 0.34) * (0.55 + 0.45 * self.k) * (1.0 if abs(fw) > abs(rt) else 0.75)
        lift = (0.17 if not crouch else 0.09) * k
        bob = (0.028 if not crouch else 0.014) * k
        run_drop = 0 if crouch else 0.05 * k
        hz = base['hips_t'][2] - run_drop + bob * math.cos(2 * phase + math.pi)
        P['hips_t'] = (0, 0, hz)
        spine, chest = base['spine_r'][0], base['chest_r'][0]
        lean_f = (5.0 if not crouch else 2.0) * fw
        lean_s = -5.0 * rt                                 # lean into the side step
        P['spine_r'] = (spine + lean_f * 0.6, 0, math.sin(phase) * 2.2)
        P['chest_r'] = (chest + lean_f * 0.4, lean_s * 0.0, math.sin(phase) * 2.0)
        P['hips_r'] = (base['hips_r'][0], lean_s * 0.7 + math.sin(phase) * 1.5, -math.sin(phase) * 3.0)
        P['head_r'] = (base['head_r'][0], -lean_s * 0.4, math.sin(phase) * 1.2)
        for s in 'LR':
            ph = phase + (0.0 if s == 'L' else math.pi)
            a = self.ankle(s)
            sx = 1 if s == 'L' else -1
            off = -math.cos(ph) * stride / 2
            lf = max(0.0, math.sin(ph)) ** 0.8 * lift
            x = a.x * (1.12 if not crouch else 1.45) + u.x * off
            # never cross the other foot when stepping sideways
            x = max(x, 0.04) if s == 'L' else min(x, -0.04)
            y = a.y + u.y * off - (0.03 * k if crouch else 0.0)
            P['foot' + s] = V((x, y, a.z + lf))
            P['foot_r' + s] = (-18 * lf / lift * (1 if fw >= 0 else -0.3) if lift else 0, 0, 0)
            P['pole' + s] = V((sx * (0.25 if crouch else 0.1), -1, 0.25 if crouch else 0.08))
        return P

    # ---- other states -----------------------------------------------------------------------------------
    def idle(self, t):
        return self.stand(breathe=math.sin(2 * math.pi * t) * 0.9, sway=math.sin(2 * math.pi * t) * 0.7)

    def crouch_idle(self, t):
        P = self.crouch_pose()
        P['chest_r'] = (P['chest_r'][0] + math.sin(2 * math.pi * t) * 1.2, 0, 0)
        return P

    def air(self, t):
        P = self.stand()
        k = self.k
        P['hips_t'] = (0, 0, -0.03 * k)
        P['spine_r'] = (6.0, 0, 0)
        for s, fwd in (('L', 1), ('R', -1)):
            a = self.ankle(s)
            if fwd > 0:   # leading leg: knee up and forward
                P['foot' + s] = V((a.x * 1.2, a.y - 0.18 * k, a.z + 0.2 * k))
            else:         # trailing leg: folded behind
                P['foot' + s] = V((a.x * 1.2, a.y + 0.2 * k, a.z + 0.3 * k))
            P['pole' + s] = V((0, -1, 0.0 if fwd > 0 else 0.3))
        P['hips_t'] = (0, 0, -0.03 * k + math.sin(2 * math.pi * t) * 0.01)
        return P

    def roll(self, f, n):
        """f in 0..n (frames): a forward somersault with the knees pulled up and the blaster held to the chest."""
        t = f / n
        spin = 360.0 * smooth((t - 0.06) / 0.88)
        tuck = smooth(t / 0.22) * (1.0 - 0.45 * smooth((t - 0.82) / 0.18))
        k = self.k
        base = self.crouch_pose()
        hips_c = self.J['hips'] + V(base['hips_t'])
        P = dict(hips_t=base['hips_t'], hips_r=(lerp(base['hips_r'][0], 18.0, tuck) + spin, 0, 0))
        P['spine_r'] = (lerp(base['spine_r'][0], 34, tuck), 0, 0)
        P['chest_r'] = (lerp(base['chest_r'][0], 30, tuck), 0, 0)
        P['neck_r'] = (lerp(base['neck_r'][0], 28, tuck), 0, 0)
        P['head_r'] = (lerp(base['head_r'][0], 12, tuck), 0, 0)
        for s in 'LR':
            sx = 1 if s == 'L' else -1
            normal = base['foot' + s] - hips_c
            tucked = V((sx * (0.11 + 0.02 * k), -0.30 * k, -0.16 * k))
            P['foot_local' + s] = normal.lerp(tucked, tuck)
            P['uarm_r' + s] = (lerp(base['uarm_r' + s][0], 38, tuck), 0, 0)
            P['larm_r' + s] = (-92 * tuck, 0, 0)
            P['pole' + s] = V((sx * 0.4, -1, 0.5))
        # Keep the lowest part of the body just on the floor while it turns (rolling on the back, not through it).
        D, tt = self.rig.solve(P)
        pos = self.rig.positions(D, tt)
        probes = [pos[n].z for n in ('Head', 'Chest', 'Spine', 'Hips', 'LowerLeg.L', 'LowerLeg.R', 'Foot.L', 'Foot.R',
                                     'Hand.L', 'Hand.R', 'LowerArm.L', 'LowerArm.R', 'Muzzle')]
        probes.append(self.rig.point('Head', self.J['head_top'], D, pos).z + self.S['hair_extra'] * .0)
        probes.append(self.rig.point('Hand.R', self.J['muzzle'], D, pos).z)
        low = min(probes)
        ball_z = P['hips_t'][2] + (0.07 - low)
        ramp = smooth(t / 0.18) * (1.0 - smooth((t - 0.88) / 0.12))
        P['hips_t'] = (0, 0, lerp(base['hips_t'][2], ball_z, ramp))
        return P

    def dead(self):
        P = self.crouch_pose(lean=18, drop=0.35 * self.leg)
        for s in 'LR':
            P['uarm_r' + s] = (62, 0, 0)
            P['larm_r' + s] = (-14, 0, 0)
        P['head_r'] = (14, 0, 0)
        return P


# --------------------------------------------------------------------------------------------------- authoring


def make_action(rig, name, frames, fn, loop=True):
    """frames: number of frames; the clip has frames+1 keys (last == first for loops) so it lasts frames/FPS."""
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.obj.animation_data_create()
    rig.obj.animation_data.action = act
    for f in range(frames + 1):
        rig.apply(fn(f, frames), f)
    act.frame_range = (0, frames)
    return act


def author_all(rig, clips):
    """Creates every clip the game uses. Returns {name: seconds}."""
    out = {}
    acts = {}
    run_frames, idle_frames = 24, 60

    def add(name, frames, fn):
        acts[name] = make_action(rig, name, frames, fn)
        out[name] = frames / FPS

    add('Idle', idle_frames, lambda f, n: clips.idle(f / n))
    add('CrouchIdle', idle_frames, lambda f, n: clips.crouch_idle(f / n))
    dirs = {'F': (0, 1), 'B': (0, -1), 'L': (-1, 0), 'R': (1, 0)}
    for tag, d in dirs.items():
        add('Run' + tag, run_frames, lambda f, n, d=d: clips.gait(2 * math.pi * f / n, d, 0))
        add('CrouchRun' + tag, run_frames, lambda f, n, d=d: clips.gait(2 * math.pi * f / n, d, 1))
    add('Air', 24, lambda f, n: clips.air(f / n))
    add('Roll', 15, lambda f, n: clips.roll(f, n))
    add('Dead', 2, lambda f, n: clips.dead())
    rig.obj.animation_data.action = acts['Idle']
    rig.actions = acts
    return out


def export_fbx(path, objects):
    for o in bpy.data.objects:
        o.select_set(False)
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
        use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, mesh_smooth_type='OFF', apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True, path_mode='AUTO', embed_textures=False)
