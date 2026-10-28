"""The slugs that ride on the heroes' shoulders, drawn like the show's slugs: an upright soft body that is also the
head, a flared foot with a short tail, two little side flippers, big eyes ON THE FACE (not on stalks), a mouth under
them and two long antennae on top, plus each species' markings.

Eli    Burpy (Infurnus): orange, yellow flames over the head and back, a pale yellow belly, angry red-brown eyes.
Trixie a pale green slug with a darker green hood.
Kord   a tan slug with a red hood and a blue crest (a Rammstone), open mouth with two teeth.
Pronto a blue slug with a pale blue belly.

A slug is built standing at the origin with the Head machinery of bt_face (so the eyes and mouth sit on its surface),
then moved onto the shoulder and skinned to one bone.
"""
import math

from mathutils import Matrix, Vector

from bt_face import Bump, Head, lock
from bt_lib import Acc, loft, material

V = Vector

# body rows from the foot (u = -1) to the top of the head (u = 1): (u, width, depth, forward shift)

BODY = [(-1.0, .95, .9, -.1), (-.92, .8, .72, -.05), (-.78, .66, .62, .0), (-.4, .68, .64, .02), (.0, .78, .72, .03),
        (.35, .9, .8, .03), (.65, .9, .8, .01), (.85, .72, .66, -.01), (.96, .4, .38, -.02), (1.0, .08, .08, -.03)]

SPECIES = {
    'burpy': dict(Body='#f4621b', Mark='#fdb12c', Belly='#e4dc78', Iris='#7a2414', Mouth='#5a1a10', hood=None,
                  belly=True, flames=True, ant=('Body', 'Mark'), brow=-12, teeth=0),
    'mint': dict(Body='#a7e8a2', Mark='#3fa45f', Belly='#d8f5c8', Iris='#2a6a3a', Mouth='#2c4a2a', hood='Mark',
                 belly=True, flames=False, ant=('Mark', 'Mark'), brow=4, teeth=0),
    'rammstone': dict(Body='#e2c39b', Mark='#c9343c', Crest='#4f78c8', Belly='#f0dcbc', Iris='#5a3418',
                      Mouth='#4a1a18', hood='Mark', belly=False, flames=False, ant=None, brow=0, teeth=2),
    'blue': dict(Body='#3a9fe2', Mark='#1f6fc0', Belly='#a8dcf4', Iris='#1c3a6a', Mouth='#14305a', hood=None,
                 belly=True, flames=False, ant=('Body', 'Body'), brow=0, teeth=0),
}


def make_materials(kind):
    P = SPECIES[kind]
    for role in ('Body', 'Mark', 'Belly', 'Iris', 'Mouth', 'Crest'):
        if role in P:
            material('Slug' + role + '_' + kind, P[role], .45)


def build(kind, height=.12):
    """The slug standing on the origin, facing -Y. Returns (bmesh, material names) ready for Acc.add."""
    P = SPECIES[kind]
    make_materials(kind)

    def m(role):
        return 'Slug' + role + '_' + kind
    h = height
    S = dict(head_c=h / 2, head_r=(h * .38, h * .33, h / 2))
    acc = Acc()
    H = Head(S, BODY, [Bump(0, .38, .7, .3, h * .02), Bump(0, -.2, .5, .5, h * .015)], seg=96, mat=m('Body'), rings=80)

    def paint(x, y, z, nrm):
        u = (z - H.c) / H.rz
        front = -nrm.y
        X = x / H.rx
        if P['flames']:
            # a tongue of flame up the middle of the forehead, two more over the temples, licks down the back
            if front > .2 and u > .62 and abs(X) < .2 * (1 - (u - .62) / .5) + .04 * math.cos(u * 30):
                return m('Mark')
            if front > -.1 and u > .72 and .45 < abs(X) < .75 - (u - .72) * 1.2:
                return m('Mark')
            ang = math.atan2(x, y)
            if front < -.3 and u > .1 + .18 * math.cos(ang * 6):
                return m('Mark')
        if P['hood'] and u > .2 - .25 * max(0.0, -front) and not (front > .45 and u < .82):
            return m(P['hood'])
        if P['belly'] and front > .3:
            if P['flames']:   # a pale flame rising from the foot
                edge = -.12 - 1.5 * X * X + .1 * math.cos(X * 13)
            else:
                edge = -.12 - .2 * X * X
            if u < edge:
                return m('Belly')
        return None
    H.paint(paint)
    H.build(acc)
    rx = H.rx
    lid = P['brow']
    E = dict(inner=(rx * .03, .33), outer=(rx * .82, .45), up=.28, low=.22, peak_up=.5, peak_low=.5, iris=rx * .33,
             look=(.42, .0), iris_mat=m('Iris'), pupil=.52, dome=h * .03, lash=(h * .02, h * .026, 0, 0), lower=h * .008,
             lash_mat='Pupil', lash_thick=h * .01)
    if lid < 0:   # a frowning lid slanting down towards the middle (Burpy's look)
        E.update(lid_mat=m('Body'), lid_drop=(.5, .12), lid_out=h * .004)
    for sx in (1, -1):
        H.eye(acc, E, sx)
    # the mouth: a wide smile under the eyes (open with two teeth for the rammstone)
    if P['teeth']:
        from bt_face import open_mouth
        open_mouth(H, acc, -rx * .5, rx * .5, .1, -.12, m('Mouth'), curve=.0, smile=.04, teeth_up=(2, .07, h * .02))
    else:
        pts = [(rx * (-.48 + .96 * i / 12), .1 + .05 * ((-1 + 2 * i / 12) ** 2)) for i in range(13)]
        H.strip(acc, pts, [h * .012 * (.4 + .6 * math.sin(math.pi * i / 12)) for i in range(13)], m('Mouth'), out=h * .003,
                thick=h * .006)
    # little flippers on the sides
    for sx in (1, -1):
        p0 = H.side(sx, -.3, -h * .03)
        acc.add(loft(p0, p0 + V((sx * h * .2, -h * .02, -h * .1)), [(0, h * .07, h * .026), (.6, h * .05, h * .018),
                                                                     (1, h * .012, h * .008)],
                     seg=10, front=(0, 0, 1), mats=[(0, 1, m('Body'))]), 'Head')
    # the tail: the foot trails back into a point
    acc.add(loft(V((0, h * .1, h * .06)), V((0, h * .48, h * .02)), [(0, h * .25, h * .1), (.6, h * .14, h * .06),
                                                                      (1, h * .02, h * .02)],
                 seg=12, front=(0, 0, 1), cap0=True, mats=[(0, 1, m('Body'))]), 'Head')
    # antennae
    if P['ant']:
        base, tip = P['ant']
        for sx in (1, -1):
            r = V((sx * rx * .35, H.ry * .05, H.z(.9)))
            pts = [r, r + V((sx * h * .1, h * .06, h * .32)), r + V((sx * h * .16, h * .16, h * .62))]
            lock(H, acc, pts, h * .045, h * .04, m(base), n=10, gap=-.002, taper=1.0, seg=8)
            if tip != base:
                lock(H, acc, [pts[1].lerp(pts[2], .55), pts[2] + (pts[2] - pts[1]).normalized() * h * .02],
                     h * .03, h * .026, m(tip), n=6, gap=-1, taper=1.0, seg=8)
    if kind == 'rammstone':
        # the blue crest standing up from the hood, swept back
        r = V((0, H.ry * .1, H.z(.92)))
        lock(H, acc, [r, r + V((0, h * .1, h * .22)), r + V((0, h * .3, h * .32))], h * .09, h * .05, m('Crest'), n=10,
             gap=-.002, taper=1.0)
        # the hood flares out over the sides of the head
        for sx in (1, -1):
            r = H.side(sx, .55, 0)
            lock(H, acc, [r, r + V((sx * h * .08, h * .05, -h * .18)), r + V((sx * h * .1, h * .12, -h * .38))], h * .1,
                 h * .03, m('Mark'), n=10, gap=.001, taper=1.0)
    return acc.bm, acc.mats


def place(acc, kind, pos, bone, height=.12, yaw=0.0, lean=0.0):
    """Builds one slug and adds it to the character standing at `pos` on `bone`."""
    bm, names = build(kind, height)
    mtx = Matrix.Translation(V(pos)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z') @ Matrix.Rotation(math.radians(lean), 4, 'X')
    import bmesh
    bmesh.ops.transform(bm, matrix=mtx, verts=bm.verts)
    acc.add((bm, names), bone)
