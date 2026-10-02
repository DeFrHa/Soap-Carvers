using UnityEngine;

namespace BuildCrew.Core
{
    public static class PhysicsUtil
    {
        static readonly RaycastHit[] Hits = new RaycastHit[64];

        /// <summary>
        /// Raycast that ignores colliders under <paramref name="ignoreRoot"/>
        /// (e.g. the player's own CharacterController) and under
        /// <paramref name="ignoreOther"/> (e.g. the tool in hand), returning the nearest hit.
        /// </summary>
        public static bool Raycast(Vector3 origin, Vector3 direction, float distance, Transform ignoreRoot,
            QueryTriggerInteraction triggers, out RaycastHit nearest, Transform ignoreOther = null)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, distance, ~0, triggers);
            nearest = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = Hits[i];
                Transform t = h.collider.transform;
                if (ignoreRoot != null && t.IsChildOf(ignoreRoot)) continue;
                if (ignoreOther != null && t.IsChildOf(ignoreOther)) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    nearest = h;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Area (m^2) of a box of local size <paramref name="size"/> projected onto a
        /// plane perpendicular to <paramref name="worldDir"/>: each face pair
        /// contributes its area times |cos| of the angle to the direction.
        /// </summary>
        public static float ProjectedBoxArea(Vector3 size, Quaternion rotation, Vector3 worldDir)
        {
            Vector3 d = Quaternion.Inverse(rotation) * worldDir.normalized;
            return Mathf.Abs(d.x) * size.y * size.z + Mathf.Abs(d.y) * size.x * size.z + Mathf.Abs(d.z) * size.x * size.y;
        }
    }
}
