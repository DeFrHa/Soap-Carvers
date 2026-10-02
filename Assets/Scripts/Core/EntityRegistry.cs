using System.Collections.Generic;
using BuildCrew.Interaction;
using UnityEngine;

namespace BuildCrew.Core
{
    /// <summary>
    /// Id -> object lookup for everything commands refer to (all grabbable
    /// rigidbodies). Scene objects get fixed ids from SceneBuilder; runtime
    /// spawns (pile parts, cut pieces) get ids from <see cref="AllocateId"/>,
    /// which only the host would call.
    /// </summary>
    public class EntityRegistry : MonoBehaviour
    {
        public const int FirstRuntimeId = 100000;

        readonly Dictionary<int, Grabbable> _entities = new Dictionary<int, Grabbable>();
        int _nextRuntimeId = FirstRuntimeId;

        public IEnumerable<Grabbable> All => _entities.Values;

        public int AllocateId() => _nextRuntimeId++;

        public void Register(Grabbable entity)
        {
            if (entity.EntityId <= 0 ||
                (_entities.TryGetValue(entity.EntityId, out Grabbable existing) && existing != entity && existing != null))
                entity.AssignId(AllocateId());
            _entities[entity.EntityId] = entity;
        }

        public void Unregister(Grabbable entity)
        {
            if (_entities.TryGetValue(entity.EntityId, out Grabbable existing) && existing == entity)
                _entities.Remove(entity.EntityId);
        }

        public Grabbable Get(int id) => _entities.TryGetValue(id, out Grabbable g) && g != null ? g : null;

        public T Get<T>(int id) where T : Grabbable => Get(id) as T;
    }
}
