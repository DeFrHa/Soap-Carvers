namespace SoapCarvers.Core
{
    public enum GameState
    {
        /// <summary>Lobby: walk around, grab tools. First carve or the Start button begins the round.</summary>
        Ready = 0,
        /// <summary>Timer running, tools work.</summary>
        Carving = 1,
        /// <summary>Time is up; the scan plane sweeps the block. Tools disabled.</summary>
        Scanning = 2,
        /// <summary>Score screen. R restarts.</summary>
        Results = 3,
    }
}
