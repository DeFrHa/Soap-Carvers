using UnityEngine;

namespace SoapCarvers.Soap
{
    /// <summary>
    /// A cubic grid of density samples stored at grid POINTS (cell corners).
    /// N cells per axis => N+1 points per axis. Density convention:
    ///   density &gt; 0  : solid
    ///   density &lt;= 0 : empty
    /// Values are clamped signed distances measured in voxel units
    /// (density = clamp(-sdf / voxelSize, -MaxDensity, MaxDensity)), which keeps
    /// marching cubes interpolation smooth while staying cheap to store and carve.
    /// The ±2 voxel band (rather than ±1) keeps the central-difference gradient
    /// meaningful at every corner of a surface cell, which smooth normals need.
    /// Coordinates are in the owner's LOCAL space.
    /// </summary>
    public class VoxelGrid
    {
        /// <summary>Densities are clamped to [-MaxDensity, MaxDensity] voxel units.</summary>
        public const float MaxDensity = 2f;

        public readonly int Cells;          // cells per axis
        public readonly int Points;         // points per axis = Cells + 1
        public readonly float VoxelSize;
        public readonly Vector3 Origin;     // local position of point (0,0,0)
        public readonly float[] Density;

        public VoxelGrid(int cells, float voxelSize, Vector3 origin)
        {
            Cells = cells;
            Points = cells + 1;
            VoxelSize = voxelSize;
            Origin = origin;
            Density = new float[Points * Points * Points];
        }

        public int Index(int x, int y, int z) => x + Points * (y + Points * z);

        public float Get(int x, int y, int z) => Density[Index(x, y, z)];

        public Vector3 PointPosition(int x, int y, int z) =>
            new Vector3(Origin.x + x * VoxelSize, Origin.y + y * VoxelSize, Origin.z + z * VoxelSize);

        /// <summary>Size of the grid in local units.</summary>
        public float Extent => Cells * VoxelSize;

        public Vector3 Center => Origin + Vector3.one * (Extent * 0.5f);

        public bool IsSolid(int index) => Density[index] > 0f;

        /// <summary>
        /// Unnormalized density gradient at a grid point (central differences,
        /// one-sided at the borders). Points INTO the solid; the outward surface
        /// normal is its negation.
        /// </summary>
        public Vector3 Gradient(int x, int y, int z)
        {
            int x0 = x > 0 ? x - 1 : x, x1 = x < Points - 1 ? x + 1 : x;
            int y0 = y > 0 ? y - 1 : y, y1 = y < Points - 1 ? y + 1 : y;
            int z0 = z > 0 ? z - 1 : z, z1 = z < Points - 1 ? z + 1 : z;
            return new Vector3(
                (Get(x1, y, z) - Get(x0, y, z)) / (x1 - x0),
                (Get(x, y1, z) - Get(x, y0, z)) / (y1 - y0),
                (Get(x, y, z1) - Get(x, y, z0)) / (z1 - z0));
        }

        public void CopyFrom(VoxelGrid other)
        {
            System.Array.Copy(other.Density, Density, Density.Length);
        }

        /// <summary>Converts a signed distance in meters to stored density.</summary>
        public float DensityFromSdf(float sdfMeters) => Mathf.Clamp(-sdfMeters / VoxelSize, -MaxDensity, MaxDensity);

        /// <summary>
        /// Clamps a local-space AABB to point index ranges. Returns false if empty.
        /// </summary>
        public bool PointRange(Vector3 min, Vector3 max, out Vector3Int lo, out Vector3Int hi)
        {
            lo = new Vector3Int(
                Mathf.Max(0, Mathf.FloorToInt((min.x - Origin.x) / VoxelSize)),
                Mathf.Max(0, Mathf.FloorToInt((min.y - Origin.y) / VoxelSize)),
                Mathf.Max(0, Mathf.FloorToInt((min.z - Origin.z) / VoxelSize)));
            hi = new Vector3Int(
                Mathf.Min(Points - 1, Mathf.CeilToInt((max.x - Origin.x) / VoxelSize)),
                Mathf.Min(Points - 1, Mathf.CeilToInt((max.y - Origin.y) / VoxelSize)),
                Mathf.Min(Points - 1, Mathf.CeilToInt((max.z - Origin.z) / VoxelSize)));
            return lo.x <= hi.x && lo.y <= hi.y && lo.z <= hi.z;
        }
    }
}
