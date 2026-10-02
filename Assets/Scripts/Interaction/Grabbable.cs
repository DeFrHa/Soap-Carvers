using System.Collections.Generic;
using BuildCrew.Core;
using UnityEngine;

namespace BuildCrew.Interaction
{
    /// <summary>
    /// Any physical object players can grab: parts, tools, buckets, the ladder,
    /// the wheelbarrow, pallets. It is ALWAYS a dynamic rigidbody with real
    /// mass; it is never parented to a player.
    ///
    /// Grabbing adds a ConfigurableJoint at the grabbed point, connected to the
    /// grabber's kinematic hand body. Linear drives pull the point to the hand
    /// with a spring of stiffness mass * f^2, capped at the player's grab
    /// strength; a slerp drive keeps the orientation relative to the hand,
    /// capped at a max torque. So light things follow snappily, medium things
    /// lag and sag, heavy things (weight > strength) can only be dragged, and
    /// several grabbers simply add their forces.
    ///
    /// Exception: grip items (tools) are held rigidly, see <see cref="HeldRigidly"/>.
    ///
    /// Only <see cref="GrabManager"/> attaches/detaches grabbers (via commands).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        [SerializeField] int entityId;
        [SerializeField] string displayName = "Thing";
        [Tooltip("Tools: always grabbed at the grip point and held in a pose in front of the camera.")]
        [SerializeField] bool useGrip;
        [SerializeField] Vector3 gripLocal;
        [Tooltip("Hold pose relative to the camera (grip items only).")]
        [SerializeField] Vector3 holdPosition = new Vector3(0.28f, -0.26f, 0.62f);
        [SerializeField] Vector3 holdEuler;
        [Tooltip("Multiplier on the angular grab spring/torque. Low = the object pivots freely (wheelbarrow).")]
        [SerializeField] float angularGrip = 1f;
        [SerializeField] bool spawnedAtRuntime;

        struct Link
        {
            public IGrabber Grabber;
            public ConfigurableJoint Joint; // null when held rigidly
        }

        readonly List<Link> _links = new List<Link>(2);
        Vector3 _homePosition;
        Quaternion _homeRotation;

        public int EntityId => entityId;
        public virtual string DisplayName => displayName;
        public Rigidbody Body { get; private set; }
        public Collider[] Colliders { get; private set; }
        /// <summary>Grabbed at the grip and held in a pose (tools). Decided at grab time.</summary>
        public virtual bool UsesGrip => useGrip;

        /// <summary>
        /// Grip items are held RIGIDLY: kinematic, colliders off, locked to the
        /// hold pose in front of the camera (PlayerGrabber.LateUpdate), so tools
        /// are steady in the hand. Back to a normal dynamic body on release.
        /// Override to false for big grip items that should stay physical (ladder).
        /// </summary>
        public virtual bool HeldRigidly => UsesGrip;
        public Vector3 GripLocal => gripLocal;
        public bool SpawnedAtRuntime => spawnedAtRuntime;
        public int GrabberCount => _links.Count;
        public bool IsGrabbed => _links.Count > 0;
        public IGrabber PrimaryGrabber => _links.Count > 0 ? _links[0].Grabber : null;
        public WeightClass Weight => WeightClasses.Of(Body != null ? Body.mass : 1f, World.Settings);

        public virtual bool CanBeGrabbed => true;

        public virtual string GrabPrompt
        {
            get
            {
                string extra = Weight == WeightClass.Heavy ? "  (heavy: drag it or lift together)" : string.Empty;
                return IsGrabbed ? $"E: Help carry {DisplayName}{extra}" : $"E: Grab {DisplayName}{extra}";
            }
        }

        /// <summary>Text shown under the crosshair when looking at it.</summary>
        public virtual string LookInfo => $"{DisplayName}  ({WeightClasses.Label(Weight)}, {Body.mass:0.#} kg)";

        /// <summary>Control hint shown on the HUD while held.</summary>
        public virtual string HeldHint => "E: Release   G: Throw   Scroll: Distance   Hold R + Mouse: Rotate";

        public void Configure(int id, string niceName)
        {
            entityId = id;
            displayName = niceName;
        }

        /// <summary>Grip items (tools): grab point and hold pose relative to the camera.</summary>
        public void ConfigureGrip(Vector3 grip, Vector3 holdPos, Vector3 holdRot)
        {
            useGrip = true;
            gripLocal = grip;
            holdPosition = holdPos;
            holdEuler = holdRot;
        }

        public void SetAngularGrip(float multiplier) => angularGrip = multiplier;
        public void MarkSpawnedAtRuntime() => spawnedAtRuntime = true;

        /// <summary>Only the EntityRegistry calls this (id collisions / runtime spawns).</summary>
        public void AssignId(int id) => entityId = id;

        /// <summary>Hold pose relative to the camera. Tools override to animate (swings, sawing strokes).</summary>
        public virtual void GetHoldPose(out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = holdPosition;
            localRotation = Quaternion.Euler(holdEuler);
        }

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            RefreshColliders();
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            EntityRegistry registry = World.Registry;
            if (registry != null) registry.Register(this);
        }

        protected virtual void OnDestroy()
        {
            GrabManager grabs = World.Grabs;
            if (grabs != null && IsGrabbed) grabs.ReleaseAll(this);
            EntityRegistry registry = World.Registry;
            if (registry != null) registry.Unregister(this);
        }

        public void RefreshColliders()
        {
            var list = new List<Collider>();
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && c.attachedRigidbody == Body) list.Add(c);
            Colliders = list.ToArray();
        }

        void SetCollidersEnabled(bool enabled)
        {
            foreach (Collider c in Colliders)
                if (c != null) c.enabled = enabled;
        }

        public bool IsGrabbedBy(IGrabber grabber)
        {
            foreach (Link l in _links)
                if (l.Grabber == grabber) return true;
            return false;
        }

        /// <summary>Creates the grab joint (or the rigid hold). Called by GrabManager after the hand was moved to the grab point.</summary>
        public void AttachGrabber(IGrabber grabber, Vector3 localPoint, GameSettings s)
        {
            if (HeldRigidly)
            {
                Body.isKinematic = true;
                Body.interpolation = RigidbodyInterpolation.None; // the transform is driven every frame
                SetCollidersEnabled(false);
                _links.Add(new Link { Grabber = grabber, Joint = null });
                return;
            }

            var joint = gameObject.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedBody = grabber.Hand;
            joint.anchor = localPoint;
            joint.connectedAnchor = Vector3.zero;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.enablePreprocessing = false;
            joint.enableCollision = false;

            // Linear spring: critically-ish damped mass-spring with natural frequency f.
            float m = Body.mass;
            float k = m * s.grabFrequency * s.grabFrequency;
            float c = 2f * s.grabDampingRatio * Mathf.Sqrt(k * m);
            var linear = new JointDrive { positionSpring = k, positionDamper = c, maximumForce = s.grabStrength };
            joint.xDrive = linear;
            joint.yDrive = linear;
            joint.zDrive = linear;

            // Angular spring about the grab point: inertia ~ mean principal moment + m r^2.
            Vector3 it = Body.inertiaTensor;
            float r2 = (localPoint - Body.centerOfMass).sqrMagnitude;
            float inertia = (it.x + it.y + it.z) / 3f + m * r2;
            float ka = inertia * s.grabAngularFrequency * s.grabAngularFrequency * angularGrip;
            float ca = 2f * 0.9f * Mathf.Sqrt(ka * inertia);
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.slerpDrive = new JointDrive { positionSpring = ka, positionDamper = ca, maximumForce = s.grabMaxTorque * angularGrip };

            _links.Add(new Link { Grabber = grabber, Joint = joint });
            Body.WakeUp();
        }

        public bool DetachGrabber(IGrabber grabber)
        {
            for (int i = 0; i < _links.Count; i++)
            {
                if (_links[i].Grabber != grabber) continue;
                if (_links[i].Joint != null) Destroy(_links[i].Joint);
                else
                {
                    // Rigid hold ends: a normal dynamic body again, moving with the player.
                    SetCollidersEnabled(true);
                    Body.isKinematic = false;
                    Body.interpolation = RigidbodyInterpolation.Interpolate;
                    Body.linearVelocity = grabber.Velocity;
                    Body.angularVelocity = Vector3.zero;
                }
                _links.RemoveAt(i);
                Body.WakeUp();
                return true;
            }
            return false;
        }

        /// <summary>Grabbers attached, in grab order (primary first).</summary>
        public IEnumerable<IGrabber> Grabbers
        {
            get
            {
                foreach (Link l in _links) yield return l.Grabber;
            }
        }

        /// <summary>Called by GrabManager after a grabber attached.</summary>
        public virtual void OnGrabStarted(IGrabber grabber) { }

        /// <summary>Called by GrabManager after a grabber let go.</summary>
        public virtual void OnGrabEnded(IGrabber grabber) { }

        /// <summary>Called by GrabManager for a Place command, after everyone let go.</summary>
        public virtual void PlaceAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            Body.position = position;
            Body.rotation = rotation;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.WakeUp();
        }

        /// <summary>Restart: back to the spawn pose, at rest.</summary>
        public virtual void ResetToHome()
        {
            gameObject.SetActive(true);
            if (Body.isKinematic && _links.Count == 0)
            {
                Body.isKinematic = false;
                SetCollidersEnabled(true);
            }
            PlaceAt(_homePosition, _homeRotation);
        }
    }
}
