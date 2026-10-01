using System.Collections.Generic;
using SoapCarvers.Core;
using UnityEngine;

namespace SoapCarvers.Soap
{
    /// <summary>
    /// Cosmetic soap chips that tumble out of every carve. Purely visual: NOT part
    /// of the deterministic soap state, so each client may simulate its own.
    /// Pooled; when the cap is reached the oldest chip is recycled.
    /// </summary>
    [RequireComponent(typeof(SoapBlock))]
    public class SoapDebrisPool : MonoBehaviour
    {
        [SerializeField] int maxActive = 150;
        [SerializeField] float lifetime = 8f;
        [SerializeField] float shrinkSeconds = 1.2f;
        [Tooltip("Removed grid points per spawned chip (lower = more chips).")]
        [SerializeField] float pointsPerChip = 6f;
        [SerializeField] int maxChipsPerCarve = 10;

        SoapBlock _block;
        Transform _poolRoot;
        Mesh _cubeMesh;
        readonly Queue<Chip> _free = new Queue<Chip>();
        readonly LinkedList<Chip> _active = new LinkedList<Chip>();

        class Chip
        {
            public GameObject Go;
            public Rigidbody Body;
            public float SpawnTime;
            public Vector3 BaseScale;
            public LinkedListNode<Chip> Node;
        }

        void Awake()
        {
            _block = GetComponent<SoapBlock>();
            GameSettings s = _block.Settings;
            if (s != null)
            {
                maxActive = s.maxDebris;
                lifetime = s.debrisLifetime;
            }
            _poolRoot = new GameObject("SoapDebrisPool").transform;
            // Grab Unity's built-in cube mesh once.
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
        }

        void OnEnable()
        {
            if (_block == null) _block = GetComponent<SoapBlock>();
            _block.CarveApplied += OnCarveApplied;
            _block.SoapReset += ClearAll;
        }

        void OnDisable()
        {
            _block.CarveApplied -= OnCarveApplied;
            _block.SoapReset -= ClearAll;
        }

        void OnDestroy()
        {
            if (_poolRoot != null) Destroy(_poolRoot.gameObject);
        }

        void OnCarveApplied(SoapCarveCommand cmd, int removedPoints)
        {
            if (removedPoints <= 0 || cmd.DebrisScale <= 0f) return;
            int count = Mathf.Clamp(Mathf.CeilToInt(removedPoints / pointsPerChip * cmd.DebrisScale), 1,
                Mathf.CeilToInt(maxChipsPerCarve * Mathf.Max(1f, cmd.DebrisScale)));
            Vector3 worldCenter = transform.TransformPoint((cmd.LocalA + cmd.LocalB) * 0.5f);
            for (int i = 0; i < count; i++)
            {
                Vector3 along = transform.TransformPoint(Vector3.Lerp(cmd.LocalA, cmd.LocalB, Random.value));
                Vector3 pos = along + Random.insideUnitSphere * cmd.Radius * 0.8f;
                Vector3 outward = (pos - worldCenter).normalized + Vector3.up * 0.6f;
                Vector3 vel = (outward.normalized + Random.insideUnitSphere * 0.5f) * Random.Range(1.5f, 4f) * Mathf.Sqrt(cmd.DebrisScale);
                Spawn(pos, vel, Random.Range(0.1f, 0.28f) * Mathf.Lerp(1f, 1.8f, Mathf.Clamp01(cmd.Radius / 3f)));
            }
        }

        void Spawn(Vector3 pos, Vector3 velocity, float size)
        {
            Chip chip;
            if (_free.Count > 0) chip = _free.Dequeue();
            else if (_active.Count + _free.Count < maxActive) chip = CreateChip();
            else
            {
                chip = _active.First.Value; // recycle oldest
                _active.RemoveFirst();
            }

            chip.Go.transform.SetPositionAndRotation(pos, Random.rotation);
            // Slightly squashed random boxes read as "chips" rather than dice.
            chip.BaseScale = new Vector3(size, size * Random.Range(0.5f, 1f), size * Random.Range(0.6f, 1.2f));
            chip.Go.transform.localScale = chip.BaseScale;
            chip.Go.SetActive(true);
            chip.Body.linearVelocity = velocity;
            chip.Body.angularVelocity = Random.insideUnitSphere * 10f;
            chip.SpawnTime = Time.time;
            chip.Node = _active.AddLast(chip);
        }

        Chip CreateChip()
        {
            var go = new GameObject("SoapChip");
            go.transform.SetParent(_poolRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = _cubeMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _block.SoapMaterial;
            go.AddComponent<BoxCollider>();
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            go.SetActive(false);
            return new Chip { Go = go, Body = rb };
        }

        void Update()
        {
            float now = Time.time;
            var node = _active.First;
            while (node != null)
            {
                var next = node.Next;
                Chip chip = node.Value;
                float age = now - chip.SpawnTime;
                if (age >= lifetime)
                {
                    Release(chip);
                }
                else if (age > lifetime - shrinkSeconds)
                {
                    // "Fade" by shrinking: avoids transparent material juggling.
                    float k = (lifetime - age) / shrinkSeconds;
                    chip.Go.transform.localScale = chip.BaseScale * k;
                }
                node = next;
            }
        }

        void Release(Chip chip)
        {
            if (chip.Node != null && chip.Node.List != null) _active.Remove(chip.Node);
            chip.Node = null;
            chip.Go.SetActive(false);
            _free.Enqueue(chip);
        }

        void ClearAll()
        {
            while (_active.First != null) Release(_active.First.Value);
        }

        public int ActiveCount => _active.Count;
    }
}
