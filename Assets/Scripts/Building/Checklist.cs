using System.Collections.Generic;
using BuildCrew.Core;
using BuildCrew.Parts;
using BuildCrew.Sorting;
using UnityEngine;

namespace BuildCrew.Building
{
    public struct ChecklistLine
    {
        public string Label;
        public int Stage;
        public int Needed;
        public int Built;
        public int Sorted;
    }

    /// <summary>
    /// What the plan table shows: per needed part group, how many are in the
    /// building and how many are sorted onto pallets. Parts still in the pile
    /// (or lying around) are deliberately NOT counted: they show as "?".
    /// </summary>
    public static class Checklist
    {
        public static List<ChecklistLine> Compute(BuildSite site, IEnumerable<SortingZone> zones)
        {
            var lines = new List<ChecklistLine>();
            if (site == null) return lines;
            var index = new Dictionary<string, int>();
            foreach (BuildSlot slot in site.Slots)
            {
                if (slot.Definition == null) continue;
                string label = slot.Definition.GroupLabel(slot.Size);
                if (!index.TryGetValue(label, out int i))
                {
                    i = lines.Count;
                    index[label] = i;
                    lines.Add(new ChecklistLine { Label = label, Stage = slot.Stage });
                }
                ChecklistLine l = lines[i];
                l.Needed++;
                if (slot.State != SlotState.Empty) l.Built++;
                l.Stage = Mathf.Min(l.Stage, slot.Stage);
                lines[i] = l;
            }
            if (zones != null)
            {
                foreach (SortingZone z in zones)
                {
                    if (z == null || z.TeamId != site.TeamId) continue;
                    foreach (Part p in z.Inside)
                    {
                        if (p == null || !index.TryGetValue(p.GroupLabel, out int i)) continue;
                        ChecklistLine l = lines[i];
                        l.Sorted++;
                        lines[i] = l;
                    }
                }
            }
            return lines;
        }

        /// <summary>Parts that are neither in the building nor on a pallet (pile, ground, hands).</summary>
        public static bool AnyUnsorted(IEnumerable<SortingZone> zones)
        {
            PartManager parts = World.Parts;
            if (parts == null) return false;
            foreach (Part p in parts.Parts)
            {
                if (p == null || p.InBuilding || p.Definition == null || p.Definition.IsJunk) continue;
                bool sorted = false;
                if (zones != null)
                    foreach (SortingZone z in zones)
                        if (z != null && z.Contains(p)) { sorted = true; break; }
                if (!sorted) return true;
            }
            return false;
        }

        /// <summary>
        /// Lengths that have to be cut, for slots of the current and later stages
        /// that are still empty: e.g. "Planks 2.5 m x6 (from 3 m)".
        /// </summary>
        public static List<string> CutList(BuildSite site)
        {
            var result = new List<string>();
            if (site == null) return result;
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            float tol = World.Settings.sizeTolerance;
            int current = site.CurrentStage;
            foreach (BuildSlot slot in site.Slots)
            {
                PartDefinition def = slot.Definition;
                if (def == null || slot.State != SlotState.Empty || slot.Stage < current) continue;
                string from = null;
                if (def.IsLinear)
                {
                    bool standard = false;
                    float stock = float.MaxValue;
                    foreach (float s in def.StandardLengths)
                    {
                        if (Mathf.Abs(s - slot.Size.x) <= tol) standard = true;
                        if (s >= slot.Size.x && s < stock) stock = s;
                    }
                    if (standard) continue;
                    from = stock < float.MaxValue ? PartDefinition.FormatLength(stock) : "?";
                }
                else if (def.CutTool == CutTool.GlassCutter)
                {
                    Vector3 stock = def.StockSize(slot.Size);
                    if (Mathf.Abs(stock.x - slot.Size.x) <= tol && Mathf.Abs(stock.y - slot.Size.y) <= tol) continue;
                    from = def.FormatSize(stock);
                }
                else continue;
                string key = $"{def.GroupLabel(slot.Size)}|{from}";
                if (!counts.ContainsKey(key)) order.Add(key);
                counts.TryGetValue(key, out int c);
                counts[key] = c + 1;
            }
            foreach (string key in order)
            {
                string[] parts = key.Split('|');
                result.Add($"{parts[0]} x{counts[key]} (from {parts[1]})");
            }
            return result;
        }
    }
}
