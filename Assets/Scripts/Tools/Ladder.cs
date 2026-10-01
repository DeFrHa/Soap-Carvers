using SoapCarvers.Core;
using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// A long, awkward, fully physical ladder. Pivot = bottom center, local +Y
    /// runs up the rails, and you climb it from its local -Z side (a ClimbZone
    /// trigger child sits there).
    ///
    /// Placing (E while carrying) just releases it at a leaning pose with zero
    /// velocity: grippy feet and a wall to rest against keep it up; stand it in
    /// open air (or lean it too far) and physics knocks it over. Climbers add
    /// their weight via the ClimbZone, so marginal placements slip under load.
    /// </summary>
    public class Ladder : Holdable
    {
        [SerializeField] float length = 12f;
        [Tooltip("Lean angle from vertical when placed against a wall. Steeper = more stable.")]
        [SerializeField] float leanDegrees = 16f;
        [SerializeField] int solverIterations = 12;

        public float Length => length;
        public override bool CanBePlaced => true;
        public override string PickupPrompt => "E: Pick up Ladder (it's big)";
        public override string HeldHint => "E: Place ladder   Q: Drop   G: Throw";

        public void SetDimensions(float ladderLength, float lean)
        {
            length = ladderLength;
            leanDegrees = lean;
        }

        protected override void Awake()
        {
            base.Awake();
            // Grippy feet/rails so a well-leaned ladder holds; mass centered on the rails.
            foreach (Collider c in Colliders)
                if (c != null && !c.isTrigger) c.sharedMaterial = PhysicsMaterials.Grippy;
            Body.centerOfMass = new Vector3(0f, length * 0.5f, 0f);
            Body.solverIterations = solverIterations;
        }

        public override bool TryGetPlacement(IItemHolder holder, out Vector3 position, out Quaternion rotation, out string prompt)
        {
            bool againstWall = ComputePlacement(holder.AimTransform, holder.Root.position, holder.Root, out position, out rotation);
            prompt = againstWall ? "E: Lean ladder against the wall" : "E: Stand ladder up (nothing to lean on - it will fall!)";
            return true;
        }

        /// <summary>
        /// Computes a placement leaning toward where the player looks. If a wall
        /// (the soap) is ahead, the base is set back so the top just touches it
        /// (returns true); otherwise the base goes ~1.2 m ahead with nothing to
        /// lean on (returns false, and it will topple once released).
        /// </summary>
        public bool ComputePlacement(Transform aim, Vector3 feet, Transform ignoreRoot, out Vector3 basePos, out Quaternion rotation)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(aim.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(aim.up, Vector3.up);
            fwd.Normalize();

            // Horizontal distance the top travels when leaning; +5 cm so it starts just off the wall.
            float setBack = length * Mathf.Sin(leanDegrees * Mathf.Deg2Rad) + 0.05f;
            Vector3 eye = new Vector3(feet.x, feet.y + 1.5f, feet.z);
            bool wall = PhysicsUtil.Raycast(eye, fwd, setBack + 4f, ignoreRoot, QueryTriggerInteraction.Ignore, out RaycastHit hit);
            basePos = wall ? hit.point - fwd * setBack : feet + fwd * 1.2f;

            // Drop onto whatever is below (ground, or a soap ledge), 2 cm above to avoid starting inside it.
            Vector3 probe = new Vector3(basePos.x, feet.y + 2f, basePos.z);
            basePos.y = (PhysicsUtil.Raycast(probe, Vector3.down, 6f, ignoreRoot, QueryTriggerInteraction.Ignore, out RaycastHit ground)
                ? ground.point.y
                : feet.y) + 0.02f;

            // Positive X rotation tips local +Y toward local +Z, i.e. toward fwd.
            rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(leanDegrees, 0f, 0f);
            return wall;
        }
    }
}
