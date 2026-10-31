"""The fifteen slugs of the game as standalone 3D models, the ones the blasters fire.

Each one follows its official protoform art (Disney's Slug Arsenal and the series): an upright soft body that is also
the head, with a flared foot and a short tail, two small side flippers, big eyes ON the face and each species' own
crown, markings and mouth. They are built with the same sculpted-head machinery as the heroes (bt_face), standing at
the origin facing -Y, `HEIGHT` metres tall, one mesh per slug, and exported to
unity/BajoTerraClient/Assets/Resources/Models/Slugs/<id>.fbx (no skeleton: the game moves them as a whole).

  Grenuke    tan body under a dark olive shell segmented like a grenade, two stubby feelers, heavy-lidded scowl
             and a clenched grin.
  Phosphoro  pale glowing teal with a dark blue hood down its back, three light dots on the forehead, big blue
             eyes looking up and a sweet smile (its belly glows: it lights up on its own).
  Infurnus   Burpy: orange with yellow flames over the head and back and a flame-shaped pale belly, angry eyes.
  Flaringo   red-orange with a white face and belly, flame-shaped feelers and flippers tipped yellow.
  Hop Rock   pale grey under a lumpy amber rock cap with red spots, sleepy lids and a wide grin.
  Tazerling  blue with a long light-blue bib, yellow lightning-bolt feelers and brows, yellow seeds on the belly
             and three buck teeth.
  Frostcrawler  sky blue fading paler towards the foot, ONE big violet eye under a heavy lid, two little
             horns of ice and a sly grin with a single fang (it raises ice walls).
  Frostfang  navy with a pale green mask and horns, one red eye and a mouthful of needle teeth.
  Arachnet   purple with a pale green belly, a cluster of little black eyes on the forehead, curled feelers and
             two buck teeth.
  Armashelt  sky blue under an orange armoured helmet with a yellow gem, an orange belly patch and a grin.
  AquaBeek   deep sea blue with a lighter belly, a broad flat head, eyes set wide apart and two white glints.
  Boon Doc   mint green with darker green sides, ONE big eye under a heavy brow and a smug smile.
  Doc        the albino Boon Doc: pearly white with pink sides and a pink eye.
  Rammstone  tan with a red hood flaring over its cheeks, a blue crest and an open mouth with two teeth.
  Thresher   teal with a mint belly, shark fins at the cheeks and on the back and a grin full of sharp teeth.
  Tormato    red-orange with dark freckles, a pale yellow bib, two striped yellow horns and two buck teeth.

Headless:   blender -b -P unity/art/blender/bt_build.py -- slugs          (all of them)
            blender -b -P unity/art/blender/bt_build.py -- slug:grenuke   (one)
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

from bt_face import Bump, Head, lock, open_mouth
from bt_lib import Acc, collection, loft, material, shell, sphere

V = Vector

HEIGHT = .5
IDS = ('aquabeek', 'arachnet', 'armashelt', 'boon_doc', 'doc', 'flaringo', 'frostcrawler', 'frostfang', 'grenuke',
       'hop_rock', 'infurnus', 'phosphoro', 'rammstone', 'tazerling', 'thresher', 'tormato')

# Body rows from the foot (u = -1) to the top (u = 1): (u, width, depth, forward shift). TALL is the classic slug,
# BLOCK a squarer one (Grenuke, Hop Rock, Armashelt) and BROAD widens towards a flat-topped head (AquaBeek, Thresher).
TALL = [(-1.0, .95, .9, -.1), (-.92, .8, .72, -.05), (-.78, .66, .62, .0), (-.4, .68, .64, .02), (.0, .78, .72, .03),
        (.35, .9, .8, .03), (.65, .9, .8, .01), (.85, .72, .66, -.01), (.96, .4, .38, -.02), (1.0, .08, .08, -.03)]
BLOCK = [(-1.0, 1.0, .92, -.08), (-.92, .9, .8, -.04), (-.75, .84, .74, .0), (-.3, .86, .76, .02), (.2, .9, .8, .03),
         (.6, .92, .82, .02), (.85, .86, .78, .0), (.96, .6, .56, -.02), (1.0, .1, .1, -.03)]
BROAD = [(-1.0, .95, .9, -.1), (-.92, .8, .72, -.05), (-.78, .66, .62, .0), (-.4, .7, .64, .02), (.0, .84, .72, .03),
         (.35, 1.0, .8, .03), (.62, 1.04, .8, .01), (.84, .96, .72, -.01), (.96, .62, .44, -.02), (1.0, .1, .1, -.03)]

# Colours and features of each slug (hex colours as in the official art).
SPECIES = {
    'grenuke': dict(shape=BLOCK, Body='#c8ad6e', Belly='#dcc68e', Iris='#7a2c18', Mouth='#4a1c14',
                    Shell='#58682a', Groove='#2f3a14', belly=.18, cap='grenade', ant=('Shell', .2, .1),
                    eyes=dict(lid=(.42, .42), look=(.55, -.02), size=.95), mouth=('teeth', 6, 6)),
    'phosphoro': dict(shape=TALL, Body='#63d4e4', Belly='#c4f7ff', Iris='#2a3fa8', Mouth='#14305a', Hood='#1d4fae',
                      Dot='#e8ffff', belly=.32, glow_belly=True, hood=True, dots=3, ant=('Hood', .62, .14),
                      eyes=dict(look=(.55, .1), size=1.08), mouth=('smile',)),
    'infurnus': dict(shape=TALL, Body='#f4621b', Mark='#fdb12c', Belly='#e4dc78', Iris='#7a2414', Mouth='#5a1a10',
                     belly=.3, flames=True, ant=('Body', .62, .14, 'Mark'), eyes=dict(lid=(.5, .12), look=(.45, 0)),
                     mouth=('smile',)),
    'flaringo': dict(shape=TALL, Body='#e8461e', Belly='#f6f0ea', Flame='#ffb21e', FlameTip='#ffe46a',
                     Iris='#1a1a3a', Mouth='#5a1a10', belly=.55, face_white=True, flame_ant=True, flame_fins=True,
                     eyes=dict(look=(.75, .05), size=1.0), mouth=('smile',)),
    'hop_rock': dict(shape=BLOCK, Body='#cdc9be', Belly='#dedad0', Rock='#e2a63a', Spot='#cf3434',
                     Iris='#5a3018', Mouth='#3a1410', belly=.1, cap='rock', eyes=dict(lid=(.38, .3), look=(.5, -.03)),
                     mouth=('grin', 5, 0)),
    'tazerling': dict(shape=TALL, Body='#2a7ee6', Belly='#8fd0f2', Bolt='#ffd21e', Iris='#7a4a1c', Mouth='#3a1418',
                      belly=.28, bib=True, seeds=True, bolt_ant=True, bolt_brows=True, eyes=dict(look=(.5, 0), size=1.05),
                      mouth=('teeth3',)),
    'frostcrawler': dict(shape=TALL, Body='#4fa9ea', Belly='#9ad6f7', Horn='#c8eeff', Iris='#7b2fd0',
                         Mouth='#14305a', belly=.15, cyclops=True, horns=('Horn', .4),
                         eyes=dict(lid=(.36, .36), look=(.64, .08), size=1.05), mouth=('fang',)),
    'frostfang': dict(shape=TALL, Body='#1d2a3e', Belly='#2a3a52', Mask='#a8dcb6', Iris='#e2141c', Mouth='#3a0c14',
                      belly=.0, mask=True, horns=('Mask', .55), cyclops=True, spines=True,
                      eyes=dict(white='SlugEyeBlack', look=(.5, 0), size=1.0), mouth=('fangs',)),
    'arachnet': dict(shape=TALL, Body='#7b2aa8', Belly='#cde7b2', Iris='#1a1a1a', Mouth='#3a1440', belly=.6,
                     wide_belly=True, spider_eyes=True, curl_ant=True, eyes=dict(look=(.6, .02), size=.95),
                     mouth=('buck',)),
    'armashelt': dict(shape=BLOCK, Body='#5ccff0', Belly='#f09a30', Helmet='#f2a01e', Gem='#f8e44a',
                      Iris='#1e4fa0', Mouth='#3a1410', belly=.0, belly_patch=True, cap='helmet',
                      ant=('Body', .2, .06), eyes=dict(lid=(.3, .1), look=(.5, -.02)), mouth=('grin', 7, 0),
                      fin_mat='Belly'),
    'aquabeek': dict(shape=BROAD, Body='#1c6f9c', Belly='#79aee8', Glint='#ffffff', Iris='#101418', Mouth='#0c2a40',
                     belly=.6, wide_belly=True, glints=True, ant=('Body', .3, .1), wide_eyes=True,
                     eyes=dict(look=(.5, .0), size=1.1, iris=.5), mouth=('flat',)),
    'boon_doc': dict(shape=TALL, Body='#a8eaa0', Belly='#c8f4bc', Side='#2f9a5e', Iris='#3a8a3a', Mouth='#24401c',
                     belly=.3, sides=True, cyclops=True, ant=('Side', .75, .03), eyes=dict(lid=(.3, .3), size=1.0),
                     mouth=('smirk',)),
    'doc': dict(shape=TALL, Body='#f6eef0', Belly='#fffafa', Side='#eba2c0', Iris='#d0406a', Mouth='#5a2030',
                belly=.3, sides=True, cyclops=True, ant=('Side', .75, .03), eyes=dict(lid=(.3, .3), size=1.0),
                mouth=('smile',)),
    'rammstone': dict(shape=TALL, Body='#e2c39b', Belly='#f0dcbc', Hood='#c9343c', Crest='#4f78c8', Iris='#5a3418',
                      Mouth='#4a1a18', belly=.0, ram_hood=True, eyes=dict(look=(.5, 0)), mouth=('ram',)),
    'thresher': dict(shape=BROAD, Body='#3aa9bb', Belly='#c8ecd2', Fin='#2c8a9c', FinEdge='#e8c84a',
                     Iris='#b0581c', Mouth='#3a0c10', belly=.55, wide_belly=True, cheek_fins=True, dorsal=True,
                     ant=('FinEdge', .45, .1), eyes=dict(look=(.55, .03), size=1.0), mouth=('shark',)),
    'tormato': dict(shape=TALL, Body='#d4502a', Belly='#f2d87a', Horn='#f0c63a', HornBand='#b88a1c',
                    Freckle='#a83a1c', Iris='#1d6a78', Mouth='#3a1410', belly=.22, bib=True, freckles=True,
                    horns=('Horn', .6), eyes=dict(look=(.5, .05), size=1.1), mouth=('buck',)),
}


def make_materials(kind):
    P = SPECIES[kind]
    for role, value in P.items():
        if isinstance(value, str) and value.startswith('#'):
            glow = role == 'Belly' and P.get('glow_belly')
            material(mname(kind, ('Glow' if glow else '') + role), value, .35, emit=2.0 if glow else 0.0)
    material('Eye', '#ffffff', .25)
    material('Pupil', '#0d0d12', .3)
    material('Tooth', '#f6f2e6', .3)
    material('SlugEyeBlack', '#141018', .25)


def mname(kind, role):
    """Material names are the contract with the game's importer: Slug_<id>_<Role>, Glow in the role = emissive."""
    if role in ('Eye', 'Pupil', 'Tooth', 'SlugEyeBlack'):
        return role
    return 'Slug_' + kind + '_' + role


def flat_part(points, thickness, mat):
    """A flat solid from a 2D outline [(x, z)] in the XZ plane, `thickness` deep along Y (flames, bolts, fins)."""
    bm = bmesh.new()
    verts = [bm.verts.new((x, 0.0, z)) for x, z in points]
    face = bm.faces.new(verts)
    res = bmesh.ops.extrude_face_region(bm, geom=[face])
    moved = [g for g in res['geom'] if isinstance(g, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(0, thickness, 0), verts=moved)
    bmesh.ops.translate(bm, vec=(0, -thickness / 2, 0), verts=list(bm.verts))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm, [mat]


def add_flat(acc, part):
    """Adds a flat cut-out part with crisp edges (smooth shading would round its sides off)."""
    acc.add(part, 'Head', smooth=True, sharp_angle=40)


def back(H, u):
    """The point of the slug's back (the +Y side) at height u."""
    z = H.z(u)
    hit = H.bvh.ray_cast(V((0.0, 3.0, z)), V((0, -1, 0)))
    return hit[0] if hit[0] is not None else V((0.0, H.ry * .8, z))


def placed(part, at, yaw=0.0, pitch=0.0, roll=0.0):
    """Moves a part built around the origin to `at`, turned yaw (about Z), pitch (about X) and roll (about Y)."""
    bm, names = part
    m = Matrix.Translation(V(at)) @ Matrix.Rotation(math.radians(yaw), 4, 'Z') @ \
        Matrix.Rotation(math.radians(pitch), 4, 'X') @ Matrix.Rotation(math.radians(roll), 4, 'Y')
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm, names


def flame_outline(w, h, tongues=3, lean=.0):
    """A stylised flame: a rounded base rising into `tongues` licks, the middle one tallest."""
    pts = []
    n = 40
    for i in range(n + 1):
        t = i / n
        x = -w / 2 + w * t
        base = -h * .08 * math.sin(math.pi * t)
        pts.append((x + lean * 0, base))
    top = []
    for i in range(n, -1, -1):
        t = i / n
        x = -w / 2 + w * t
        lick = abs(math.sin(math.pi * t * tongues))
        tall = h * (.35 + .65 * math.sin(math.pi * t)) * (.55 + .45 * lick)
        top.append((x + lean * tall / h * w * .3, tall))
    return pts[1:-1] + top


def bolt_outline(w, h):
    """A lightning bolt pointing up, about w wide and h tall, its foot at the origin."""
    raw = [(-.15, 0), (.25, 0), (.08, .38), (.45, .42), (-.05, 1.0), (.1, .55), (-.3, .5)]
    return [(x * w, z * h) for x, z in raw]


def build(kind, height=HEIGHT):
    """One slug standing on the origin, facing -Y. Returns the Acc with every part merged."""
    P = SPECIES[kind]
    make_materials(kind)

    def m(role):
        return mname(kind, role)
    h = height
    S = dict(head_c=h / 2, head_r=(h * .38, h * .33, h / 2))
    acc = Acc()
    bumps = [Bump(0, .38, .7, .3, h * .02), Bump(0, -.2, .5, .5, h * .015)]
    H = Head(S, P['shape'], bumps, seg=96, mat=m('Body'), rings=80)
    rx = H.rx

    belly_role = 'GlowBelly' if P.get('glow_belly') else 'Belly'

    # ---- markings painted on the body itself ------------------------------------------------------------------
    def paint(x, y, z, nrm):
        u = (z - H.c) / H.rz
        front = -nrm.y
        X = x / H.rx
        ang = math.degrees(math.atan2(x, -y))   # 0 = the face, +-180 = the back
        if P.get('flames'):
            if front > .2 and u > .62 and abs(X) < .2 * (1 - (u - .62) / .5) + .04 * math.cos(u * 30):
                return m('Mark')
            if front > -.1 and u > .72 and .45 < abs(X) < .75 - (u - .72) * 1.2:
                return m('Mark')
            if front < -.3 and u > .1 + .18 * math.cos(math.radians(ang) * 6):
                return m('Mark')
        if P.get('dots') and front > .4 and abs(u - .8) < .06:
            for k in range(P['dots']):
                cx = (k - (P['dots'] - 1) / 2) * .2
                if (X - cx) ** 2 + (u - .8) ** 2 < .055 ** 2:
                    return m('Dot')
        if P.get('hood') and (u > .68 - .1 * max(0.0, -front) or abs(ang) > 80 - 35 * max(0.0, u) and u > -.6):
            return m('Hood')
        if P.get('mask') and u > .5 - .15 * max(0.0, -front):
            return m('Mask')
        if P.get('sides') and (abs(ang) > 62 - 10 * u):
            return m('Side')
        if P.get('freckles') and front < .7 and u > -.2:
            for k in range(14):
                fa = (k * 137.5) % 360 - 180
                fu = -.1 + (k * .071) % 1.0
                if abs(fa) > 50 and abs(ang - fa) < 9 and abs(u - fu) < .045:
                    return m('Freckle')
        if P.get('glints') and front > .2 and .78 < u < .88 and .25 < abs(X) < .4:
            return m('Glint')
        if P.get('seeds') and front > .55 and -.85 < u < -.45:
            for k in range(5):
                cx = (k - 2) * .2
                cu = -.62 - .12 * ((k % 2) * 2 - 1)
                if ((X - cx) / .06) ** 2 + ((u - cu) / .07) ** 2 < 1:
                    return m('Bolt')
        if P.get('belly_patch') and front > .35 and u < -.45 - .5 * X * X:
            return m(belly_role)
        belly = P.get('belly', 0)
        if belly > 0 and front > (.05 if P.get('wide_belly') or P.get('face_white') else .3):
            if P.get('flames'):
                edge = -.12 - 1.5 * X * X + .1 * math.cos(X * 13)
            elif P.get('bib'):
                edge = belly - .9 * X * X * (2.2 if P.get('freckles') else 3.5)
            elif P.get('face_white'):
                edge = .45 - .5 * X * X
            else:
                edge = -.12 + belly - .25 * X * X
            if u < edge:
                return m(belly_role)
        return None
    H.paint(paint)
    H.build(acc)

    # ---- eyes ------------------------------------------------------------------------------------------------------
    EP = P.get('eyes', {})
    k = EP.get('size', 1.0)
    white = EP.get('white', 'Eye')
    if P.get('cyclops'):
        w = rx * .4 * k
        E = dict(inner=(-w, .42), outer=(w, .42), up=.3 * k, low=.26 * k, peak_up=.5, peak_low=.5,
                 iris=rx * .3 * k, look=EP.get('look', (.5, .0)), iris_mat=m('Iris'), pupil=.5, dome=h * .035,
                 lash=(h * .016, h * .016, 0, 0), lower=h * .006, lash_mat='Pupil', lash_thick=h * .008, white=white)
        lid = EP.get('lid')
        if lid:
            E.update(lid_mat=m('Side') if P.get('sides') else m('Body'), lid_drop=lid, lid_out=h * .004)
        H.eye(acc, E, 1)
    else:
        spread = 1.25 if P.get('wide_eyes') else 1.0
        inner = rx * (.06 if not P.get('wide_eyes') else .3)
        E = dict(inner=(inner, .34), outer=(min(rx * .9, rx * .78 * spread * k), .46), up=.27 * k, low=.21 * k,
                 peak_up=.5, peak_low=.5, iris=rx * .3 * k * EP.get('iris', .9) / .9, look=EP.get('look', (.45, .0)),
                 iris_mat=m('Iris'), pupil=.55, dome=h * .03, lash=(h * .018, h * .024, 0, 0), lower=h * .007,
                 lash_mat='Pupil', lash_thick=h * .009, white=white)
        lid = EP.get('lid')
        if lid:
            E.update(lid_mat=m('Body'), lid_drop=lid, lid_out=h * .004)
        for sx in (1, -1):
            H.eye(acc, E, sx)

    # ---- mouth ---------------------------------------------------------------------------------------------------
    mouth = P.get('mouth', ('smile',))
    kind_m = mouth[0]
    if kind_m in ('smile', 'smirk', 'flat'):
        smile = {'smile': .05, 'smirk': .03, 'flat': .0}[kind_m]
        n = 14
        pts, ws = [], []
        for i in range(n + 1):
            t = -1 + 2 * i / n
            lift = smile * t * t + (.04 * max(0.0, t) ** 2 if kind_m == 'smirk' else 0)
            pts.append((rx * .46 * t, .1 + lift))
            ws.append(h * .012 * (.4 + .6 * math.sin(math.pi * i / n)))
        H.strip(acc, pts, ws, m('Mouth'), out=h * .003, thick=h * .006)
    elif kind_m == 'teeth':      # clenched: a wide open strip filled with teeth top and bottom
        open_mouth(H, acc, -rx * .5, rx * .5, .14, .0, m('Mouth'), curve=.0, smile=.02,
                   teeth_up=(mouth[1], .07, h * .01), teeth_low=(mouth[2], .07, h * .01))
    elif kind_m == 'grin':
        open_mouth(H, acc, -rx * .55, rx * .55, .15, -.06, m('Mouth'), curve=-.02, smile=.06,
                   teeth_up=(mouth[1], .06, h * .012) if mouth[1] else None,
                   teeth_low=(mouth[2], .05, h * .012) if mouth[2] else None)
    elif kind_m == 'teeth3':
        open_mouth(H, acc, -rx * .42, rx * .42, .14, -.1, m('Mouth'), curve=-.01, smile=.05,
                   teeth_up=(3, .075, h * .03), teeth_low=None)
    elif kind_m == 'buck':
        open_mouth(H, acc, -rx * .4, rx * .4, .13, -.05, m('Mouth'), curve=-.02, smile=.05,
                   teeth_up=(2, .08, h * .02))
    elif kind_m == 'fangs':
        open_mouth(H, acc, -rx * .62, rx * .62, .2, -.18, m('Mouth'), curve=-.05, smile=.08,
                   teeth_up=(9, .1, h * .006), teeth_low=(8, .09, h * .006), pointed=True)
    elif kind_m == 'shark':
        open_mouth(H, acc, -rx * .66, rx * .66, .18, -.12, m('Mouth'), curve=-.03, smile=.1,
                   teeth_up=(8, .1, h * .004), teeth_low=(7, .08, h * .004), pointed=True)
    elif kind_m == 'fang':
        # a sly closed grin with a single fang poking down from its right side
        n = 14
        pts, ws = [], []
        for i in range(n + 1):
            t = -1 + 2 * i / n
            pts.append((rx * .44 * t, .12 + .05 * t * t + .03 * max(0.0, t)))
            ws.append(h * .012 * (.4 + .6 * math.sin(math.pi * i / n)))
        H.strip(acc, pts, ws, m('Mouth'), out=h * .003, thick=h * .006)
        from bt_face import hull_on_face
        x = rx * .2
        hull_on_face(H, acc, [(x - h * .018, .13, h * .004), (x + h * .018, .135, h * .004), (x, .06, h * .006),
                              (x, .12, h * .012)], 'Tooth', bevel=h * .002)
    elif kind_m == 'ram':
        open_mouth(H, acc, -rx * .5, rx * .5, .1, -.12, m('Mouth'), curve=.0, smile=.04, teeth_up=(2, .07, h * .02))

    # ---- body parts: flippers, tail ------------------------------------------------------------------------------
    fin = m(P.get('fin_mat', 'Body'))
    if P.get('flame_fins'):
        for sx in (1, -1):
            p0 = H.side(sx, -.25, -h * .02)
            part = flat_part(flame_outline(h * .2, h * .2, tongues=2), h * .02, m('Flame'))
            add_flat(acc, placed(part, p0, yaw=90 * sx, roll=-70 * sx))
    else:
        for sx in (1, -1):
            p0 = H.side(sx, -.3, -h * .03)
            acc.add(loft(p0, p0 + V((sx * h * .13, -h * .03, -h * .13)),
                         [(0, h * .07, h * .03), (.6, h * .05, h * .022), (1, h * .016, h * .01)],
                         seg=10, front=(0, 0, 1), mats=[(0, 1, fin)]), 'Head')
    tail_mat = m('Flame') if P.get('flame_fins') else m('Body')
    acc.add(loft(V((0, h * .1, h * .06)), V((0, h * .48, h * .02)),
                 [(0, h * .25, h * .1), (.6, h * .14, h * .06), (1, h * .02, h * .02)],
                 seg=12, front=(0, 0, 1), cap0=True, mats=[(0, 1, tail_mat)]), 'Head')

    # ---- crowns ----------------------------------------------------------------------------------------------------
    cap = P.get('cap')
    if cap:
        build_cap(H, acc, kind, cap, h, m)
    ant = P.get('ant')
    if ant:
        mat, length, spread = ant[0], ant[1], ant[2]
        tip = ant[3] if len(ant) > 3 else None
        for sx in (1, -1):
            r = V((sx * rx * (.25 + spread), H.ry * .05, H.z(.88 if not cap else .98)))
            pts = [r, r + V((sx * h * .1 * length, h * .06 * length, h * .32 * length)),
                   r + V((sx * h * .16 * length, h * .16 * length, h * .62 * length))]
            lock(H, acc, pts, h * .045, h * .04, m(mat), n=10, gap=-.002, taper=1.0, seg=8)
            if tip:
                lock(H, acc, [pts[1].lerp(pts[2], .55), pts[2] + (pts[2] - pts[1]).normalized() * h * .02],
                     h * .03, h * .026, m(tip), n=6, gap=-1, taper=1.0, seg=8)
    if P.get('curl_ant'):
        for sx in (1, -1):
            r = V((sx * rx * .3, H.ry * .05, H.z(.9)))
            pts = [r, r + V((sx * h * .12, h * .04, h * .2)), r + V((sx * h * .26, h * .06, h * .26)),
                   r + V((sx * h * .32, h * .02, h * .18)), r + V((sx * h * .28, -h * .01, h * .13))]
            lock(H, acc, pts, h * .04, h * .035, m('Body'), n=16, gap=-.002, taper=1.0, seg=8)
    if P.get('flame_ant'):
        for sx in (1, -1):
            at = V((sx * rx * .5, H.ry * .05, H.z(.8)))
            outer = flat_part(flame_outline(h * .16, h * .38, tongues=3, lean=sx * .6), h * .03, m('Flame'))
            inner = flat_part(flame_outline(h * .08, h * .22, tongues=2, lean=sx * .6), h * .034, m('FlameTip'))
            add_flat(acc, placed(outer, at, yaw=0, roll=-32 * sx))
            add_flat(acc, placed(inner, at + V((sx * h * .012, -h * .004, h * .03)), yaw=0, roll=-32 * sx))
    if P.get('bolt_ant'):
        for sx in (1, -1):
            at = V((sx * rx * .36, H.ry * .02, H.z(.82)))
            bolt = flat_part([(x * sx, z) for x, z in bolt_outline(h * .2, h * .42)][::sx], h * .035, m('Bolt'))
            add_flat(acc, placed(bolt, at, roll=-14 * sx))
    if P.get('bolt_brows'):
        for sx in (1, -1):
            p = H.front(sx * rx * .5, .74, h * .012)
            brow = flat_part([(x * sx, z) for x, z in [(-.5, 0), (.5, .12), (.15, .22), (.6, .5), (-.4, .2),
                                                         (-.05, .12)]][::sx], h * .02, m('Bolt'))
            bm, names = brow
            bmesh.ops.scale(bm, vec=(h * .22, 1, h * .2), verts=bm.verts)
            add_flat(acc, placed((bm, names), p, pitch=-12))
    horns = P.get('horns')
    if horns:
        mat, length = horns
        band = 'HornBand' in P
        for sx in (1, -1):
            r = V((sx * rx * .42, H.ry * .02, H.z(.8)))
            pts = [r, r + V((sx * h * .1, h * .02, h * .14 * length / .6)),
                   r + V((sx * h * .06, h * .1, h * .3 * length / .6))]
            lock(H, acc, pts, h * .09, h * .08, m(mat), n=12, gap=-.003, taper=1.0, seg=10)
            if band:
                for t in (.3, .55):
                    c = pts[0].lerp(pts[1], t * 2) if t < .5 else pts[1].lerp(pts[2], (t - .5) * 2)
                    acc.add(sphere(c, (h * .05 * (1 - t * .6), h * .05 * (1 - t * .6), h * .018), m('HornBand'),
                                   14, 6), 'Head')
    if P.get('spines'):
        for i, (u, ang) in enumerate([(.2, 120), (-.1, 140), (-.4, 120), (.2, -120), (-.1, -140), (-.4, -120)]):
            sx = 1 if ang > 0 else -1
            p = H.side(sx, u, -h * .01, y=H.ry * .25)
            tip = p + V((sx * h * .1, h * .06, h * .03))
            acc.add(loft(p, tip, [(0, h * .03, h * .02), (1, h * .002, h * .002)], seg=8, front=(0, -1, 0),
                         mats=[(0, 1, m('Mask'))]), 'Head')
    if P.get('spider_eyes'):
        for (x, u, r) in [(0, .78, .09), (-.2, .7, .07), (.2, .7, .07), (-.12, .86, .06), (.12, .86, .06)]:
            def at(s, v, x=x, u=u, r=r):
                a = 2 * math.pi * s
                return x * rx * 1.0 + r * rx * v * math.cos(a), u + r * rx * v * math.sin(a) / H.rz
            H.patch(acc, at, 16, 3, 'SlugEyeBlack', lambda s, v: h * .006 * (1 - v * v) + .0006, skirt=.002)
    if P.get('cheek_fins'):
        for sx in (1, -1):
            p = H.side(sx, .5, -h * .02, y=H.ry * .1)
            outline = [(x * sx, z) for x, z in [(0, -.4), (1.0, -.1), (.85, .1), (1.1, .45), (0, .5)]]
            bm, names = flat_part(outline, h * .025, m('Fin'))
            bmesh.ops.scale(bm, vec=(h * .22, 1, h * .22), verts=bm.verts)
            add_flat(acc, placed((bm, names), p, yaw=-20 * sx, roll=-10 * sx))
    if P.get('dorsal'):
        bm, names = flat_part([(-.1, 0), (.9, 0), (.25, .95), (-.2, .4)], h * .03, m('Fin'))
        bmesh.ops.scale(bm, vec=(h * .3, 1, h * .3), verts=bm.verts)
        add_flat(acc, placed((bm, names), back(H, .25) + V((0, -h * .03, 0)), yaw=90))
    if P.get('ram_hood'):
        r = V((0, H.ry * .1, H.z(.92)))
        lock(H, acc, [r, r + V((0, h * .1, h * .22)), r + V((0, h * .3, h * .32))], h * .09, h * .05, m('Crest'),
             n=10, gap=-.002, taper=1.0)
        hood = shell(V((0, H.ry * .05, H.z(.45))), (rx * 1.12, H.ry * 1.1, H.rz * .64),
                     cap_mask(56, 130), m('Hood'), seg=96, rings=48,
                     thickness=h * .02, fix=lambda q: H.outside(q, h * .012))
        acc.add(hood, 'Head')
        for sx in (1, -1):
            r = H.side(sx, .55, 0)
            lock(H, acc, [r, r + V((sx * h * .08, h * .05, -h * .18)), r + V((sx * h * .1, h * .12, -h * .38))],
                 h * .1, h * .03, m('Hood'), n=10, gap=.004, taper=1.0)
    return acc


def cap_mask(front, back):
    """Mask for shell(): down to phi = front degrees over the face and `back` at the nape, with an exact edge."""
    def limit(th):
        return front + (back - front) * min(1.0, abs(th) / 150)

    def mask(th, ph):
        return ph < limit(th)
    mask.limit = limit
    return mask


def build_cap(H, acc, kind, cap, h, m):
    """The hard tops: Grenuke's segmented shell, Hop Rock's lumpy rock and Armashelt's helmet with its gem."""
    rx, ry = H.rx, H.ry
    centre = V((0, ry * .02, H.z(.42)))
    if cap == 'grenade':
        radii = (rx * 1.14, ry * 1.16, H.rz * .66)
        mask = cap_mask(74, 108)

        def matfn(th, ph):
            ring = abs(ph - 34) < 3.2 or abs(ph - 60) < 3.2
            meridian = min(abs(((th + 22.5) % 45) - 22.5), 99) < 2.8 * (1 if ph > 8 else 0)
            return m('Groove') if ring or meridian else m('Shell')
        acc.add(shell(centre, radii, mask, m('Shell'), seg=96, rings=48, thickness=h * .03, matfn=matfn,
                      fix=lambda q: H.outside(q, h * .02)), 'Head', smooth=True)
    elif cap == 'rock':
        radii = (rx * 1.1, ry * 1.14, H.rz * .72)

        def bulge(th, ph):
            t, p = math.radians(th), math.radians(ph)
            return 1 + .07 * math.sin(5 * t + 1.3) * math.sin(3 * p) + .05 * math.cos(7 * t) * math.sin(4 * p + .7)

        mask = cap_mask(84, 100)

        def matfn(th, ph):
            for st, sp, r in ((-48, 52, 13), (48, 52, 13), (0, 22, 10), (140, 48, 12), (-140, 48, 12)):
                d = math.hypot((((th - st) + 180) % 360 - 180) * math.sin(math.radians(max(ph, 8))), ph - sp)
                if d < r:
                    return m('Spot')
            return m('Rock')
        acc.add(shell(centre + V((0, 0, H.rz * .06)), radii, mask, m('Rock'), seg=96, rings=48, thickness=h * .05,
                      bulge=bulge, matfn=matfn, fix=lambda q: H.outside(q, h * .025)), 'Head', smooth=True)
    elif cap == 'helmet':
        radii = (rx * 1.16, ry * 1.18, H.rz * .62)
        mask = cap_mask(56, 96)
        acc.add(shell(centre, radii, mask, m('Helmet'), seg=96, rings=48, thickness=h * .035,
                      fix=lambda q: H.outside(q, h * .02)), 'Head', smooth=True)
        # the gem on the front of the helmet: a stretched diamond standing out of it
        front = V((0, centre.y - radii[1] * math.sin(math.radians(28)) - h * .03,
                   centre.z + radii[2] * math.cos(math.radians(28))))
        gem = bmesh.new()
        bmesh.ops.create_icosphere(gem, subdivisions=1, radius=1.0)
        bmesh.ops.scale(gem, vec=(h * .045, h * .03, h * .085), verts=gem.verts)
        bmesh.ops.translate(gem, vec=front, verts=gem.verts)
        acc.add((gem, [m('Gem')]), 'Head', smooth=False)


def to_object(kind, acc, col):
    obj = acc.to_object('Slug_' + kind, col)
    # the game moves a slug as a whole: no skeleton, no vertex groups
    obj.vertex_groups.clear()
    return obj


def export(kind, out_dir):
    import os
    from bt_lib import clear_scene
    clear_scene()
    col = collection('Slugs')
    acc = build(kind)
    obj = to_object(kind, acc, col)
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, kind + '.fbx')
    for o in bpy.data.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, bake_anim=False,
                             mesh_smooth_type='OFF', apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                             axis_up='Y', use_mesh_modifiers=True, path_mode='AUTO', embed_textures=False)
    return path


def lineup(kinds=IDS, spacing=.62):
    """All the slugs in a row in the current scene (for looking at them)."""
    col = collection('Slugs')
    objs = []
    for i, kind in enumerate(kinds):
        obj = to_object(kind, build(kind), col)
        obj.location = V((i * spacing - (len(kinds) - 1) * spacing / 2, 0, 0))
        objs.append(obj)
    return objs
