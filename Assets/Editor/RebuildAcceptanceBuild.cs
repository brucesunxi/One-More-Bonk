using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using OneMoreBonk.Rebuild.Acceptance;

namespace OneMoreBonk.Rebuild.Editor
{
    // Dedicated package: it never opens, installs over or writes the player's .rebuild application.
    public static class RebuildAcceptanceBuild
    {
        const string ScenePath = "Assets/Scenes/DeviceAcceptance.unity";
        public const string ApplicationId = "com.brucesunxi.onemorebonk.acceptance";
        [MenuItem("One More Bonk Rebuild/Acceptance/Run Native Editor Suite")]
        public static void ValidateNative()
        {
            var report = RebuildDeviceAcceptance.Run("editor-controlled", Path.GetFullPath("Tests/Results/NativeAcceptance"));
            if (report.Status != "PASS") throw new InvalidOperationException("Native acceptance failed: " + JsonUtility.ToJson(report));
        }
        [MenuItem("One More Bonk Rebuild/Acceptance/Build Isolated Android Checks")]
        public static void BuildAndroid()
        {
            ValidateNative();
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            string oldId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string oldName = PlayerSettings.productName, oldVersion = PlayerSettings.bundleVersion;
            int oldCode = PlayerSettings.Android.bundleVersionCode;
            bool oldBundle = EditorUserBuildSettings.buildAppBundle;
            var oldBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            var oldArchitectures = PlayerSettings.Android.targetArchitectures;
            const string resolverCache = "ProjectSettings/AndroidResolverDependencies.xml";
            byte[] oldResolverCache = File.Exists(resolverCache) ? File.ReadAllBytes(resolverCache) : null;
            string buildId = "acceptance-" + Guid.NewGuid().ToString("N");
            string output = Path.GetFullPath("Builds/Android/Acceptance/OneMoreBonk-NativeAcceptance.apk");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Controlled Acceptance Only");
                root.AddComponent<RebuildDeviceAcceptance>().BuildId = buildId;
                var camera = new GameObject("Acceptance Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.04f, .08f, .12f);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save isolated acceptance scene.");
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
                PlayerSettings.productName = "Bonk Controlled Acceptance";
                PlayerSettings.bundleVersion = "0.1.0-acceptance";
                PlayerSettings.Android.bundleVersionCode = 1;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                EditorUserBuildSettings.buildAppBundle = false;
                var stamp = new AcceptanceStamp { BuildId = buildId, BuiltAtUtc = DateTime.UtcNow.ToString("O"),
                    ApplicationId = ApplicationId, RuleSha256 = Hash("Assets/Resources/Risk/rule-data.json"),
                    RunnerSha256 = Hash("Assets/OneMoreBonkRebuild/Acceptance/RebuildDeviceAcceptance.cs"),
                    GameplaySha256 = SourceHash("Assets/OneMoreBonkRebuild/Gameplay"),
                    SessionSha256 = SourceHash("Assets/OneMoreBonkRebuild/Session"),
                    PersistenceSha256 = SourceHash("Assets/OneMoreBonkRebuild/Persistence"),
                    RewardsSha256 = SourceHash("Assets/OneMoreBonkRebuild/Rewards") };
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.Android,
                    options = BuildOptions.Development, extraScriptingDefines = new[] { "BONK_DEVICE_ACCEPTANCE" }
                });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Acceptance player build failed.");
                if (stamp.RuleSha256 != Hash("Assets/Resources/Risk/rule-data.json") ||
                    stamp.RunnerSha256 != Hash("Assets/OneMoreBonkRebuild/Acceptance/RebuildDeviceAcceptance.cs") ||
                    stamp.GameplaySha256 != SourceHash("Assets/OneMoreBonkRebuild/Gameplay") ||
                    stamp.SessionSha256 != SourceHash("Assets/OneMoreBonkRebuild/Session") ||
                    stamp.PersistenceSha256 != SourceHash("Assets/OneMoreBonkRebuild/Persistence") ||
                    stamp.RewardsSha256 != SourceHash("Assets/OneMoreBonkRebuild/Rewards"))
                    throw new InvalidOperationException("Acceptance sources changed during build; do not install this APK.");
                stamp.ApkSha256 = Hash(output);
                File.WriteAllText(Path.ChangeExtension(output, ".build.json"), JsonUtility.ToJson(stamp, true));
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldId);
                PlayerSettings.productName = oldName; PlayerSettings.bundleVersion = oldVersion;
                PlayerSettings.Android.bundleVersionCode = oldCode;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, oldBackend);
                PlayerSettings.Android.targetArchitectures = oldArchitectures;
                EditorUserBuildSettings.buildAppBundle = oldBundle;
                try
                {
                    if (scenes.Any(x => x.isLoaded && x.isActive && !string.IsNullOrEmpty(x.path)))
                        EditorSceneManager.RestoreSceneManagerSetup(scenes);
                    else if (File.Exists(RebuildBuild.ScenePath)) EditorSceneManager.OpenScene(RebuildBuild.ScenePath);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                finally
                {
                    if (oldResolverCache != null) File.WriteAllBytes(resolverCache, oldResolverCache);
                    else if (File.Exists(resolverCache)) File.Delete(resolverCache);
                    AssetDatabase.SaveAssets();
                }
            }
            Debug.Log("BONK_ACCEPTANCE_BUILD_PASS: " + buildId);
        }
        [Serializable] sealed class AcceptanceStamp
        {
            public string BuildId, BuiltAtUtc, ApplicationId, ApkSha256, RuleSha256, RunnerSha256;
            public string GameplaySha256, SessionSha256, PersistenceSha256, RewardsSha256;
            public string Define = "BONK_DEVICE_ACCEPTANCE";
            public string Scope = "Controlled test-only fixtures; no SDK ad playback or natural completion claim.";
        }
        static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string SourceHash(string directory)
        {
            string joined = string.Join("\n", Directory.GetFiles(directory, "*.cs").OrderBy(x => x, StringComparer.Ordinal)
                .Select(x => x.Replace('\\', '/') + ":" + Hash(x)));
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(joined))).Replace("-", "").ToLowerInvariant();
        }
    }
}
