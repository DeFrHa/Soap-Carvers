using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildCrew.Kits
{
    public enum FinalTestKind
    {
        Wind = 0,
        WindRain = 1,
    }

    public enum FixMethod
    {
        None = 0,
        Nails = 1,
        Screws = 2,
        Mortar = 3,
    }

    /// <summary>
    /// A building kit as stored in StreamingAssets/Kits/*.json (read with
    /// JsonUtility, so: public fields only, no dictionaries). Coordinates are
    /// build-site local, in meters: x right, y up, the building's front faces -z.
    /// Part sizes are box sizes in the part's local frame; linear parts
    /// (post, plank, beam) run along local x.
    /// </summary>
    [Serializable]
    public class KitDefinition
    {
        public string id;
        public string name;
        public string description;
        /// <summary>Round length in seconds.</summary>
        public float roundTime = 240f;
        /// <summary>"wind" or "windRain".</summary>
        public string finalTest = "wind";
        /// <summary>Stage names, in build order. KitSlot.stage indexes this.</summary>
        public string[] stages;
        public KitSlot[] slots;

        public FinalTestKind GetFinalTest() =>
            string.Equals(finalTest, "windRain", StringComparison.OrdinalIgnoreCase) ? FinalTestKind.WindRain : FinalTestKind.Wind;

        public string StageName(int stage) =>
            stages != null && stage >= 0 && stage < stages.Length ? stages[stage] : $"Stage {stage + 1}";

        public int StageCount() => stages != null ? stages.Length : 0;

        /// <summary>Returns a list of problems (empty if the kit is usable).</summary>
        public List<string> Validate(Func<string, bool> isKnownPartType = null)
        {
            var errors = new List<string>();
            if (slots == null || slots.Length == 0)
            {
                errors.Add("kit has no slots");
                return errors;
            }
            var ids = new Dictionary<string, KitSlot>();
            foreach (KitSlot s in slots)
            {
                if (string.IsNullOrEmpty(s.id)) { errors.Add("slot without id"); continue; }
                if (ids.ContainsKey(s.id)) errors.Add($"duplicate slot id '{s.id}'");
                ids[s.id] = s;
                if (isKnownPartType != null && !isKnownPartType(s.partType)) errors.Add($"slot '{s.id}': unknown partType '{s.partType}'");
                if (s.size.x <= 0f || s.size.y <= 0f || s.size.z <= 0f) errors.Add($"slot '{s.id}': size must be positive");
                if (s.stage < 0 || s.stage >= StageCount()) errors.Add($"slot '{s.id}': stage {s.stage} out of range");
                if (ParseFixMethod(s.fixMethod) == FixMethod.None) errors.Add($"slot '{s.id}': unknown fixMethod '{s.fixMethod}'");
            }
            foreach (KitSlot s in slots)
            {
                if (s.restsOn == null) continue;
                foreach (string r in s.restsOn)
                    if (!ids.ContainsKey(r)) errors.Add($"slot '{s.id}': restsOn unknown slot '{r}'");
            }
            // Cycles would make slots that can never activate.
            var state = new Dictionary<string, int>(); // 1 = visiting, 2 = done
            foreach (KitSlot s in slots)
                if (HasCycle(s, ids, state)) { errors.Add($"restsOn cycle through '{s.id}'"); break; }
            return errors;
        }

        static bool HasCycle(KitSlot s, Dictionary<string, KitSlot> ids, Dictionary<string, int> state)
        {
            if (state.TryGetValue(s.id, out int st)) return st == 1;
            state[s.id] = 1;
            if (s.restsOn != null)
                foreach (string r in s.restsOn)
                    if (ids.TryGetValue(r, out KitSlot other) && HasCycle(other, ids, state)) return true;
            state[s.id] = 2;
            return false;
        }

        public static FixMethod ParseFixMethod(string value)
        {
            switch ((value ?? string.Empty).ToLowerInvariant())
            {
                case "nails": return FixMethod.Nails;
                case "screws": return FixMethod.Screws;
                case "mortar": return FixMethod.Mortar;
                default: return FixMethod.None;
            }
        }

        public static string FixMethodId(FixMethod method)
        {
            switch (method)
            {
                case FixMethod.Nails: return "nails";
                case FixMethod.Screws: return "screws";
                case FixMethod.Mortar: return "mortar";
                default: return "none";
            }
        }
    }

    /// <summary>One place in the building that takes exactly one part.</summary>
    [Serializable]
    public class KitSlot
    {
        public string id;
        /// <summary>Part type id from the PartCatalog, e.g. "plank".</summary>
        public string partType;
        /// <summary>Box size of the part in its local frame (m).</summary>
        public Vector3 size;
        /// <summary>Center of the part, build-site local (m).</summary>
        public Vector3 position;
        /// <summary>Euler angles (degrees, Unity order), build-site local.</summary>
        public Vector3 rotation;
        public int stage;
        /// <summary>"nails", "screws" or "mortar".</summary>
        public string fixMethod;
        /// <summary>Slots this one rests on. Empty = rests on the ground. The slot
        /// only accepts a part once all of these are fixed, and a fixed part is
        /// jointed to them.</summary>
        public string[] restsOn;
    }
}
