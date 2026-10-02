using System.Collections.Generic;
using BuildCrew.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildCrew.Parts
{
    /// <summary>
    /// Pooled glass shards (and other cosmetic debris). Capped: when full, the
    /// oldest shard is recycled. Cosmetic and non-deterministic: not game state.
    /// </summary>
    public class ShardPool : MonoBehaviour
    {
        struct Shard
        {
            public Rigidbody Body;
            public BoxCollider Collider;
            public float Until;
        }

        readonly List<Shard> _active = new List<Shard>();
        readonly Stack<Shard> _free = new Stack<Shard>();
        int _created;

        public int ActiveCount => _active.Count;

        /// <summary>Spray shards over a box (the broken part's pose and size).</summary>
        public void Burst(Vector3 center, Quaternion rotation, Vector3 size, Vector3 velocity, Material material, int count)
        {
            GameSettings s = World.Settings;
            for (int i = 0; i < count; i++)
            {
                Shard shard = Take(s.maxShards, material);
                Vector3 local = new Vector3((Random.value - 0.5f) * size.x, (Random.value - 0.5f) * size.y, (Random.value - 0.5f) * size.z);
                Transform t = shard.Body.transform;
                t.SetPositionAndRotation(center + rotation * local, rotation * Quaternion.Euler(Random.insideUnitSphere * 60f));
                float a = Random.Range(0.08f, 0.25f);
                t.localScale = new Vector3(a, Random.Range(0.06f, 0.2f), Mathf.Max(0.006f, size.z));
                shard.Body.position = t.position;
                shard.Body.rotation = t.rotation;
                shard.Body.mass = 0.15f;
                shard.Body.linearVelocity = velocity + Random.insideUnitSphere * 2.5f + Vector3.up;
                shard.Body.angularVelocity = Random.insideUnitSphere * 10f;
                shard.Until = Time.time + s.shardLifetime * Random.Range(0.8f, 1.2f);
                _active.Add(shard);
            }
        }

        Shard Take(int max, Material material)
        {
            Shard shard;
            if (_free.Count > 0) shard = _free.Pop();
            else if (_created >= max && _active.Count > 0)
            {
                shard = _active[0];
                _active.RemoveAt(0);
            }
            else
            {
                var go = new GameObject("Shard");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMeshes.Get(PrimitiveType.Cube);
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                shard = new Shard
                {
                    Collider = go.AddComponent<BoxCollider>(),
                    Body = go.AddComponent<Rigidbody>(),
                };
                shard.Body.interpolation = RigidbodyInterpolation.Interpolate;
                _created++;
            }
            shard.Body.GetComponent<MeshRenderer>().sharedMaterial = material;
            shard.Body.gameObject.SetActive(true);
            shard.Body.isKinematic = false;
            return shard;
        }

        void Update()
        {
            float now = Time.time;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Until > now) continue;
                Shard s = _active[i];
                _active.RemoveAt(i);
                s.Body.gameObject.SetActive(false);
                _free.Push(s);
            }
        }

        public void Clear()
        {
            foreach (Shard s in _active)
            {
                s.Body.gameObject.SetActive(false);
                _free.Push(s);
            }
            _active.Clear();
        }
    }
}
