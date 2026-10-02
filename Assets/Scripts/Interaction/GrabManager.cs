using System.Collections.Generic;
using BuildCrew.Core;
using UnityEngine;

namespace BuildCrew.Interaction
{
    /// <summary>
    /// Authority over who grabs what. Handles Grab / Release / Place commands;
    /// other systems (snapping, cutting, breaking) call <see cref="ReleaseAll"/>
    /// as a side effect of their own commands.
    /// </summary>
    public class GrabManager : MonoBehaviour
    {
        readonly Dictionary<int, IGrabber> _grabbers = new Dictionary<int, IGrabber>();
        CommandBus _bus;
        bool _registered;

        public IEnumerable<IGrabber> Grabbers => _grabbers.Values;

        void Awake()
        {
            _bus = GetComponent<CommandBus>();
            if (_bus == null) _bus = World.Bus;
            if (_bus == null || _registered) return;
            _bus.Register<GrabCommand>(HandleGrab);
            _bus.Register<ReleaseCommand>(HandleRelease);
            _bus.Register<PlaceCommand>(HandlePlace);
            _registered = true;
        }

        public void RegisterGrabber(IGrabber grabber) => _grabbers[grabber.PlayerId] = grabber;

        public void UnregisterGrabber(IGrabber grabber)
        {
            if (_grabbers.TryGetValue(grabber.PlayerId, out IGrabber g) && g == grabber) _grabbers.Remove(grabber.PlayerId);
        }

        public IGrabber GetGrabber(int playerId) => _grabbers.TryGetValue(playerId, out IGrabber g) ? g : null;

        bool HandleGrab(GrabCommand cmd)
        {
            IGrabber grabber = GetGrabber(cmd.PlayerId);
            Grabbable target = World.Registry != null ? World.Registry.Get(cmd.EntityId) : null;
            if (grabber == null || target == null || !target.CanBeGrabbed) return false;
            if (target.IsGrabbedBy(grabber)) return false;
            GameSettings s = World.Settings;
            if (target.GrabberCount >= s.maxGrabbersPerObject) return false;
            // Tools are held by one player at a time.
            if (target.UsesGrip && target.IsGrabbed) return false;

            if (grabber.Grabbed != null) ReleaseInternal(grabber, false, Vector3.zero);

            Vector3 local = target.UsesGrip ? target.GripLocal : cmd.LocalPoint;
            grabber.PrepareHand(target.transform.TransformPoint(local));
            target.AttachGrabber(grabber, local, s);
            grabber.OnGrabbed(target, local);
            target.OnGrabStarted(grabber);
            return true;
        }

        bool HandleRelease(ReleaseCommand cmd)
        {
            IGrabber grabber = GetGrabber(cmd.PlayerId);
            if (grabber == null || grabber.Grabbed == null) return false;
            ReleaseInternal(grabber, cmd.Throw, cmd.Direction);
            return true;
        }

        bool HandlePlace(PlaceCommand cmd)
        {
            IGrabber grabber = GetGrabber(cmd.PlayerId);
            Grabbable target = World.Registry != null ? World.Registry.Get(cmd.EntityId) : null;
            if (grabber == null || target == null || !target.IsGrabbedBy(grabber)) return false;
            ReleaseAll(target);
            target.PlaceAt(cmd.Position, cmd.Rotation);
            return true;
        }

        void ReleaseInternal(IGrabber grabber, bool throwIt, Vector3 direction)
        {
            Grabbable target = grabber.Grabbed;
            if (target == null)
            {
                grabber.OnReleased();
                return;
            }
            Vector3 point = target.transform.TransformPoint(grabber.GrabLocalPoint);
            target.DetachGrabber(grabber);
            grabber.OnReleased();
            target.OnGrabEnded(grabber);

            if (throwIt && direction.sqrMagnitude > 1e-6f)
            {
                GameSettings s = World.Settings;
                // Same impulse for everyone, capped so light things don't go supersonic.
                float impulse = Mathf.Min(s.throwImpulse, target.Body.mass * s.maxThrowSpeed);
                target.Body.AddForceAtPosition(direction.normalized * impulse, point, ForceMode.Impulse);
            }
        }

        /// <summary>Everyone lets go of <paramref name="target"/> (snapping, cutting, breaking, placing).</summary>
        public void ReleaseAll(Grabbable target)
        {
            if (target == null) return;
            var holders = new List<IGrabber>(target.Grabbers);
            foreach (IGrabber g in holders)
                if (g.Grabbed == target) ReleaseInternal(g, false, Vector3.zero);
                else target.DetachGrabber(g);
        }

        /// <summary>Round restart: empty every hand.</summary>
        public void ReleaseEverything()
        {
            foreach (IGrabber g in new List<IGrabber>(_grabbers.Values))
                if (g.Grabbed != null) ReleaseInternal(g, false, Vector3.zero);
        }
    }
}
