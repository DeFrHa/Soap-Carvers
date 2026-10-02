using UnityEngine;
using UnityEngine.InputSystem;

namespace BuildCrew.Player
{
    /// <summary>
    /// Raw input only (new Input System, actions defined in code; no
    /// .inputactions asset). Exposes plain values that PlayerMotor / PlayerLook /
    /// PlayerActions read. A networked remote player simply won't have this
    /// component (or feeds the same values from the network).
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        InputAction _move, _look, _scroll, _jump, _sprint;
        InputAction _interact, _release, _throw, _primary, _rotate;
        InputAction _unlockCursor;
        InputAction[] _all;

        public Vector2 Move => _move.ReadValue<Vector2>();
        /// <summary>Mouse delta in pixels this frame.</summary>
        public Vector2 Look => _look.ReadValue<Vector2>();
        /// <summary>Mouse wheel this frame (y > 0 = away from you).</summary>
        public float Scroll => _scroll.ReadValue<Vector2>().y;
        public bool JumpPressed => _jump.WasPressedThisFrame();
        public bool SprintHeld => _sprint.IsPressed();
        /// <summary>E: grab / release / interact.</summary>
        public bool InteractPressed => _interact.WasPressedThisFrame();
        /// <summary>Q: release (same as E while holding).</summary>
        public bool ReleasePressed => _release.WasPressedThisFrame();
        /// <summary>G: throw.</summary>
        public bool ThrowPressed => _throw.WasPressedThisFrame();
        public bool PrimaryPressed => _primary.WasPressedThisFrame();
        public bool PrimaryHeld => _primary.IsPressed();
        /// <summary>R held: rotate the held object with the mouse.</summary>
        public bool RotateHeld => _rotate.IsPressed();
        /// <summary>R pressed: restart on the results screen.</summary>
        public bool RestartPressed => _rotate.WasPressedThisFrame();
        public bool UnlockCursorPressed => _unlockCursor.WasPressedThisFrame();

        void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            _look = new InputAction("Look", InputActionType.PassThrough, "<Mouse>/delta");
            _scroll = new InputAction("Scroll", InputActionType.PassThrough, "<Mouse>/scroll");
            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _sprint = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            _release = new InputAction("Release", InputActionType.Button, "<Keyboard>/q");
            _throw = new InputAction("Throw", InputActionType.Button, "<Keyboard>/g");
            _primary = new InputAction("Primary", InputActionType.Button, "<Mouse>/leftButton");
            _rotate = new InputAction("Rotate", InputActionType.Button, "<Keyboard>/r");
            _unlockCursor = new InputAction("UnlockCursor", InputActionType.Button, "<Keyboard>/escape");

            _all = new[]
            {
                _move, _look, _scroll, _jump, _sprint, _interact, _release, _throw, _primary, _rotate, _unlockCursor,
            };
        }

        void OnEnable()
        {
            foreach (var a in _all) a.Enable();
        }

        void OnDisable()
        {
            foreach (var a in _all) a.Disable();
        }

        void OnDestroy()
        {
            foreach (var a in _all) a.Dispose();
        }
    }
}
