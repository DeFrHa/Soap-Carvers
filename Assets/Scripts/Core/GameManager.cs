using System;
using System.Collections.Generic;
using BuildCrew.Building;
using BuildCrew.Interaction;
using BuildCrew.Kits;
using BuildCrew.Parts;
using BuildCrew.Player;
using BuildCrew.Round;
using UnityEngine;

namespace BuildCrew.Core
{
    /// <summary>Built-in kits, selectable on the GameManager in the inspector.</summary>
    public enum KitChoice
    {
        GardenShed = 0,
        Cottage = 1,
    }

    /// <summary>
    /// Round flow: Briefing -> Dump -> Build -> FinalTest -> Results -> (R) Briefing.
    /// Holds no player-specific state. Round changes come in as Request* calls
    /// (and its own timers), so a network host can own this object and clients
    /// only send requests. Runs the round on every TeamArea in the scene.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] GameSettings settings;
        [Tooltip("Which kit to build.")]
        [SerializeField] KitChoice kit = KitChoice.GardenShed;
        [Tooltip("Optional: a JSON file name in StreamingAssets/Kits (community kits). Overrides the dropdown.")]
        [SerializeField] string customKitFile = "";
        [Tooltip("Pile seed. 0 = a new random pile every round.")]
        [SerializeField] int pileSeed;
        [SerializeField] List<TeamArea> areas = new List<TeamArea>();

        readonly List<ScoreResult> _teamResults = new List<ScoreResult>();
        int _round;
        int _roundSeed;
        float _stateStart;
        float _timeAtTestStart;
        readonly List<int> _fixedAtTestStart = new List<int>();

        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Briefing;
        public float TimeRemaining { get; private set; }
        public float StateTime => Time.time - _stateStart;
        public ScoreResult LastResult { get; private set; }
        public IReadOnlyList<ScoreResult> TeamResults => _teamResults;
        public GameSettings Settings => settings;
        public KitDefinition Kit { get; private set; }
        public string KitSource { get; private set; }
        public IReadOnlyList<TeamArea> Areas => areas;
        public float BriefingTimeLeft => Mathf.Max(0f, settings.briefingSeconds - StateTime);

        public string KitFileName =>
            !string.IsNullOrWhiteSpace(customKitFile) ? customKitFile.Trim()
            : kit == KitChoice.Cottage ? KitDesigns.CottageFile : KitDesigns.GardenShedFile;

        public void Configure(GameSettings gameSettings, KitChoice kitChoice, List<TeamArea> teamAreas)
        {
            settings = gameSettings;
            kit = kitChoice;
            areas = teamAreas;
        }

        public TeamArea AreaForTeam(int teamId)
        {
            foreach (TeamArea a in areas)
                if (a != null && a.TeamId == teamId) return a;
            return areas.Count > 0 ? areas[0] : null;
        }

        void Awake()
        {
            if (settings == null) settings = GameSettings.CreateDefault();
            if (areas.Count == 0) areas.AddRange(FindObjectsByType<TeamArea>(FindObjectsSortMode.None));
            Kit = KitLoader.Load(KitFileName, out string source);
            KitSource = source;
            Debug.Log($"[Build Crew] Kit '{Kit.name}' ({Kit.slots.Length} slots) from {source}");
        }

        void Start()
        {
            foreach (TeamArea a in areas)
                if (a != null && a.Site != null) a.Site.Setup(Kit);
            StartRound();
        }

        // ================================================================ loop

        void Update()
        {
            switch (State)
            {
                case GameState.Briefing:
                    if (StateTime >= settings.briefingSeconds) BeginDump();
                    break;

                case GameState.Dump:
                    if (PileReady()) BeginBuild();
                    break;

                case GameState.Build:
                    TimeRemaining -= Time.deltaTime;
                    if (TimeRemaining <= 0f)
                    {
                        TimeRemaining = 0f;
                        BeginFinalTest();
                    }
                    else if (AllSitesComplete()) BeginFinalTest();
                    break;

                case GameState.FinalTest:
                    bool done = true;
                    foreach (TeamArea a in areas)
                        if (a != null && a.FinalTest != null && !a.FinalTest.IsFinished) done = false;
                    if (done) FinishRound();
                    break;
            }
        }

        // ============================================================ requests

        /// <summary>Ring the bell: the team is done, start the final test now (time bonus).</summary>
        public void RequestFinishEarly()
        {
            if (State == GameState.Build) BeginFinalTest();
        }

        /// <summary>R on the results screen: new round, new pile.</summary>
        public void RequestRestart()
        {
            if (State != GameState.Results) return;
            StartRound();
        }

        // =========================================================== internals

        void StartRound()
        {
            _round++;
            _roundSeed = pileSeed != 0 ? pileSeed + _round - 1 : Environment.TickCount ^ (_round * 7919);

            if (World.Grabs != null) World.Grabs.ReleaseEverything();
            if (World.Parts != null) World.Parts.DespawnAll();
            if (World.Bus != null) World.Bus.ClearLog();

            // Scene items (tools, buckets, ladder, pallets...) back to where they started.
            if (World.Registry != null)
                foreach (Grabbable g in new List<Grabbable>(World.Registry.All))
                    if (g != null && !g.SpawnedAtRuntime) g.ResetToHome();

            foreach (TeamArea a in areas)
            {
                if (a == null) continue;
                if (a.Site != null) a.Site.ResetSite();
                if (a.FinalTest != null) a.FinalTest.ResetTest();
                if (a.Pile != null) a.Pile.Clear();
                SpawnStarterSupplies(a);
            }

            foreach (PlayerGrabber p in FindObjectsByType<PlayerGrabber>(FindObjectsSortMode.None))
            {
                if (p.Inventory != null)
                {
                    // Start with a pocketful so the hammer works right away.
                    p.Inventory.Clear();
                    p.Inventory.Add(FixMethod.Nails, settings.startNails);
                    p.Inventory.Add(FixMethod.Screws, settings.startScrews);
                }
                TeamArea a = AreaForTeam(p.TeamId);
                PlayerMotor motor = p.GetComponent<PlayerMotor>();
                if (a != null && a.PlayerSpawn != null && motor != null) motor.Teleport(a.PlayerSpawn.position, a.PlayerSpawn.rotation);
            }

            TimeRemaining = Kit.roundTime;
            LastResult = default;
            _teamResults.Clear();
            SetState(GameState.Briefing);
        }

        /// <summary>A box of nails and one of screws by the tool rack, so fixing can start before the pile is sorted.</summary>
        void SpawnStarterSupplies(TeamArea a)
        {
            PartManager parts = World.Parts;
            if (parts == null || a.SupplyPoint == null) return;
            int i = 0;
            foreach (string type in new[] { "nails", "screws" })
            {
                PartDefinition def = PartCatalog.Get(type);
                if (def == null) continue;
                Vector3 pos = a.SupplyPoint.position + a.SupplyPoint.right * (0.35f * i++);
                parts.Spawn(new PartSpec(type, def.DefaultSize(0f), PileRole.Fixings), pos, a.SupplyPoint.rotation);
            }
        }

        void BeginDump()
        {
            var options = new PileOptions
            {
                SparePercent = settings.sparePercent,
                WrongLengthDecoys = settings.wrongLengthDecoys,
                NailsPerFix = settings.nailHitsPerFix,
                ScrewsPerFix = settings.screwsPerFix,
                FixingsPerBox = settings.fixingsPerBox,
                LengthTolerance = settings.sizeTolerance,
            };
            foreach (TeamArea a in areas)
            {
                if (a == null || a.Pile == null) continue;
                // Same seed for every team: a fair race.
                List<PartSpec> pile = PileGenerator.Generate(Kit, _roundSeed, options);
                a.Pile.Drop(pile, World.Parts);
            }
            SetState(GameState.Dump);
        }

        /// <summary>The timer starts once the pile has mostly settled, or settleTimeout after it was dropped.</summary>
        bool PileReady()
        {
            float latest = -1f;
            foreach (TeamArea a in areas)
            {
                if (a == null || a.Pile == null) continue;
                latest = Mathf.Max(latest, a.Pile.DroppedAt);
            }
            if (latest < 0f) return true;
            if (Time.time - latest < 1f) return false; // let it start falling first
            if (Time.time - latest >= settings.settleTimeout) return true;
            PartManager parts = World.Parts;
            return parts == null || parts.PileSettledFraction(settings.settleSpeed) >= settings.settleFraction;
        }

        void BeginBuild()
        {
            TimeRemaining = Kit.roundTime;
            SetState(GameState.Build);
        }

        bool AllSitesComplete()
        {
            bool any = false;
            foreach (TeamArea a in areas)
            {
                if (a == null || a.Site == null) continue;
                any = true;
                if (!a.Site.IsComplete) return false;
            }
            return any;
        }

        void BeginFinalTest()
        {
            _timeAtTestStart = Mathf.Max(0f, TimeRemaining);
            _fixedAtTestStart.Clear();
            FinalTestKind kind = Kit.GetFinalTest();
            foreach (TeamArea a in areas)
            {
                _fixedAtTestStart.Add(a != null && a.Site != null ? a.Site.FixedCount : 0);
                if (a != null && a.FinalTest != null) a.FinalTest.Begin(kind, settings.finalTestSeconds);
            }
            SetState(GameState.FinalTest);
        }

        void FinishRound()
        {
            _teamResults.Clear();
            for (int i = 0; i < areas.Count; i++)
            {
                TeamArea a = areas[i];
                if (a == null || a.Site == null) continue;
                int lost = a.FinalTest != null ? a.FinalTest.LostParts : 0;
                int fixedCount = i < _fixedAtTestStart.Count ? _fixedAtTestStart[i] : a.Site.FixedCount;
                _teamResults.Add(ScoreCalculator.Compute(fixedCount, a.Site.TotalCount, lost, _timeAtTestStart, Kit.roundTime,
                    settings.timeBonusWeight));
                if (a.FinalTest != null) a.FinalTest.EndView();
            }
            LastResult = _teamResults.Count > 0 ? _teamResults[0] : default;
            SetState(GameState.Results);
        }

        void SetState(GameState next)
        {
            State = next;
            _stateStart = Time.time;
            StateChanged?.Invoke(next);
        }

        /// <summary>"m:ss" formatting shared by HUD and plan table.</summary>
        public static string FormatTime(float seconds)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
