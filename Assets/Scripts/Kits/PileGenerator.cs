using System;
using System.Collections.Generic;
using BuildCrew.Parts;
using UnityEngine;

namespace BuildCrew.Kits
{
    public enum PileRole
    {
        Needed = 0,
        Spare = 1,
        Decoy = 2,
        Fixings = 3,
    }

    /// <summary>What to spawn: a part type at a size (local box size, m).</summary>
    [Serializable]
    public struct PartSpec
    {
        public string TypeId;
        public Vector3 Size;
        public PileRole Role;

        public PartSpec(string typeId, Vector3 size, PileRole role)
        {
            TypeId = typeId;
            Size = size;
            Role = role;
        }

        public override string ToString() => $"{TypeId} {Size.x:0.00}x{Size.y:0.00}x{Size.z:0.00} ({Role})";
    }

    public struct PileOptions
    {
        public float SparePercent;
        public int WrongLengthDecoys;
        public int NailsPerFix;
        public int ScrewsPerFix;
        public int FixingsPerBox;
        public float LengthTolerance;
    }

    /// <summary>
    /// Turns a kit into the list of parts in the pile. Everything comes
    /// in STANDARD sizes: linear parts are first-fit-decreasing packed into the
    /// part type's standard lengths (so a 2 m plank may yield two 1 m boards,
    /// and anything shorter than its stock must be cut), panes come as 1 x 1 m,
    /// ready-made parts in their exact size. Then spares, decoys and boxes of
    /// nails/screws are added and the list is shuffled. Deterministic for a seed.
    /// </summary>
    public static class PileGenerator
    {
        public static List<PartSpec> Generate(KitDefinition kit, int seed, PileOptions o)
        {
            var rng = new System.Random(seed);
            var result = new List<PartSpec>();
            if (kit?.slots == null) return result;

            // ---- needed parts in stock sizes
            var linearGroups = new Dictionary<string, List<float>>();
            var linearCross = new Dictionary<string, (string type, float y, float z)>();
            var neededLengths = new Dictionary<string, List<float>>(); // per type, for decoy choice
            int nailFixes = 0, screwFixes = 0;
            foreach (KitSlot slot in kit.slots)
            {
                FixMethod fix = KitDefinition.ParseFixMethod(slot.fixMethod);
                if (fix == FixMethod.Nails) nailFixes++;
                else if (fix == FixMethod.Screws) screwFixes++;

                PartDefinition def = PartCatalog.Get(slot.partType);
                if (def == null) continue;
                if (def.StandardLengths != null)
                {
                    string key = $"{slot.partType}|{slot.size.y:0.000}|{slot.size.z:0.000}";
                    if (!linearGroups.TryGetValue(key, out List<float> list))
                    {
                        list = new List<float>();
                        linearGroups[key] = list;
                        linearCross[key] = (slot.partType, slot.size.y, slot.size.z);
                    }
                    list.Add(slot.size.x);
                    if (!neededLengths.TryGetValue(slot.partType, out List<float> lens))
                        neededLengths[slot.partType] = lens = new List<float>();
                    lens.Add(slot.size.x);
                }
                else
                {
                    result.Add(new PartSpec(slot.partType, def.StockSize(slot.size), PileRole.Needed));
                }
            }

            var groupKeys = new List<string>(linearGroups.Keys);
            groupKeys.Sort(StringComparer.Ordinal); // dictionary order isn't guaranteed; keep the seed meaningful
            foreach (string key in groupKeys)
            {
                (string type, float y, float z) = linearCross[key];
                PartDefinition def = PartCatalog.Get(type);
                foreach (float stock in PackLengths(linearGroups[key], def.StandardLengths, o.LengthTolerance))
                    result.Add(new PartSpec(type, new Vector3(stock, y, z), PileRole.Needed));
            }

            // ---- spares: sparePercent of the needed parts, picked at random
            int neededCount = result.Count;
            int spares = Mathf.CeilToInt(neededCount * o.SparePercent);
            for (int i = 0; i < spares && neededCount > 0; i++)
            {
                PartSpec pick = result[rng.Next(neededCount)];
                result.Add(new PartSpec(pick.TypeId, pick.Size, PileRole.Spare));
            }

            // ---- boxes of nails and screws, 25% extra
            AddBoxes(result, "nails", nailFixes * o.NailsPerFix, o.FixingsPerBox);
            AddBoxes(result, "screws", screwFixes * o.ScrewsPerFix, o.FixingsPerBox);

            // ---- decoys: wrong lengths of linear parts, a toilet and a garden gnome
            var linearTypes = new List<string>(neededLengths.Keys);
            linearTypes.Sort(StringComparer.Ordinal);
            for (int i = 0; i < o.WrongLengthDecoys && linearTypes.Count > 0; i++)
            {
                string type = linearTypes[rng.Next(linearTypes.Count)];
                PartDefinition def = PartCatalog.Get(type);
                float len = WrongLength(def.StandardLengths, neededLengths[type], rng);
                Vector3 cross = def.DefaultSize(len);
                // Keep the cross-section of a needed part of that type.
                foreach (string key in groupKeys)
                    if (linearCross[key].type == type) { cross.y = linearCross[key].y; cross.z = linearCross[key].z; break; }
                result.Add(new PartSpec(type, new Vector3(len, cross.y, cross.z), PileRole.Decoy));
            }
            foreach (string junk in new[] { "toilet", "gnome" })
            {
                PartDefinition def = PartCatalog.Get(junk);
                if (def != null) result.Add(new PartSpec(junk, def.DefaultSize(0f), PileRole.Decoy));
            }

            // ---- shuffle so the heap is properly tangled
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return result;
        }

        static void AddBoxes(List<PartSpec> result, string type, int count, int perBox)
        {
            if (count <= 0) return;
            PartDefinition def = PartCatalog.Get(type);
            if (def == null) return;
            int boxes = Mathf.CeilToInt(count * 1.25f / Mathf.Max(1, perBox));
            for (int i = 0; i < boxes; i++) result.Add(new PartSpec(type, def.DefaultSize(0f), PileRole.Fixings));
        }

        /// <summary>
        /// First-fit-decreasing (best fit) bin packing of the needed lengths into
        /// standard stock lengths. A piece within tolerance of the stock length
        /// needs no cut. Pieces longer than every standard length come as custom stock.
        /// </summary>
        public static List<float> PackLengths(List<float> needed, float[] standards, float tolerance)
        {
            var sorted = new List<float>(needed);
            sorted.Sort((a, b) => b.CompareTo(a));
            var stock = new List<float>();
            var remaining = new List<float>();
            var sortedStandards = new List<float>(standards);
            sortedStandards.Sort();

            foreach (float len in sorted)
            {
                int best = -1;
                for (int i = 0; i < remaining.Count; i++)
                    if (remaining[i] >= len - tolerance && (best < 0 || remaining[i] < remaining[best])) best = i;
                if (best >= 0)
                {
                    remaining[best] = Mathf.Max(0f, remaining[best] - len);
                    continue;
                }
                float pick = len;
                foreach (float s in sortedStandards)
                    if (s >= len - tolerance) { pick = s; break; }
                stock.Add(pick);
                remaining.Add(Mathf.Max(0f, pick - len));
            }
            return stock;
        }

        /// <summary>A length that is close to standard but useless: not within 15 cm of anything needed.</summary>
        static float WrongLength(float[] standards, List<float> needed, System.Random rng)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                float std = standards[rng.Next(standards.Length)];
                float len = Mathf.Round(std * (0.35f + (float)rng.NextDouble() * 0.4f) * 10f) / 10f;
                bool clash = false;
                foreach (float n in needed)
                    if (Mathf.Abs(n - len) < 0.15f) { clash = true; break; }
                if (!clash && len >= 0.5f) return len;
            }
            return 0.7f;
        }
    }
}
