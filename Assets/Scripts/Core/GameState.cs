namespace BuildCrew.Core
{
    public enum GameState
    {
        /// <summary>Target and stages shown on screen before the pile arrives.</summary>
        Briefing = 0,
        /// <summary>The pile drops in; waiting for it to settle.</summary>
        Dump = 1,
        /// <summary>Timer running: sort, prepare and assemble.</summary>
        Build = 2,
        /// <summary>Wind (and rain) hit the building; fixed camera.</summary>
        FinalTest = 3,
        /// <summary>Score screen. R restarts with a new pile.</summary>
        Results = 4,
    }
}
