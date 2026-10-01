using UnityEngine;
using UnityEngine.InputSystem;

namespace SoapCarvers.Player
{
    /// <summary>
    /// Raw input only (new Input System, actions defined in code; no
    /// .inputactions asset). Exposes plain values that PlayerMotor / PlayerLook /
    /// PlayerActions read. A networked remote player would simply not have this
    /// component (or would feed the same values from the network).
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        InputAction _move, _look, _jump, _sprint;
        InputAction _interact, _drop, _throw, _primary, _raise;
        InputAction _view1, _view2, _view3, _view4, _restart, _unlockCursor;
        InputAction[] _all;

        public Vector2 Move => _move.ReadValue<Vector2>();
        /// <summary>Mouse delta in pixels this frame.</summary>
        public Vector2 Look => _look.ReadValue<Vector2>();
        public bool JumpPressed => _jump.WasPressedThisFrame();
        public bool SprintHeld => _sprint.IsPressed();
        public bool InteractPressed => _interact.WasPressedThisFrame();
        public bool DropPressed => _drop.WasPressedThisFrame();
        public bool ThrowPressed => _throw.WasPressedThisFrame();
        public bool PrimaryPressed => _primary.WasPressedThisFrame();
        public bool PrimaryHeld => _primary.IsPressed();
        /// <summary>RMB or Tab: raise the blueprint tablet.</summary>
        public bool RaiseHeld => _raise.IsPressed();
        public bool RestartPressed => _restart.WasPressedThisFrame();
        public bool UnlockCursorPressed => _unlockCursor.WasPressedThisFrame();

        /// <summary>Returns 0-3 if a view key (1-4) was pressed this frame, else -1.</summary>
        public int ViewKeyPressed
        {
            get
            {
                if (_view1.WasPressedThisFrame()) return 0;
                if (_view2.WasPressedThisFrame()) return 1;
                if (_view3.WasPressedThisFrame()) return 2;
                if (_view4.WasPressedThisFrame()) return 3;
                return -1;
            }
        }

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
            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _sprint = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            _drop = new InputAction("Drop", InputActionType.Button, "<Keyboard>/q");
            _throw = new InputAction("Throw", InputActionType.Button, "<Keyboard>/g");
            _primary = new InputAction("Primary", InputActionType.Button, "<Mouse>/leftButton");
            _raise = new InputAction("RaiseTablet", InputActionType.Button, "<Mouse>/rightButton");
            _raise.AddBinding("<Keyboard>/tab");
            _view1 = new InputAction("View1", InputActionType.Button, "<Keyboard>/1");
            _view2 = new InputAction("View2", InputActionType.Button, "<Keyboard>/2");
            _view3 = new InputAction("View3", InputActionType.Button, "<Keyboard>/3");
            _view4 = new InputAction("View4", InputActionType.Button, "<Keyboard>/4");
            _restart = new InputAction("Restart", InputActionType.Button, "<Keyboard>/r");
            _unlockCursor = new InputAction("UnlockCursor", InputActionType.Button, "<Keyboard>/escape");

            _all = new[]
            {
                _move, _look, _jump, _sprint, _interact, _drop, _throw, _primary, _raise,
                _view1, _view2, _view3, _view4, _restart, _unlockCursor,
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
