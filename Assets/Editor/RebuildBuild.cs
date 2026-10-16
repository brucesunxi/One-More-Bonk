using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OneMoreBonk.Rebuild.Editor
{
    public static class RebuildBuild
    {
        public const string ScenePath = "Assets/Scenes/RebuildWorkshop.unity";
        public const string Version = "0.1.0";
        public const int VersionCode = 5;

        [MenuItem("One More Bonk Rebuild/Create Independent Scene")]
        public static void Bootstrap()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Settings");
            const string pipelinePath = "Assets/Settings/RebuildPipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/Settings/RebuildRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.msaaSampleCount = 2;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int index = 0; index < QualitySettings.names.Length; index++)
            {
                QualitySettings.SetQualityLevel(index, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.vSyncCount = 0;
            PlayerSettings.companyName = "OneMoreBonk";
            PlayerSettings.productName = "One More Bonk Rebuild";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.brucesunxi.onemorebonk.rebuild");
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 1200;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var workshopType = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("OneMoreBonk.Rebuild.Presentation.RebuildWorkshop"))
                .FirstOrDefault(x => x != null);
            if (workshopType == null) throw new InvalidOperationException("The rebuilt presentation component has not compiled.");
            var root = new GameObject("Rebuild Workshop");
            root.AddComponent(workshopType);
            var adsType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
                .FirstOrDefault(x => x.Name == "RebuildRewardedAds" && typeof(MonoBehaviour).IsAssignableFrom(x));
            if (adsType != null) root.AddComponent(adsType);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save the independent rebuild scene.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("REBUILD_BOOTSTRAP_PASS: independent scene, product, application ID and URP settings created.");
        }

        [MenuItem("One More Bonk Rebuild/Validate Native State")]
        public static void ValidateNative()
        {
            string directory = Path.Combine(Path.GetTempPath(), "bonk-native-state-" + Guid.NewGuid().ToString("N"));
            try
            {
                var empty = RiskStateCodec.Copy(new RiskState());
                Require(empty.Run == null && empty.LastResult == null && empty.PendingReward == null, "Native inline-null presence regression.");
                var store = new RiskFileSaveStore(Path.Combine(directory, "player.json"));
                Require(RiskSession.TryOpen(store, 1234, out var session, out string reason), reason);
                Require(session.TryStart(out reason), reason);
                var run = session.Snapshot().Run;
                Require(run.Swords.Count == 3 && run.ShopOffers.Count == 3 && run.PendingBreak == null, "Native initialization mismatch.");
                string before = RiskStateCodec.Encode(session.Snapshot());
                session.GetProbabilities(run.SelectedId);
                Require(before == RiskStateCodec.Encode(session.Snapshot()), "Native probability preview mutated authority.");
                Require(session.TryBeginReward(RiskRewardPlacement.RelicSlot, out var request, out reason), reason);
                Require(RiskSession.TryOpen(store, 999, out var reopened, out reason), reason);
                Require(reopened.Snapshot().PendingReward.Id == request.Id && reopened.Snapshot().Profile.UnlockedRelicSlots == 2,
                    "Native pending reward or permanent entitlement did not survive restart.");
                Require(reopened.TryCancelReward(request.Id, out reason), reason);
                Require(reopened.TryStrike(out var strike, out reason), reason);
                Require(strike.Accepted && reopened.Snapshot().Run.TotalHits == 1, "Native first hammer did not commit.");
                Require(reopened.TryCheckpoint(out reason), reason);
                Require(RiskSession.TryOpen(store, 999, out var saved, out reason), reason);
                Require(saved.Snapshot().Run.TotalHits == 1 && saved.Snapshot().Profile.UnlockedRelicSlots == 2, "Native save/reopen mismatch.");
                var font = Resources.Load<Font>("RebuildArt/Fonts/NotoSansSC-Regular");
                Require(font != null && font.HasCharacter('剑'), "Packaged Chinese font is unavailable.");
                Debug.Log("REBUILD_NATIVE_STATE_PASS: JsonUtility presence, initialization, preview, pending reward, one-hit commit and file round-trip.");
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [MenuItem("One More Bonk Rebuild/Build Development Android")]
        public static void BuildAndroid()
        {
            ValidateNative();
            Bootstrap();
            var stamp = new BuildStamp {
                BuildId = Guid.NewGuid().ToString("N"), BuiltAtUtc = DateTime.UtcNow.ToString("O"),
                Version = Version, VersionCode = VersionCode,
                ApplicationId = "com.brucesunxi.onemorebonk.rebuild",
                CodeSha256 = HashFiles(Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)),
                RuleSha256 = HashFile("Assets/Resources/Risk/rule-data.json"),
                ArtSha256 = HashFiles(Directory.GetFiles("Assets/Resources/RebuildArt", "*", SearchOption.AllDirectories)),
                DependencyLockSha256 = HashFile("Packages/packages-lock.json")
            };
            File.WriteAllText("Assets/Resources/RebuildBuildInfo.json", JsonUtility.ToJson(stamp, true));
            AssetDatabase.ImportAsset("Assets/Resources/RebuildBuildInfo.json");
            string output = Path.GetFullPath("Builds/Android/OneMoreBonk-Rebuild-" + Version + "-dev.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.Android, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Rebuild Android build failed: " + report.summary.result);
            if (stamp.CodeSha256 != HashFiles(Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)) ||
                stamp.RuleSha256 != HashFile("Assets/Resources/Risk/rule-data.json") ||
                stamp.ArtSha256 != HashFiles(Directory.GetFiles("Assets/Resources/RebuildArt", "*", SearchOption.AllDirectories)) ||
                stamp.DependencyLockSha256 != HashFile("Packages/packages-lock.json"))
                throw new InvalidOperationException("Source changed during build; do not deliver this APK.");
            stamp.ApkSha256 = HashFile(output);
            File.WriteAllText(Path.ChangeExtension(output, ".build.json"), JsonUtility.ToJson(stamp, true));
            Debug.Log("REBUILD_ANDROID_BUILD_PASS: " + Path.GetFileName(output) + " bytes=" + new FileInfo(output).Length);
        }

        [Serializable] sealed class BuildStamp
        {
            public string BuildId, BuiltAtUtc, Version, ApplicationId, CodeSha256, RuleSha256, ArtSha256, DependencyLockSha256, ApkSha256;
            public int VersionCode;
        }
        static string HashFile(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string HashFiles(string[] files)
        {
            var content = string.Join("\n", files.OrderBy(x => x, StringComparer.Ordinal)
                .Select(path => path.Replace('\\', '/') + ":" + HashFile(path)));
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", "").ToLowerInvariant();
        }

        static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    }
}
