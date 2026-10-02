# Build Crew – Setup

## 1. Open the project

The repo is a complete Unity project (`Assets/`, `Packages/`, `ProjectSettings/`).

1. **Unity Hub → Add → Add project from disk**, pick this repo folder, open
   it with **6000.3.6f1**.
2. Check **Window → Package Manager → In Project**: **Input System**,
   **Universal RP** and **Unity UI** (`com.unity.ugui`) are installed.
3. **Edit → Project Settings → Player → Other Settings → Active Input
   Handling** must be **Input System Package (New)** or **Both**.

The old Soap Carvers scene/materials were removed. If an old
`Assets/Settings/GameSettings.asset` is still around in your local copy,
delete it so the new defaults are used.

## 2. Build and play

1. **Build Crew → Create Playable Scene**. It:
   - adds a `Hologram` layer (index 31 if free),
   - writes `Assets/StreamingAssets/Kits/garden_shed.json` and `cottage.json`
     if they are missing (they are also committed),
   - generates `Assets/Materials/*.mat` and `Assets/Settings/GameSettings.asset`,
   - builds and saves `Assets/Scenes/BuildCrew.unity` and adds it to Build Settings.
2. Pick the kit: select **GameManager** in the hierarchy → **Kit** dropdown
   (**Garden Shed** default, or **Cottage**). For a community kit, put its JSON in
   `StreamingAssets/Kits` and type the file name into **Custom Kit File**.
3. Press **Play**. Click the Game view to capture the mouse.

Other menu items:
- **Build Crew → Generate Kit JSONs** rewrites the two built-in kit files
  from `KitDesigns.cs` (do this after changing the designs).
- **Build Crew → Open Playable Scene**.

Runtime-only alternative: in any empty scene, add an empty GameObject with
**GameBootstrap** and press Play. It builds everything in code.

## 3. Controls

| Input | Action |
|---|---|
| WASD / arrows | Move (slower while carrying medium/heavy things) |
| Mouse | Look |
| Shift | Sprint |
| Space | Jump (also hops off a ladder) |
| E | Grab what you look at (at that point) / release. On a station, nail box or bell: use it |
| Q | Release |
| G | Throw (an impulse: light things fly, heavy things barely move) |
| Mouse wheel | Hold distance |
| Hold R + mouse | Rotate the held object |
| LMB | Use the held tool |
| RMB or Tab (hold) | Raise the blueprint tablet; 1-4 or LMB switch Front/Side/Top/Back |
| Esc | Free the mouse cursor (click to recapture) |
| R (results) | Restart with a new pile |

### Building

- Hold a part near its glowing ghost (≤ 30 cm, ≤ 20°): it gets pulled in by a
  soft spring and turns yellow-outlined ("placed"). Grab it again to take it out.
- Ghosts: current stage glows blue, a slot that can take a part right now is
  brighter, the slot you're about to snap into is green, later stages are faint.
  A slot only accepts a part once everything under it is FIXED.
- Fixing: **wood** = hammer (3 hits = 3 nails), **door / tin roof** =
  screwdriver (hold LMB, 3 screws), **stone / bricks** = trowel with fresh mortar.
  Nails/screws come from your pockets (E on a box while holding the hammer or
  screwdriver, or LMB while holding the box) or from a box within 2.5 m.
- **Saw**: aim at a loose plank/post/beam, hold LMB and move the mouse left and
  right. The red line and the HUD show where it cuts and the two lengths; it
  snaps to lengths the building still needs. Beams use the two-person saw
  (alone: half speed).
- **Glass cutter**: aim at a pane, hold LMB and drag slowly. Too fast = crack.
  Panes shatter if dropped or hit hard (only once the build timer runs).
- **Mortar**: put a bucket next to the station (or hold it), E on cement /
  sand / tap to add 1 unit each: recipe **1 cement : 3 sand : 1 water**. Stir
  with the shovel (aim at the bucket, hold LMB, move the mouse in circles).
  Too wet = useless soup; too dry won't mix. Fresh for 60 s, then it sets. Turn
  a bucket upside down to tip it out. Trowel: LMB on the bucket to scoop
  (3 dabs), LMB on a placed stone/brick stack to mortar it.
- **Ladder**: carry it, LMB to lean it against the wall you look at, walk into
  it and look up/down to climb. Grab a standing ladder to hold it steady; an
  unattended ladder may slip while climbed.
- **Wheelbarrow**: grab a handle, lift, push. It tips on bumps.
- **Pallets**: drop parts on the labeled pallets; they count what's on them.
  The tablet checklist only counts sorted or built parts; the pile is "?".

### Round flow

1. **Briefing** (10 s): the target and stages are shown; the truck backs in.
2. **Dump**: the bed tips, the pile slides out. The clock starts when the pile
   has mostly settled (or 5 s after tipping).
3. **Build**: shed 4:00, cottage 8:00. Ring the bell at the site to finish early
   (time bonus).
4. **Final test** (10 s, fixed camera): wind (cottage: wind + rain). Placed but
   unfixed parts lose their spring; badly fixed parts can break loose.
5. **Results**: score = fixed slots / all − parts lost in the test + time bonus;
   rank title. R or the button restarts with a new pile.

## 4. Layers and tags

| Name | Kind | Created by |
|---|---|---|
| `Hologram` | Layer (prefers index 31) | the menu item, automatically (already in TagManager) |
| `Player`, `MainCamera` | Tags | built into Unity |

## 5. Known risks (nothing here has run in the editor yet)

All code was written without access to Unity. It compiles cleanly with
`Tools~/compile-check/run.sh` (Unity 2021.3 reference DLLs + shims) and the kit
designs pass `Tools~/kit-gen/run.sh`, but nothing has been play-tested.

- **Unity 6 API drift**: possible obsolete warnings (`FindObjectsByType(...SortMode)`,
  `Physics.*NonAlloc`). Should be warnings; if one is an error in 6.3 it's a one-liner.
- **Joint target rotation**: the grab and snap joints rely on "a ConfigurableJoint
  keeps the relative rotation it had when created" and rotate their kinematic
  anchor instead of setting `targetRotation`. If tools don't swing into their
  hold pose, or snapped parts don't turn into place, look at
  `PlayerGrabber.FixedUpdate` / `BuildSlot.FixedUpdate` (both wait 2 physics
  steps before rotating the anchor).
- **Physics stability**: tall FixedJoint chains (brick walls, chimney) may sag or
  jitter. Raise `buildingSolverIterations`, the break forces, or lower masses.
  The pile (52 parts shed, ~175 cottage) spawns in one frame on the truck bed;
  watch for parts popping out on spawn (packing gap is 3 cm).
- **Snap spring**: the anchor is raised by the static sag `g / snapFrequency^2`
  (0.2 m at 7 rad/s). If placed parts float or sink, tune `snapFrequency` /
  `snapForceWeights`.
- **Glass**: breaking uses relative speed AND velocity change; panes might shatter
  too easily (being nailed, snapped) or never. Tune `glassBreakSpeed`,
  `glassBreakDeltaV`.
- **Truck dump**: parts might stick in the bed or fly. Tune `Truck.tipAngle`,
  hold time and the bed friction (default material).
- **Hold poses**: tool grip points and hold poses in `SceneBuilder.Build<Tool>`
  were guessed (hammer swing axis, saw/shovel orientation, tablet raise pose).
- **Climbing onto roofs**: the cottage roof is 43°; standing on it relies on the
  CharacterController slope limit (50°).
- **Tablet readability**: lots of small world-space text (checklist at 11 px/mm).
- **Wind**: `F = ½ ρ v² Cd A` per part (+ uplift on flat things), capped at 25 g.
  Might be too weak/strong for the joint strengths.
- **Performance**: ~175 rigidbodies + combined meshes for the cottage; fine on
  desktop, untested.
- **StreamingAssets** are read with `File.ReadAllText` (desktop only; Android/WebGL
  would need `UnityWebRequest`).
- **Input System UI module**: the builder calls `AssignDefaultActions()`. If the
  results button doesn't respond, use R.

## 6. What to tune first (`Assets/Settings/GameSettings.asset`)

| Setting | Default | What it does |
|---|---|---|
| `grabStrength` | 420 N | Max force per player. Weight above it can't be lifted alone. |
| `grabFrequency` / `grabDampingRatio` | 14 rad/s / 0.75 | How snappy/bouncy held things are. |
| `grabMaxTorque` | 110 N m | How much long things sag when held off-center. |
| `lightMaxMass` / `mediumMaxMass` | 10 / 40 kg | Weight class thresholds (HUD + walk speed). |
| `snapDistance` / `snapAngle` | 0.3 m / 20° | When a held part gets pulled into its slot. |
| `snapFrequency` | 7 rad/s | Softness of a placed (unfixed) part. |
| `nail/screw/mortarBreakForce` | 6000/8000/12000 N | How strong fixed joints are. |
| `buildingSolverIterations` | 24 | Stability of the jointed building. |
| `plank/post/beamStrokes`, `sawStrokePixels` | 6/10/14, 45 px | Sawing effort. |
| `glassScorePixels`, `glassCrackSpeed` | 900 px, 2600 px/s | Glass cutting effort and risk. |
| `mortarFreshSeconds`, `waterRatioMin/Max` | 60 s, 0.18–0.34 | Mortar window and recipe tolerance. |
| `windSpeed` / `windGust` | 13 / 6 m/s | Final test strength. |
| `settleFraction`, `settleTimeout` | 0.85, 5 s | When the clock starts after the dump. |
| `sparePercent`, `wrongLengthDecoys` | 15 %, 3 | Pile generation. |

Also see the serialized fields on `Truck` (tip angle, timings) and `Ladder`
(lean angle), and the hold poses in `SceneBuilder`.
