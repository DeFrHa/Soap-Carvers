# Soap Carvers

A wonky first-person "friendslop" game: you get a 16 m bar of soap, a
workbench full of questionable tools (knife, pickaxe, chainsaw, dynamite,
a 12 m ladder) and a blueprint tablet that shows the target, a swan. You
have five minutes, and then the scanner decides how close you got.

- Unity 6000.3.6f1, URP, new Input System, C#.
- Voxel soap uses flat-shaded marching cubes, a chunked mesh, and
  command-based carving with a replayable log.
- Targets are code-defined SDFs (Swan, Mushroom), and scoring uses
  normalized IoU.
- The whole scene is generated from code (**Soap Carvers → Create Playable
  Scene**), or at runtime with `GameBootstrap`.

See **[SETUP.md](SETUP.md)** to get it running and for the controls, and
**[CLAUDE.md](CLAUDE.md)** for architecture and conventions.
