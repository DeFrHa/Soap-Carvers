using System.Collections.Generic;
using System.IO;
using BuildCrew.Kits;
using BuildCrew.Parts;
using UnityEditor;
using UnityEngine;

namespace BuildCrew.EditorTools
{
    /// <summary>
    /// Menu: Build Crew/Generate Kit JSONs. Writes the procedural kit designs
    /// (KitDesigns: garden shed, cottage) to Assets/StreamingAssets/Kits/*.json
    /// so slot positions are computed, not hand-typed. Edit the JSON (or add
    /// new files) to make community kits; re-running overwrites the built-in two.
    /// </summary>
    public static class KitGenerator
    {
        [MenuItem("Build Crew/Generate Kit JSONs", priority = 20)]
        public static void GenerateKits() => WriteKits(true);

        /// <summary>Writes every built-in kit; with overwrite = false only missing files.</summary>
        public static void WriteKits(bool overwrite)
        {
            string dir = Path.Combine(Application.dataPath, "StreamingAssets", KitLoader.Folder);
            Directory.CreateDirectory(dir);
            int written = 0;
            foreach (KeyValuePair<string, KitDefinition> kv in KitDesigns.All())
            {
                string path = Path.Combine(dir, kv.Key);
                if (!overwrite && File.Exists(path)) continue;
                List<string> errors = kv.Value.Validate(PartCatalog.IsKnown);
                if (errors.Count > 0)
                    Debug.LogWarning($"[Build Crew] Kit '{kv.Value.name}' has problems:\n- " + string.Join("\n- ", errors));
                File.WriteAllText(path, JsonUtility.ToJson(kv.Value, true));
                written++;
                Debug.Log($"[Build Crew] Wrote {path} ({kv.Value.slots.Length} slots)");
            }
            if (written > 0) AssetDatabase.Refresh();
        }
    }
}
