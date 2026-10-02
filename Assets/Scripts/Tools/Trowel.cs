using BuildCrew.Building;
using BuildCrew.Kits;
using BuildCrew.Parts;
using UnityEngine;

namespace BuildCrew.Tools
{
    /// <summary>
    /// LMB on a bucket of fresh mortar: scoop a load (good for a few slots).
    /// LMB on a placed stone or brick stack: mortar it in place (fixes it).
    /// The mortar on the trowel sets at the same time as the batch it came from.
    /// </summary>
    public class Trowel : Tool
    {
        [SerializeField] GameObject mortarBlob;
        [SerializeField] int applicationsPerLoad = 3;
        const float Reach = 2.6f;

        public int Applications { get; private set; }
        public float MortarMixedAt { get; private set; } = -1f;

        public bool HasFreshMortar => Applications > 0 && MortarMixedAt >= 0f &&
                                      Time.time - MortarMixedAt < Settings.mortarFreshSeconds;

        public override string HeldHint => "LMB on fresh mortar: Scoop   LMB on a placed stone/brick: Mortar it   E: Drop";

        public override string StatusText
        {
            get
            {
                if (HasFreshMortar)
                    return $"Trowel: {Applications} dab(s) of mortar, sets in {Settings.mortarFreshSeconds - (Time.time - MortarMixedAt):0} s";
                return Applications > 0 ? "The mortar on your trowel has SET. Scoop fresh mortar." : "Trowel empty: scoop fresh mortar from a bucket";
            }
        }

        public void SetBlob(GameObject blob) => mortarBlob = blob;

        public override void Use()
        {
            if (!AimRaycast(Reach, out RaycastHit hit) || hit.rigidbody == null) return;
            Part part = hit.rigidbody.GetComponent<Part>();
            BuildSlot slot = part != null ? part.Slot : null;
            if (slot != null && slot.State == SlotState.Snapped && slot.Method == FixMethod.Mortar)
            {
                if (HasFreshMortar)
                    Send(new FixCommand { SiteIndex = slot.Site.SiteIndex, SlotId = slot.Id, Method = FixMethod.Mortar, ToolId = EntityId });
                return;
            }
            Bucket bucket = hit.rigidbody.GetComponent<Bucket>();
            if (bucket != null) Send(new TakeMortarCommand { BucketId = bucket.EntityId, TrowelId = EntityId });
        }

        // ================================================ command handlers only

        public void Load(float mixedAt)
        {
            Applications = applicationsPerLoad;
            MortarMixedAt = mixedAt;
        }

        public bool ConsumeApplication()
        {
            if (!HasFreshMortar) return false;
            Applications--;
            return true;
        }

        void Update()
        {
            if (mortarBlob != null && mortarBlob.activeSelf != Applications > 0) mortarBlob.SetActive(Applications > 0);
        }

        public override void ResetToHome()
        {
            Applications = 0;
            MortarMixedAt = -1f;
            base.ResetToHome();
        }
    }
}
