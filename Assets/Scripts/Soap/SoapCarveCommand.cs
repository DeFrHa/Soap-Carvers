using UnityEngine;

namespace SoapCarvers.Soap
{
    public enum CarveShape : byte
    {
        Sphere = 0,
        Capsule = 1,
    }

    /// <summary>
    /// The single unit of soap modification. The soap state is fully determined
    /// by the ordered list of applied commands (see <see cref="SoapBlock.CarveLog"/>),
    /// so a network layer only needs to replicate these, in order.
    /// Positions are in the SoapBlock's LOCAL space so the log replays
    /// identically wherever the block sits.
    /// </summary>
    [System.Serializable]
    public struct SoapCarveCommand
    {
        /// <summary>Assigned by SoapBlock.ApplyCarve (order in the log).</summary>
        public int Sequence;
        /// <summary>Who issued it (player id, or -1 for world/dynamite).</summary>
        public int SourceId;
        public CarveShape Shape;
        public Vector3 LocalA;
        /// <summary>Second capsule point. Equal to LocalA for spheres.</summary>
        public Vector3 LocalB;
        public float Radius;
        /// <summary>Cosmetic only: scales the debris burst. Not part of soap state.</summary>
        public float DebrisScale;

        public static SoapCarveCommand Sphere(Vector3 localCenter, float radius, int sourceId = -1, float debrisScale = 1f) =>
            new SoapCarveCommand
            {
                Shape = CarveShape.Sphere, LocalA = localCenter, LocalB = localCenter,
                Radius = radius, SourceId = sourceId, DebrisScale = debrisScale,
            };

        public static SoapCarveCommand Capsule(Vector3 localA, Vector3 localB, float radius, int sourceId = -1, float debrisScale = 1f) =>
            new SoapCarveCommand
            {
                Shape = CarveShape.Capsule, LocalA = localA, LocalB = localB,
                Radius = radius, SourceId = sourceId, DebrisScale = debrisScale,
            };

        /// <summary>Signed distance (meters) from p to the carve shape. Negative inside.</summary>
        public float Sdf(Vector3 p)
        {
            if (Shape == CarveShape.Sphere) return (p - LocalA).magnitude - Radius;
            Vector3 ab = LocalB - LocalA;
            float abLenSq = ab.sqrMagnitude;
            float t = abLenSq < 1e-8f ? 0f : Mathf.Clamp01(Vector3.Dot(p - LocalA, ab) / abLenSq);
            return (p - (LocalA + ab * t)).magnitude - Radius;
        }

        public void Bounds(out Vector3 min, out Vector3 max)
        {
            Vector3 r = Vector3.one * Radius;
            min = Vector3.Min(LocalA, LocalB) - r;
            max = Vector3.Max(LocalA, LocalB) + r;
        }
    }
}
