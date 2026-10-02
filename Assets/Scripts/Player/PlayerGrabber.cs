using BuildCrew.Core;
using BuildCrew.Interaction;
using UnityEngine;

namespace BuildCrew.Player
{
    /// <summary>
    /// This player's hand: a kinematic rigidbody (a separate root object, never
    /// a parent of anything) that the grab joint pulls the held object toward.
    /// Every FixedUpdate it moves to a point in front of the camera: hold
    /// distance along the view for normal objects (rotated by R + mouse), or
    /// the item's hold pose for grip items (tools).
    ///
    /// The joint keeps the object's rotation relative to the hand as it was
    /// when grabbed, so rotating the hand rotates the object.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class PlayerGrabber : MonoBehaviour, IGrabber
    {
        [SerializeField] int playerId = 1;
        [SerializeField] int teamId;
        [SerializeField] Transform aim;
        [SerializeField] CameraShake shake;

        Rigidbody _hand;
        PlayerInventory _inventory;
        PlayerMotor _motor;
        CharacterController _body;
        GameSettings _settings;

        Vector3 _handPos;
        Quaternion _userRotation = Quaternion.identity; // R + mouse, camera-local
        Quaternion _gripOffset = Quaternion.identity;
        Quaternion _gripRelativeAtGrab = Quaternion.identity;
        int _stepsSinceGrab;
        bool _gripMode;
        bool _rigid;
        float _rigidBlend;
        float _leashTimer;

        // --- IGrabber ---
        public int PlayerId => playerId;
        public int TeamId => teamId;
        public Rigidbody Hand => _hand;
        public Transform Aim => aim;
        public Transform Root => transform;
        public CameraShake Shake => shake;
        public PlayerInventory Inventory => _inventory;
        public Vector3 Velocity => _motor != null ? _motor.Velocity : Vector3.zero;
        public Grabbable Grabbed { get; private set; }
        public Vector3 GrabLocalPoint { get; private set; }

        public float HoldDistance { get; private set; } = 1.5f;
        /// <summary>Holding a tool by its grip (decided when grabbed).</summary>
        public bool HoldsWithGrip => Grabbed != null && _gripMode;
        /// <summary>The held object got stuck far from the hand; PlayerActions sends a release.</summary>
        public bool LeashBroken => _leashTimer > 0.6f;

        public void Configure(int id, int team, Transform aimTransform, CameraShake cameraShake)
        {
            playerId = id;
            teamId = team;
            aim = aimTransform;
            shake = cameraShake;
        }

        void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            _motor = GetComponent<PlayerMotor>();
            _body = GetComponent<CharacterController>();
            _settings = World.Settings;

            var handGo = new GameObject($"Hand P{playerId}");
            _hand = handGo.AddComponent<Rigidbody>();
            _hand.isKinematic = true;
            _hand.useGravity = false;
            _hand.interpolation = RigidbodyInterpolation.None;
            _handPos = aim != null ? aim.position : transform.position;
            _hand.position = _handPos;
        }

        void Start()
        {
            GrabManager grabs = World.Grabs;
            if (grabs != null) grabs.RegisterGrabber(this);
        }

        void OnDestroy()
        {
            GrabManager grabs = World.Grabs;
            if (grabs != null) grabs.UnregisterGrabber(this);
            if (_hand != null) Destroy(_hand.gameObject);
        }

        public void PrepareHand(Vector3 worldPoint)
        {
            _handPos = worldPoint;
            _hand.transform.SetPositionAndRotation(worldPoint, aim.rotation);
            _hand.position = worldPoint;
            _hand.rotation = aim.rotation;
        }

        public void OnGrabbed(Grabbable target, Vector3 localPoint)
        {
            Grabbed = target;
            GrabLocalPoint = localPoint;
            _gripMode = target.UsesGrip;
            _rigid = target.HeldRigidly;
            _rigidBlend = 0f;
            _userRotation = Quaternion.identity;
            _gripOffset = Quaternion.identity;
            _gripRelativeAtGrab = Quaternion.Inverse(aim.rotation) * target.transform.rotation;
            _stepsSinceGrab = 0;
            _leashTimer = 0f;
            HoldDistance = Mathf.Clamp(Vector3.Distance(aim.position, target.transform.TransformPoint(localPoint)),
                _settings.holdDistanceMin, _settings.holdDistanceMax);
            // Rigidly held tools have their colliders off (IgnoreCollision needs enabled colliders).
            if (!_rigid) SetIgnoreCollisions(target, true);
        }

        public void OnReleased()
        {
            if (Grabbed != null && !_rigid) SetIgnoreCollisions(Grabbed, false);
            Grabbed = null;
            _rigid = false;
            _leashTimer = 0f;
        }

        /// <summary>
        /// Our own body never collides with what we hold, so you can't stand on a
        /// plank you're carrying and fly away on it.
        /// </summary>
        void SetIgnoreCollisions(Grabbable target, bool ignore)
        {
            if (_body == null || target.Colliders == null) return;
            foreach (Collider c in target.Colliders)
                if (c != null) Physics.IgnoreCollision(_body, c, ignore);
        }

        public void AdjustDistance(float steps)
        {
            HoldDistance = Mathf.Clamp(HoldDistance + steps * _settings.holdScrollStep, _settings.holdDistanceMin, _settings.holdDistanceMax);
        }

        /// <summary>R + mouse: yaw around the camera's up, pitch around its right.</summary>
        public void Rotate(Vector2 mouseDelta)
        {
            float k = _settings.rotateSensitivity;
            _userRotation = Quaternion.AngleAxis(-mouseDelta.x * k, Vector3.up) *
                            Quaternion.AngleAxis(mouseDelta.y * k, Vector3.right) * _userRotation;
        }

        /// <summary>Walk speed multiplier: medium loads slow you, heavy drags crawl. Shared by co-carriers.</summary>
        public float CarrySpeedFactor
        {
            get
            {
                if (Grabbed == null || Grabbed.Body == null) return 1f;
                float share = Grabbed.Body.mass / Mathf.Max(1, Grabbed.GrabberCount);
                if (share <= _settings.lightMaxMass) return 1f;
                if (share <= _settings.mediumMaxMass)
                    return Mathf.Lerp(0.9f, 0.6f, Mathf.InverseLerp(_settings.lightMaxMass, _settings.mediumMaxMass, share));
                return _settings.heavyCarrySpeed;
            }
        }

        /// <summary>
        /// Rigidly held tools follow the camera exactly, after it moved this
        /// frame, so they are rock steady. They swoop in from where they were
        /// over the first ~0.15 s.
        /// </summary>
        void LateUpdate()
        {
            if (!_rigid || Grabbed == null || aim == null) return;
            Grabbed.GetHoldPose(out Vector3 lp, out Quaternion lr);
            Quaternion rot = aim.rotation * lr;
            Vector3 pos = aim.TransformPoint(lp) - rot * GrabLocalPoint;
            Transform t = Grabbed.transform;
            if (_rigidBlend < 1f)
            {
                _rigidBlend = Mathf.Min(1f, _rigidBlend + Time.deltaTime / 0.15f);
                float k = 1f - Mathf.Exp(-25f * Time.deltaTime);
                pos = _rigidBlend >= 1f ? pos : Vector3.Lerp(t.position, pos, k);
                rot = _rigidBlend >= 1f ? rot : Quaternion.Slerp(t.rotation, rot, k);
            }
            t.SetPositionAndRotation(pos, rot);
        }

        void FixedUpdate()
        {
            if (Grabbed == null || aim == null || _rigid)
            {
                _handPos = aim != null ? aim.position : transform.position;
                _leashTimer = 0f;
                return;
            }

            Vector3 targetPos;
            Quaternion targetRot;
            float dt = Time.fixedDeltaTime;
            if (_gripMode)
            {
                Grabbed.GetHoldPose(out Vector3 lp, out Quaternion lr);
                targetPos = aim.TransformPoint(lp);
                // Object = hand * relativeAtGrab, so hand = aim * pose * inverse(relativeAtGrab).
                // Wait two physics steps so the joint has captured its reference rotation first.
                Quaternion desired = lr * Quaternion.Inverse(_gripRelativeAtGrab);
                if (_stepsSinceGrab >= 2) _gripOffset = Quaternion.Slerp(_gripOffset, desired, 1f - Mathf.Exp(-14f * dt));
                targetRot = aim.rotation * _gripOffset;
            }
            else
            {
                targetPos = aim.position + aim.forward * HoldDistance;
                targetRot = aim.rotation * _userRotation;
            }

            _handPos = Vector3.MoveTowards(_handPos, targetPos, _settings.handMaxSpeed * dt);
            _hand.MovePosition(_handPos);
            _hand.MoveRotation(targetRot);
            _stepsSinceGrab++;

            Vector3 grabPoint = Grabbed.transform.TransformPoint(GrabLocalPoint);
            if (Vector3.Distance(grabPoint, _handPos) > _settings.leashDistance) _leashTimer += dt;
            else _leashTimer = 0f;
        }
    }
}
