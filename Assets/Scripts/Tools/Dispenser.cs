using BuildCrew.Core;
using BuildCrew.Interaction;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>
    /// Part of the mortar station: the cement bag, the sand heap, the water tap.
    /// E adds one unit to the bucket you hold, or to the nearest bucket standing
    /// within reach of the dispenser.
    /// </summary>
    public class Dispenser : MonoBehaviour, IInteractable
    {
        [SerializeField] Ingredient ingredient;
        [SerializeField] float bucketRadius = 1.6f;

        public void Configure(Ingredient kind) => ingredient = kind;

        string Noun => ingredient == Ingredient.Water ? "water" : ingredient == Ingredient.Sand ? "a scoop of sand" : "a scoop of cement";

        Bucket TargetBucket(IGrabber who)
        {
            if (who.Grabbed is Bucket held) return held;
            Bucket best = null;
            float bestD = bucketRadius;
            foreach (Collider c in Physics.OverlapSphere(transform.position, bucketRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Bucket b = c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<Bucket>() : null;
                if (b == null) continue;
                float d = Vector3.Distance(transform.position, b.transform.position);
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        public string GetPrompt(IGrabber who)
        {
            Bucket b = TargetBucket(who);
            if (b == null) return $"{Name}: bring a bucket (hold it or put it next to me)";
            return b.Units >= World.Settings.bucketCapacity ? "The bucket is full" : $"E: Add {Noun} to the bucket";
        }

        string Name => ingredient == Ingredient.Water ? "Water tap" : ingredient == Ingredient.Sand ? "Sand" : "Cement";

        public void Interact(IGrabber who)
        {
            Bucket b = TargetBucket(who);
            CommandBus bus = World.Bus;
            if (b == null || bus == null) return;
            bus.Execute(new AddIngredientCommand { PlayerId = who.PlayerId, BucketId = b.EntityId, Ingredient = ingredient, Amount = 1 });
        }
    }
}
