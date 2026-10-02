using System;
using BuildCrew.Core;
using UnityEngine;

namespace BuildCrew.Parts
{
    public enum PartCategory
    {
        Wood = 0,
        Stone = 1,
        Roof = 2,
        Glass = 3,
        ReadyMade = 4,
        Fixings = 5,
        Junk = 6,
    }

    public enum CutTool
    {
        None = 0,
        Saw = 1,
        GlassCutter = 2,
    }

    /// <summary>
    /// Describes one part type: how it looks, weighs, is delivered and cut.
    /// Register new types with <see cref="PartCatalog.Register"/>; kits refer to
    /// them by <see cref="Id"/>. Sizes are local box sizes in meters; linear
    /// types (those with <see cref="StandardLengths"/>) run along local x.
    /// </summary>
    public abstract class PartDefinition
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public virtual string PluralName => DisplayName + "s";
        public abstract PartCategory Category { get; }

        /// <summary>Density of the bounding box (kg/m^3); override <see cref="Mass"/> for fixed weights.</summary>
        public virtual float Density => 500f;
        public virtual float Mass(Vector3 size) => Mathf.Max(0.2f, size.x * size.y * size.z * Density);

        /// <summary>Stock lengths the pile contains (linear types), or null for parts delivered as-is.</summary>
        public virtual float[] StandardLengths => null;
        public bool IsLinear => StandardLengths != null;

        public virtual CutTool CutTool => IsLinear ? CutTool.Saw : CutTool.None;
        /// <summary>Hook for the two-person saw (beams). Solo sawing runs at reduced speed.</summary>
        public virtual bool NeedsTwoPersonSaw => false;
        public virtual int SawStrokes(GameSettings s) => s.plankStrokes;
        public virtual bool Fragile => false;
        public virtual bool IsJunk => Category == PartCategory.Junk;

        /// <summary>The size delivered for a slot of the given size (non-linear types).</summary>
        public virtual Vector3 StockSize(Vector3 needed) => needed;

        /// <summary>Size for a fresh part of this type (length used by linear types).</summary>
        public virtual Vector3 DefaultSize(float length) => new Vector3(Mathf.Max(0.1f, length), 0.1f, 0.1f);

        /// <summary>The component added to the part object (FixingBox for nail boxes).</summary>
        public virtual Type ComponentType => typeof(Part);

        public static string FormatLength(float meters) => meters.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " m";

        /// <summary>Size as shown on look-at: "2.5 m" for linear parts, "1 x 0.8 m" otherwise.</summary>
        public virtual string FormatSize(Vector3 size) =>
            IsLinear ? FormatLength(size.x) : $"{size.x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} x {FormatLength(size.y)}";

        /// <summary>Sorting/checklist group label, e.g. "Planks 2 m".</summary>
        public virtual string GroupLabel(Vector3 size) => IsLinear || CutTool != CutTool.None ? $"{PluralName} {FormatSize(size)}" : PluralName;

        /// <summary>Adds the look (primitives, combined afterwards) under the part root.</summary>
        public abstract void BuildVisual(PartVisualBuilder v, Vector3 size);

        /// <summary>Default: one box collider, a hair smaller than the size so neighbours in the building don't fight.</summary>
        public virtual void BuildColliders(GameObject root, Vector3 size)
        {
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(0.02f, size.x - 0.01f), Mathf.Max(0.02f, size.y - 0.01f), Mathf.Max(0.02f, size.z - 0.01f));
        }

        protected static void AddBox(GameObject root, Vector3 center, Vector3 size)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }
    }
}
