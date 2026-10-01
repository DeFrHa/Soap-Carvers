using SoapCarvers.Core;
using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>Cheap explosion visual: an emissive ball that pops and shrinks, plus a light flash.</summary>
    public class ExplosionFlash : MonoBehaviour
    {
        const float Duration = 0.45f;

        float _age;
        float _radius;
        Light _light;

        public static void Spawn(Vector3 position, float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ExplosionFlash";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = material != null
                ? material
                : MaterialFactory.Emissive("ExplosionFlash", new Color(1f, 0.6f, 0.2f), new Color(4f, 2f, 0.5f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var flash = go.AddComponent<ExplosionFlash>();
            flash._radius = radius;
            flash._light = go.AddComponent<Light>();
            flash._light.type = LightType.Point;
            flash._light.color = new Color(1f, 0.7f, 0.35f);
            flash._light.range = radius * 6f;
            flash._light.intensity = 12f;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            // Pop out fast, then collapse.
            float s = t < 0.3f ? Mathf.Lerp(0.2f, 1.2f, t / 0.3f) : Mathf.Lerp(1.2f, 0f, (t - 0.3f) / 0.7f);
            transform.localScale = Vector3.one * (_radius * 2f * s);
            _light.intensity = Mathf.Lerp(12f, 0f, t);
        }
    }
}
