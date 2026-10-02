using BuildCrew.Core;
using BuildCrew.Interaction;
using BuildCrew.Parts;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>Input a held tool gets from its player each frame (already separated from devices).</summary>
    public struct ToolInput
    {
        public bool PrimaryHeld;
        public bool PrimaryPressed;
        /// <summary>Mouse delta in pixels this frame.</summary>
        public Vector2 MouseDelta;
        public float DeltaTime;
    }

    /// <summary>
    /// Base class for tools: physical pickups (rigidbodies, grabbed at their
    /// grip and held rigidly in a pose in front of the camera). The holding
    /// player's PlayerActions calls <see cref="Tick"/> every frame; LMB maps to
    /// StartUse / StopUse, and subclasses implement <see cref="Use"/> (one
    /// action) and may override the hooks for continuous use. Tools never
    /// change game state directly: they send commands (<see cref="Send"/>).
    ///
    /// Adding a tool: subclass Tool, add a Build&lt;Tool&gt; method in SceneBuilder.
    /// </summary>
    public abstract class Tool : Grabbable
    {
        public IGrabber Holder => PrimaryGrabber;
        public bool IsUsing { get; private set; }

        /// <summary>Most tools only work while the build timer runs.</summary>
        public virtual bool WorksOutsideBuild => false;
        public bool ToolsEnabled => WorksOutsideBuild || World.ToolsEnabled;

        /// <summary>True while the mouse drives the tool (sawing, stirring) instead of the view.</summary>
        public virtual bool CapturesMouse => false;

        /// <summary>Extra HUD line while held (cut lengths, mortar state...), or null.</summary>
        public virtual string StatusText => null;

        public override string LookInfo => $"{DisplayName}  (tool)";
        public override string HeldHint => "LMB: Use   E: Drop   G: Throw";

        protected int OwnerId => Holder != null ? Holder.PlayerId : -1;
        protected GameSettings Settings => World.Settings;

        /// <summary>Animation on top of the hold pose (camera space): swings, sawing strokes.</summary>
        protected Vector3 AnimOffset;
        protected Quaternion AnimRotation = Quaternion.identity;

        public virtual void Tick(IGrabber holder, ToolInput input)
        {
            if (input.PrimaryHeld && ToolsEnabled) StartUse();
            else StopUse();
            if (IsUsing) WhileUsing(holder, input);
        }

        public void StartUse()
        {
            if (IsUsing) return;
            IsUsing = true;
            OnStartUse();
        }

        public void StopUse()
        {
            if (!IsUsing) return;
            IsUsing = false;
            OnStopUse();
        }

        protected virtual void OnStartUse() => Use();
        protected virtual void OnStopUse() { }
        protected virtual void WhileUsing(IGrabber holder, ToolInput input) { }

        /// <summary>Perform one action (one swing, take mortar...).</summary>
        public abstract void Use();

        public override void GetHoldPose(out Vector3 localPosition, out Quaternion localRotation)
        {
            base.GetHoldPose(out localPosition, out localRotation);
            localPosition += AnimOffset;
            localRotation *= AnimRotation;
        }

        public override void OnGrabEnded(IGrabber grabber)
        {
            StopUse();
            AnimOffset = Vector3.zero;
            AnimRotation = Quaternion.identity;
            OnDropped();
        }

        /// <summary>Hide previews etc. when the tool leaves the hand.</summary>
        protected virtual void OnDropped() { }

        /// <summary>Raycast from the holder's eyes, ignoring the holder and this tool.</summary>
        protected bool AimRaycast(float reach, out RaycastHit hit)
        {
            hit = default;
            IGrabber h = Holder;
            if (h == null || h.Aim == null) return false;
            return PhysicsUtil.Raycast(h.Aim.position, h.Aim.forward, reach, h.Root, QueryTriggerInteraction.Ignore, out hit, transform);
        }

        protected Part AimedPart(float reach, out RaycastHit hit)
        {
            if (!AimRaycast(reach, out hit) || hit.rigidbody == null) return null;
            return hit.rigidbody.GetComponent<Part>();
        }

        protected bool Send(GameCommand cmd)
        {
            cmd.PlayerId = OwnerId;
            CommandBus bus = World.Bus;
            return bus != null && bus.Execute(cmd);
        }
    }
}
