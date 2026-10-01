using UnityEngine;

namespace SoapCarvers.Player
{
    /// <summary>
    /// Sits on the camera (child of the look pivot) and offsets it with Perlin
    /// noise. Two inputs:
    ///   Trauma - one-shot hits (pickaxe, dynamite). Decays; shake ~ trauma^2.
    ///   Jitter - continuous buzz (chainsaw). Must be re-added every frame.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] float maxAngle = 6f;
        [SerializeField] float maxOffset = 0.12f;
        [SerializeField] float traumaDecay = 1.6f;
        [SerializeField] float frequency = 22f;

        float _trauma;
        float _jitter;
        float _jitterThisFrame;
        float _seed;

        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        public void AddJitter(float amount) => _jitterThisFrame = Mathf.Max(_jitterThisFrame, amount);

        void Awake()
        {
            _seed = Random.value * 1000f;
        }

        void LateUpdate()
        {
            _trauma = Mathf.MoveTowards(_trauma, 0f, traumaDecay * Time.deltaTime);
            _jitter = Mathf.Lerp(_jitter, _jitterThisFrame, 1f - Mathf.Exp(-20f * Time.deltaTime));
            _jitterThisFrame = 0f;

            float shake = _trauma * _trauma + _jitter * 0.15f;
            float t = Time.time * frequency;
            // Perlin returns [0,1]; remap to [-1,1] with a different seed per axis.
            float N(float o) => Mathf.PerlinNoise(_seed + o, t) * 2f - 1f;

            transform.localRotation = Quaternion.Euler(N(0f) * maxAngle * shake, N(10f) * maxAngle * shake, N(20f) * maxAngle * shake);
            transform.localPosition = new Vector3(N(30f), N(40f), 0f) * (maxOffset * shake);
        }
    }
}
