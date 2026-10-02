using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>
    /// A trigger volume in front of something climbable (the ladder).
    /// PlayerMotor climbs when it overlaps one, presses forward and looks
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

        Interaction.Grabbable _grabbable;
        Vector3 _climberPoint;
        float _lastClimbTime = -10f;

        public Vector3 Up => transform.up;
        public Vector3 TopPosition => transform.TransformPoint(0f, topLocalY, 0f);

        /// <summary>Only while roughly upright (a ladder lying on the ground isn't climbable).
        /// PlayerMotor also refuses a ladder the climber is holding himself.</summary>
        public bool IsClimbable => Vector3.Dot(Up, Vector3.up) > 0.6f;

        /// <summary>The ladder (or whatever) this zone belongs to.</summary>
        public Interaction.Grabbable Owner => _grabbable;

        /// <summary>A player climbed within the last moment.</summary>
        public bool IsBeingClimbed => Time.time - _lastClimbTime < 0.2f;

        public void Configure(float topY, Rigidbody climbedBody)
        {
            topLocalY = topY;
            body = climbedBody;
        }

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (body == null) body = GetComponentInParent<Rigidbody>();
            _grabbable = GetComponentInParent<Interaction.Grabbable>();
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
