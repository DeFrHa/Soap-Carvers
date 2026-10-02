using System.Collections.Generic;
using BuildCrew.Core;
using BuildCrew.Interaction;
using BuildCrew.Kits;
using BuildCrew.Parts;
using BuildCrew.Tools;
using UnityEngine;

namespace BuildCrew.Building
{
    /// <summary>
    /// Authority for the building: handles Snap / Unsnap / Fix commands for
    /// every team's BuildSite, and consumes the fixings they need (nails and
    /// screws from the player's pocket or a nearby box, mortar from the trowel).
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        readonly List<BuildSite> _sites = new List<BuildSite>();

        public IReadOnlyList<BuildSite> Sites => _sites;

        void Awake()
        {
            CommandBus bus = GetComponent<CommandBus>();
            if (bus == null) bus = World.Bus;
            if (bus == null) return;
            bus.Register<SnapCommand>(HandleSnap);
            bus.Register<UnsnapCommand>(HandleUnsnap);
            bus.Register<FixCommand>(HandleFix);
        }

        public void RegisterSite(BuildSite site)
        {
            if (!_sites.Contains(site)) _sites.Add(site);
        }

        public BuildSite GetSite(int index)
        {
            foreach (BuildSite s in _sites)
                if (s != null && s.SiteIndex == index) return s;
            return null;
        }

        bool HandleSnap(SnapCommand cmd)
        {
            BuildSite site = GetSite(cmd.SiteIndex);
            BuildSlot slot = site != null ? site.Get(cmd.SlotId) : null;
            Part part = World.Registry != null ? World.Registry.Get<Part>(cmd.PartId) : null;
            if (slot == null || part == null || !slot.IsActive || part.State != PartState.Loose) return false;
            GameSettings s = World.Settings;
            if (!slot.TryMatch(part, s, out Quaternion rot, out float angle)) return false;
            // A little slack over the client-side check (latency, wobble).
            if (Vector3.Distance(part.transform.position, slot.TargetPosition) > s.snapDistance * 1.5f || angle > s.snapAngle * 1.5f) return false;

            if (World.Grabs != null) World.Grabs.ReleaseAll(part);
            slot.Snap(part, rot, s);
            part.SetBuildState(PartState.Snapped, slot);
            site.NotifyChanged();
            return true;
        }

        bool HandleUnsnap(UnsnapCommand cmd)
        {
            BuildSite site = GetSite(cmd.SiteIndex);
            BuildSlot slot = site != null ? site.Get(cmd.SlotId) : null;
            if (slot == null || slot.State == SlotState.Empty) return false;
            Part part = slot.Clear();
            if (part != null)
            {
                part.SetBuildState(PartState.Loose, null);
                part.SnapBlockedSlot = slot;
                DetachJointsTo(part);
            }
            site.NotifyChanged();
            return true;
        }

        bool HandleFix(FixCommand cmd)
        {
            BuildSite site = GetSite(cmd.SiteIndex);
            BuildSlot slot = site != null ? site.Get(cmd.SlotId) : null;
            if (slot == null || slot.State != SlotState.Snapped || slot.Part == null) return false;
            if (cmd.Method != slot.Method || !slot.RestsSatisfied) return false;
            if (!Consume(cmd)) return false;

            GameSettings s = World.Settings;
            slot.AddFixProgress(1);
            if (slot.FixProgress >= slot.FixNeeded(s))
            {
                slot.Fix(s);
                slot.Part.SetBuildState(PartState.Fixed, slot);
            }
            site.NotifyChanged();
            return true;
        }

        /// <summary>Takes one nail/screw (pocket first, then a box within reach) or one trowel of mortar.</summary>
        bool Consume(FixCommand cmd)
        {
            if (cmd.Method == FixMethod.Mortar)
            {
                Trowel trowel = World.Registry != null ? World.Registry.Get<Trowel>(cmd.ToolId) : null;
                return trowel != null && trowel.ConsumeApplication();
            }

            IGrabber player = World.Grabs != null ? World.Grabs.GetGrabber(cmd.PlayerId) : null;
            if (player == null) return false;
            if (player.Inventory != null && player.Inventory.TryConsume(cmd.Method, 1)) return true;

            FixingBox box = NearestBox(player.Root.position, cmd.Method, World.Settings.fixingBoxReach);
            return box != null && box.Take(1) == 1;
        }

        public static FixingBox NearestBox(Vector3 position, FixMethod kind, float radius)
        {
            PartManager parts = World.Parts;
            if (parts == null) return null;
            FixingBox best = null;
            float bestD = radius;
            foreach (Part p in parts.Parts)
            {
                if (!(p is FixingBox box) || box.Kind != kind || box.Count <= 0) continue;
                float d = Vector3.Distance(position, box.transform.position);
                if (d > bestD) continue;
                bestD = d;
                best = box;
            }
            return best;
        }

        /// <summary>A part leaves the world (shattered, cut): empty its slot and drop joints that point at it.</summary>
        public void DetachFromBuilding(Part part)
        {
            if (part == null) return;
            foreach (BuildSite site in _sites)
            {
                BuildSlot slot = site.SlotOf(part);
                if (slot == null) continue;
                slot.Clear();
                site.NotifyChanged();
            }
            part.SetBuildState(PartState.Loose, null);
            DetachJointsTo(part);
        }

        /// <summary>
        /// Fixed joints whose connected body disappears would pin their part to
        /// the world, so remove any joint that connects to <paramref name="part"/>.
        /// </summary>
        void DetachJointsTo(Part part)
        {
            foreach (BuildSite site in _sites)
            foreach (BuildSlot slot in site.Slots)
                if (slot.Part != null && slot.Part != part) slot.Part.DetachJointsTo(part.Body);
        }

        /// <summary>Final test: snapped-but-not-fixed parts lose their spring and just rest where they are.</summary>
        public void ReleaseUnfixed(BuildSite site)
        {
            foreach (BuildSlot slot in site.Slots)
            {
                if (slot.State != SlotState.Snapped) continue;
                CommandBus bus = World.Bus;
                if (bus != null) bus.Execute(new UnsnapCommand { SiteIndex = site.SiteIndex, SlotId = slot.Id });
            }
        }
    }
}
