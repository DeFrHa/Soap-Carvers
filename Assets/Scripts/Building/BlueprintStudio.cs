using System.Collections.Generic;
using BuildCrew.Core;
using BuildCrew.Parts;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildCrew.Building
{
    public enum BlueprintView
    {
        Front = 0,
        Side = 1,
        Top = 2,
        Back = 3,
    }

    /// <summary>
    /// A hidden "photo studio" far above the world: a hologram model of the
    /// team's kit (one box per slot, on the Hologram layer) and an orthographic
    /// camera that renders it into a RenderTexture for the plan table
    /// and the briefing screen. Fixed slots show green, the current stage
    /// yellow, the rest blue.
    ///
    /// Multiplayer note: one studio per team (one plan table each).
    /// </summary>
    public class BlueprintStudio : MonoBehaviour
    {
        [SerializeField] BuildSite site;
        [SerializeField] int textureSize = 512;
        [SerializeField] Color backgroundColor = new Color(0.04f, 0.1f, 0.22f, 1f);

        readonly List<MeshRenderer> _boxes = new List<MeshRenderer>();
        Camera _camera;
        Transform _model;
        Bounds _bounds;
        int _layer;
        bool _dirty;

        public RenderTexture Texture { get; private set; }
        public BlueprintView CurrentView { get; private set; } = BlueprintView.Front;
        public BuildSite Site => site;

        public void Configure(BuildSite buildSite) => site = buildSite;

        void Awake()
        {
            _layer = Layers.Hologram;
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

            var lightGo = new GameObject("StudioLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localRotation = Quaternion.Euler(40f, -30f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 0.6f;
            l.cullingMask = 1 << _layer;
            l.shadows = LightShadows.None;

            _model = new GameObject("Model").transform;
            _model.SetParent(transform, false);
        }

        void OnEnable()
        {
            if (site != null) site.Changed += MarkDirty;
        }

        void OnDisable()
        {
            if (site != null) site.Changed -= MarkDirty;
        }

        void MarkDirty() => _dirty = true;

        void LateUpdate()
        {
            if (site == null) return;
            if (_boxes.Count != site.Slots.Count) Rebuild();
            if (_dirty) Recolor();
        }

        void Rebuild()
        {
            foreach (MeshRenderer r in _boxes) if (r != null) Destroy(r.gameObject);
            _boxes.Clear();
            Mesh cube = PrimitiveMeshes.Get(PrimitiveType.Cube);
            _bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (BuildSlot slot in site.Slots)
            {
                var go = new GameObject("Box");
                go.layer = _layer;
                go.transform.SetParent(_model, false);
                go.transform.localPosition = slot.Data.position;
                go.transform.localRotation = Quaternion.Euler(slot.Data.rotation);
                go.transform.localScale = slot.Data.size;
                go.AddComponent<MeshFilter>().sharedMesh = cube;
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                _boxes.Add(r);
                var b = new Bounds(slot.Data.position, slot.Data.size);
                if (first) { _bounds = b; first = false; }
                else _bounds.Encapsulate(b);
            }
            // Center the model on the studio pivot.
            _model.localPosition = -_bounds.center;
            Recolor();
            SetView(CurrentView);
        }

        void Recolor()
        {
            _dirty = false;
            MaterialPalette p = World.Palette;
            Material Get(string k) => p != null ? p.Get(k) : MaterialPalette.Create(k);
            Material done = Get("hologramFixed"), stage = Get("hologramStage"), rest = Get("hologram");
            int current = site.CurrentStage;
            for (int i = 0; i < _boxes.Count && i < site.Slots.Count; i++)
            {
                BuildSlot s = site.Slots[i];
                _boxes[i].sharedMaterial = s.State == SlotState.Fixed ? done : s.Stage == current ? stage : rest;
            }
        }

        /// <summary>
        /// Front looks at the building's front (-z side) toward +z; Side from +x;
        /// Top straight down with the front at the bottom; Back from +z.
        /// </summary>
        public void SetView(BlueprintView view)
        {
            CurrentView = view;
            if (_camera == null) return;
            float extent = Mathf.Max(_bounds.size.x, Mathf.Max(_bounds.size.y, _bounds.size.z), 1f);
            float dist = extent * 2f;
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
            _camera.orthographicSize = extent * 0.62f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = dist * 2f + extent;
        }

        public void CycleView() => SetView((BlueprintView)(((int)CurrentView + 1) % 4));

        void OnDestroy()
        {
            if (Texture != null) Texture.Release();
        }
    }
}
