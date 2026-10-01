using SoapCarvers.Player;
using SoapCarvers.Soap;
using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// LMB lights the fuse, G throws it, and after the fuse it carves a big
    /// sphere, shoves every rigidbody and player nearby, shakes cameras and
    /// throws a huge debris burst. Works whether held, thrown or lying around.
    ///
    /// Sticky: when a free stick touches soap it freezes in place (kinematic),
    /// and E while looking at soap jams it in by hand (Place command). It drops
    /// off again if the soap it is stuck to gets carved away.
    /// </summary>
    public class Dynamite : Tool
    {
        [SerializeField] GameObject sparkVisual;
        [SerializeField] Light sparkLight;
        [SerializeField] Material flashMaterial;

        const float StickCheckInterval = 0.25f;
        const float StickProbeRadius = 0.25f;

        bool _lit;
        bool _exploded;
        bool _stuck;
        Vector3 _anchor;          // a point on the soap surface it is stuck to
        float _nextStickCheck;
        float _fuseLeft;
        int _lastOwnerId = -1;

        public bool IsLit => _lit;
        public bool HasExploded => _exploded;
        public bool IsStuck => _stuck;
        public override bool CanBePlaced => true;

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
            _stuck = false;
            base.OnAttached(holder);
            _lastOwnerId = holder.PlayerId;
        }

        /// <summary>E while looking at soap within reach: jam the stick into the surface.</summary>
        public override bool TryGetPlacement(IItemHolder holder, out Vector3 position, out Quaternion rotation, out string prompt)
        {
            position = default;
            rotation = Quaternion.identity;
            prompt = null;
            if (!RaycastSoap(Settings.interactReach, out RaycastHit hit)) return false;
            // Stick axis (local +Y) along the surface normal, fuse pointing out, half buried.
            rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            position = hit.point + hit.normal * 0.04f;
            prompt = _lit ? "E: Stick it here! (it's lit!)" : "E: Stick dynamite to the soap";
            return true;
        }

        public override void PlaceAt(Vector3 position, Quaternion rotation)
        {
            base.PlaceAt(position, rotation);
            StickHere(position);
        }

        // Thrown/dropped sticks glue themselves to the first soap they touch.
        void OnCollisionEnter(Collision collision)
        {
            if (IsHeld || _stuck || _exploded) return;
            if (collision.collider.GetComponentInParent<SoapChunk>() == null) return;
            StickHere(collision.contactCount > 0 ? collision.GetContact(0).point : transform.position);
        }

        void StickHere(Vector3 surfacePoint)
        {
            _stuck = true;
            _anchor = surfacePoint;
            _nextStickCheck = Time.time + StickCheckInterval;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;
        }

        void Unstick()
        {
            _stuck = false;
            Body.isKinematic = false;
            Body.WakeUp();
        }

        void Update()
        {
            // Soap under the stick carved away? Fall off.
            if (_stuck && Time.time >= _nextStickCheck)
            {
                _nextStickCheck = Time.time + StickCheckInterval;
                if (Soap == null || !Soap.IsSolidNear(_anchor, StickProbeRadius)) Unstick();
            }

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
            _stuck = false;
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
