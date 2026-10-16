#!/bin/sh
set -eu
if [ "${1:-}" = "--help" ]; then
    printf '%s\n' 'Usage: UNITY_EDITOR_ROOT="<Unity.app>" ./Tools/probe-growth-balance.sh [common-seeds=20]' 'Exploratory legal-Session strategy probe; source/report remain repository-local.'
    exit 0
fi
if [ "$#" -gt 1 ] || [ -z "${UNITY_EDITOR_ROOT:-}" ]; then
    printf '%s\n' 'Error: set UNITY_EDITOR_ROOT to an existing Unity.app installation; optional sole argument is common-seed count.' >&2
    exit 2
fi
case "${1:-20}" in ''|*[!0-9]*) printf '%s\n' 'Error: common-seeds must be a positive integer.' >&2;exit 2;; esac
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)
UNITY_EDITOR_ROOT=$(CDPATH= cd -- "$UNITY_EDITOR_ROOT" && pwd)
mono_root="$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/MonoBleedingEdge"
json_dll="${NEWTONSOFT_JSON_PATH:-$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/Managed/Newtonsoft.Json.dll}"
for required in "$mono_root/bin/mono" "$mono_root/lib/mono/4.5/csc.exe" "$mono_root/lib/mono/4.5/Facades/netstandard.dll" "$json_dll";do
    if [ ! -f "$required" ];then printf 'Error: missing offline dependency: %s\n' "$required" >&2;exit 2;fi
done
json_dir=$(CDPATH= cd -- "$(dirname -- "$json_dll")" && pwd)
json_dll="$json_dir/$(basename -- "$json_dll")"
cd "$repo_root"
mkdir -p Tests/Headless/.build
if ! command -v shasum >/dev/null 2>&1;then printf '%s\n' 'Error: shasum is required to bind the probe to its compiled source snapshot.' >&2;exit 2;fi
shasum -a 256 Assets/OneMoreBonkRebuild/Gameplay/*.cs Assets/OneMoreBonkRebuild/Session/*.cs \
    Assets/OneMoreBonkRebuild/Rewards/*.cs Assets/OneMoreBonkRebuild/Persistence/*.cs \
    Assets/Resources/Risk/rule-data.json Tests/Headless/UnityDataShim.cs Tests/Headless/BalanceGrowthProbe.cs Tools/probe-growth-balance.sh \
    > Tests/Headless/.build/growth-compile-sources.sha256
"$mono_root/bin/mono" "$mono_root/lib/mono/4.5/csc.exe" /nologo /langversion:latest /define:BALANCE_GROWTH_PROBE_STANDALONE /target:exe \
    /out:Tests/Headless/.build/growth-balance.exe "/r:$json_dll" "/r:$mono_root/lib/mono/4.5/Facades/netstandard.dll" \
    Assets/OneMoreBonkRebuild/Gameplay/*.cs Assets/OneMoreBonkRebuild/Session/*.cs \
    Assets/OneMoreBonkRebuild/Rewards/*.cs Assets/OneMoreBonkRebuild/Persistence/*.cs \
    Tests/Headless/UnityDataShim.cs Tests/Headless/BalanceGrowthProbe.cs
shasum -a 256 -c Tests/Headless/.build/growth-compile-sources.sha256 >/dev/null
MONO_PATH="$json_dir${MONO_PATH:+:$MONO_PATH}" "$mono_root/bin/mono" Tests/Headless/.build/growth-balance.exe Assets/Resources Docs/Research/balance-growth-policy.json "${1:-20}"
