using UnityEngine;

namespace SoapCarvers.Soap
{
    /// <summary>
    /// A cubic grid of density samples stored at grid POINTS (cell corners).
    /// N cells per axis => N+1 points per axis. Density convention:
    ///   density &gt; 0  : solid
    ///   density &lt;= 0 : empty
    /// Values are clamped signed distances measured in voxel units
    /// (density = clamp(-sdf / voxelSize, -1, 1)), which keeps marching cubes
    /// interpolation smooth while staying cheap to store and to carve.
    /// Coordinates are in the owner's LOCAL space.
    /// </summary>
    public class VoxelGrid
    {
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

        public void CopyFrom(VoxelGrid other)
        {
            System.Array.Copy(other.Density, Density, Density.Length);
        }

        /// <summary>Converts a signed distance in meters to stored density.</summary>
        public float DensityFromSdf(float sdfMeters) => Mathf.Clamp(-sdfMeters / VoxelSize, -1f, 1f);

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
