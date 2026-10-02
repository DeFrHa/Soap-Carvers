using BuildCrew.Core;
using BuildCrew.Interaction;
using UnityEngine;

namespace BuildCrew.Tools
{
    public enum BucketState
    {
        Empty = 0,
        /// <summary>Ingredients in, not stirred enough yet.</summary>
        Unmixed = 1,
        /// <summary>Not enough water: it won't mix. Add water.</summary>
        TooDry = 2,
        /// <summary>Too much water: soup. Useless; tip it out.</summary>
        TooWet = 3,
        /// <summary>Wrong sand:cement ratio. Useless; tip it out.</summary>
        BadMix = 4,
        /// <summary>Good mortar, usable until it sets.</summary>
        Fresh = 5,
        /// <summary>Set solid. Tip it out.</summary>
        Set = 6,
    }

    /// <summary>
    /// A mortar bucket: a physical, grabbable rigidbody whose mass grows with
    /// its contents. Fill it at the mortar station (cement + sand + water),
    /// stir it with the shovel, scoop it with the trowel. Mortar is fresh for
    /// mortarFreshSeconds after mixing, then sets. Turn the bucket upside down
    /// to tip it out. Contents change only through WorkshopManager commands.
    /// </summary>
    public class Bucket : Grabbable
    {
        [SerializeField] Transform contents;
        [SerializeField] MeshRenderer contentsRenderer;
        [SerializeField] float emptyMass = 1.5f;
        [SerializeField] float massPerUnit = 1.5f;
        [SerializeField] float innerHeight = 0.3f;

        float _upsideDown;

        public int Cement { get; private set; }
        public int Sand { get; private set; }
        public int Water { get; private set; }
        public float Mix { get; private set; }
        public float MixedAt { get; private set; } = -1f;
        public int Loads { get; private set; }
        public int Units => Cement + Sand + Water;
        public bool Ruined { get; private set; }

        public float WaterRatio => Cement + Sand > 0 ? Water / (float)(Cement + Sand) : 0f;
        public float SandRatio => Cement > 0 ? Sand / (float)Cement : 0f;
        public float FreshSecondsLeft => MixedAt < 0f ? 0f : Mathf.Max(0f, World.Settings.mortarFreshSeconds - (Time.time - MixedAt));

        public void SetParts(Transform contentsTransform, MeshRenderer renderer, float heightInside)
        {
            contents = contentsTransform;
            contentsRenderer = renderer;
            innerHeight = heightInside;
        }

        public BucketState State
        {
            get
            {
                GameSettings s = World.Settings;
                if (Units == 0) return BucketState.Empty;
                if (MixedAt >= 0f) return FreshSecondsLeft > 0f ? BucketState.Fresh : BucketState.Set;
                if (Ruined) return WaterRatio > s.waterRatioMax ? BucketState.TooWet : BucketState.BadMix;
                if (Cement > 0 && WaterRatio < s.waterRatioMin) return BucketState.TooDry;
                return BucketState.Unmixed;
            }
        }

        public override string LookInfo
        {
            get
            {
                string recipe = $"{Cement} cement, {Sand} sand, {Water} water";
                switch (State)
                {
                    case BucketState.Empty: return "Bucket (empty)  -  recipe: 1 cement : 3 sand : 1 water";
                    case BucketState.Unmixed: return $"Bucket: {recipe}  -  stir with the shovel ({Mix * 100f:0}%)";
                    case BucketState.TooDry: return $"Bucket: {recipe}  -  too dry to mix, add water";
                    case BucketState.TooWet: return $"Bucket: {recipe}  -  TOO WET, useless. Tip it out";
                    case BucketState.BadMix: return $"Bucket: {recipe}  -  wrong mix, useless. Tip it out";
                    case BucketState.Fresh: return $"Bucket: FRESH MORTAR, {Loads} trowels, sets in {FreshSecondsLeft:0} s";
                    default: return "Bucket: mortar has SET. Tip it out";
                }
            }
        }

        public override string HeldHint => "E on cement/sand/tap: fill   Turn upside down: tip out   E: Release";

        // ================================================= WorkshopManager only

        public bool Add(Ingredient ingredient, int amount)
        {
            GameSettings s = World.Settings;
            if (Units + amount > s.bucketCapacity || State == BucketState.Set) return false;
            switch (ingredient)
            {
                case Ingredient.Cement: Cement += amount; break;
                case Ingredient.Sand: Sand += amount; break;
                default: Water += amount; break;
            }
            // Fresh ingredients un-mix the batch.
            Mix = 0f;
            MixedAt = -1f;
            Loads = 0;
            Ruined = false;
            Refresh();
            return true;
        }

        public bool Stir(float circles)
        {
            GameSettings s = World.Settings;
            BucketState st = State;
            if (st == BucketState.Empty || st == BucketState.Fresh || st == BucketState.Set || Ruined) return false;
            float add = circles / Mathf.Max(0.1f, s.mixCircles);
            // Too dry: it clumps and never comes together.
            Mix = st == BucketState.TooDry ? Mathf.Min(0.6f, Mix + add) : Mix + add;
            if (Mix >= 1f)
            {
                Mix = 1f;
                bool wet = WaterRatio > s.waterRatioMax;
                bool badSand = Cement == 0 || SandRatio < s.sandRatioMin || SandRatio > s.sandRatioMax;
                if (wet || badSand) Ruined = true;
                else
                {
                    MixedAt = Time.time;
                    Loads = Cement * s.loadsPerCement;
                }
            }
            Refresh();
            return true;
        }

        /// <summary>Returns the mix time of the scooped load, or -1 if there's no fresh mortar.</summary>
        public float TakeLoad()
        {
            if (State != BucketState.Fresh || Loads <= 0) return -1f;
            float mixedAt = MixedAt;
            Loads--;
            if (Loads == 0) Empty();
            else Refresh();
            return mixedAt;
        }

        public void Empty()
        {
            Cement = Sand = Water = 0;
            Mix = 0f;
            MixedAt = -1f;
            Loads = 0;
            Ruined = false;
            Refresh();
        }

        // ============================================================ visuals

        void Refresh()
        {
            Body.mass = emptyMass + Units * massPerUnit;
            if (contents == null) return;
            float fill = Mathf.Clamp01(Units / (float)Mathf.Max(1, World.Settings.bucketCapacity));
            contents.gameObject.SetActive(Units > 0);
            Vector3 sc = contents.localScale;
            float h = Mathf.Max(0.01f, fill * innerHeight);
            contents.localScale = new Vector3(sc.x, h * 0.5f, sc.z);
            contents.localPosition = new Vector3(0f, 0.02f + h * 0.5f, 0f);
            UpdateColor();
        }

        void UpdateColor()
        {
            if (contentsRenderer == null) return;
            string key;
            switch (State)
            {
                case BucketState.Fresh: key = "mortarFresh"; break;
                case BucketState.Set: key = "mortarSet"; break;
                case BucketState.TooWet: key = "mortarWet"; break;
                case BucketState.TooDry: key = "mortarDry"; break;
                default: key = Water > Cement + Sand ? "water" : Sand >= Cement ? "sand" : "cement"; break;
            }
            MaterialPalette p = World.Palette;
            Material m = p != null ? p.Get(key) : null;
            if (m != null && contentsRenderer.sharedMaterial != m) contentsRenderer.sharedMaterial = m;
        }

        void Update()
        {
            if (MixedAt >= 0f) UpdateColor(); // fresh -> set over time

            // Upside down for a moment with something inside: it pours out.
            if (Units > 0 && transform.up.y < -0.2f) _upsideDown += Time.deltaTime;
            else _upsideDown = 0f;
            if (_upsideDown > 0.4f)
            {
                _upsideDown = 0f;
                CommandBus bus = World.Bus;
                if (bus != null) bus.Execute(new SpillCommand { BucketId = EntityId });
            }
        }

        public override void ResetToHome()
        {
            Empty();
            base.ResetToHome();
        }
    }
}
