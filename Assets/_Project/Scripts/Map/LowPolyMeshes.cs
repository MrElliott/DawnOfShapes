using System.Collections.Generic;
using UnityEngine;

namespace DawnOfShapes.Map
{
    /// <summary>
    /// Procedural flat-shaded meshes used for placeholder art. Shape meshes fit in a unit cube
    /// centred on the origin (y from -0.5 to 0.5) so they line up with Unity's cube primitive.
    /// </summary>
    public static class LowPolyMeshes
    {
        public static Mesh CreateBox()
        {
            var tris = new List<Vector3>();
            Vector3[] c =
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
            };
            int[][] faces =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
                new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 }
            };
            foreach (var f in faces)
            {
                AddOutwardTri(tris, c[f[0]], c[f[1]], c[f[2]], Vector3.zero);
                AddOutwardTri(tris, c[f[0]], c[f[2]], c[f[3]], Vector3.zero);
            }
            return BuildMesh("Box", tris);
        }

        /// <summary>
        /// Square pyramid, base at y = -0.5 and apex at y = 0.5.
        /// </summary>
        public static Mesh CreatePyramid()
        {
            Vector3 a = new Vector3(-0.5f, -0.5f, -0.5f);
            Vector3 b = new Vector3(0.5f, -0.5f, -0.5f);
            Vector3 c = new Vector3(0.5f, -0.5f, 0.5f);
            Vector3 d = new Vector3(-0.5f, -0.5f, 0.5f);
            Vector3 apex = new Vector3(0f, 0.5f, 0f);
            Vector3 centre = new Vector3(0f, -0.25f, 0f);

            var tris = new List<Vector3>();
            AddOutwardTri(tris, a, apex, b, centre);
            AddOutwardTri(tris, b, apex, c, centre);
            AddOutwardTri(tris, c, apex, d, centre);
            AddOutwardTri(tris, d, apex, a, centre);
            AddOutwardTri(tris, a, b, c, centre);
            AddOutwardTri(tris, a, c, d, centre);
            return BuildMesh("Pyramid", tris);
        }

        /// <summary>
        /// Diamond shape (octahedron), used for crystal-like units.
        /// </summary>
        public static Mesh CreateOctahedron()
        {
            Vector3 top = new Vector3(0f, 0.5f, 0f);
            Vector3 bottom = new Vector3(0f, -0.5f, 0f);
            Vector3[] ring =
            {
                new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0f, -0.5f)
            };
            var tris = new List<Vector3>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = ring[i];
                Vector3 q = ring[(i + 1) % 4];
                AddOutwardTri(tris, p, q, top, Vector3.zero);
                AddOutwardTri(tris, p, q, bottom, Vector3.zero);
            }
            return BuildMesh("Octahedron", tris);
        }

        /// <summary>
        /// Faceted icosphere of radius 0.5. One subdivision gives 80 faces, a clear low-poly ball.
        /// </summary>
        public static Mesh CreateIcosphere(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            var faces = new List<int[]>
            {
                new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 },
                new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
                new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 },
                new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 }
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized * 0.5f;

            for (int s = 0; s < subdivisions; s++)
            {
                var midCache = new Dictionary<long, int>();
                var next = new List<int[]>();
                foreach (var f in faces)
                {
                    int a = Midpoint(verts, midCache, f[0], f[1]);
                    int b = Midpoint(verts, midCache, f[1], f[2]);
                    int c = Midpoint(verts, midCache, f[2], f[0]);
                    next.Add(new[] { f[0], a, c });
                    next.Add(new[] { f[1], b, a });
                    next.Add(new[] { f[2], c, b });
                    next.Add(new[] { a, b, c });
                }
                faces = next;
            }

            var tris = new List<Vector3>();
            foreach (var f in faces) AddOutwardTri(tris, verts[f[0]], verts[f[1]], verts[f[2]], Vector3.zero);
            return BuildMesh("Icosphere", tris);
        }

        private static int Midpoint(List<Vector3> verts, Dictionary<long, int> cache, int i, int j)
        {
            long key = i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i;
            if (cache.TryGetValue(key, out int index)) return index;
            verts.Add(((verts[i] + verts[j]) * 0.5f).normalized * 0.5f);
            index = verts.Count - 1;
            cache[key] = index;
            return index;
        }

        /// <summary>
        /// Faceted ground grid centred on the origin. Each triangle has its own vertices for flat shading.
        /// </summary>
        public static Mesh CreateGround(float size, int cells, float heightJitter, int seed)
        {
            var rng = new System.Random(seed);
            float cell = size / cells;
            float half = size * 0.5f;
            var heights = new float[cells + 1, cells + 1];
            for (int z = 0; z <= cells; z++)
            {
                for (int x = 0; x <= cells; x++)
                {
                    bool edge = x == 0 || z == 0 || x == cells || z == cells;
                    heights[x, z] = edge ? 0f : (float)rng.NextDouble() * heightJitter;
                }
            }

            var tris = new List<Vector3>(cells * cells * 6);
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    Vector3 p00 = new Vector3(-half + x * cell, heights[x, z], -half + z * cell);
                    Vector3 p10 = new Vector3(-half + (x + 1) * cell, heights[x + 1, z], -half + z * cell);
                    Vector3 p01 = new Vector3(-half + x * cell, heights[x, z + 1], -half + (z + 1) * cell);
                    Vector3 p11 = new Vector3(-half + (x + 1) * cell, heights[x + 1, z + 1], -half + (z + 1) * cell);

                    // Alternate the diagonal for a less regular look. Winding is clockwise seen from above.
                    if (((x + z) & 1) == 0)
                    {
                        tris.Add(p00); tris.Add(p01); tris.Add(p11);
                        tris.Add(p00); tris.Add(p11); tris.Add(p10);
                    }
                    else
                    {
                        tris.Add(p00); tris.Add(p01); tris.Add(p10);
                        tris.Add(p10); tris.Add(p01); tris.Add(p11);
                    }
                }
            }
            return BuildMesh("LowPolyGround", tris);
        }

        /// <summary>
        /// Adds a triangle wound so its front face points away from <paramref name="inside"/>.
        /// </summary>
        private static void AddOutwardTri(List<Vector3> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f)
            {
                (b, c) = (c, b);
            }
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        private static Mesh BuildMesh(string name, List<Vector3> tris)
        {
            var indices = new int[tris.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            var mesh = new Mesh
            {
                name = name,
                indexFormat = tris.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.SetVertices(tris);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
