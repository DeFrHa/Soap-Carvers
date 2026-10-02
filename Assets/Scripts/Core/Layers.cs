using UnityEngine;

namespace BuildCrew.Core
{
    /// <summary>
    /// Layer lookup. The editor menu creates a "Hologram" layer in TagManager;
    /// if it does not exist (e.g. GameBootstrap in a fresh project) we fall back
    /// to a raw layer index, which works even without a name.
    /// </summary>
    public static class Layers
    {
        public const string HologramName = "Hologram";
        public const int HologramFallback = 31;

        public static int Hologram
        {
            get
            {
                int l = LayerMask.NameToLayer(HologramName);
                return l >= 0 ? l : HologramFallback;
            }
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }
}
