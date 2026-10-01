using UnityEngine;

namespace SoapCarvers.Core
{
    /// <summary>
    /// Shared physics materials, created at runtime on first use (so nothing has
    /// to be saved as an asset). Components assign them to their colliders in Awake.
    /// </summary>
    public static class PhysicsMaterials
    {
        static PhysicsMaterial _grippy;
        static PhysicsMaterial _frictionless;

        /// <summary>Ladder feet, braked wheels: high friction, wins over the other surface.</summary>
        public static PhysicsMaterial Grippy
        {
            get
            {
                if (_grippy == null)
                {
                    _grippy = new PhysicsMaterial("Grippy")
                    {
                        staticFriction = 1.2f,
                        dynamicFriction = 0.9f,
                        bounciness = 0f,
                        frictionCombine = PhysicsMaterialCombine.Maximum,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                    };
                }
                return _grippy;
            }
        }

        /// <summary>Unbraked caster wheels: slide freely (damping on the body provides rolling resistance).</summary>
        public static PhysicsMaterial Frictionless
        {
            get
            {
                if (_frictionless == null)
                {
                    _frictionless = new PhysicsMaterial("Frictionless")
                    {
                        staticFriction = 0f,
                        dynamicFriction = 0f,
                        bounciness = 0f,
                        frictionCombine = PhysicsMaterialCombine.Minimum,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                    };
                }
                return _frictionless;
            }
        }
    }
}
