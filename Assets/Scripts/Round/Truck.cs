using System.Collections.Generic;
using BuildCrew.Building;
using BuildCrew.Kits;
using BuildCrew.Parts;
using UnityEngine;

namespace BuildCrew.Round
{
    public enum TruckPhase
    {
        Away = 0,
        Arriving = 1,
        Parked = 2,
        Tipping = 3,
        Holding = 4,
        Lowering = 5,
        Leaving = 6,
        Gone = 7,
    }

    /// <summary>
    /// The dump truck: a kinematic chassis and a separate kinematic bed body,
    /// both moved with MovePosition/MoveRotation so the parts on the bed get
    /// proper friction and contacts. It reverses in from far away during the
    /// briefing; on Dump the covered load turns into real parts packed on the
    /// flat bed, the bed tips about its rear hinge and everything slides out
    /// into one tangled physics heap. Then it lowers the bed and drives off.
    ///
    /// Bed frame: pivot on the rear hinge (bed-local origin), floor top at
    /// floorTop, bed runs toward +z (the cab), the rear (-z) is open.
    /// </summary>
    public class Truck : MonoBehaviour
    {
        [SerializeField] Rigidbody chassis;
        [SerializeField] Rigidbody bed;
        [SerializeField] GameObject cargoVisual;
        [SerializeField] Vector3 hingeLocal = new Vector3(0f, 1.1f, -3.4f);
        [SerializeField] float bedLength = 6.2f;
        [SerializeField] float bedWidth = 2.3f;
        [SerializeField] float floorTop = 0.1f;
        [SerializeField] float arriveDistance = 45f;
        [SerializeField] float tipAngle = 52f;
        [SerializeField] float tipSeconds = 3.5f;
        [SerializeField] float holdSeconds = 3.5f;
        [SerializeField] float lowerSeconds = 2.5f;
        [SerializeField] float leaveSeconds = 8f;
        [SerializeField] float leaveDistance = 70f;

        Vector3 _parkPos;
        Quaternion _parkRot;
        Vector3 _pos;
        float _angle;
        float _t;
        float _arriveSeconds = 7f;
        bool _initialized;

        public TruckPhase Phase { get; private set; } = TruckPhase.Parked;
        /// <summary>The bed has been fully tipped (Time.time), or -1.</summary>
        public float DumpFinishedAt { get; private set; } = -1f;
        public bool HasDumped => DumpFinishedAt >= 0f;
        public bool IsParked => Phase == TruckPhase.Parked;

        public void Configure(Rigidbody chassisBody, Rigidbody bedBody, GameObject cargo, Vector3 hinge, float length, float width, float floor)
        {
            chassis = chassisBody;
            bed = bedBody;
            cargoVisual = cargo;
            hingeLocal = hinge;
            bedLength = length;
            bedWidth = width;
            floorTop = floor;
        }

        void Awake() => Init();

        void Init()
        {
            if (_initialized) return;
            _initialized = true;
            // The scene places the truck at its park pose.
            _parkPos = chassis.position;
            _parkRot = chassis.rotation;
            _pos = _parkPos;
        }

        Vector3 Forward => _parkRot * Vector3.forward;

        /// <summary>Teleport far away (in front of the park spot), bed down, covered load visible.</summary>
        public void ResetAway()
        {
            Init();
            Phase = TruckPhase.Away;
            DumpFinishedAt = -1f;
            _angle = 0f;
            _pos = _parkPos + Forward * arriveDistance;
            Teleport();
            SetVisible(true);
            if (cargoVisual != null) cargoVisual.SetActive(true);
        }

        /// <summary>Reverse to the park spot over <paramref name="seconds"/>.</summary>
        public void Arrive(float seconds)
        {
            Phase = TruckPhase.Arriving;
            _arriveSeconds = Mathf.Max(0.5f, seconds);
            _t = 0f;
        }

        /// <summary>Turn the covered load into real parts on the bed and start tipping.</summary>
        public void Dump(IList<PartSpec> specs, PartManager parts)
        {
            if (Phase == TruckPhase.Arriving || Phase == TruckPhase.Away)
            {
                _pos = _parkPos;
                Teleport();
            }
            if (cargoVisual != null) cargoVisual.SetActive(false);
            SpawnCargo(specs, parts);
            Phase = TruckPhase.Tipping;
            _t = 0f;
        }

        void SpawnCargo(IList<PartSpec> specs, PartManager parts)
        {
            if (parts == null) return;
            // Heavy, big things first (bottom layers), but keep the generator's shuffle among equals.
            var order = new List<PartSpec>(specs);
            order.Sort((a, b) => (b.Size.x * b.Size.y * b.Size.z).CompareTo(a.Size.x * a.Size.y * a.Size.z));

            Quaternion bedRot = bed.rotation;
            Vector3 hinge = bed.position;
            const float gap = 0.03f, margin = 0.06f;
            float halfW = bedWidth * 0.5f;
            float x = -halfW + margin, z = margin, y = floorTop + 0.02f;
            float rowW = 0f, layerH = 0f;

            foreach (PartSpec spec in order)
            {
                // Lay it down: longest side along the bed, thinnest side up.
                Quaternion local = LieDown(spec.Size, out Vector3 d); // d = (width, height, length) in bed frame
                if (z + d.z > bedLength - margin && z > margin)
                {
                    x += rowW + gap;
                    z = margin;
                    rowW = 0f;
                }
                if (x + d.x > halfW - margin && x > -halfW + margin)
                {
                    y += layerH + gap;
                    layerH = 0f;
                    x = -halfW + margin;
                    z = margin;
                    rowW = 0f;
                }
                var center = new Vector3(x + d.x * 0.5f, y + d.y * 0.5f, z + d.z * 0.5f);
                parts.SpawnPilePart(spec, hinge + bedRot * center, bedRot * local);
                z += d.z + gap;
                rowW = Mathf.Max(rowW, d.x);
                layerH = Mathf.Max(layerH, d.y);
            }
        }

        /// <summary>Axis-aligned rotation mapping the part's largest size to z, smallest to y.</summary>
        static Quaternion LieDown(Vector3 size, out Vector3 dims)
        {
            float[] s = { size.x, size.y, size.z };
            System.Array.Sort(s);
            var want = new Vector3(s[1], s[0], s[2]);
            foreach (Quaternion r in SlotMatcher.Rotations)
            {
                Vector3 d = r * size;
                d = new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z));
                if (Mathf.Abs(d.x - want.x) < 1e-3f && Mathf.Abs(d.y - want.y) < 1e-3f && Mathf.Abs(d.z - want.z) < 1e-3f)
                {
                    dims = d;
                    return r;
                }
            }
            dims = size;
            return Quaternion.identity;
        }

        void FixedUpdate()
        {
            if (chassis == null || bed == null) return;
            float dt = Time.fixedDeltaTime;
            switch (Phase)
            {
                case TruckPhase.Arriving:
                    _t += dt / _arriveSeconds;
                    // Ease out: fast at first, gentle stop.
                    float e = 1f - (1f - Mathf.Clamp01(_t)) * (1f - Mathf.Clamp01(_t));
                    _pos = Vector3.Lerp(_parkPos + Forward * arriveDistance, _parkPos, e);
                    if (_t >= 1f) Phase = TruckPhase.Parked;
                    break;
                case TruckPhase.Tipping:
                    _t += dt / tipSeconds;
                    _angle = Mathf.SmoothStep(0f, tipAngle, Mathf.Clamp01(_t));
                    if (_t >= 1f) { Phase = TruckPhase.Holding; _t = 0f; }
                    break;
                case TruckPhase.Holding:
                    _t += dt / holdSeconds;
                    // A little shake to free stuck parts.
                    _angle = tipAngle + Mathf.Sin(Time.time * 25f) * 1.2f;
                    if (_t >= 1f) { Phase = TruckPhase.Lowering; _t = 0f; DumpFinishedAt = Time.time; }
                    break;
                case TruckPhase.Lowering:
                    _t += dt / lowerSeconds;
                    _angle = Mathf.SmoothStep(tipAngle, 0f, Mathf.Clamp01(_t));
                    if (_t >= 1f) { Phase = TruckPhase.Leaving; _t = 0f; }
                    break;
                case TruckPhase.Leaving:
                    _t += dt / leaveSeconds;
                    float l = Mathf.Clamp01(_t);
                    _pos = _parkPos + Forward * (leaveDistance * l * l);
                    if (_t >= 1f)
                    {
                        Phase = TruckPhase.Gone;
                        SetVisible(false);
                    }
                    break;
                default:
                    return;
            }
            chassis.MovePosition(_pos);
            chassis.MoveRotation(_parkRot);
            bed.MovePosition(_pos + _parkRot * hingeLocal);
            // Negative x rotation lifts the bed's +z (front) end about the rear hinge.
            bed.MoveRotation(_parkRot * Quaternion.Euler(-_angle, 0f, 0f));
        }

        void Teleport()
        {
            chassis.transform.SetPositionAndRotation(_pos, _parkRot);
            chassis.position = _pos;
            chassis.rotation = _parkRot;
            Quaternion bedRot = _parkRot * Quaternion.Euler(-_angle, 0f, 0f);
            Vector3 bedPos = _pos + _parkRot * hingeLocal;
            bed.transform.SetPositionAndRotation(bedPos, bedRot);
            bed.position = bedPos;
            bed.rotation = bedRot;
        }

        void SetVisible(bool visible)
        {
            if (chassis.gameObject.activeSelf != visible) chassis.gameObject.SetActive(visible);
            if (bed.gameObject.activeSelf != visible) bed.gameObject.SetActive(visible);
        }
    }
}
