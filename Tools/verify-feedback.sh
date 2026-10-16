#!/bin/sh
set -eu
: "${UNITY_EDITOR_ROOT:?Set UNITY_EDITOR_ROOT to the Unity.app installation}"
repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repo_root"
assembly="Library/ScriptAssemblies/Assembly-CSharp.dll"
if [ ! -f "$assembly" ]; then
    echo 'Compile the current project in Unity first; this probe checks the compiled assembly.' >&2
    exit 1
fi
mono_root="$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/MonoBleedingEdge"
build_dir="Tests/Headless/.build/feedback"
mkdir -p "$build_dir"
"$mono_root/bin/mono" "$mono_root/lib/mono/4.5/csc.exe" /nologo /langversion:latest /target:exe \
    "/out:$build_dir/feedback-checks.exe" Tests/Presentation/FeedbackWaveChecks.cs
MONO_PATH="$UNITY_EDITOR_ROOT/Contents/Resources/Scripting/Managed/UnityEngine:Library/ScriptAssemblies${MONO_PATH:+:$MONO_PATH}" \
    "$mono_root/bin/mono" "$build_dir/feedback-checks.exe" "$assembly" "$build_dir/waves"
