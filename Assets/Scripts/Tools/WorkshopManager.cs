using BuildCrew.Core;
using BuildCrew.Interaction;
using BuildCrew.Parts;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>
    /// Authority for workshop actions: mortar (add ingredient, mix, scoop,
    /// spill) and taking handfuls of nails/screws.
    /// </summary>
    public class WorkshopManager : MonoBehaviour
    {
        void Awake()
        {
            CommandBus bus = GetComponent<CommandBus>();
            if (bus == null) bus = World.Bus;
            if (bus == null) return;
            bus.Register<AddIngredientCommand>(c => Get<Bucket>(c.BucketId) is Bucket b && b.Add(c.Ingredient, Mathf.Max(1, c.Amount)));
            bus.Register<MixCommand>(c => Get<Bucket>(c.BucketId) is Bucket b && b.Stir(Mathf.Clamp(c.Circles, 0f, 1f)));
            bus.Register<SpillCommand>(HandleSpill);
            bus.Register<TakeMortarCommand>(HandleTakeMortar);
            bus.Register<TakeFixingsCommand>(HandleTakeFixings);
        }

        static T Get<T>(int id) where T : Grabbable => World.Registry != null ? World.Registry.Get<T>(id) : null;

        bool HandleSpill(SpillCommand cmd)
        {
            Bucket b = Get<Bucket>(cmd.BucketId);
            if (b == null || b.Units == 0) return false;
            b.Empty();
            return true;
        }

        bool HandleTakeMortar(TakeMortarCommand cmd)
        {
            Bucket bucket = Get<Bucket>(cmd.BucketId);
            Trowel trowel = Get<Trowel>(cmd.TrowelId);
            if (bucket == null || trowel == null || trowel.HasFreshMortar) return false;
            float mixedAt = bucket.TakeLoad();
            if (mixedAt < 0f) return false;
            trowel.Load(mixedAt);
            return true;
        }

        bool HandleTakeFixings(TakeFixingsCommand cmd)
        {
            FixingBox box = Get<FixingBox>(cmd.BoxId);
            IGrabber player = World.Grabs != null ? World.Grabs.GetGrabber(cmd.PlayerId) : null;
            if (box == null || player == null || player.Inventory == null) return false;
            if (Vector3.Distance(player.Root.position, box.transform.position) > World.Settings.interactReach + 1f) return false;
            int n = box.Take(World.Settings.fixingsPerHandful);
            if (n <= 0) return false;
            player.Inventory.Add(box.Kind, n);
            return true;
        }
    }
}
