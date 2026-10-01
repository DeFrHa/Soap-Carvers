using SoapCarvers.Soap;
using UnityEngine;

namespace SoapCarvers.Targets
{
    /// <summary>
    /// Samples a TargetShape into a VoxelGrid with exactly the same layout as the
    /// soap grid, so scoring can compare point by point and the hologram can be
    /// meshed with the same marching cubes code.
    /// </summary>
    public static class TargetVoxelizer
    {
        public static VoxelGrid Build(TargetShape shape, SoapBlock soap)
        {
            VoxelGrid soapGrid = soap.Grid;
            var grid = new VoxelGrid(soapGrid.Cells, soapGrid.VoxelSize, soapGrid.Origin);
            Vector3 center = soap.BoxCenter;
            float scale = soap.BoxHalfExtent; // normalized unit -> meters

            int n = grid.Points;
            for (int z = 0; z < n; z++)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector3 local = grid.PointPosition(x, y, z);
                Vector3 normalized = (local - center) / scale;
                float sdfMeters = shape.Evaluate(normalized) * scale;
                // Clip to the untouched block: you cannot carve soap that isn't there.
                float density = Mathf.Min(grid.DensityFromSdf(sdfMeters), soap.UntouchedDensity(x, y, z));
                grid.Density[grid.Index(x, y, z)] = density;
            }
            return grid;
        }
    }
}
