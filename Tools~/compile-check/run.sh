#!/usr/bin/env bash
# Compile-checks Assets/Scripts WITHOUT Unity (e.g. in a CI/cloud container).
#
# It compiles the scripts against Unity 2021.3 reference DLLs from NuGet plus a
# tiny Input System stub. Unity 6-only APIs are shimmed by text substitution
# (Rigidbody.linearVelocity -> velocity), so this catches syntax/type/naming
# errors but NOT Unity 6 API differences. Unity itself is the real authority.
#
# Requirements: dotnet SDK (8+), curl, unzip, network access to api.nuget.org.
# Usage: Tools~/compile-check/run.sh      (folders ending in ~ are ignored by Unity)
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
WORK="${COMPILE_CHECK_DIR:-/tmp/soapcarvers-compile-check}"
REFS="$WORK/refs"
mkdir -p "$REFS"

fetch() { # id version
  local dir="$REFS/$1_$2"
  [ -d "$dir" ] && return
  curl -sSfL -o "$dir.nupkg" "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg"
  mkdir -p "$dir" && (cd "$dir" && unzip -q -o "../$1_$2.nupkg")
}
fetch unityengine.modules 2021.3.33
fetch unity3d.unityengine.ui 2020.3.21
fetch unity3d.sdk 2021.1.14.1   # for UnityEditor.dll

PROJ="$WORK/proj"
rm -rf "$PROJ" && mkdir -p "$PROJ/src" "$PROJ/stubs"
cp -r "$REPO/Assets/Scripts/." "$PROJ/src/"
cp "$HERE/InputSystemStub.cs.txt" "$PROJ/stubs/InputSystemStub.cs"
find "$PROJ/src" -name "*.cs" -exec sed -i 's/\.linearVelocity/.velocity/g' {} +

{
  echo '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
  echo '<TargetFramework>netstandard2.1</TargetFramework><LangVersion>9.0</LangVersion>'
  echo '<EnableDefaultCompileItems>false</EnableDefaultCompileItems><NoWarn>CS0649;CS0414</NoWarn>'
  echo '</PropertyGroup><ItemGroup><Compile Include="src/**/*.cs" /><Compile Include="stubs/*.cs" /></ItemGroup><ItemGroup>'
  find "$REFS/unityengine.modules_2021.3.33" -name "*.dll" | awk -F/ '!seen[$NF]++' | while read -r f; do
    echo "<Reference Include=\"$(basename "$f" .dll)\"><HintPath>$f</HintPath></Reference>"
  done
  echo "<Reference Include=\"UnityEngine.UI\"><HintPath>$(find "$REFS/unity3d.unityengine.ui_2020.3.21" -name UnityEngine.UI.dll | head -1)</HintPath></Reference>"
  echo "<Reference Include=\"UnityEditor\"><HintPath>$(find "$REFS/unity3d.sdk_2021.1.14.1" -name UnityEditor.dll | head -1)</HintPath></Reference>"
  echo '</ItemGroup></Project>'
} > "$PROJ/check.csproj"

cd "$PROJ"
dotnet build -nologo -v q 2>&1 | grep -E "error|warning CS|Build succeeded" | grep -v "MSB3277\|MSB3243" | sort -u
