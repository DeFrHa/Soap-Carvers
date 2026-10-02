using BuildCrew.Core;
using UnityEngine;

namespace BuildCrew.Interaction
{
    /// <summary>Grab an entity at a point (local to the entity). Tools ignore the point and use their grip.</summary>
    [System.Serializable]
    public class GrabCommand : GameCommand
    {
        public int EntityId;
        public Vector3 LocalPoint;
    }

    /// <summary>Let go of whatever the player holds; optionally throw it (impulse along Direction).</summary>
    [System.Serializable]
    public class ReleaseCommand : GameCommand
    {
        public bool Throw;
        public Vector3 Direction;
    }

    /// <summary>Release an entity everyone holds and put it at a pose (lean the ladder).</summary>
    [System.Serializable]
    public class PlaceCommand : GameCommand
    {
        public int EntityId;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
