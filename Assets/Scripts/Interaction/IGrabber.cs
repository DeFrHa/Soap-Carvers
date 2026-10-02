using BuildCrew.Player;
using UnityEngine;

namespace BuildCrew.Interaction
{
    /// <summary>
    /// Something with one hand that can grab a Grabbable (a player). The hand
    /// is a kinematic rigidbody the grab joint pulls toward. Only
    /// <see cref="GrabManager"/> calls <see cref="OnGrabbed"/>/<see cref="OnReleased"/>.
    /// </summary>
    public interface IGrabber
    {
        int PlayerId { get; }
        int TeamId { get; }
        /// <summary>Kinematic anchor body the grab joint connects to.</summary>
        Rigidbody Hand { get; }
        /// <summary>Where the player aims from (the camera).</summary>
        Transform Aim { get; }
        /// <summary>Root of the grabber, ignored by its own raycasts.</summary>
        Transform Root { get; }
        CameraShake Shake { get; }
        PlayerInventory Inventory { get; }
        Vector3 Velocity { get; }
        Grabbable Grabbed { get; }
        /// <summary>Grab point in the grabbed object's local space.</summary>
        Vector3 GrabLocalPoint { get; }

        /// <summary>Teleport the hand to the grab point before the joint is created.</summary>
        void PrepareHand(Vector3 worldPoint);
        void OnGrabbed(Grabbable target, Vector3 localPoint);
        void OnReleased();
    }

    /// <summary>Things you press E on that aren't (only) grabbed: dispensers, nail boxes, the inspection bell.</summary>
    public interface IInteractable
    {
        /// <summary>Prompt text, or null if there's nothing to do for this grabber right now (E then grabs instead).</summary>
        string GetPrompt(IGrabber who);
        void Interact(IGrabber who);
    }
}
