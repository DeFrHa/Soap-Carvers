using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// A physical item that can be picked up, carried in front of the camera,
    /// dropped and thrown. Ownership changes only via <see cref="ItemManager"/>;
    /// the OnAttached/OnReleased hooks just apply the visual/physics result.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Holdable : MonoBehaviour
    {
        [SerializeField] int itemId;
        [SerializeField] string displayName = "Thing";
        [Tooltip("Local position relative to the holder's HoldPoint.")]
        [SerializeField] protected Vector3 holdPosition = new Vector3(0.35f, -0.3f, 0.55f);
        [SerializeField] protected Vector3 holdEuler = Vector3.zero;
        [Tooltip("How snappily the item follows its hold pose. Lower = wobblier.")]
        [SerializeField] protected float holdFollowSpeed = 16f;
        [SerializeField] bool spawnedAtRuntime;

        public int ItemId => itemId;
        public string DisplayName => displayName;
        public IItemHolder Holder { get; private set; }
        public bool IsHeld => Holder != null;
        public bool SpawnedAtRuntime => spawnedAtRuntime;
        public virtual bool CanPickUp => true;
        public virtual string PickupPrompt => $"E: Pick up {displayName}";
        /// <summary>Control hint shown on the HUD while held.</summary>
        public virtual string HeldHint => "Q: Drop   G: Throw";

        protected Rigidbody Body { get; private set; }
        protected ItemManager Manager { get; private set; }
        protected Collider[] Colliders { get; private set; }

        Vector3 _homePosition;
        Quaternion _homeRotation;

        public void Configure(int id, string niceName, Vector3 holdPos, Vector3 holdRot)
        {
            itemId = id;
            displayName = niceName;
            holdPosition = holdPos;
            holdEuler = holdRot;
        }

        public void MarkSpawnedAtRuntime() => spawnedAtRuntime = true;

        /// <summary>Only ItemManager calls this (id collisions / runtime spawns).</summary>
        public void AssignId(int id) => itemId = id;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Colliders = GetComponentsInChildren<Collider>(true);
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            Manager = FindFirstObjectByType<ItemManager>();
            if (Manager != null) Manager.Register(this);
        }

        protected virtual void OnDestroy()
        {
            if (Manager != null) Manager.Unregister(this);
        }

        /// <summary>Called by ItemManager after it assigned this item to a holder.</summary>
        public virtual void OnAttached(IItemHolder holder)
        {
            Holder = holder;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            SetCollidersEnabled(false);
            // Keep the world pose so the item swoops from where it was into the hand.
            transform.SetParent(holder.HoldPoint, true);
        }

        /// <summary>Called by ItemManager after removing this item from its holder.</summary>
        public virtual void OnReleased(Vector3 velocity)
        {
            Holder = null;
            transform.SetParent(null, true);
            SetCollidersEnabled(true);
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.linearVelocity = velocity;
            Body.angularVelocity = velocity.sqrMagnitude > 4f ? Random.insideUnitSphere * 6f : Vector3.zero;
        }

        protected virtual void GetHoldPose(out Vector3 localPosition, out Quaternion localRotation)
        {
            localPosition = holdPosition;
            localRotation = Quaternion.Euler(holdEuler);
        }

        protected virtual void LateUpdate()
        {
            if (!IsHeld) return;
            GetHoldPose(out Vector3 pos, out Quaternion rot);
            // Exponential smoothing: frame-rate independent "lag behind the hand".
            float k = 1f - Mathf.Exp(-holdFollowSpeed * Time.deltaTime);
            transform.localPosition = Vector3.Lerp(transform.localPosition, pos, k);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, rot, k);
        }

        protected void SetCollidersEnabled(bool enabled)
        {
            foreach (Collider c in Colliders)
                if (c != null) c.enabled = enabled;
        }

        /// <summary>Restart: back to the spawn pose, at rest.</summary>
        public virtual void ResetToHome()
        {
            gameObject.SetActive(true);
            transform.SetParent(null, true);
            Holder = null;
            SetCollidersEnabled(true);
            Body.isKinematic = false;
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            Body.position = _homePosition;
            Body.rotation = _homeRotation;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }
    }
}
