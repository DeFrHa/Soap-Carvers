using BuildCrew.Core;
using UnityEngine;

namespace BuildCrew.Parts
{
    /// <summary>
    /// Split a loose part in two across one of its local axes. Offset is the
    /// cut position along that axis, measured from the part's center (m).
    /// </summary>
    [System.Serializable]
    public class CutCommand : GameCommand
    {
        public int PartId;
        /// <summary>0 = local x (length), 1 = local y (pane height).</summary>
        public int Axis;
        public float Offset;
    }

    /// <summary>Shatter a fragile part (impact, or a rushed glass cutter).</summary>
    [System.Serializable]
    public class BreakCommand : GameCommand
    {
        public int PartId;
        public Vector3 Point;
    }

    /// <summary>Take a handful of nails/screws from a box into the player's pocket.</summary>
    [System.Serializable]
    public class TakeFixingsCommand : GameCommand
    {
        public int BoxId;
    }
}
