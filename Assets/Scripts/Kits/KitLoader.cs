using System.Collections.Generic;
using System.IO;
using BuildCrew.Parts;
using UnityEngine;

namespace BuildCrew.Kits
{
    /// <summary>
    /// Loads kits from StreamingAssets/Kits/*.json. Community kits are just more
    /// JSON files there (set GameManager's Custom Kit File). If a built-in kit's
    /// file is missing or broken, the procedural design from KitDesigns is
    /// used instead, so the game always runs.
    /// </summary>
    public static class KitLoader
    {
        public const string Folder = "Kits";

        public static string KitsPath => Path.Combine(Application.streamingAssetsPath, Folder);

        public static KitDefinition Load(string fileName, out string source)
        {
            string path = Path.Combine(KitsPath, fileName);
            KitDefinition kit = null;
            source = path;
            try
            {
                // Desktop/editor only: on Android/WebGL StreamingAssets needs UnityWebRequest.
                if (File.Exists(path)) kit = JsonUtility.FromJson<KitDefinition>(File.ReadAllText(path));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Build Crew] Could not read kit '{path}': {e.Message}");
                kit = null;
            }

            if (kit != null)
            {
                List<string> errors = kit.Validate(PartCatalog.IsKnown);
                if (errors.Count == 0) return kit;
                Debug.LogWarning($"[Build Crew] Kit '{path}' has problems:\n- " + string.Join("\n- ", errors));
                if (kit.slots != null && kit.slots.Length > 0 && KitDesigns.ByFileName(fileName) == null) return kit; // custom kit: best effort
            }

            KitDefinition fallback = KitDesigns.ByFileName(fileName);
            if (fallback != null)
            {
                if (kit == null) Debug.Log($"[Build Crew] Kit file '{path}' not found; using the built-in design. Run Build Crew > Generate Kit JSONs to write it.");
                source = "built-in design (" + fileName + ")";
                return fallback;
            }
            Debug.LogError($"[Build Crew] Kit '{fileName}' not found; falling back to the garden shed.");
            source = "built-in design (" + KitDesigns.GardenShedFile + ")";
            return KitDesigns.GardenShed();
        }
    }
}
