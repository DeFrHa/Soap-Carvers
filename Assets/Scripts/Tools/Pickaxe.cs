using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// Big chunky swings. Wind up, smash (carve r = 1 m at the impact moment),
    /// recover. Holding LMB keeps swinging every cooldown.
    /// </summary>
    public class Pickaxe : Tool
    {
        [SerializeField] Transform model;
        [SerializeField] float swingDuration = 0.4f;
        [Tooltip("Normalized swing time at which the blow lands.")]
        [SerializeField] float impactAt = 0.45f;

        float _nextSwing;
        float _swingT = -1f;
        bool _impactDone;
        Quaternion _modelBaseRot = Quaternion.identity;

        public override string HeldHint => "LMB: Swing (big)   Q: Drop   G: Throw";

        public void SetModel(Transform m) => model = m;

        protected override void Awake()
        {
            base.Awake();
            if (model != null) _modelBaseRot = model.localRotation;
        }

        public override void Use()
        {
            if (_swingT >= 0f || Time.time < _nextSwing) return;
            _swingT = 0f;
            _impactDone = false;
            _nextSwing = Time.time + Settings.pickaxeCooldown;
        }

        void Update()
        {
            if (IsUsing && ToolsEnabled && _swingT < 0f && Time.time >= _nextSwing) Use();
            if (_swingT < 0f) return;

            _swingT += Time.deltaTime / swingDuration;
            if (!_impactDone && _swingT >= impactAt)
            {
                _impactDone = true;
                Impact();
            }

            if (model != null) model.localRotation = _modelBaseRot * Quaternion.Euler(SwingAngle(_swingT), 0f, 0f);
            if (_swingT >= 1f)
            {
                _swingT = -1f;
                if (model != null) model.localRotation = _modelBaseRot;
            }
        }

        /// <summary>Pitch curve: wind back to -35°, slam to +75° at impact, ease back to 0.</summary>
        float SwingAngle(float t)
        {
            float windup = impactAt * 0.55f;
            if (t < windup) return Mathf.Lerp(0f, -35f, Mathf.SmoothStep(0f, 1f, t / windup));
            if (t < impactAt) return Mathf.Lerp(-35f, 75f, (t - windup) / (impactAt - windup));
            return Mathf.Lerp(75f, 0f, Mathf.SmoothStep(0f, 1f, (t - impactAt) / (1f - impactAt)));
        }

        void Impact()
        {
            if (!ToolsEnabled || Holder == null) return;
            if (RaycastSoap(Settings.pickaxeReach, out RaycastHit hit))
            {
                Soap.CarveSphere(hit.point, Settings.pickaxeRadius, OwnerId, 1.2f);
                Holder.Shake?.AddTrauma(0.3f);
            }
            else
            {
                Holder.Shake?.AddTrauma(0.05f); // whiff
            }
        }
    }
}
