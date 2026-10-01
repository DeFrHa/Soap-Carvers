using UnityEngine;

namespace SoapCarvers.Tools
{
    public enum ItemCommandType : byte
    {
        PickUp = 0,
        Drop = 1,
        Throw = 2,
        PlaceLadder = 3,
    }

    /// <summary>
    /// A request to change item ownership/placement. Players never touch item
    /// state directly; they send these to <see cref="ItemManager.Execute"/>.
    /// A network layer would send them to the host instead.
    /// </summary>
    [System.Serializable]
    public struct ItemCommand
    {
        public ItemCommandType Type;
        public int PlayerId;
        public int ItemId;
        /// <summary>PlaceLadder: base position (world).</summary>
        public Vector3 Position;
        /// <summary>PlaceLadder: rotation (world).</summary>
        public Quaternion Rotation;
        /// <summary>Drop/Throw: initial velocity of the released item.</summary>
        public Vector3 Velocity;
    }
}
