using BuildCrew.Interaction;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>
    /// Aim at a bucket, hold LMB and move the mouse in circles to stir. Only
    /// net circling counts (wiggling back and forth cancels out). Sends a
    /// MixCommand every quarter turn.
    /// </summary>
    public class Shovel : Tool
    {
        const float Reach = 2.6f;

        Bucket _bucket;
        float _lastAngle;
        bool _hasAngle;
        float _turned; // signed degrees not yet sent
        float _swirl;

        public override bool CapturesMouse => IsUsing && _bucket != null;
        public override string HeldHint => "Aim at a bucket, hold LMB + move the mouse in circles: Stir   E: Drop";

        public override string StatusText
        {
            get
            {
                Bucket b = _bucket != null ? _bucket : AimedBucket();
                return b != null ? b.LookInfo : "Aim at a bucket to stir it";
            }
        }

        Bucket AimedBucket()
        {
            if (!AimRaycast(Reach, out RaycastHit hit) || hit.rigidbody == null) return null;
            return hit.rigidbody.GetComponent<Bucket>();
        }

        protected override void OnStartUse()
        {
            _bucket = AimedBucket();
            _hasAngle = false;
            _turned = 0f;
        }

        public override void Use() { }

        protected override void WhileUsing(IGrabber holder, ToolInput input)
        {
            if (_bucket == null) return;
            Vector2 d = input.MouseDelta;
            if (d.sqrMagnitude < 2f) return;
            // Circling = the direction of mouse motion keeps turning the same way.
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            if (_hasAngle)
            {
                float diff = Mathf.DeltaAngle(_lastAngle, angle);
                if (Mathf.Abs(diff) < 120f) _turned += diff;
            }
            _lastAngle = angle;
            _hasAngle = true;

            _swirl += Time.deltaTime * 10f;
            AnimOffset = new Vector3(Mathf.Cos(_swirl) * 0.06f, -0.1f, 0.15f + Mathf.Sin(_swirl) * 0.06f);

            if (Mathf.Abs(_turned) >= 90f)
            {
                _turned = 0f;
                Send(new MixCommand { BucketId = _bucket.EntityId, Circles = 0.25f });
            }
        }

        protected override void OnStopUse()
        {
            _bucket = null;
            AnimOffset = Vector3.zero;
        }
    }
}
