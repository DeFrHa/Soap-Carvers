using UnityEngine;

namespace BuildCrew.Round
{
    [System.Serializable]
    public struct ScoreResult
    {
        public int FixedSlots;
        public int TotalSlots;
        public int LostParts;
        public float TimeRemaining;
        public float RoundTime;
        public float Completion01;
        public float LostPenalty01;
        public float TimeBonus01;
        /// <summary>Final score, 0..~1.2 (time bonus can push a perfect build over 100%).</summary>
        public float Score01;
        public string Rank;
        public bool Valid;
    }

    /// <summary>
    /// Score = fixed slots / all slots
    ///       - parts lost in the final test / all slots
    ///       + time bonus (share of the clock left, scaled by completion, times a weight).
    /// </summary>
    public static class ScoreCalculator
    {
        public static ScoreResult Compute(int fixedSlots, int totalSlots, int lostParts, float timeRemaining, float roundTime, float timeBonusWeight)
        {
            var r = new ScoreResult
            {
                FixedSlots = fixedSlots,
                TotalSlots = totalSlots,
                LostParts = lostParts,
                TimeRemaining = timeRemaining,
                RoundTime = roundTime,
                Valid = true,
            };
            float total = Mathf.Max(1, totalSlots);
            r.Completion01 = fixedSlots / total;
            r.LostPenalty01 = lostParts / total;
            r.TimeBonus01 = roundTime > 0f ? timeBonusWeight * Mathf.Clamp01(timeRemaining / roundTime) * r.Completion01 : 0f;
            r.Score01 = Mathf.Max(0f, r.Completion01 - r.LostPenalty01 + r.TimeBonus01);
            r.Rank = RankFor(r.Score01);
            return r;
        }

        public static string RankFor(float score01)
        {
            if (score01 >= 1f) return "LEGENDARY BUILD CREW";
            if (score01 >= 0.85f) return "Master Builders";
            if (score01 >= 0.65f) return "Solid Crew";
            if (score01 >= 0.45f) return "Cowboy Builders";
            if (score01 >= 0.25f) return "Weekend Wobblers";
            if (score01 > 0.05f) return "Pile Admirers";
            return "Professional Tea Drinkers";
        }
    }
}
