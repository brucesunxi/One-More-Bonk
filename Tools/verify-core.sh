#!/bin/sh
set -eu

usage() {
    cat <<'HELP'
Usage: UNITY_EDITOR_ROOT="<Unity editor installation>" ./Tools/verify-core.sh

Offline probability baseline. No Unity Editor process or package restore is started.
UNITY_EDITOR_ROOT must name the Unity.app directory on macOS (Unity 6000.3.17f1
was verified). It must contain Contents/Resources/Scripting/MonoBleedingEdge.
Optional NEWTONSOFT_JSON_PATH overrides the existing Editor assembly at
Contents/Resources/Scripting/Managed/Newtonsoft.Json.dll. No SDK DLL is vendored.
Outputs are ignored under Tests/Headless/.build; inputs are repository-local.
HELP
}

if [ "${1:-}" = "--help" ]; then usage; exit 0; fi
if [ "$#" -ne 0 ]; then usage >&2; exit 2; fi
if [ -z "${UNITY_EDITOR_ROOT:-}" ]; then
    printf '%s\n' 'Error: UNITY_EDITOR_ROOT is required; run Tools/verify-core.sh --help.' >&2
    exit 2
fi

if [ ! -d "$UNITY_EDITOR_ROOT" ]; then
    printf 'Error: Unity Editor installation directory does not exist: %s\n' "$UNITY_EDITOR_ROOT" >&2
    exit 2
fi
UNITY_EDITOR_ROOT=$(CDPATH= cd -- "$UNITY_EDITOR_ROOT" && pwd)

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
mono_root="$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/MonoBleedingEdge"
mono_bin="$mono_root/bin/mono"
csc_exe="$mono_root/lib/mono/4.5/csc.exe"
netstandard_dll="$mono_root/lib/mono/4.5/Facades/netstandard.dll"
json_dll="${NEWTONSOFT_JSON_PATH:-$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/Managed/Newtonsoft.Json.dll}"

for required_file in "$mono_bin" "$csc_exe" "$netstandard_dll" "$json_dll"; do
    if [ ! -f "$required_file" ]; then
        printf 'Error: required offline dependency is missing: %s\n' "$required_file" >&2
        printf '%s\n' 'Check UNITY_EDITOR_ROOT and NEWTONSOFT_JSON_PATH; nothing was downloaded.' >&2
        exit 2
    fi
done
if [ ! -x "$mono_bin" ]; then
    printf 'Error: Mono is not executable: %s\n' "$mono_bin" >&2
    exit 2
fi

json_dir=$(CDPATH= cd -- "$(dirname -- "$json_dll")" && pwd)
json_dll="$json_dir/$(basename -- "$json_dll")"

cd "$repo_root"
build_dir="Tests/Headless/.build"
mkdir -p "$build_dir"
# Invoke the compiler assembly explicitly. Editor bin/csc wrappers may reference build-machine paths.
"$mono_bin" "$csc_exe" /nologo /langversion:latest /target:exe \
    "/out:$build_dir/golden.exe" "/r:$json_dll" "/r:$netstandard_dll" \
    Assets/OneMoreBonkRebuild/Gameplay/ForgeTypes.cs \
    Assets/OneMoreBonkRebuild/Gameplay/RiskForgeModels.cs \
    Assets/OneMoreBonkRebuild/Gameplay/RiskForgeRules.cs \
    Assets/OneMoreBonkRebuild/Gameplay/RiskRelicData.cs \
    Tests/Headless/UnityDataShim.cs Tests/Headless/GoldenRunner.cs

MONO_PATH="$json_dir${MONO_PATH:+:$MONO_PATH}" \
    "$mono_bin" "$build_dir/golden.exe" Assets/Resources Tests/Fixtures/probability-vectors.json
