using BuildCrew.Building;
using BuildCrew.Kits;
using BuildCrew.Parts;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>A tool that fixes snapped parts with nails or screws (hammer, screwdriver).</summary>
    public abstract class FixingTool : Tool
    {
        public abstract FixMethod Method { get; }
        protected const float Reach = 2.6f;

        string Noun => Method == FixMethod.Screws ? "Screws" : "Nails";

        /// <summary>The snapped slot under the crosshair that wants our fixing method, or null.</summary>
        protected BuildSlot AimedSlot(out RaycastHit hit)
        {
            Part p = AimedPart(Reach, out hit);
            BuildSlot slot = p != null ? p.Slot : null;
            return slot != null && slot.State == SlotState.Snapped && slot.Method == Method ? slot : null;
        }

        public override string StatusText
        {
            get
            {
                int pocket = Holder != null && Holder.Inventory != null ? Holder.Inventory.Count(Method) : 0;
                string line = $"{Noun} in pocket: {pocket}";
                if (pocket == 0) line += $"  (E on a box of {Noun.ToLowerInvariant()} to grab some)";
                BuildSlot slot = AimedSlot(out _);
                if (slot != null) line += $"\n{slot.Definition?.DisplayName}: {slot.FixProgress}/{slot.FixNeeded(Settings)}";
                else
                {
                    Part p = AimedPart(Reach, out _);
                    if (p != null && p.Slot != null && p.State == PartState.Snapped && p.Slot.Method != Method)
                        line += $"\nThis one needs {KitDefinition.FixMethodId(p.Slot.Method)}";
                }
                return line;
            }
        }

        protected bool TryFix(BuildSlot slot)
        {
            return Send(new FixCommand
            {
                SiteIndex = slot.Site.SiteIndex,
                SlotId = slot.Id,
                Method = Method,
                ToolId = EntityId,
            });
        }
    }
}
