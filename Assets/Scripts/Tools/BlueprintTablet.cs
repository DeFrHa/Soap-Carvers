using SoapCarvers.Core;
using SoapCarvers.Targets;
using UnityEngine;
using UnityEngine.UI;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// The blueprint tablet (think Sea of Thieves map). Carried low in the hand;
    /// hold RMB or Tab to raise it in front of your face. Keys 1-4 (or LMB while
    /// raised) switch Front / Side / Top / Back. The screen shows the
    /// BlueprintStudio RenderTexture plus shape name, view and the timer.
    /// </summary>
    public class BlueprintTablet : Holdable
    {
        [SerializeField] BlueprintStudio studio;
        [SerializeField] Renderer screen;
        [SerializeField] Text titleText;
        [SerializeField] Text timerText;
        [SerializeField] Text viewText;
        [SerializeField] Vector3 raisedPosition = new Vector3(0f, -0.02f, 0.42f);
        [SerializeField] Vector3 raisedEuler = Vector3.zero;

        GameManager _game;
        bool _raised;

        public bool IsRaised => _raised && IsHeld;
        public override string PickupPrompt => "E: Pick up Blueprint Tablet";
        public override string HeldHint => "Hold RMB/Tab: Look   1-4: Front/Side/Top/Back   Q: Drop";

        public void SetParts(BlueprintStudio blueprintStudio, Renderer screenRenderer, Text title, Text timer, Text view)
        {
            studio = blueprintStudio;
            screen = screenRenderer;
            titleText = title;
            timerText = timer;
            viewText = view;
        }

        void Start()
        {
            _game = FindFirstObjectByType<GameManager>();
            if (studio == null) studio = FindFirstObjectByType<BlueprintStudio>();
            if (screen != null && studio != null && studio.Texture != null)
                MaterialFactory.SetTexture(screen.material, studio.Texture); // .material = per-tablet instance
        }

        public void SetRaised(bool raised) => _raised = raised;

        public void SetView(BlueprintView view)
        {
            if (studio != null) studio.SetView(view);
        }

        public void CycleView()
        {
            if (studio == null) return;
            SetView((BlueprintView)(((int)studio.CurrentView + 1) % 4));
        }

        public override void OnReleased(Vector3 velocity)
        {
            _raised = false;
            base.OnReleased(velocity);
        }

        protected override void GetHoldPose(out Vector3 localPosition, out Quaternion localRotation)
        {
            if (_raised)
            {
                localPosition = raisedPosition;
                localRotation = Quaternion.Euler(raisedEuler);
            }
            else
            {
                base.GetHoldPose(out localPosition, out localRotation);
            }
        }

        void Update()
        {
            if (titleText != null && studio != null) titleText.text = $"TARGET: {studio.ShapeName.ToUpperInvariant()}";
            if (viewText != null && studio != null)
                viewText.text = ViewLabel(studio.CurrentView);
            if (timerText != null && _game != null)
            {
                switch (_game.State)
                {
                    case GameState.Ready: timerText.text = $"{GameManager.FormatTime(_game.TimeRemaining)}  (starts on first carve)"; break;
                    case GameState.Carving: timerText.text = GameManager.FormatTime(_game.TimeRemaining); break;
                    case GameState.Scanning: timerText.text = "SCANNING..."; break;
                    default: timerText.text = $"SCORE {_game.LastResult.Score01 * 100f:0}%"; break;
                }
            }
        }

        static string ViewLabel(BlueprintView current)
        {
            string Mark(BlueprintView v, string label) => v == current ? $"<b>[{label}]</b>" : label;
            return $"{Mark(BlueprintView.Front, "1 Front")}  {Mark(BlueprintView.Side, "2 Side")}  " +
                   $"{Mark(BlueprintView.Top, "3 Top")}  {Mark(BlueprintView.Back, "4 Back")}";
        }
    }
}
