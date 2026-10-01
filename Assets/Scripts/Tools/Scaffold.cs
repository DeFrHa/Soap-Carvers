using SoapCarvers.Core;
using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// A rolling scaffold tower: a dynamic rigidbody on four caster "wheels"
    /// (sphere colliders), with a deck on top and a built-in ladder (ClimbZone)
    /// on its -Z face.
    ///
    /// Brakes on (default): wheels are grippy and the body heavily damped, so it
    /// stays put while you stand on it. Press E to release the brakes, then walk
    /// into it to push it (PlayerMotor calls <see cref="Push"/>). The push is a
    /// horizontal force at the center of mass, capped at a walking-ish speed; the
    /// low center of mass keeps it from tipping unless something (dynamite) really
    /// wants it to.
    ///
    /// Networking note: brake toggles come in through IInteractable.Interact (a
    /// request) and the tower's pose is physics state that a network transform
    /// would sync.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Scaffold : MonoBehaviour, IInteractable, IPushable
    {
        [SerializeField] Collider[] wheels;
        [SerializeField] float pushForce = 1400f;
        [SerializeField] float maxPushSpeed = 2.2f;
        [SerializeField] Vector3 centerOfMass = new Vector3(0f, 0.6f, 0f);
        [SerializeField] bool brakesOn = true;
        [SerializeField] float rollingDamping = 0.8f;
        [SerializeField] float brakedDamping = 6f;

        Rigidbody _rb;
        Vector3 _homePosition;
        Quaternion _homeRotation;
        bool _homeBrakes;
        Vector3 _pushDirection;
        float _lastPushTime = -10f;

        public bool BrakesOn => brakesOn;

        public void Configure(Collider[] wheelColliders, float mass)
        {
            wheels = wheelColliders;
            GetComponent<Rigidbody>().mass = mass;
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = centerOfMass;
            _rb.angularDamping = 2f;
            _rb.solverIterations = 10;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            _homeBrakes = brakesOn;
            ApplyBrakes();
        }

        public string GetPrompt(IItemHolder who) =>
            brakesOn ? "E: Release wheel brakes (then walk into it to push)" : "E: Lock wheel brakes";

        public void Interact(IItemHolder who)
        {
            brakesOn = !brakesOn;
            ApplyBrakes();
        }

        public void Push(Vector3 direction)
        {
            if (brakesOn) return;
            _pushDirection = direction;
            _lastPushTime = Time.time;
        }

        void FixedUpdate()
        {
            if (Time.time - _lastPushTime > 0.1f) return;
            float along = Vector3.Dot(_rb.linearVelocity, _pushDirection);
            if (along < maxPushSpeed)
                _rb.AddForce(_pushDirection * pushForce, ForceMode.Force); // at the COM: no tipping torque
        }

        void ApplyBrakes()
        {
            PhysicsMaterial m = brakesOn ? PhysicsMaterials.Grippy : PhysicsMaterials.Frictionless;
            if (wheels != null)
                foreach (Collider w in wheels)
                    if (w != null) w.sharedMaterial = m;
            _rb.linearDamping = brakesOn ? brakedDamping : rollingDamping;
            _rb.WakeUp();
        }

        public void ResetToHome()
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            _rb.position = _homePosition;
            _rb.rotation = _homeRotation;
            brakesOn = _homeBrakes;
            ApplyBrakes();
        }
    }
}
