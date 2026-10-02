using BuildCrew.Core;
using BuildCrew.Interaction;
using BuildCrew.Kits;
using BuildCrew.Tools;
using UnityEngine;

namespace BuildCrew.Parts
{
    /// <summary>
    /// A box of nails or screws. Holding a hammer (nails) or screwdriver
    /// (screws), press E on it to pocket a handful; holding the box itself,
    /// LMB does the same. Fixing also draws from a box within reach when the
    /// pocket is empty.
    /// </summary>
    public class FixingBox : Part, IInteractable
    {
        [SerializeField] int count = -1;

        public int Count => count;
        public FixMethod Kind => TypeId == "screws" ? FixMethod.Screws : FixMethod.Nails;
        string Noun => Kind == FixMethod.Screws ? "screws" : "nails";

        public override string LookInfo => $"{DisplayName}: {count} {Noun} left";
        public override string HeldHint => "LMB: Pocket a handful   E: Release   G: Throw";

        protected override void Awake()
        {
            base.Awake();
            if (count < 0) count = World.Settings.fixingsPerBox;
        }

        /// <summary>WorkshopManager only.</summary>
        public int Take(int amount)
        {
            int n = Mathf.Clamp(amount, 0, count);
            count -= n;
            return n;
        }

        public string GetPrompt(IGrabber who)
        {
            if (who.Grabbed is FixingTool tool && tool.Method == Kind)
                return count > 0 ? $"E: Pocket a handful of {Noun} ({count} left)" : $"Box is empty";
            return null;
        }

        public void Interact(IGrabber who)
        {
            if (count <= 0) return;
            CommandBus bus = World.Bus;
            if (bus != null) bus.Execute(new TakeFixingsCommand { PlayerId = who.PlayerId, BoxId = EntityId });
        }
    }
}
