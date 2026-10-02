using BuildCrew.Interaction;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>
    /// A wheelbarrow: grab a handle, lift and push. The wheel is its own
    /// rigidbody on a hinge. Its grab joint barely resists rotation, so it
    /// pivots on the wheel and tips over on bumps or when loaded unevenly.
    /// </summary>
    public class Wheelbarrow : Grabbable
    {
        [SerializeField] Vector3 centerOfMass = new Vector3(0f, 0.45f, 0.15f);

        public override string LookInfo => $"Wheelbarrow  ({Body.mass:0} kg)  - load it up, grab a handle";

        protected override void Awake()
        {
            base.Awake();
            SetAngularGrip(0.12f);
            Body.centerOfMass = centerOfMass;
            Body.solverIterations = 16;
        }
    }
}
