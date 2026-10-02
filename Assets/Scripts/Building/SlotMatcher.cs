using System.Collections.Generic;
using UnityEngine;

namespace BuildCrew.Building
{
    /// <summary>
    /// Decides whether a box-shaped part fits a slot, and in which orientation.
    /// A box fits a slot in any of the 24 axis-aligned rotations that map its
    /// dimensions onto the slot's (within tolerance); we pick the one closest
    /// to how the player is holding it, so a plank held upside down snaps
    /// upside down instead of spinning around.
    /// </summary>
    public static class SlotMatcher
    {
        static Quaternion[] _rotations;

        /// <summary>The 24 rotations of a cube (all combinations of 90 degree turns, deduplicated).</summary>
        public static Quaternion[] Rotations
        {
            get
            {
                if (_rotations != null) return _rotations;
                var list = new List<Quaternion>();
                for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                for (int z = 0; z < 4; z++)
                {
                    Quaternion q = Quaternion.Euler(x * 90f, y * 90f, z * 90f);
                    bool dup = false;
                    foreach (Quaternion o in list)
                        if (Quaternion.Angle(o, q) < 1f) { dup = true; break; }
                    if (!dup) list.Add(q);
                }
                _rotations = list.ToArray();
                return _rotations;
            }
        }

        /// <summary>
        /// Returns true if a part of <paramref name="partSize"/> fits a slot of
        /// <paramref name="slotSize"/>. <paramref name="bestRotation"/> is the world
        /// rotation the part should take; <paramref name="angle"/> how far (degrees)
        /// the part currently is from it.
        /// </summary>
        public static bool TryMatch(Vector3 partSize, Vector3 slotSize, Quaternion partRotation, Quaternion slotRotation,
            float tolerance, out Quaternion bestRotation, out float angle)
        {
            bestRotation = slotRotation;
            angle = float.MaxValue;
            // Part rotation expressed in the slot's frame.
            Quaternion relative = Quaternion.Inverse(slotRotation) * partRotation;
            bool found = false;
            foreach (Quaternion r in Rotations)
            {
                // Rotating an axis-aligned size by a 90-degree rotation permutes (and negates) its components.
                Vector3 d = r * partSize;
                if (Mathf.Abs(Mathf.Abs(d.x) - slotSize.x) > tolerance ||
                    Mathf.Abs(Mathf.Abs(d.y) - slotSize.y) > tolerance ||
                    Mathf.Abs(Mathf.Abs(d.z) - slotSize.z) > tolerance) continue;
                float a = Quaternion.Angle(relative, r);
                if (a < angle)
                {
                    angle = a;
                    bestRotation = slotRotation * r;
                    found = true;
                }
            }
            return found;
        }
    }
}
