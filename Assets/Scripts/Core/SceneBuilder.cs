using System;
using System.Collections.Generic;
using BuildCrew.Building;
using BuildCrew.Interaction;
using BuildCrew.Parts;
using BuildCrew.Player;
using BuildCrew.Round;
using BuildCrew.Sorting;
using BuildCrew.Tools;
using BuildCrew.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BuildCrew.Core
{
    /// <summary>
    /// Builds the complete playable scene from Unity primitives, wiring every
    /// reference in code. Used by the editor menu (Build Crew/Create Playable
    /// Scene, which saves the result) and by <see cref="GameBootstrap"/> at runtime.
    ///
    /// Everything is built under an INACTIVE temporary root and detached at the
    /// end, managers first, so at runtime each component's Awake runs after its
    /// references were assigned and the managers exist.
    ///
    /// Layout of team area 0 (meters, world): the truck parks at z = 13 with its
    /// tail toward the pile at z ~ 6; pallets line both sides between pile and
    /// site; the build site is at z = -11 (front facing the pile), tool rack to
    /// its left, mortar station to its right; the player spawns at z = -1
    /// facing the arriving truck. More teams would get their own area offset in x.
    /// </summary>
    public class SceneBuilder
    {
        /// <summary>
        /// Optional hook that saves a generated asset (material, settings) under
        /// the given path relative to Assets/, returning the persisted object.
        /// Null at runtime.
        /// </summary>
        readonly Func<Object, string, Object> _persist;
        readonly KitChoice _kit;

        Transform _root;
        GameSettings _settings;
        Font _font;
        MaterialPalette _palette;
        Material _screen, _sky;
        readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        GameManager _game;
        PlayerActions _player;
        readonly List<TeamArea> _areas = new List<TeamArea>();
        readonly List<Transform> _detachOrder = new List<Transform>();
        int _nextId = 1;

        public SceneBuilder(Func<Object, string, Object> persist = null, KitChoice kit = KitChoice.GardenShed)
        {
            _persist = persist;
            _kit = kit;
        }

        // ================================================================ entry

        public void Build()
        {
            var rootGo = new GameObject("__BuildCrewBuild");
            rootGo.SetActive(false);
            _root = rootGo.transform;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _settings = Persist(GameSettings.CreateDefault(), "Settings/GameSettings.asset");
            _settings.name = "GameSettings";

            Transform managers = BuildManagers();
            CreateMaterials();
            _detachOrder.Add(managers);
            _detachOrder.Add(BuildEnvironment());

            TeamArea area = BuildTeamArea(0, Vector3.zero);
            _areas.Add(area);

            _game.Configure(_settings, _kit, _areas);
            _detachOrder.Add(BuildPlayer(area));
            _detachOrder.Add(BuildHud());
            _detachOrder.Add(BuildEventSystem());

            // Detach in dependency order (managers before the things that look them up).
            foreach (Transform t in _detachOrder) t.SetParent(null, true);
            while (_root.childCount > 0) _root.GetChild(0).SetParent(null, true);
            Kill(rootGo);
        }

        // ============================================================ materials

        T Persist<T>(T asset, string relativePath) where T : Object
        {
            return _persist != null ? (T)_persist(asset, relativePath) : asset;
        }

        void CreateMaterials()
        {
            var entries = new List<MaterialPalette.Entry>();
            foreach (MaterialPalette.Spec spec in MaterialPalette.Defaults)
            {
                Material m = Persist(MaterialPalette.Create(spec), $"Materials/{spec.Key}.mat");
                _mats[spec.Key] = m;
                entries.Add(new MaterialPalette.Entry { key = spec.Key, material = m });
            }
            _palette.SetEntries(entries);
            _screen = Persist(MaterialFactory.UnlitTexture("TabletScreen", null), "Materials/TabletScreen.mat");

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader) { name = "Sky" };
                sky.SetColor("_SkyTint", new Color(0.45f, 0.65f, 1f));
                sky.SetColor("_GroundColor", new Color(0.55f, 0.7f, 0.5f));
                sky.SetFloat("_AtmosphereThickness", 0.75f);
                sky.SetFloat("_Exposure", 1.25f);
                sky.SetFloat("_SunSize", 0.05f);
                _sky = Persist(sky, "Materials/Sky.mat");
            }
        }

        Material M(string key) => _mats.TryGetValue(key, out Material m) ? m : MaterialPalette.Create(key);

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
            string mat, bool collider = true, Vector3? localEuler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler ?? Vector3.zero);
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = M(mat);
            if (!collider) Kill(go.GetComponent<Collider>());
            return go;
        }

        GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, string mat, bool collider = true, Vector3? euler = null) =>
            Prim(PrimitiveType.Cube, name, parent, pos, size, mat, collider, euler);

        /// <summary>A dynamic rigidbody object with a Grabbable (or subclass) and a "Model" child.</summary>
        T Item<T>(string name, Transform parent, Vector3 worldPos, Quaternion worldRot, float mass, out Transform model) where T : Grabbable
        {
            GameObject go = Node(name, parent, Vector3.zero);
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            model = Node("Model", go.transform, Vector3.zero).transform;
            var g = go.AddComponent<T>();
            g.Configure(_nextId++, name);
            return g;
        }

        // ============================================================= managers

        Transform BuildManagers()
        {
            GameObject go = Node("GameManager", _root, Vector3.zero);
            go.AddComponent<CommandBus>();
            go.AddComponent<EntityRegistry>();
            _palette = go.AddComponent<MaterialPalette>();
            go.AddComponent<GrabManager>();
            var parts = go.AddComponent<PartManager>();
            go.AddComponent<BuildManager>();
            go.AddComponent<WorkshopManager>();
            ShardPool shards = Node("Shards", go.transform, Vector3.zero).AddComponent<ShardPool>();
            parts.Configure(_palette, shards);
            _game = go.AddComponent<GameManager>();
            return go.transform;
        }

        // ========================================================== environment

        Transform BuildEnvironment()
        {
            GameObject env = Node("Environment", _root, Vector3.zero);

            GameObject sunGo = Node("Sun", env.transform, new Vector3(0f, 30f, 0f), new Vector3(50f, -35f, 0f));
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;

            if (_sky != null) RenderSettings.skybox = _sky;
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
            Prim(PrimitiveType.Plane, "Ground", env.transform, Vector3.zero, new Vector3(30f, 1f, 30f), "ground");

            // Low-poly trees in a ring (seeded: same every build).
            var rng = new System.Random(1234);
            string[] leaves = { "leavesGreen", "leavesLime", "leavesPink", "leavesTeal" };
            Transform trees = Node("Trees", env.transform, Vector3.zero).transform;
            for (int i = 0; i < 24; i++)
            {
                float angle = (float)(i / 24.0 * Math.PI * 2 + rng.NextDouble() * 0.2);
                float dist = 40f + (float)rng.NextDouble() * 45f;
                var pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                if (Mathf.Abs(pos.x) < 8f && pos.z > 0f) continue; // keep the truck road clear
                float h = 3f + (float)rng.NextDouble() * 4f;
                Transform tree = Node("Tree", trees, pos, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f)).transform;
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.6f, h * 0.5f, 0.6f), "trunk");
                float crown = 2.5f + (float)rng.NextDouble() * 2.5f;
                Prim(PrimitiveType.Sphere, "Crown", tree, new Vector3(0f, h + crown * 0.3f, 0f),
                    new Vector3(crown, crown * 0.85f, crown), leaves[rng.Next(leaves.Length)]);
            }
            return env.transform;
        }

        // ============================================================ team area

        TeamArea BuildTeamArea(int team, Vector3 origin)
        {
            GameObject areaGo = Node($"TeamArea {team}", _root, origin);
            Transform a = areaGo.transform;
            _detachOrder.Add(a);
            var area = areaGo.AddComponent<TeamArea>();

            // Road for the truck and a dirt plot for the building.
            Box("Road", a, new Vector3(0f, 0.005f, 45f), new Vector3(5f, 0.01f, 70f), "road", false);
            Box("PileDirt", a, new Vector3(0f, 0.004f, 6f), new Vector3(11f, 0.008f, 9f), "dirt", false);
            Box("SitePlot", a, new Vector3(0f, 0.004f, -11f), new Vector3(12f, 0.008f, 11f), "dirt", false);

            // A few bumps between pile and site: uneven ground for the wheelbarrow.
            Box("Bump", a, new Vector3(2.5f, 0f, -2.5f), new Vector3(2.2f, 0.18f, 0.8f), "dirt", true, new Vector3(0f, 20f, 0f));
            Box("Bump", a, new Vector3(-1.8f, 0f, -4.2f), new Vector3(1.6f, 0.22f, 0.9f), "dirt", true, new Vector3(0f, -35f, 4f));
            Box("Bump", a, new Vector3(0.6f, 0f, 1.2f), new Vector3(1.4f, 0.14f, 1.1f), "dirt", true, new Vector3(3f, 60f, 0f));

            Transform spawn = Node("PlayerSpawn", a, new Vector3(0f, 0.05f, -1f)).transform;

            // Build site: kit front (-z in kit space) faces the pile (+z world).
            GameObject siteGo = Node("BuildSite", a, new Vector3(0f, 0f, -11f), new Vector3(0f, 180f, 0f));
            var site = siteGo.AddComponent<BuildSite>();
            site.Configure(team, team);

            BlueprintStudio studio = BuildStudio(team, site);
            Truck truck = BuildTruck(a, new Vector3(0f, 0f, 13f));
            FinalTestDirector test = BuildFinalTest(a, site);
            List<SortingZone> zones = BuildPallets(a, team);
            BuildMortarStation(a, new Vector3(9.5f, 0f, -9f));
            Transform supply = BuildToolRack(a, new Vector3(-9.5f, 0f, -9f), area);
            BuildLadder(a, new Vector3(-6.5f, 0.05f, -16.5f));
            BuildWheelbarrow(a, new Vector3(3.2f, 0f, -0.5f));
            BuildBell(a, new Vector3(5.5f, 0f, -5.5f));

            area.Configure(team, site, truck, test, studio, zones, spawn, supply);
            return area;
        }

        BlueprintStudio BuildStudio(int team, BuildSite site)
        {
            // Far above the world (and shadow-less) so nothing can see or touch it.
            GameObject go = Node($"BlueprintStudio {team}", _root, new Vector3(team * 100f, 1500f, 0f));
            _detachOrder.Add(go.transform);
            var studio = go.AddComponent<BlueprintStudio>();
            studio.Configure(site);
            return studio;
        }

        FinalTestDirector BuildFinalTest(Transform area, BuildSite site)
        {
            GameObject go = Node("FinalTest", area, Vector3.zero);
            // Tripod camera: front-left of the site, looking at the building.
            Vector3 sitePos = site.transform.localPosition;
            GameObject camGo = Node("FinalTestCamera", go.transform, sitePos + new Vector3(-11f, 6.5f, 11f));
            camGo.transform.LookAt(area.TransformPoint(sitePos + Vector3.up * 2f));
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.cullingMask = ~(1 << Layers.Hologram);
            cam.depth = 5;
            cam.enabled = false;
            var director = go.AddComponent<FinalTestDirector>();
            director.Configure(site, cam, M("rain"));
            return director;
        }

        // ================================================================ truck

        Truck BuildTruck(Transform area, Vector3 parkPos)
        {
            const float bedLength = 6.2f, bedWidth = 2.3f, floorT = 0.1f, wallH = 0.75f;
            var hinge = new Vector3(0f, 1.15f, -3.5f);

            GameObject truckGo = Node("Truck", area, parkPos);
            var chassis = truckGo.AddComponent<Rigidbody>();
            chassis.isKinematic = true;
            chassis.interpolation = RigidbodyInterpolation.Interpolate;
            Transform t = truckGo.transform;
            Box("Frame", t, new Vector3(0f, 0.85f, 0f), new Vector3(2.2f, 0.35f, 8.4f), "black");
            Box("Cab", t, new Vector3(0f, 2.0f, 3.65f), new Vector3(2.5f, 2.0f, 1.9f), "truck");
            Box("Windshield", t, new Vector3(0f, 2.35f, 4.61f), new Vector3(2.2f, 0.8f, 0.04f), "glass", false);
            Box("Bumper", t, new Vector3(0f, 0.7f, 4.7f), new Vector3(2.6f, 0.3f, 0.2f), "metal");
            Box("Light", t, new Vector3(0.9f, 1.25f, 4.62f), new Vector3(0.3f, 0.2f, 0.04f), "yellow", false);
            Box("Light", t, new Vector3(-0.9f, 1.25f, 4.62f), new Vector3(0.3f, 0.2f, 0.04f), "yellow", false);
            foreach (float z in new[] { -2.6f, -1.5f, 3.4f })
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cylinder, "Wheel", t, new Vector3(s * 1.15f, 0.55f, z), new Vector3(1.1f, 0.18f, 1.1f), "rubber", false,
                    new Vector3(0f, 0f, 90f));

            // Bed: its own kinematic body, pivot on the rear hinge, floor running toward the cab.
            GameObject bedGo = Node("TruckBed", area, parkPos + hinge);
            var bed = bedGo.AddComponent<Rigidbody>();
            bed.isKinematic = true;
            bed.interpolation = RigidbodyInterpolation.Interpolate;
            Transform b = bedGo.transform;
            Box("Floor", b, new Vector3(0f, floorT * 0.5f, bedLength * 0.5f), new Vector3(bedWidth + 0.2f, floorT, bedLength), "truck");
            for (int s = -1; s <= 1; s += 2)
                Box("Side", b, new Vector3(s * (bedWidth * 0.5f + 0.05f), floorT + wallH * 0.5f, bedLength * 0.5f),
                    new Vector3(0.1f, wallH, bedLength), "truck");
            Box("Front", b, new Vector3(0f, floorT + wallH * 0.8f, bedLength + 0.05f), new Vector3(bedWidth + 0.2f, wallH * 1.6f, 0.1f), "truck");
            GameObject cargo = Box("CoveredLoad", b, new Vector3(0f, floorT + 0.75f, bedLength * 0.5f),
                new Vector3(bedWidth - 0.05f, 1.5f, bedLength - 0.2f), "tarp", false);

            var truck = truckGo.AddComponent<Truck>();
            truck.Configure(chassis, bed, cargo, hinge, bedLength, bedWidth, floorT);
            return truck;
        }

        // ============================================================== pallets

        List<SortingZone> BuildPallets(Transform area, int team)
        {
            var zones = new List<SortingZone>();
            // (category, label, position, deck size)
            var defs = new (PartCategory cat, string title, Vector3 pos, Vector2 size)[]
            {
                (PartCategory.Wood, "WOOD", new Vector3(-7.5f, 0f, 3f), new Vector2(4.4f, 1.6f)),
                (PartCategory.Stone, "STONE", new Vector3(-7.5f, 0f, -0.5f), new Vector2(3.2f, 1.6f)),
                (PartCategory.Roof, "ROOF", new Vector3(-7.5f, 0f, -4f), new Vector2(3.4f, 1.8f)),
                (PartCategory.Glass, "GLASS", new Vector3(7.5f, 0f, 3f), new Vector2(2.4f, 1.6f)),
                (PartCategory.ReadyMade, "READY-MADE", new Vector3(7.5f, 0f, -0.5f), new Vector2(3.2f, 2.4f)),
                (PartCategory.Fixings, "FIXINGS", new Vector3(7.5f, 0f, -4f), new Vector2(1.6f, 1.2f)),
            };
            foreach (var d in defs)
            {
                // Pallets are objects too: heavy, draggable rigidbodies. Long side along z.
                var pallet = Item<Grabbable>($"Pallet {d.title}", area, area.TransformPoint(d.pos), area.rotation, 25f, out Transform m);
                float w = d.size.y, l = d.size.x;
                const float h = 0.14f;
                for (int i = 0; i < 3; i++)
                    Box("Runner", m, new Vector3((i - 1) * (w * 0.5f - 0.05f), 0.05f, 0f), new Vector3(0.1f, 0.1f, l), "darkWood");
                Box("Deck", m, new Vector3(0f, h - 0.02f, 0f), new Vector3(w, 0.04f, l), "pallet");
                for (int i = 0; i < 4; i++)
                    Box("Slat", m, new Vector3(0f, h + 0.002f, (i - 1.5f) * l / 4f), new Vector3(w, 0.006f, 0.1f), "darkWood", false);

                // Zone above the deck; label above that, turning to face the camera.
                GameObject zoneGo = Node("SortingZone", pallet.transform, new Vector3(0f, h, 0f));
                GameObject canvasGo = Node("Label", pallet.transform, new Vector3(0f, 2.1f, 0f));
                Text label = WorldText(canvasGo, new Vector2(420f, 320f), 0.005f, 30, TextAnchor.LowerCenter);
                canvasGo.AddComponent<WorldLabel>();
                var zone = zoneGo.AddComponent<SortingZone>();
                zone.Configure(d.cat, d.title, new Vector3(w * 0.5f + 0.1f, 0.9f, l * 0.5f + 0.1f), label, team);
                zones.Add(zone);
            }
            return zones;
        }

        /// <summary>World-space canvas with one text; 1 canvas unit = <paramref name="scale"/> meters.</summary>
        Text WorldText(GameObject canvasGo, Vector2 size, float scale, int fontSize, TextAnchor align)
        {
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            Text t = MakeText(canvasGo.transform, "Text", fontSize, align, FontStyle.Normal,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.supportRichText = true;
            return t;
        }

        // ======================================================= mortar station

        void BuildMortarStation(Transform area, Vector3 pos)
        {
            Quaternion rot = area.rotation * Quaternion.Euler(0f, -90f, 0f); // long side facing the site
            var station = Item<Grabbable>("Mortar Station", area, area.TransformPoint(pos), rot, 220f, out Transform m);
            Box("Top", m, new Vector3(0f, 0.85f, 0f), new Vector3(2.6f, 0.08f, 1f), "darkWood");
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Box("Leg", m, new Vector3(sx * 1.2f, 0.4f, sz * 0.42f), new Vector3(0.1f, 0.8f, 0.1f), "darkWood");

            // Cement bag on the table (left), sand heap on the ground (right), tap at the back.
            GameObject cement = Box("CementBag", m, new Vector3(-0.85f, 1.05f, 0.15f), new Vector3(0.6f, 0.32f, 0.45f), "cement");
            Box("Print", m, new Vector3(-0.85f, 1.05f, -0.08f), new Vector3(0.35f, 0.15f, 0.01f), "red", false);
            cement.AddComponent<Dispenser>().Configure(Ingredient.Cement);

            GameObject sand = Prim(PrimitiveType.Sphere, "SandHeap", m, new Vector3(1.8f, 0.23f, 0f), new Vector3(1.1f, 0.45f, 1.1f), "sand");
            sand.AddComponent<Dispenser>().Configure(Ingredient.Sand);

            Box("TapPost", m, new Vector3(0.2f, 1.3f, 0.45f), new Vector3(0.08f, 0.9f, 0.08f), "metal");
            GameObject tap = Prim(PrimitiveType.Cylinder, "Tap", m, new Vector3(0.2f, 1.62f, 0.3f), new Vector3(0.07f, 0.15f, 0.07f), "metal",
                true, new Vector3(90f, 0f, 0f));
            Box("TapHandle", m, new Vector3(0.2f, 1.72f, 0.3f), new Vector3(0.16f, 0.03f, 0.03f), "red", false);
            tap.AddComponent<Dispenser>().Configure(Ingredient.Water);

            GameObject sign = Node("RecipeSign", m, new Vector3(0f, 1.6f, 0.52f), new Vector3(0f, 180f, 0f));
            Text t = WorldText(sign, new Vector2(300f, 90f), 0.004f, 26, TextAnchor.MiddleCenter);
            t.text = "<b>MORTAR</b>\n1 cement : 3 sand : 1 water\nthen stir with the shovel";

            // Two buckets on the table and the shovel leaning on it.
            for (int i = 0; i < 2; i++)
                BuildBucket(station.transform.TransformPoint(new Vector3(0.05f + i * 0.45f, 0.9f, -0.15f)), rot);
            BuildShovel(station.transform.TransformPoint(new Vector3(-0.3f, 0.1f, -0.85f)), rot * Quaternion.Euler(0f, 0f, 90f));
        }

        void BuildBucket(Vector3 pos, Quaternion rot)
        {
            var bucket = Item<Bucket>("Bucket", _root, pos, rot, 1.5f, out Transform m);
            const float r = 0.17f, h = 0.34f;
            Box("Bottom", m, new Vector3(0f, 0.01f, 0f), new Vector3(r * 2f, 0.02f, r * 2f), "orange");
            // Eight staves make an open-top bucket; only four wall colliders.
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, h * 0.5f, r);
                Box("Stave", m, p, new Vector3(r * 0.85f, h, 0.02f), "orange", i % 2 == 0, new Vector3(0f, a, 0f));
            }
            Box("Handle", m, new Vector3(0f, h + 0.08f, 0f), new Vector3(r * 2f, 0.015f, 0.015f), "black", false);
            GameObject contents = Prim(PrimitiveType.Cylinder, "Contents", m, new Vector3(0f, 0.05f, 0f), new Vector3(r * 1.8f, 0.02f, r * 1.8f),
                "cement", false);
            contents.SetActive(false);
            bucket.SetParts(contents.transform, contents.GetComponent<MeshRenderer>(), h - 0.04f);
        }

        // ============================================================ tool rack

        Transform BuildToolRack(Transform area, Vector3 pos, TeamArea teamArea)
        {
            Quaternion rot = area.rotation * Quaternion.Euler(0f, 90f, 0f);
            var rack = Item<Grabbable>("Tool Rack", area, area.TransformPoint(pos), rot, 70f, out Transform m);
            const float w = 3.4f, d = 0.7f;
            foreach (float y in new[] { 0.45f, 1.0f })
                Box("Shelf", m, new Vector3(0f, y, 0f), new Vector3(w, 0.05f, d), "darkWood");
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Box("Leg", m, new Vector3(sx * (w * 0.5f - 0.05f), 0.55f, sz * (d * 0.5f - 0.05f)), new Vector3(0.08f, 1.1f, 0.08f), "darkWood");
            Box("Back", m, new Vector3(0f, 1.6f, d * 0.5f - 0.02f), new Vector3(w, 1.1f, 0.04f), "pallet");

            Transform rt = rack.transform;
            Vector3 Top(float x, float z = -0.05f) => rt.TransformPoint(new Vector3(x, 1.04f, z));
            Quaternion lying = rot * Quaternion.Euler(0f, 0f, 90f);
            BuildHammer(Top(-1.45f), lying);
            BuildHammer(Top(-1.15f), lying);
            BuildScrewdriver(Top(-0.85f), rot);
            BuildSaw(Top(-0.5f), rot * Quaternion.Euler(0f, 0f, 90f));
            BuildSaw(Top(-0.05f), rot * Quaternion.Euler(0f, 0f, 90f));
            BuildGlassCutter(Top(0.4f), rot);
            BuildTrowel(Top(0.7f), rot);
            BuildTablet(Top(1.2f, -0.1f) + Vector3.up * 0.03f, rot * Quaternion.Euler(80f, 0f, 0f), teamArea);

            // Starter fixings appear on the lower shelf each round.
            GameObject supply = Node("SupplyPoint", rt, new Vector3(-1f, 0.6f, -0.05f));
            return supply.transform;
        }

        void BuildHammer(Vector3 pos, Quaternion rot)
        {
            var hammer = Item<Hammer>("Hammer", _root, pos, rot, 0.8f, out Transform m);
            Box("Handle", m, new Vector3(0f, 0.14f, 0f), new Vector3(0.035f, 0.34f, 0.035f), "wood");
            Box("Grip", m, new Vector3(0f, 0.02f, 0f), new Vector3(0.042f, 0.1f, 0.042f), "black");
            Box("Head", m, new Vector3(0f, 0.31f, 0.02f), new Vector3(0.045f, 0.05f, 0.15f), "metal");
            hammer.ConfigureGrip(new Vector3(0f, 0.03f, 0f), new Vector3(0.3f, -0.32f, 0.55f), new Vector3(-10f, 0f, 0f));
        }

        void BuildScrewdriver(Vector3 pos, Quaternion rot)
        {
            var sd = Item<Screwdriver>("Screwdriver", _root, pos, rot, 0.25f, out Transform m);
            Prim(PrimitiveType.Cylinder, "Handle", m, Vector3.zero, new Vector3(0.035f, 0.055f, 0.035f), "yellow", true, new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Cylinder, "Shaft", m, new Vector3(0f, 0f, 0.12f), new Vector3(0.008f, 0.07f, 0.008f), "metal", false,
                new Vector3(90f, 0f, 0f));
            sd.ConfigureGrip(Vector3.zero, new Vector3(0.24f, -0.24f, 0.5f), new Vector3(5f, 0f, 0f));
        }

        void BuildSaw(Vector3 pos, Quaternion rot)
        {
            var saw = Item<Saw>("Saw", _root, pos, rot, 0.9f, out Transform m);
            Box("Blade", m, new Vector3(0f, -0.01f, 0.1f), new Vector3(0.004f, 0.13f, 0.58f), "metal");
            Box("Teeth", m, new Vector3(0f, -0.08f, 0.1f), new Vector3(0.006f, 0.015f, 0.58f), "black", false);
            Box("Handle", m, new Vector3(0f, 0.02f, -0.24f), new Vector3(0.035f, 0.13f, 0.13f), "red");
            saw.ConfigureGrip(new Vector3(0f, 0.02f, -0.24f), new Vector3(0.24f, -0.3f, 0.48f), new Vector3(20f, 0f, 0f));
        }

        void BuildGlassCutter(Vector3 pos, Quaternion rot)
        {
            var gc = Item<GlassCutter>("Glass Cutter", _root, pos, rot, 0.15f, out Transform m);
            Prim(PrimitiveType.Cylinder, "Handle", m, Vector3.zero, new Vector3(0.025f, 0.07f, 0.025f), "blue", true, new Vector3(90f, 0f, 0f));
            Box("Head", m, new Vector3(0f, 0f, 0.085f), new Vector3(0.012f, 0.03f, 0.03f), "metal", false);
            Prim(PrimitiveType.Cylinder, "Wheel", m, new Vector3(0f, -0.01f, 0.1f), new Vector3(0.015f, 0.002f, 0.015f), "metal", false,
                new Vector3(0f, 0f, 90f));
            gc.ConfigureGrip(Vector3.zero, new Vector3(0.22f, -0.22f, 0.45f), new Vector3(35f, 0f, 0f));
        }

        void BuildTrowel(Vector3 pos, Quaternion rot)
        {
            var trowel = Item<Trowel>("Trowel", _root, pos, rot, 0.45f, out Transform m);
            Prim(PrimitiveType.Cylinder, "Handle", m, Vector3.zero, new Vector3(0.03f, 0.05f, 0.03f), "wood", true, new Vector3(90f, 0f, 0f));
            Box("Neck", m, new Vector3(0f, -0.02f, 0.06f), new Vector3(0.01f, 0.04f, 0.02f), "metal", false);
            Box("Blade", m, new Vector3(0f, -0.04f, 0.15f), new Vector3(0.12f, 0.004f, 0.12f), "metal", true, new Vector3(0f, 45f, 0f));
            GameObject blob = Prim(PrimitiveType.Sphere, "Mortar", m, new Vector3(0f, -0.02f, 0.15f), new Vector3(0.1f, 0.04f, 0.1f),
                "mortarFresh", false);
            blob.SetActive(false);
            trowel.SetBlob(blob);
            trowel.ConfigureGrip(Vector3.zero, new Vector3(0.25f, -0.25f, 0.5f), new Vector3(15f, 0f, 0f));
        }

        void BuildShovel(Vector3 pos, Quaternion rot)
        {
            var shovel = Item<Shovel>("Shovel", _root, pos, rot, 2f, out Transform m);
            Prim(PrimitiveType.Cylinder, "Shaft", m, Vector3.zero, new Vector3(0.04f, 0.55f, 0.04f), "wood");
            Box("Blade", m, new Vector3(0f, -0.65f, 0f), new Vector3(0.24f, 0.28f, 0.02f), "metal");
            Box("Grip", m, new Vector3(0f, 0.58f, 0f), new Vector3(0.14f, 0.04f, 0.04f), "black", false);
            // Blade forward and down (see SETUP.md: hold pose tuning).
            shovel.ConfigureGrip(new Vector3(0f, 0.35f, 0f), new Vector3(0.2f, -0.15f, 0.55f), new Vector3(-45f, 0f, 0f));
        }

        void BuildTablet(Vector3 pos, Quaternion rot, TeamArea teamArea)
        {
            var tablet = Item<BlueprintTablet>("Blueprint Tablet", _root, pos, rot, 0.9f, out Transform m);
            const float tw = 0.7f, th = 0.45f;
            Box("Body", m, Vector3.zero, new Vector3(tw, th, 0.03f), "black");
            // Quad faces -Z: the side that looks at the camera when held.
            GameObject screen = Prim(PrimitiveType.Quad, "Screen", m, new Vector3(-0.13f, -0.015f, -0.0165f), new Vector3(0.4f, 0.4f, 1f), "black", false);
            screen.GetComponent<MeshRenderer>().sharedMaterial = _screen;
            screen.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // World-space canvas overlay: 1 canvas unit = 1 mm.
            GameObject canvasGo = Node("ScreenCanvas", m, new Vector3(0f, 0f, -0.018f));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(tw * 1000f, th * 1000f);
            rt.localPosition = new Vector3(0f, 0f, -0.018f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * 0.001f;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;

            Text title = MakeText(canvasGo.transform, "Title", 18, TextAnchor.UpperLeft, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(-16f, 0f), new Vector2(8f, -2f));
            Text timer = MakeText(canvasGo.transform, "Timer", 20, TextAnchor.UpperRight, FontStyle.Bold,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(-16f, 0f), new Vector2(-8f, -2f));
            Text view = MakeText(canvasGo.transform, "Views", 13, TextAnchor.LowerLeft, FontStyle.Normal,
                new Vector2(0f, 0f), new Vector2(0.62f, 0f), new Vector2(0f, 22f), new Vector2(0f, 0f), new Vector2(10f, 2f));
            Text info = MakeText(canvasGo.transform, "Checklist", 11, TextAnchor.UpperLeft, FontStyle.Normal,
                new Vector2(0.6f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, Vector2.zero);
            info.rectTransform.offsetMin = new Vector2(4f, 6f);
            info.rectTransform.offsetMax = new Vector2(-6f, -32f);
            info.verticalOverflow = VerticalWrapMode.Truncate;
            info.supportRichText = true;

            tablet.ConfigureGrip(new Vector3(0f, -th * 0.5f, 0f), new Vector3(0.28f, -0.36f, 0.55f), new Vector3(35f, -18f, 0f));
            tablet.SetParts(teamArea, screen.GetComponent<MeshRenderer>(), title, timer, view, info);
        }

        // ===================================================== ladder, barrow, bell

        void BuildLadder(Transform area, Vector3 pos)
        {
            float length = _settings.ladderLength;
            // Lying flat on the ground along x (rails side by side, rungs level).
            Quaternion rot = area.rotation * Quaternion.LookRotation(Vector3.up, Vector3.right);
            var ladder = Item<Ladder>("Ladder", _root, area.TransformPoint(pos + Vector3.up * 0.05f), rot, 12f, out Transform m);
            // Only the rails collide (simple, stable contacts); rungs are visual.
            for (int s = -1; s <= 1; s += 2)
                Box("Rail", m, new Vector3(s * 0.25f, length * 0.5f, 0f), new Vector3(0.06f, length, 0.06f), "yellow");
            for (float h = 0.3f; h < length - 0.1f; h += 0.3f)
                Box("Rung", m, new Vector3(0f, h, 0f), new Vector3(0.5f, 0.04f, 0.04f), "metal", false);
            GameObject zoneGo = Node("ClimbZone", m, Vector3.zero);
            var box = zoneGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, length * 0.5f, -0.4f);
            box.size = new Vector3(0.75f, length, 0.6f);
            var zone = zoneGo.AddComponent<ClimbZone>();
            zone.Configure(length, ladder.GetComponent<Rigidbody>());
            // Carried roughly level, ahead and low; grip a third of the way up.
            ladder.ConfigureGrip(new Vector3(0f, length * 0.35f, 0f), new Vector3(0.35f, -0.55f, 0.9f), new Vector3(80f, 0f, 0f));
            ladder.SetDimensions(length, _settings.ladderLean, zone);
        }

        void BuildWheelbarrow(Transform area, Vector3 pos)
        {
            Quaternion rot = area.rotation * Quaternion.Euler(0f, 160f, 0f);
            var barrow = Item<Wheelbarrow>("Wheelbarrow", _root, area.TransformPoint(pos), rot, 14f, out Transform m);
            // Tray (local +z = toward the wheel), legs at the back, handles sticking out back.
            Box("TrayBottom", m, new Vector3(0f, 0.42f, 0.1f), new Vector3(0.7f, 0.04f, 0.9f), "green");
            Box("TrayFront", m, new Vector3(0f, 0.58f, 0.57f), new Vector3(0.7f, 0.32f, 0.04f), "green", true, new Vector3(-20f, 0f, 0f));
            Box("TrayBack", m, new Vector3(0f, 0.56f, -0.36f), new Vector3(0.7f, 0.28f, 0.04f), "green");
            for (int s = -1; s <= 1; s += 2)
            {
                Box("TraySide", m, new Vector3(s * 0.36f, 0.57f, 0.1f), new Vector3(0.04f, 0.3f, 0.95f), "green");
                Box("Handle", m, new Vector3(s * 0.28f, 0.42f, -0.65f), new Vector3(0.04f, 0.04f, 0.9f), "wood");
                Box("Leg", m, new Vector3(s * 0.25f, 0.2f, -0.3f), new Vector3(0.04f, 0.4f, 0.04f), "black");
            }
            Box("Axle", m, new Vector3(0f, 0.2f, 0.72f), new Vector3(0.3f, 0.03f, 0.03f), "black", false);

            // The wheel: its own small rigidbody on a hinge.
            GameObject wheel = Node("Wheel", _root, Vector3.zero);
            wheel.transform.SetPositionAndRotation(barrow.transform.TransformPoint(new Vector3(0f, 0.2f, 0.72f)), rot);
            var wrb = wheel.AddComponent<Rigidbody>();
            wrb.mass = 2f;
            wrb.interpolation = RigidbodyInterpolation.Interpolate;
            var sphere = wheel.AddComponent<SphereCollider>();
            sphere.radius = 0.2f;
            Prim(PrimitiveType.Cylinder, "Tyre", wheel.transform, Vector3.zero, new Vector3(0.4f, 0.04f, 0.4f), "rubber", false,
                new Vector3(0f, 0f, 90f));
            var hinge = wheel.AddComponent<HingeJoint>();
            hinge.connectedBody = barrow.GetComponent<Rigidbody>();
            hinge.axis = Vector3.right;
            hinge.anchor = Vector3.zero;
            wheel.transform.SetParent(barrow.transform, true); // keeps it with the barrow in the hierarchy; still its own body
        }

        void BuildBell(Transform area, Vector3 pos)
        {
            var post = Item<Grabbable>("Inspection Bell", area, area.TransformPoint(pos), area.rotation, 45f, out Transform m);
            Box("Base", m, new Vector3(0f, 0.05f, 0f), new Vector3(0.6f, 0.1f, 0.6f), "darkWood");
            Box("Post", m, new Vector3(0f, 0.8f, 0f), new Vector3(0.1f, 1.5f, 0.1f), "darkWood");
            Box("Arm", m, new Vector3(0.15f, 1.5f, 0f), new Vector3(0.35f, 0.06f, 0.06f), "darkWood");
            GameObject bell = Prim(PrimitiveType.Sphere, "Bell", m, new Vector3(0.28f, 1.36f, 0f), new Vector3(0.22f, 0.24f, 0.22f), "yellow");
            bell.AddComponent<InspectionBell>();
            GameObject sign = Node("Sign", m, new Vector3(0f, 1.9f, 0f));
            Text t = WorldText(sign, new Vector2(320f, 60f), 0.004f, 28, TextAnchor.MiddleCenter);
            t.text = "<b>DONE? RING ME!</b>";
            sign.AddComponent<WorldLabel>();
        }

        // =============================================================== player

        Transform BuildPlayer(TeamArea area)
        {
            Transform spawn = area.PlayerSpawn;
            GameObject go = Node("Player", _root, Vector3.zero);
            go.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            go.tag = "Player";
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;

            go.AddComponent<PlayerInputHandler>();
            go.AddComponent<PlayerInventory>();
            var grabber = go.AddComponent<PlayerGrabber>();
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

            look.Configure(pivot, _settings.mouseSensitivity);
            grabber.Configure(1, area.TeamId, camGo.transform, shake);
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
            t.supportRichText = true;
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

        GameObject Panel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            return panel;
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
            hud.player = _player;

            var cross = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            cross.transform.SetParent(c, false);
            var crt = (RectTransform)cross.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(6f, 6f);
            var img = cross.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.85f);
            img.raycastTarget = false;
            hud.crosshair = cross;

            Vector2 mid = new Vector2(0.5f, 0.5f);
            hud.timerText = ScreenText(c, "Timer", 60, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(400f, 76f), FontStyle.Bold);
            hud.bannerText = ScreenText(c, "Banner", 32, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(1500f, 50f), FontStyle.Bold);
            hud.bannerText.color = new Color(1f, 0.88f, 0.35f);
            hud.stageText = ScreenText(c, "Stage", 22, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(700f, 60f));
            hud.pocketText = ScreenText(c, "Pockets", 20, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(500f, 30f));
            hud.lookText = ScreenText(c, "LookAt", 24, TextAnchor.LowerCenter, mid, new Vector2(0f, 30f), new Vector2(1100f, 34f));
            hud.promptText = ScreenText(c, "Prompt", 28, TextAnchor.UpperCenter, mid, new Vector2(0f, -40f), new Vector2(1100f, 40f), FontStyle.Bold);
            hud.toolStatusText = ScreenText(c, "ToolStatus", 24, TextAnchor.UpperCenter, mid, new Vector2(0f, -90f), new Vector2(1100f, 90f));
            hud.toolStatusText.color = new Color(0.75f, 1f, 0.8f);
            hud.heldText = ScreenText(c, "Held", 26, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-30f, 60f), new Vector2(900f, 40f), FontStyle.Bold);
            hud.heldHintText = ScreenText(c, "HeldHint", 20, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-30f, 25f), new Vector2(1100f, 30f));
            hud.controlsText = ScreenText(c, "Controls", 17, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(25f, 20f), new Vector2(760f, 150f));
            hud.controlsText.text =
                "WASD move  Shift sprint  Space jump\n" +
                "E grab / release / use station  G throw  Scroll hold distance\n" +
                "Hold R + mouse rotate held  LMB use tool\n" +
                "RMB/Tab raise tablet  1-4 views  Esc free mouse\n" +
                "Ladder: walk into it + look up/down";
            hud.controlsText.color = new Color(1f, 1f, 1f, 0.7f);

            // Briefing: the blueprint render plus kit info.
            GameObject brief = Panel(c, "BriefingPanel", new Color(0.03f, 0.06f, 0.14f, 0.82f));
            var rawGo = new GameObject("Blueprint", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(brief.transform, false);
            var rrt = (RectTransform)rawGo.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.27f, 0.5f);
            rrt.sizeDelta = new Vector2(640f, 640f);
            hud.briefingImage = rawGo.GetComponent<RawImage>();
            hud.briefingImage.raycastTarget = false;
            hud.briefingText = ScreenText(brief.transform, "BriefingText", 24, TextAnchor.MiddleLeft, new Vector2(0.52f, 0.5f), Vector2.zero,
                new Vector2(860f, 700f));
            hud.briefingPanel = brief;
            brief.SetActive(false);

            // Results panel
            GameObject panel = Panel(c, "ResultsPanel", new Color(0.05f, 0.04f, 0.12f, 0.75f));
            Transform p = panel.transform;
            Text heading = ScreenText(p, "Heading", 34, TextAnchor.MiddleCenter, mid, new Vector2(0f, 250f), new Vector2(1200f, 50f));
            heading.text = "INSPECTION COMPLETE - YOUR CREW IS:";
            hud.rankText = ScreenText(p, "Rank", 80, TextAnchor.MiddleCenter, mid, new Vector2(0f, 160f), new Vector2(1700f, 110f), FontStyle.Bold);
            hud.rankText.color = new Color(1f, 0.85f, 0.35f);
            hud.scoreText = ScreenText(p, "Score", 56, TextAnchor.MiddleCenter, mid, new Vector2(0f, 50f), new Vector2(1200f, 80f), FontStyle.Bold);
            hud.detailsText = ScreenText(p, "Details", 28, TextAnchor.UpperCenter, mid, new Vector2(0f, -10f), new Vector2(1200f, 140f));

            var btnGo = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(p, false);
            var brt = (RectTransform)btnGo.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = mid;
            brt.sizeDelta = new Vector2(460f, 80f);
            brt.anchoredPosition = new Vector2(0f, -220f);
            btnGo.GetComponent<Image>().color = new Color(1f, 0.6f, 0.2f);
            Text btnText = ScreenText(btnGo.transform, "Label", 32, TextAnchor.MiddleCenter, mid, Vector2.zero, new Vector2(460f, 80f), FontStyle.Bold);
            btnText.text = "New pile, build again  (R)";
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
