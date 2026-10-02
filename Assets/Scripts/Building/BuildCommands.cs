using BuildCrew.Core;
using BuildCrew.Kits;

namespace BuildCrew.Building
{
    /// <summary>Put a (held) part into a slot: it gets pulled in by a soft spring joint.</summary>
    [System.Serializable]
    public class SnapCommand : GameCommand
    {
        public int SiteIndex;
        public string SlotId;
        public int PartId;
    }

    /// <summary>A snapped or fixed part leaves its slot (pulled out, blown away, joints broke).</summary>
    [System.Serializable]
    public class UnsnapCommand : GameCommand
    {
        public int SiteIndex;
        public string SlotId;
    }

    /// <summary>
    /// One fixing action on a snapped slot: a hammer hit (one nail), one screw,
    /// or one trowel of mortar. Enough of them fix the part.
    /// </summary>
    [System.Serializable]
    public class FixCommand : GameCommand
    {
        public int SiteIndex;
        public string SlotId;
        public FixMethod Method;
        /// <summary>The tool used (the trowel carries the mortar).</summary>
        public int ToolId;
    }
}
