using BuildCrew.Core;
using BuildCrew.Interaction;
using BuildCrew.Parts;
using BuildCrew.Tools;
using UnityEngine;

namespace BuildCrew.Player
{
    /// <summary>
    /// Turns this player's input into commands. Looks at what the crosshair
    /// hits, builds the HUD prompt, and sends Grab / Release commands to the
    /// CommandBus or forwards LMB/mouse to the held Tool (which sends its own
    /// commands). Never changes game state directly.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler), typeof(PlayerGrabber))]
    public class PlayerActions : MonoBehaviour
    {
        PlayerInputHandler _input;
        PlayerGrabber _grabber;
        GameSettings _settings;

        // --- for the HUD ---
        /// <summary>What pressing E would do right now, or null.</summary>
        public string CurrentPrompt { get; private set; }
        /// <summary>Name/size/weight of what the crosshair is on.</summary>
        public string LookInfo { get; private set; }
        public Grabbable Held => _grabber != null ? _grabber.Grabbed : null;
        public string HeldInfo => Held != null ? Held.LookInfo : null;
        public string HeldHint => Held != null ? Held.HeldHint : null;
        public string ToolStatus => Held is Tool t && t.PrimaryGrabber == (IGrabber)_grabber ? t.StatusText : null;
        public PlayerInventory Inventory => _grabber != null ? _grabber.Inventory : null;
        public PlayerGrabber Grabber => _grabber;

        /// <summary>True while the mouse drives something other than the view (PlayerLook reads it).</summary>
        public bool SuppressLook { get; private set; }

        void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
            _grabber = GetComponent<PlayerGrabber>();
            _settings = World.Settings;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            GameManager game = World.Game;
            GameState state = game != null ? game.State : GameState.Build;
            UpdateCursor(state);
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            bool frozen = state == GameState.FinalTest;
            SuppressLook = false;
            Grabbable held = _grabber.Grabbed;
            Transform aim = _grabber.Aim;

            if (held != null && (_grabber.LeashBroken || frozen)) Release(false);
            held = _grabber.Grabbed;

            // ---- What are we looking at? (ignore ourselves and what we hold)
            Grabbable lookGrabbable = null;
            IInteractable lookInteractable = null;
            RaycastHit hit = default;
            if (!frozen && aim != null && PhysicsUtil.Raycast(aim.position, aim.forward, _settings.interactReach, transform,
                    QueryTriggerInteraction.Ignore, out hit, held != null ? held.transform : null))
            {
                lookInteractable = hit.collider.GetComponentInParent<IInteractable>();
                lookGrabbable = hit.rigidbody != null ? hit.rigidbody.GetComponent<Grabbable>() : null;
            }
            string interactPrompt = lookInteractable?.GetPrompt(_grabber);
            LookInfo = lookGrabbable != null ? lookGrabbable.LookInfo : null;

            if (frozen) CurrentPrompt = null;
            else if (interactPrompt != null) CurrentPrompt = interactPrompt;
            else if (held == null && lookGrabbable != null && lookGrabbable.CanBeGrabbed) CurrentPrompt = lookGrabbable.GrabPrompt;
            else CurrentPrompt = null;

            if (!frozen)
            {
                // ---- E: interact > release > grab
                if (_input.InteractPressed)
                {
                    if (interactPrompt != null) lookInteractable.Interact(_grabber);
                    else if (held != null) Release(false);
                    else if (lookGrabbable != null && lookGrabbable.CanBeGrabbed)
                        Send(new GrabCommand { EntityId = lookGrabbable.EntityId, LocalPoint = lookGrabbable.transform.InverseTransformPoint(hit.point) });
                }
                else if (held != null && _input.ReleasePressed) Release(false);
                else if (held != null && _input.ThrowPressed) Release(true);
            }

            held = _grabber.Grabbed;
            if (held != null && cursorLocked && !frozen)
            {
                float scroll = _input.Scroll;
                if (Mathf.Abs(scroll) > 0.01f) _grabber.AdjustDistance(Mathf.Sign(scroll));

                bool rotating = _input.RotateHeld && !_grabber.HoldsWithGrip && state != GameState.Results;
                if (rotating)
                {
                    _grabber.Rotate(_input.Look);
                    SuppressLook = true;
                }

                if (held is Tool tool && tool.PrimaryGrabber == (IGrabber)_grabber)
                {
                    tool.Tick(_grabber, new ToolInput
                    {
                        PrimaryHeld = _input.PrimaryHeld,
                        PrimaryPressed = _input.PrimaryPressed,
                        SecondaryHeld = _input.SecondaryHeld,
                        MouseDelta = _input.Look,
                        ViewKey = _input.ViewKeyPressed,
                        DeltaTime = Time.deltaTime,
                    });
                    if (tool.CapturesMouse) SuppressLook = true;
                }
                else if (held is FixingBox box && _input.PrimaryPressed && box.Count > 0)
                {
                    Send(new TakeFixingsCommand { BoxId = box.EntityId });
                }
            }
            else if (held is Tool idleTool && idleTool.PrimaryGrabber == (IGrabber)_grabber)
            {
                idleTool.Tick(_grabber, new ToolInput { DeltaTime = Time.deltaTime, ViewKey = -1 });
            }

            // ---- R: restart from the results screen
            if (_input.RestartPressed && game != null && state == GameState.Results)
                game.RequestRestart();
        }

        void Release(bool throwIt)
        {
            Vector3 dir = _grabber.Aim != null ? _grabber.Aim.forward + Vector3.up * 0.15f : transform.forward;
            Send(new ReleaseCommand { Throw = throwIt, Direction = dir });
        }

        void Send(GameCommand cmd)
        {
            cmd.PlayerId = _grabber.PlayerId;
            CommandBus bus = World.Bus;
            if (bus != null) bus.Execute(cmd);
        }

        /// <summary>
        /// Lock the cursor for mouse look; free it on the results screen (for the
        /// restart button) or when Escape is pressed. Click to re-lock.
        /// </summary>
        void UpdateCursor(GameState state)
        {
            if (state == GameState.Results || _input.UnlockCursorPressed)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Cursor.lockState != CursorLockMode.Locked && _input.PrimaryPressed)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
