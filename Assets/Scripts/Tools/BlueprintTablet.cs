using System.Collections.Generic;
using System.Text;
using BuildCrew.Building;
using BuildCrew.Core;
using BuildCrew.Interaction;
using BuildCrew.Round;
using UnityEngine;
using UnityEngine.UI;

namespace BuildCrew.Tools
{
    /// <summary>
    /// The blueprint tablet. Carried low in the hand; hold RMB or Tab to raise
    /// it in front of your face. Keys 1-4 (or LMB while raised) switch the
    /// target view Front / Side / Top / Back. Next to the hologram render it
    /// shows the current stage, the checklist (built / sorted; the pile is "?")
    /// and the lengths that still need cutting.
    /// </summary>
    public class BlueprintTablet : Tool
    {
        [SerializeField] TeamArea area;
        [SerializeField] Renderer screen;
        [SerializeField] Text titleText;
        [SerializeField] Text timerText;
        [SerializeField] Text viewText;
        [SerializeField] Text infoText;
        [SerializeField] Vector3 raisedPosition = new Vector3(0f, -0.03f, 0.5f);
        [SerializeField] Vector3 raisedEuler = Vector3.zero;

        readonly StringBuilder _sb = new StringBuilder();
        bool _raised;
        float _refresh;

        public bool IsRaised => _raised && IsGrabbed;
        public override bool WorksOutsideBuild => true;
        public override string LookInfo => "Blueprint tablet";
        public override string HeldHint => "Hold RMB/Tab: Look   1-4: Front/Side/Top/Back   E: Drop";

        public void SetParts(TeamArea teamArea, Renderer screenRenderer, Text title, Text timer, Text view, Text info)
        {
            area = teamArea;
            screen = screenRenderer;
            titleText = title;
            timerText = timer;
            viewText = view;
            infoText = info;
        }

        BlueprintStudio Studio => area != null ? area.Studio : null;

        void Start()
        {
            if (screen != null && Studio != null && Studio.Texture != null)
                MaterialFactory.SetTexture(screen.material, Studio.Texture); // .material = per-tablet instance
        }

        public override void Tick(IGrabber holder, ToolInput input)
        {
            _raised = input.SecondaryHeld;
            if (Studio == null) return;
            if (input.ViewKey >= 0) Studio.SetView((BlueprintView)input.ViewKey);
            if (_raised && input.PrimaryPressed) Studio.CycleView();
        }

        public override void Use() { }

        public override void GetHoldPose(out Vector3 localPosition, out Quaternion localRotation)
        {
            if (_raised)
            {
                localPosition = raisedPosition;
                localRotation = Quaternion.Euler(raisedEuler);
            }
            else base.GetHoldPose(out localPosition, out localRotation);
        }

        protected override void OnDropped() => _raised = false;

        void Update()
        {
            _refresh -= Time.deltaTime;
            if (_refresh > 0f) return;
            _refresh = 0.25f;
            GameManager game = World.Game;
            BuildSite site = area != null ? area.Site : null;
            if (game == null || site == null || site.Kit == null) return;

            int stage = site.CurrentStage;
            int stages = site.Kit.StageCount();
            if (titleText != null)
            {
                titleText.text = stage < stages
                    ? $"{site.Kit.name.ToUpperInvariant()}  -  Stage {stage + 1}/{stages}: {site.Kit.StageName(stage)}"
                    : $"{site.Kit.name.ToUpperInvariant()}  -  COMPLETE!";
            }
            if (timerText != null) timerText.text = HudTime(game);
            if (viewText != null && Studio != null) viewText.text = ViewLabel(Studio.CurrentView);
            if (infoText != null) infoText.text = BuildInfo(site, stage);
        }

        static string HudTime(GameManager game)
        {
            switch (game.State)
            {
                case GameState.Briefing: return "BRIEFING";
                case GameState.Dump: return "INCOMING!";
                case GameState.Build: return GameManager.FormatTime(game.TimeRemaining);
                case GameState.FinalTest: return "FINAL TEST";
                default: return $"SCORE {game.LastResult.Score01 * 100f:0}%";
            }
        }

        string BuildInfo(BuildSite site, int stage)
        {
            _sb.Clear();
            List<ChecklistLine> lines = Checklist.Compute(site, area.Zones);
            bool unsorted = Checklist.AnyUnsorted(area.Zones);
            _sb.Append("<b>CHECKLIST</b>  built / need  (+sorted)\n");
            foreach (ChecklistLine l in lines)
            {
                bool done = l.Built >= l.Needed;
                string color = done ? "#7CFF8A" : l.Stage == stage ? "#FFE066" : "#9FC6FF";
                string extra = done ? string.Empty : $"  +{l.Sorted}{(l.Built + l.Sorted < l.Needed && unsorted ? " ?" : string.Empty)}";
                _sb.Append("<color=").Append(color).Append('>').Append(l.Label).Append(": ")
                    .Append(l.Built).Append('/').Append(l.Needed).Append(extra).Append("</color>\n");
            }
            List<string> cuts = Checklist.CutList(site);
            if (cuts.Count > 0)
            {
                _sb.Append("\n<b>CUT</b>\n");
                foreach (string c in cuts) _sb.Append(c).Append('\n');
            }
            return _sb.ToString();
        }

        static string ViewLabel(BlueprintView current)
        {
            string Mark(BlueprintView v, string label) => v == current ? $"<b>[{label}]</b>" : label;
            return $"{Mark(BlueprintView.Front, "1 Front")}  {Mark(BlueprintView.Side, "2 Side")}  " +
                   $"{Mark(BlueprintView.Top, "3 Top")}  {Mark(BlueprintView.Back, "4 Back")}";
        }
    }
}
