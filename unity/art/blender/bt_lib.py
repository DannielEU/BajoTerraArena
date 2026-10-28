"""Geometry helpers for the Bajoterra character pipeline (Blender 5.x, bpy + bmesh only).

Everything a character or a blaster is made of goes through an Acc (accumulator): parts are built in small bmeshes,
merged into one big bmesh with each vertex weighted 1.0 to ONE bone (rigid skinning), and turned into a single mesh
object at the end. No bpy.ops are used, so the same code runs in the live Blender and in `blender -b`.

Conventions: metres, Z up, the character faces -Y (Blender's "front"), +X is its left-hand side as seen from the front.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

# --------------------------------------------------------------------------------------------------- colours


def srgb(hexstr):
    """'#rrggbb' -> linear RGBA tuple (what Blender stores)."""
    h = hexstr.lstrip('#')
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


SUFFIX = ''  # appended to every datablock name; lets a preview scene hold several characters at once


def material(name, hexcolor, rough=0.5, metal=0.0, emit=0.0):
    """One Principled material per (name, colour). Names are the contract with the Unity importer, see
    CharacterModel.cs: Glow*/Chamber*/Team* are emissive, Metal*/Steel* are metallic, the rest are plain."""
    key = name + SUFFIX
    mat = bpy.data.materials.get(key)
    if mat is None:
        mat = bpy.data.materials.new(key)
    mat.use_nodes = True
    node = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    color = srgb(hexcolor)
    node.inputs['Base Color'].default_value = color
    node.inputs['Roughness'].default_value = rough
    node.inputs['Metallic'].default_value = metal
    if emit > 0:
        node.inputs['Emission Color'].default_value = color
        node.inputs['Emission Strength'].default_value = emit
    else:
        node.inputs['Emission Strength'].default_value = 0.0
    mat.diffuse_color = color
    return mat


# --------------------------------------------------------------------------------------------------- accumulator


class Acc:
    """Collects parts into one bmesh. add(...) merges a part and weights it to a bone."""

    def __init__(self):
        self.bm = bmesh.new()
        self.dl = self.bm.verts.layers.deform.verify()
        self.groups = {}
        self.mats = []

    def group(self, bone):
        if bone not in self.groups:
            self.groups[bone] = len(self.groups)
        return self.groups[bone]

    def mat_index(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def add(self, part, bone, smooth=True, sharp_angle=None):
        """Merge a (bmesh, [material names]) part. smooth: shade smooth. sharp_angle (deg): with smooth, edges
        whose faces meet at more than this angle stay crisp (bevelled hard-surface parts)."""
        bm, names = part
        gi = self.group(bone)
        if smooth and sharp_angle is not None:
            for e in bm.edges:
                if len(e.link_faces) == 2:
                    e.smooth = e.calc_face_angle(0.0) < math.radians(sharp_angle)
        vmap = {}
        for v in bm.verts:
            nv = self.bm.verts.new(v.co)
            nv[self.dl][gi] = 1.0
            vmap[v] = nv
        emap = {}
        for f in bm.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.smooth = smooth
            nf.material_index = self.mat_index(names[f.material_index] if names else 'Skin')
            emap[f] = nf
        if smooth and sharp_angle is not None:
            for e in bm.edges:
                if not e.smooth:
                    a, b = vmap[e.verts[0]], vmap[e.verts[1]]
                    ne = self.bm.edges.get((a, b))
                    if ne is not None:
                        ne.smooth = False
        bm.free()

    def to_object(self, name, collection):
        mesh = bpy.data.meshes.new(name)
        self.bm.verts.index_update()
        self.bm.normal_update()
        indices = {gi: [] for gi in self.groups.values()}
        for v in self.bm.verts:
            for gi in v[self.dl].keys():
                indices[gi].append(v.index)
        self.bm.to_mesh(mesh)
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        for bone, gi in self.groups.items():
            vg = obj.vertex_groups.new(name=bone)
            vg.add(indices[gi], 1.0, 'REPLACE')
        for n in self.mats:
            mesh.materials.append(bpy.data.materials[n + SUFFIX])
        self.bm.free()
        return obj


# --------------------------------------------------------------------------------------------------- primitives


def _new(names):
    return bmesh.new(), list(names)


def _setmat(bm, index=0):
    for f in bm.faces:
        f.material_index = index


def _frame(a, front=None):
    """Orthonormal frame for a part running along unit vector a. Returns (side, front)."""
    a = Vector(a).normalized()
    if front is None:
        front = Vector((0, -1, 0))
    front = Vector(front)
    front = (front - a * front.dot(a))
    if front.length < 1e-4:
        front = Vector((0, 0, 1)) - a * a.z
        if front.length < 1e-4:
            front = Vector((1, 0, 0))
    front.normalize()
    side = a.cross(front)
    return side, front


def loft(p0, p1, profile, seg=14, front=None, cap0=True, cap1=True, mats=None, cap_rings=3, matfn=None, keep=None):
    """A solid that runs from p0 to p1 with an elliptical cross-section that changes along it.

    profile: [(t, rx, ry)] or [(t, rx, ry, ox, oy)] with t in 0..1 (rx along the part's side axis, ry along its
    front axis, ox/oy shift the ring's centre in those axes). cap0/cap1: True = rounded, 'flat' = flat, False = open.
    mats: [(t0, t1, 'Name'), ...] bands along the length, default 'Skin'.
    matfn(t, angle) / keep(t, angle): material name / whether to keep a face, by its place along the part and its angle
    around it in degrees (0 = the front axis, +-180 = the back, positive towards -side), for side panels, open vests."""
    p0, p1 = Vector(p0), Vector(p1)
    axis = p1 - p0
    length = axis.length
    a = axis / length
    side, fr = _frame(a, front)
    names = []

    def mname(t):
        if mats:
            for t0, t1, n in mats:
                if t0 - 1e-6 <= t <= t1 + 1e-6:
                    return n
        return 'Skin'

    bm = bmesh.new()
    rings = []  # (t, [verts])
    spec = []
    for p in profile:
        t, rx, ry = p[0], p[1], p[2]
        ox = p[3] if len(p) > 3 else 0.0
        oy = p[4] if len(p) > 4 else 0.0
        spec.append((t, rx, ry, ox, oy))

    def ring(t, rx, ry, ox, oy, shift):
        c = p0 + a * (t * length + shift) + side * ox + fr * oy
        return [bm.verts.new(c + side * (rx * math.cos(2 * math.pi * i / seg)) + fr * (ry * math.sin(2 * math.pi * i / seg)))
                for i in range(seg)]

    seq = []
    t, rx, ry, ox, oy = spec[0]
    if cap0 is True:
        h = min(rx, ry)
        for k in range(cap_rings, 0, -1):
            ph = (k / (cap_rings + 1)) * math.pi / 2
            seq.append((t, ring(t, rx * math.cos(ph), ry * math.cos(ph), ox, oy, -h * math.sin(ph))))
    for (t, rx, ry, ox, oy) in spec:
        seq.append((t, ring(t, rx, ry, ox, oy, 0.0)))
    t, rx, ry, ox, oy = spec[-1]
    if cap1 is True:
        h = min(rx, ry)
        for k in range(1, cap_rings + 1):
            ph = (k / (cap_rings + 1)) * math.pi / 2
            seq.append((t, ring(t, rx * math.cos(ph), ry * math.cos(ph), ox, oy, h * math.sin(ph))))

    def fangle(i):
        a_mid = 2 * math.pi * (i + 0.5) / seg
        return math.degrees(((a_mid - math.pi / 2 + math.pi) % (2 * math.pi)) - math.pi)

    for (ta, ra), (tb, rb) in zip(seq[:-1], seq[1:]):
        for i in range(seg):
            j = (i + 1) % seg
            tm, ang = (ta + tb) / 2, fangle(i)
            if keep is not None and not keep(tm, ang):
                continue
            f = bm.faces.new((ra[i], ra[j], rb[j], rb[i]))
            f.material_index = _midx(names, matfn(tm, ang) if matfn else mname(tm))
    ends = []
    if cap0 is True:
        t, rx, ry, ox, oy = spec[0]
        pole = bm.verts.new(p0 + a * (-min(rx, ry)) + side * ox + fr * oy)
        ends.append((seq[0][1], pole, mname(t), True))
    elif cap0 == 'flat':
        ends.append((seq[0][1], None, mname(spec[0][0]), True))
    if cap1 is True:
        t, rx, ry, ox, oy = spec[-1]
        pole = bm.verts.new(p0 + a * (length + min(rx, ry)) + side * ox + fr * oy)
        ends.append((seq[-1][1], pole, mname(t), False))
    elif cap1 == 'flat':
        ends.append((seq[-1][1], None, mname(spec[-1][0]), False))
    for verts, pole, n, first in ends:
        if matfn:
            n = matfn(0.0 if first else 1.0, 0.0)
        mi = _midx(names, n)
        if pole is not None:
            for i in range(seg):
                j = (i + 1) % seg
                f = bm.faces.new((verts[j], verts[i], pole) if first else (verts[i], verts[j], pole))
                f.material_index = mi
        else:
            f = bm.faces.new(verts[::-1] if first else verts)
            f.material_index = mi
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm, names or ['Skin']


def _midx(names, n):
    if n not in names:
        names.append(n)
    return names.index(n)


def sphere(center, radii, mat='Skin', seg=18, rings=12):
    bm = bmesh.new()
    m = Matrix.Translation(center) @ Matrix.Diagonal(Vector((radii[0], radii[1], radii[2], 1.0)))
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0, matrix=m)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm, [mat]


def shell(center, radii, mask, mat='Hair', seg=72, rings=48, thickness=0.01, bulge=None, matfn=None, fix=None):
    """A patch of an ellipsoid (hair, caps, hoods): keeps the cells where mask(theta, phi) is true, then gives it a
    thickness. theta: degrees around the vertical from the front (-Y), positive towards +X (the character's left);
    phi: degrees from the top (0) down to the bottom (180). bulge(theta, phi) scales the radius (volume, sweeps).
    fix(point) -> point is applied to every vertex before the thickness (e.g. to keep it outside a head)."""
    c = Vector(center)
    rx, ry, rz = radii
    bm = bmesh.new()
    grid = {}
    where = {}

    def point(th, ph):
        k = bulge(th, ph) if bulge else 1.0
        t, p = math.radians(th), math.radians(ph)
        return c + Vector((rx * k * math.sin(p) * math.sin(t), -ry * k * math.sin(p) * math.cos(t), rz * k * math.cos(p)))

    def vert(i, j):
        key = (i % seg, j) if 0 < j < rings else (0, j)   # one vertex at each pole, no slivers
        if key not in grid:
            th = 360.0 * (i % seg) / seg - 180.0
            ph = 180.0 * j / rings
            v = bm.verts.new(point(th, ph))
            grid[key] = v
            where[v] = th
        return grid[key]

    names = [mat]
    for j in range(rings):
        for i in range(seg):
            th = 360.0 * (i + 0.5) / seg - 180.0
            ph = 180.0 * (j + 0.5) / rings
            if not mask(th, ph):
                continue
            quad = [vert(i, j), vert(i + 1, j), vert(i + 1, j + 1), vert(i, j + 1)]
            uniq = []
            for v in quad:
                if v not in uniq:
                    uniq.append(v)
            if len(uniq) < 3:
                continue
            try:
                f = bm.faces.new(uniq)
            except ValueError:
                continue
            f.material_index = _midx(names, matfn(th, ph)) if matfn else 0
    limit = getattr(mask, 'limit', None)
    if limit is not None:
        # the cells follow the grid, so their border is a staircase: put the border exactly on the hairline
        for v in list(bm.verts):
            if any(e.is_boundary for e in v.link_edges) and v in where:
                v.co = point(where[v], limit(where[v]))
    if fix is not None:
        for v in bm.verts:
            v.co = fix(v.co)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # recalc may flip an open patch inwards: make the normals point away from the centre
    for f in bm.faces:
        if f.normal.dot(f.calc_center_median() - c) < 0:
            f.normal_flip()
    if thickness > 0:
        bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=thickness)
    return bm, names


def hairline(front, side, back, part=0.0, sweep=0.0):
    """Mask for shell(): hair down to phi=front at the forehead, side over the ears, back at the nape. part shifts
    the hairline towards one side (degrees of theta); sweep brings it lower over the right brow (a side fringe)."""
    def limit(theta):
        a = abs(theta - part)
        a = min(a, 360 - a)
        if a <= 90:
            lim = front + (side - front) * (0.5 - 0.5 * math.cos(math.pi * a / 90))
        else:
            lim = side + (back - side) * (0.5 - 0.5 * math.cos(math.pi * (a - 90) / 90))
        t = math.radians(theta)
        return lim + sweep * max(0.0, -math.sin(t)) * max(0.0, math.cos(t)) * 2.0

    def mask(theta, phi):
        return phi < limit(theta)
    mask.limit = limit
    return mask


def cyl(center, radius, length, axis='Z', mat='Metal', seg=20, radius2=None, cap=True, rot=None):
    """Cylinder/cone frustum centred on `center`, along axis X, Y or Z (radius at -axis end is radius, at +axis
    end radius2)."""
    bm = bmesh.new()
    r2 = radius if radius2 is None else radius2
    base = {'X': Matrix.Rotation(math.pi / 2, 4, 'Y'), 'Y': Matrix.Rotation(-math.pi / 2, 4, 'X'),
            'Z': Matrix.Identity(4)}[axis]
    m = Matrix.Translation(center) @ (rot if rot is not None else Matrix.Identity(4)) @ base
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=radius, radius2=r2, depth=length,
                          matrix=m)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm, [mat]


def hull(points, mat='Metal', bevel=0.0, bevel_segments=2, merge_angle=2.0):
    """Convex hull of a point cloud, optionally bevelled: the quickest way to get angular shells, wedges, jaws and
    grips. Coplanar triangles are merged back into proper faces first."""
    bm = bmesh.new()
    verts = [bm.verts.new(Vector(p)) for p in points]
    res = bmesh.ops.convex_hull(bm, input=verts, use_existing_faces=False)
    interior = set(res.get('geom_interior', [])) | set(res.get('geom_unused', []))
    stray = [g for g in interior if isinstance(g, bmesh.types.BMVert) and g.is_valid]
    if stray:
        bmesh.ops.delete(bm, geom=stray, context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(merge_angle), verts=bm.verts, edges=bm.edges)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type='OFFSET', segments=bevel_segments,
                        profile=0.5, affect='EDGES', clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm, [mat]


def box(center, size, mat='Metal', bevel=0.004, rot=None, taper=None):
    """A bevelled box. size = full (x, y, z). taper = (sx, sz) scale of the -Y end relative to the +Y end."""
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    sx, sz = (1.0, 1.0) if taper is None else taper
    pts = []
    for x in (-1, 1):
        for y in (-1, 1):
            for z in (-1, 1):
                k = (sx, sz) if y < 0 else (1.0, 1.0)
                pts.append((x * hx * k[0], y * hy, z * hz * k[1]))
    bm, names = hull(pts, mat, bevel=bevel, bevel_segments=2)
    m = Matrix.Translation(center) @ (rot if rot is not None else Matrix.Identity(4))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm, names


def cone(base, tip, radius, mat='Hair', seg=10, radius_tip=0.0):
    base, tip = Vector(base), Vector(tip)
    d = tip - base
    bm = bmesh.new()
    up = d.normalized()
    rot = up.to_track_quat('Z', 'Y').to_matrix().to_4x4()
    m = Matrix.Translation((base + tip) / 2) @ rot
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=radius, radius2=radius_tip,
                          depth=d.length, matrix=m)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm, [mat]


def star(center, r_out, r_in, thickness, mat='Trim', points=5, plane='X', rot_deg=0.0):
    """A flat extruded star lying in the plane whose normal is `plane` (X: on the side of a blaster)."""
    bm = bmesh.new()
    verts = []
    for i in range(points * 2):
        ang = math.radians(rot_deg) + math.pi / points * i + math.pi / 2
        r = r_out if i % 2 == 0 else r_in
        verts.append(bm.verts.new((0.0, math.cos(ang) * r, math.sin(ang) * r)))
    face = bm.faces.new(verts)
    res = bmesh.ops.extrude_face_region(bm, geom=[face])
    mv = [g for g in res['geom'] if isinstance(g, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(thickness, 0, 0), verts=mv)
    bmesh.ops.translate(bm, vec=(-thickness / 2, 0, 0), verts=list(bm.verts))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if plane == 'Y':
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'Z'), verts=bm.verts)
    elif plane == 'Z':
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'Y'), verts=bm.verts)
    bmesh.ops.translate(bm, vec=center, verts=list(bm.verts))
    return bm, [mat]


def transform(part, matrix):
    bm, names = part
    bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts)
    return bm, names


def mirror_x(part):
    """Copy of a part mirrored across the YZ plane (for the left side of a right-side part)."""
    bm, names = part
    nb = bm.copy()
    bmesh.ops.scale(nb, vec=(-1, 1, 1), verts=nb.verts)
    bmesh.ops.reverse_faces(nb, faces=nb.faces)
    return nb, list(names)


def collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.actions, bpy.data.materials):
        for item in list(coll):
            coll.remove(item)
