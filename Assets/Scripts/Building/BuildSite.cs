using System;
using System.Collections.Generic;
using BuildCrew.Core;
using BuildCrew.Interaction;
using BuildCrew.Kits;
using BuildCrew.Parts;
using UnityEngine;

namespace BuildCrew.Building
{
    /// <summary>
    /// One team's building plot. Builds a BuildSlot per kit slot (ghost
    /// outline: current stage glows, later stages faint, a slot about to
    /// accept a held part lights up green), watches the team's held parts and
    /// sends SnapCommands when one is close and aligned enough, and notices
    /// parts that leave their slot (UnsnapCommand).
    /// </summary>
    public class BuildSite : MonoBehaviour
    {
        [SerializeField] int siteIndex;
        [SerializeField] int teamId;

        readonly List<BuildSlot> _slots = new List<BuildSlot>();
        readonly Dictionary<string, BuildSlot> _byId = new Dictionary<string, BuildSlot>();
        BuildSlot _highlight;
        float _ghostTimer;

        public int SiteIndex => siteIndex;
        public int TeamId => teamId;
        public KitDefinition Kit { get; private set; }
        public IReadOnlyList<BuildSlot> Slots => _slots;
        public int TotalCount => _slots.Count;
        /// <summary>Raised whenever a slot's state changes.</summary>
        public event Action Changed;

        public int FixedCount
        {
            get
            {
                int n = 0;
                foreach (BuildSlot s in _slots) if (s.State == SlotState.Fixed) n++;
                return n;
            }
        }

        public bool IsComplete => _slots.Count > 0 && FixedCount == _slots.Count;

        /// <summary>The lowest stage that still has unfixed slots (== stage count when done).</summary>
        public int CurrentStage
        {
            get
            {
                int stages = Kit != null ? Kit.StageCount() : 0;
                for (int st = 0; st < stages; st++)
                    foreach (BuildSlot s in _slots)
                        if (s.Stage == st && s.State != SlotState.Fixed) return st;
                return stages;
            }
        }

        public void Configure(int index, int team)
        {
            siteIndex = index;
            teamId = team;
        }

        void Awake()
        {
            BuildManager build = World.Build;
            if (build != null) build.RegisterSite(this);
        }

        public void StageProgress(int stage, out int fixedCount, out int total)
        {
            fixedCount = total = 0;
            foreach (BuildSlot s in _slots)
            {
                if (s.Stage != stage) continue;
                total++;
                if (s.State == SlotState.Fixed) fixedCount++;
            }
        }

        public BuildSlot Get(string id) => id != null && _byId.TryGetValue(id, out BuildSlot s) ? s : null;

        /// <summary>(Re)builds the slots for a kit. Any parts in old slots are released.</summary>
        public void Setup(KitDefinition kit)
        {
            foreach (BuildSlot s in _slots)
            {
                Part p = s.Clear();
                if (p != null) p.SetBuildState(PartState.Loose, null);
                Destroy(s.gameObject);
            }
            _slots.Clear();
            _byId.Clear();
            Kit = kit;
            if (kit?.slots == null) return;

            Material faint = Palette("ghostFuture");
            for (int i = 0; i < kit.slots.Length; i++)
            {
                KitSlot data = kit.slots[i];
                var go = new GameObject($"Slot {data.id}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = data.position;
                go.transform.localRotation = Quaternion.Euler(data.rotation);
                var slot = go.AddComponent<BuildSlot>();
                slot.Initialize(this, i, data, faint);
                _slots.Add(slot);
                _byId[data.id] = slot;
            }
            foreach (BuildSlot s in _slots)
            {
                var rests = new List<BuildSlot>();
                if (s.Data.restsOn != null)
                    foreach (string id in s.Data.restsOn)
                        if (_byId.TryGetValue(id, out BuildSlot r)) rests.Add(r);
                s.ResolveRestsOn(rests.ToArray());
            }
            RefreshGhosts();
            NotifyChanged();
        }

        /// <summary>Restart: empty every slot (the parts themselves are despawned by PartManager).</summary>
        public void ResetSite()
        {
            foreach (BuildSlot s in _slots)
            {
                Part p = s.Clear();
                if (p != null) p.SetBuildState(PartState.Loose, null);
            }
            RefreshGhosts();
            NotifyChanged();
        }

        public void NotifyChanged() => Changed?.Invoke();

        Material Palette(string key)
        {
            MaterialPalette p = World.Palette;
            return p != null ? p.Get(key) : MaterialPalette.Create(key);
        }

        void Update()
        {
            GameManager game = World.Game;
            GameState state = game != null ? game.State : GameState.Build;
            GameSettings s = World.Settings;

            _highlight = null;
            if (state == GameState.Build) DetectSnaps(s);
            if (state == GameState.Build || state == GameState.FinalTest) WatchSlots(s);

            _ghostTimer -= Time.deltaTime;
            if (_ghostTimer <= 0f || _highlight != null)
            {
                _ghostTimer = 0.15f;
                RefreshGhosts();
            }
        }

        /// <summary>Held parts of this team: snap into a matching active slot when close and aligned.</summary>
        void DetectSnaps(GameSettings s)
        {
            GrabManager grabs = World.Grabs;
            if (grabs == null) return;
            foreach (IGrabber g in grabs.Grabbers)
            {
                if (g.TeamId != teamId || !(g.Grabbed is Part part) || part.State != PartState.Loose) continue;
                if (part.PrimaryGrabber != g) continue; // one report per part
                BuildSlot best = null;
                float bestDist = float.MaxValue, bestAngle = 0f;
                Vector3 pos = part.transform.position;
                foreach (BuildSlot slot in _slots)
                {
                    if (!slot.IsActive) continue;
                    float d = Vector3.Distance(pos, slot.TargetPosition);
                    if (slot == part.SnapBlockedSlot)
                    {
                        if (d > s.snapDistance * 1.5f) part.SnapBlockedSlot = null;
                        else continue;
                    }
                    if (d > 1.5f || d >= bestDist) continue;
                    if (!slot.TryMatch(part, s, out _, out float angle)) continue;
                    best = slot;
                    bestDist = d;
                    bestAngle = angle;
                }
                if (best == null) continue;
                if (bestDist <= s.snapDistance && bestAngle <= s.snapAngle)
                {
                    CommandBus bus = World.Bus;
                    if (bus != null) bus.Execute(new SnapCommand { PlayerId = g.PlayerId, SiteIndex = siteIndex, SlotId = best.Id, PartId = part.EntityId });
                }
                else _highlight = best;
            }
        }

        /// <summary>Parts that left their slot: pulled out, blown away, shattered, or all fixed joints broke.</summary>
        void WatchSlots(GameSettings s)
        {
            CommandBus bus = World.Bus;
            if (bus == null) return;
            foreach (BuildSlot slot in _slots)
            {
                if (slot.State == SlotState.Empty) continue;
                Part p = slot.Part;
                bool gone;
                if (p == null) gone = true;
                else if (slot.State == SlotState.Fixed) gone = p.IntactFixJoints == 0;
                else
                {
                    // Grabbing a placed (not yet fixed) part takes it out of its slot.
                    gone = p.IsGrabbed || Vector3.Distance(p.transform.position, slot.TargetPosition) > s.unsnapDistance;
                }
                if (gone) bus.Execute(new UnsnapCommand { SiteIndex = siteIndex, SlotId = slot.Id });
            }
        }

        public void RefreshGhosts()
        {
            GameManager game = World.Game;
            bool hidden = game != null && (game.State == GameState.FinalTest || game.State == GameState.Results);
            int current = CurrentStage;
            Material faint = Palette("ghostFuture"), stage = Palette("ghostStage"), active = Palette("ghostActive"), target = Palette("ghostTarget");
            foreach (BuildSlot slot in _slots)
            {
                Material m;
                if (hidden || slot.State != SlotState.Empty) m = null;
                else if (slot == _highlight) m = target;
                else if (slot.IsActive) m = slot.Stage <= current ? active : stage;
                else m = slot.Stage == current ? stage : faint;
                slot.SetGhostMaterial(m);
            }
        }

        // ============================================================ queries

        /// <summary>The slot (if any) whose part is this one.</summary>
        public BuildSlot SlotOf(Part part)
        {
            foreach (BuildSlot s in _slots)
                if (s.Part == part) return s;
            return null;
        }
    }
}
