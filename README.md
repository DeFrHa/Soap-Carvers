# Build Crew (working title)

A wonky first-person "friendslop" co-op building game. A big, tangled
physics pile of building parts lands next to an empty plot. Sort it onto pallets, saw planks to
length, cut glass, mix mortar, and assemble the building from the blueprint
before the clock runs out. Then the wind (and rain) test shows what you built
badly.

- Unity 6000.3.6f1, URP, new Input System, C#. Everything is made from
  primitives and code; no external assets.
- Every object is a real rigidbody: holding is a physics joint, heavy things
  sag and need a friend, placed parts hang on soft springs, fixed parts are
  jointed to what they rest on.
- Data-driven kits (`StreamingAssets/Kits/*.json`): a garden shed (4 min,
  wind) and a brick cottage (8 min, wind + rain), generated procedurally by
  `KitDesigns`.
- Every change goes through command objects, ready for a network layer.
- The whole scene is generated (**Build Crew → Create Playable Scene**), or
  at runtime with `GameBootstrap`.

See **[SETUP.md](SETUP.md)** to get it running, the controls and tuning, and
**[CLAUDE.md](CLAUDE.md)** for architecture and rules.
