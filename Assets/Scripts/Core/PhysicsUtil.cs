using UnityEngine;

namespace SoapCarvers.Core
{
    public static class PhysicsUtil
    {
        static readonly RaycastHit[] Hits = new RaycastHit[32];

        /// <summary>
        /// Raycast that ignores colliders under <paramref name="ignoreRoot"/>
        /// (e.g. the player's own CharacterController) and returns the nearest hit.
        /// </summary>
        public static bool Raycast(Vector3 origin, Vector3 direction, float distance, Transform ignoreRoot,
            QueryTriggerInteraction triggers, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, distance, ~0, triggers);
            nearest = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = Hits[i];
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    nearest = h;
                    found = true;
                }
            }
            return found;
        }
    }
}
