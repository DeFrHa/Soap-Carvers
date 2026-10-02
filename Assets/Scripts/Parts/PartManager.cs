using System.Collections.Generic;
using BuildCrew.Core;
using BuildCrew.Kits;
using UnityEngine;

namespace BuildCrew.Parts
{
    /// <summary>
    /// Owns all parts: spawning (pile, cut pieces), the Cut and Break command
    /// handlers, impact reports from fragile parts, the two-person-saw hook
    /// and pile-settling checks.
    /// </summary>
    public class PartManager : MonoBehaviour
    {
        [SerializeField] MaterialPalette palette;
        [SerializeField] ShardPool shards;

        readonly List<Part> _parts = new List<Part>(256);
        readonly Dictionary<int, Dictionary<int, float>> _sawing = new Dictionary<int, Dictionary<int, float>>();
        readonly List<Part> _pile = new List<Part>(256);

        public IReadOnlyList<Part> Parts => _parts;
        public MaterialPalette Palette => palette;

        public void Configure(MaterialPalette materialPalette, ShardPool shardPool)
        {
            palette = materialPalette;
            shards = shardPool;
        }

        void Awake()
        {
            if (palette == null) palette = GetComponent<MaterialPalette>();
            if (shards == null) shards = FindFirstObjectByType<ShardPool>();
            CommandBus bus = GetComponent<CommandBus>();
            if (bus == null) bus = World.Bus;
            if (bus == null) return;
            bus.Register<CutCommand>(HandleCut);
            bus.Register<BreakCommand>(HandleBreak);
        }

        public void Register(Part part)
        {
            if (!_parts.Contains(part)) _parts.Add(part);
        }

        public void Unregister(Part part)
        {
            _parts.Remove(part);
            _pile.Remove(part);
        }

        // ============================================================ spawning

        /// <summary>Host only: spawns a part with a fresh entity id.</summary>
        public Part Spawn(PartSpec spec, Vector3 position, Quaternion rotation)
        {
            int id = World.Registry != null ? World.Registry.AllocateId() : 0;
            return PartFactory.Create(spec, position, rotation, id, palette);
        }

        /// <summary>Spawns a pile part (tracked for the settle check).</summary>
        public Part SpawnPilePart(PartSpec spec, Vector3 position, Quaternion rotation)
        {
            Part p = Spawn(spec, position, rotation);
            if (p != null) _pile.Add(p);
            return p;
        }

        /// <summary>Round restart: every runtime part goes away.</summary>
        public void DespawnAll()
        {
            foreach (Part p in new List<Part>(_parts))
                if (p != null && p.SpawnedAtRuntime) Destroy(p.gameObject);
            _parts.RemoveAll(p => p == null || p.SpawnedAtRuntime);
            _pile.Clear();
            _sawing.Clear();
            if (shards != null) shards.Clear();
        }

        /// <summary>Fraction (0-1) of pile parts that are at rest (asleep or slower than <paramref name="speed"/>).</summary>
        public float PileSettledFraction(float speed)
        {
            if (_pile.Count == 0) return 1f;
            int settled = 0, total = 0;
            foreach (Part p in _pile)
            {
                if (p == null) continue;
                total++;
                if (p.Body.IsSleeping() || p.Body.linearVelocity.sqrMagnitude < speed * speed) settled++;
            }
            return total == 0 ? 1f : settled / (float)total;
        }

        // =============================================================== cut

        bool HandleCut(CutCommand cmd)
        {
            Part part = World.Registry != null ? World.Registry.Get<Part>(cmd.PartId) : null;
            if (part == null || part.InBuilding || part.Definition == null || part.Definition.CutTool == CutTool.None) return false;
            if (cmd.Axis < 0 || cmd.Axis > 1) return false;
            Vector3 size = part.Size;
            float len = size[cmd.Axis];
            float min = World.Settings.minPieceLength;
            float a = len * 0.5f + cmd.Offset;  // piece on the negative side
            float b = len * 0.5f - cmd.Offset;  // piece on the positive side
            if (a < min || b < min) return false;

            Vector3 axis = Vector3.zero;
            axis[cmd.Axis] = 1f;
            Vector3 sizeA = size, sizeB = size;
            sizeA[cmd.Axis] = a;
            sizeB[cmd.Axis] = b;
            Vector3 centerA = part.transform.TransformPoint(axis * (-len * 0.5f + a * 0.5f));
            Vector3 centerB = part.transform.TransformPoint(axis * (len * 0.5f - b * 0.5f));
            Quaternion rot = part.transform.rotation;
            Vector3 vA = part.Body.GetPointVelocity(centerA), vB = part.Body.GetPointVelocity(centerB);
            PileRole role = part.Role;
            string type = part.TypeId;

            Remove(part);
            Part pa = Spawn(new PartSpec(type, sizeA, role), centerA, rot);
            Part pb = Spawn(new PartSpec(type, sizeB, role), centerB, rot);
            if (pa != null) pa.Body.linearVelocity = vA;
            if (pb != null) pb.Body.linearVelocity = vB;
            return true;
        }

        // ============================================================= break

        bool HandleBreak(BreakCommand cmd)
        {
            Part part = World.Registry != null ? World.Registry.Get<Part>(cmd.PartId) : null;
            if (part == null || part.Definition == null || !part.Definition.Fragile) return false;
            if (shards != null)
            {
                int count = Mathf.Clamp(Mathf.RoundToInt(part.Size.x * part.Size.y * 14f), 5, 16);
                Material glass = palette != null ? palette.Get("glass") : MaterialPalette.Create("glass");
                shards.Burst(part.transform.position, part.transform.rotation, part.Size, part.Body.linearVelocity, glass, count);
            }
            Remove(part);
            return true;
        }

        /// <summary>Takes a part out of the world: everyone lets go, the building lets go, then it's destroyed.</summary>
        void Remove(Part part)
        {
            if (World.Grabs != null) World.Grabs.ReleaseAll(part);
            if (World.Build != null) World.Build.DetachFromBuilding(part);
            _parts.Remove(part);
            _pile.Remove(part);
            Destroy(part.gameObject);
        }

        /// <summary>Glass hit something hard enough? Only armed once building starts (the truck ride is bubble-wrapped).</summary>
        public void ReportImpact(Part part, Collision collision)
        {
            GameManager game = World.Game;
            if (game == null || (game.State != GameState.Build && game.State != GameState.FinalTest)) return;
            if (Time.time - part.SpawnTime < 0.5f) return;
            GameSettings s = World.Settings;
            float speed = collision.relativeVelocity.magnitude;
            float deltaV = collision.impulse.magnitude / Mathf.Max(0.1f, part.Body.mass);
            if (speed < s.glassBreakSpeed || deltaV < s.glassBreakDeltaV) return;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : part.transform.position;
            CommandBus bus = World.Bus;
            if (bus != null) bus.Execute(new BreakCommand { PartId = part.EntityId, Point = point });
        }

        // ================================================= two-person saw hook

        /// <summary>A saw is working on a part this frame.</summary>
        public void ReportSawing(int partId, int playerId)
        {
            if (!_sawing.TryGetValue(partId, out Dictionary<int, float> players))
                _sawing[partId] = players = new Dictionary<int, float>();
            players[playerId] = Time.time;
        }

        /// <summary>How many different players sawed this part in the last half second.</summary>
        public int SawyersOn(int partId)
        {
            if (!_sawing.TryGetValue(partId, out Dictionary<int, float> players)) return 0;
            int n = 0;
            foreach (float t in players.Values)
                if (Time.time - t < 0.5f) n++;
            return n;
        }
    }
}
