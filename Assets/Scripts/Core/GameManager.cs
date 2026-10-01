using System;
using SoapCarvers.Player;
using SoapCarvers.Soap;
using SoapCarvers.Targets;
using SoapCarvers.Tools;
using UnityEngine;

namespace SoapCarvers.Core
{
    /// <summary>
    /// Round flow: Ready -> Carving -> Scanning -> Results -> (restart) Ready.
    /// Holds no player-specific state. All state changes come in as Request*
    /// calls (or carve events), so a network host could own this object and
    /// clients would just send requests.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] GameSettings settings;
        [SerializeField] SoapBlock soap;
        [SerializeField] TargetManager targets;
        [SerializeField] ScanEffect scan;
        [SerializeField] ItemManager items;
        [SerializeField] Transform playerSpawn;

        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Ready;
        public float TimeRemaining { get; private set; }
        public ScoreResult LastResult { get; private set; }
        public GameSettings Settings => settings;
        public SoapBlock Soap => soap;
        public TargetManager Targets => targets;
        public Transform PlayerSpawn => playerSpawn;

        /// <summary>Tools work in the lobby (the first carve starts the clock) and while carving.</summary>
        public bool ToolsEnabled => State == GameState.Ready || State == GameState.Carving;

        public void Configure(GameSettings gameSettings, SoapBlock soapBlock, TargetManager targetManager,
            ScanEffect scanEffect, ItemManager itemManager, Transform spawn)
        {
            settings = gameSettings;
            soap = soapBlock;
            targets = targetManager;
            scan = scanEffect;
            items = itemManager;
            playerSpawn = spawn;
        }

        void Awake()
        {
            if (settings == null) settings = GameSettings.CreateDefault();
            if (soap == null) soap = FindFirstObjectByType<SoapBlock>();
            if (targets == null) targets = FindFirstObjectByType<TargetManager>();
            if (scan == null) scan = FindFirstObjectByType<ScanEffect>();
            if (items == null) items = FindFirstObjectByType<ItemManager>();
            TimeRemaining = settings.roundSeconds;
        }

        void OnEnable()
        {
            if (soap == null) return;
            soap.CarveFilter = _ => ToolsEnabled;
            soap.CarveApplied += OnCarveApplied;
        }

        void OnDisable()
        {
            if (soap == null) return;
            soap.CarveFilter = null;
            soap.CarveApplied -= OnCarveApplied;
        }

        void Start()
        {
            // Voxelize the target up front so the first tablet view / scan doesn't hitch.
            if (targets != null) _ = targets.Grid;
        }

        void OnCarveApplied(SoapCarveCommand cmd, int removedPoints)
        {
            if (State == GameState.Ready) RequestStartRound();
        }

        void Update()
        {
            if (State != GameState.Carving) return;
            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                BeginScan();
            }
        }

        // ------------------------------------------------------------ requests

        public void RequestStartRound()
        {
            if (State != GameState.Ready) return;
            TimeRemaining = settings.roundSeconds;
            SetState(GameState.Carving);
        }

        /// <summary>Ends the round now (workbench button while the clock runs).</summary>
        public void RequestFinishEarly()
        {
            if (State != GameState.Carving) return;
            TimeRemaining = 0f;
            BeginScan();
        }

        public void RequestRestart()
        {
            if (State == GameState.Scanning) return;
            if (scan != null) scan.Stop();
            if (soap != null) soap.ResetSoap();
            if (items != null) items.ResetAll();
            foreach (var scaffold in FindObjectsByType<Scaffold>(FindObjectsSortMode.None))
                scaffold.ResetToHome();
            if (playerSpawn != null)
            {
                foreach (var motor in FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
                    motor.Teleport(playerSpawn.position, playerSpawn.rotation);
            }
            TimeRemaining = settings.roundSeconds;
            LastResult = default;
            SetState(GameState.Ready);
        }

        // ------------------------------------------------------------ internals

        void BeginScan()
        {
            SetState(GameState.Scanning);
            if (scan != null) scan.Play(settings.scanSeconds, FinishScan);
            else FinishScan();
        }

        void FinishScan()
        {
            if (soap != null && targets != null && targets.Grid != null)
                LastResult = ScoreCalculator.Compute(soap, targets.Grid);
            SetState(GameState.Results);
        }

        void SetState(GameState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }

        /// <summary>"m:ss" formatting shared by HUD and tablet.</summary>
        public static string FormatTime(float seconds)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
