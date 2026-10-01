using SoapCarvers.Core;
using SoapCarvers.Soap;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoapCarvers.Targets
{
    public enum BlueprintView
    {
        Front = 0,
        Side = 1,
        Top = 2,
        Back = 3,
    }

    /// <summary>
    /// A hidden "photo studio" far away from the play area: a hologram mesh of
    /// the target (meshed with the same marching cubes code as the soap) on its
    /// own layer, plus an orthographic camera rendering it into a RenderTexture
    /// that blueprint tablets display.
    ///
    /// Multiplayer note: one studio is shared, so all tablets show the same view.
    /// For per-player views give each tablet its own camera/RenderTexture.
    /// </summary>
    public class BlueprintStudio : MonoBehaviour
    {
        [SerializeField] TargetManager targets;
        [SerializeField] Material hologramMaterial;
        [SerializeField] Material outlineMaterial;
        [SerializeField] int textureSize = 512;
        [SerializeField] Color backgroundColor = new Color(0.04f, 0.1f, 0.22f, 1f);

        public RenderTexture Texture { get; private set; }
        public BlueprintView CurrentView { get; private set; } = BlueprintView.Front;
        public string ShapeName => targets != null ? targets.Shape.Name : "?";

        Camera _camera;
        Transform _hologramRoot;
        MeshFilter _hologramFilter;
        Mesh _hologramMesh;
        float _extent = 16f;
        int _layer;

        public void Configure(TargetManager targetManager, Material hologram, Material outline)
        {
            targets = targetManager;
            hologramMaterial = hologram;
            outlineMaterial = outline;
        }

        void Awake()
        {
            _layer = Layers.Hologram;
            if (targets == null) targets = FindFirstObjectByType<TargetManager>();
            if (hologramMaterial == null)
                hologramMaterial = MaterialFactory.Emissive("Hologram", new Color(0.3f, 0.85f, 1f), new Color(0.1f, 0.45f, 0.6f));
            if (outlineMaterial == null)
                outlineMaterial = MaterialFactory.Unlit("HologramOutline", new Color(0.4f, 0.7f, 1f));

            Texture = new RenderTexture(textureSize, textureSize, 24) { name = "BlueprintRT", antiAliasing = 2 };
            Texture.Create();

            var camGo = new GameObject("BlueprintCamera");
            camGo.transform.SetParent(transform, false);
            camGo.layer = _layer;
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = backgroundColor;
            _camera.cullingMask = 1 << _layer;
            _camera.targetTexture = Texture;
            _camera.depth = -50;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;

            _hologramRoot = new GameObject("Hologram").transform;
            _hologramRoot.SetParent(transform, false);
            _hologramRoot.gameObject.layer = _layer;
            _hologramFilter = _hologramRoot.gameObject.AddComponent<MeshFilter>();
            var mr = _hologramRoot.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = hologramMaterial;
            // The studio floats high above the world; never let it cast shadows there.
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void OnEnable()
        {
            if (targets != null) targets.TargetChanged += Rebuild;
        }

        void OnDisable()
        {
            if (targets != null) targets.TargetChanged -= Rebuild;
        }

        void Start()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            if (targets == null || targets.Grid == null) return;
            VoxelGrid grid = targets.Grid;
            _extent = grid.Extent;

            if (_hologramMesh == null) _hologramMesh = new Mesh { name = "HologramMesh" };
            new MarchingCubesMesher().Build(grid, Vector3Int.zero, Vector3Int.one * grid.Cells, _hologramMesh);
            _hologramFilter.sharedMesh = _hologramMesh;
            // Center the hologram on the studio pivot.
            _hologramRoot.localPosition = -grid.Center;

            BuildOutline(grid);
            SetView(CurrentView);
        }

        /// <summary>Thin boxes along the 12 edges of the block, for scale.</summary>
        void BuildOutline(VoxelGrid grid)
        {
            Transform old = transform.Find("Outline");
            if (old != null) Destroy(old.gameObject);
            var root = new GameObject("Outline").transform;
            root.SetParent(transform, false);

            float h = _extent * 0.5f;
            float t = _extent * 0.006f;
            for (int axis = 0; axis < 3; axis++)
            for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                Vector3 pos, scale;
                if (axis == 0) { pos = new Vector3(0, a * h, b * h); scale = new Vector3(_extent, t, t); }
                else if (axis == 1) { pos = new Vector3(a * h, 0, b * h); scale = new Vector3(t, _extent, t); }
                else { pos = new Vector3(a * h, b * h, 0); scale = new Vector3(t, t, _extent); }
                var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(edge.GetComponent<Collider>());
                edge.name = "Edge";
                edge.layer = _layer;
                edge.transform.SetParent(root, false);
                edge.transform.localPosition = pos;
                edge.transform.localScale = scale;
                var r = edge.GetComponent<MeshRenderer>();
                r.sharedMaterial = outlineMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// Front looks from the player spawn side (-Z) toward +Z; Side from +X;
        /// Top straight down with the front at the bottom of the image; Back from +Z.
        /// </summary>
        public void SetView(BlueprintView view)
        {
            CurrentView = view;
            if (_camera == null) return;
            float dist = _extent * 1.5f;
            Vector3 pos;
            Quaternion rot;
            switch (view)
            {
                case BlueprintView.Side:
                    pos = new Vector3(dist, 0, 0); rot = Quaternion.LookRotation(Vector3.left, Vector3.up); break;
                case BlueprintView.Top:
                    pos = new Vector3(0, dist, 0); rot = Quaternion.LookRotation(Vector3.down, Vector3.forward); break;
                case BlueprintView.Back:
                    pos = new Vector3(0, 0, dist); rot = Quaternion.LookRotation(Vector3.back, Vector3.up); break;
                default:
                    pos = new Vector3(0, 0, -dist); rot = Quaternion.LookRotation(Vector3.forward, Vector3.up); break;
            }
            _camera.transform.localPosition = pos;
            _camera.transform.localRotation = rot;
            _camera.orthographicSize = _extent * 0.6f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = dist * 2f + _extent;
        }

        void OnDestroy()
        {
            if (Texture != null) Texture.Release();
            if (_hologramMesh != null) Destroy(_hologramMesh);
        }
    }
}
