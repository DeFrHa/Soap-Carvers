namespace BuildCrew.Core
{
    public enum GameState
    {
        /// <summary>Target shown on the tablet/HUD while the truck drives in.</summary>
        Briefing = 0,
        /// <summary>The truck tips its bed; waiting for the pile to settle.</summary>
        Dump = 1,
        /// <summary>Timer running: sort, prepare and assemble.</summary>
        Build = 2,
        /// <summary>Wind (and rain) hit the building; fixed camera.</summary>
        FinalTest = 3,
        /// <summary>Score screen. R restarts with a new pile.</summary>
        Results = 4,
    }
}
