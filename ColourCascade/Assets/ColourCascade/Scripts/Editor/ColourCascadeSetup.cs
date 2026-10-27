using System.IO;
using ColourCascade.App;
using ColourCascade.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

namespace ColourCascade.EditorTools
{
    /// <summary>
    /// One-time project setup that runs automatically when the project is opened, so nothing has to be
    /// configured by hand: creates the config asset, registers the scene for builds and applies the
    /// portrait/Android player settings. Everything is also available from the "Colour Cascade" menu.
    /// </summary>
    [InitializeOnLoad]
    public static class ColourCascadeSetup
    {
        public const string ScenePath = "Assets/ColourCascade/Scenes/Main.unity";
        public const string ConfigPath = "Assets/ColourCascade/Resources/ColourCascadeConfig.asset";
        public const string ProductName = "Colour Cascade";
        public const string CompanyName = "Ryan Clapham";
        public const string BundleId = "com.ryanclapham.colourcascade";
        const string AppliedKey = "ColourCascade.PlayerSettingsApplied.v1";

        static ColourCascadeSetup()
        {
            EditorApplication.delayCall += RunAutomaticSetup;
        }

        static void RunAutomaticSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureConfigAsset();
            UpgradeConfigAsset();
            EnsureSceneInBuild();
            OpenMainSceneInsteadOfUntitled();
            if (PlayerSettings.productName != ProductName || !EditorPrefs.GetBool(AppliedKey + "." + Application.dataPath, false))
            {
                ApplyPlayerSettings();
                EditorPrefs.SetBool(AppliedKey + "." + Application.dataPath, true);
            }
        }

        /// <summary>
        /// Unity opens an empty "Untitled" scene when a project has no last-opened scene, which shows only a sky in the
        /// Game view. Once per editor session, swap an unsaved, unchanged Untitled scene for the game's scene.
        /// </summary>
        static void OpenMainSceneInsteadOfUntitled()
        {
            const string key = "ColourCascade.MainSceneChecked";
            if (UnityEditor.SessionState.GetBool(key, false)) return;
            UnityEditor.SessionState.SetBool(key, true);
            var active = EditorSceneManager.GetActiveScene();
            if (EditorSceneManager.sceneCount != 1 || !string.IsNullOrEmpty(active.path) || active.isDirty) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Colour Cascade/Open Main Scene", false, 0)]
        public static void OpenMainScene()
        {
            EnsureSceneInBuild();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Colour Cascade/Select Game Config", false, 1)]
        public static void SelectConfig()
        {
            EnsureConfigAsset();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<ColourCascadeConfig>(ConfigPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Colour Cascade/Reset Game Config To Defaults", false, 2)]
        public static void ResetConfig()
        {
            if (!EditorUtility.DisplayDialog("Reset config?", "Restore every tunable value (levels, prices, rewards...) to the built-in defaults?", "Reset", "Cancel")) return;
            if (File.Exists(ConfigPath)) AssetDatabase.DeleteAsset(ConfigPath);
            EnsureConfigAsset();
            SelectConfig();
        }

        [MenuItem("Colour Cascade/Apply Mobile Player Settings", false, 20)]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1") PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.runInBackground = false;
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
#else
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
#endif
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
            Debug.Log("[Colour Cascade] Applied player settings: portrait only, '" + ProductName + "', " + BundleId + ", Android IL2CPP ARM64.");
        }

        [MenuItem("Colour Cascade/Build/Android APK (Development)", false, 40)]
        public static void BuildDevelopmentApk()
        {
            Build(false, BuildOptions.Development);
        }

        [MenuItem("Colour Cascade/Build/Android App Bundle (Release)", false, 41)]
        public static void BuildReleaseBundle()
        {
            Build(true, BuildOptions.None);
        }

        static void Build(bool appBundle, BuildOptions options)
        {
            EnsureSceneInBuild();
            ApplyPlayerSettings();
            EditorUserBuildSettings.buildAppBundle = appBundle;
            Directory.CreateDirectory("Builds/Android");
            string path = "Builds/Android/ColourCascade" + (appBundle ? ".aab" : ".apk");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                options = options,
            });
            Debug.Log("[Colour Cascade] Build " + report.summary.result + ": " + path + " (" + report.summary.totalErrors + " errors)");
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Colour Cascade/Save Data/Open Save Folder", false, 60)]
        public static void OpenSaveFolder()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        [MenuItem("Colour Cascade/Save Data/Delete Save (start as a new player)", false, 61)]
        public static void DeleteSave()
        {
            if (!EditorUtility.DisplayDialog("Delete save?", "Erase all local progress for this project (coins, levels, settings)?", "Delete", "Cancel")) return;
            foreach (var name in new[] { "save.json", "save.json.tmp", "save.json.bak", "art-cache.bin" })
            {
                string p = Path.Combine(Application.persistentDataPath, name);
                if (File.Exists(p)) File.Delete(p);
            }
            Debug.Log("[Colour Cascade] Save data deleted.");
        }

        static void EnsureConfigAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<ColourCascadeConfig>(ConfigPath) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            var asset = ScriptableObject.CreateInstance<ColourCascadeConfig>();
            asset.rewardTuningVersion = EconomyTuning.Current;
            AssetDatabase.CreateAsset(asset, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Colour Cascade] Created " + ConfigPath + " with default values. Edit it to tune the game.");
        }

        /// <summary>Moves a config asset saved with older reward tuning to the current defaults (hand-edited values are kept).</summary>
        static void UpgradeConfigAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ColourCascadeConfig>(ConfigPath);
            if (asset == null || asset.rewardTuningVersion >= EconomyTuning.Current) return;
            var changed = EconomyTuning.Upgrade(asset.ToData(), asset.rewardTuningVersion);
            asset.rewardTuningVersion = EconomyTuning.Current;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[Colour Cascade] Updated the game config to reward tuning version " + EconomyTuning.Current +
                      (changed.Count > 0 ? " (" + string.Join(", ", changed) + ")." : "; your edited reward values were kept."));
        }

        static void EnsureSceneInBuild()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length > 0 && scenes[0].path == ScenePath && scenes[0].enabled) return;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Colour Cascade] Registered " + ScenePath + " as the build scene.");
        }
    }
}
