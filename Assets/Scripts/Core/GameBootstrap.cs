using UnityEngine;

namespace BuildCrew.Core
{
    /// <summary>
    /// Drop this on an empty GameObject in any (empty) scene and press Play:
    /// it builds the whole game at runtime with the same SceneBuilder the editor
    /// menu uses. Does nothing if a GameManager already exists (e.g. in the
    /// generated BuildCrew.unity scene).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Remove any cameras/lights already in the scene (e.g. the default Main Camera) before building.")]
        [SerializeField] bool removeExistingCamerasAndLights = true;
        [SerializeField] KitChoice kit = KitChoice.GardenShed;

        void Awake()
        {
            if (FindFirstObjectByType<GameManager>() != null)
            {
                Destroy(gameObject);
                return;
            }

            if (removeExistingCamerasAndLights)
            {
                foreach (Camera c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(c.gameObject);
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);
            }

            new SceneBuilder(null, kit).Build();
            Destroy(gameObject);
        }
    }
}
