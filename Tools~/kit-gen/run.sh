#!/usr/bin/env bash
# Runs the procedural kit designs WITHOUT Unity: validates them, checks geometry
# (overlapping slots, restsOn pairs that don't touch, floating ground slots),
# prints the generated pile, and writes Assets/StreamingAssets/Kits/*.json.
# Same JSON as the editor menu "Build Crew/Generate Kit JSONs" (field order may differ).
# Requirements: dotnet SDK 8+. Usage: Tools~/kit-gen/run.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
WORK="${KIT_GEN_DIR:-/tmp/buildcrew-kit-gen}"
rm -rf "$WORK" && mkdir -p "$WORK/src"
cp "$REPO/Assets/Scripts/Kits/KitData.cs" "$REPO/Assets/Scripts/Kits/KitDesigns.cs" "$REPO/Assets/Scripts/Kits/PileGenerator.cs" "$WORK/src/"
cp "$HERE/UnityShim.cs.txt" "$WORK/src/UnityShim.cs"
cp "$HERE/PartCatalogStub.cs.txt" "$WORK/src/PartCatalogStub.cs"
cp "$HERE/Program.cs.txt" "$WORK/src/Program.cs"
cat > "$WORK/kitgen.csproj" <<'PROJ'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>
<LangVersion>9.0</LangVersion><Nullable>disable</Nullable><ImplicitUsings>disable</ImplicitUsings></PropertyGroup></Project>
PROJ
cd "$WORK"
dotnet run -v q -- "$REPO/Assets/StreamingAssets/Kits"
