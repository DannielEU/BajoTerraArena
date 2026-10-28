"""The four blasters, rebuilt from the reference art in images/ (`*_wepon.webp`, `Pronto_blaster.webp`). Each builder
works in gun space: the origin is the centre of the pistol grip, the muzzle points to -Y, up is +Z, the round slug dial
faces +/-X. The reference pictures show the gun's +X side with the muzzle to the left.

Eli     orange forked shell (a long upper prong and a shorter lower one) with a big blue window and a blue star, a
        round dial behind it, a grey frame with a big square trigger guard, a dark grip.
Trixie  a bulbous green nose with a blue window, an orange star outlined in black and a blue lens at the front, a
        silver body with a tall fin, a big dial under the front, a curved black grip with a silver backstrap.
Kord    a blue shark head: two open jaws full of teeth with a red eye, two barrels with orange rings and cyan cores
        between them, a dark body with an orange star, a big dial at the back and a long black grip.
Pronto  a long rifle: flared funnel with an orange rim, a glass housing with an orange top bar, a thin rod, a handle
        under the barrel, a big dial with a scope over it, a square frame around the grip and a stock with four vials.

A builder gets `A(part, smooth=..., sharp=...)` (adds a part to the character mesh, skinned to the right hand, already
moved to where the gun is held) and returns the points the rig needs: where the hands go (grip_r; grip_l and the
optional `supports`, other places the left hand may hold), the slug dial (`chamber`, which glows in the colour of the
selected slug in the game) and the muzzle.

Material roles (the Unity importer keys on the names): GunMain / GunMain2 shell colours, Metal (dark), Steel (light),
Steel2 (white-ish), Rubber (black), GunTrim (orange accents), Tooth, Gum, Glow (cyan light), GlowBlue (blue window),
Chamber (slug dial, recoloured at run time).
"""
import math

from mathutils import Matrix, Vector

from bt_lib import box, cyl, hull, loft, material, sphere, star

V = Vector


def palette(kind):
    """Create the gun materials for one blaster (colours from the reference images)."""
    common = dict(
        Metal=('#3b3f48', 0.45, 0.7), Steel=('#a8b0b9', 0.35, 0.8), Steel2=('#e6eef2', 0.4, 0.3),
        Rubber=('#17191e', 0.7, 0.0), Tooth=('#f4f1ea', 0.35, 0.0),
    )
    for name, (hexcol, rough, metal) in common.items():
        material(name, hexcol, rough, metal)
    material('Glow', '#38e6ff', 0.2, 0.0, emit=3.0)
    material('GlowBlue', '#3da5ff', 0.2, 0.0, emit=2.2)
    material('Chamber', '#38e6ff', 0.2, 0.0, emit=3.0)
    if kind == 'eli':
        material('GunMain', '#f2761b', 0.35, 0.1)
        material('GunMain2', '#3a4756', 0.45, 0.2)
        material('GunTrim', '#ff9a2e', 0.4, 0.0)
        material('GunStar', '#2f7ad8', 0.3, 0.0)
    elif kind == 'trixie':
        material('GunMain', '#4cbf34', 0.35, 0.1)
        material('GunMain2', '#2f8a26', 0.4, 0.1)
        material('GunTrim', '#ff6a1c', 0.4, 0.0)
    elif kind == 'kord':
        material('GunMain', '#2a66d0', 0.3, 0.15)
        material('GunMain2', '#163d80', 0.35, 0.2)
        material('GunTrim', '#ff7a1d', 0.4, 0.0)
        material('Gum', '#b8222c', 0.5, 0.0)
    elif kind == 'pronto':
        material('GunMain', '#5aa6bb', 0.3, 0.25)
        material('GunMain2', '#2f6c80', 0.35, 0.2)
        material('GunTrim', '#f29a1e', 0.4, 0.0)


def sym(pts):
    """[(x, y, z)] -> the same points plus their mirror across the YZ plane."""
    out = []
    for x, y, z in pts:
        out.append((x, y, z))
        out.append((-x, y, z))
    return out


def _dial(A, centre, r_out, thick, with_white=True, studs=0):
    """The round slug dial: steel ring, optional white ring, glowing slug disc, steel hub (axis X); studs: little
    white dots around the glow (Eli's)."""
    c = V(centre)
    A(cyl(c, r_out, thick, 'X', 'Steel', seg=32))
    A(cyl(c, r_out * .9, thick + .004, 'X', 'Metal', seg=32))
    if with_white:
        A(cyl(c, r_out * .8, thick + .008, 'X', 'Steel2', seg=32))
    A(cyl(c, r_out * .64, thick + .012, 'X', 'Chamber', seg=32))
    A(cyl(c, r_out * .34, thick + .016, 'X', 'Steel', seg=24))
    A(cyl(c, r_out * .2, thick + .02, 'X', 'Chamber', seg=16))
    for i in range(studs):
        a = 2 * math.pi * i / studs
        p = c + V((0, math.cos(a) * r_out * .72, math.sin(a) * r_out * .72))
        A(cyl(p, r_out * .06, thick + .016, 'X', 'Steel2', seg=8))


def _bar(A, p0, p1, thick, mat='Steel', depth=None):
    """A straight rod between two points (trigger guards, handles)."""
    p0, p1 = V(p0), V(p1)
    d = p1 - p0
    rot = d.normalized().to_track_quat('Y', 'Z').to_matrix().to_4x4()
    A(box((p0 + p1) / 2, (thick if depth is None else depth, d.length + thick * .5, thick), mat, min(.004, thick * .3),
          rot=rot), smooth=True, sharp=40)


def _rod(A, p0, p1, r, mat='Steel', seg=10):
    p0, p1 = V(p0), V(p1)
    d = p1 - p0
    rot = d.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
    A(cyl((p0 + p1) / 2, r, d.length, 'Z', mat, seg=seg, rot=rot))


def _bolt(A, p, r, mat='Steel'):
    A(cyl(p, r, .006, 'X', mat, seg=12))


def _star_outlined(A, c, r, rot, fill='GunTrim', line='Rubber', inner=None):
    for s in (1, -1):
        p = V((s * c[0], c[1], c[2]))
        A(star(p, r * 1.2, r * .5, .004, line, plane='X', rot_deg=rot), smooth=False)
        A(star(p + V((s * .0015, 0, 0)), r, r * .4, .0045, fill, plane='X', rot_deg=rot), smooth=False)
        if inner:
            A(star(p + V((s * .003, 0, 0)), r * .5, r * .2, .005, inner, plane='X', rot_deg=rot), smooth=False)


# ------------------------------------------------------------------------------------------------------- Eli


def eli_blaster(A):
    palette('eli')
    # the orange shell: a solid rear block holding the window, a long upper prong and a shorter lower one
    A(hull(sym([(.037, -.06, -.012), (.037, -.06, .112), (.036, -.25, -.008), (.036, -.25, .104)]), 'GunMain',
           bevel=.008, bevel_segments=3), smooth=True, sharp=35)
    A(hull(sym([(.033, -.06, .1), (.03, -.19, .1), (.022, -.08, .138), (.02, -.15, .13)]), 'GunMain', bevel=.006,
           bevel_segments=2), smooth=True, sharp=35)
    A(hull(sym([(.035, -.24, .064), (.035, -.24, .104), (.012, -.385, .094), (.014, -.37, .104)]), 'GunMain',
           bevel=.006, bevel_segments=2), smooth=True, sharp=35)
    A(hull(sym([(.035, -.24, -.008), (.035, -.24, .036), (.016, -.345, .012), (.016, -.335, .03)]), 'GunMain',
           bevel=.006, bevel_segments=2), smooth=True, sharp=35)
    # the barrel showing in the slot between the prongs
    A(cyl((0, -.29, .05), .015, .1, 'Y', 'Metal', seg=14))
    A(cyl((0, -.345, .05), .011, .012, 'Y', 'Glow', seg=12))
    # the big blue window on both sides (framed in dark grey) with the slug tube seen through it, and the star
    for s in (1, -1):
        A(box((s * .0375, -.16, .05), (.004, .17, .082), 'GunMain2', .01), smooth=True, sharp=50)
        A(box((s * .039, -.16, .05), (.004, .152, .066), 'GlowBlue', .012), smooth=True, sharp=50)
        A(box((s * .0405, -.16, .05), (.003, .12, .02), 'Glow', .006), smooth=True, sharp=50)
        A(star((s * .034, -.1, .118), .018, .007, .004, 'GunStar', plane='X', rot_deg=10), smooth=False)
    # grey frame behind the shell, a rear spur and the dial with its ring of studs
    A(hull(sym([(.028, -.07, -.016), (.028, -.07, .086), (.026, .12, .0), (.026, .12, .074)]), 'Steel', bevel=.006,
           bevel_segments=2), smooth=True, sharp=35)
    A(hull(sym([(.02, .09, .06), (.02, .15, .086), (.018, .165, .118), (.016, .12, .1)]), 'Steel', bevel=.004),
      smooth=True, sharp=35)
    _dial(A, (0, .0, .05), .048, .074, with_white=False, studs=8)
    # the big square trigger guard, the orange trigger, the grip with dark side panels and a heel plate
    _bar(A, (0, -.072, -.012), (0, -.072, -.112), .012)
    _bar(A, (0, -.072, -.112), (0, .03, -.122), .012)
    A(box((0, -.02, -.035), (.012, .012, .04), 'GunTrim', .003, rot=Matrix.Rotation(math.radians(-20), 4, 'X')))
    rot = Matrix.Rotation(math.radians(18), 4, 'X')
    A(box((0, .07, -.09), (.046, .066, .2), 'Steel', .01, rot=rot), smooth=True, sharp=35)
    for s in (1, -1):
        A(box((s * .0235, .074, -.095), (.004, .05, .15), 'GunMain2', .004, rot=rot), smooth=True, sharp=50)
    A(box((0, .098, -.19), (.05, .08, .02), 'Metal', .006, rot=rot), smooth=True, sharp=35)
    return dict(grip_r=V((0, .07, -.06)), grip_l=V((0, -.19, -.03)), supports=[V((0, -.07, -.06))],
                chamber=V((0, .0, .05)), muzzle=V((0, -.39, .05)), length=.56)


# ----------------------------------------------------------------------------------------------------- Trixie


def trixie_blaster(A):
    palette('trixie')
    # the bulbous green nose
    A(loft(V((0, -.1, .1)), V((0, -.36, .085)), [(0, .052, .1), (.3, .06, .11), (.68, .05, .085), (.9, .032, .05),
                                                (1, .022, .034)],
           seg=28, front=(0, 0, 1), cap0=True, cap1='flat', mats=[(0, 1, 'GunMain')]), smooth=True)
    # green fins: one under the front, two spiky ones over the nose
    A(hull(sym([(.01, -.2, .02), (.01, -.32, .045), (.008, -.27, -.035), (.006, -.33, -.01)]), 'GunMain2', bevel=.004),
      smooth=True, sharp=35)
    A(hull(sym([(.01, -.13, .19), (.01, -.22, .2), (.006, -.11, .235)]), 'GunMain', bevel=.003), smooth=True, sharp=35)
    A(hull(sym([(.01, -.25, .175), (.01, -.32, .14), (.006, -.235, .215)]), 'GunMain', bevel=.003), smooth=True, sharp=35)
    A(cyl((0, -.3, .15), .022, .07, 'X', 'GunTrim', seg=18))
    # blue window on both sides in a silver frame, the outlined orange star over it, blue lens and orange ring in front
    for s in (1, -1):
        A(box((s * .05, -.22, .1), (.012, .13, .07), 'Steel', .012, rot=Matrix.Rotation(math.radians(-s * 6), 4, 'Z')),
          smooth=True, sharp=50)
        A(box((s * .055, -.22, .1), (.008, .11, .054), 'GlowBlue', .012, rot=Matrix.Rotation(math.radians(-s * 6), 4, 'Z')),
          smooth=True, sharp=50)
    _star_outlined(A, (.046, -.2, .176), .036, -14)
    A(cyl((0, -.36, .085), .026, .016, 'Y', 'GunTrim', seg=20))
    A(cyl((0, -.366, .085), .019, .01, 'Y', 'GlowBlue', seg=20))
    # silver body with the tall fin on top
    A(hull(sym([(.042, -.12, .0), (.042, -.12, .19), (.04, .03, .03), (.04, .03, .16), (.034, .07, .07), (.034, .07, .14)]),
           'Steel', bevel=.008, bevel_segments=2), smooth=True, sharp=35)
    A(hull(sym([(.007, -.09, .18), (.007, -.03, .18), (.004, -.075, .27), (.004, -.06, .262)]), 'Steel', bevel=.003),
      smooth=True, sharp=35)
    for y in (-.06, .0):
        _bolt(A, (.043, y, .1), .008)
        _bolt(A, (-.043, y, .1), .008)
    # the big dial under the front
    _dial(A, (0, -.15, -.02), .056, .1)
    # curved black grip with a silver backstrap, wire trigger guard, orange trigger
    rot = Matrix.Rotation(math.radians(25), 4, 'X')
    A(hull(sym([(.022, .0, .02), (.022, .07, .03), (.024, .02, -.12), (.024, .1, -.12), (.02, .05, -.17), (.02, .12, -.16)]),
           'Rubber', bevel=.008, bevel_segments=2), smooth=True, sharp=35)
    A(hull(sym([(.018, .07, .03), (.018, .085, .03), (.02, .1, -.12), (.02, .125, -.12), (.016, .12, -.165), (.016, .135, -.16)]),
           'Steel', bevel=.004), smooth=True, sharp=35)
    _bar(A, (0, -.1, -.07), (0, -.02, -.13), .007)
    _bar(A, (0, -.02, -.13), (0, .06, -.16), .007)
    A(box((0, -.03, -.04), (.012, .012, .038), 'GunTrim', .003, rot=Matrix.Rotation(math.radians(-18), 4, 'X')))
    return dict(grip_r=V((0, .06, -.06)), grip_l=V((0, -.25, -.0)), supports=[V((0, -.15, -.085))],
                chamber=V((0, -.15, -.02)), muzzle=V((0, -.37, .085)), length=.5)


# ------------------------------------------------------------------------------------------------------ Kord


def kord_blaster(A):
    palette('kord')
    # dark body with an orange star and a red emblem, bolts
    A(hull(sym([(.05, -.1, -.03), (.05, -.1, .17), (.05, .16, -.01), (.05, .16, .15), (.046, .2, .04), (.046, .2, .12)]),
           'Metal', bevel=.01, bevel_segments=3), smooth=True, sharp=35)
    _star_outlined(A, (.05, .02, .07), .03, -10, line='Metal')
    for s in (1, -1):
        A(hull([(s * .0525, -.06, .05), (s * .0525, -.02, .05), (s * .0525, -.04, .09), (s * .056, -.04, .066)], 'GunTrim'),
          smooth=False)
        for y, z in ((-.08, .15), (-.08, -.01), (.12, .14), (.12, -.0)):
            _bolt(A, (s * .051, y, z), .009)
    # the big dial at the back
    _dial(A, (0, .13, .13), .095, .112)
    # long black grip with a flared heel and the orange trigger hooking down
    A(hull(sym([(.04, .02, -.02), (.04, .14, -.02), (.044, .09, -.3), (.044, .22, -.29)]), 'Rubber', bevel=.008,
           bevel_segments=2), smooth=True, sharp=35)
    A(hull(sym([(.048, .07, -.29), (.048, .25, -.29), (.05, .06, -.32), (.05, .27, -.32)]), 'Metal', bevel=.006),
      smooth=True, sharp=35)
    for s in (1, -1):
        A(hull([(s * x, y, z) for x0, y, z in ((.0445, .05, -.06), (.0445, .12, -.06), (.0465, .1, -.26), (.0465, .19, -.25))
                for x in (x0, x0 - .0035)], 'GunMain2', bevel=.003), smooth=True, sharp=40)
    _bar(A, (0, -.04, -.04), (0, -.02, -.1), .016, 'GunTrim')
    _bar(A, (0, -.02, -.1), (0, -.045, -.135), .014, 'GunTrim')
    # the shark: upper jaw with a dorsal fin and a red eye, lower jaw, red gums and rows of teeth
    A(hull(sym([(.062, -.08, .25), (.062, -.08, .13), (.054, -.3, .26), (.048, -.44, .225), (.032, -.6, .18),
                (.012, -.71, .15), (.014, -.7, .12), (.04, -.46, .125), (.056, -.2, .13)]),
           'GunMain', bevel=.007, bevel_segments=3), smooth=True, sharp=38)
    A(hull(sym([(.058, -.1, -.01), (.058, -.1, -.085), (.05, -.3, .0), (.04, -.47, .02), (.014, -.67, .035),
                (.014, -.65, .005), (.036, -.45, -.05), (.052, -.25, -.095)]),
           'GunMain', bevel=.007, bevel_segments=3), smooth=True, sharp=38)
    A(hull(sym([(.008, -.12, .24), (.008, -.28, .25), (.005, -.21, .32), (.004, -.17, .31)]), 'GunMain2', bevel=.003),
      smooth=True, sharp=35)
    _rod(A, (0, -.03, .235), (0, .02, .25), .005, 'Rubber')
    for s in (1, -1):
        for k in range(3):   # gill plates
            A(box((s * .058, -.11 - k * .03, .19), (.005, .012, .07), 'GunMain2', .002), smooth=False)
        # the angry red eye
        A(hull([(s * .047, -.44, .19), (s * .041, -.52, .172), (s * .044, -.48, .21), (s * .038, -.53, .19)], 'Gum',
               bevel=.002), smooth=True, sharp=50)
        A(sphere((s * .045, -.485, .19), (.004, .01, .006), 'Rubber', 8, 6))
        A(hull([(s * .046, -.42, .2), (s * .04, -.54, .2), (s * .046, -.42, .207), (s * .04, -.54, .214)], 'GunMain2',
               bevel=.001), smooth=False)
    n = 12
    for i in range(n):
        t = i / (n - 1)
        y = -.12 - t * .55
        # saw-like rows: upper teeth hang from the jaw's lower edge, lower teeth stand on the lower jaw's edge
        zu = .135 - t * .012
        wu = .056 - t * .04
        zl = -.01 + t * .042
        wl = .054 - t * .04
        for s in (1, -1):
            A(hull([(s * (wu + .002), y + .013, zu), (s * (wu + .002), y - .013, zu), (s * (wu - .006), y, zu),
                    (s * wu, y - .002, zu - .052 + t * .014)], 'Tooth', bevel=.0015), smooth=True, sharp=40)
            A(hull([(s * (wl + .002), y + .001, zl), (s * (wl + .002), y - .025, zl), (s * (wl - .006), y - .012, zl),
                    (s * wl, y - .014, zl + .046 - t * .012)], 'Tooth', bevel=.0015), smooth=True, sharp=40)
        if i < n - 1:
            for s in (1, -1):
                A(box((s * (wu + .001), y - .025, zu + .006), (.006, .05, .014), 'Gum', .002), smooth=False)
                A(box((s * (wl + .001), y - .037, zl - .006), (.006, .05, .014), 'Gum', .002), smooth=False)
    # between the jaws: two dark barrels with orange rings and cyan light bands, a blue core between them
    A(cyl((0, -.17, .06), .042, .1, 'Y', 'Metal', seg=20))
    A(cyl((0, -.12, .06), .046, .014, 'Y', 'GunTrim', seg=20))
    A(cyl((0, -.18, .06), .0435, .016, 'Y', 'Glow', seg=20))
    A(cyl((0, -.27, .06), .03, .1, 'Y', 'GlowBlue', seg=18))
    A(cyl((0, -.39, .06), .046, .13, 'Y', 'Metal', seg=20))
    A(cyl((0, -.33, .06), .05, .014, 'Y', 'GunTrim', seg=20))
    A(cyl((0, -.45, .06), .05, .014, 'Y', 'GunTrim', seg=20))
    A(cyl((0, -.39, .06), .0475, .016, 'Y', 'Glow', seg=20))
    A(cyl((0, -.49, .06), .024, .05, 'Y', 'Metal', seg=14))
    A(cyl((0, -.515, .06), .018, .006, 'Y', 'Glow', seg=12))
    return dict(grip_r=V((0, .11, -.11)), grip_l=V((0, -.2, -.09)), supports=[V((0, -.02, -.06))],
                chamber=V((0, .13, .13)), muzzle=V((0, -.7, .07)), length=.92)


# ---------------------------------------------------------------------------------------------------- Pronto


def pronto_blaster(A):
    palette('pronto')
    # flared funnel with an orange rim and a dark mouth, on a steel neck
    A(cyl((0, -.6, .075), .03, .12, 'Y', 'GunMain', seg=28, radius2=.078))
    A(cyl((0, -.66, .075), .082, .016, 'Y', 'GunTrim', seg=28))
    A(cyl((0, -.665, .075), .062, .012, 'Y', 'Rubber', seg=28))
    A(cyl((0, -.665, .075), .02, .016, 'Y', 'Glow', seg=12))
    A(cyl((0, -.51, .075), .032, .07, 'Y', 'Steel', seg=16))
    # the glass housing between two end blocks, an orange top bar and a steel bottom rail
    A(box((0, -.455, .075), (.088, .03, .124), 'GunMain2', .006), smooth=True, sharp=35)
    A(box((0, -.165, .075), (.088, .03, .124), 'GunMain2', .006), smooth=True, sharp=35)
    A(cyl((0, -.31, .075), .036, .27, 'Y', 'Glow', seg=20))
    A(cyl((0, -.31, .075), .041, .25, 'Y', 'GlowBlue', seg=20, cap=False))
    A(box((0, -.31, .137), (.05, .3, .018), 'GunTrim', .005), smooth=True, sharp=35)
    A(box((0, -.31, .013), (.04, .3, .014), 'Steel', .004), smooth=True, sharp=35)
    # the thin rod running along the top
    _rod(A, (0, -.56, .158), (0, -.1, .158), .008)
    _bar(A, (0, -.53, .1), (0, -.53, .158), .008)
    # the handle under the barrel: a steel frame and a black grip bar
    _bar(A, (0, -.4, .006), (0, -.4, -.07), .014)
    _bar(A, (0, -.24, .006), (0, -.24, -.07), .014)
    A(cyl((0, -.32, -.075), .018, .17, 'Y', 'Rubber', seg=14))
    # receiver and the big dial with the scope over it
    A(hull(sym([(.042, -.15, -.02), (.042, -.15, .15), (.042, .1, -.03), (.042, .1, .13), (.036, .14, .02), (.036, .14, .1)]),
           'Steel', bevel=.008, bevel_segments=2), smooth=True, sharp=35)
    _dial(A, (0, -.03, .07), .1, .1)
    A(box((0, -.03, .19), (.026, .07, .04), 'Metal', .005), smooth=True, sharp=35)
    A(cyl((0, -.04, .23), .022, .26, 'Y', 'GunMain', seg=16))
    for y in (-.1, -.06, -.02):
        A(cyl((0, y, .23), .0245, .012, 'Y', 'GunMain2', seg=16))
    A(cyl((0, -.185, .23), .026, .04, 'Y', 'Metal', seg=16, radius2=.032))
    A(cyl((0, -.207, .23), .026, .008, 'Y', 'Glow', seg=16))
    A(cyl((0, .1, .23), .02, .03, 'Y', 'Rubber', seg=14))
    # the pistol grip with the orange trigger, inside a square steel frame
    rot = Matrix.Rotation(math.radians(18), 4, 'X')
    A(box((0, .09, -.075), (.042, .062, .17), 'Steel', .008, rot=rot), smooth=True, sharp=35)
    A(box((0, .095, -.075), (.046, .04, .12), 'Rubber', .005, rot=rot), smooth=True, sharp=35)
    _bar(A, (0, -.06, -.02), (0, -.06, -.17), .011)
    _bar(A, (0, -.06, -.17), (0, .2, -.17), .011)
    A(box((0, .015, -.04), (.012, .012, .036), 'GunTrim', .003, rot=Matrix.Rotation(math.radians(-18), 4, 'X')))
    # the stock: a teal block with a window of four glowing vials, and the hooked butt plate
    A(hull(sym([(.034, .13, .12), (.034, .13, -.05), (.03, .36, .1), (.03, .36, -.12)]), 'GunMain', bevel=.006,
           bevel_segments=2), smooth=True, sharp=35)
    for s in (1, -1):
        A(box((s * .033, .25, .0), (.004, .17, .09), 'Metal', .003), smooth=False)
        for i in range(4):
            A(cyl((s * .035, .19 + i * .04, .0), .013, .08, 'Z', 'Glow', seg=12), smooth=True)
    A(hull(sym([(.026, .35, .1), (.026, .39, .09), (.026, .39, -.14), (.026, .35, -.14), (.022, .37, .16),
                (.022, .4, .15)]), 'Steel', bevel=.004), smooth=True, sharp=35)
    return dict(grip_r=V((0, .09, -.06)), grip_l=V((0, -.32, -.075)),
                supports=[V((0, -.03, -.05)), V((0, .27, -.09))],
                chamber=V((0, -.03, .07)), muzzle=V((0, -.67, .075)), length=1.07)


BUILDERS = dict(eli=eli_blaster, trixie=trixie_blaster, kord=kord_blaster, pronto=pronto_blaster)
