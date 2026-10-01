using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// Hold LMB: every chainsawInterval seconds carves a capsule along the blade
    /// (from bladeStart to bladeEnd, wherever the blade currently is), with
    /// camera jitter. Just push it into the soap.
    /// </summary>
    public class Chainsaw : Tool
    {
        [SerializeField] Transform model;
        [SerializeField] Transform bladeStart;
        [SerializeField] Transform bladeEnd;

        float _nextCut;
        Vector3 _modelBasePos;

        public override string HeldHint => "Hold LMB: Rev & cut   Q: Drop   G: Throw";

        public void SetParts(Transform m, Transform start, Transform end)
        {
            model = m;
            bladeStart = start;
            bladeEnd = end;
        }

        protected override void Awake()
        {
            base.Awake();
            if (model != null) _modelBasePos = model.localPosition;
        }

        // Continuous tool: the work happens in Update while IsUsing.
        protected override void OnStartUse() { }

        public override void Use()
        {
            if (bladeStart == null || bladeEnd == null) return;
            Soap.CarveCapsule(bladeStart.position, bladeEnd.position, Settings.chainsawRadius, OwnerId, 0.25f);
        }

        void Update()
        {
            bool running = IsUsing && ToolsEnabled && IsHeld;
            if (running)
            {
                if (Time.time >= _nextCut)
                {
                    _nextCut = Time.time + Settings.chainsawInterval;
                    Use();
                }
                Holder.Shake?.AddJitter(0.5f);
            }

            if (model != null)
            {
                // Idle putter when held, angry buzz when cutting.
                float amp = running ? 0.012f : (IsHeld ? 0.002f : 0f);
                model.localPosition = _modelBasePos + Random.insideUnitSphere * amp;
            }
        }
    }
}
