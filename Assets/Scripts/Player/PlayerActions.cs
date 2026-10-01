using SoapCarvers.Core;
using SoapCarvers.Targets;
using SoapCarvers.Tools;
using UnityEngine;

namespace SoapCarvers.Player
{
    /// <summary>
    /// Turns input into actions. Looks at what the crosshair hits, builds the
    /// interaction prompt, and sends ItemCommands to the ItemManager (pick up,
    /// drop, throw, place) or forwards use to the held Tool.
    /// Implements IItemHolder: this player's hand.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerActions : MonoBehaviour, IItemHolder
    {
        [SerializeField] int playerId = 1;
        [SerializeField] Transform aim;       // the camera
        [SerializeField] Transform holdPoint; // child of the camera
        [SerializeField] CameraShake shake;
        [SerializeField] ItemManager items;
        [SerializeField] GameManager game;

        PlayerInputHandler _input;
        PlayerMotor _motor;
        float _reach = 3f;
        float _throwSpeed = 12f;

        // --- IItemHolder ---
        public int PlayerId => playerId;
        public Transform HoldPoint => holdPoint;
        public Transform AimTransform => aim;
        public Transform Root => transform;
        public CameraShake Shake => shake;
        public Vector3 Velocity => _motor != null ? _motor.Velocity : Vector3.zero;
        public Holdable HeldItem { get; private set; }
        public void SetHeldItem(Holdable item) => HeldItem = item;

        // --- for the HUD ---
        /// <summary>What pressing E would do right now, or null.</summary>
        public string CurrentPrompt { get; private set; }
        public string HeldItemName => HeldItem != null ? HeldItem.DisplayName : null;
        public string HeldItemHint => HeldItem != null ? HeldItem.HeldHint : null;

        public void Configure(int id, Transform aimTransform, Transform hold, CameraShake cameraShake,
            ItemManager itemManager, GameManager gameManager)
        {
            playerId = id;
            aim = aimTransform;
            holdPoint = hold;
            shake = cameraShake;
            items = itemManager;
            game = gameManager;
        }

        void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
            _motor = GetComponent<PlayerMotor>();
            if (items == null) items = FindFirstObjectByType<ItemManager>();
            if (game == null) game = FindFirstObjectByType<GameManager>();
            if (game != null && game.Settings != null)
            {
                _reach = game.Settings.interactReach;
                _throwSpeed = game.Settings.throwSpeed;
            }
        }

        void Start()
        {
            if (items != null) items.RegisterHolder(this);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDestroy()
        {
            if (items != null) items.UnregisterHolder(this);
        }

        void Update()
        {
            UpdateCursor();
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;

            // ---- What are we looking at? ----
            Holdable lookItem = null;
            IInteractable lookInteractable = null;
            if (aim != null && PhysicsUtil.Raycast(aim.position, aim.forward, _reach, transform,
                    QueryTriggerInteraction.Collide, out RaycastHit hit))
            {
                lookItem = hit.collider.GetComponentInParent<Holdable>();
                if (lookItem != null && (lookItem.IsHeld || !lookItem.CanPickUp)) lookItem = null;
                if (lookItem == null) lookInteractable = hit.collider.GetComponentInParent<IInteractable>();
            }

            // Placement (lean the ladder, stick dynamite on soap) wins over picking things up.
            Vector3 placePos = default;
            Quaternion placeRot = Quaternion.identity;
            string placePrompt = null;
            bool canPlace = HeldItem != null && HeldItem.CanBePlaced &&
                            HeldItem.TryGetPlacement(this, out placePos, out placeRot, out placePrompt);

            if (canPlace) CurrentPrompt = placePrompt;
            else if (lookItem != null) CurrentPrompt = lookItem.PickupPrompt;
            else if (lookInteractable != null) CurrentPrompt = lookInteractable.GetPrompt(this);
            else CurrentPrompt = null;

            // ---- E: interact ----
            if (_input.InteractPressed && items != null)
            {
                if (canPlace) Place(HeldItem, placePos, placeRot);
                else if (lookItem != null) Send(ItemCommandType.PickUp, lookItem);
                else if (lookInteractable != null) lookInteractable.Interact(this);
            }

            // ---- Q / G: drop / throw ----
            if (HeldItem != null && items != null)
            {
                if (_input.DropPressed)
                    Send(ItemCommandType.Drop, HeldItem, Velocity);
                else if (_input.ThrowPressed)
                    Send(ItemCommandType.Throw, HeldItem, aim.forward * _throwSpeed + Vector3.up * 1.5f + Velocity);
            }

            // ---- Held item use ----
            if (HeldItem is BlueprintTablet tablet)
            {
                tablet.SetRaised(_input.RaiseHeld);
                int view = _input.ViewKeyPressed;
                if (view >= 0) tablet.SetView((BlueprintView)view);
                if (tablet.IsRaised && cursorLocked && _input.PrimaryPressed) tablet.CycleView();
            }
            else if (HeldItem is Tool tool)
            {
                if (cursorLocked && _input.PrimaryHeld) tool.StartUse();
                else tool.StopUse();
            }

            // ---- R: restart from the results screen ----
            if (_input.RestartPressed && game != null && game.State == GameState.Results)
                game.RequestRestart();
        }

        void Place(Holdable item, Vector3 pos, Quaternion rot)
        {
            items.Execute(new ItemCommand
            {
                Type = ItemCommandType.Place, PlayerId = playerId, ItemId = item.ItemId,
                Position = pos, Rotation = rot,
            });
        }

        void Send(ItemCommandType type, Holdable item, Vector3 velocity = default)
        {
            items.Execute(new ItemCommand { Type = type, PlayerId = playerId, ItemId = item.ItemId, Velocity = velocity });
        }

        /// <summary>
        /// Lock the cursor for mouse look; free it on the results screen (for the
        /// restart button) or when Escape is pressed. Click to re-lock.
        /// </summary>
        void UpdateCursor()
        {
            bool results = game != null && game.State == GameState.Results;
            if (results || _input.UnlockCursorPressed)
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
