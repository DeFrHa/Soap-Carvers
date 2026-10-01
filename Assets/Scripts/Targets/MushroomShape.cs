using UnityEngine;

namespace SoapCarvers.Targets
{
    /// <summary>
    /// Easy test target: cylinder stem + half-sphere cap.
    /// Normalized block space ([-1, 1]^3), +Y up.
    /// </summary>
    public class MushroomShape : TargetShape
    {
        public override string Name => "Mushroom";
        public override string Description => "Easy mode. It's a mushroom.";

        public override float Evaluate(Vector3 p)
        {
            float stem = Sdf.Cylinder(p, new Vector3(0f, -1.1f, 0f), new Vector3(0f, 0.15f, 0f), 0.3f);

            // Half sphere = sphere intersected with the half-space above y = capBase.
            // The half-space "y >= capBase" has SDF (capBase - y).
            const float capBase = 0.0f;
            float cap = Sdf.Intersect(Sdf.Sphere(p, new Vector3(0f, capBase, 0f), 0.88f), capBase - p.y);

            return Sdf.SmoothUnion(stem, cap, 0.08f);
        }
    }
}
