using SoapCarvers.Core;
using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// Big red button on the workbench. Ready: starts the clock.
    /// Carving: finishes early and scans. Results: restarts.
    /// </summary>
    public class StartButton : MonoBehaviour, IInteractable
    {
        [SerializeField] GameManager game;
        [SerializeField] Transform cap;

        float _press;
        Vector3 _capBase;

        public void Configure(GameManager gameManager, Transform buttonCap)
        {
            game = gameManager;
            cap = buttonCap;
        }

        void Awake()
        {
            if (game == null) game = FindFirstObjectByType<GameManager>();
            if (cap != null) _capBase = cap.localPosition;
        }

        public string GetPrompt(IItemHolder who)
        {
            if (game == null) return null;
            switch (game.State)
            {
                case GameState.Ready: return "E: Start the clock!";
                case GameState.Carving: return "E: I'm done - scan it now";
                case GameState.Results: return "E: Play again";
                default: return null;
            }
        }

        public void Interact(IItemHolder who)
        {
            if (game == null) return;
            _press = 1f;
            switch (game.State)
            {
                case GameState.Ready: game.RequestStartRound(); break;
                case GameState.Carving: game.RequestFinishEarly(); break;
                case GameState.Results: game.RequestRestart(); break;
            }
        }

        void Update()
        {
            if (cap == null) return;
            _press = Mathf.MoveTowards(_press, 0f, Time.deltaTime * 4f);
            cap.localPosition = _capBase - Vector3.up * (0.03f * _press);
        }
    }
}
