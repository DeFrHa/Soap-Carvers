using SoapCarvers.Player;
using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// LMB lights the fuse, G throws it, and after the fuse it carves a big
    /// sphere, shoves every rigidbody and player nearby, shakes cameras and
    /// throws a huge debris burst. Works whether held, thrown or lying around.
    /// </summary>
    public class Dynamite : Tool
    {
        [SerializeField] GameObject sparkVisual;
        [SerializeField] Light sparkLight;
        [SerializeField] Material flashMaterial;

        bool _lit;
        bool _exploded;
        float _fuseLeft;
        int _lastOwnerId = -1;

        public bool IsLit => _lit;
        public bool HasExploded => _exploded;

        public override string PickupPrompt => _lit ? "E: Grab the LIT dynamite!" : base.PickupPrompt;
        public override string HeldHint => _lit
            ? $"G: THROW IT! ({Mathf.CeilToInt(_fuseLeft)})"
            : "LMB: Light fuse   G: Throw   Q: Drop";

        public void SetParts(GameObject spark, Light light, Material flash)
        {
            sparkVisual = spark;
            sparkLight = light;
            flashMaterial = flash;
        }

        protected override void Awake()
        {
            base.Awake();
            SetSpark(false);
        }

        public override void Use()
        {
            if (_lit || _exploded) return;
            _lit = true;
            _fuseLeft = Settings.dynamiteFuse;
            SetSpark(true);
        }

        public override void OnAttached(IItemHolder holder)
        {
            base.OnAttached(holder);
            _lastOwnerId = holder.PlayerId;
        }

        void Update()
        {
            if (!_lit || _exploded) return;
            _fuseLeft -= Time.deltaTime;
            if (sparkVisual != null)
                sparkVisual.transform.localScale = Vector3.one * Random.Range(0.05f, 0.09f);
            if (sparkLight != null)
                sparkLight.intensity = Random.Range(1.5f, 3.5f) * (1f + 2f * Mathf.Clamp01(1f - _fuseLeft));
            if (_fuseLeft <= 0f) Explode();
        }

        void Explode()
        {
            _exploded = true;
            Vector3 pos = transform.position;
            float radius = Settings.dynamiteRadius;
            if (Manager != null) Manager.ForceRelease(this);

            // 1) Soap: one big carve command (filtered by game state like every carve).
            if (Soap != null) Soap.CarveSphere(pos, radius, _lastOwnerId, 4f);

            // 2) Physics shove. VelocityChange so tiny chips and heavy ladders both fly sensibly.
            float blastRadius = radius * 2.5f;
            float knock = Settings.dynamiteKnockback;
            foreach (Collider c in Physics.OverlapSphere(pos, blastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Rigidbody rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic || rb == Body) continue;
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(pos, rb.worldCenterOfMass) / blastRadius);
                Vector3 dir = (rb.worldCenterOfMass - pos).normalized + Vector3.up * 0.5f;
                rb.AddForce(dir.normalized * knock * falloff, ForceMode.VelocityChange);
            }

            // 3) Players: knockback and screen shake by distance.
            foreach (PlayerMotor motor in FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
            {
                Vector3 to = motor.transform.position + Vector3.up - pos;
                float falloff = 1f - Mathf.Clamp01(to.magnitude / blastRadius);
                if (falloff > 0f) motor.AddImpulse((to.normalized + Vector3.up * 0.6f).normalized * knock * 0.8f * falloff);
            }
            foreach (CameraShake shake in FindObjectsByType<CameraShake>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(shake.transform.position, pos);
                shake.AddTrauma(Mathf.Lerp(1f, 0.1f, Mathf.Clamp01(d / (blastRadius * 3f))));
            }

            ExplosionFlash.Spawn(pos, radius, flashMaterial);

            if (SpawnedAtRuntime) Destroy(gameObject);
            else gameObject.SetActive(false); // scene-placed sticks come back on restart
        }

        public override void ResetToHome()
        {
            base.ResetToHome();
            _lit = false;
            _exploded = false;
            SetSpark(false);
        }

        void SetSpark(bool on)
        {
            if (sparkVisual != null) sparkVisual.SetActive(on);
            if (sparkLight != null) sparkLight.enabled = on;
        }
    }
}
