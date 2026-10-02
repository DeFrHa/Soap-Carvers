using BuildCrew.Core;

namespace BuildCrew.Tools
{
    public enum Ingredient : byte
    {
        Cement = 0,
        Sand = 1,
        Water = 2,
    }

    /// <summary>Add a scoop of cement/sand or a splash of water to a bucket.</summary>
    [System.Serializable]
    public class AddIngredientCommand : GameCommand
    {
        public int BucketId;
        public Ingredient Ingredient;
        public int Amount = 1;
    }

    /// <summary>Stir a bucket with the shovel; Circles = how many mouse circles this batch of stirring was.</summary>
    [System.Serializable]
    public class MixCommand : GameCommand
    {
        public int BucketId;
        public float Circles;
    }

    /// <summary>Scoop one load of fresh mortar from a bucket onto a trowel.</summary>
    [System.Serializable]
    public class TakeMortarCommand : GameCommand
    {
        public int BucketId;
        public int TrowelId;
    }

    /// <summary>A bucket was tipped over: its contents are gone.</summary>
    [System.Serializable]
    public class SpillCommand : GameCommand
    {
        public int BucketId;
    }
}
