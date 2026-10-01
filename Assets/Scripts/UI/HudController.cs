using SoapCarvers.Core;
using SoapCarvers.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SoapCarvers.UI
{
    /// <summary>
    /// Screen-space HUD for the LOCAL player: crosshair, timer, status line,
    /// interaction prompt, held item + hint, and the results panel.
    /// References are public fields so SceneBuilder can wire them from code.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        public GameManager game;
        public PlayerActions player;

        [Header("In-game")]
        public GameObject crosshair;
        public Text timerText;
        public Text statusText;
        public Text promptText;
        public Text heldText;
        public Text heldHintText;
        public Text controlsText;

        [Header("Results")]
        public GameObject resultsPanel;
        public Text rankText;
        public Text scoreText;
        public Text detailsText;
        public Button restartButton;

        void Awake()
        {
            if (game == null) game = FindFirstObjectByType<GameManager>();
            if (player == null) player = FindFirstObjectByType<PlayerActions>();
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        }

        void OnRestartClicked()
        {
            if (game != null) game.RequestRestart();
        }

        void Update()
        {
            if (game == null) return;
            GameState state = game.State;

            if (timerText != null)
            {
                timerText.text = GameManager.FormatTime(game.TimeRemaining);
                bool urgent = state == GameState.Carving && game.TimeRemaining < 30f;
                // Pulse red in the last 30 seconds.
                timerText.color = urgent
                    ? Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.time * 8f))
                    : Color.white;
            }

            if (statusText != null)
            {
                switch (state)
                {
                    case GameState.Ready:
                        statusText.text = "Grab a tool and the blueprint tablet from the workbench.\nThe clock starts on your first carve (or press the red button).";
                        break;
                    case GameState.Carving:
                        statusText.text = string.Empty;
                        break;
                    case GameState.Scanning:
                        statusText.text = "TIME'S UP!  Scanning your masterpiece...";
                        break;
                    default:
                        statusText.text = string.Empty;
                        break;
                }
            }

            bool playing = state != GameState.Results;
            if (crosshair != null) crosshair.SetActive(playing);

            if (promptText != null)
                promptText.text = playing && player != null && player.CurrentPrompt != null ? player.CurrentPrompt : string.Empty;

            if (heldText != null)
                heldText.text = player != null && player.HeldItemName != null ? player.HeldItemName : string.Empty;
            if (heldHintText != null)
                heldHintText.text = player != null && player.HeldItemHint != null ? player.HeldItemHint : string.Empty;

            if (resultsPanel != null)
            {
                bool show = state == GameState.Results;
                if (resultsPanel.activeSelf != show) resultsPanel.SetActive(show);
                if (show) FillResults();
            }
        }

        void FillResults()
        {
            var r = game.LastResult;
            if (rankText != null) rankText.text = string.IsNullOrEmpty(r.Rank) ? "???" : r.Rank;
            if (scoreText != null) scoreText.text = $"Match: {r.Score01 * 100f:0.0}%";
            if (detailsText != null)
            {
                detailsText.text =
                    $"Raw IoU: {r.IoU * 100f:0.0}%   (untouched block: {r.IoUUntouched * 100f:0.0}%)\n" +
                    $"Target shape still intact: {r.TargetIntact01 * 100f:0.0}%\n" +
                    $"Excess soap remaining: {r.ExcessRemaining01 * 100f:0.0}%";
            }
        }
    }
}
