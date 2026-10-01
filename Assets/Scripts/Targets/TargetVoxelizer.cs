using SoapCarvers.Soap;
using UnityEngine;

namespace SoapCarvers.Targets
{
    /// <summary>
    /// Samples a TargetShape into a VoxelGrid with exactly the same layout as the
    /// soap grid, so scoring can compare point by point and the hologram can be
    /// meshed with the same marching cubes code.
    ///
    /// Narrow-band optimization: the grid is visited in BxBxB blocks. The SDF is
    /// evaluated once at each block's center; because an SDF never changes faster
    /// than distance (|grad| &lt;= 1), if the center is further from the surface
    /// than the block's half-diagonal plus the density band, every point in the
    /// block has the same clamped density and the per-point evaluations can be
    /// skipped. A safety factor covers the approximate (ellipsoid / smooth-union)
    /// SDFs, which can overestimate distance a little. Only blocks near the
    /// surface are sampled point by point, roughly a 10x speedup at 0.125 m voxels.
    /// </summary>
    public static class TargetVoxelizer
    {
        const int Block = 4;
        const float SafetyFactor = 1.5f;

        public static VoxelGrid Build(TargetShape shape, SoapBlock soap)
        {
            VoxelGrid soapGrid = soap.Grid;
            var grid = new VoxelGrid(soapGrid.Cells, soapGrid.VoxelSize, soapGrid.Origin);
            Vector3 center = soap.BoxCenter;
            float scale = soap.BoxHalfExtent; // normalized unit -> meters
            float vs = grid.VoxelSize;
            int n = grid.Points;

            float halfDiagonal = Mathf.Sqrt(3f) * (Block - 1) * 0.5f * vs;
            float skipDistance = halfDiagonal * SafetyFactor + VoxelGrid.MaxDensity * vs;

            for (int bz = 0; bz < n; bz += Block)
            for (int by = 0; by < n; by += Block)
            for (int bx = 0; bx < n; bx += Block)
            {
                int ex = Mathf.Min(bx + Block, n), ey = Mathf.Min(by + Block, n), ez = Mathf.Min(bz + Block, n);
                Vector3 blockCenter = (grid.PointPosition(bx, by, bz) + grid.PointPosition(ex - 1, ey - 1, ez - 1)) * 0.5f;
                float centerSdf = shape.Evaluate((blockCenter - center) / scale) * scale;
                bool uniform = Mathf.Abs(centerSdf) > skipDistance;
                float uniformDensity = centerSdf > 0f ? -VoxelGrid.MaxDensity : VoxelGrid.MaxDensity;

                for (int z = bz; z < ez; z++)
                for (int y = by; y < ey; y++)
                for (int x = bx; x < ex; x++)
                {
                    float density;
                    if (uniform)
                    {
                        density = uniformDensity;
                    }
                    else
                    {
                        Vector3 local = grid.PointPosition(x, y, z);
                        density = grid.DensityFromSdf(shape.Evaluate((local - center) / scale) * scale);
                    }
                    // Clip to the untouched block: you cannot carve soap that isn't there.
                    grid.Density[grid.Index(x, y, z)] = Mathf.Min(density, soap.UntouchedDensity(x, y, z));
                }
            }
            return grid;
        }
    }
}
