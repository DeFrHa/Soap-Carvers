using System.Collections.Generic;
using BuildCrew.Building;
using BuildCrew.Core;
using BuildCrew.Kits;
using BuildCrew.Parts;
using UnityEngine;

namespace BuildCrew.Round
{
    /// <summary>
    /// The final test for one team's building: a fixed camera, gusty wind (and
    /// rain) pushing on every part around the site for a few seconds.
    /// Snapped-but-not-fixed parts lose their spring first, so they only stay
    /// if they happen to rest somewhere. Afterwards it counts the building's
    /// parts that moved or turned too far (or shattered): those are lost.
    ///
    /// Wind force per part: F = 1/2 rho v^2 Cd A, with A the part's box
    /// projected onto the wind direction, plus a bit of uplift on roof-like
    /// surfaces. Rain adds a load on upward-facing surfaces.
    /// </summary>
    public class FinalTestDirector : MonoBehaviour
    {
        [SerializeField] BuildSite site;
        [SerializeField] Camera testCamera;
        [SerializeField] Vector3 windDirection = new Vector3(1f, 0f, 0.35f);
        [SerializeField] float radius = 9f;
        [SerializeField] Material rainMaterial;

        struct Tracked
        {
            public Part Part;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        const float AirDensity = 1.2f;
        const float DragCoefficient = 1.1f;

        readonly List<Tracked> _tracked = new List<Tracked>();
        ParticleSystem _rain;
        Camera _playerCamera;
        FinalTestKind _kind;
        float _duration, _t, _noiseSeed;

        public bool IsRunning { get; private set; }
        public bool IsFinished { get; private set; }
        public int TrackedCount => _tracked.Count;
        public int LostParts { get; private set; }
        public FinalTestKind Kind => _kind;
        public float TimeLeft => Mathf.Max(0f, _duration - _t);
        /// <summary>Current gust speed (m/s), for the HUD.</summary>
        public float WindSpeed { get; private set; }

        public void Configure(BuildSite buildSite, Camera cam, Material rain)
        {
            site = buildSite;
            testCamera = cam;
            rainMaterial = rain;
        }

        void Awake()
        {
            if (testCamera != null) testCamera.enabled = false;
            _noiseSeed = Random.value * 100f;
        }

        public void ResetTest()
        {
            IsRunning = false;
            IsFinished = false;
            LostParts = 0;
            _tracked.Clear();
            ShowTestCamera(false);
            if (_rain != null) _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Begin(FinalTestKind kind, float seconds)
        {
            _kind = kind;
            _duration = seconds;
            _t = 0f;
            IsRunning = true;
            IsFinished = false;
            LostParts = 0;
            _tracked.Clear();
            if (site != null)
            {
                foreach (BuildSlot slot in site.Slots)
                    if (slot.Part != null && slot.State != SlotState.Empty)
                        _tracked.Add(new Tracked { Part = slot.Part, Position = slot.Part.transform.position, Rotation = slot.Part.transform.rotation });
                if (World.Build != null) World.Build.ReleaseUnfixed(site);
            }
            ShowTestCamera(true);
            if (kind == FinalTestKind.WindRain) StartRain();
        }

        void Update()
        {
            if (!IsRunning) return;
            _t += Time.deltaTime;
            if (_t >= _duration) Finish();
        }

        void Finish()
        {
            IsRunning = false;
            IsFinished = true;
            GameSettings s = World.Settings;
            int lost = 0;
            foreach (Tracked t in _tracked)
            {
                if (t.Part == null) { lost++; continue; } // shattered
                bool moved = Vector3.Distance(t.Part.transform.position, t.Position) > s.lostDistance;
                bool turned = Quaternion.Angle(t.Part.transform.rotation, t.Rotation) > s.lostAngle;
                if (moved || turned) lost++;
            }
            LostParts = lost;
            if (_rain != null) _rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>Back to the player's view (results screen).</summary>
        public void EndView() => ShowTestCamera(false);

        void FixedUpdate()
        {
            if (!IsRunning || site == null) return;
            GameSettings s = World.Settings;
            PartManager parts = World.Parts;
            if (parts == null) return;

            // Gusts: Perlin noise on top of the base speed, ramping in over the first second.
            float ramp = Mathf.Clamp01(_t);
            float gust = Mathf.PerlinNoise(_noiseSeed, Time.time * 0.7f);
            float v = (s.windSpeed + s.windGust * gust) * ramp;
            if (_kind == FinalTestKind.WindRain) v *= s.rainWindMultiplier;
            WindSpeed = v;
            float q = 0.5f * AirDensity * v * v; // dynamic pressure, Pa
            Vector3 wind = windDirection.normalized;
            Vector3 center = site.transform.position;

            foreach (Part p in parts.Parts)
            {
                if (p == null || p.Body == null || p.Body.isKinematic) continue;
                if ((p.transform.position - center).sqrMagnitude > radius * radius) continue;
                Quaternion rot = p.transform.rotation;
                float area = PhysicsUtil.ProjectedBoxArea(p.Size, rot, wind);
                float topArea = PhysicsUtil.ProjectedBoxArea(p.Size, rot, Vector3.up);
                Vector3 force = wind * (q * DragCoefficient * area) + Vector3.up * (q * 0.3f * topArea);
                if (_kind == FinalTestKind.WindRain) force += Vector3.down * (s.rainLoad * topArea);
                // Keep small things from going ballistic.
                float max = p.Body.mass * 25f;
                if (force.sqrMagnitude > max * max) force = force.normalized * max;
                // Slightly off-center so things also turn.
                Vector3 at = p.transform.TransformPoint(new Vector3(0f, p.Size.y * 0.25f, 0f));
                p.Body.AddForceAtPosition(force, at, ForceMode.Force);
            }
        }

        void ShowTestCamera(bool show)
        {
            if (testCamera == null) return;
            if (show)
            {
                Camera main = Camera.main;
                if (main != null && main != testCamera)
                {
                    _playerCamera = main;
                    _playerCamera.enabled = false;
                }
                testCamera.enabled = true;
            }
            else
            {
                testCamera.enabled = false;
                if (_playerCamera != null) _playerCamera.enabled = true;
                _playerCamera = null;
            }
        }

        void StartRain()
        {
            if (_rain == null)
            {
                var go = new GameObject("Rain");
                go.transform.SetParent(transform, false);
                go.transform.position = (site != null ? site.transform.position : transform.position) + Vector3.up * 14f;
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                _rain = go.AddComponent<ParticleSystem>();
                _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = _rain.main;
                main.playOnAwake = false;
                main.loop = true;
                main.startLifetime = 1.2f;
                main.startSpeed = 16f;
                main.startSize = 0.03f;
                main.maxParticles = 4000;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                ParticleSystem.EmissionModule emission = _rain.emission;
                emission.rateOverTime = 2500f;
                ParticleSystem.ShapeModule shape = _rain.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(26f, 26f, 0.1f);
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.06f;
                r.lengthScale = 1f;
                if (rainMaterial != null) r.sharedMaterial = rainMaterial;
            }
            _rain.Play();
        }
    }
}
