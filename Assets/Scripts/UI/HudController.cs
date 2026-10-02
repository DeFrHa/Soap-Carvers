using System.Text;
using BuildCrew.Building;
using BuildCrew.Core;
using BuildCrew.Player;
using BuildCrew.Round;
using UnityEngine;
using UnityEngine.UI;

namespace BuildCrew.UI
{
    /// <summary>
    /// Screen-space HUD for the LOCAL player: crosshair, timer, state banner,
    /// interaction prompt, look-at info (name, length, weight), held object +
    /// hint + tool status, pockets, stage progress, the briefing screen
    /// (blueprint render) and the results panel. References are public fields
    /// so SceneBuilder can wire them from code.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        public PlayerActions player;

        [Header("In-game")]
        public GameObject crosshair;
        public Text timerText;
        public Text bannerText;
        public Text promptText;
        public Text lookText;
        public Text heldText;
        public Text heldHintText;
        public Text toolStatusText;
        public Text pocketText;
        public Text stageText;
        public Text controlsText;

        [Header("Briefing")]
        public GameObject briefingPanel;
        public RawImage briefingImage;
        public Text briefingText;

        [Header("Results")]
        public GameObject resultsPanel;
        public Text rankText;
        public Text scoreText;
        public Text detailsText;
        public Button restartButton;

        readonly StringBuilder _sb = new StringBuilder();
        TeamArea _area;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerActions>();
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        }

        void OnRestartClicked()
        {
            GameManager game = World.Game;
            if (game != null) game.RequestRestart();
        }

        void Update()
        {
            GameManager game = World.Game;
            if (game == null) return;
            if (_area == null && player != null && player.Grabber != null) _area = game.AreaForTeam(player.Grabber.TeamId);
            GameState state = game.State;
            BuildSite site = _area != null ? _area.Site : null;

            UpdateTimer(game, state);
            UpdateBanner(game, state);

            bool playing = state != GameState.Results && state != GameState.FinalTest;
            if (crosshair != null && crosshair.activeSelf != playing) crosshair.SetActive(playing);
            SetText(promptText, playing && player != null ? player.CurrentPrompt : null);
            SetText(lookText, playing && player != null ? player.LookInfo : null);
            SetText(heldText, playing && player != null ? player.HeldInfo : null);
            SetText(heldHintText, playing && player != null ? player.HeldHint : null);
            SetText(toolStatusText, playing && player != null ? player.ToolStatus : null);
            if (controlsText != null && controlsText.gameObject.activeSelf != playing) controlsText.gameObject.SetActive(playing);

            PlayerInventory inv = player != null ? player.Inventory : null;
            SetText(pocketText, playing && inv != null ? $"Pockets: {inv.Nails} nails, {inv.Screws} screws" : null);
            SetText(stageText, playing ? StageLine(site) : null);

            bool briefing = state == GameState.Briefing;
            if (briefingPanel != null && briefingPanel.activeSelf != briefing) briefingPanel.SetActive(briefing);
            if (briefing) FillBriefing(game);

            bool results = state == GameState.Results;
            if (resultsPanel != null && resultsPanel.activeSelf != results) resultsPanel.SetActive(results);
            if (results) FillResults(game);
        }

        static void SetText(Text t, string value)
        {
            if (t == null) return;
            string v = value ?? string.Empty;
            if (t.text != v) t.text = v;
        }

        void UpdateTimer(GameManager game, GameState state)
        {
            if (timerText == null) return;
            switch (state)
            {
                case GameState.Briefing:
                    timerText.text = GameManager.FormatTime(game.Kit != null ? game.Kit.roundTime : 0f);
                    timerText.color = new Color(1f, 1f, 1f, 0.6f);
                    return;
                case GameState.Dump:
                    timerText.text = GameManager.FormatTime(game.TimeRemaining);
                    timerText.color = new Color(1f, 1f, 1f, 0.6f);
                    return;
                default:
                    timerText.text = GameManager.FormatTime(game.TimeRemaining);
                    bool urgent = state == GameState.Build && game.TimeRemaining < 30f;
                    // Pulse red in the last 30 seconds.
                    timerText.color = urgent
                        ? Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.time * 8f))
                        : Color.white;
                    return;
            }
        }

        void UpdateBanner(GameManager game, GameState state)
        {
            if (bannerText == null) return;
            switch (state)
            {
                case GameState.Briefing:
                    SetText(bannerText, $"BRIEFING - the pile arrives in {Mathf.CeilToInt(game.BriefingTimeLeft)}");
                    break;
                case GameState.Dump:
                    SetText(bannerText, "HERE COMES THE PILE!  (the clock starts when it settles)");
                    break;
                case GameState.Build:
                    SetText(bannerText, game.StateTime < 4f ? "GO GO GO!  Sort the pile, prep the parts, build it!" : null);
                    break;
                case GameState.FinalTest:
                    FinalTestDirector d = _area != null ? _area.FinalTest : null;
                    string kind = d != null && d.Kind == Kits.FinalTestKind.WindRain ? "WIND + RAIN" : "WIND";
                    SetText(bannerText, d != null
                        ? $"FINAL TEST: {kind}!   {d.WindSpeed * 3.6f:0} km/h   ({Mathf.CeilToInt(d.TimeLeft)})"
                        : "FINAL TEST!");
                    break;
                default:
                    SetText(bannerText, null);
                    break;
            }
        }

        string StageLine(BuildSite site)
        {
            if (site == null || site.Kit == null) return null;
            int stage = site.CurrentStage;
            int stages = site.Kit.StageCount();
            if (stage >= stages) return $"{site.Kit.name}: ALL STAGES DONE - ring the bell!";
            site.StageProgress(stage, out int done, out int total);
            return $"{site.Kit.name}  |  Stage {stage + 1}/{stages}: {site.Kit.StageName(stage)}  {done}/{total}  |  Total {site.FixedCount}/{site.TotalCount}";
        }

        void FillBriefing(GameManager game)
        {
            if (briefingImage != null && _area != null && _area.Studio != null && briefingImage.texture != _area.Studio.Texture)
                briefingImage.texture = _area.Studio.Texture;
            if (briefingText == null || game.Kit == null) return;
            _sb.Clear();
            _sb.Append("<size=46><b>").Append(game.Kit.name.ToUpperInvariant()).Append("</b></size>\n");
            if (!string.IsNullOrEmpty(game.Kit.description)) _sb.Append(game.Kit.description).Append('\n');
            _sb.Append('\n').Append("Time: ").Append(GameManager.FormatTime(game.Kit.roundTime))
                .Append("     Final test: ").Append(game.Kit.GetFinalTest() == Kits.FinalTestKind.WindRain ? "wind + rain" : "wind")
                .Append("     Parts: ").Append(game.Kit.slots.Length).Append("\n\n<b>Stages</b>\n");
            for (int i = 0; i < game.Kit.StageCount(); i++) _sb.Append(i + 1).Append(". ").Append(game.Kit.StageName(i)).Append('\n');
            _sb.Append("\nSort the pile onto the pallets, cut parts to length, mix mortar.\n")
                .Append("Carry a part to a GREEN ghost: it snaps in. Then fix it:\n")
                .Append("hammer = 3 hits (wood), screwdriver (door, tin roof), trowel + mortar (stone, bricks).\n")
                .Append("The plan table by the site shows the target and the checklist.");
            briefingText.text = _sb.ToString();
        }

        void FillResults(GameManager game)
        {
            ScoreResult r = game.LastResult;
            if (rankText != null) rankText.text = string.IsNullOrEmpty(r.Rank) ? "???" : r.Rank;
            if (scoreText != null) scoreText.text = $"Score: {r.Score01 * 100f:0}%";
            if (detailsText != null)
            {
                detailsText.text =
                    $"Fixed parts: {r.FixedSlots}/{r.TotalSlots}  ({r.Completion01 * 100f:0}%)\n" +
                    $"Lost in the final test: {r.LostParts}  (-{r.LostPenalty01 * 100f:0}%)\n" +
                    $"Time left: {GameManager.FormatTime(r.TimeRemaining)}  (+{r.TimeBonus01 * 100f:0}%)";
            }
        }
    }
}
