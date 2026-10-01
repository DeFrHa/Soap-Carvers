using SoapCarvers.Soap;
using UnityEngine;

namespace SoapCarvers.Targets
{
    [System.Serializable]
    public struct ScoreResult
    {
        /// <summary>Normalized score: 0 = untouched block, 1 = perfect.</summary>
        public float Score01;
        /// <summary>Raw intersection-over-union of soap vs target.</summary>
        public float IoU;
        /// <summary>IoU an untouched block would get (the 0% baseline).</summary>
        public float IoUUntouched;
        /// <summary>Fraction of the target volume still made of soap.</summary>
        public float TargetIntact01;
        /// <summary>Fraction of the soap that should be removed but is still there.</summary>
        public float ExcessRemaining01;
        public string Rank;
    }

    /// <summary>
    /// Compares soap and target grids sample by sample (grid points; "solid"
    /// means density &gt; 0).
    ///
    ///   S = soap points, T = target points, B = untouched block points (T ⊆ B)
    ///   IoU            = |S ∩ T| / |S ∪ T|
    ///   IoU_untouched  = |B ∩ T| / |B ∪ T| = |T| / |B|
    ///   score          = clamp01((IoU - IoU_untouched) / (1 - IoU_untouched))
    ///
    /// The normalization matters: an untouched block already overlaps the whole
    /// target, so its raw IoU is |T|/|B| (~10-20%), not 0. Rescaling maps that
    /// baseline to 0% and a perfect carve to 100%. Removing more than the target
    /// can push IoU below the baseline, which clamps to 0%.
    /// </summary>
    public static class ScoreCalculator
    {
        public static ScoreResult Compute(SoapBlock soap, VoxelGrid target)
        {
            VoxelGrid s = soap.Grid;
            int n = s.Points;
            long inter = 0, union = 0, targetCount = 0, blockCount = 0, blockAndTarget = 0;
            long excessNow = 0, excessStart = 0;

            for (int z = 0; z < n; z++)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = s.Index(x, y, z);
                bool sv = s.Density[i] > 0f;
                bool tv = target.Density[i] > 0f;
                bool bv = soap.UntouchedDensity(x, y, z) > 0f;

                if (sv && tv) inter++;
                if (sv || tv) union++;
                if (tv) targetCount++;
                if (bv) blockCount++;
                if (bv && tv) blockAndTarget++;
                if (bv && !tv) excessStart++;
                if (sv && !tv) excessNow++;
            }

            var r = new ScoreResult();
            r.IoU = union > 0 ? (float)inter / union : 0f;
            long blockUnionTarget = blockCount + targetCount - blockAndTarget;
            r.IoUUntouched = blockUnionTarget > 0 ? (float)blockAndTarget / blockUnionTarget : 0f;
            float denom = 1f - r.IoUUntouched;
            r.Score01 = denom > 1e-6f ? Mathf.Clamp01((r.IoU - r.IoUUntouched) / denom) : 0f;
            r.TargetIntact01 = targetCount > 0 ? (float)inter / targetCount : 0f;
            r.ExcessRemaining01 = excessStart > 0 ? (float)excessNow / excessStart : 0f;
            r.Rank = RankTitle(r.Score01);
            return r;
        }

        public static string RankTitle(float score01)
        {
            float pct = score01 * 100f;
            if (pct < 5f) return "Soap Dropper";
            if (pct < 15f) return "Bubble Trouble";
            if (pct < 30f) return "Soap Apprentice";
            if (pct < 45f) return "Suds Sculptor";
            if (pct < 60f) return "Foam Artisan";
            if (pct < 75f) return "Bubble Baron";
            if (pct < 90f) return "Master Carver";
            return "Legendary Lathersmith";
        }
    }
}
