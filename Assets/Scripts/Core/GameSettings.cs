using UnityEngine;

namespace BuildCrew.Core
{
    /// <summary>
    /// All tunable numbers in one place. The scene menu creates an asset at
    /// Assets/Settings/GameSettings.asset (kept across rebuilds); components
    /// fall back to <see cref="CreateDefault"/> if none is assigned.
    /// Kit-specific values (round time, final test) live in the kit JSON.
    /// </summary>
    [CreateAssetMenu(menuName = "Build Crew/Game Settings", fileName = "GameSettings")]
    public class GameSettings : ScriptableObject
    {
        [Header("Round flow")]
        public float briefingSeconds = 10f;
        public float finalTestSeconds = 10f;
        [Tooltip("Max seconds to wait for the pile to settle after the bed has tipped.")]
        public float settleTimeout = 5f;
        [Tooltip("Fraction of pile parts that must be (nearly) at rest for the timer to start.")]
        [Range(0f, 1f)] public float settleFraction = 0.85f;
        public float settleSpeed = 0.25f;
        [Tooltip("Score bonus weight for time left on the clock (scaled by completion).")]
        public float timeBonusWeight = 0.2f;

        [Header("Player")]
        public float mouseSensitivity = 0.12f;
        public float interactReach = 3.2f;
        [Tooltip("Degrees of held-object rotation per pixel of mouse movement while R is held.")]
        public float rotateSensitivity = 0.35f;

        [Header("Physics grab")]
        [Tooltip("Max force one player's grab spring can apply (N). Weight above this can't be lifted alone.")]
        public float grabStrength = 420f;
        [Tooltip("Natural frequency of the grab spring (rad/s). Spring = mass * f^2, capped by strength.")]
        public float grabFrequency = 14f;
        [Range(0f, 2f)] public float grabDampingRatio = 0.75f;
        public float grabAngularFrequency = 9f;
        [Tooltip("Max torque one player can apply to keep a held object's orientation (N m). Long heavy things sag.")]
        public float grabMaxTorque = 110f;
        public float holdDistanceMin = 0.8f;
        public float holdDistanceMax = 3.2f;
        public float holdScrollStep = 0.2f;
        [Tooltip("Hand anchor max speed (m/s); smooths the first yank when grabbing.")]
        public float handMaxSpeed = 10f;
        [Tooltip("Release automatically if the grabbed point is this far from the hand for a moment.")]
        public float leashDistance = 3.5f;
        public int maxGrabbersPerObject = 4;
        [Tooltip("Throw impulse (N s), capped so light things don't go supersonic.")]
        public float throwImpulse = 32f;
        public float maxThrowSpeed = 14f;
        [Tooltip("Mass (kg) up to which an object counts as light.")]
        public float lightMaxMass = 10f;
        [Tooltip("Mass (kg) up to which one player can lift it (medium). Above = heavy (drag or team lift).")]
        public float mediumMaxMass = 40f;
        [Tooltip("Walk speed multiplier when dragging a heavy object alone.")]
        [Range(0.1f, 1f)] public float heavyCarrySpeed = 0.4f;

        [Header("Snapping and fixing")]
        public float snapDistance = 0.3f;
        public float snapAngle = 20f;
        [Tooltip("Size tolerance when matching a part to a slot (m). Brief: +-5 cm.")]
        public float sizeTolerance = 0.05f;
        [Tooltip("Soft snap spring frequency (rad/s).")]
        public float snapFrequency = 7f;
        [Tooltip("Soft snap spring max force as a multiple of the part's weight (plus 150 N).")]
        public float snapForceWeights = 2.5f;
        [Tooltip("A snapped part pulled further than this from its slot comes loose.")]
        public float unsnapDistance = 0.6f;
        public int nailHitsPerFix = 3;
        public int screwsPerFix = 3;
        public float screwSeconds = 0.6f;
        public int fixingsPerHandful = 20;
        public int fixingsPerBox = 50;
        [Tooltip("Fixed joint strength (N / N m) per fixing method.")]
        public float nailBreakForce = 6000f;
        public float nailBreakTorque = 3000f;
        public float screwBreakForce = 8000f;
        public float screwBreakTorque = 4000f;
        public float mortarBreakForce = 12000f;
        public float mortarBreakTorque = 6000f;
        public int partSolverIterations = 10;
        public int buildingSolverIterations = 24;

        [Header("Pile")]
        [Range(0f, 1f)] public float sparePercent = 0.15f;
        [Tooltip("Wrong-length decoys (a toilet and a gnome are always added).")]
        public int wrongLengthDecoys = 3;

        [Header("Saw")]
        [Tooltip("Mouse travel (pixels) one direction must cover to count as a stroke.")]
        public float sawStrokePixels = 45f;
        public int plankStrokes = 6;
        public int postStrokes = 10;
        public int beamStrokes = 14;
        [Tooltip("Two-person saw speed when sawing alone (single player).")]
        [Range(0.1f, 1f)] public float soloTwoPersonSawSpeed = 0.5f;
        [Tooltip("Cut position snaps to a needed length within this distance (m).")]
        public float cutSnapDistance = 0.04f;
        public float minPieceLength = 0.05f;

        [Header("Glass")]
        [Tooltip("Mouse travel (pixels) to score a pane.")]
        public float glassScorePixels = 900f;
        [Tooltip("Scoring faster than this (pixels/s) risks cracking the pane.")]
        public float glassCrackSpeed = 2600f;
        [Tooltip("Crack chance per second while rushing.")]
        public float glassCrackChance = 1.5f;
        [Tooltip("Impact speed (m/s) above which panes shatter.")]
        public float glassBreakSpeed = 4.5f;
        [Tooltip("...and the velocity change of the pane must exceed this (m/s), so light taps don't count.")]
        public float glassBreakDeltaV = 1.2f;

        [Header("Mortar")]
        public float mortarFreshSeconds = 60f;
        [Tooltip("Shovel circles needed to mix a bucket.")]
        public float mixCircles = 4f;
        [Tooltip("Water / (cement + sand). Above max = too wet (useless), below min = too dry to mix.")]
        public float waterRatioMin = 0.18f;
        public float waterRatioMax = 0.34f;
        public float sandRatioMin = 1.5f;
        public float sandRatioMax = 5f;
        public int loadsPerCement = 3;
        public int bucketCapacity = 12;

        [Header("Ladder")]
        public float ladderLength = 6f;
        public float ladderLean = 16f;
        [Tooltip("Chance per second that an unattended ladder slips while climbed.")]
        public float ladderSlipChance = 0.03f;

        [Header("Final test")]
        public float windSpeed = 13f;
        public float windGust = 6f;
        public float rainWindMultiplier = 1.25f;
        [Tooltip("Rain load on upward-facing surfaces (N/m^2).")]
        public float rainLoad = 40f;
        [Tooltip("A building part that moved more than this (m) or rotated more than lostAngle is lost.")]
        public float lostDistance = 0.5f;
        public float lostAngle = 30f;

        [Header("Debris")]
        public int maxShards = 80;
        public float shardLifetime = 8f;

        public static GameSettings CreateDefault()
        {
            var s = CreateInstance<GameSettings>();
            s.name = "GameSettings (runtime default)";
            return s;
        }
    }
}
