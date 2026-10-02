using BuildCrew.Core;

namespace BuildCrew.Interaction
{
    public enum WeightClass
    {
        /// <summary>Flies around in your hands.</summary>
        Light = 0,
        /// <summary>One player can lift it, slowly; long ones sag.</summary>
        Medium = 1,
        /// <summary>Heavier than one player's grab strength: drag it or lift it as a team.</summary>
        Heavy = 2,
    }

    public static class WeightClasses
    {
        public static WeightClass Of(float mass, GameSettings s)
        {
            if (mass <= s.lightMaxMass) return WeightClass.Light;
            return mass <= s.mediumMaxMass ? WeightClass.Medium : WeightClass.Heavy;
        }

        public static string Label(WeightClass w)
        {
            switch (w)
            {
                case WeightClass.Light: return "light";
                case WeightClass.Medium: return "medium";
                default: return "HEAVY";
            }
        }
    }
}
