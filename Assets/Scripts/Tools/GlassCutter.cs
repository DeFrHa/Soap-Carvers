using BuildCrew.Interaction;
using BuildCrew.Parts;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>
    /// Aim at a loose pane, hold LMB and drag the mouse to score a line; when
    /// the score is complete the pane snaps in two along the marked line. The
    /// line runs along whichever pane edge is most aligned with your view's
    /// left-right. Rushing (fast mouse) can crack the whole pane.
    /// </summary>
    public class GlassCutter : CuttingTool
    {
        float _speed;

        protected override CutTool Kind => CutTool.GlassCutter;
        protected override string MarkerMaterial => "scoreMarker";
        protected override string Verb => "Score";
        protected override string TargetNoun => "pane";
        public override bool CapturesMouse => IsUsing && Target != null;
        public override string HeldHint => "Hold LMB + drag the mouse slowly: Score and snap   E: Drop";

        protected override string ExtraStatus =>
            _speed > Settings.glassCrackSpeed * 0.7f ? "\nCareful! Too fast and it cracks" : string.Empty;

        protected override int ChooseAxis(Part part)
        {
            if (Holder == null) return 0;
            Vector3 right = Holder.Aim.right;
            float alongX = Mathf.Abs(Vector3.Dot(part.transform.right, right));
            float alongY = Mathf.Abs(Vector3.Dot(part.transform.up, right));
            // The score line follows the more aligned edge; the cut separates along the other axis.
            return alongX >= alongY ? 1 : 0;
        }

        public override void Use() { }

        protected override void WhileUsing(IGrabber holder, ToolInput input)
        {
            if (Target == null) return;
            float travel = input.MouseDelta.magnitude;
            _speed = Mathf.Lerp(_speed, travel / Mathf.Max(1e-4f, input.DeltaTime), 0.3f);
            if (travel < 0.01f) return;
            AnimOffset = new Vector3(Mathf.Sin(Time.time * 6f) * 0.03f, 0f, 0f);

            if (_speed > Settings.glassCrackSpeed && Random.value < Settings.glassCrackChance * input.DeltaTime)
            {
                Send(new BreakCommand { PartId = Target.EntityId, Point = Target.transform.position });
                if (holder.Shake != null) holder.Shake.AddTrauma(0.2f);
                return;
            }
            Progress += travel / Mathf.Max(1f, Settings.glassScorePixels);
            if (Progress >= 1f) FinishCut();
        }

        protected override void OnStopUse()
        {
            AnimOffset = Vector3.zero;
            _speed = 0f;
        }
    }
}
