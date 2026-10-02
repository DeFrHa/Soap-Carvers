using BuildCrew.Interaction;
using BuildCrew.Parts;
using BuildCrew.Core;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>
    /// Aim at a loose plank/post/beam, hold LMB and move the mouse left and
    /// right: every direction change after enough travel is a stroke. After N
    /// strokes the part splits at the marked point into two parts of the shown
    /// lengths. Beams need the two-person saw: two players sawing the same beam
    /// go full speed, one alone goes at soloTwoPersonSawSpeed.
    /// </summary>
    public class Saw : CuttingTool
    {
        float _travel;
        int _dir;
        float _strokeAnim;

        protected override CutTool Kind => CutTool.Saw;
        protected override string MarkerMaterial => "cutMarker";
        protected override string Verb => "Saw";
        protected override string TargetNoun => "plank, post or beam";
        public override bool CapturesMouse => IsUsing && Target != null;
        public override string HeldHint => "Hold LMB + move mouse left/right: Saw   E: Drop   G: Throw";

        protected override string ExtraStatus =>
            Target != null && Target.Definition.NeedsTwoPersonSaw
                ? (World.Parts != null && World.Parts.SawyersOn(Target.EntityId) >= 2
                    ? "\nTwo-person saw: full speed!"
                    : "\nTwo-person saw: a buddy on the other end doubles the speed")
                : string.Empty;

        protected override void OnStartUse()
        {
            _travel = 0f;
            _dir = 0;
        }

        public override void Use() { }

        protected override void WhileUsing(IGrabber holder, ToolInput input)
        {
            if (Target == null) return;
            float dx = input.MouseDelta.x;
            if (Mathf.Abs(dx) < 0.01f) return;

            int d = dx > 0f ? 1 : -1;
            if (d != _dir)
            {
                if (_travel >= Settings.sawStrokePixels) Stroke();
                _travel = 0f;
                _dir = d;
            }
            _travel += Mathf.Abs(dx);

            // Push the saw back and forth along the view (the joint drags the real saw with it).
            _strokeAnim = Mathf.Clamp(_strokeAnim + dx * 0.0015f, -0.12f, 0.12f);
            AnimOffset = new Vector3(0f, 0f, _strokeAnim);
            if (holder.Shake != null) holder.Shake.AddJitter(0.25f);
            if (World.Parts != null) World.Parts.ReportSawing(Target.EntityId, OwnerId);
        }

        void Stroke()
        {
            if (Target == null) return;
            PartDefinition def = Target.Definition;
            float speed = 1f;
            if (def.NeedsTwoPersonSaw)
            {
                int sawyers = World.Parts != null ? World.Parts.SawyersOn(Target.EntityId) : 1;
                speed = sawyers >= 2 ? 1f : Settings.soloTwoPersonSawSpeed;
            }
            Progress += speed / Mathf.Max(1, def.SawStrokes(Settings));
            if (Progress >= 0.999f) FinishCut();
        }

        protected override void OnStopUse()
        {
            AnimOffset = Vector3.zero;
            _strokeAnim = 0f;
        }
    }
}
