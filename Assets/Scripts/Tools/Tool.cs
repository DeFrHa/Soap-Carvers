using SoapCarvers.Core;
using SoapCarvers.Soap;
using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// Base class for carving tools. PlayerActions calls StartUse when the
    /// primary button goes down and StopUse when it goes up (or the tool is
    /// dropped). Subclasses implement <see cref="Use"/> (one action) and may
    /// override the start/stop hooks for continuous behaviour.
    ///
    /// Tools never touch the density grid: they call SoapBlock.CarveSphere /
    /// CarveCapsule, which build a SoapCarveCommand for SoapBlock.ApplyCarve.
    ///
    /// Adding a tool: subclass Tool, build its model in SceneBuilder (or a
    /// prefab), done. See WireCutterTool.cs for the planned 2-player tool.
    /// </summary>
    public abstract class Tool : Holdable
    {
        protected GameManager Game { get; private set; }
        protected SoapBlock Soap { get; private set; }
        protected GameSettings Settings { get; private set; }

        public bool IsUsing { get; private set; }

        /// <summary>False once the timer has run out (or during scan/results).</summary>
        public bool ToolsEnabled => Game == null || Game.ToolsEnabled;

        public override string HeldHint => "LMB: Use   Q: Drop   G: Throw";

        /// <summary>Player id for carve commands; -1 when nobody holds it.</summary>
        protected int OwnerId => Holder != null ? Holder.PlayerId : -1;

        protected override void Awake()
        {
            base.Awake();
            Game = FindFirstObjectByType<GameManager>();
            Soap = FindFirstObjectByType<SoapBlock>();
            Settings = Game != null && Game.Settings != null ? Game.Settings : GameSettings.CreateDefault();
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

        protected virtual void OnStartUse()
        {
            if (ToolsEnabled) Use();
        }

        protected virtual void OnStopUse() { }

        /// <summary>Perform one action (one stab, one swing, light the fuse...).</summary>
        public abstract void Use();

        public override void OnReleased(Vector3 velocity)
        {
            StopUse();
            base.OnReleased(velocity);
        }

        /// <summary>Raycast from the holder's eyes; true only if it hits soap.</summary>
        protected bool RaycastSoap(float reach, out RaycastHit hit)
        {
            hit = default;
            if (Holder == null) return false;
            Transform aim = Holder.AimTransform;
            if (!PhysicsUtil.Raycast(aim.position, aim.forward, reach, Holder.Root, QueryTriggerInteraction.Ignore, out hit))
                return false;
            return hit.collider.GetComponentInParent<SoapChunk>() != null;
        }
    }
}
