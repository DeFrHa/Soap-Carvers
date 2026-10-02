using BuildCrew.Core;
using BuildCrew.Tools;
using UnityEngine;

namespace BuildCrew.Player
{
    /// <summary>
    /// CharacterController movement: WASD, Shift sprint, Space jump, a bit floaty
    /// (low gravity, some air control), climbing (overlap a ladder's ClimbZone,
    /// press forward, look up to climb up / down to climb down). Carrying medium
    /// or heavy things slows you down (PlayerGrabber.CarrySpeedFactor). Frozen
    /// during the final test.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Move")]
        [SerializeField] float walkSpeed = 5f;
        [SerializeField] float sprintMultiplier = 1.7f;
        [SerializeField] float groundAcceleration = 12f;
        [SerializeField] float airAcceleration = 3f;
        [Header("Jump / gravity")]
        [SerializeField] float gravity = -13f;          // floatier than -9.81 * 1.5 "game" gravity
        [SerializeField] float jumpHeight = 1.3f;
        [Header("Ladder")]
        [SerializeField] float climbSpeed = 3.2f;
        [SerializeField] float climbUpPitch = 12f;    // look at least this far up to climb up
        [SerializeField] float climbDownPitch = 35f;  // look this far down to climb down (so you can still walk past)
        [Header("Misc")]
        [SerializeField] float pushPower = 1.5f;
        [Tooltip("Rigidbodies heavier than this aren't nudged by walking into them.")]
        [SerializeField] float lightObjectMass = 4f;
        [SerializeField] float impulseDamping = 2.5f;

        CharacterController _cc;
        PlayerInputHandler _input;
        PlayerLook _look;
        PlayerGrabber _grabber;
        Vector3 _planarVelocity;
        float _verticalVelocity;
        Vector3 _impulse; // decaying external horizontal velocity (explosions)

        public Vector3 Velocity => _cc != null ? _cc.velocity : Vector3.zero;
        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public bool IsClimbing { get; private set; }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputHandler>();
            _look = GetComponent<PlayerLook>();
            _grabber = GetComponent<PlayerGrabber>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            GameManager game = World.Game;
            bool frozen = game != null && game.State == GameState.FinalTest;
            Vector2 move = _input != null && !frozen ? _input.Move : Vector2.zero;
            bool sprint = _input != null && _input.SprintHeld;
            bool jump = _input != null && !frozen && _input.JumpPressed;

            Vector3 wish = transform.right * move.x + transform.forward * move.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            float speed = walkSpeed * (sprint ? sprintMultiplier : 1f) * (_grabber != null ? _grabber.CarrySpeedFactor : 1f);

            ClimbZone zone = FindClimbZone();
            float pitch = _look != null ? _look.Pitch : 0f;
            bool lookUp = pitch < -climbUpPitch, lookDown = pitch > climbDownPitch;
            bool wantsClimb = zone != null && move.y > 0.1f && (lookUp || lookDown);

            Vector3 motion;
            if (wantsClimb)
            {
                IsClimbing = true;
                zone.NotifyClimbing(transform.position + Vector3.up * 0.9f);
                float dir = lookUp ? 1f : -1f;
                // Move along the ladder's rails, so leaning ladders carry you toward the wall.
                Vector3 climb = zone.Up * (climbSpeed * dir * move.y);
                // Hop off the top so you can get onto a ledge / scaffold deck.
                if (dir > 0f && transform.position.y > zone.TopPosition.y - 0.6f)
                    climb += transform.forward * 2.5f + Vector3.up * 1.5f;
                _verticalVelocity = 0f;
                _planarVelocity = Vector3.zero;
                if (jump) // jump off backwards
                {
                    _verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity) * 0.7f;
                    _planarVelocity = -transform.forward * 3f;
                    IsClimbing = false;
                }
                motion = climb + Vector3.up * _verticalVelocity + _planarVelocity;
            }
            else
            {
                IsClimbing = false;
                bool grounded = _cc.isGrounded;
                float accel = grounded ? groundAcceleration : airAcceleration;
                // Exponential approach toward the wished velocity; slow in the air = floaty.
                _planarVelocity = Vector3.Lerp(_planarVelocity, wish * speed, 1f - Mathf.Exp(-accel * dt));

                if (grounded && _verticalVelocity < 0f) _verticalVelocity = -2f; // stick to slopes
                if (grounded && jump) _verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                _verticalVelocity += gravity * dt;
                motion = _planarVelocity + Vector3.up * _verticalVelocity;
            }

            _impulse = Vector3.Lerp(_impulse, Vector3.zero, 1f - Mathf.Exp(-impulseDamping * dt));
            motion += _impulse;

            CollisionFlags flags = _cc.Move(motion * dt);
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;
        }

        ClimbZone FindClimbZone()
        {
            float r = _cc.radius + 0.15f;
            Vector3 center = transform.position + _cc.center;
            float half = Mathf.Max(0f, _cc.height * 0.5f - _cc.radius);
            Collider[] hits = Physics.OverlapCapsule(center - Vector3.up * half, center + Vector3.up * half, r,
                ~0, QueryTriggerInteraction.Collide);
            foreach (Collider c in hits)
            {
                if (!c.isTrigger) continue;
                ClimbZone z = c.GetComponent<ClimbZone>();
                if (z == null || !z.IsClimbable) continue;
                // Can't climb the ladder you're holding yourself (a helper steadying it is fine).
                if (_grabber != null && z.Owner != null && _grabber.Grabbed == z.Owner) continue;
                return z;
            }
            return null;
        }

        /// <summary>External velocity kick (a slipping ladder, a bonk). Vertical part launches you.</summary>
        public void AddImpulse(Vector3 velocityChange)
        {
            _verticalVelocity += velocityChange.y;
            _impulse += new Vector3(velocityChange.x, 0f, velocityChange.z);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _cc.enabled = true;
            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _impulse = Vector3.zero;
            if (_look != null) _look.ResetView(rotation);
        }

        // CharacterControllers don't push rigidbodies on their own.
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            // Standing on it (a ladder rung, a pile): don't shove what we stand on.
            if (hit.normal.y > 0.5f || hit.moveDirection.y < -0.3f) return;
            Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (push.sqrMagnitude < 1e-4f) return;
            push.Normalize();

            // Light stuff (shards, nail boxes) gets nudged; heavier stuff doesn't
            // budge, otherwise climbing would kick the ladder out from under you
            // and walking would bulldoze the building. Grab things to move them.
            if (rb.mass <= lightObjectMass)
                rb.AddForce(push * pushPower, ForceMode.VelocityChange);
        }
    }
}
