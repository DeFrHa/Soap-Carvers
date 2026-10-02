using BuildCrew.Building;
using BuildCrew.Kits;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>
    /// Swing (hold LMB to keep swinging). At the bottom of the swing it nails
    /// the snapped slot you look at (one nail per hit, 3 hits fix a part), or
    /// bonks whatever rigidbody is there. The head is a real collider, so the
    /// swing itself can knock things too.
    /// </summary>
    public class Hammer : FixingTool
    {
        [SerializeField] float swingSeconds = 0.42f;
        [SerializeField] float bonkImpulse = 5f;

        float _swingTime = -1f;
        bool _hitDone;

        public override FixMethod Method => FixMethod.Nails;
        public override string HeldHint => "LMB: Swing (nail placed parts)   E: Drop   G: Throw";

        protected override void OnStartUse() => Use();

        public override void Use()
        {
            if (_swingTime >= 0f) return;
            _swingTime = 0f;
            _hitDone = false;
        }

        void Update()
        {
            if (_swingTime < 0f) return;
            _swingTime += Time.deltaTime;
            float t = _swingTime / swingSeconds;

            // Raise (0-0.4), strike (0.4-0.55), recover (0.55-1). Angle about the hammer's local x.
            float angle;
            if (t < 0.4f) angle = Mathf.Lerp(0f, -55f, Mathf.SmoothStep(0f, 1f, t / 0.4f));
            else if (t < 0.55f) angle = Mathf.Lerp(-55f, 40f, (t - 0.4f) / 0.15f);
            else angle = Mathf.Lerp(40f, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f));
            AnimRotation = Quaternion.Euler(angle, 0f, 0f);
            AnimOffset = new Vector3(0f, t < 0.55f ? -0.05f * t : 0f, t > 0.4f && t < 0.7f ? 0.12f : 0f);

            if (!_hitDone && t >= 0.52f)
            {
                _hitDone = true;
                Hit();
            }
            if (t >= 1f)
            {
                _swingTime = -1f;
                AnimRotation = Quaternion.identity;
                AnimOffset = Vector3.zero;
                if (IsUsing && ToolsEnabled) Use(); // keep swinging while held
            }
        }

        void Hit()
        {
            if (Holder == null) return;
            BuildSlot slot = AimedSlot(out RaycastHit hit);
            if (slot != null)
            {
                if (TryFix(slot) && Holder.Shake != null) Holder.Shake.AddTrauma(0.18f);
                return;
            }
            if (hit.collider == null) return;
            if (Holder.Shake != null) Holder.Shake.AddTrauma(0.1f);
            // Bonk.
            Rigidbody rb = hit.rigidbody;
            if (rb != null && !rb.isKinematic)
                rb.AddForceAtPosition(Holder.Aim.forward * bonkImpulse, hit.point, ForceMode.Impulse);
        }

        protected override void OnDropped() => _swingTime = -1f;
    }
}
