using SoapCarvers.Core;
using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// A long, awkward ladder. Pivot = bottom center, local +Y runs up the rails,
    /// and you climb it from its local -Z side.
    ///
    /// Placed: kinematic, colliders become triggers so players can walk "into"
    /// it (PlayerMotor detects the overlap and climbs), and it wobbles a bit.
    /// Carried / dropped: a normal physics object.
    /// </summary>
    public class Ladder : Holdable
    {
        [SerializeField] float length = 12f;
        [SerializeField] float leanDegrees = 16f;
        [SerializeField] bool startPlaced = true;
        [SerializeField] float idleWobbleDegrees = 0.35f;
        [SerializeField] float climbWobbleDegrees = 2.2f;

        public bool IsPlaced { get; private set; }
        public float Length => length;
        public override string PickupPrompt => "E: Pick up Ladder (it's big)";
        public override string HeldHint => "E: Place ladder   Q: Drop   G: Throw";

        Quaternion _placedRotation;
        float _climbWobble;
        float _seed;

        public void SetDimensions(float ladderLength, float lean, bool placedAtStart)
        {
            length = ladderLength;
            leanDegrees = lean;
            startPlaced = placedAtStart;
        }

        protected override void Awake()
        {
            base.Awake();
            _seed = Random.value * 100f;
            if (startPlaced) Place(transform.position, transform.rotation);
        }

        /// <summary>Called by ItemManager for a PlaceLadder command (after release).</summary>
        public void Place(Vector3 basePosition, Quaternion rotation)
        {
            IsPlaced = true;
            _placedRotation = rotation;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            transform.SetPositionAndRotation(basePosition, rotation);
            SetCollidersEnabled(true);
            SetTriggers(true);
        }

        public override void OnAttached(IItemHolder holder)
        {
            IsPlaced = false;
            SetTriggers(false);
            base.OnAttached(holder);
        }

        public override void OnReleased(Vector3 velocity)
        {
            IsPlaced = false;
            SetTriggers(false);
            base.OnReleased(velocity);
        }

        public override void ResetToHome()
        {
            IsPlaced = false;
            SetTriggers(false);
            base.ResetToHome();
            if (startPlaced) Place(transform.position, transform.rotation);
        }

        /// <summary>PlayerMotor calls this every frame someone climbs.</summary>
        public void NotifyClimbing() => _climbWobble = 1f;

        /// <summary>World-space height of the top rung area.</summary>
        public Vector3 TopPosition => transform.position + transform.up * length;

        void Update()
        {
            if (!IsPlaced) return;
            _climbWobble = Mathf.MoveTowards(_climbWobble, 0f, Time.deltaTime * 1.5f);
            float amp = idleWobbleDegrees + climbWobbleDegrees * _climbWobble;
            float t = Time.time + _seed;
            // Two incommensurate sines = organic-looking sway.
            var wobble = Quaternion.Euler(Mathf.Sin(t * 1.7f) * amp, 0f, Mathf.Sin(t * 1.13f + 1f) * amp * 0.7f);
            transform.rotation = _placedRotation * wobble;
        }

        /// <summary>
        /// Computes where a held ladder would be placed: leaning toward where the
        /// player looks. If a wall (the soap) is ahead, the base is set back so
        /// the top rests against it; otherwise the base goes ~1.2 m ahead.
        /// </summary>
        public void ComputePlacement(Transform aim, Vector3 feet, Transform ignoreRoot, out Vector3 basePos, out Quaternion rotation)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(aim.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(aim.up, Vector3.up);
            fwd.Normalize();

            float setBack = length * Mathf.Sin(leanDegrees * Mathf.Deg2Rad);
            Vector3 eye = new Vector3(feet.x, feet.y + 1.5f, feet.z);
            if (PhysicsUtil.Raycast(eye, fwd, setBack + 4f, ignoreRoot, QueryTriggerInteraction.Ignore, out RaycastHit wall))
                basePos = wall.point - fwd * setBack;
            else
                basePos = feet + fwd * 1.2f;

            // Drop onto whatever is below (ground, or a soap ledge).
            Vector3 probe = new Vector3(basePos.x, feet.y + 2f, basePos.z);
            basePos.y = PhysicsUtil.Raycast(probe, Vector3.down, 6f, ignoreRoot, QueryTriggerInteraction.Ignore, out RaycastHit ground)
                ? ground.point.y
                : feet.y;

            // Positive X rotation tips local +Y toward local +Z, i.e. toward fwd.
            rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(leanDegrees, 0f, 0f);
        }

        void SetTriggers(bool trigger)
        {
            foreach (Collider c in Colliders)
                if (c != null) c.isTrigger = trigger;
        }
    }
}
