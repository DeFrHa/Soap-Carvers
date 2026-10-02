using BuildCrew.Core;
using BuildCrew.Interaction;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>
    /// A long, awkward, fully physical ladder. Pivot = bottom center, local +Y
    /// runs up the rails, and you climb it from its local -Z side (a ClimbZone
    /// trigger child sits there).
    ///
    /// LMB while carrying: lean it against whatever you look at (PlaceCommand:
    /// it is released at a leaning pose, then physics takes over). Climbers add
    /// their weight via the ClimbZone and make it wobble. If nobody is holding
    /// it while someone climbs, there is a small chance per second that the
    /// feet slip. Grab it while a friend climbs to keep it steady.
    /// </summary>
    public class Ladder : Tool
    {
        [SerializeField] float length = 6f;
        [Tooltip("Lean angle from vertical when placed against a wall. Steeper = more stable.")]
        [SerializeField] float leanDegrees = 16f;
        [SerializeField] int solverIterations = 16;
        [SerializeField] ClimbZone climbZone;

        float _slipCooldown;

        public float Length => length;

        /// <summary>Standing up: grab it where you touch it (to steady it), not by the carry grip.</summary>
        public bool IsUpright => Vector3.Dot(transform.up, Vector3.up) > 0.6f;
        public override bool UsesGrip => base.UsesGrip && !IsUpright;
        public override bool WorksOutsideBuild => true;
        public override string LookInfo => $"Ladder {length:0.#} m  ({Body.mass:0} kg)";
        public override string HeldHint => IsUpright && !UsesGripNow
            ? "Holding the ladder steady   E: Let go"
            : "LMB: Lean it against the wall you look at   E: Drop";

        bool UsesGripNow => Holder is Player.PlayerGrabber g && g.HoldsWithGrip;

        public void SetDimensions(float ladderLength, float lean, ClimbZone zone)
        {
            length = ladderLength;
            leanDegrees = lean;
            climbZone = zone;
        }

        protected override void Awake()
        {
            base.Awake();
            // Grippy feet/rails so a well-leaned ladder holds; mass centered on the rails.
            foreach (Collider c in Colliders)
                if (c != null) c.sharedMaterial = PhysicsMaterials.Grippy;
            Body.centerOfMass = new Vector3(0f, length * 0.5f, 0f);
            Body.solverIterations = solverIterations;
            if (climbZone == null) climbZone = GetComponentInChildren<ClimbZone>();
        }

        public override void Use()
        {
            IGrabber h = Holder;
            if (h == null) return;
            ComputePlacement(h.Aim, h.Root.position, h.Root, out Vector3 pos, out Quaternion rot);
            Send(new PlaceCommand { EntityId = EntityId, Position = pos, Rotation = rot });
        }

        public override string StatusText
        {
            get
            {
                IGrabber h = Holder;
                if (h == null) return null;
                bool wall = ComputePlacement(h.Aim, h.Root.position, h.Root, out _, out _);
                return wall ? "LMB: lean it against the wall" : "Nothing to lean on here - it will fall over!";
            }
        }

        /// <summary>
        /// A placement leaning toward where the player looks. If a wall is ahead,
        /// the base is set back so the top just touches it (returns true);
        /// otherwise the base goes ~1.2 m ahead with nothing to lean on (false).
        /// </summary>
        public bool ComputePlacement(Transform aim, Vector3 feet, Transform ignoreRoot, out Vector3 basePos, out Quaternion rotation)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(aim.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(aim.up, Vector3.up);
            fwd.Normalize();

            // Horizontal distance the top travels when leaning; +5 cm so it starts just off the wall.
            float setBack = length * Mathf.Sin(leanDegrees * Mathf.Deg2Rad) + 0.05f;
            Vector3 eye = new Vector3(feet.x, feet.y + 1.5f, feet.z);
            bool wall = PhysicsUtil.Raycast(eye, fwd, setBack + 4f, ignoreRoot, QueryTriggerInteraction.Ignore, out RaycastHit hit, transform);
            basePos = wall ? hit.point - fwd * setBack : feet + fwd * 1.2f;

            // Drop onto whatever is below, 2 cm above to avoid starting inside it.
            Vector3 probe = new Vector3(basePos.x, feet.y + 2f, basePos.z);
            basePos.y = (PhysicsUtil.Raycast(probe, Vector3.down, 6f, ignoreRoot, QueryTriggerInteraction.Ignore, out RaycastHit ground, transform)
                ? ground.point.y
                : feet.y) + 0.02f;

            // Positive X rotation tips local +Y toward local +Z, i.e. toward fwd.
            rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(leanDegrees, 0f, 0f);
            return wall;
        }

        void FixedUpdate()
        {
            if (climbZone == null || !climbZone.IsBeingClimbed || Body.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            // Wobble: a little sideways shove at the climber's height.
            Vector3 top = transform.TransformPoint(0f, length * 0.8f, 0f);
            Body.AddForceAtPosition(transform.right * Mathf.Sin(Time.time * 7f) * 25f, top, ForceMode.Force);

            if (IsGrabbed) return; // a helper holds it steady
            _slipCooldown -= dt;
            if (_slipCooldown > 0f || Random.value > Settings.ladderSlipChance * dt) return;
            _slipCooldown = 2f;
            // The feet kick out away from the wall.
            Vector3 away = -Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
            Body.AddForceAtPosition(away * 40f + Vector3.up * 5f, transform.position, ForceMode.Impulse);
        }
    }
}
