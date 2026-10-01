using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>Fast, small, precise. Hold LMB to keep stabbing.</summary>
    public class CarvingKnife : Tool
    {
        [SerializeField] Transform model;

        float _nextStab;
        float _stabAnim;

        public override string HeldHint => "LMB: Carve (small)   Q: Drop   G: Throw";

        public void SetModel(Transform m) => model = m;

        public override void Use()
        {
            if (Time.time < _nextStab) return;
            _nextStab = Time.time + Settings.knifeCooldown;
            _stabAnim = 1f;

            if (!RaycastSoap(Settings.knifeReach, out RaycastHit hit)) return;
            // Center slightly inside the surface so even a tiny radius bites.
            float r = Settings.knifeRadius;
            Vector3 center = hit.point - hit.normal * (r * 0.35f);
            if (Soap.CarveSphere(center, r, OwnerId, 0.5f) && Holder?.Shake != null)
                Holder.Shake.AddTrauma(0.04f);
        }

        void Update()
        {
            if (IsUsing && ToolsEnabled && Time.time >= _nextStab) Use();

            if (model != null)
            {
                // Quick forward jab that decays.
                _stabAnim = Mathf.MoveTowards(_stabAnim, 0f, Time.deltaTime * 9f);
                model.localPosition = new Vector3(0f, 0f, Mathf.Sin(_stabAnim * Mathf.PI) * 0.15f);
            }
        }
    }
}
