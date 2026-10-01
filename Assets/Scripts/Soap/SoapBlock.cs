using System;
using System.Collections.Generic;
using SoapCarvers.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoapCarvers.Soap
{
    /// <summary>
    /// The carvable soap block: a density grid split into chunk meshes.
    ///
    /// RULE: the soap is only ever modified through <see cref="ApplyCarve"/>.
    /// <see cref="CarveSphere"/> / <see cref="CarveCapsule"/> are convenience
    /// wrappers that build a <see cref="SoapCarveCommand"/> and call it.
    /// Every applied command is appended to <see cref="CarveLog"/>; replaying
    /// that log on a fresh block reproduces the exact same soap.
    ///
    /// The transform pivot is the center of the block's bottom face.
    /// </summary>
    [DisallowMultipleComponent]
    public class SoapBlock : MonoBehaviour
    {
        [SerializeField] GameSettings settings;
        [SerializeField] Material soapMaterial;

        /// <summary>Raised after a command changed the soap. int = number of grid points that went from solid to empty.</summary>
        public event Action<SoapCarveCommand, int> CarveApplied;
        /// <summary>Raised after the soap was reset to an untouched block.</summary>
        public event Action SoapReset;

        /// <summary>
        /// Optional gate (e.g. "is the round running?"). Return false to reject a command.
        /// A network host would validate here; clients would apply host-approved commands.
        /// </summary>
        public Func<SoapCarveCommand, bool> CarveFilter;

        public VoxelGrid Grid { get; private set; }
        public IReadOnlyList<SoapCarveCommand> CarveLog => _log;
        public Material SoapMaterial => soapMaterial;
        public GameSettings Settings => settings;
        /// <summary>Half extent (meters) of the untouched soap box.</summary>
        public float BoxHalfExtent { get; private set; }
        /// <summary>Local-space center of the untouched soap box.</summary>
        public Vector3 BoxCenter { get; private set; }

        readonly List<SoapCarveCommand> _log = new List<SoapCarveCommand>(1024);
        readonly HashSet<int> _dirtyChunks = new HashSet<int>();
        readonly MarchingCubesMesher _mesher = new MarchingCubesMesher();
        Chunk[] _chunks;
        int _chunkCells;
        int _chunksPerAxis;

        class Chunk
        {
            public Vector3Int Coord;
            public Mesh Mesh;
            public MeshCollider Collider;
            public MeshRenderer Renderer;
        }

        public void Configure(GameSettings gameSettings, Material material)
        {
            settings = gameSettings;
            soapMaterial = material;
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void EnsureInitialized()
        {
            if (Grid != null) return;
            if (settings == null) settings = GameSettings.CreateDefault();
            if (soapMaterial == null)
                soapMaterial = MaterialFactory.Lit("Soap", settings.soapColor, settings.soapSmoothness);

            int cells = settings.CellsPerAxis;
            float vs = settings.blockSize / cells;
            float half = settings.blockSize * 0.5f;
            // Shift down half a voxel so the soap's bottom face lands exactly on y = 0.
            Grid = new VoxelGrid(cells, vs, new Vector3(-half, -vs * 0.5f, -half));
            // The untouched box is half a voxel smaller than the grid on every side,
            // so the outermost layer of points is "air" and the mesh is closed.
            BoxHalfExtent = half - vs * 0.5f;
            BoxCenter = Grid.Center;

            _chunkCells = Mathf.Max(1, settings.chunkCells);
            _chunksPerAxis = Mathf.CeilToInt(cells / (float)_chunkCells);
            CreateChunks();
            FillUntouched();
            MarkAllDirty();
        }

        // ------------------------------------------------------------------ API

        public bool CarveSphere(Vector3 worldPos, float radius, int sourceId = -1, float debrisScale = 1f)
        {
            return ApplyCarve(SoapCarveCommand.Sphere(transform.InverseTransformPoint(worldPos), radius, sourceId, debrisScale));
        }

        public bool CarveCapsule(Vector3 worldA, Vector3 worldB, float radius, int sourceId = -1, float debrisScale = 1f)
        {
            return ApplyCarve(SoapCarveCommand.Capsule(
                transform.InverseTransformPoint(worldA), transform.InverseTransformPoint(worldB),
                radius, sourceId, debrisScale));
        }

        /// <summary>
        /// The one and only entry point that modifies soap. Returns true if the
        /// command passed the filter and actually changed the density grid.
        /// Commands that change nothing are not logged (they are no-ops, so the
        /// log still fully determines the state).
        /// </summary>
        public bool ApplyCarve(SoapCarveCommand cmd)
        {
            EnsureInitialized();
            if (CarveFilter != null && !CarveFilter(cmd)) return false;
            if (!Execute(cmd, out int removed)) return false;

            cmd.Sequence = _log.Count;
            _log.Add(cmd);
            CarveApplied?.Invoke(cmd, removed);
            return true;
        }

        /// <summary>Restores the untouched block and clears the log.</summary>
        public void ResetSoap()
        {
            EnsureInitialized();
            _log.Clear();
            FillUntouched();
            MarkAllDirty();
            SoapReset?.Invoke();
        }

        /// <summary>
        /// Rebuilds the soap from scratch from an ordered command list
        /// (late-joiners / resync). Bypasses the filter and raises no carve events.
        /// </summary>
        public void ReplayLog(IList<SoapCarveCommand> commands)
        {
            EnsureInitialized();
            _log.Clear();
            FillUntouched();
            for (int i = 0; i < commands.Count; i++)
            {
                SoapCarveCommand cmd = commands[i];
                if (!Execute(cmd, out _)) continue;
                cmd.Sequence = _log.Count;
                _log.Add(cmd);
            }
            MarkAllDirty();
        }

        /// <summary>Density of the untouched block at a grid point (used by scoring).</summary>
        public float UntouchedDensity(int x, int y, int z)
        {
            Vector3 p = Grid.PointPosition(x, y, z) - BoxCenter;
            // Signed distance to an axis-aligned box (negative inside).
            Vector3 q = new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)) - Vector3.one * BoxHalfExtent;
            float outside = Vector3.Max(q, Vector3.zero).magnitude;
            float inside = Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
            return Grid.DensityFromSdf(outside + inside);
        }

        public Vector3 WorldCenter => transform.TransformPoint(BoxCenter);

        // ------------------------------------------------------------- internals

        /// <summary>
        /// CSG subtraction: soap AND NOT shape. In density terms that is
        /// min(soap, -shape), and -shape's density is clamp(+sdf / voxel).
        /// </summary>
        bool Execute(SoapCarveCommand cmd, out int removedPoints)
        {
            removedPoints = 0;
            if (cmd.Radius <= 0f) return false;

            cmd.Bounds(out Vector3 min, out Vector3 max);
            Vector3 pad = Vector3.one * Grid.VoxelSize; // density ramps over one voxel
            if (!Grid.PointRange(min - pad, max + pad, out Vector3Int lo, out Vector3Int hi)) return false;

            float[] d = Grid.Density;
            bool changed = false;
            for (int z = lo.z; z <= hi.z; z++)
            for (int y = lo.y; y <= hi.y; y++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                int i = Grid.Index(x, y, z);
                float old = d[i];
                if (old <= -1f) continue; // already fully empty
                float keep = Mathf.Clamp(cmd.Sdf(Grid.PointPosition(x, y, z)) / Grid.VoxelSize, -1f, 1f);
                if (keep >= old) continue;
                d[i] = keep;
                changed = true;
                if (old > 0f && keep <= 0f) removedPoints++;
            }

            if (changed) MarkDirtyPoints(lo, hi);
            return changed;
        }

        void FillUntouched()
        {
            int n = Grid.Points;
            for (int z = 0; z < n; z++)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                Grid.Density[Grid.Index(x, y, z)] = UntouchedDensity(x, y, z);
        }

        void CreateChunks()
        {
            int c = _chunksPerAxis;
            _chunks = new Chunk[c * c * c];
            for (int z = 0; z < c; z++)
            for (int y = 0; y < c; y++)
            for (int x = 0; x < c; x++)
            {
                var go = new GameObject($"SoapChunk_{x}_{y}_{z}");
                go.transform.SetParent(transform, false);
                go.layer = gameObject.layer;
                var mesh = new Mesh { name = go.name, indexFormat = IndexFormat.UInt32 };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = soapMaterial;
                mr.shadowCastingMode = ShadowCastingMode.On;
                var mc = go.AddComponent<MeshCollider>();
                mc.enabled = false;
                go.AddComponent<SoapChunk>().Init(this, new Vector3Int(x, y, z));
                _chunks[ChunkIndex(x, y, z)] = new Chunk
                {
                    Coord = new Vector3Int(x, y, z), Mesh = mesh, Collider = mc, Renderer = mr,
                };
            }
        }

        int ChunkIndex(int x, int y, int z) => x + _chunksPerAxis * (y + _chunksPerAxis * z);

        void MarkAllDirty()
        {
            for (int i = 0; i < _chunks.Length; i++) _dirtyChunks.Add(i);
        }

        /// <summary>
        /// A chunk with cells [c*K, c*K+K) reads points [c*K, c*K+K]. A modified
        /// point p therefore affects chunk floor(p/K) and, when it sits on a chunk
        /// border, also chunk floor((p-1)/K). That is the "include neighbors" rule.
        /// </summary>
        void MarkDirtyPoints(Vector3Int lo, Vector3Int hi)
        {
            int last = _chunksPerAxis - 1;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((lo.x - 1) / (float)_chunkCells), 0, last);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((lo.y - 1) / (float)_chunkCells), 0, last);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((lo.z - 1) / (float)_chunkCells), 0, last);
            int x1 = Mathf.Clamp(hi.x / _chunkCells, 0, last);
            int y1 = Mathf.Clamp(hi.y / _chunkCells, 0, last);
            int z1 = Mathf.Clamp(hi.z / _chunkCells, 0, last);
            for (int z = z0; z <= z1; z++)
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                _dirtyChunks.Add(ChunkIndex(x, y, z));
        }

        // Rebuild dirty chunks at most once per frame, after all carves of the frame.
        void LateUpdate()
        {
            if (_dirtyChunks.Count == 0) return;
            foreach (int i in _dirtyChunks) RebuildChunk(_chunks[i]);
            _dirtyChunks.Clear();
        }

        void RebuildChunk(Chunk chunk)
        {
            int cells = Grid.Cells;
            Vector3Int min = chunk.Coord * _chunkCells;
            Vector3Int max = new Vector3Int(
                Mathf.Min(min.x + _chunkCells, cells),
                Mathf.Min(min.y + _chunkCells, cells),
                Mathf.Min(min.z + _chunkCells, cells));
            int tris = _mesher.Build(Grid, min, max, chunk.Mesh);

            chunk.Renderer.enabled = tris > 0;
            // Re-assigning forces PhysX to re-cook the collider.
            chunk.Collider.sharedMesh = null;
            if (tris > 0) chunk.Collider.sharedMesh = chunk.Mesh;
            chunk.Collider.enabled = tris > 0;
        }

        void OnDestroy()
        {
            if (_chunks == null) return;
            foreach (var c in _chunks)
                if (c?.Mesh != null) Destroy(c.Mesh);
        }

        void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            float size = settings != null ? settings.blockSize : 16f;
            Gizmos.color = new Color(1f, 0.6f, 0.8f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, size * 0.5f, 0f), Vector3.one * size);
        }
    }
}
