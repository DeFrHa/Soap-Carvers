using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// A trigger volume in front of something climbable (ladder, scaffold
    /// ladder). PlayerMotor climbs when it overlaps one, presses forward and looks
    /// up/down. Climbing moves along this transform's +Y.
    ///
    /// Climbers push down on the body they climb (climberMass at their position),
    /// so a badly placed ladder slips once someone is on it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ClimbZone : MonoBehaviour
    {
        [Tooltip("Local Y (along this transform's up) of the top of the climbable part.")]
        [SerializeField] float topLocalY = 10f;
        [SerializeField] Rigidbody body;
        [Tooltip("Weight (kg) a climber adds to the body while climbing.")]
        [SerializeField] float climberMass = 25f;

        Holdable _holdable;
        Vector3 _climberPoint;
        float _lastClimbTime = -10f;

        public Vector3 Up => transform.up;
        public Vector3 TopPosition => transform.TransformPoint(0f, topLocalY, 0f);

        /// <summary>Not while carried, and only while roughly upright (a ladder lying on the ground isn't climbable).</summary>
        public bool IsClimbable => (_holdable == null || !_holdable.IsHeld) && Vector3.Dot(Up, Vector3.up) > 0.6f;

        public void Configure(float topY, Rigidbody climbedBody)
        {
            topLocalY = topY;
            body = climbedBody;
        }

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (body == null) body = GetComponentInParent<Rigidbody>();
            _holdable = GetComponentInParent<Holdable>();
        }

        /// <summary>PlayerMotor calls this every frame while climbing.</summary>
        public void NotifyClimbing(Vector3 climberPosition)
        {
            _climberPoint = climberPosition;
            _lastClimbTime = Time.time;
        }

        void FixedUpdate()
        {
            if (body == null || body.isKinematic || Time.time - _lastClimbTime > 0.1f) return;
            body.AddForceAtPosition(Physics.gravity * climberMass, _climberPoint, ForceMode.Force);
        }
    }
}
