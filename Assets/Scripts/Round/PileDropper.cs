using System.Collections.Generic;
using BuildCrew.Building;
using BuildCrew.Kits;
using BuildCrew.Parts;
using UnityEngine;

namespace BuildCrew.Round
{
    /// <summary>
    /// Drops the round's parts as one heap at this spot. Parts are stacked in
    /// layers a little above the ground (each layer turned 90 degrees and
    /// shifted a bit, like a badly stacked woodpile), then physics tumbles
    /// them into a tangled pile.
    /// </summary>
    public class PileDropper : MonoBehaviour
    {
        [Tooltip("Footprint of the stack (m). Must be longer than the longest part (4 m beams).")]
        [SerializeField] float footprint = 4.6f;
        [SerializeField] float startHeight = 0.3f;
        [SerializeField] float gap = 0.06f;
        [SerializeField] float layerGap = 0.12f;

        /// <summary>Time.time of the last drop, or -1.</summary>
        public float DroppedAt { get; private set; } = -1f;

        public void Clear() => DroppedAt = -1f;

        public void Drop(IList<PartSpec> specs, PartManager parts)
        {
            if (parts == null) return;
            // Big things first (bottom layers).
            var order = new List<PartSpec>(specs);
            order.Sort((a, b) => (b.Size.x * b.Size.y * b.Size.z).CompareTo(a.Size.x * a.Size.y * a.Size.z));

            float half = footprint * 0.5f;
            int layer = 0;
            float y = startHeight, layerH = 0f;
            float x = -half, z = -half, rowW = 0f;
            Quaternion layerRot = transform.rotation;
            Vector3 layerShift = Vector3.zero;

            foreach (PartSpec spec in order)
            {
                // Lying down: longest side along the row (local z), thinnest side up.
                Quaternion local = LieDown(spec.Size, out Vector3 d);
                if (z + d.z > half && z > -half)
                {
                    x += rowW + gap;
                    z = -half;
                    rowW = 0f;
                }
                if (x + d.x > half && x > -half)
                {
                    // Next layer: turned 90 degrees and nudged, so the heap isn't a neat block.
                    y += layerH + layerGap;
                    layerH = 0f;
                    x = -half;
                    z = -half;
                    rowW = 0f;
                    layer++;
                    layerRot = transform.rotation * Quaternion.Euler(0f, layer % 2 == 0 ? 0f : 90f, 0f);
                    layerShift = new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
                }
                var center = new Vector3(x + d.x * 0.5f, y + d.y * 0.5f, z + d.z * 0.5f);
                Vector3 pos = transform.position + layerRot * center + layerShift;
                parts.SpawnPilePart(spec, pos, layerRot * local);
                z += d.z + gap;
                rowW = Mathf.Max(rowW, d.x);
                layerH = Mathf.Max(layerH, d.y);
            }
            DroppedAt = Time.time;
        }

        /// <summary>Axis-aligned rotation mapping the part's largest size to z, smallest to y.</summary>
        static Quaternion LieDown(Vector3 size, out Vector3 dims)
        {
            float[] s = { size.x, size.y, size.z };
            System.Array.Sort(s);
            var want = new Vector3(s[1], s[0], s[2]);
            foreach (Quaternion r in SlotMatcher.Rotations)
            {
                Vector3 d = r * size;
                d = new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z));
                if (Mathf.Abs(d.x - want.x) < 1e-3f && Mathf.Abs(d.y - want.y) < 1e-3f && Mathf.Abs(d.z - want.z) < 1e-3f)
                {
                    dims = d;
                    return r;
                }
            }
            dims = size;
            return Quaternion.identity;
        }
    }
}
