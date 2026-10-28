using System.Collections.Generic;
using Bajoterra.Client.Content;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Procedural meshes: boxes and ramps whose UVs are measured in metres (so a texture keeps its scale whatever the
    /// size of the thing), crystals, cones and rocks. Every face is flat shaded. Nothing here needs an asset.
    /// </summary>
    public static class MeshKit
    {
        // ------------------------------------------------------------------------------------------ builder

        private sealed class Builder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int>[] _triangles;

            public Builder(int submeshes)
            {
                _triangles = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++)
                {
                    _triangles[i] = new List<int>();
                }
            }

            /// <summary>One flat triangle that faces the way <paramref name="normal"/> points.</summary>
            public void Tri(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC,
                Vector3 normal)
            {
                // Unity treats the clockwise side as the front: the cross product then points at the viewer.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
                {
                    Vector3 swap = b;
                    b = c;
                    c = swap;
                    Vector2 swapUv = uvB;
                    uvB = uvC;
                    uvC = swapUv;
                }
                int start = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _normals.Add(normal);
                _normals.Add(normal);
                _normals.Add(normal);
                _uvs.Add(uvA);
                _uvs.Add(uvB);
                _uvs.Add(uvC);
                _triangles[submesh].Add(start);
                _triangles[submesh].Add(start + 1);
                _triangles[submesh].Add(start + 2);
            }

            public void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB,
                Vector2 uvC, Vector2 uvD, Vector3 normal)
            {
                Tri(submesh, a, b, c, uvA, uvB, uvC, normal);
                Tri(submesh, a, c, d, uvA, uvC, uvD, normal);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = _triangles.Length;
                for (int i = 0; i < _triangles.Length; i++)
                {
                    mesh.SetTriangles(_triangles[i], i);
                }
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // ------------------------------------------------------------------------------------------ boxes

        /// <summary>
        /// A box centred on the origin. Submesh 0 is the four sides and the bottom (UVs in metres / <paramref name="tile"/>),
        /// submesh 1 the top (metres / <paramref name="topTile"/>), so a plinth can have stone sides and a wooden deck.
        /// <paramref name="seed"/> shifts the UVs so neighbouring boxes do not repeat the same pattern.
        /// </summary>
        public static Mesh Box(Vector3 size, float tile, float topTile, int seed = 0)
        {
            var b = new Builder(2);
            Vector3 h = size * 0.5f;
            Vector2 shift = UvShift(seed);
            float st = 1f / tile;
            float tt = 1f / topTile;

            // +X and -X: u along z, v along y.
            Face(b, 0, new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z),
                new Vector3(h.x, h.y, -h.z), Vector3.right, size.z, size.y, st, shift);
            Face(b, 0, new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, h.y, h.z), Vector3.left, size.z, size.y, st, shift);
            // +Z and -Z: u along x.
            Face(b, 0, new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z),
                new Vector3(h.x, h.y, h.z), Vector3.forward, size.x, size.y, st, shift);
            Face(b, 0, new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z),
                new Vector3(-h.x, h.y, -h.z), Vector3.back, size.x, size.y, st, shift);
            // Bottom (never seen, but it keeps the mesh closed for shadows).
            Face(b, 0, new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z),
                new Vector3(h.x, -h.y, -h.z), Vector3.down, size.z, size.x, st, shift);
            // Top.
            Face(b, 1, new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z),
                new Vector3(h.x, h.y, -h.z), Vector3.up, size.z, size.x, tt, shift);
            return b.Build("Box");
        }

        /// <summary>
        /// Like <see cref="Box"/>, but the UVs are measured from the world origin (the box is centred on
        /// <paramref name="worldCenter"/>), so neighbouring slabs carry one texture across their joints without a seam.
        /// A big floor or wall is cut into slabs this way so that each slab is lit by the lights near it (the renderer
        /// only takes the few strongest lights per object).
        /// </summary>
        public static Mesh WorldBox(Vector3 size, Vector3 worldCenter, float tile, float topTile)
        {
            var b = new Builder(2);
            Vector3 h = size * 0.5f;
            Vector3 c = worldCenter;
            float st = 1f / tile;
            float tt = 1f / topTile;
            // +X / -X faces: u along world z, v along world y.
            Vector2 UvX(float z, float y) { return new Vector2((c.z + z) * st, (c.y + y) * st); }
            Vector2 UvZ(float x, float y) { return new Vector2((c.x + x) * st, (c.y + y) * st); }
            Vector2 UvTop(float x, float z) { return new Vector2((c.z + z) * tt, (c.x + x) * tt); }
            b.Quad(0, new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z),
                new Vector3(h.x, h.y, -h.z), UvX(-h.z, -h.y), UvX(h.z, -h.y), UvX(h.z, h.y), UvX(-h.z, h.y), Vector3.right);
            b.Quad(0, new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, h.y, h.z), UvX(h.z, -h.y), UvX(-h.z, -h.y), UvX(-h.z, h.y), UvX(h.z, h.y), Vector3.left);
            b.Quad(0, new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z),
                new Vector3(h.x, h.y, h.z), UvZ(h.x, -h.y), UvZ(-h.x, -h.y), UvZ(-h.x, h.y), UvZ(h.x, h.y), Vector3.forward);
            b.Quad(0, new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z),
                new Vector3(-h.x, h.y, -h.z), UvZ(-h.x, -h.y), UvZ(h.x, -h.y), UvZ(h.x, h.y), UvZ(-h.x, h.y), Vector3.back);
            b.Quad(0, new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z),
                new Vector3(h.x, -h.y, -h.z), UvTop(-h.x, -h.z), UvTop(-h.x, h.z), UvTop(h.x, h.z), UvTop(h.x, -h.z), Vector3.down);
            b.Quad(1, new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z),
                new Vector3(h.x, h.y, -h.z), UvTop(-h.x, -h.z), UvTop(-h.x, h.z), UvTop(h.x, h.z), UvTop(h.x, -h.z), Vector3.up);
            return b.Build("WorldBox");
        }

        private static void Face(Builder b, int submesh, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
            Vector3 normal, float widthMetres, float heightMetres, float scale, Vector2 shift)
        {
            // p0->p1 runs along u, p1->p2 along v.
            b.Quad(submesh, p0, p1, p2, p3,
                new Vector2(shift.x, shift.y),
                new Vector2(shift.x + widthMetres * scale, shift.y),
                new Vector2(shift.x + widthMetres * scale, shift.y + heightMetres * scale),
                new Vector2(shift.x, shift.y + heightMetres * scale), normal);
        }

        // ------------------------------------------------------------------------------------------ ramps

        /// <summary>
        /// A wedge that fills the box <paramref name="size"/> (centred on the origin): flat underside, the surface
        /// climbing from the bottom edge at one end to the top edge at the other, in the direction of
        /// <paramref name="rise"/>. Submesh 1 is the sloped surface (UV u runs up the slope, in metres /
        /// <paramref name="deckTile"/>, so planks run along the climb); submesh 0 is the rest.
        /// </summary>
        public static Mesh Ramp(Vector3 size, RampDirection rise, float tile, float deckTile, int seed = 0)
        {
            bool alongX = rise == RampDirection.PosX || rise == RampDirection.NegX;
            float length = alongX ? size.x : size.z;
            float width = alongX ? size.z : size.x;
            float height = size.y;
            float hl = length * 0.5f;
            float hw = width * 0.5f;
            float hh = height * 0.5f;
            float slopeLength = Mathf.Sqrt(length * length + height * height);
            Vector3 up = new Vector3(-height / slopeLength, length / slopeLength, 0f);

            // Canonical frame: x runs up the slope (low end at -hl), z across, y up.
            var lowBottom = new Vector3(-hl, -hh, 0f);
            var highBottom = new Vector3(hl, -hh, 0f);
            var highTop = new Vector3(hl, hh, 0f);
            Vector3 lowL = lowBottom + new Vector3(0f, 0f, -hw);
            Vector3 lowR = lowBottom + new Vector3(0f, 0f, hw);
            Vector3 highBL = highBottom + new Vector3(0f, 0f, -hw);
            Vector3 highBR = highBottom + new Vector3(0f, 0f, hw);
            Vector3 highTL = highTop + new Vector3(0f, 0f, -hw);
            Vector3 highTR = highTop + new Vector3(0f, 0f, hw);

            Vector2 shift = UvShift(seed);
            float st = 1f / tile;
            float dt = 1f / deckTile;
            var b = new Builder(2);

            // Sloped surface: u up the slope, v across.
            b.Quad(1, lowL, lowR, highTR, highTL,
                new Vector2(shift.x, shift.y),
                new Vector2(shift.x, shift.y + width * dt),
                new Vector2(shift.x + slopeLength * dt, shift.y + width * dt),
                new Vector2(shift.x + slopeLength * dt, shift.y), up);
            // High end wall.
            b.Quad(0, highBL, highBR, highTR, highTL,
                new Vector2(shift.x, shift.y), new Vector2(shift.x + width * st, shift.y),
                new Vector2(shift.x + width * st, shift.y + height * st), new Vector2(shift.x, shift.y + height * st),
                Vector3.right);
            // Underside.
            b.Quad(0, lowL, lowR, highBR, highBL,
                new Vector2(shift.x, shift.y), new Vector2(shift.x, shift.y + width * st),
                new Vector2(shift.x + length * st, shift.y + width * st), new Vector2(shift.x + length * st, shift.y),
                Vector3.down);
            // Two triangular flanks.
            b.Tri(0, lowL, highBL, highTL, new Vector2(shift.x, shift.y), new Vector2(shift.x + length * st, shift.y),
                new Vector2(shift.x + length * st, shift.y + height * st), Vector3.back);
            b.Tri(0, lowR, highBR, highTR, new Vector2(shift.x, shift.y), new Vector2(shift.x + length * st, shift.y),
                new Vector2(shift.x + length * st, shift.y + height * st), Vector3.forward);

            Mesh mesh = b.Build("Ramp");
            if (rise != RampDirection.PosX)
            {
                RotateAboutY(mesh, rise);
            }
            return mesh;
        }

        /// <summary>Turns a mesh built for a ramp climbing along +x so that it climbs the other way.</summary>
        private static void RotateAboutY(Mesh mesh, RampDirection rise)
        {
            Quaternion turn;
            switch (rise)
            {
                case RampDirection.NegX:
                    turn = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case RampDirection.PosZ:
                    // +x must become +z: a quarter turn that Unity writes as -90 degrees about the up axis.
                    turn = Quaternion.Euler(0f, -90f, 0f);
                    break;
                default:
                    turn = Quaternion.Euler(0f, 90f, 0f);
                    break;
            }
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = turn * vertices[i];
                normals[i] = turn * normals[i];
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.RecalculateBounds();
        }

        // ------------------------------------------------------------------------------------------ natural shapes

        /// <summary>
        /// A hexagonal crystal: a prism that narrows into a pointed tip. The base is at y = 0 and the tip at
        /// <paramref name="height"/>, leaning by <paramref name="lean"/> metres.
        /// </summary>
        public static Mesh Crystal(float radius, float height, int sides, Vector2 lean)
        {
            var b = new Builder(1);
            float shoulder = height * 0.72f;
            var tip = new Vector3(lean.x, height, lean.y);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                var bottom0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                var bottom1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Vector3 top0 = new Vector3(Mathf.Cos(a0) * radius * 0.85f + lean.x * 0.7f, shoulder,
                    Mathf.Sin(a0) * radius * 0.85f + lean.y * 0.7f);
                Vector3 top1 = new Vector3(Mathf.Cos(a1) * radius * 0.85f + lean.x * 0.7f, shoulder,
                    Mathf.Sin(a1) * radius * 0.85f + lean.y * 0.7f);
                Vector3 mid = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                b.Quad(0, bottom0, bottom1, top1, top0, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, mid);
                Vector3 faceNormal = Vector3.Cross(top1 - top0, tip - top0).normalized;
                if (Vector3.Dot(faceNormal, mid) < 0f)
                {
                    faceNormal = -faceNormal;
                }
                b.Tri(0, top0, top1, tip, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f), faceNormal);
            }
            return b.Build("Crystal");
        }

        /// <summary>A cone with its base at y = 0 (flat shaded), pointing up; flip it to make a stalactite.</summary>
        public static Mesh Cone(float radius, float height, int sides, float tile)
        {
            var b = new Builder(1);
            var tip = new Vector3(0f, height, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Vector3 mid = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                Vector3 normal = (mid * height + Vector3.up * radius).normalized;
                float scale = 1f / tile;
                b.Tri(0, p0, p1, tip, new Vector2(i * 0.3f, 0f), new Vector2(i * 0.3f + radius * scale, 0f),
                    new Vector2(i * 0.3f + radius * 0.5f * scale, height * scale), normal);
            }
            return b.Build("Cone");
        }

        /// <summary>A lumpy low-poly boulder of the given radius, resting on y = 0 (flat shaded).</summary>
        public static Mesh Rock(float radius, int seed, float tile)
        {
            // Icosahedron, one subdivision, every vertex pushed in or out by a hash of its direction.
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var corners = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            var b = new Builder(1);
            float scale = 1f / tile;
            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = corners[faces[f]].normalized;
                Vector3 c = corners[faces[f + 1]].normalized;
                Vector3 d = corners[faces[f + 2]].normalized;
                Vector3 ab = ((a + c) * 0.5f).normalized;
                Vector3 cd = ((c + d) * 0.5f).normalized;
                Vector3 da = ((d + a) * 0.5f).normalized;
                RockTri(b, a, ab, da, radius, seed, scale);
                RockTri(b, ab, c, cd, radius, seed, scale);
                RockTri(b, da, cd, d, radius, seed, scale);
                RockTri(b, ab, cd, da, radius, seed, scale);
            }
            return b.Build("Rock");
        }

        private static void RockTri(Builder b, Vector3 a, Vector3 c, Vector3 d, float radius, int seed, float scale)
        {
            Vector3 pa = Lump(a, radius, seed);
            Vector3 pc = Lump(c, radius, seed);
            Vector3 pd = Lump(d, radius, seed);
            Vector3 normal = Vector3.Cross(pc - pa, pd - pa).normalized;
            if (Vector3.Dot(normal, a + c + d) < 0f)
            {
                normal = -normal;
            }
            b.Tri(0, pa, pc, pd, new Vector2(pa.x, pa.z) * scale, new Vector2(pc.x, pc.z) * scale,
                new Vector2(pd.x, pd.z) * scale, normal);
        }

        private static Vector3 Lump(Vector3 direction, float radius, int seed)
        {
            int hx = Mathf.RoundToInt(direction.x * 1000f);
            int hy = Mathf.RoundToInt(direction.y * 1000f);
            int hz = Mathf.RoundToInt(direction.z * 1000f);
            float jitter = 0.72f + 0.5f * Noise.Hash(hx * 31 + hz, hy, seed);
            Vector3 p = direction * (radius * jitter);
            // Flatten the underside so the boulder sits on the floor.
            p.y = Mathf.Max(p.y, -radius * 0.15f);
            return p + Vector3.up * (radius * 0.15f);
        }

        private static Vector2 UvShift(int seed)
        {
            return new Vector2(Noise.Hash(seed, 1, 5), Noise.Hash(seed, 2, 5));
        }
    }
}
