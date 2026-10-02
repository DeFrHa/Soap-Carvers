using System.Collections.Generic;
using System.IO;
using BuildCrew.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildCrew.EditorTools
{
    /// <summary>
    /// Menu: Build Crew/Create Playable Scene.
    /// Creates the "Hologram" layer, writes the kit JSONs if they are missing,
    /// builds the scene with SceneBuilder, saves generated materials to
    /// Assets/Materials and settings to Assets/Settings/GameSettings.asset
    /// (kept if it already exists, so your tuning survives a rebuild), saves
    /// Assets/Scenes/BuildCrew.unity and adds it to the build settings.
    /// Pick the kit afterwards on the GameManager (dropdown).
    /// </summary>
    public static class BuildCrewSceneMenu
    {
        const string ScenePath = "Assets/Scenes/BuildCrew.unity";

        [MenuItem("Build Crew/Create Playable Scene", priority = 0)]
        public static void CreatePlayableScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            KitGenerator.WriteKits(false);

            EnsureLayer(Layers.HologramName, Layers.HologramFallback);
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Settings");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new SceneBuilder(PersistAsset, KitChoice.GardenShed).Build();

            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[Build Crew] Failed to save {ScenePath}");
                return;
            }
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[Build Crew] Created {ScenePath}. Press Play! (Kit: GameManager > Kit)");
        }

        [MenuItem("Build Crew/Open Playable Scene", priority = 1)]
        public static void OpenPlayableScene()
        {
            if (!File.Exists(ScenePath))
            {
                CreatePlayableScene();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>SceneBuilder persistence hook: path is relative to Assets/.</summary>
        static Object PersistAsset(Object asset, string relativePath)
        {
            string path = "Assets/" + relativePath;
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));

            Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                // Keep the user's tuned settings across rebuilds.
                if (existing is GameSettings) return existing;
                if (existing.GetType() == asset.GetType())
                {
                    // Overwrite in place so the GUID (and any references) stay valid.
                    EditorUtility.CopySerialized(asset, existing);
                    EditorUtility.SetDirty(existing);
                    return existing;
                }
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>
        /// Adds a named user layer via the TagManager asset. Prefers
        /// <paramref name="preferredIndex"/> so it matches the runtime fallback index.
        /// </summary>
        static void EnsureLayer(string layerName, int preferredIndex)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[Build Crew] Could not open TagManager; add a layer named 'Hologram' manually (see SETUP.md).");
                return;
            }
            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || !layers.isArray) return;

            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;

            var candidates = new List<int> { preferredIndex };
            for (int i = 31; i >= 8; i--) if (i != preferredIndex) candidates.Add(i);
            foreach (int i in candidates)
            {
                if (i >= layers.arraySize) continue;
                SerializedProperty p = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(p.stringValue)) continue;
                p.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[Build Crew] Added layer '{layerName}' at index {i}.");
                return;
            }
            Debug.LogWarning($"[Build Crew] No free layer slot for '{layerName}'.");
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene s in scenes)
                if (s.path == path) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
