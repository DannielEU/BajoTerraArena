"""Heads and faces for the Bajoterra heroes: a dense head mesh sculpted in code, with the features laid on its surface.

The head starts as a loft from chin to crown through rows (u, width, depth, forward shift), u in -1..1 of its height,
like a turnaround drawing read slice by slice. Then a list of soft bumps (`Bump`) pushes the face forwards or back:
brow ridge, cheekbones, eye sockets, nose, lips, chin. The result is kept in a BVH so every feature can be placed exactly
on the sculpted surface:

- `Head.patch` fills a region of the face (eye whites, irises, mouth, lips) with a grid that hugs the skin, with a skirt
  that tucks under it so no gap shows from the side;
- `Head.strip` lays a raised band along a curve (lash lines, brows, the line of the mouth, lid creases).

Face coordinates are (x in metres, the character's left is +x; u in units of the head's half height, 0 = centre).
Every vertex is skinned to the `Head` bone.
"""
import math

import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

from bt_lib import _midx, hull, loft, sphere

V = Vector


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


class Bump:
    """A soft displacement of the front of the head along -Y (out of the face) centred on (x, u), x in units of the
    head's half width. sx/su: its radii. amp: metres (negative digs in). mirror: also at -x. power > 2 flattens the top
    (a plateau, e.g. a cheekbone), < 2 sharpens it (a ridge)."""

    def __init__(self, x, u, sx, su, amp, mirror=False, power=2.0):
        self.x, self.u, self.sx, self.su, self.amp, self.mirror, self.power = x, u, sx, su, amp, mirror, power

    def value(self, X, U):
        total = 0.0
        for cx in ((self.x, -self.x) if self.mirror else (self.x,)):
            d = math.sqrt(((X - cx) / self.sx) ** 2 + ((U - self.u) / self.su) ** 2)
            total += math.exp(-(d ** self.power))
        return self.amp * total


class Head:
    """A sculpted head plus helpers that put features exactly on its surface."""

    def __init__(self, S, rows, bumps=(), seg=72, mat='Skin', rings=44):
        self.c = S['head_c']
        self.rx, self.ry, self.rz = S['head_r']
        self.rows = rows
        self.bumps = list(bumps)
        self.mat = mat
        self.seg = seg
        self.nrings = rings
        self._make()

    # ---- the mesh --------------------------------------------------------------------------------------------
    def ring(self, u):
        rows = self.rows
        if u <= rows[0][0]:
            return rows[0][1:]
        for r0, r1 in zip(rows[:-1], rows[1:]):
            if u <= r1[0]:
                t = (u - r0[0]) / (r1[0] - r0[0])
                t = t * t * (3 - 2 * t) * .35 + t * .65       # soften the corners between rows
                return tuple(a + (b - a) * t for a, b in zip(r0[1:], r1[1:]))
        return rows[-1][1:]

    def _make(self):
        u0, u1 = self.rows[0][0], self.rows[-1][0]
        z0, z1 = self.z(u0), self.z(u1)
        n = self.nrings
        prof = []
        for i in range(n + 1):
            u = u0 + (u1 - u0) * i / n
            kx, ky, oy = self.ring(u)
            prof.append((i / n, kx * self.rx, ky * self.ry, 0.0, oy * self.ry))
        bm, names = loft((0, 0, z0), (0, 0, z1), prof, seg=self.seg, front=(0, -1, 0), mats=[(0, 1, self.mat)],
                         cap_rings=9)
        bm.normal_update()
        if self.bumps:
            for v in bm.verts:
                face_on = smoothstep(.05, .6, -v.normal.y)
                if face_on <= 0:
                    continue
                X, U = v.co.x / self.rx, (v.co.z - self.c) / self.rz
                d = sum(b.value(X, U) for b in self.bumps)
                v.co.y -= d * face_on
            bm.normal_update()
        self.names = names
        self.bm = bm
        self.bvh = BVHTree.FromBMesh(bm)

    def paint(self, fn):
        """Recolour the head's own faces: fn(x, y, z, normal) -> material name, or None to keep it (markings, hoods)."""
        for f in self.bm.faces:
            c = f.calc_center_median()
            name = fn(c.x, c.y, c.z, f.normal)
            if name:
                f.material_index = _midx(self.names, name)

    def build(self, acc, bone='Head'):
        acc.add((self.bm.copy(), list(self.names)), bone)

    def neck(self, acc, S, top_u=-.75, mat='Skin'):
        nr = S['neck_r']
        acc.add(loft(V((0, 0, S['neck_z'] - .04)), V((0, 0, self.z(top_u))), [(0, nr[0], nr[1]), (1, nr[0] * .95, nr[1] * .95)],
                     seg=20, cap0='flat', cap1='flat', mats=[(0, 1, mat)]), 'Neck')

    def z(self, u):
        return self.c + u * self.rz

    # ---- the surface -----------------------------------------------------------------------------------------
    def surf(self, x, u, out=0.0):
        """Point of the face at (x, u) seen from the front, and the surface normal there."""
        z = self.z(u)
        hit = self.bvh.ray_cast(V((x, -3.0, z)), V((0, 1, 0)))
        if hit[0] is None:
            kx, ky, oy = self.ring(u)
            p = V((x, -oy * self.ry, z))
            nrm = V((x, -.3, 0)).normalized()
            return p + nrm * out, nrm
        p, nrm = hit[0], hit[1]
        return p + nrm * out, nrm

    def front(self, x, u, out=0.0):
        return self.surf(x, u, out)[0]

    def side(self, sx, u, out=0.0, y=0.0):
        """Point on the side of the head at height u, at depth y (from the ring's centre), pushed `out` metres."""
        z = self.z(u)
        kx, ky, oy = self.ring(u)
        o = V((sx * 3.0, y - oy * self.ry, z))
        hit = self.bvh.ray_cast(o, V((-sx, 0, 0)))
        if hit[0] is None:
            return V((sx * (kx * self.rx + out), y - oy * self.ry, z))
        return hit[0] + V((sx * out, 0, 0))

    def inside(self, q, gap=0.0):
        """True if q is inside the head grown by `gap`."""
        loc, nrm, _, dist = self.bvh.find_nearest(q)
        if loc is None:
            return False
        return (q - loc).dot(nrm) < gap

    def outside(self, p, gap):
        """p moved out along the ray from the head's centre until it is at least `gap` outside the head."""
        if not self.inside(p, gap):
            return p
        o = V((0.0, 0.0, self.c))
        d = p - o
        if d.length < 1e-6:
            return p
        d.normalize()
        lo, hi = (p - o).length, 3.0 * self.rz
        for _ in range(30):
            mid = (lo + hi) / 2
            if self.inside(o + d * mid, gap):
                lo = mid
            else:
                hi = mid
        return o + d * hi

    # ---- decals -------------------------------------------------------------------------------------------------
    def patch(self, acc, at, ns, nv, mat, off, skirt=.003, bone='Head', smooth=True):
        """A grid hugging the face. at(s, v) -> (x, u) for s, v in 0..1 (v = 0 and 1 are the two long edges, s = 0
        and 1 the ends); off(s, v) -> metres above the skin. The border gets a skirt that dips under the skin."""
        bm = bmesh.new()
        grid = []
        for i in range(ns + 1):
            row = []
            for j in range(nv + 1):
                s, v = i / ns, j / nv
                x, u = at(s, v)
                p, nrm = self.surf(x, u)
                row.append(bm.verts.new(p + nrm * off(s, v)))
            grid.append(row)
        names = [mat]
        for i in range(ns):
            for j in range(nv):
                try:
                    bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
                except ValueError:
                    pass
        if skirt:
            border = [grid[i][0] for i in range(ns + 1)] + [grid[ns][j] for j in range(1, nv + 1)] + \
                     [grid[i][nv] for i in range(ns - 1, -1, -1)] + [grid[0][j] for j in range(nv - 1, 0, -1)]
            low = []
            for bv in border:
                loc, nrm, _, _ = self.bvh.find_nearest(bv.co)
                low.append(bm.verts.new(loc - nrm * skirt))
            for k in range(len(border)):
                a, b = border[k], border[(k + 1) % len(border)]
                la, lb = low[k], low[(k + 1) % len(border)]
                if a is b:
                    continue
                try:
                    bm.faces.new((b, a, la, lb))
                except ValueError:
                    pass
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        for f in bm.faces:   # point away from the head
            cn = f.calc_center_median()
            loc, nrm, _, _ = self.bvh.find_nearest(cn)
            if loc is not None and f.normal.dot(nrm) < 0 and (cn - loc).dot(nrm) > -1e-4:
                f.normal_flip()
        acc.add((bm, names), bone, smooth=smooth)

    def strip(self, acc, pts, widths, mat, out=.0008, thick=.0015, bone='Head', across=None):
        """A raised band along the face through `pts` [(x, u)], `widths` [metres] per point (tapers to a point when
        0). across: optional per-point unit offsets of the band's centre across it (-1..1) to bias it to one side."""
        bm = bmesh.new()
        sec = []
        P = [self.surf(x, u) for x, u in pts]
        n = len(P)
        for i, (p, nrm) in enumerate(P):
            a = P[min(i + 1, n - 1)][0] - P[max(i - 1, 0)][0]
            t = (a - nrm * a.dot(nrm)).normalized()
            sd = nrm.cross(t).normalized()
            w = max(widths[i], .0004) / 2
            c = p + sd * (w * (across[i] if across else 0.0))
            ring = []
            for k, (dx, dy) in enumerate(((-1, 0), (1, 0), (1, 1), (-1, 1))):
                q = c + sd * (dx * w)
                loc, qn, _, _ = self.bvh.find_nearest(q)
                q = (loc if loc is not None else q) + nrm * (out - .002 if dy == 0 else out + thick)
                ring.append(bm.verts.new(q))
            sec.append(ring)
        for r0, r1 in zip(sec[:-1], sec[1:]):
            for k in range(4):
                j = (k + 1) % 4
                bm.faces.new((r0[k], r0[j], r1[j], r1[k]))
        bm.faces.new(sec[0][::-1])
        bm.faces.new(sec[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        acc.add((bm, [mat]), bone, smooth=True, sharp_angle=60)

    # ---- features -----------------------------------------------------------------------------------------------
    def eye(self, acc, E, sx):
        """One cartoon eye described by the dict E (see Eye below), on the left (sx = 1) or right (sx = -1)."""
        Eye(self, E, sx).build(acc)

    def ears(self, acc, u, mat='Skin', size=1.0, inner='SkinShade', y=.05, tilt=12):
        for sx in (-1, 1):
            base = self.side(sx, u, -.004, y=self.ry * y)
            r = (self.rx * .13 * size, self.ry * .2 * size, self.rz * .27 * size)
            part = sphere(V((0, 0, 0)), r, mat, 16, 12)
            inn = sphere(V((sx * r[0] * .45, -r[1] * .05, -r[2] * .05)), (r[0] * .55, r[1] * .62, r[2] * .68), inner, 12, 10)
            from mathutils import Matrix
            m = Matrix.Translation(base + V((sx * r[0] * .55, 0, 0))) @ Matrix.Rotation(math.radians(-tilt), 4, 'X') \
                @ Matrix.Rotation(math.radians(sx * 14), 4, 'Z')
            for pt in (part, inn):
                bmesh.ops.transform(pt[0], matrix=m, verts=pt[0].verts)
                acc.add(pt, 'Head')


class Eye:
    """A cartoon eye on the face: a domed white almond, an iris and pupil clipped by the lids, a catch-light, a dark
    lash line along the upper lid (with an optional flick) and an optional lower line and lid crease.

    E keys: inner=(x, u), outer=(x, u) corners for the LEFT eye (x > 0, mirrored for the right); up / low: heights of
    the upper and lower lid arcs (u units); peak_up / peak_low: where along the eye they peak (0 inner .. 1 outer);
    iris: radius (m); look=(ds, du): iris centre in the almond (0.5, 0 = centred, du in u units); iris_mat, pupil (k
    of the iris radius); dome (m); lash: (width inner, width outer, flick length in s, flick rise in u) or None;
    lower: width of the lower line or 0; crease: (lift in u, width, mat) or None; lid_mat for a heavy lid; white."""

    def __init__(self, H, E, sx):
        self.H, self.E, self.sx = H, E, sx
        xi, ui = E['inner']
        xo, uo = E['outer']
        self.xi, self.ui, self.xo, self.uo = sx * xi, ui, sx * xo, uo
        self.ku = self._k(E.get('peak_up', .45))
        self.kl = self._k(E.get('peak_low', .55))

    @staticmethod
    def _k(peak):
        return math.log(.5) / math.log(max(.05, min(.95, peak)))

    def base(self, s):
        return self.xi + (self.xo - self.xi) * s, self.ui + (self.uo - self.ui) * s

    def upper(self, s):
        s = min(max(s, 0.0), 1.0)
        return self.base(s)[1] + self.E['up'] * math.sin(math.pi * s ** self.ku) ** self.E.get('round_up', .8)

    def lower(self, s):
        s = min(max(s, 0.0), 1.0)
        return self.base(s)[1] - self.E['low'] * math.sin(math.pi * s ** self.kl) ** self.E.get('round_low', .9)

    def almond(self, s, v):
        x, _ = self.base(s)
        lo, up = self.lower(s), self.upper(s)
        return x, lo + (up - lo) * v

    def dome(self, s, v):
        e = max(0.0, 1 - (2 * s - 1) ** 2)
        f = max(0.0, 1 - (2 * v - 1) ** 2)
        return .0006 + self.E.get('dome', .004) * math.sqrt(e * f) ** .8

    def to_sv(self, x, u):
        s = (x - self.xi) / (self.xo - self.xi)
        sc = min(max(s, .0), 1.0)
        lo, up = self.lower(sc), self.upper(sc)
        v = (u - lo) / max(1e-6, up - lo)
        return s, v

    def clamp(self, x, u, margin=.0):
        s, v = self.to_sv(x, u)
        s = min(max(s, .02), .98)
        v = min(max(v, margin), 1 - margin)
        return self.almond(s, v)

    def build(self, acc):
        H, E = self.H, self.E
        acc_mat = E.get('white', 'Eye')
        H.patch(acc, self.almond, 16, 8, acc_mat, self.dome, skirt=.003)
        # iris / pupil: polar grids clamped to the almond (the lids cover what falls outside)
        ds, du = E.get('look', (.5, 0.0))
        cx, cu = self.base(ds)
        cu += du + (self.upper(ds) + self.lower(ds)) / 2 - self.base(ds)[1]
        r = E['iris']
        ratio = H.rz  # u units are rz metres

        def disc(radius, mat, lift, clamp=True, centre=(cx, cu), nr=5, na=24):
            def at(s, v):
                ang = 2 * math.pi * s
                rr = radius * v
                x, u = centre[0] + rr * math.cos(ang), centre[1] + rr * math.sin(ang) / ratio
                return self.clamp(x, u, .03) if clamp else (x, u)

            def off(s, v):
                x, u = at(s, v)
                ss, vv = self.to_sv(x, u)
                return self.dome(min(max(ss, 0), 1), min(max(vv, 0), 1)) + lift
            H.patch(acc, at, na, nr, mat, off, skirt=0)
        disc(r, E.get('iris_mat', 'Iris'), .0004)
        if E.get('iris_ring'):
            disc(r * .62, E['iris_ring'], .0006)
        disc(r * E.get('pupil', .5), 'Pupil', .0008)
        # the catch-light: upper right of the iris as seen from the front, on both eyes
        disc(r * .22, 'Eye', .0011, clamp=False, centre=(cx + r * .32, cu + r * .38 / ratio), nr=2, na=14)
        # lash line along the upper lid, thicker towards the outer corner, with a flick
        lash = E.get('lash')
        if lash:
            w0, w1, flick, rise = lash
            pts, ws = [], []
            n = 18
            for i in range(n + 1):
                s = -.02 + 1.04 * i / n
                x, _ = self.base(s)
                u = self.upper(s)
                w = w0 + (w1 - w0) * smoothstep(0, 1, s)
                pts.append((x, u + w * .3 / ratio))
                ws.append(w * (.4 + .6 * smoothstep(-.05, .2, s)))
            if flick:
                for i in range(1, 5):
                    t = i / 4
                    s = 1.02 + flick * t
                    x, _ = self.base(s)
                    pts.append((x, self.base(1.0)[1] + rise * t + w1 * .3 / ratio))
                    ws.append(w1 * (1 - t) + .0004)
            H.strip(acc, pts, ws, E.get('lash_mat', 'Lash'), out=self.dome(0, 1) + .0002, thick=E.get('lash_thick', .0018))
        low = E.get('lower', 0)
        if low:
            pts = [(self.base(s)[0], self.lower(s) - low * .3 / ratio) for s in [.25 + .77 * i / 10 for i in range(11)]]
            ws = [low * smoothstep(.2, .5, .25 + .77 * i / 10) for i in range(11)]
            H.strip(acc, pts, ws, E.get('lash_mat', 'Lash'), out=.0004, thick=.0009)
        crease = E.get('crease')
        if crease:
            lift, w, mat = crease
            pts = [(self.base(s)[0], self.upper(s) + lift) for s in [.08 + .9 * i / 12 for i in range(13)]]
            ws = [w * math.sin(math.pi * (.08 + .9 * i / 12)) ** .6 for i in range(13)]
            H.strip(acc, pts, ws, mat, out=.0003, thick=.0007)
        lid = E.get('lid_mat')
        if lid:   # a heavy upper lid: a skin-coloured cap over the top of the eye, down to `lid_drop` of its height
            dd = E.get('lid_drop', .4)       # one value, or (inner, outer) for a slanted (angry / sly) lid

            def drop(s):
                return dd[0] + (dd[1] - dd[0]) * s if isinstance(dd, tuple) else dd

            def at(s, v):
                x, _ = self.base(s)
                up = self.upper(s)
                lo = self.lower(s)
                return x, up + (lo - up) * drop(s) * (1 - v) + .012 * v

            H.patch(acc, at, 16, 5, lid,
                    lambda s, v: self.dome(s, min(1.0, 1 - (1 - v) * drop(s))) + E.get('lid_out', .0016) + .002 * v,
                    skirt=.003)
            if E.get('lid_line', True) and E.get('lash'):
                pts = [(self.base(s)[0], self.upper(s) + (self.lower(s) - self.upper(s)) * drop(s))
                       for s in [-.02 + 1.04 * i / 16 for i in range(17)]]
                w = E['lash'][1]
                H.strip(acc, pts, [w * (.5 + .5 * math.sin(math.pi * i / 16)) for i in range(17)],
                        E.get('lash_mat', 'Lash'), out=self.dome(.5, .5) + E.get('lid_out', .0016) + .0004,
                        thick=E.get('lash_thick', .0018))


def brow(H, acc, pts, widths, mat, thick=.003, out=.001):
    """An eyebrow from inner to outer through pts [(x, u)] on the face, `widths` per point (m)."""
    H.strip(acc, pts, widths, mat, out=out, thick=thick)


def mirrored(pts):
    return [(-x, u) for x, u in pts]


def lips(H, acc, x0, x1, u_line, up, low, mat, bow=.25, line_mat='Mouth', line_w=.0016, smile=0.0, smirk=0.0,
         out_up=.0012, out_low=.0018):
    """Closed lips between the corners at x0 < 0 < x1 on the line at height u_line (with smile: corners raised, smirk:
    the left corner raised more). up / low: heights of the upper and lower lip (u units). bow: Cupid's bow dip."""
    def line(s):
        x = x0 + (x1 - x0) * s
        t = 2 * s - 1
        return x, u_line + smile * t * t + smirk * max(0.0, t) ** 2

    def upper(s, v):
        x, u = line(s)
        t = 2 * s - 1
        h = up * (1 - t * t) ** .6 * (1 - bow * math.exp(-(t / .12) ** 2))
        return x, u + h * v

    def lower(s, v):
        x, u = line(s)
        t = 2 * s - 1
        return x, u - low * (1 - t * t) ** .7 * v

    if up > 0:
        H.patch(acc, upper, 18, 3, mat, lambda s, v: out_up * (1 - (2 * s - 1) ** 2) ** .5 + .0004, skirt=.002)
    if low > 0:
        H.patch(acc, lower, 18, 3, mat, lambda s, v: out_low * (1 - (2 * s - 1) ** 2) ** .5 * (1 - .4 * v) + .0004,
                skirt=.002)
    pts = [line(-.03 + 1.06 * i / 20) for i in range(21)]
    ws = [line_w * (.5 + .5 * math.sin(math.pi * min(1, max(0, (-.03 + 1.06 * i / 20))))) for i in range(21)]
    H.strip(acc, pts, ws, line_mat, out=max(out_up, out_low) * .6 + .0003, thick=.0008)


def open_mouth(H, acc, x0, x1, u_top, u_bot, mat='Mouth', curve=.0, smile=.0, teeth_up=None, teeth_low=None,
               lip_mat=None, depth=.0012, teeth_mat='Tooth', smirk=0.0, pointed=False):
    """An open grin between x0 and x1: the top edge at u_top (bowed down in the middle by `curve`, corners raised by
    `smile`, the left corner raised `smirk` more), the bottom edge reaching u_bot in the middle. teeth_up / teeth_low:
    (count, height in u, gap in m) for rows of teeth along the top / bottom edge; pointed=True makes them sharp
    triangles (fangs, a shark's grin) instead of rounded blocks."""
    def top(s):
        t = 2 * s - 1
        return u_top - curve * (1 - t * t) + smile * t * t + smirk * max(0.0, t) ** 2

    def bot(s):
        t = 2 * s - 1
        return top(s) - (top(s) - u_bot) * (1 - t * t) ** .75

    def at(s, v):
        x = x0 + (x1 - x0) * s
        return x, bot(s) + (top(s) - bot(s)) * v
    H.patch(acc, at, 22, 6, mat, lambda s, v: .0005, skirt=.003)

    def row(spec, edge, sign):
        n, h, gap = spec
        for i in range(n):
            s0, s1 = .06 + .88 * i / n, .06 + .88 * (i + 1) / n
            w = (x1 - x0) * (s1 - s0)
            g = min(gap / max(w, 1e-6), .4) / 2

            def tooth(s, v, s0=s0, s1=s1, g=g):
                ss = s0 + (s1 - s0) * (g + (1 - 2 * g) * s)
                x = x0 + (x1 - x0) * ss
                e = edge(ss)
                lim = bot(ss) if sign < 0 else top(ss)
                far = e + sign * h * ((1 - abs(2 * s - 1)) if pointed else (1 - .25 * (2 * s - 1) ** 2))
                if sign < 0:
                    far = max(far, lim)
                else:
                    far = min(far, lim)
                return x, e + (far - e) * v
            H.patch(acc, tooth, 4, 3, teeth_mat, lambda s, v: .0012 + .0012 * (1 - (2 * s - 1) ** 2), skirt=0)
    if teeth_up:
        row(teeth_up, top, -1)
    if teeth_low:
        row(teeth_low, bot, 1)
    if lip_mat:
        pts = [(x0 + (x1 - x0) * s, bot(s)) for s in [i / 20 for i in range(21)]]
        H.strip(acc, pts, [.004 * (.3 + .7 * math.sin(math.pi * i / 20)) for i in range(21)], lip_mat, out=.0006,
                thick=.0012)


def nostrils(H, acc, x, u, w, h, mat='SkinShade'):
    for sx in (-1, 1):
        def at(s, v, sx=sx):
            ang = 2 * math.pi * s
            return sx * x + w * v * math.cos(ang), u + h * v * math.sin(ang)
        H.patch(acc, at, 12, 2, mat, lambda s, v: .0004, skirt=0)


def goatee(H, acc, u_top, u_bot, w, mat='Hair', thick=.004):
    def at(s, v):
        t = 2 * s - 1
        return t * w * (1 - .55 * v), u_top + (u_bot - u_top) * v
    H.patch(acc, at, 10, 6, mat, lambda s, v: .0008 + thick * (1 - (2 * s - 1) ** 2) ** .5 * (1 - .3 * v), skirt=.002)


def hull_on_face(H, acc, pts_xu_out, mat, bevel=.002):
    """A small solid on the face from [(x, u, out)] points (teeth, a nose tip)."""
    acc.add(hull([H.front(x, u, o) for x, u, o in pts_xu_out], mat, bevel=bevel), 'Head', smooth=True, sharp_angle=50)


# --------------------------------------------------------------------------------------------------- hair


def scalp(H, theta, phi, out=0.0, grow=1.08, top=1.1):
    """A point over the scalp: theta degrees around the vertical from the front (positive towards the character's
    left), phi degrees down from the crown; out = extra distance in units of the head radius."""
    t, p = math.radians(theta), math.radians(phi)
    return V((H.rx * (grow + out) * math.sin(p) * math.sin(t), H.ry * .04 - H.ry * (grow + out) * math.sin(p) * math.cos(t),
              H.c + H.rz * .1 + H.rz * (top + out) * math.cos(p)))


def catmull(points, n):
    """n + 1 points along a Catmull-Rom spline through `points`."""
    P = [V(p) for p in points]
    P = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]
    out = []
    segs = len(P) - 3
    for i in range(n + 1):
        f = i / n * segs
        k = min(int(f), segs - 1)
        t = f - k
        p0, p1, p2, p3 = P[k], P[k + 1], P[k + 2], P[k + 3]
        out.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                         (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    return out


def lock(H, acc, points, width, thick, mat='Hair', n=14, gap=.004, centre=None, twist=0.0, curl=0.0, seg=8,
         taper=.8, bone='Head', point=True):
    """A tapered lock of hair along a smooth curve through `points` (3D), lying on the head: its width runs along the
    scalp and its thickness away from it. Every point is kept `gap` metres outside the head. curl > 0 bends the
    cross-section into a C (a fuller lock); twist turns it (degrees, along the lock). point=False: no taper and a flat
    end (bands, straps, rims)."""
    c = V(centre) if centre is not None else V((0, H.ry * .04, H.c))
    path = [H.outside(p, gap) for p in catmull(points, n)]
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(path):
        t = i / n
        a = path[min(i + 1, n)] - path[max(i - 1, 0)]
        tan = a.normalized()
        rad = p - c
        nrm = (rad - tan * rad.dot(tan)).normalized()
        sd = nrm.cross(tan).normalized()
        if twist:
            ang = math.radians(twist * t)
            sd, nrm = sd * math.cos(ang) + nrm * math.sin(ang), nrm * math.cos(ang) - sd * math.sin(ang)
        if point:
            k = (1 - t) ** taper if t < 1 else 0.0
            w = width * max(k, 0.0) * (.85 + .15 * math.sin(math.pi * min(1, t * 2)))
            h = thick * max(k, 0.0) ** .6
        else:
            w, h = width, thick
        if i == n and point:
            rings.append([bm.verts.new(p)])
            break
        ring = []
        for j in range(seg):
            ang = 2 * math.pi * j / seg
            cx, cy = math.cos(ang), math.sin(ang)
            q = p + sd * (w * cx) + nrm * (h * cy - curl * w * (cx * cx) * .35)
            ring.append(bm.verts.new(q))
        rings.append(ring)
    for r0, r1 in zip(rings[:-1], rings[1:]):
        for j in range(seg):
            jj = (j + 1) % seg
            if len(r1) == 1:
                bm.faces.new((r0[j], r0[jj], r1[0]))
            else:
                bm.faces.new((r0[j], r0[jj], r1[jj], r1[j]))
    bm.faces.new(rings[0][::-1])
    if not point:
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    acc.add((bm, [mat]), bone, smooth=True)


def lock_sph(H, acc, ctrl, width, thick, mat='Hair', **kw):
    """lock() with control points given as (theta, phi, out) over the scalp (see scalp())."""
    lock(H, acc, [scalp(H, th, ph, o) for th, ph, o in ctrl], width, thick, mat, **kw)


def hairline_band(H, acc, front, side, back, width, thick, mat, theta=(-100, 100), lift=4.0, out=.03, n=40, gap=.02,
                  limit=None):
    """A band (headband, cap rim) following a hairline(front, side, back) - or the curve limit(theta) -> phi - just
    above it, from theta[0] to theta[1] degrees (the whole way round when the range is 360)."""
    from bt_lib import hairline as _hl
    lim = limit or _hl(front, side, back).limit
    pts = [scalp(H, th, lim(th) - lift, out) for th in [theta[0] + (theta[1] - theta[0]) * i / n for i in range(n + 1)]]
    lock(H, acc, pts, width, thick, mat, n=n * 2, gap=gap, point=False, seg=10)
