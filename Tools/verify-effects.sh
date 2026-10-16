#!/bin/sh
set -eu
if [ "${1:-}" = "--help" ]; then
    printf '%s\n' 'Usage: UNITY_EDITOR_ROOT="<Unity.app>" ./Tools/verify-effects.sh' 'Offline pure-core effects replay; starts no Unity Editor or network restore.' 'Optional NEWTONSOFT_JSON_PATH and EFFECT_REPORT_PATH override existing JSON assembly and report path.'
    exit 0
fi
if [ "$#" -ne 0 ] || [ -z "${UNITY_EDITOR_ROOT:-}" ]; then
    printf '%s\n' 'Error: UNITY_EDITOR_ROOT must identify the installed Unity.app; use --help.' >&2
    exit 2
fi
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
mono_root="$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/MonoBleedingEdge"
json_dll="${NEWTONSOFT_JSON_PATH:-$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/Managed/Newtonsoft.Json.dll}"
for required_file in "$mono_root/bin/mono" "$mono_root/lib/mono/4.5/csc.exe" "$mono_root/lib/mono/4.5/Facades/netstandard.dll" "$json_dll"; do
    if [ ! -f "$required_file" ]; then
        printf 'Error: missing offline dependency: %s\n' "$required_file" >&2
        exit 2
    fi
done
json_dir=$(CDPATH= cd -- "$(dirname -- "$json_dll")" && pwd)
cd "$repo_root"
mkdir -p Tests/Headless/.build
"$mono_root/bin/mono" "$mono_root/lib/mono/4.5/csc.exe" /nologo /langversion:latest /target:exe \
    /out:Tests/Headless/.build/effects.exe "/r:$json_dll" "/r:$mono_root/lib/mono/4.5/Facades/netstandard.dll" \
    Assets/OneMoreBonkRebuild/Gameplay/ForgeTypes.cs Assets/OneMoreBonkRebuild/Gameplay/RiskForgeModels.cs \
    Assets/OneMoreBonkRebuild/Gameplay/RiskForgeRules.cs Assets/OneMoreBonkRebuild/Gameplay/RiskRelicData.cs \
    Tests/Headless/UnityDataShim.cs Tests/Headless/ReplayChecks.cs
MONO_PATH="$json_dir${MONO_PATH:+:$MONO_PATH}" "$mono_root/bin/mono" Tests/Headless/.build/effects.exe \
    Assets/Resources Tests/Fixtures "${EFFECT_REPORT_PATH:-Docs/Research/effect-regression.json}"
