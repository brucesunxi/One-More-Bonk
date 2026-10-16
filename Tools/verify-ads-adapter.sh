#!/bin/sh
set -eu
: "${UNITY_EDITOR_ROOT:?Set UNITY_EDITOR_ROOT to the Unity.app installation}"
repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repo_root"
mono_root="$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/MonoBleedingEdge"
json_dll="$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/Managed/Newtonsoft.Json.dll"
build_dir="Tests/Headless/.build/ads"
mkdir -p "$build_dir"
"$mono_root/bin/mono" "$mono_root/lib/mono/4.5/csc.exe" /nologo /langversion:latest /target:exe \
    "/out:$build_dir/ads-checks.exe" "/r:$json_dll" "/r:$mono_root/lib/mono/4.5/Facades/netstandard.dll" \
    Assets/OneMoreBonkRebuild/Gameplay/*.cs Assets/OneMoreBonkRebuild/Session/*.cs \
    Assets/OneMoreBonkRebuild/Persistence/*.cs Assets/OneMoreBonkRebuild/Rewards/*.cs \
    Assets/OneMoreBonkRebuild/Ads/RebuildRewardedAds.cs Tests/Headless/UnityDataShim.cs Tests/Ads/ControlledSdkChecks.cs
MONO_PATH="$(dirname -- "$json_dll")${MONO_PATH:+:$MONO_PATH}" \
    "$mono_root/bin/mono" "$build_dir/ads-checks.exe" Assets/Resources
