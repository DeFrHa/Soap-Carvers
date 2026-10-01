# Soap Carvers – Setup

This repo currently holds only `Assets/`, plus docs and tools. It does not
contain `ProjectSettings/` or `Packages/` yet, so the first time you need to
pair it with a fresh URP project.

## 1. First-time setup (turn the repo into a Unity project)

1. In **Unity Hub**, choose **New project**, editor **6000.3.6f1**, template
   **Universal 3D** (URP). Create it anywhere, e.g. `SoapCarversTemplate`.
2. Close Unity. From the new project, copy these into the root of this repo:
   - `Packages/`
   - `ProjectSettings/`
   - `Assets/Settings/` (the URP pipeline/renderer assets the template made)

   Do not copy the template's sample scene or `TutorialInfo`.
3. Unity Hub: choose **Add**, then **Add project from disk**, and pick this
   repo folder. Open it with 6000.3.6f1.
4. Check the packages in **Window → Package Manager → In Project**:
   - **Input System** is installed. The Unity 6 URP template includes it;
     otherwise install it.
   - **Universal RP** is installed.
   - **Unity UI** (`com.unity.ugui`) is installed. It is part of the template.
5. Go to **Edit → Project Settings → Player → Other Settings → Active Input
   Handling** and set it to **Input System Package (New)** or **Both**. Unity
   restarts if you change it.
6. Commit `Packages/` and `ProjectSettings/` (and `Assets/Settings/`) so the
   next clone opens directly.

Quick alternative for a throwaway test: create the URP project and copy this
repo's `Assets/Scripts` folder into its `Assets/`.

## 2. Build and play

1. Run the menu **Soap Carvers → Create Playable Scene**. It:
   - adds a `Hologram` layer (index 31 if free),
   - generates `Assets/Materials/*.mat` and `Assets/Settings/GameSettings.asset`,
   - builds and saves `Assets/Scenes/SoapCarvers.unity` and adds it to Build
     Settings.
2. Press **Play**. Click the Game view to capture the mouse.

Runtime-only alternative: in any empty scene, add an empty GameObject with
the **GameBootstrap** component and press Play. It builds everything in code.

To tweak numbers (timer, block/voxel size, tool radii, debris), select
`Assets/Settings/GameSettings.asset`. Re-running the menu keeps this asset,
so after updating the code, new defaults (for example `voxelSize` 0.125)
don't apply to an existing asset. Delete it, or edit it, to pick them up.
To try the easy target, set **Target Shape Name** to `Mushroom`.

## 3. Controls

| Input | Action |
|---|---|
| WASD / arrows | Move |
| Mouse | Look |
| Shift | Sprint |
| Space | Jump (also hops off a ladder) |
| E | Pick up; press the workbench button; toggle scaffold wheel brakes. While carrying the ladder: place it. While holding dynamite and looking at soap: stick it on |
| Q | Drop held item |
| G | Throw held item |
| LMB | Use tool (hold for knife, chainsaw, pickaxe repeat). Dynamite: light the fuse |
| RMB or Tab (hold) | Raise the blueprint tablet |
| 1 / 2 / 3 / 4 | Tablet view: Front / Side / Top / Back (LMB cycles while raised) |
| Ladder / scaffold | Walk into the ladder side holding W. Look up to climb up, look down to climb down. At the top you hop off onto the ledge or deck |
| Scaffold | Look at it and press E to release the brakes, then walk into it to push it. Press E again to lock it before climbing |
| R | Restart (results screen) |
| Esc | Free the mouse cursor (click to recapture) |

### Round flow

1. **Ready**: the clock (5:00) starts on the first carve, or when you press the
   red button on the workbench.
2. **Carving**: once the timer hits 0:00, tools stop working. Pressing the red
   button during the round ends it early.
3. **Scanning**: a glowing plane sweeps down over the block.
4. **Results**: match %, rank title, raw IoU, how much of the target is still
   intact, and how much excess soap is left. Press R, the on-screen button or
   the red button to play again.

## 4. Layers and tags

| Name | Kind | Created by |
|---|---|---|
| `Hologram` | Layer (prefers index 31) | the menu item, automatically |
| `Player`, `MainCamera` | Tags | built into Unity |

If the menu cannot add the layer (all user slots are taken), name any free
layer `Hologram` by hand. If no layer is named `Hologram`, the code falls back
to raw layer index 31, which works but won't show a name in the Inspector.

## 5. Known risks and things that need tuning

This code was written without access to the Unity editor. It compiles
cleanly with `Tools~/compile-check/run.sh`, which checks against Unity 2021.3
reference DLLs, but nothing has been run or play-tested yet. Expect to tune:

- **Compile drift in 6000.3.** Possible obsolete warnings, for example
  `FindObjectsByType(FindObjectsSortMode)` or `Physics.RaycastNonAlloc`. They
  should be warnings, not errors. If a call turned into an error in 6.3, it is
  a one-line fix.
- **Feel.** Movement speeds, gravity, jump, mouse sensitivity, the item hold
  poses (`Configure(...)` calls in `SceneBuilder`), the pickaxe swing curve,
  chainsaw jitter, and dynamite knockback (`GameSettings.dynamiteKnockback`).
- **Ladder.** It is a dynamic rigidbody, 12 m long, with grippy rails, and is
  placed leaning at 16°. If there's nothing to lean on, it falls. Climbers add
  25 kg at their position (`ClimbZone.climberMass`). Tune friction
  (`PhysicsMaterials.Grippy`), the lean angle and the mass if it slips or
  jitters. The scene starts with it leaning against the soap.
- **Scaffolds.** Three rolling towers (4.5 m, 8.5 m and 12.5 m decks) with
  sphere "caster" wheels. The wheels are frictionless with brakes off and
  grippy with brakes on. The center of mass is set very low so they don't
  tip; dynamite will still launch them. Check the push force and speed
  (`Scaffold.pushForce`, `maxPushSpeed`), the climb-zone fit and the step-off
  onto the deck.
- **Dynamite sticking.** A free stick turns kinematic on its first touch
  with soap. It checks every 0.25 s whether soap is still under it, and falls
  if not.
- **Performance.** At 0.125 m voxels the grid is 129³ points, about 8.6 MB
  each for the soap and the target, in 512 chunks. Startup meshes every chunk
  and voxelizes the target (narrow-band, so only near the surface). Expect
  roughly a second. A dynamite blast dirties about 100 chunks; they rebuild at
  most `maxChunkRebuildsPerFrame` (24) per frame. If carving feels slow, raise
  `voxelSize` to 0.16–0.25, or move meshing to Burst/Jobs.
- **Floating soap.** Islands cut loose from the block stay floating; they
  don't fall.
- **Materials.** The URP Lit/Unlit properties are set from code. If the scan
  plane renders opaque, set its material's Surface Type to Transparent in the
  inspector. With **GameBootstrap only** (no generated scene) in a player
  build, URP shaders can be stripped, giving pink objects. Use the generated
  scene, or add the shaders to *Always Included Shaders*.
- **Tablet readability.** RenderTexture resolution (512²), hologram color, and
  the text overlay layout on the tablet.
- **Scoring.** It samples grid points (density > 0). An untouched block scores
  0%. Carving everything away also scores 0%, because the score is clamped.
- **Input System UI module.** The builder calls `AssignDefaultActions()`. If
  the results-screen button doesn't respond to clicks, use R.
- **Determinism.** The soap is deterministic given the carve log on the same
  platform. Debris uses `UnityEngine.Random` and is cosmetic only.
