using UnityEngine;

namespace SoapCarvers.Core
{
    /// <summary>
    /// All tunable numbers in one place. The scene builder creates an asset at
    /// Assets/Settings/GameSettings.asset; components fall back to
    /// <see cref="CreateDefault"/> if no asset is assigned.
    /// </summary>
    [CreateAssetMenu(menuName = "Soap Carvers/Game Settings", fileName = "GameSettings")]
    public class GameSettings : ScriptableObject
    {
        [Header("Round")]
        [Tooltip("Round length in seconds.")]
        public float roundSeconds = 300f;
        [Tooltip("Seconds the scanning plane takes to sweep the block.")]
        public float scanSeconds = 3f;
        [Tooltip("Name of the target shape (see TargetLibrary): Swan, Mushroom.")]
        public string targetShapeName = "Swan";

        [Header("Soap block")]
        [Tooltip("Edge length of the cubic soap block in meters.")]
        public float blockSize = 16f;
        [Tooltip("Edge length of one voxel cell in meters.")]
        public float voxelSize = 0.25f;
        [Tooltip("Cells per chunk edge. Each chunk is one mesh + collider.")]
        public int chunkCells = 16;
        public Color soapColor = new Color(1f, 0.78f, 0.86f);
        [Range(0f, 1f)] public float soapSmoothness = 0.65f;

        [Header("Debris")]
        public int maxDebris = 150;
        public float debrisLifetime = 8f;

        [Header("Carving knife")]
        public float knifeRadius = 0.3f;
        public float knifeReach = 2.5f;
        public float knifeCooldown = 0.12f;

        [Header("Pickaxe")]
        public float pickaxeRadius = 1.0f;
        public float pickaxeReach = 3f;
        public float pickaxeCooldown = 0.6f;

        [Header("Chainsaw")]
        public float chainsawRadius = 0.5f;
        public float chainsawInterval = 0.05f;

        [Header("Dynamite")]
        public float dynamiteRadius = 3f;
        public float dynamiteFuse = 3f;
        [Tooltip("Velocity change (m/s) at the blast center; falls off with distance.")]
        public float dynamiteKnockback = 14f;
        public float dynamiteRespawnSeconds = 10f;
        public int dynamiteMaxLying = 3;

        [Header("Player")]
        public float mouseSensitivity = 0.12f;
        public float interactReach = 3f;
        public float throwSpeed = 12f;

        public int CellsPerAxis => Mathf.Max(1, Mathf.RoundToInt(blockSize / voxelSize));

        public static GameSettings CreateDefault()
        {
            var s = CreateInstance<GameSettings>();
            s.name = "GameSettings (runtime default)";
            return s;
        }
    }
}
