using BuildCrew.Kits;
using UnityEngine;

namespace BuildCrew.Player
{
    /// <summary>
    /// A player's pockets: handfuls of nails and screws. Per-player state on
    /// the player object (no statics). Only command handlers change it.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        public int Nails { get; private set; }
        public int Screws { get; private set; }

        public int Count(FixMethod kind) => kind == FixMethod.Nails ? Nails : kind == FixMethod.Screws ? Screws : 0;

        public void Add(FixMethod kind, int amount)
        {
            if (kind == FixMethod.Nails) Nails = Mathf.Max(0, Nails + amount);
            else if (kind == FixMethod.Screws) Screws = Mathf.Max(0, Screws + amount);
        }

        public bool TryConsume(FixMethod kind, int amount)
        {
            if (Count(kind) < amount) return false;
            Add(kind, -amount);
            return true;
        }

        public void Clear()
        {
            Nails = 0;
            Screws = 0;
        }
    }
}
