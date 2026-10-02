using BuildCrew.Building;
using BuildCrew.Interaction;
using BuildCrew.Kits;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>Hold LMB on a snapped door/sheet: one screw per screwSeconds, three screws fix it.</summary>
    public class Screwdriver : FixingTool
    {
        float _progress;
        BuildSlot _slot;

        public override FixMethod Method => FixMethod.Screws;
        public override string HeldHint => "Hold LMB: Drive screws into placed doors/sheets   E: Drop";

        protected override void OnStartUse()
        {
            _progress = 0f;
            _slot = null;
        }

        public override void Use() { }

        protected override void WhileUsing(IGrabber holder, ToolInput input)
        {
            BuildSlot slot = AimedSlot(out _);
            if (slot != _slot)
            {
                _slot = slot;
                _progress = 0f;
            }
            if (slot == null)
            {
                AnimRotation = Quaternion.identity;
                return;
            }
            _progress += input.DeltaTime;
            // Twist around the shaft (local z) while driving.
            AnimRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 18f) * 40f);
            if (holder.Shake != null) holder.Shake.AddJitter(0.15f);
            if (_progress >= Settings.screwSeconds)
            {
                _progress = 0f;
                TryFix(slot);
            }
        }

        protected override void OnStopUse()
        {
            AnimRotation = Quaternion.identity;
            _slot = null;
        }
    }
}
