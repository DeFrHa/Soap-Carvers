using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// Lives on the workbench. Every N seconds, if fewer than the max unexploded
    /// sticks exist, clones the (inactive) template stick onto the bench.
    /// </summary>
    public class DynamiteSpawner : MonoBehaviour
    {
        [SerializeField] Dynamite template;
        [SerializeField] Transform spawnPoint;
        [SerializeField] float interval = 10f;
        [SerializeField] int maxLying = 3;

        float _timer;

        public void Configure(Dynamite templateStick, Transform point, float seconds, int max)
        {
            template = templateStick;
            spawnPoint = point;
            interval = seconds;
            maxLying = max;
        }

        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < interval) return;
            _timer = 0f;
            if (template == null) return;

            // FindObjectsByType skips inactive objects, so the template and
            // exploded scene sticks are not counted.
            int count = 0;
            foreach (Dynamite d in FindObjectsByType<Dynamite>(FindObjectsSortMode.None))
                if (!d.HasExploded) count++;
            if (count >= maxLying) return;

            Transform p = spawnPoint != null ? spawnPoint : transform;
            Vector3 pos = p.position + new Vector3(Random.Range(-0.25f, 0.25f), 0.15f, Random.Range(-0.1f, 0.1f));
            Dynamite stick = Instantiate(template, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f));
            stick.name = "Dynamite";
            stick.MarkSpawnedAtRuntime();
            stick.AssignId(0); // ItemManager hands out a fresh dynamic id on register
            stick.gameObject.SetActive(true);
        }
    }
}
