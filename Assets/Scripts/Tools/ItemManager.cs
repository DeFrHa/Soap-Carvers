using System.Collections.Generic;
using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// Authority for who holds which item. Every pick up / drop / throw / place
    /// goes through <see cref="Execute"/> as an <see cref="ItemCommand"/>
    /// and is logged. Items and holders register themselves by id.
    /// </summary>
    public class ItemManager : MonoBehaviour
    {
        // Collections are initialized inline so items may register before our Awake.
        readonly Dictionary<int, Holdable> _items = new Dictionary<int, Holdable>();
        readonly Dictionary<int, IItemHolder> _holders = new Dictionary<int, IItemHolder>();
        readonly List<ItemCommand> _log = new List<ItemCommand>(256);
        int _nextDynamicId = 1000;

        public IReadOnlyList<ItemCommand> Log => _log;

        public void Register(Holdable item)
        {
            if (item.ItemId <= 0 || (_items.TryGetValue(item.ItemId, out Holdable existing) && existing != item))
                item.AssignId(_nextDynamicId++);
            _items[item.ItemId] = item;
        }

        public void Unregister(Holdable item)
        {
            if (_items.TryGetValue(item.ItemId, out Holdable existing) && existing == item)
                _items.Remove(item.ItemId);
            if (item.Holder != null && item.Holder.HeldItem == item) item.Holder.SetHeldItem(null);
        }

        public void RegisterHolder(IItemHolder holder) => _holders[holder.PlayerId] = holder;

        public void UnregisterHolder(IItemHolder holder)
        {
            if (_holders.TryGetValue(holder.PlayerId, out IItemHolder h) && h == holder) _holders.Remove(holder.PlayerId);
        }

        public bool Execute(ItemCommand cmd)
        {
            if (!_items.TryGetValue(cmd.ItemId, out Holdable item) || item == null) return false;
            if (!_holders.TryGetValue(cmd.PlayerId, out IItemHolder holder)) return false;

            switch (cmd.Type)
            {
                case ItemCommandType.PickUp:
                    if (item.IsHeld || !item.CanPickUp) return false;
                    if (holder.HeldItem != null) Release(holder, holder.Velocity);
                    holder.SetHeldItem(item);
                    item.OnAttached(holder);
                    break;

                case ItemCommandType.Drop:
                case ItemCommandType.Throw:
                    if (item.Holder != holder) return false;
                    Release(holder, cmd.Velocity);
                    break;

                case ItemCommandType.Place:
                    if (item.Holder != holder || !item.CanBePlaced) return false;
                    Release(holder, Vector3.zero);
                    item.PlaceAt(cmd.Position, cmd.Rotation);
                    break;

                default:
                    return false;
            }

            _log.Add(cmd);
            return true;
        }

        void Release(IItemHolder holder, Vector3 velocity)
        {
            Holdable item = holder.HeldItem;
            holder.SetHeldItem(null);
            if (item != null) item.OnReleased(velocity);
        }

        /// <summary>Drops an item from whoever holds it (e.g. dynamite exploding in hand).</summary>
        public void ForceRelease(Holdable item)
        {
            if (item.Holder != null && item.Holder.HeldItem == item) Release(item.Holder, item.Holder.Velocity);
        }

        /// <summary>Round restart: empty all hands, remove spawned items, put the rest back home.</summary>
        public void ResetAll()
        {
            foreach (IItemHolder h in _holders.Values)
                if (h.HeldItem != null) Release(h, Vector3.zero);

            var items = new List<Holdable>(_items.Values);
            foreach (Holdable item in items)
            {
                if (item == null) continue;
                if (item.SpawnedAtRuntime) Destroy(item.gameObject);
                else item.ResetToHome();
            }
            _log.Clear();
        }
    }
}
