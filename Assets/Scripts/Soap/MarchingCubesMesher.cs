using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoapCarvers.Soap
{
    /// <summary>
    /// Marching cubes mesher with two shading modes:
    ///   Smooth - each vertex gets the density-field normal (negated gradient,
    ///            interpolated along the cell edge exactly like the position), so
    ///            carved curves read as soft, clean soap instead of facets.
    ///   Flat   - one face normal per triangle (faceted low-poly look).
    /// Triangles always get their own three vertices (no welding); with smooth
    /// normals that is visually identical to a welded mesh and keeps this simple.
    /// Not thread-safe: reuses internal buffers.
    /// </summary>
    public class MarchingCubesMesher
    {
        const float Iso = 0f;

        readonly List<Vector3> _vertices = new List<Vector3>(16384);
        readonly List<Vector3> _normals = new List<Vector3>(16384);
        readonly List<int> _indices = new List<int>(16384);
        readonly float[] _corner = new float[8];
        readonly Vector3[] _cornerGradient = new Vector3[8];
        readonly Vector3[] _edgeVertex = new Vector3[12];
        readonly Vector3[] _edgeNormal = new Vector3[12];

        public bool SmoothShading = true;

        /// <summary>
        /// Meshes the cells [cellMin, cellMax) of the grid into <paramref name="mesh"/>.
        /// Vertex positions are in the grid's local space.
        /// Returns the triangle count.
        /// </summary>
        public int Build(VoxelGrid grid, Vector3Int cellMin, Vector3Int cellMax, Mesh mesh)
        {
            _vertices.Clear();
            _normals.Clear();
            _indices.Clear();

            int[] tri = MarchingCubesTables.TriTable;
            int[] ox = MarchingCubesTables.CornerOffsetX;
            int[] oy = MarchingCubesTables.CornerOffsetY;
            int[] oz = MarchingCubesTables.CornerOffsetZ;
            int[] ea = MarchingCubesTables.EdgeCornerA;
            int[] eb = MarchingCubesTables.EdgeCornerB;
            float[] density = grid.Density;
            float vs = grid.VoxelSize;
            bool smooth = SmoothShading;

            for (int z = cellMin.z; z < cellMax.z; z++)
            for (int y = cellMin.y; y < cellMax.y; y++)
            for (int x = cellMin.x; x < cellMax.x; x++)
            {
                int caseIndex = 0;
                for (int c = 0; c < 8; c++)
                {
                    float d = density[grid.Index(x + ox[c], y + oy[c], z + oz[c])];
                    _corner[c] = d;
                    if (d <= Iso) caseIndex |= 1 << c; // bit set = corner outside
                }
                if (caseIndex == 0 || caseIndex == 255) continue; // fully solid / fully empty

                int row = caseIndex * 16;
                Vector3 cellOrigin = grid.PointPosition(x, y, z);

                // Lazily compute only the corner gradients / edge vertices this case uses.
                int gradientMask = 0;
                int edgeMask = 0;
                for (int t = 0; tri[row + t] != -1; t += 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int e = tri[row + t + k];
                        if ((edgeMask & (1 << e)) != 0) continue;
                        edgeMask |= 1 << e;
                        int a = ea[e], b = eb[e];
                        float da = _corner[a], db = _corner[b];
                        // Linear interpolation of the iso crossing along the edge.
                        float denom = db - da;
                        float s = Mathf.Abs(denom) < 1e-6f ? 0.5f : Mathf.Clamp01((Iso - da) / denom);
                        Vector3 pa = new Vector3(ox[a], oy[a], oz[a]);
                        Vector3 pb = new Vector3(ox[b], oy[b], oz[b]);
                        _edgeVertex[e] = cellOrigin + (pa + (pb - pa) * s) * vs;

                        if (!smooth) continue;
                        if ((gradientMask & (1 << a)) == 0)
                        {
                            gradientMask |= 1 << a;
                            _cornerGradient[a] = grid.Gradient(x + ox[a], y + oy[a], z + oz[a]);
                        }
                        if ((gradientMask & (1 << b)) == 0)
                        {
                            gradientMask |= 1 << b;
                            _cornerGradient[b] = grid.Gradient(x + ox[b], y + oy[b], z + oz[b]);
                        }
                        // Density grows into the solid, so the outward normal is -gradient.
                        _edgeNormal[e] = -Vector3.Lerp(_cornerGradient[a], _cornerGradient[b], s);
                    }

                    int e0 = tri[row + t], e1 = tri[row + t + 1], e2 = tri[row + t + 2];
                    Vector3 v0 = _edgeVertex[e0];
                    Vector3 v1 = _edgeVertex[e1];
                    Vector3 v2 = _edgeVertex[e2];
                    Vector3 face = Vector3.Cross(v1 - v0, v2 - v0);
                    float len = face.magnitude;
                    if (len < 1e-9f) continue; // degenerate sliver, skip
                    face /= len;

                    int baseIndex = _vertices.Count;
                    _vertices.Add(v0); _vertices.Add(v1); _vertices.Add(v2);
                    if (smooth)
                    {
                        _normals.Add(SafeNormal(_edgeNormal[e0], face));
                        _normals.Add(SafeNormal(_edgeNormal[e1], face));
                        _normals.Add(SafeNormal(_edgeNormal[e2], face));
                    }
                    else
                    {
                        _normals.Add(face); _normals.Add(face); _normals.Add(face);
                    }
                    _indices.Add(baseIndex); _indices.Add(baseIndex + 1); _indices.Add(baseIndex + 2);
                }
            }

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetTriangles(_indices, 0, true);
            return _indices.Count / 3;
        }

        /// <summary>Normalized gradient normal, or the face normal where the field is flat.</summary>
        static Vector3 SafeNormal(Vector3 n, Vector3 face)
        {
            float m = n.magnitude;
            return m > 1e-5f ? n / m : face;
        }
    }
}
