#!/bin/sh
set -eu
if [ -z "${UNITY_EDITOR_ROOT:-}" ]; then
    printf '%s\n' 'Error: UNITY_EDITOR_ROOT is required (the Unity.app directory on macOS).' >&2
    exit 2
fi
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
mono_root="$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/MonoBleedingEdge"
json_dll="${NEWTONSOFT_JSON_PATH:-$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/Managed/Newtonsoft.Json.dll}"
json_dir=$(CDPATH= cd -- "$(dirname -- "$json_dll")" && pwd)
cd "$repo_root"
mkdir -p Tests/Headless/.build
"$mono_root/bin/mono" "$mono_root/lib/mono/4.5/csc.exe" /nologo /langversion:latest /target:exe \
    /out:Tests/Headless/.build/session.exe "/r:$json_dll" "/r:$mono_root/lib/mono/4.5/Facades/netstandard.dll" \
    Assets/OneMoreBonkRebuild/Gameplay/*.cs Assets/OneMoreBonkRebuild/Session/*.cs \
    Assets/OneMoreBonkRebuild/Rewards/*.cs Assets/OneMoreBonkRebuild/Persistence/*.cs \
    Tests/Headless/UnityDataShim.cs Tests/Headless/EconomyChecks.cs Tests/Headless/SessionChecks.cs
MONO_PATH="$json_dir${MONO_PATH:+:$MONO_PATH}" "$mono_root/bin/mono" Tests/Headless/.build/session.exe Assets/Resources
