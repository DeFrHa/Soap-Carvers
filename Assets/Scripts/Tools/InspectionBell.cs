using BuildCrew.Core;
using BuildCrew.Interaction;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>A bell on a post at the build site: ring it to call the inspector early (time bonus).</summary>
    public class InspectionBell : MonoBehaviour, IInteractable
    {
        public string GetPrompt(IGrabber who)
        {
            GameManager game = World.Game;
            return game != null && game.State == GameState.Build ? "E: Ring the bell - we're done, start the final test!" : null;
        }

        public void Interact(IGrabber who)
        {
            GameManager game = World.Game;
            if (game != null) game.RequestFinishEarly();
        }
    }
}
