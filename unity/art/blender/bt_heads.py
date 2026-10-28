"""The four heroes' heads: shape, face and hair, matched to the show's renders (images/ and the official character art).

Eli     long angular face, pointed chin; thick dark brows angled down to the nose; big light-blue irises half under a
        heavy black lash line; straight nose; a closed smirk. Navy-black hair: a fringe of long pointed locks swept from
        his left across the forehead to his right brow and cheek, locks in front of the ears, spikes pointing back.
Trixie  heart-shaped face, small pointed chin; green eyes with a bold lash line ending in a flick, thin arched brows,
        a small upturned nose, full pink lips in a smirk. Auburn bob: a big swoop over the forehead to her right, side
        locks curling to the jaw, two short pigtails high at the back.
Kord    broad troll face with a wide jaw and muzzle; heavy brow ridge, small yellow eyes under sly lids, a broad nose,
        a toothy grin and an orange tuft under the lip; small pointed ears. A black helmet of hair with an orange band
        at the front and a grey horn.
Pronto  round mole face with a big drooping mauve snout, bulging eyes under heavy lids, one thick black brow, dark
        freckles, a huge grin full of square teeth. Aviator cap with ear flaps and yellow goggles.
"""
import math

from mathutils import Matrix, Vector

from bt_face import (Bump, Head, brow, goatee, hairline_band, lips, lock, lock_sph, nostrils, open_mouth, scalp,
                     smoothstep)
from bt_lib import cone, cyl, hairline, hull, loft, shell, sphere

V = Vector


def hair_cap(acc, H, front, side, back, grow=1.06, top=1.14, bulge=None, mat='Hair', thickness=.01, sweep=0.0, part=0.0,
             limit=None):
    """Hair (or a cap) covering the whole scalp down to a hairline, every point kept outside the head (no bald
    patches). limit(theta) -> phi replaces the hairline(front, side, back) curve when given."""
    c = V((0, H.ry * .04, H.c + H.rz * .1))
    if limit is None:
        mask = hairline(front, side, back, part=part, sweep=sweep)
    else:
        def mask(theta, phi):
            return phi < limit(theta)
        mask.limit = limit
    acc.add(shell(c, (H.rx * grow, H.ry * grow, H.rz * top), mask, mat=mat,
                  thickness=thickness, bulge=bulge, fix=lambda p: H.outside(p, .006 + thickness)), 'Head', smooth=True)


# --------------------------------------------------------------------------------------------------- Eli

ELI_ROWS = [(-1.02, .06, .08, .5), (-1.0, .2, .22, .48), (-.9, .38, .4, .42), (-.76, .56, .6, .3), (-.58, .73, .8, .17),
            (-.36, .85, .92, .07), (-.12, .91, .97, .02), (.15, .96, 1.0, -.01), (.45, .97, 1.0, -.04), (.7, .88, .93, -.06),
            (.86, .68, .74, -.06), (.95, .42, .46, -.05), (1.0, .08, .09, -.04)]
ELI_BUMPS = [Bump(.42, .24, .34, .09, .0035, True), Bump(.5, .05, .24, .12, -.0035, True),
             Bump(0, -.04, .09, .28, .008), Bump(0, -.3, .12, .09, .009), Bump(.13, -.34, .07, .06, .003, True),
             Bump(.64, -.2, .2, .16, .003, True), Bump(0, -.5, .3, .05, .0015), Bump(0, -.63, .24, .06, .0025),
             Bump(0, -.88, .22, .1, .0035)]


def eli(acc, S):
    H = Head(S, ELI_ROWS, ELI_BUMPS)
    H.neck(acc, S)
    H.build(acc)
    rx = H.rx
    E = dict(inner=(rx * .2, .03), outer=(rx * .74, .1), up=.1, low=.055, peak_up=.4, peak_low=.6, iris=rx * .135,
             look=(.46, .012), iris_ring='IrisDark', pupil=.42, dome=.0035, lash=(.0022, .0042, .1, .03), lower=.0012,
             crease=(.07, .0012, 'SkinShade'))
    for sx in (1, -1):
        H.eye(acc, E, sx)
        pts = [(sx * rx * x, u) for x, u in ((.1, .16), (.28, .2), (.5, .25), (.72, .28), (.88, .24))]
        brow(H, acc, pts, [.013, .015, .012, .008, .003], 'Brow', thick=.003)
    H.ears(acc, .02)
    nostrils(H, acc, rx * .1, -.34, rx * .05, .025)
    lips(H, acc, -rx * .32, rx * .3, -.56, .0, .045, 'Lips', smile=.02, smirk=.05, line_mat='Mouth')

    def bulge(th, ph):
        f = math.cos(math.radians(th))
        return .98 + .08 * math.exp(-((ph - 28) / 30) ** 2) * (.6 + .4 * f) - .04 * smoothstep(95, 125, ph)
    hair_cap(acc, H, 60, 98, 134, bulge=bulge, sweep=18, grow=1.04, top=1.12)
    W, T = rx * .38, rx * .12
    # the fringe: long locks from the parting on his left, swept across the forehead to his right, tips past the brow
    for ctrl, w in (
            ([(45, 16, .06), (18, 40, .13), (-16, 66, .12), (-38, 88, .07), (-48, 104, .03)], 1.0),
            ([(28, 14, .06), (4, 42, .14), (-24, 72, .1), (-46, 96, .05), (-52, 106, .03)], .9),
            ([(62, 22, .06), (40, 46, .13), (16, 68, .12), (4, 84, .06)], .85),
            ([(80, 32, .05), (66, 54, .11), (50, 74, .09), (42, 90, .05)], .8),
            ([(5, 18, .06), (-26, 44, .12), (-58, 72, .09), (-70, 98, .04), (-73, 114, .02)], 1.0),
            ([(-30, 28, .05), (-62, 50, .1), (-86, 78, .06), (-92, 104, .03), (-93, 118, .01)], .85)):
        lock_sph(H, acc, ctrl, W * w, T, curl=.6, gap=.005)
    for s in (1, -1):   # locks in front of the ears
        lock_sph(H, acc, [(s * 85, 50, .05), (s * 96, 78, .06), (s * 100, 100, .04), (s * 102, 116, .02)], W * .65, T * .9,
                 curl=.5)
    # spikes over the top and the back, pointing back and out
    for th, ph, ln in ((150, 28, 1.0), (175, 32, 1.05), (-160, 34, .95), (122, 40, .9), (-128, 44, .9), (158, 58, .9),
                       (-172, 62, .85), (138, 72, .75), (-148, 78, .75), (178, 86, .7), (108, 64, .65), (-108, 68, .65),
                       (90, 40, .6), (-80, 46, .55)):
        lock_sph(H, acc, [(th * .45, ph * .3, .04), (th * .85, ph * .8, .1), (th, ph + 18 * ln, .22 * ln),
                          (th, ph + 32 * ln, .36 * ln)], W * .85, T * 1.1, curl=.4, gap=.006, taper=1.0)
    for th in (150, 180, -150):   # short locks over the nape
        lock_sph(H, acc, [(th, 104, .04), (th, 122, .06), (th, 140, .05)], W * 1.1, T * .8, curl=.4, gap=.004)
    return H


# --------------------------------------------------------------------------------------------------- Trixie

TRIXIE_ROWS = [(-1.02, .05, .07, .46), (-1.0, .16, .2, .44), (-.9, .34, .36, .38), (-.76, .52, .56, .28),
               (-.58, .7, .76, .16), (-.36, .84, .9, .07), (-.12, .93, .96, .02), (.15, .98, 1.0, -.01),
               (.45, .98, 1.0, -.04), (.7, .9, .93, -.06), (.86, .7, .75, -.06), (.95, .44, .47, -.05), (1.0, .08, .09, -.04)]
TRIXIE_BUMPS = [Bump(.45, .22, .34, .08, .002, True), Bump(.5, .05, .24, .12, -.003, True), Bump(0, -.05, .08, .24, .004),
                Bump(0, -.28, .11, .08, .0055), Bump(.12, -.31, .06, .05, .002, True), Bump(.62, -.12, .22, .16, .003, True),
                Bump(0, -.5, .28, .05, .0018), Bump(0, -.62, .24, .06, .003), Bump(0, -.88, .18, .09, .003)]


def trixie(acc, S):
    H = Head(S, TRIXIE_ROWS, TRIXIE_BUMPS)
    H.neck(acc, S)
    H.build(acc)
    rx = H.rx
    E = dict(inner=(rx * .18, .02), outer=(rx * .77, .12), up=.135, low=.065, peak_up=.42, peak_low=.6, iris=rx * .15,
             look=(.46, .008), iris_ring='IrisDark', pupil=.42, dome=.0035, lash=(.0026, .0048, .16, .07), lower=.0014,
             crease=(.075, .0016, 'Lid'))
    for sx in (1, -1):
        H.eye(acc, E, sx)
        pts = [(sx * rx * x, u) for x, u in ((.14, .21), (.32, .28), (.52, .32), (.72, .31), (.88, .25))]
        brow(H, acc, pts, [.004, .0055, .005, .0038, .0012], 'Brow', thick=.0018)
    nostrils(H, acc, rx * .085, -.32, rx * .04, .02)
    lips(H, acc, -rx * .3, rx * .32, -.56, .04, .058, 'Lips', smile=.02, smirk=.045, line_mat='Mouth', bow=.35)

    # auburn bob: flares out over the jaw, longer at the front
    def bob(th, ph):
        a = abs(th)
        return 1.02 + .14 * smoothstep(70, 118, ph) * (1.0 - .45 * smoothstep(110, 180, a))
    def bob_line(theta):   # high over the forehead and the temples, down to the jaw over the ears, short at the back
        a = abs(theta)
        if a <= 100:
            return 54 + 70 * smoothstep(58, 100, a)
        return 124 - 8 * smoothstep(100, 180, a)
    hair_cap(acc, H, 54, 124, 116, bulge=bob, grow=1.05, top=1.13, limit=bob_line)
    W, T = rx * .42, rx * .12
    # the big swoop: from the parting on her left across the forehead above the brows, falling past her right eye
    # down the side of the face to the cheekbone (the eye stays clear)
    for ctrl, w in (
            ([(40, 14, .06), (8, 34, .16), (-30, 52, .15), (-62, 64, .12), (-84, 82, .09), (-94, 104, .06), (-94, 126, .03)], 1.1),
            ([(58, 22, .06), (32, 40, .15), (-4, 54, .14), (-38, 62, .11), (-62, 72, .07), (-74, 84, .03)], 1.0),
            ([(74, 30, .05), (54, 46, .13), (26, 58, .11), (4, 64, .07), (-8, 68, .04)], .9),
            ([(20, 12, .06), (-14, 30, .15), (-52, 46, .14), (-82, 66, .1), (-100, 96, .07), (-102, 124, .03)], 1.05),
            ([(-6, 18, .05), (-46, 36, .12), (-86, 56, .11), (-108, 88, .07), (-110, 120, .02)], 1.0)):
        lock_sph(H, acc, ctrl, W * w, T, curl=.7, gap=.005)
    # side locks down to the jaw, the points curling forwards
    for s, th0 in ((1, 90), (1, 108), (-1, 112), (-1, 128)):
        lock_sph(H, acc, [(s * th0, 50, .07), (s * (th0 - 4), 84, .12), (s * (th0 - 12), 112, .14),
                          (s * (th0 - 20), 128, .08)], W * .9, T, curl=.6, gap=.005)
    for th in (140, 165, -165, -140):   # back of the bob
        lock_sph(H, acc, [(th, 60, .08), (th, 92, .14), (th * .98, 116, .14), (th * .96, 128, .08)], W * 1.0, T, curl=.5)
    # two buns tied high at the back, each with a short full tuft sticking out backwards
    for s in (1, -1):
        bun = scalp(H, s * 142, 70, .14)
        acc.add(sphere(bun, (rx * .28, rx * .3, rx * .27), 'Hair', 18, 12), 'Head')
        out = (V((s * .5, 1.0, -.15))).normalized()
        tie = bun + out * rx * .24
        acc.add(loft(tie - out * .006, tie + out * .01, [(0, rx * .14, rx * .14), (1, rx * .13, rx * .13)], seg=14,
                     cap0='flat', cap1='flat', mats=[(0, 1, 'Outfit2')]), 'Head')
        lock(H, acc, [tie, tie + out * rx * .3 + V((0, 0, -rx * .04)), tie + out * rx * .55 + V((s * rx * .08, 0, -rx * .16))],
             W * 1.1, T * 2.4, curl=.5, gap=-1, taper=.6)
    return H


# --------------------------------------------------------------------------------------------------- Kord

KORD_ROWS = [(-1.02, .34, .22, .3), (-1.0, .7, .42, .3), (-.88, .88, .64, .26), (-.7, .98, .8, .2), (-.45, 1.02, .92, .12),
             (-.2, .98, .97, .06), (.1, .9, .98, .02), (.4, .82, .95, -.02), (.65, .72, .88, -.05), (.84, .54, .7, -.06),
             (.95, .3, .4, -.05), (1.0, .05, .06, -.04)]
KORD_BUMPS = [Bump(.38, .3, .42, .09, .008, True), Bump(.42, .14, .2, .09, -.005, True), Bump(0, .02, .11, .22, .013),
              Bump(0, -.17, .19, .11, .022), Bump(.19, -.23, .09, .07, .008, True), Bump(.6, -.05, .24, .18, .006, True),
              Bump(0, -.42, .4, .07, .004), Bump(0, -.56, .3, .06, .004), Bump(0, -.86, .42, .14, .016, power=2.6)]


def kord(acc, S):
    """A long troll face: big square chin, broad short nose, small amber eyes under sly lids, a lopsided smile showing
    a crescent of upper teeth, an orange tuft under the lip and pointed elf ears."""
    H = Head(S, KORD_ROWS, KORD_BUMPS, mat='Skin')
    H.neck(acc, S, top_u=-.6)
    H.build(acc)
    rx = H.rx
    E = dict(inner=(rx * .14, .15), outer=(rx * .6, .22), up=.11, low=.06, peak_up=.45, peak_low=.55, iris=rx * .125,
             look=(.5, .0), iris_mat='Iris', iris_ring='IrisDark', pupil=.48, dome=.005, lash=(.0035, .005, .06, .02),
             lower=.0016, lid_mat='Skin', lid_drop=(.36, .26), lid_out=.002)
    for sx in (1, -1):
        H.eye(acc, E, sx)
        pts = [(sx * rx * x, u) for x, u in ((.1, .3), (.3, .35), (.52, .38), (.74, .35))]
        brow(H, acc, pts, [.018, .02, .016, .006], 'Brow', thick=.003, out=.0012)
    nostrils(H, acc, rx * .15, -.27, rx * .05, .025, mat='Skin2')
    open_mouth(H, acc, -rx * .42, rx * .46, -.45, -.52, 'Mouth', curve=.0, smile=.06, smirk=.05,
               teeth_up=(6, .05, .0006), lip_mat='Skin2')
    goatee(H, acc, -.6, -.72, rx * .09, 'Goatee', thick=.005)
    # pointed elf ears sticking out backwards and up
    for sx in (1, -1):
        c = H.side(sx, .1, -.012, y=H.ry * .1)
        base = [c + V((0, dy * H.ry, dz * H.rz)) for dy, dz in ((-.12, .0), (.06, .2), (.2, .05), (.1, -.2), (-.06, -.18))]
        tip = c + V((sx * rx * .32, H.ry * .5, H.rz * .42))
        acc.add(hull(base + [b + V((sx * rx * .1, 0, 0)) for b in base] + [tip], 'Skin', bevel=.006), 'Head', smooth=True,
                sharp_angle=60)
        inner = [c + V((sx * rx * .11, dy * H.ry, dz * H.rz)) for dy, dz in ((-.05, .0), (.06, .12), (.13, .03), (.05, -.1))]
        acc.add(hull(inner + [p + V((sx * .004, 0, 0)) for p in inner] + [c.lerp(tip, .8) + V((sx * .004, 0, 0))], 'Skin2',
                     bevel=.002), 'Head', smooth=True, sharp_angle=60)
    # a black helmet of hair down to the brows with an orange band at the front, and the grey horn
    def cap_line(theta):   # straight across the forehead just above the brows, down behind the ears, low at the nape
        a = abs(theta)
        if a <= 70:
            return 70.0
        if a <= 110:
            return 70 + 30 * smoothstep(70, 110, a)
        return 100 + 28 * smoothstep(110, 180, a)
    hair_cap(acc, H, 40, 92, 128, grow=1.05, top=1.12, thickness=.014, limit=cap_line)
    hairline_band(H, acc, 0, 0, 0, rx * .17, .007, 'Trim', theta=(-84, 84), lift=13, out=.035, gap=.022, limit=cap_line)
    base = scalp(H, 0, 22, .02)
    tip = base + V((0, H.ry * .32, H.rz * 1.05))
    acc.add(cone(base, tip, rx * .34, 'Horn', 16), 'Head')
    acc.add(cone(base.lerp(tip, .72), tip + (tip - base).normalized() * .004, rx * .1, 'Horn2', 12), 'Head')
    return H


# --------------------------------------------------------------------------------------------------- Pronto

PRONTO_ROWS = [(-1.02, .1, .1, .3), (-1.0, .5, .5, .3), (-.85, .8, .85, .34), (-.65, .95, 1.12, .4),
               (-.45, 1.0, 1.3, .46), (-.25, .98, 1.4, .5), (-.05, .9, 1.32, .44), (.15, .84, 1.1, .3),
               (.35, .84, .95, .14), (.55, .82, .9, .05), (.75, .68, .78, .0), (.9, .46, .55, -.01), (.97, .22, .26, -.02),
               (1.0, .05, .05, -.02)]
PRONTO_BUMPS = [Bump(.45, .38, .22, .14, -.006, True), Bump(0, -.18, .5, .3, .01, power=2.6)]


def pronto(acc, S):
    """A mole: the whole lower face is a big forward snout with a small dark nose pad at its tip, a grin full of
    square teeth across it and whisker spots on its sides; big eyes under heavy lids above it and two thick arched
    brows. An aviator cap with yellow goggles sits back on the head."""
    H = Head(S, PRONTO_ROWS, PRONTO_BUMPS, seg=80)
    H.neck(acc, S, top_u=-.7)
    H.build(acc)
    rx, rz = H.rx, H.rz
    # big eyes with small pupils under heavy lids, looking slyly to his left
    E = dict(inner=(rx * .1, .26), outer=(rx * .62, .32), up=.18, low=.13, peak_up=.5, peak_low=.5, iris=rx * .075,
             look=(.64, -.02), iris_mat='Pupil', pupil=.6, dome=.008, lash=(.0025, .0035, 0, 0), lower=.0014,
             lid_mat='Skin', lid_drop=(.42, .36), lid_out=.0025)
    for sx in (1, -1):
        H.eye(acc, E, sx)
        pts = [(sx * rx * x, u) for x, u in ((.14, .58), (.28, .68), (.48, .73), (.68, .68), (.8, .58))]
        brow(H, acc, pts, [.007, .018, .022, .018, .006], 'Brow', thick=.003, out=.0012)
    # the nose pad at the tip of the snout, with two big nostrils
    p, nrm = H.surf(0, -.08)
    pad = p - nrm * rx * .05
    acc.add(sphere(pad, (rx * .3, rx * .17, rx * .2), 'Nose', 20, 14), 'Head')
    for sx in (1, -1):
        acc.add(sphere(pad + V((sx * rx * .12, -rx * .13, -rx * .06)), (rx * .075, rx * .05, rx * .055), 'Mouth', 12, 8),
                'Head')
    # the grin across the snout: upper and lower rows of square teeth
    open_mouth(H, acc, -rx * .82, rx * .82, -.34, -.7, 'Mouth', curve=.02, smile=.22, teeth_up=(8, .17, .0016),
               teeth_low=(7, .12, .0016), lip_mat='SkinShade')
    # whisker spots on the sides of the snout
    for sx in (1, -1):
        for x, u in ((.5, -.16), (.62, -.12), (.56, -.24), (.7, -.22), (.64, -.32), (.76, -.3)):
            def at(s, v, x=x, u=u, sx=sx):
                a = 2 * math.pi * s
                return sx * rx * x + rx * .03 * v * math.cos(a), u + rx * .03 * v * math.sin(a) / rz
            H.patch(acc, at, 10, 2, 'Spot', lambda s, v: .0005, skirt=0)
    # aviator cap sitting back on the head, a strap and yellow goggles pushed up on it
    hair_cap(acc, H, 28, 100, 112, grow=1.05, top=1.14, mat='Cap', thickness=.014,
             bulge=lambda th, ph: 1.02 + .03 * smoothstep(60, 100, ph))
    hairline_band(H, acc, 28, 100, 112, rx * .13, .008, 'CapDark', theta=(-180, 180), lift=3, out=.03, n=64, gap=.02)
    hairline_band(H, acc, 22, 64, 76, rx * .16, .007, 'Rubber', theta=(-180, 180), lift=0, out=.07, n=64, gap=.026)
    tilt = Matrix.Rotation(math.radians(-50), 4, 'X')
    for sx in (1, -1):
        g = scalp(H, sx * 25, 26, .22)
        acc.add(cyl(g, .044, .03, 'Y', 'Metal', seg=24, rot=tilt), 'Head')
        acc.add(cyl(g + V((0, -.011, .011)), .036, .014, 'Y', 'Lens', seg=24, rot=tilt), 'Head')
    acc.add(cyl(scalp(H, 0, 23, .2), .011, .046, 'X', 'Metal', seg=10), 'Head')
    return H
