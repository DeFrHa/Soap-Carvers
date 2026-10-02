namespace BuildCrew.Core
{
    /// <summary>
    /// Base class for every state change request. Concrete commands are plain
    /// serializable data (ids, numbers, vectors), never object references, so
    /// they can be sent over a network unchanged. PlayerId is -1 for commands
    /// raised by the simulation itself (glass breaking, joints snapping).
    /// </summary>
    [System.Serializable]
    public abstract class GameCommand
    {
        public int PlayerId = -1;
        /// <summary>Set by the CommandBus when executed (log only).</summary>
        public float Time;
    }
}
