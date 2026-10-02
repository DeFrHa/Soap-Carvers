using System.Collections.Generic;
using System.Text;
using BuildCrew.Core;
using BuildCrew.Interaction;
using BuildCrew.Round;
using UnityEngine;
using UnityEngine.UI;

namespace BuildCrew.Building
{
    /// <summary>
    /// The building plan, pinned to a drafting table next to the site: the
    /// hologram render of the target (E cycles Front / Side / Top / Back), the
    /// current stage, the checklist (built / sorted; parts still in the pile
    /// show "?") and the lengths that still need cutting. Sits on the board
    /// of the table; the table itself is an ordinary heavy rigidbody.
    /// </summary>
    public class PlanTable : MonoBehaviour, IInteractable
    {
        [SerializeField] TeamArea area;
        [SerializeField] Renderer screen;
        [SerializeField] Text titleText;
        [SerializeField] Text timerText;
        [SerializeField] Text viewText;
        [SerializeField] Text infoText;

        readonly StringBuilder _sb = new StringBuilder();
        float _refresh;

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
                MaterialFactory.SetTexture(screen.material, Studio.Texture); // .material = per-table instance
        }

        public string GetPrompt(IGrabber who) => Studio != null ? "E: Next view (Front / Side / Top / Back)" : null;

        public void Interact(IGrabber who)
        {
            if (Studio != null) Studio.CycleView();
            _refresh = 0f;
        }

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
                    : $"{site.Kit.name.ToUpperInvariant()}  -  COMPLETE! Ring the bell";
            }
            if (timerText != null) timerText.text = TimeLabel(game);
            if (viewText != null && Studio != null) viewText.text = ViewLabel(Studio.CurrentView);
            if (infoText != null) infoText.text = BuildInfo(site, stage);
        }

        static string TimeLabel(GameManager game)
        {
            switch (game.State)
            {
                case GameState.Briefing: return "BRIEFING";
                case GameState.Dump: return "PILE INCOMING";
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
            _sb.Append("<b>CHECKLIST</b>   built / needed  (+ sorted on pallets)\n");
            foreach (ChecklistLine l in lines)
            {
                bool done = l.Built >= l.Needed;
                string color = done ? "#2E9E44" : l.Stage == stage ? "#B35C00" : "#1F4E8C";
                string extra = done ? "  OK" : $"  +{l.Sorted}{(l.Built + l.Sorted < l.Needed && unsorted ? " ?" : string.Empty)}";
                _sb.Append("<color=").Append(color).Append('>').Append(l.Label).Append(": ")
                    .Append(l.Built).Append('/').Append(l.Needed).Append(extra).Append("</color>\n");
            }
            List<string> cuts = Checklist.CutList(site);
            if (cuts.Count > 0)
            {
                _sb.Append("\n<b>TO CUT</b>\n");
                foreach (string c in cuts) _sb.Append(c).Append('\n');
            }
            return _sb.ToString();
        }

        static string ViewLabel(BlueprintView current)
        {
            string Mark(BlueprintView v, string label) => v == current ? $"<b>[{label}]</b>" : label;
            return $"{Mark(BlueprintView.Front, "Front")}  {Mark(BlueprintView.Side, "Side")}  " +
                   $"{Mark(BlueprintView.Top, "Top")}  {Mark(BlueprintView.Back, "Back")}   (E: next)";
        }
    }
}
