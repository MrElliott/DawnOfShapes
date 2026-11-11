using UnityEngine;

namespace DawnOfShapes.Map
{
    /// <summary>
    /// Procedural flat-shaded meshes used for placeholder art.
    /// </summary>
    public static class LowPolyMeshes
    {
        /// <summary>
        /// Square pyramid, base centred at y = -0.5 and apex at y = 0.5 so it lines up with Unity's cube primitive.
        /// </summary>
        public static Mesh CreatePyramid()
        {
            Vector3 a = new Vector3(-0.5f, -0.5f, -0.5f);
            Vector3 b = new Vector3(0.5f, -0.5f, -0.5f);
            Vector3 c = new Vector3(0.5f, -0.5f, 0.5f);
            Vector3 d = new Vector3(-0.5f, -0.5f, 0.5f);
            Vector3 apex = new Vector3(0f, 0.5f, 0f);

            Vector3[] verts =
            {
                a, apex, b,
                b, apex, c,
                c, apex, d,
                d, apex, a,
                a, b, c,
                a, c, d
            };
            int[] tris = new int[verts.Length];
            for (int i = 0; i < tris.Length; i++) tris[i] = i;

            var mesh = new Mesh { name = "Pyramid", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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

            int triCount = cells * cells * 2;
            var verts = new Vector3[triCount * 3];
            var tris = new int[triCount * 3];
            int v = 0;
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    Vector3 p00 = new Vector3(-half + x * cell, heights[x, z], -half + z * cell);
                    Vector3 p10 = new Vector3(-half + (x + 1) * cell, heights[x + 1, z], -half + z * cell);
                    Vector3 p01 = new Vector3(-half + x * cell, heights[x, z + 1], -half + (z + 1) * cell);
                    Vector3 p11 = new Vector3(-half + (x + 1) * cell, heights[x + 1, z + 1], -half + (z + 1) * cell);

                    // Alternate the diagonal for a less regular look.
                    if (((x + z) & 1) == 0)
                    {
                        AddTri(verts, tris, ref v, p00, p01, p11);
                        AddTri(verts, tris, ref v, p00, p11, p10);
                    }
                    else
                    {
                        AddTri(verts, tris, ref v, p00, p01, p10);
                        AddTri(verts, tris, ref v, p10, p01, p11);
                    }
                }
            }

            var mesh = new Mesh
            {
                name = "LowPolyGround",
                indexFormat = verts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
                vertices = verts,
                triangles = tris
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTri(Vector3[] verts, int[] tris, ref int v, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            verts[v] = p0; tris[v] = v; v++;
            verts[v] = p1; tris[v] = v; v++;
            verts[v] = p2; tris[v] = v; v++;
        }
    }
}
