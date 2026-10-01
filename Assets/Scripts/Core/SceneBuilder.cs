using System;
using SoapCarvers.Player;
using SoapCarvers.Soap;
using SoapCarvers.Targets;
using SoapCarvers.Tools;
using SoapCarvers.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SoapCarvers.Core
{
    /// <summary>
    /// Builds the complete playable scene from Unity primitives, wiring every
    /// reference in code. Used both by the editor menu
    /// (Soap Carvers/Create Playable Scene, which saves the result) and by
    /// <see cref="GameBootstrap"/> at runtime.
    ///
    /// Everything is built under an INACTIVE temporary root and detached at the
    /// end, in dependency order, so at runtime each component's Awake runs only
    /// after all its references have been assigned.
    ///
    /// World layout (meters): the soap block sits on the origin (bottom face at
    /// y = 0), the player spawns at z = -14 facing +Z, and the workbench is to
    /// the front-left at z = -12.
    /// </summary>
    public class SceneBuilder
    {
        /// <summary>
        /// Optional hook that saves a generated asset (material, settings) under
        /// the given path relative to Assets/, returning the persisted object.
        /// Null at runtime.
        /// </summary>
        readonly Func<Object, string, Object> _persist;

        Transform _root;
        GameSettings _settings;
        Font _font;

        // Shared materials
        Material _grass, _wood, _darkWood, _metal, _red, _orange, _yellow, _navy, _black;
        Material _trunk, _soapMat, _spark, _flash, _buttonRed, _scanMat, _hologram, _hologramOutline, _screen;
        Material[] _leaves;

        // Built objects that others need
        GameManager _game;
        ItemManager _items;
        TargetManager _targets;
        SoapBlock _soap;
        ScanEffect _scan;
        BlueprintStudio _studio;
        Transform _spawn;
        PlayerActions _player;
        int _nextItemId = 1;

        static readonly Vector3 BenchCenter = new Vector3(-5.5f, 0f, -12f);
        const float BenchTop = 0.95f;

        public SceneBuilder(Func<Object, string, Object> persist = null)
        {
            _persist = persist;
        }

        // ================================================================ entry

        public void Build()
        {
            var rootGo = new GameObject("__SoapCarversBuild");
            rootGo.SetActive(false);
            _root = rootGo.transform;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _settings = Persist(GameSettings.CreateDefault(), "Settings/GameSettings.asset");
            _settings.name = "GameSettings";
            CreateMaterials();

            Transform managers = BuildManagers();
            Transform soap = BuildSoap();
            Transform environment = BuildEnvironment();
            Transform studio = BuildStudio();
            Transform scan = BuildScanEffect();
            _game.Configure(_settings, _soap, _targets, _scan, _items, _spawn);
            Transform bench = BuildWorkbenchAndTools();
            Transform player = BuildPlayer();
            Transform hud = BuildHud();
            Transform eventSystem = BuildEventSystem();

            // Detach in dependency order (managers before the things that look them up).
            Transform[] order = { managers, soap, studio, scan, environment, bench, player, hud, eventSystem };
            foreach (Transform t in order) t.SetParent(null, true);
            while (_root.childCount > 0) _root.GetChild(0).SetParent(null, true);
            Kill(rootGo);
        }

        // ============================================================ materials

        T Persist<T>(T asset, string relativePath) where T : Object
        {
            return _persist != null ? (T)_persist(asset, relativePath) : asset;
        }

        Material Mat(string name, Color c, float smoothness = 0.15f, float metallic = 0f) =>
            Persist(MaterialFactory.Lit(name, c, smoothness, metallic), $"Materials/{name}.mat");

        void CreateMaterials()
        {
            _grass = Mat("Grass", new Color(0.45f, 0.74f, 0.34f), 0.05f);
            _wood = Mat("Wood", new Color(0.72f, 0.5f, 0.3f));
            _darkWood = Mat("DarkWood", new Color(0.45f, 0.29f, 0.18f));
            _metal = Mat("Metal", new Color(0.78f, 0.8f, 0.84f), 0.7f, 0.6f);
            _red = Mat("Red", new Color(0.9f, 0.22f, 0.2f), 0.3f);
            _orange = Mat("Orange", new Color(1f, 0.55f, 0.12f), 0.35f);
            _yellow = Mat("Yellow", new Color(1f, 0.84f, 0.25f), 0.2f);
            _navy = Mat("Navy", new Color(0.13f, 0.17f, 0.32f), 0.4f);
            _black = Mat("Black", new Color(0.08f, 0.08f, 0.09f), 0.3f);
            _trunk = Mat("TreeTrunk", new Color(0.5f, 0.33f, 0.2f));
            _leaves = new[]
            {
                Mat("LeavesGreen", new Color(0.32f, 0.66f, 0.3f)),
                Mat("LeavesLime", new Color(0.55f, 0.8f, 0.3f)),
                Mat("LeavesPink", new Color(0.98f, 0.62f, 0.75f)),
                Mat("LeavesTeal", new Color(0.3f, 0.7f, 0.6f)),
            };
            _soapMat = Mat("Soap", _settings.soapColor, _settings.soapSmoothness);
            _spark = Persist(MaterialFactory.Emissive("Spark", new Color(1f, 0.9f, 0.4f), new Color(3f, 2.2f, 0.6f)), "Materials/Spark.mat");
            _flash = Persist(MaterialFactory.Emissive("ExplosionFlash", new Color(1f, 0.6f, 0.2f), new Color(4f, 2f, 0.5f)), "Materials/ExplosionFlash.mat");
            _buttonRed = Persist(MaterialFactory.Emissive("ButtonRed", new Color(1f, 0.15f, 0.12f), new Color(0.6f, 0.05f, 0.03f)), "Materials/ButtonRed.mat");
            _scanMat = Persist(MaterialFactory.UnlitTransparent("ScanPlane", new Color(0.3f, 1f, 0.95f, 0.35f)), "Materials/ScanPlane.mat");
            _hologram = Persist(MaterialFactory.Emissive("Hologram", new Color(0.35f, 0.85f, 1f), new Color(0.1f, 0.45f, 0.65f), 0.5f), "Materials/Hologram.mat");
            _hologramOutline = Persist(MaterialFactory.Unlit("HologramOutline", new Color(0.45f, 0.75f, 1f)), "Materials/HologramOutline.mat");
            _screen = Persist(MaterialFactory.UnlitTexture("TabletScreen", null), "Materials/TabletScreen.mat");
        }

        // ============================================================== helpers

        static void Kill(Object o)
        {
            // Builder runs in edit mode too, where Destroy is not allowed.
            Object.DestroyImmediate(o);
        }

        GameObject Node(string name, Transform parent, Vector3 localPos, Vector3? localEuler = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler ?? Vector3.zero);
            return go;
        }

        GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
            Material mat, bool collider = true, Vector3? localEuler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler ?? Vector3.zero);
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Kill(go.GetComponent<Collider>());
            return go;
        }

        // ============================================================= managers

        Transform BuildManagers()
        {
            GameObject go = Node("GameManager", _root, Vector3.zero);
            _items = go.AddComponent<ItemManager>();
            _targets = go.AddComponent<TargetManager>();
            _game = go.AddComponent<GameManager>();
            return go.transform;
        }

        // ================================================================= soap

        Transform BuildSoap()
        {
            GameObject go = Node("SoapBlock", _root, Vector3.zero);
            _soap = go.AddComponent<SoapBlock>();
            _soap.Configure(_settings, _soapMat);
            go.AddComponent<SoapDebrisPool>();
            _targets.Configure(_settings, _soap);
            return go.transform;
        }

        // ========================================================== environment

        Transform BuildEnvironment()
        {
            GameObject env = Node("Environment", _root, Vector3.zero);

            // Sun with soft shadows.
            GameObject sunGo = Node("Sun", env.transform, new Vector3(0f, 30f, 0f), new Vector3(50f, -35f, 0f));
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;

            // Sky: procedural skybox + gradient ambient + a bit of fog for depth.
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader) { name = "Sky" };
                sky.SetColor("_SkyTint", new Color(0.45f, 0.65f, 1f));
                sky.SetColor("_GroundColor", new Color(0.55f, 0.7f, 0.5f));
                sky.SetFloat("_AtmosphereThickness", 0.75f);
                sky.SetFloat("_Exposure", 1.25f);
                sky.SetFloat("_SunSize", 0.05f);
                RenderSettings.skybox = Persist(sky, "Materials/Sky.mat");
            }
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.82f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.75f, 0.78f, 0.7f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.48f, 0.34f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.87f, 0.97f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 260f;

            // Ground: Unity's plane is 10x10 m, so scale 30 = 300 m.
            Prim(PrimitiveType.Plane, "Ground", env.transform, Vector3.zero, new Vector3(30f, 1f, 30f), _grass);

            // Low-poly trees in a ring around the play area (seeded: same every build).
            var rng = new System.Random(1234);
            Transform trees = Node("Trees", env.transform, Vector3.zero).transform;
            for (int i = 0; i < 22; i++)
            {
                float angle = (float)(i / 22.0 * Math.PI * 2 + rng.NextDouble() * 0.2);
                float dist = 32f + (float)rng.NextDouble() * 45f;
                var pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                float h = 3f + (float)rng.NextDouble() * 4f;
                Transform tree = Node("Tree", trees, pos, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f)).transform;
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.6f, h * 0.5f, 0.6f), _trunk);
                float crown = 2.5f + (float)rng.NextDouble() * 2.5f;
                Prim(PrimitiveType.Sphere, "Crown", tree, new Vector3(0f, h + crown * 0.3f, 0f),
                    new Vector3(crown, crown * 0.85f, crown), _leaves[rng.Next(_leaves.Length)]);
            }

            // Player spawn: a few meters in front of the block's -Z face, facing it.
            _spawn = Node("PlayerSpawn", env.transform, new Vector3(0f, 0.05f, -14f)).transform;
            return env.transform;
        }

        // ======================================================= studio + scan

        Transform BuildStudio()
        {
            // Far above the world (and shadow-less) so nothing can see or touch it.
            GameObject go = Node("BlueprintStudio", _root, new Vector3(0f, 1500f, 0f));
            _studio = go.AddComponent<BlueprintStudio>();
            _studio.Configure(_targets, _hologram, _hologramOutline);
            return go.transform;
        }

        Transform BuildScanEffect()
        {
            GameObject go = Node("ScanEffect", _root, Vector3.zero);
            GameObject plane = Prim(PrimitiveType.Cube, "ScanPlane", go.transform, Vector3.zero, Vector3.one, _scanMat, false);
            plane.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            GameObject lightGo = Node("ScanLight", go.transform, Vector3.zero);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.35f, 1f, 0.95f);
            l.range = _settings.blockSize * 1.4f;
            l.intensity = 6f;
            _scan = go.AddComponent<ScanEffect>();
            _scan.Configure(_soap, plane.transform, l);
            return go.transform;
        }

        // ===================================================== workbench & tools

        Transform BuildWorkbenchAndTools()
        {
            GameObject bench = Node("Workbench", _root, BenchCenter);
            Transform b = bench.transform;
            const float w = 3.2f, d = 1.1f;
            Prim(PrimitiveType.Cube, "Top", b, new Vector3(0f, BenchTop - 0.04f, 0f), new Vector3(w, 0.08f, d), _wood);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Prim(PrimitiveType.Cube, "Leg", b, new Vector3(sx * (w * 0.5f - 0.1f), (BenchTop - 0.08f) * 0.5f, sz * (d * 0.5f - 0.1f)),
                    new Vector3(0.1f, BenchTop - 0.08f, 0.1f), _darkWood);
            Prim(PrimitiveType.Cube, "Shelf", b, new Vector3(0f, 0.25f, 0f), new Vector3(w - 0.2f, 0.05f, d - 0.2f), _darkWood);

            // Start button on a little pedestal at the right end.
            GameObject pedestal = Prim(PrimitiveType.Cube, "StartButton", b, new Vector3(1.38f, BenchTop + 0.06f, -0.3f),
                new Vector3(0.24f, 0.12f, 0.24f), _black);
            // Unscaled holder so the cap isn't squashed by the pedestal scale.
            GameObject buttonRoot = Node("StartButtonRoot", b, pedestal.transform.localPosition);
            pedestal.transform.SetParent(buttonRoot.transform, true);
            GameObject cap = Prim(PrimitiveType.Cylinder, "Cap", buttonRoot.transform, new Vector3(0f, 0.08f, 0f),
                new Vector3(0.16f, 0.025f, 0.16f), _buttonRed);
            buttonRoot.AddComponent<StartButton>().Configure(_game, cap.transform);

            float y = BenchTop + 0.06f;
            Vector3 world(float x, float z) => BenchCenter + new Vector3(x, y, z);

            BuildKnife(world(-1.35f, -0.1f), Quaternion.Euler(0f, 10f, 90f));
            BuildPickaxe(world(-0.8f, -0.45f), Quaternion.Euler(0f, 90f, 90f));
            BuildChainsaw(world(-0.15f, -0.4f) + Vector3.up * 0.07f, Quaternion.identity);
            BuildDynamite(world(0.45f, -0.1f), Quaternion.Euler(0f, 30f, 90f), false);
            BuildTablet(world(0.95f, 0.1f) + Vector3.down * 0.03f, Quaternion.Euler(90f, 0f, 0f));

            // Dynamite respawner: clones an inactive template stick.
            Dynamite template = BuildDynamite(BenchCenter + new Vector3(0.45f, -5f, 0f), Quaternion.identity, true);
            template.gameObject.SetActive(false);
            template.transform.SetParent(b, true);
            GameObject spawnPoint = Node("DynamiteSpawnPoint", b, new Vector3(0.45f, BenchTop + 0.1f, 0.2f));
            bench.AddComponent<DynamiteSpawner>().Configure(template, spawnPoint.transform,
                _settings.dynamiteRespawnSeconds, _settings.dynamiteMaxLying);

            // Ladder standing next to the bench, leaning slightly toward the block.
            BuildLadder(BenchCenter + new Vector3(-2.6f, 0f, 0.6f), Quaternion.Euler(6f, 0f, 0f));
            return b;
        }

        /// <summary>Common item root: rigidbody + Holdable/Tool subclass + "Model" child.</summary>
        T ItemRoot<T>(string name, Vector3 pos, Quaternion rot, float mass, out Transform model) where T : Holdable
        {
            GameObject go = Node(name, _root, Vector3.zero);
            go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            model = Node("Model", go.transform, Vector3.zero).transform;
            return go.AddComponent<T>();
        }

        void BuildKnife(Vector3 pos, Quaternion rot)
        {
            var knife = ItemRoot<CarvingKnife>("CarvingKnife", pos, rot, 0.4f, out Transform m);
            Prim(PrimitiveType.Cube, "Handle", m, Vector3.zero, new Vector3(0.04f, 0.04f, 0.14f), _darkWood);
            Prim(PrimitiveType.Cube, "Guard", m, new Vector3(0f, 0f, 0.075f), new Vector3(0.07f, 0.05f, 0.012f), _metal);
            Prim(PrimitiveType.Cube, "Blade", m, new Vector3(0f, 0.004f, 0.19f), new Vector3(0.01f, 0.04f, 0.22f), _metal);
            knife.Configure(_nextItemId++, "Carving Knife", new Vector3(0.28f, -0.27f, 0.45f), new Vector3(5f, -5f, 0f));
            knife.SetModel(m);
        }

        void BuildPickaxe(Vector3 pos, Quaternion rot)
        {
            var pick = ItemRoot<Pickaxe>("Pickaxe", pos, rot, 2.5f, out Transform m);
            Prim(PrimitiveType.Cube, "Handle", m, new Vector3(0f, 0.35f, 0f), new Vector3(0.05f, 0.9f, 0.05f), _wood);
            Prim(PrimitiveType.Cube, "Head", m, new Vector3(0f, 0.78f, 0f), new Vector3(0.07f, 0.09f, 0.5f), _metal);
            Prim(PrimitiveType.Cube, "TipFront", m, new Vector3(0f, 0.76f, 0.3f), new Vector3(0.05f, 0.05f, 0.14f), _metal, true, new Vector3(-15f, 0f, 0f));
            Prim(PrimitiveType.Cube, "TipBack", m, new Vector3(0f, 0.76f, -0.3f), new Vector3(0.05f, 0.05f, 0.14f), _metal, true, new Vector3(15f, 0f, 0f));
            pick.Configure(_nextItemId++, "Pickaxe", new Vector3(0.38f, -0.55f, 0.55f), new Vector3(15f, 0f, 0f));
            pick.SetModel(m);
        }

        void BuildChainsaw(Vector3 pos, Quaternion rot)
        {
            var saw = ItemRoot<Chainsaw>("Chainsaw", pos, rot, 4f, out Transform m);
            Prim(PrimitiveType.Cube, "Body", m, Vector3.zero, new Vector3(0.17f, 0.2f, 0.36f), _orange);
            Prim(PrimitiveType.Cube, "Handle", m, new Vector3(0f, 0.15f, -0.02f), new Vector3(0.035f, 0.05f, 0.24f), _black);
            Prim(PrimitiveType.Cube, "Bar", m, new Vector3(0f, -0.03f, 0.6f), new Vector3(0.025f, 0.1f, 0.84f), _metal);
            Prim(PrimitiveType.Cube, "Chain", m, new Vector3(0f, -0.03f, 0.6f), new Vector3(0.018f, 0.12f, 0.86f), _black, false);
            Transform start = Node("BladeStart", m, new Vector3(0f, -0.03f, 0.25f)).transform;
            Transform end = Node("BladeEnd", m, new Vector3(0f, -0.03f, 1.0f)).transform;
            saw.Configure(_nextItemId++, "Chainsaw", new Vector3(0.3f, -0.42f, 0.4f), new Vector3(-3f, -4f, 0f));
            saw.SetParts(m, start, end);
        }

        Dynamite BuildDynamite(Vector3 pos, Quaternion rot, bool isTemplate)
        {
            var stick = ItemRoot<Dynamite>(isTemplate ? "DynamiteTemplate" : "Dynamite", pos, rot, 0.5f, out Transform m);
            Prim(PrimitiveType.Cylinder, "Stick", m, Vector3.zero, new Vector3(0.07f, 0.12f, 0.07f), _red);
            Prim(PrimitiveType.Cylinder, "Band", m, new Vector3(0f, 0.06f, 0f), new Vector3(0.074f, 0.012f, 0.074f), _black, false);
            Prim(PrimitiveType.Cylinder, "Fuse", m, new Vector3(0f, 0.15f, 0f), new Vector3(0.01f, 0.035f, 0.01f), _black, false);
            GameObject spark = Prim(PrimitiveType.Sphere, "Spark", m, new Vector3(0f, 0.19f, 0f), Vector3.one * 0.07f, _spark, false);
            spark.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var light = spark.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.3f);
            light.range = 2.5f;
            light.intensity = 2f;
            stick.Configure(isTemplate ? 0 : _nextItemId++, "Dynamite", new Vector3(0.3f, -0.3f, 0.5f), new Vector3(-25f, 0f, 0f));
            stick.SetParts(spark, light, _flash);
            return stick;
        }

        void BuildTablet(Vector3 pos, Quaternion rot)
        {
            var tablet = ItemRoot<BlueprintTablet>("BlueprintTablet", pos, rot, 0.8f, out Transform m);
            Prim(PrimitiveType.Cube, "Body", m, Vector3.zero, new Vector3(0.5f, 0.36f, 0.03f), _navy);
            // Quad faces -Z: the side that looks at the camera when held.
            GameObject screen = Prim(PrimitiveType.Quad, "Screen", m, new Vector3(0f, 0f, -0.0165f), new Vector3(0.46f, 0.32f, 1f), _screen, false);
            screen.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // World-space canvas overlay: 1 canvas unit = 1 mm.
            GameObject canvasGo = Node("ScreenCanvas", m, new Vector3(0f, 0f, -0.018f));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Adding a Canvas swaps Transform for RectTransform; re-apply the pose to be safe.
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(460f, 320f);
            rt.localPosition = new Vector3(0f, 0f, -0.018f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * 0.001f;
            // Rasterize glyphs at 3x so the small world-space text isn't blurry up close.
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;

            Text title = MakeText(canvasGo.transform, "Title", 24, TextAnchor.UpperLeft, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -36f), new Vector2(-16f, 0f), new Vector2(8f, 0f));
            Text timer = MakeText(canvasGo.transform, "Timer", 24, TextAnchor.UpperRight, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -36f), new Vector2(-16f, 0f), new Vector2(-8f, 0f));
            Text view = MakeText(canvasGo.transform, "Views", 18, TextAnchor.LowerCenter, FontStyle.Normal,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 30f), new Vector2(0f, 0f), new Vector2(0f, 4f));

            tablet.Configure(_nextItemId++, "Blueprint Tablet", new Vector3(0.3f, -0.33f, 0.5f), new Vector3(35f, -18f, 0f));
            tablet.SetParts(_studio, screen.GetComponent<MeshRenderer>(), title, timer, view);
        }

        void BuildLadder(Vector3 pos, Quaternion rot)
        {
            const float length = 12f;
            var ladder = ItemRoot<Ladder>("Ladder", pos, rot, 10f, out Transform m);
            ladder.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.Discrete;
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, "Rail", m, new Vector3(s * 0.28f, length * 0.5f, 0f), new Vector3(0.07f, length, 0.07f), _yellow);
            for (float h = 0.3f; h < length - 0.1f; h += 0.35f)
                Prim(PrimitiveType.Cube, "Rung", m, new Vector3(0f, h, 0f), new Vector3(0.56f, 0.045f, 0.045f), _wood);
            // Carried awkwardly: mostly horizontal, sticking out ahead, low on the right.
            ladder.Configure(_nextItemId++, "Ladder", new Vector3(0.45f, -0.75f, 0.2f), new Vector3(72f, 6f, 0f));
            ladder.SetDimensions(length, 16f, true);
        }

        // =============================================================== player

        Transform BuildPlayer()
        {
            GameObject go = Node("Player", _root, Vector3.zero);
            go.transform.SetPositionAndRotation(_spawn.position, _spawn.rotation);
            go.tag = "Player";
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;

            go.AddComponent<PlayerInputHandler>();
            go.AddComponent<PlayerMotor>();
            var look = go.AddComponent<PlayerLook>();
            _player = go.AddComponent<PlayerActions>();

            Transform pivot = Node("CameraPivot", go.transform, new Vector3(0f, 1.6f, 0f)).transform;
            GameObject camGo = Node("Main Camera", pivot, Vector3.zero);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;
            cam.cullingMask = ~(1 << Layers.Hologram);
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var shake = camGo.AddComponent<CameraShake>();
            Transform hold = Node("HoldPoint", camGo.transform, Vector3.zero).transform;

            look.Configure(pivot, _settings.mouseSensitivity);
            _player.Configure(1, camGo.transform, hold, shake, _items, _game);
            return go.transform;
        }

        // ================================================================== HUD

        Text MakeText(Transform parent, string name, int size, TextAnchor align, FontStyle style,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivotOffset, Vector2 sizeDelta, Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, anchorMin.y > 0.5f ? 1f : (anchorMax.y < 0.5f ? 0f : 0.5f));
            // pivotOffset.y = height of the line box; sizeDelta.x = horizontal shrink.
            rt.sizeDelta = new Vector2(sizeDelta.x, Mathf.Abs(pivotOffset.y));
            rt.anchoredPosition = anchoredPos;
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        /// <summary>Text anchored to a point on the screen (anchor == pivot).</summary>
        Text ScreenText(Transform parent, string name, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 boxSize,
            FontStyle style = FontStyle.Normal)
        {
            Text t = MakeText(parent, name, size, align, style, anchor, anchor, Vector2.zero, Vector2.zero, Vector2.zero);
            var rt = t.rectTransform;
            rt.pivot = anchor;
            rt.sizeDelta = boxSize;
            rt.anchoredPosition = pos;
            return t;
        }

        Transform BuildHud()
        {
            GameObject canvasGo = Node("HUD", _root, Vector3.zero);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform c = canvasGo.transform;
            var hud = canvasGo.AddComponent<HudController>();
            hud.game = _game;
            hud.player = _player;

            // Crosshair
            var cross = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            cross.transform.SetParent(c, false);
            var crt = (RectTransform)cross.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(6f, 6f);
            var img = cross.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.85f);
            img.raycastTarget = false;
            hud.crosshair = cross;

            hud.timerText = ScreenText(c, "Timer", 60, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(400f, 80f), FontStyle.Bold);
            hud.statusText = ScreenText(c, "Status", 26, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1400f, 90f));
            hud.promptText = ScreenText(c, "Prompt", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(900f, 50f), FontStyle.Bold);
            hud.heldText = ScreenText(c, "Held", 30, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-30f, 60f), new Vector2(700f, 40f), FontStyle.Bold);
            hud.heldHintText = ScreenText(c, "HeldHint", 20, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-30f, 25f), new Vector2(900f, 30f));
            hud.controlsText = ScreenText(c, "Controls", 18, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(25f, 20f), new Vector2(700f, 140f));
            hud.controlsText.text =
                "WASD move  Shift sprint  Space jump\n" +
                "E pick up / use  Q drop  G throw  LMB use tool\n" +
                "RMB/Tab raise tablet  1-4 tablet views\n" +
                "Ladder: walk into it + look up/down  Esc free mouse";
            hud.controlsText.color = new Color(1f, 1f, 1f, 0.75f);

            // Results panel
            var panel = new GameObject("ResultsPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(c, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.12f, 0.72f);
            Transform p = panel.transform;
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Text heading = ScreenText(p, "Heading", 34, TextAnchor.MiddleCenter, mid, new Vector2(0f, 250f), new Vector2(1200f, 50f));
            heading.text = "SCAN COMPLETE - YOUR RANK:";
            hud.rankText = ScreenText(p, "Rank", 84, TextAnchor.MiddleCenter, mid, new Vector2(0f, 160f), new Vector2(1600f, 110f), FontStyle.Bold);
            hud.rankText.color = new Color(1f, 0.85f, 0.35f);
            hud.scoreText = ScreenText(p, "Score", 56, TextAnchor.MiddleCenter, mid, new Vector2(0f, 50f), new Vector2(1200f, 80f), FontStyle.Bold);
            hud.detailsText = ScreenText(p, "Details", 28, TextAnchor.UpperCenter, mid, new Vector2(0f, -10f), new Vector2(1200f, 140f));

            var btnGo = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(p, false);
            var brt = (RectTransform)btnGo.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = mid;
            brt.sizeDelta = new Vector2(420f, 80f);
            brt.anchoredPosition = new Vector2(0f, -220f);
            btnGo.GetComponent<Image>().color = new Color(1f, 0.55f, 0.75f);
            Text btnText = ScreenText(btnGo.transform, "Label", 34, TextAnchor.MiddleCenter, mid, Vector2.zero, new Vector2(420f, 80f), FontStyle.Bold);
            btnText.text = "Carve again  (R)";
            hud.restartButton = btnGo.GetComponent<Button>();
            hud.resultsPanel = panel;
            panel.SetActive(false);
            return c;
        }

        Transform BuildEventSystem()
        {
            GameObject go = Node("EventSystem", _root, Vector3.zero);
            go.AddComponent<EventSystem>();
            // Runtime AddComponent doesn't run Reset(), so assign the default UI actions explicitly.
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return go.transform;
        }
    }
}
