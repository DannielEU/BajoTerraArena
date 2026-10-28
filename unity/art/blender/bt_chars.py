"""The four heroes' bodies, rebuilt from close-ups of images/BajoTerra-Team.webp and the official character art. The
heads (face and hair) are in bt_heads, the shoulder slugs in bt_slugs, the blasters in bt_weapons. What each must read as:

Eli    slim and long-legged; black jacket with orange side panels, light-grey sleeves, two blue slug cards on the left
       chest; steel-blue trousers with darker thigh panels and knee pads; black knee-high boots with grey soles; Burpy.
Trixie olive t-shirt with cap sleeves over a black bodice; light-grey trousers, black knee pads and thigh strap; black
       knee-high boots with white toes and soles; fingerless gloves; a green slug on her shoulder.
Kord   huge blue troll, top-heavy (massive chest and arms, slimmer legs); black sleeveless vest with an orange star over
       a white shirt; bandolier of blue slug tubes; dark-brown trousers, short grey boots; a red-hooded slug.
Pronto small and round; wide blue/white striped shirt under an orange harness; long dark gloves with orange elbow pads;
       baggy green shorts, dark legs, black boots; three slug cans on the belt; a backpack frame; a blue slug. He holds
       his rifle one-handed at his side with the left fist on his hip, like his official pose (see place_gun).

Sizes follow the server's hitboxes (content/characters/*.json): Eli 1.8 m, Trixie 1.7 m, Kord 2.35 m, Pronto 1.15 m.
Everything is rigid-skinned (each part weighted 1.0 to one bone); see bt_lib / bt_rig for the machinery.
"""
import math

from mathutils import Matrix, Vector

import bt_heads
import bt_slugs
from bt_lib import Acc, box, cyl, hull, loft, material, sphere, star, transform
from bt_rig import ik2
from bt_weapons import BUILDERS

V = Vector


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


# --------------------------------------------------------------------------------------------------- specs

SPECS = {
    'eli': dict(
        height=1.80, hair_extra=0.055, hip_z=.95, hip_x=.085, knee_z=.52, knee_fwd=.02, ankle_z=.11, foot_len=.3,
        foot_w=.115, waist_z=1.11, chest_z=1.28, neck_z=1.46, head_c=1.622, head_r=(.088, .105, .122),
        neck_r=(.042, .045), shoulder_x=.2, shoulder_z=1.39, upper_arm=.29, fore_arm=.27, hand_len=.09, hand_r=.042,
        arm_r=(.05, .043, .038), thigh_r=(.085, .062), shin_r=(.06, .046),
        torso=[(.86, .125, .088), (.95, .15, .098), (1.03, .145, .095), (1.11, .13, .088), (1.2, .152, .098),
               (1.3, .178, .108), (1.385, .198, .112), (1.43, .15, .095), (1.48, .05, .048)],
        gun_scale=1.0, gun_x=-.07, gun_fwd=.30, gun_drop=.085,
    ),
    'trixie': dict(
        height=1.70, hair_extra=0.05, hip_z=.9, hip_x=.08, knee_z=.49, knee_fwd=.02, ankle_z=.1, foot_len=.23,
        foot_w=.088, waist_z=1.05, chest_z=1.21, neck_z=1.37, head_c=1.53, head_r=(.085, .1, .118),
        neck_r=(.036, .038), shoulder_x=.17, shoulder_z=1.31, upper_arm=.26, fore_arm=.245, hand_len=.08,
        hand_r=.036, arm_r=(.04, .034, .031), thigh_r=(.078, .055), shin_r=(.054, .04),
        torso=[(.82, .12, .085), (.9, .142, .094), (.97, .13, .088), (1.05, .102, .074), (1.13, .118, .082),
               (1.22, .142, .094), (1.31, .165, .096), (1.36, .12, .078), (1.4, .04, .038)],
        gun_scale=1.0, gun_x=-.06, gun_fwd=.28, gun_drop=.08,
    ),
    'kord': dict(
        height=2.35, hair_extra=0.13, hip_z=1.08, hip_x=.14, knee_z=.6, knee_fwd=.03, ankle_z=.12, foot_len=.36,
        foot_w=.14, waist_z=1.3, chest_z=1.58, neck_z=1.88, head_c=2.06, head_r=(.133, .155, .185), neck_r=(.115, .105),
        shoulder_x=.46, shoulder_z=1.82, upper_arm=.46, fore_arm=.44, hand_len=.19, hand_r=.105,
        arm_r=(.15, .13, .115), thigh_r=(.14, .105), shin_r=(.11, .085),
        torso=[(.98, .18, .14), (1.08, .215, .165), (1.2, .225, .17), (1.3, .25, .175), (1.45, .33, .21),
               (1.62, .41, .235), (1.8, .46, .245), (1.9, .28, .17), (1.97, .12, .11)],
        gun_scale=1.15, gun_x=-.12, gun_fwd=.34, gun_drop=.26,
    ),
    'pronto': dict(
        height=1.15, hair_extra=0.03, hip_z=.44, hip_x=.08, knee_z=.25, knee_fwd=.015, ankle_z=.07, foot_len=.2,
        foot_w=.09, waist_z=.56, chest_z=.7, neck_z=.81, head_c=.97, head_r=(.15, .15, .15), neck_r=(.06, .06),
        shoulder_x=.18, shoulder_z=.77, upper_arm=.19, fore_arm=.18, hand_len=.075, hand_r=.05,
        arm_r=(.055, .048, .045), thigh_r=(.085, .065), shin_r=(.055, .045),
        torso=[(.38, .13, .1), (.44, .165, .13), (.52, .2, .17), (.6, .215, .18), (.68, .2, .16), (.76, .185, .13),
               (.81, .1, .085), (.84, .06, .06)],
        # like his official pose: the rifle in his right hand at his side (its stock passes beside him, his arms are
        # far too short to hold it across the chest), the left fist on his hip
        gun_scale=.8, gun_x=-.27, gun_fwd=.12, gun_drop=.16, left_hand='hip', thigh_k=1.4,
    ),
}


def make_materials(name):
    M = material
    common = dict(Eye=('#ffffff', .3, 0), Pupil=('#141418', .2, 0), Rubber=('#1b1c21', .7, 0),
                  Metal=('#3b3f48', .45, .7), Steel=('#a8b0b9', .35, .8), Steel2=('#e9edf1', .4, .2),
                  Tooth=('#f6f3ea', .35, 0), Mouth=('#3a1a20', .6, 0))
    for n, (h, r, m) in common.items():
        M(n, h, r, m)
    M('Team', '#ff3b3b', .4, 0, emit=2.0)
    M('Glow', '#38e6ff', .2, 0, emit=3.0)
    M('GlowBlue', '#3da5ff', .2, 0, emit=2.2)
    M('Chamber', '#38e6ff', .2, 0, emit=3.0)
    palettes = {
        'eli': dict(Skin='#f0ac7f', SkinShade='#cf8a62', Lips='#d9957a', Hair='#161c38', Brow='#141729',
                    Lash='#121420', Iris='#4f9fdc', IrisDark='#1f5e9e', Outfit='#26282e', Outfit2='#d7dce2',
                    Trim='#ea6f1c', Pants='#5b7491', Pants2='#44566c', Pad='#393d45', Boots='#1b1c20', Sole='#80868f'),
        'trixie': dict(Skin='#f9c9ae', SkinShade='#e0a48a', Lips='#d8557a', Lid='#c98a9c', Hair='#c0442c',
                       Brow='#7a2617', Lash='#141418', Iris='#3db05a', IrisDark='#1c6a33', Outfit='#56692f',
                       Outfit2='#1d1e23', Trim='#ff8a1e', Pants='#a9aeb6', Pad='#1d1e23', Boots='#1b1c20',
                       Sole='#f2f2f2', Pouch='#e9ecef'),
        'kord': dict(Skin='#74a9dc', Skin2='#4f7fc0', Hair='#18191e', Brow='#1d2233', Lash='#141826', Horn='#aea397',
                     Horn2='#d8d0c4', Goatee='#b8682c', Iris='#c98a2a', IrisDark='#6a3e12', Outfit='#24262c',
                     Outfit2='#dfe2e8', Trim='#ea6a26', Pants='#3a332e', Boots='#3a3d44', Sole='#8a9098'),
        'pronto': dict(Skin='#d4a3bd', SkinShade='#a87892', Nose='#7b5868', Spot='#8a5f7c', Brow='#16141a',
                       Lash='#2a2030', Cap='#cdb27a', CapDark='#9c8458', Outfit='#eef2f6', Outfit2='#3b77c2',
                       Trim='#c8622a', Pants='#4f7a2a', Glove='#34363c', Boots='#1c1c20', Sole='#6d7179',
                       Iris='#141418', Hair='#2a2230', SlugCan='#4aa8e8'),
    }
    for n, h in palettes[name].items():
        M(n, h, .55 if n in ('Skin', 'Skin2', 'SkinShade', 'Nose') else .5)
    if name == 'pronto':
        M('Lens', '#f2c43a', .2, 0, emit=1.2)


# --------------------------------------------------------------------------------------------------- joints


def make_joints(S):
    J = {}
    J['hips'] = V((0, 0, S['hip_z']))
    J['waist'] = V((0, 0, S['waist_z']))
    J['chest'] = V((0, 0, S['chest_z']))
    J['neck'] = V((0, 0, S['neck_z']))
    hr = S['head_r']
    J['head'] = V((0, 0, S['head_c'] - hr[2] * .85))
    J['head_top'] = V((0, 0, S['head_c'] + hr[2]))
    for s, sx in (('L', 1), ('R', -1)):
        J['hip' + s] = V((sx * S['hip_x'], 0, S['hip_z'] - .02))
        J['knee' + s] = V((sx * S['hip_x'] * 1.05, -S['knee_fwd'], S['knee_z']))
        J['ankle' + s] = V((sx * S['hip_x'] * 1.1, 0, S['ankle_z']))
        J['toe' + s] = V((sx * S['hip_x'] * 1.1, -S['foot_len'], S['ankle_z']))
        J['shoulder' + s] = V((sx * S['shoulder_x'], 0, S['shoulder_z']))
    return J


def gun_points(name, step=3):
    """The blaster's vertices in gun space (every `step`-th one) and the rig info its builder returns."""
    pts = []

    def grab(part, smooth=True, sharp=None):
        pts.extend(v.co.copy() for v in part[0].verts)
        part[0].free()
    info = BUILDERS[name](grab)
    return info, pts[::step]


def _seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length


def body_hits(S, J, pts, margin=.015, stop=None):
    """How many of the (character-space) points `pts` fall inside the body: the torso and its clothes, the head with
    its hair, the legs and the arms (except right at the hands, which hold the gun)."""
    t0, t1 = S['torso'][0][0], S['torso'][-1][0]
    hc, hr = V((0, 0, S['head_c'])), S['head_r']
    tr, ar = S['thigh_r'][0] * S.get('thigh_k', 1.0), S['arm_r']
    caps = []
    for s in 'LR':
        caps.append((J['hip' + s], J['knee' + s], tr))
        caps.append((J['shoulder' + s], J['elbow' + s], ar[0]))
        el, wr = J['elbow' + s], J['wrist' + s]
        cut = wr + (el - wr).normalized() * (S['hand_len'] + S['hand_r'])   # the forearm stops short of the hand
        if (cut - el).dot(wr - el) > 0:
            caps.append((el, cut, ar[2]))
    lo = min(min(a.z, b.z) - r for a, b, r in caps)
    n = 0
    for p in pts:
        if t0 <= p.z <= t1:
            rx, ry = interp(S['torso'], p.z)
            if (p.x / (rx + margin)) ** 2 + (p.y / (ry + margin)) ** 2 < 1:
                n += 1
                if stop and n >= stop:
                    return n
                continue
        d = p - hc
        if (d.x / (hr[0] * 1.2 + margin)) ** 2 + (d.y / (hr[1] * 1.2 + margin)) ** 2 + (d.z / (hr[2] * 1.2 + margin)) ** 2 < 1:
            n += 1
            if stop and n >= stop:
                return n
            continue
        if p.z < lo:
            continue
        for a, b, r in caps:
            if _seg_dist(p, a, b) < r + margin:
                n += 1
                break
        if stop and n >= stop:
            return n
    return n


def place_gun(S, J, info, pts=None):
    """Position the blaster in front of the chest and solve both arms onto it, without the gun going through the
    body: try positions around the hero's nominal one (and, for the left hand, the gun's other holds), nearest first,
    and keep the first one where both hands reach and no vertex of the gun is inside the torso, head, legs or arms."""
    sc = S['gun_scale']
    L1, L2 = S['upper_arm'], S['fore_arm']
    reach = (L1 + L2) * .93
    hl = S['hand_len'] * .5
    base = V((S['gun_x'], -S['gun_fwd'], S['shoulder_z'] - S['gun_drop']))
    holds = [info['grip_l']] + list(info.get('supports', []))
    poleR, poleL = V((-.55, .5, -1)), V((.55, .5, -1))
    hip = None
    if S.get('left_hand') == 'hip':   # one-handed: the left fist rests on the hip, elbow out
        hip = torso_point(S, S['waist_z'] - .03, -90, .035) + V((0, -.01, 0))
        holds = [None]
        poleL = V((1, .5, -.2))
    cands = []
    for k, hold in enumerate(holds):
        for ix in range(-10, 5):
            for iy in range(-8, 9):
                for iz in range(-5, 6):
                    d = V((ix * .02, iy * .02, iz * .02))
                    cost = k * .3 + abs(d.x) * 1.5 * (1 if ix < 0 else 2) + abs(d.y) * (1 if iy < 0 else 1.6) + abs(d.z) * 2
                    cands.append((cost, k, d))
    cands.sort(key=lambda c: c[0])
    best = None
    tested = 0
    for cost, k, d in cands:
        pos = base + d
        wr = pos + info['grip_r'] * sc + V((0, hl, 0))
        wl = hip if hip is not None else pos + holds[k] * sc + V((0, hl, 0))
        if (wr - J['shoulderR']).length > reach or (wl - J['shoulderL']).length > reach:
            continue
        if pts is None:
            best = (pos, wr, wl, 0)
            break
        JJ = dict(J)
        JJ['wristR'], JJ['wristL'] = wr, wl
        JJ['elbowR'] = ik2(J['shoulderR'], wr, L1, L2, poleR)
        JJ['elbowL'] = ik2(J['shoulderL'], wl, L1, L2, poleL)
        tested += 1
        hits = body_hits(S, JJ, [pos + p * sc for p in pts], stop=1 if best is None else best[3])
        if best is None or hits < best[3]:
            best = (pos, wr, wl, hits)
        if hits == 0:
            break
        if tested > 4000:
            break
    pos, wr, wl, hits = best
    J['gun_pos'] = pos
    J['gun_clip'] = hits
    J['wristR'], J['wristL'] = wr, wl
    J['elbowR'] = ik2(J['shoulderR'], wr, L1, L2, poleR)
    J['elbowL'] = ik2(J['shoulderL'], wl, L1, L2, poleL)
    J['hand_len'] = S['hand_len']
    J['chamber'] = pos + info['chamber'] * sc
    J['muzzle'] = pos + info['muzzle'] * sc
    return Matrix.Translation(pos) @ Matrix.Scale(sc, 4)


# --------------------------------------------------------------------------------------------------- torso


def interp(table, z):
    if z <= table[0][0]:
        return table[0][1], table[0][2]
    for (z0, a0, b0), (z1, a1, b1) in zip(table[:-1], table[1:]):
        if z <= z1:
            t = (z - z0) / (z1 - z0)
            return a0 + (a1 - a0) * t, b0 + (b1 - b0) * t
    return table[-1][1], table[-1][2]


def torso_bones(S):
    return [('Hips', -9.0, S['waist_z']), ('Spine', S['waist_z'], S['chest_z']), ('Chest', S['chest_z'], 9.0)]


def torso_layer(acc, S, z0, z1, matfn, grow=0.0, keep=None, caps=('flat', 'flat'), seg=28):
    """A layer around the torso between heights z0 and z1, split at the bone boundaries. matfn(z, angle) and
    keep(z, angle) work in heights and degrees from the front (positive towards the right). grow puts it over the
    body (clothes)."""
    for bone, b0, b1 in torso_bones(S):
        a, b = max(z0, b0), min(z1, b1)
        if b - a < 1e-3:
            continue
        lo, hi = a - (.012 if a > z0 else 0), b + (.012 if b < z1 else 0)
        n = max(2, int((hi - lo) / .025))
        prof = []
        for i in range(n + 1):
            t = i / n
            rx, ry = interp(S['torso'], lo + (hi - lo) * t)
            prof.append((t, rx + grow, ry + grow))
        inner = 'flat' if keep is None else False
        c0 = caps[0] if a <= z0 + 1e-6 else inner
        c1 = caps[1] if b >= z1 - 1e-6 else inner
        mf = (lambda t, ang, lo=lo, hi=hi: matfn(lo + (hi - lo) * t, ang))
        kf = None if keep is None else (lambda t, ang, lo=lo, hi=hi: keep(lo + (hi - lo) * t, ang))
        acc.add(loft((0, 0, lo), (0, 0, hi), prof, seg=seg, front=(0, -1, 0), cap0=c0, cap1=c1, matfn=mf, keep=kf), bone)


def torso_point(S, z, ang, out=0.0):
    """A point on the torso surface at height z and angle (degrees from the front, positive towards the right)."""
    rx, ry = interp(S['torso'], z)
    a = math.radians(ang)
    return V((-(rx + out) * math.sin(a), -(ry + out) * math.cos(a), z))


def strap(acc, p0, p1, width, thick, mat, bone):
    acc.add(loft(p0, p1, [(0, width / 2, thick / 2), (1, width / 2, thick / 2)], seg=8, cap0='flat', cap1='flat',
                 mats=[(0, 1, mat)], front=(0, -1, 0)), bone)


def band(acc, bone, p0, p1, r, mat):
    """A ring around a limb between two points (cuffs, arm bands, straps)."""
    acc.add(loft(p0, p1, [(0, r, r), (1, r, r)], seg=18, cap0='flat', cap1='flat', mats=[(0, 1, mat)]), bone)


# --------------------------------------------------------------------------------------------------- limbs


def limb(acc, bone, p0, p1, radii, mats=None, caps=(True, True), seg=16, front=None, matfn=None):
    prof = [(t, r, r) for t, r in radii]
    acc.add(loft(p0, p1, prof, seg=seg, front=front, cap0=caps[0], cap1=caps[1], mats=mats, matfn=matfn), bone)


def build_arms(acc, S, J, upper_mats, fore_mats, glove, fingers, shoulder_mat=None, pad=(1.1, 1.1, 1.1)):
    ar = S['arm_r']
    for s in 'LR':
        sh, el, wr = J['shoulder' + s], J['elbow' + s], J['wrist' + s]
        limb(acc, 'UpperArm.' + s, sh, el, [(0, ar[0]), (.45, ar[1] * 1.04), (1, ar[2] * 1.05)], upper_mats, seg=18)
        acc.add(sphere(sh, (ar[0] * pad[0], ar[0] * pad[1], ar[0] * pad[2]), shoulder_mat or upper_mats[0][2], 18, 12),
                'UpperArm.' + s)
        limb(acc, 'LowerArm.' + s, el, wr, [(0, ar[2] * 1.05), (.35, ar[2] * 1.02), (1, ar[2] * .8)], fore_mats, seg=16)
        hand(acc, S, J, s, glove, fingers)


def hand(acc, S, J, s, glove, fingers):
    """A gripping hand: palm, a row of curled fingers and a thumb (fingerless gloves: palm and fingers differ)."""
    wr, el = J['wrist' + s], J['elbow' + s]
    d = (wr - el).normalized()
    hr, hl = S['hand_r'], S['hand_len']
    bone = 'Hand.' + s
    side = d.cross(V((0, 0, 1)))
    if side.length < 1e-3:
        side = V((1, 0, 0))
    side.normalize()
    down = side.cross(d).normalized()
    if down.z > 0:
        down = -down
    acc.add(loft(wr - d * .01, wr + d * hl * .62, [(0, hr * .78, hr * .62), (.6, hr * 1.02, hr * .7), (1, hr * 1.0, hr * .66)],
                 seg=14, front=down, cap0=True, cap1='flat', mats=[(0, 1, glove)]), bone)
    knuckle = wr + d * hl * .7 + down * hr * .25
    acc.add(loft(knuckle - side * hr * .95, knuckle + side * hr * .95, [(0, hr * .42, hr * .48), (1, hr * .42, hr * .48)],
                 seg=12, front=down, mats=[(0, 1, fingers)]), bone)
    thumb0 = wr + d * hl * .25 + side * (hr * .8 if s == 'R' else -hr * .8) - down * hr * .2
    acc.add(loft(thumb0, thumb0 + d * hl * .45 + down * hr * .2, [(0, hr * .32, hr * .3), (1, hr * .27, hr * .25)],
                 seg=10, mats=[(0, 1, fingers)]), bone)


def build_legs(acc, S, J, thigh, shin, boot):
    """thigh/shin: matfn(t, angle) for the trousers. boot: dict(top = fraction of the shin it covers, mat, sole,
    flare, toe = toe-cap material or None, cuff)."""
    tr, sr = S['thigh_r'], S['shin_r']
    for s in 'LR':
        hip, knee, ankle = J['hip' + s], J['knee' + s], J['ankle' + s]
        limb(acc, 'UpperLeg.' + s, hip, knee, [(0, tr[0]), (.45, tr[0] * .94), (1, tr[1])], matfn=thigh, seg=20)
        limb(acc, 'LowerLeg.' + s, knee, ankle, [(0, sr[0]), (.35, sr[0] * .96), (1, sr[1])], matfn=shin, seg=18)
        down = (ankle - knee).normalized()
        top = knee + (ankle - knee) * (1 - boot['top'])
        fl = boot['flare']
        r_top = (sr[0] * .95 * fl + .006) if boot['top'] > .6 else (sr[1] * 1.05 * fl + .01)
        r_low = sr[1] * fl + .01
        # tall boots flare out at the top and pinch in above the ankle
        prof = [(0, r_top * 1.08), (.45, (r_top + r_low) * .5), (.85, r_low * .96), (1, r_low * 1.02)] \
            if boot['top'] > .6 else [(0, r_top), (.6, r_low + .002), (1, r_low + .004)]
        limb(acc, 'LowerLeg.' + s, top, ankle, prof, mats=[(0, 1, boot['mat'])], caps=(False, 'flat'), seg=18)
        if boot.get('cuff'):
            band(acc, 'LowerLeg.' + s, top - down * .012, top + down * .03, r_top * 1.08 + .008, boot['mat'])
        w, fl_len, az = S['foot_w'], S['foot_len'], S['ankle_z']
        x, y0 = ankle.x, ankle.y
        sole_h = az * .32
        body = [(x - w * .5, y0 + .07, sole_h), (x + w * .5, y0 + .07, sole_h),
                (x - w * .48, y0 + .07, az + .04), (x + w * .48, y0 + .07, az + .04),
                (x - w * .58, y0 - fl_len * .5, sole_h), (x + w * .58, y0 - fl_len * .5, sole_h),
                (x - w * .54, y0 - fl_len * .45, az * .95), (x + w * .54, y0 - fl_len * .45, az * .95),
                (x - w * .46, y0 - fl_len * .95, sole_h), (x + w * .46, y0 - fl_len * .95, sole_h),
                (x - w * .4, y0 - fl_len * .9, az * .6), (x + w * .4, y0 - fl_len * .9, az * .6)]
        acc.add(hull(body, boot['mat'], bevel=.014, bevel_segments=3), 'Foot.' + s, smooth=True, sharp_angle=45)
        if boot.get('toe'):
            acc.add(hull([(x - w * .55, y0 - fl_len * .62, sole_h), (x + w * .55, y0 - fl_len * .62, sole_h),
                          (x - w * .5, y0 - fl_len * .6, az * .85), (x + w * .5, y0 - fl_len * .6, az * .85),
                          (x - w * .47, y0 - fl_len * .98, sole_h), (x + w * .47, y0 - fl_len * .98, sole_h),
                          (x - w * .41, y0 - fl_len * .93, az * .64), (x + w * .41, y0 - fl_len * .93, az * .64)],
                         boot['toe'], bevel=.012, bevel_segments=3), 'Foot.' + s, smooth=True, sharp_angle=45)
        acc.add(hull([(x - w * .56, y0 + .085, 0), (x + w * .56, y0 + .085, 0), (x - w * .62, y0 - fl_len, 0),
                      (x + w * .62, y0 - fl_len, 0), (x - w * .56, y0 + .085, sole_h), (x + w * .56, y0 + .085, sole_h),
                      (x - w * .62, y0 - fl_len, sole_h), (x + w * .62, y0 - fl_len, sole_h)],
                     boot['sole'], bevel=.008, bevel_segments=2), 'Foot.' + s, smooth=True, sharp_angle=45)


def knee_pad(acc, S, J, s, mat, size=1.0):
    r = S['shin_r'][0]
    c = J['knee' + s] + V((0, -r * .95, 0))
    acc.add(sphere(c, (r * .85 * size, r * .45 * size, r * 1.05 * size), mat, 16, 10), 'LowerLeg.' + s)


def team_bands(acc, S, J, at=.5):
    """A thin team-coloured band on each upper arm (tells the teams apart)."""
    r = S['arm_r'][1] * 1.06 + .006
    for s in 'LR':
        el, sh = J['elbow' + s], J['shoulder' + s]
        band(acc, 'UpperArm.' + s, sh + (el - sh) * at, sh + (el - sh) * (at + .08), r, 'Team')


# --------------------------------------------------------------------------------------------------- Eli


def build_eli(acc, S, J):
    def jacket(z, ang):
        a = abs(ang)
        if z < 1.0:
            return 'Pants'
        if z < 1.06:
            return 'Rubber'
        if 1.09 < z < 1.37 and 62 < a < 118:
            return 'Trim'           # orange side panels from the armpit to the waist
        return 'Outfit'
    torso_layer(acc, S, S['torso'][0][0], S['torso'][-1][0], jacket)
    torso_layer(acc, S, 1.06, 1.1, lambda z, a: 'Outfit', grow=.008)
    # open collar with an orange lining
    acc.add(loft(V((0, 0, S['neck_z'] - .03)), V((0, 0, S['neck_z'] + .05)), [(0, .075, .07), (1, .06, .058)], seg=24,
                 front=(0, -1, 0), cap0=False, cap1=False, matfn=lambda t, a: 'Trim' if t > .8 else 'Outfit',
                 keep=lambda t, a: abs(a) > 28), 'Chest')
    bt_heads.eli(acc, S)
    # sleeves: black shoulder, light-grey upper arm, black cuff; bare forearm; black fingerless gloves
    build_arms(acc, S, J, [(0, .18, 'Outfit'), (.18, .8, 'Outfit2'), (.8, 1, 'Outfit')],
               [(0, .12, 'Outfit'), (.12, .78, 'Skin'), (.78, 1, 'Outfit')], 'Outfit', 'Skin')
    team_bands(acc, S, J, .55)
    # steel-blue trousers with darker thigh panels and knee pads; tall black boots with grey soles; thigh holster
    build_legs(acc, S, J, lambda t, a: 'Pants2' if .12 < t < .82 and abs(a) < 52 else 'Pants', lambda t, a: 'Pants',
               dict(top=.9, mat='Boots', sole='Sole', flare=1.35, cuff=True))
    for s in 'LR':
        knee_pad(acc, S, J, s, 'Pad', 1.1)
    hp = J['hipR'].lerp(J['kneeR'], .35) + V((-.07, -.01, 0))
    acc.add(box(hp, (.04, .1, .13), 'Outfit', .01), 'UpperLeg.R', smooth=True, sharp_angle=40)
    acc.add(cyl(hp + V((-.022, -.02, .0)), .014, .09, 'Z', 'GlowBlue', seg=10), 'UpperLeg.R')
    # chest: a strap from the left shoulder holding two blue slug cards; belt buckle
    strap(acc, torso_point(S, 1.43, -40, .012), torso_point(S, 1.12, 35, .012), .045, .012, 'Rubber', 'Chest')
    for z, ang in ((1.33, -42), (1.27, -30)):
        p = torso_point(S, z, ang, .024)
        rot = Matrix.Rotation(math.radians(-ang * .8), 4, 'Z') @ Matrix.Rotation(math.radians(-18), 4, 'Y')
        acc.add(box(p, (.06, .012, .085), 'Steel2', .004, rot=rot), 'Chest', smooth=True, sharp_angle=40)
        acc.add(box(p + V((0, -.006, 0)), (.048, .006, .07), 'GlowBlue', .002, rot=rot), 'Chest', smooth=False)
    acc.add(box(torso_point(S, 1.035, 0, .014), (.06, .012, .05), 'Steel', .004), 'Hips', smooth=True, sharp_angle=40)
    bt_slugs.place(acc, 'burpy', (.165, .02, 1.445), 'Chest', .13, yaw=-8)


# --------------------------------------------------------------------------------------------------- Trixie


def build_trixie(acc, S, J):
    def top(z, ang):
        if z < .96:
            return 'Pants'
        if z < 1.01:
            return 'Rubber'
        if z < 1.2:
            return 'Outfit2'            # black bodice
        return 'Outfit'                 # olive t-shirt
    torso_layer(acc, S, S['torso'][0][0], S['torso'][-1][0], top)
    torso_layer(acc, S, 1.01, 1.2, lambda z, a: 'Outfit2', grow=.006)
    for i in range(4):   # bodice lacing
        z = 1.04 + i * .04
        acc.add(box(torso_point(S, z, 0, .012), (.03, .004, .006), 'Steel2', .001), 'Spine' if z < S['chest_z'] else 'Chest')
    acc.add(star(torso_point(S, .985, -12, .012), .022, .009, .005, 'Trim', plane='Y'), 'Hips', smooth=False)
    bt_heads.trixie(acc, S)
    # cap sleeves, bare arms, black wrist bands and fingerless gloves
    build_arms(acc, S, J, [(0, .3, 'Outfit'), (.3, 1, 'Skin')], [(0, .7, 'Skin'), (.7, 1, 'Outfit2')], 'Outfit2', 'Skin')
    team_bands(acc, S, J, .42)
    build_legs(acc, S, J, lambda t, a: 'Pants', lambda t, a: 'Pants',
               dict(top=.88, mat='Boots', sole='Sole', toe='Sole', flare=1.18, cuff=True))
    for s in 'LR':
        knee_pad(acc, S, J, s, 'Pad', 1.05)
    hip, knee = J['hipL'], J['kneeL']
    band(acc, 'UpperLeg.L', hip.lerp(knee, .32), hip.lerp(knee, .4), S['thigh_r'][0] * .95 + .006, 'Outfit2')
    acc.add(box(torso_point(S, .95, 75, .02), (.05, .06, .07), 'Pouch', .012), 'Hips', smooth=True, sharp_angle=40)
    bt_slugs.place(acc, 'mint', (.15, .02, 1.355), 'Chest', .115, yaw=-8)


# --------------------------------------------------------------------------------------------------- Kord


def build_kord(acc, S, J):
    def body(z, ang):
        if z < 1.17:
            return 'Pants'
        if z < 1.29:
            return 'Rubber'
        return 'Outfit2'                 # white shirt
    torso_layer(acc, S, S['torso'][0][0], S['torso'][-1][0], body)
    # black sleeveless vest, open at the front so the white shirt shows, with a high collar
    torso_layer(acc, S, 1.3, 1.95, lambda z, a: 'Outfit', grow=.02, keep=lambda z, a: abs(a) > 20, caps=(False, False))
    acc.add(loft(V((0, 0, S['neck_z'] - .06)), V((0, 0, S['neck_z'] + .03)), [(0, .2, .15), (1, .175, .135)], seg=26,
                 front=(0, -1, 0), cap0=False, cap1=False, mats=[(0, 1, 'Outfit')], keep=lambda t, a: abs(a) > 50), 'Chest')
    acc.add(star(torso_point(S, 1.7, -32, .03), .07, .03, .012, 'Trim', plane='Y', rot_deg=-15), 'Chest', smooth=False)
    a0, a1 = torso_point(S, 1.84, -38, .04), torso_point(S, 1.4, 40, .04)
    strap(acc, a0, a1, .07, .018, 'Rubber', 'Chest')
    for i in range(4):
        p = torso_point(S, 1.84, -38, .06).lerp(torso_point(S, 1.4, 40, .06), .2 + i * .17)
        acc.add(cyl(p, .022, .085, 'Z', 'GlowBlue', seg=10), 'Chest')
        acc.add(cyl(p + V((0, 0, .05)), .026, .016, 'Z', 'Steel', seg=10), 'Chest')
    acc.add(star(torso_point(S, 1.23, 0, .03), .045, .019, .012, 'Trim', plane='Y'), 'Spine', smooth=False)
    bt_heads.kord(acc, S)
    # bare blue arms, big fists, a black fingerless glove on the left hand
    build_arms(acc, S, J, [(0, 1, 'Skin')], [(0, 1, 'Skin')], 'Skin', 'Skin', pad=(1.0, .95, .9))
    hand(acc, S, J, 'L', 'Outfit', 'Skin')
    team_bands(acc, S, J, .5)
    build_legs(acc, S, J, lambda t, a: 'Pants', lambda t, a: 'Pants',
               dict(top=.38, mat='Boots', sole='Sole', flare=1.3, cuff=True))
    bt_slugs.place(acc, 'rammstone', (.34, .02, 1.95), 'Chest', .15, yaw=-10)


# --------------------------------------------------------------------------------------------------- Pronto


def build_pronto(acc, S, J):
    def shirt(z, ang):
        if z < .49:
            return 'Pants'
        if z < .54:
            return 'Rubber'
        return 'Outfit' if int((z - .54) / .05) % 2 == 0 else 'Outfit2'
    torso_layer(acc, S, S['torso'][0][0], S['torso'][-1][0], shirt)
    # orange harness: a yoke over the shoulders and upper chest, two straps down the front, logo plate
    torso_layer(acc, S, .7, .83, lambda z, a: 'Trim', grow=.014, keep=lambda z, a: z > .74 or abs(a) > 30,
                caps=(False, 'flat'))
    for sx in (-1, 1):
        strap(acc, torso_point(S, .74, sx * 32, .016), torso_point(S, .545, sx * 22, .016), .035, .01, 'Trim', 'Spine')
    acc.add(hull([torso_point(S, .8, sx * 35, .03) + V((0, 0, dz)) for sx in (-1, 1) for dz in (-.03, .02)]
                 + [torso_point(S, .8, 0, .04)], 'Steel', bevel=.004), 'Chest', smooth=True, sharp_angle=40)
    for ang in (-35, 0, 35):
        p = torso_point(S, .51, ang, .03)
        acc.add(cyl(p, .022, .06, 'Z', 'SlugCan', seg=12), 'Hips')
        for dz in (.033, -.033):
            acc.add(cyl(p + V((0, 0, dz)), .023, .008, 'Z', 'Steel', seg=12), 'Hips')
    # backpack frame
    for sx in (-1, 1):
        acc.add(box(V((sx * .12, .2, .64)), (.025, .025, .34), 'Steel', .006), 'Spine', smooth=True, sharp_angle=40)
    acc.add(box(V((0, .2, .76)), (.27, .025, .025), 'Steel', .006), 'Chest', smooth=True, sharp_angle=40)
    acc.add(box(V((0, .2, .5)), (.27, .025, .025), 'Steel', .006), 'Hips', smooth=True, sharp_angle=40)
    bt_heads.pronto(acc, S)
    # striped sleeves, orange shoulder and elbow pads, long dark gloves
    stripes = [(i * .16, (i + 1) * .16, 'Outfit' if i % 2 == 0 else 'Outfit2') for i in range(4)] + [(.64, 1, 'Glove')]
    build_arms(acc, S, J, stripes, [(0, 1, 'Glove')], 'Glove', 'Glove', shoulder_mat='Trim', pad=(1.25, 1.2, .8))
    for s in 'LR':
        acc.add(sphere(J['elbow' + s], (S['arm_r'][2] * 1.2, S['arm_r'][2] * 1.2, S['arm_r'][2] * 1.05), 'Trim', 14, 10),
                'LowerArm.' + s)
    team_bands(acc, S, J, .32)
    # baggy green shorts over dark legs, black boots
    build_legs(acc, S, J, lambda t, a: 'Pants', lambda t, a: 'Glove',
               dict(top=.45, mat='Boots', sole='Sole', flare=1.3, cuff=True))
    for s in 'LR':
        hip, knee = J['hip' + s], J['knee' + s]
        limb(acc, 'UpperLeg.' + s, hip + V((0, 0, .03)), knee + (knee - hip).normalized() * .03,
             [(0, S['thigh_r'][0] * 1.35), (.7, S['thigh_r'][0] * 1.4), (1, S['thigh_r'][0] * 1.3)],
             mats=[(0, 1, 'Pants')], caps=('flat', 'flat'), seg=20)
    bt_slugs.place(acc, 'blue', (.17, .03, .815), 'Chest', .11, yaw=-8)


BUILDERS_CHAR = dict(eli=build_eli, trixie=build_trixie, kord=build_kord, pronto=build_pronto)


def build_character(name, collection):
    """Builds mesh + joints for one hero in the current scene. Returns (body object, joints, spec)."""
    S = SPECS[name]
    make_materials(name)
    J = make_joints(S)
    # build the gun once without keeping it, to read where the hands go and to keep it out of the body
    info, pts = gun_points(name)
    M = place_gun(S, J, info, pts)
    acc = Acc()
    BUILDERS_CHAR[name](acc, S, J)
    BUILDERS[name](lambda part, smooth=True, sharp=None: acc.add(transform(part, M), 'Hand.R', smooth=smooth, sharp_angle=sharp))
    body = acc.to_object(name.capitalize() + 'Body', collection)
    return body, J, S
