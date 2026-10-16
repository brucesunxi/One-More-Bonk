#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using OneMoreBonk.Rebuild.Ads;

namespace OneMoreBonk.Rebuild.Editor
{
    public static class RebuildAdsSetup
    {
        [MenuItem("One More Bonk Rebuild/Ads/Configure Google Official Android Test")]
        public static void ConfigureOfficialTest()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings", false)).FirstOrDefault(t => t != null);
            var asset = type?.GetMethod("LoadInstance", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null) as UnityEngine.Object;
            if (asset == null) throw new InvalidOperationException("Google Mobile Ads 11.6.0 settings are not loaded.");
            var serialized = new SerializedObject(asset);
            SetString(serialized, "adMobAndroidAppId", RebuildRewardedAds.AndroidTestAppId);
            SetString(serialized, "adMobIOSAppId", "");
            SetBool(serialized, "overrideDefaultGmaAndroidSdk", true);
            var sdk = serialized.FindProperty("selectedGmaAndroidSdk");
            if (sdk == null) throw new InvalidOperationException("Google Standard SDK selection is missing.");
            sdk.intValue = 0;
            SetBool(serialized, "enableExternalDependencyManager", true);
            SetBool(serialized, "edmPackageDetected", true);
            SetString(serialized, "addedEdmVersion", "2.1.0");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
            Debug.Log("REBUILD_ADS_OFFICIAL_TEST_CONFIGURED: GMA11.6.0 Standard Android; EDM2.1.0; public test App ID only; no production revenue configuration.");
        }
        static void SetString(SerializedObject target, string name, string value)
        {
            var field = target.FindProperty(name);
            if (field == null) throw new InvalidOperationException("GMA settings property missing: " + name);
            field.stringValue = value;
        }
        static void SetBool(SerializedObject target, string name, bool value)
        {
            var field = target.FindProperty(name);
            if (field == null) throw new InvalidOperationException("GMA settings property missing: " + name);
            field.boolValue = value;
        }
        [MenuItem("One More Bonk Rebuild/Ads/Configure And Resolve Google Official Android Test")]
        public static void ConfigureOfficialTestAndResolve()
        {
            ConfigureOfficialTest();
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("GooglePlayServices.PlayServicesResolver", false)).FirstOrDefault(t => t != null);
            var resolve = type?.GetMethod("ResolveSync", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
            if (resolve == null) throw new InvalidOperationException("EDM2.1.0 ResolveSync(bool) is not loaded.");
            object result;
            try { result = resolve.Invoke(null, new object[] { true }); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
            if (result is bool success && !success) throw new InvalidOperationException("Android native dependency resolution failed.");
            AssetDatabase.SaveAssets();
            Debug.Log("REBUILD_ADS_TEST_RESOLVED: resolver returned; build, network test video, earned callback and cold restart still require validation.");
        }
    }
}
#endif
