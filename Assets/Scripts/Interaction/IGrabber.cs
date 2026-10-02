using SoapCarvers.Player;
using UnityEngine;

namespace SoapCarvers.Tools
{
    /// <summary>
    /// Anything that can hold one item (a player). Implemented by PlayerActions.
    /// Only <see cref="ItemManager"/> calls <see cref="SetHeldItem"/>.
    /// </summary>
    public interface IItemHolder
    {
        int PlayerId { get; }
        /// <summary>Parent transform for the held item (child of the camera).</summary>
        Transform HoldPoint { get; }
        /// <summary>Where the player aims from (the camera).</summary>
        Transform AimTransform { get; }
        /// <summary>Root of the holder, ignored by its own raycasts.</summary>
        Transform Root { get; }
        CameraShake Shake { get; }
        Vector3 Velocity { get; }
        Holdable HeldItem { get; }
        void SetHeldItem(Holdable item);
    }

    /// <summary>Non-holdable things you can press E on (e.g. the workbench Start button).</summary>
    public interface IInteractable
    {
        /// <summary>Prompt text, or null for "nothing to do here".</summary>
        string GetPrompt(IItemHolder who);
        void Interact(IItemHolder who);
    }
}
