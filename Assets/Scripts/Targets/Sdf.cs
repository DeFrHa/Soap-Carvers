using UnityEngine;

namespace SoapCarvers.Targets
{
    /// <summary>
    /// Signed distance functions (negative inside, positive outside, zero on the
    /// surface) and the boolean operators used to compose target shapes.
    /// Most formulas follow Inigo Quilez's reference SDFs
    /// (https://iquilezles.org/articles/distfunctions/).
    /// </summary>
    public static class Sdf
    {
        public static float Sphere(Vector3 p, Vector3 center, float radius) => (p - center).magnitude - radius;

        /// <summary>
        /// Ellipsoid (approximate, but a good bound). Exact ellipsoid distance needs
        /// a quartic root; this "k0 * (k0 - 1) / k1" trick divides the scaled
        /// distance by the gradient length, which is accurate near the surface,
        /// which is all voxelization needs.
        /// </summary>
        public static float Ellipsoid(Vector3 p, Vector3 center, Vector3 radii)
        {
            Vector3 q = p - center;
            float k0 = new Vector3(q.x / radii.x, q.y / radii.y, q.z / radii.z).magnitude;
            float k1 = new Vector3(q.x / (radii.x * radii.x), q.y / (radii.y * radii.y), q.z / (radii.z * radii.z)).magnitude;
            if (k1 < 1e-6f) return -Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z));
            return k0 * (k0 - 1f) / k1;
        }

        /// <summary>Ellipsoid rotated by <paramref name="rotation"/> around its center.</summary>
        public static float Ellipsoid(Vector3 p, Vector3 center, Vector3 radii, Quaternion rotation)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (p - center);
            return Ellipsoid(local, Vector3.zero, radii);
        }

        /// <summary>Capsule = distance to segment AB minus radius.</summary>
        public static float Capsule(Vector3 p, Vector3 a, Vector3 b, float radius)
        {
            Vector3 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Mathf.Max(1e-8f, Vector3.Dot(ba, ba)));
            return (pa - ba * h).magnitude - radius;
        }

        /// <summary>Axis-aligned box with half extents <paramref name="half"/>, optionally rounded.</summary>
        public static float Box(Vector3 p, Vector3 center, Vector3 half, float rounding = 0f)
        {
            Vector3 d = p - center;
            Vector3 q = new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z)) - (half - Vector3.one * rounding);
            float outside = Vector3.Max(q, Vector3.zero).magnitude;
            float inside = Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
            return outside + inside - rounding;
        }

        /// <summary>
        /// Capped cone from point a (radius ra) to point b (radius rb). With
        /// ra == rb it is a cylinder. (IQ "sdCappedCone", arbitrary orientation.)
        /// Works in the 2D (radial x, axial y) half-plane of the axis: it takes
        /// the nearer of the cap distance (ca) and the slanted side distance (cb).
        /// </summary>
        public static float Cone(Vector3 p, Vector3 a, Vector3 b, float ra, float rb)
        {
            float rba = rb - ra;
            Vector3 ba = b - a;
            float baba = Vector3.Dot(ba, ba);
            float papa = Vector3.Dot(p - a, p - a);
            float paba = Vector3.Dot(p - a, ba) / baba;          // normalized axial coordinate
            float x = Mathf.Sqrt(Mathf.Max(0f, papa - paba * paba * baba)); // radial distance
            float cax = Mathf.Max(0f, x - (paba < 0.5f ? ra : rb));
            float cay = Mathf.Abs(paba - 0.5f) - 0.5f;
            float k = rba * rba + baba;
            float f = Mathf.Clamp01((rba * (x - ra) + paba * baba) / k);
            float cbx = x - ra - f * rba;
            float cby = paba - f;
            float s = (cbx < 0f && cay < 0f) ? -1f : 1f;
            return s * Mathf.Sqrt(Mathf.Min(cax * cax + cay * cay * baba, cbx * cbx + cby * cby * baba));
        }

        public static float Cylinder(Vector3 p, Vector3 a, Vector3 b, float radius) => Cone(p, a, b, radius, radius);

        // ---- operators ----

        public static float Union(float a, float b) => Mathf.Min(a, b);

        public static float Intersect(float a, float b) => Mathf.Max(a, b);

        /// <summary>a minus b.</summary>
        public static float Subtract(float a, float b) => Mathf.Max(a, -b);

        /// <summary>
        /// Polynomial smooth minimum: blends the two surfaces within distance k,
        /// producing a fillet instead of a hard crease. k = 0 is a plain union.
        /// </summary>
        public static float SmoothUnion(float a, float b, float k)
        {
            if (k <= 0f) return Mathf.Min(a, b);
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }
    }
}
