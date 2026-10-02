using UnityEngine;

namespace BuildCrew.Player
{
    /// <summary>
    /// Mouse look: yaw rotates the body, pitch rotates the camera pivot.
    /// Also adds the "wobbly" head bob and a little roll when strafing.
    /// Mouse look pauses while PlayerActions uses the mouse for something else
    /// (R-rotating a held object, sawing, stirring).
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [SerializeField] float sensitivity = 0.12f;
        [SerializeField] float bobAmplitude = 0.05f;
        [SerializeField] float bobFrequency = 9f;
        [SerializeField] float strafeRoll = 2.5f;

        PlayerInputHandler _input;
        PlayerMotor _motor;
        PlayerActions _actions;
        float _yaw, _pitch, _roll, _bobPhase;
        Vector3 _pivotBase;

        /// <summary>Degrees; negative = looking up, positive = looking down.</summary>
        public float Pitch => _pitch;
        public Transform CameraPivot => cameraPivot;

        public void Configure(Transform pivot, float mouseSensitivity)
        {
            cameraPivot = pivot;
            sensitivity = mouseSensitivity;
        }

        void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
            _motor = GetComponent<PlayerMotor>();
            _actions = GetComponent<PlayerActions>();
            _yaw = transform.eulerAngles.y;
            if (cameraPivot != null) _pivotBase = cameraPivot.localPosition;
        }

        /// <summary>Teleport support: sets the yaw from a rotation and levels the view.</summary>
        public void ResetView(Quaternion bodyRotation)
        {
            _yaw = bodyRotation.eulerAngles.y;
            _pitch = 0f;
        }

        void Update()
        {
            bool suppressed = _actions != null && _actions.SuppressLook;
            if (_input != null && Cursor.lockState == CursorLockMode.Locked && !suppressed)
            {
                Vector2 d = _input.Look * sensitivity;
                _yaw += d.x;
                _pitch = Mathf.Clamp(_pitch - d.y, -88f, 88f);
            }
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            if (cameraPivot == null) return;

            // Wobble: bob while walking on the ground, roll into strafes.
            Vector3 v = _motor != null ? _motor.Velocity : Vector3.zero;
            float planarSpeed = new Vector2(v.x, v.z).magnitude;
            bool grounded = _motor == null || _motor.IsGrounded;
            if (grounded && planarSpeed > 0.5f) _bobPhase += Time.deltaTime * bobFrequency * Mathf.Clamp(planarSpeed / 5f, 0.6f, 1.6f);
            float bobWeight = grounded ? Mathf.Clamp01(planarSpeed / 5f) : 0f;
            Vector3 bob = new Vector3(Mathf.Cos(_bobPhase * 0.5f) * 0.5f, Mathf.Sin(_bobPhase), 0f) * (bobAmplitude * bobWeight);

            float lateral = Vector3.Dot(v, transform.right);
            _roll = Mathf.Lerp(_roll, -lateral * strafeRoll / 5f, 1f - Mathf.Exp(-6f * Time.deltaTime));

            cameraPivot.localPosition = _pivotBase + bob;
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, _roll);
        }
    }
}
