#!/usr/bin/env bash
# Downloads Unity reference assemblies from nuget.org so the Unity-facing scripts can be
# compile-checked outside the Unity Editor (used by the UnityCheck/EditorCheck projects).
# These DLLs are reference-only stand-ins; the real build always happens inside Unity.
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)/.unityrefs"
mkdir -p "$DIR"
fetch() { # id version subdir
  local id="$1" ver="$2" out="$DIR/$3"
  [ -d "$out" ] && return 0
  mkdir -p "$out"
  curl -fsSL -o "$out/pkg.nupkg" "https://api.nuget.org/v3-flatcontainer/$id/$ver/$id.$ver.nupkg"
  (cd "$out" && unzip -q -o pkg.nupkg && rm pkg.nupkg)
}
fetch unityengine.modules 2021.3.33 engine
fetch unity3d.unityengine.ui 2020.3.21 ugui
fetch unity3d.sdk 2021.1.14.1 editor
echo "Unity reference assemblies ready in $DIR"
