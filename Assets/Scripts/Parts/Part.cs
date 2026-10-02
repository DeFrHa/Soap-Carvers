using System.Collections.Generic;
using BuildCrew.Building;
using BuildCrew.Core;
using BuildCrew.Interaction;
using BuildCrew.Kits;
using UnityEngine;

namespace BuildCrew.Parts
{
    public enum PartState
    {
        /// <summary>Lying around (pile, pallet, in someone's hands).</summary>
        Loose = 0,
        /// <summary>In a slot, held by a soft spring joint. Outlined yellow.</summary>
        Snapped = 1,
        /// <summary>Nailed/screwed/mortared: fixed joints to what it rests on.</summary>
        Fixed = 2,
    }

    /// <summary>
    /// A building part: a box-ish rigidbody of a PartDefinition type at a size.
    /// Created by PartManager (pile, cut pieces). Its build state is set only
    /// by BuildManager command handlers.
    /// </summary>
    public class Part : Grabbable
    {
        [SerializeField] string typeId;
        [SerializeField] Vector3 size;
        [SerializeField] PileRole role;
        [SerializeField] GameObject outline;

        readonly List<Joint> _fixJoints = new List<Joint>();

        public PartDefinition Definition { get; private set; }
        public string TypeId => typeId;
        public Vector3 Size => size;
        public PileRole Role => role;
        public PartState State { get; private set; }
        public BuildSlot Slot { get; private set; }
        public float SpawnTime { get; private set; }
        public bool InBuilding => State != PartState.Loose;

        /// <summary>
        /// The slot this part was just taken out of: it won't snap back into it
        /// until it has been moved away once (otherwise grabbing a placed part
        /// would unsnap and re-snap it every frame). UX state only.
        /// </summary>
        public BuildSlot SnapBlockedSlot { get; set; }

        public override string DisplayName => Definition != null ? Definition.DisplayName : base.DisplayName;
        public string SizeLabel => Definition != null ? Definition.FormatSize(size) : string.Empty;
        public string GroupLabel => Definition != null ? Definition.GroupLabel(size) : DisplayName;

        public override string LookInfo
        {
            get
            {
                string state = State == PartState.Fixed ? "  [fixed]" : State == PartState.Snapped ? $"  [placed - {FixHint}]" : string.Empty;
                string size = Definition != null && Definition.IsJunk ? string.Empty : " " + SizeLabel;
                return $"{DisplayName}{size}  ({WeightClasses.Label(Weight)}, {Body.mass:0.#} kg){state}";
            }
        }

        /// <summary>How to fix this placed part, with progress.</summary>
        string FixHint
        {
            get
            {
                if (Slot == null) return "needs fixing";
                string progress = $"{Slot.FixProgress}/{Slot.FixNeeded(World.Settings)}";
                switch (Slot.Method)
                {
                    case FixMethod.Nails: return $"hit it with the HAMMER to nail it {progress}";
                    case FixMethod.Screws: return $"hold LMB with the SCREWDRIVER {progress}";
                    default: return "TROWEL fresh mortar onto it";
                }
            }
        }

        /// <summary>While carrying: where does this go?</summary>
        public override string HeldHint
        {
            get
            {
                const string keys = "E: Release   G: Throw   Scroll: Distance   R+Mouse: Rotate";
                if (Definition != null && Definition.IsJunk) return "This isn't part of any building...   " + keys;
                int free = 0, ready = 0;
                BuildManager build = World.Build;
                if (build != null)
                {
                    GameSettings s = World.Settings;
                    foreach (BuildSite site in build.Sites)
                    foreach (BuildSlot slot in site.Slots)
                    {
                        if (slot.State != SlotState.Empty || !slot.TryMatch(this, s, out _, out _)) continue;
                        free++;
                        if (slot.RestsSatisfied) ready++;
                    }
                }
                if (ready > 0) return "Bring it to a GREEN ghost to place it   " + keys;
                if (free > 0) return "Its spot isn't ready yet: fix the parts it rests on first   " + keys;
                return (Definition != null && Definition.CutTool != CutTool.None
                    ? "No slot this size: cut it to a needed length   "
                    : "Not needed (any more)   ") + keys;
            }
        }

        /// <summary>Fixed parts belong to the building; break them loose physically if you must.</summary>
        public override bool CanBeGrabbed => State != PartState.Fixed;

        /// <summary>Called by the factory before the object is activated.</summary>
        public void Initialize(PartDefinition definition, Vector3 partSize, PileRole pileRole, GameObject outlineObject)
        {
            typeId = definition.Id;
            size = partSize;
            role = pileRole;
            outline = outlineObject;
            Definition = definition;
        }

        protected override void Awake()
        {
            base.Awake();
            if (Definition == null) Definition = PartCatalog.Get(typeId);
            SpawnTime = Time.time;
            Body.solverIterations = World.Settings.partSolverIterations;
            if (outline != null) outline.SetActive(false);
            PartManager parts = World.Parts;
            if (parts != null) parts.Register(this);
        }

        protected override void OnDestroy()
        {
            PartManager parts = World.Parts;
            if (parts != null) parts.Unregister(this);
            base.OnDestroy();
        }

        /// <summary>BuildManager only.</summary>
        public void SetBuildState(PartState state, BuildSlot slot)
        {
            State = state;
            Slot = state == PartState.Loose ? null : slot;
            if (outline != null) outline.SetActive(state == PartState.Snapped);
            GameSettings s = World.Settings;
            Body.solverIterations = state == PartState.Loose ? s.partSolverIterations : s.buildingSolverIterations;
            if (state == PartState.Loose) ClearFixJoints();
        }

        public void AddFixJoint(Joint joint) => _fixJoints.Add(joint);

        /// <summary>Fixed joints that haven't broken yet.</summary>
        public int IntactFixJoints
        {
            get
            {
                _fixJoints.RemoveAll(j => j == null);
                return _fixJoints.Count;
            }
        }

        public void ClearFixJoints()
        {
            foreach (Joint j in _fixJoints)
                if (j != null) Destroy(j);
            _fixJoints.Clear();
        }

        /// <summary>Destroys our fixed joints that connect to <paramref name="other"/> (it is leaving the building).</summary>
        public void DetachJointsTo(Rigidbody other)
        {
            for (int i = _fixJoints.Count - 1; i >= 0; i--)
            {
                Joint j = _fixJoints[i];
                if (j == null || j.connectedBody != other) continue;
                Destroy(j);
                _fixJoints.RemoveAt(i);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (Definition == null || !Definition.Fragile) return;
            PartManager parts = World.Parts;
            if (parts != null) parts.ReportImpact(this, collision);
        }
    }
}
