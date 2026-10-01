using UnityEngine;

namespace SoapCarvers.Targets
{
    /// <summary>
    /// v1 target: a swan on a low plinth, facing the player (-Z).
    /// Built from: ellipsoid body, S-shaped neck of 4 capsules, sphere head,
    /// cone beak, two flattened tilted ellipsoid wings and a cone tail.
    /// All numbers are in normalized block space ([-1, 1]^3).
    /// </summary>
    public class SwanShape : TargetShape
    {
        public override string Name => "Swan";
        public override string Description => "Graceful. Long neck. Do not hit it with dynamite.";

        // Neck spine, from chest to the top of the S-curve.
        static readonly Vector3[] Neck =
        {
            new Vector3(0f, -0.30f, -0.42f),
            new Vector3(0f, 0.00f, -0.58f),
            new Vector3(0f, 0.30f, -0.50f),
            new Vector3(0f, 0.55f, -0.40f),
            new Vector3(0f, 0.70f, -0.47f),
        };
        static readonly float[] NeckRadius = { 0.15f, 0.125f, 0.105f, 0.095f };

        static readonly Quaternion LeftWingRot = Quaternion.Euler(-12f, 0f, 18f);
        static readonly Quaternion RightWingRot = Quaternion.Euler(-12f, 0f, -18f);

        public override float Evaluate(Vector3 p)
        {
            // Body: a long, slightly low egg shape.
            float body = Sdf.Ellipsoid(p, new Vector3(0f, -0.50f, 0.08f), new Vector3(0.46f, 0.33f, 0.70f));

            // Wings: flattened ellipsoids hugging the body, tilted out and tips raised at the back.
            float wings = Mathf.Min(
                Sdf.Ellipsoid(p, new Vector3(-0.33f, -0.28f, 0.18f), new Vector3(0.13f, 0.22f, 0.55f), LeftWingRot),
                Sdf.Ellipsoid(p, new Vector3(0.33f, -0.28f, 0.18f), new Vector3(0.13f, 0.22f, 0.55f), RightWingRot));
            float d = Sdf.SmoothUnion(body, wings, 0.08f);

            // Tail: short cone pointing up and back.
            float tail = Sdf.Cone(p, new Vector3(0f, -0.38f, 0.68f), new Vector3(0f, -0.12f, 0.92f), 0.16f, 0.03f);
            d = Sdf.SmoothUnion(d, tail, 0.06f);

            // Neck: chain of capsules, smoothly blended so joints don't show.
            float neck = Sdf.Capsule(p, Neck[0], Neck[1], NeckRadius[0]);
            for (int i = 1; i < NeckRadius.Length; i++)
                neck = Sdf.SmoothUnion(neck, Sdf.Capsule(p, Neck[i], Neck[i + 1], NeckRadius[i]), 0.05f);
            d = Sdf.SmoothUnion(d, neck, 0.12f);

            // Head and beak.
            float head = Sdf.Sphere(p, new Vector3(0f, 0.73f, -0.55f), 0.13f);
            float beak = Sdf.Cone(p, new Vector3(0f, 0.71f, -0.64f), new Vector3(0f, 0.67f, -0.88f), 0.07f, 0.015f);
            d = Sdf.SmoothUnion(d, Sdf.SmoothUnion(head, beak, 0.03f), 0.05f);

            // Plinth: a low rounded slab so the sculpture stands on something.
            float plinth = Sdf.Box(p, new Vector3(0f, -0.9f, 0.05f), new Vector3(0.55f, 0.1f, 0.85f), 0.05f);
            return Sdf.SmoothUnion(d, plinth, 0.1f);
        }
    }
}
