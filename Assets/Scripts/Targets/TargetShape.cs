using UnityEngine;

namespace SoapCarvers.Targets
{
    /// <summary>
    /// A carving target defined purely in code as a signed distance function.
    ///
    /// Coordinate convention ("normalized block space"):
    ///   the untouched soap block spans [-1, 1] on every axis,
    ///   +Y is up, and -Z faces the player spawn (the "Front" view).
    /// <see cref="Evaluate"/> returns a distance in the same normalized units;
    /// the voxelizer scales it to meters.
    ///
    /// To add a shape: subclass, implement Evaluate, register it in TargetLibrary.
    /// </summary>
    public abstract class TargetShape
    {
        public abstract string Name { get; }
        public virtual string Description => string.Empty;

        /// <summary>Signed distance at normalized position p (negative = inside).</summary>
        public abstract float Evaluate(Vector3 p);
    }
}
