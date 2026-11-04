using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

namespace ColourCascade.EditorTools
{
    /// <summary>
    /// Switches the SDK integrations (Scripts/Integrations) on when their SDKs are installed (docs/MONETISATION.md §6).
    /// Each integration assembly compiles only with its scripting define: CC_ADMOB once the Google Mobile Ads plugin
    /// (with its consent SDK) is found, CC_UNITY_IAP once Unity IAP 5 or later is found. Without them the game builds
    /// and runs with no adverts or purchases. If an integration ever fails to compile against a newer SDK, the menu
    /// Colour Cascade > Monetisation can turn it off. This assembly depends on nothing, so it always compiles.
    /// </summary>
    [InitializeOnLoad]
    static class MonetisationSdkSetup
    {
        const string AdMobDefine = "CC_ADMOB";
        const string IapDefine = "CC_UNITY_IAP";
        const string Menu = "Colour Cascade/Monetisation/";
        const string OpenUpmUrl = "https://package.openupm.com";

        static string OffKey(string define) => "ColourCascade.Monetisation.Off." + define + "." + Application.dataPath;

        static MonetisationSdkSetup()
        {
            EditorApplication.delayCall += Sync;
        }

        // Which SDKs are installed comes from Unity's package version defines (this assembly's Version Defines), plus
        // the folder Google's .unitypackage installs into, for projects that imported the plugin that way. (Scanning
        // the loaded assemblies would also work, but Unity warns against it: UAC0005.)
        static bool AdMobInstalled
        {
            get
            {
#if COLOURCASCADE_ADMOB_PACKAGE
                return true;
#else
                return AssetDatabase.IsValidFolder("Assets/GoogleMobileAds");
#endif
            }
        }

        static bool IapInstalled
        {
            get
            {
#if COLOURCASCADE_IAP5_PACKAGE
                return true;
#else
                return false;
#endif
            }
        }

        static bool OlderIapInstalled
        {
            get
            {
#if COLOURCASCADE_IAP_PACKAGE && !COLOURCASCADE_IAP5_PACKAGE
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Adds or removes the defines to match what is installed (and what the developer switched off).</summary>
        static void Sync()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            bool admob = AdMobInstalled && !EditorPrefs.GetBool(OffKey(AdMobDefine), false);
            bool iap = IapInstalled && !EditorPrefs.GetBool(OffKey(IapDefine), false);
            if (OlderIapInstalled)
                Debug.LogWarning("[Colour Cascade] Unity IAP is installed but older than version 5. Update it (Window > Package Manager) to enable purchases; see docs/MONETISATION.md.");
            bool changed = false;
            foreach (var target in Targets)
            {
                changed |= SetDefine(target, AdMobDefine, admob);
                changed |= SetDefine(target, IapDefine, iap);
            }
            if (changed)
                Debug.Log("[Colour Cascade] Monetisation integrations: AdMob " + (admob ? "on" : "off") + ", Unity IAP " + (iap ? "on" : "off") + ". Unity will recompile.");
        }

        static readonly BuildTargetGroup[] Targets = { BuildTargetGroup.Android, BuildTargetGroup.iOS, BuildTargetGroup.Standalone };

        static bool SetDefine(BuildTargetGroup group, string define, bool on)
        {
            string current = GetDefines(group);
            var parts = new System.Collections.Generic.List<string>(current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            bool has = parts.Contains(define);
            if (has == on) return false;
            if (on) parts.Add(define);
            else parts.RemoveAll(p => p == define);
            SetDefines(group, string.Join(";", parts.ToArray()));
            return true;
        }

        static string GetDefines(BuildTargetGroup group)
        {
#if UNITY_2021_2_OR_NEWER
            return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group)) ?? "";
#else
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(group) ?? "";
#endif
        }

        static void SetDefines(BuildTargetGroup group, string defines)
        {
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(group), defines);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
#endif
        }

        // ------------------------------------------------------------------ Menu

        [MenuItem(Menu + "Install Google Mobile Ads (AdMob)", false, 300)]
        static void InstallAdMob()
        {
            if (!AddOpenUpmRegistry()) return;
            Install("com.google.ads.mobile");
        }

        [MenuItem(Menu + "Install Unity IAP", false, 301)]
        static void InstallIap()
        {
            Install("com.unity.purchasing");
        }

        [MenuItem(Menu + "Turn AdMob integration off", false, 320)]
        static void ToggleAdMob() => Toggle(AdMobDefine);

        [MenuItem(Menu + "Turn AdMob integration off", true)]
        static bool ToggleAdMobCheck()
        {
            UnityEditor.Menu.SetChecked(Menu + "Turn AdMob integration off", EditorPrefs.GetBool(OffKey(AdMobDefine), false));
            return true;
        }

        [MenuItem(Menu + "Turn Unity IAP integration off", false, 321)]
        static void ToggleIap() => Toggle(IapDefine);

        [MenuItem(Menu + "Turn Unity IAP integration off", true)]
        static bool ToggleIapCheck()
        {
            UnityEditor.Menu.SetChecked(Menu + "Turn Unity IAP integration off", EditorPrefs.GetBool(OffKey(IapDefine), false));
            return true;
        }

        [MenuItem(Menu + "Show status", false, 340)]
        static void Status()
        {
            Debug.Log("[Colour Cascade] Google Mobile Ads + UMP: " + (AdMobInstalled ? "installed" : "not installed") +
                      (EditorPrefs.GetBool(OffKey(AdMobDefine), false) ? " (integration switched off)" : "") +
                      "; Unity IAP 5: " + (IapInstalled ? "installed" : "not installed") +
                      (EditorPrefs.GetBool(OffKey(IapDefine), false) ? " (integration switched off)" : "") +
                      "; Android defines: " + GetDefines(BuildTargetGroup.Android));
        }

        [MenuItem(Menu + "Open setup guide", false, 341)]
        static void Guide()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/MONETISATION.md"));
            if (File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
            else Debug.Log("[Colour Cascade] See docs/MONETISATION.md in the repository.");
        }

        static void Toggle(string define)
        {
            bool off = !EditorPrefs.GetBool(OffKey(define), false);
            EditorPrefs.SetBool(OffKey(define), off);
            Sync();
        }

        static AddRequest request;

        static void Install(string package)
        {
            if (request != null)
            {
                Debug.LogWarning("[Colour Cascade] Another package is still installing.");
                return;
            }
            Debug.Log("[Colour Cascade] Installing " + package + " (latest version compatible with this editor)...");
            request = Client.Add(package);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (request == null || !request.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (request.Status == StatusCode.Success)
                Debug.Log("[Colour Cascade] Installed " + request.Result.packageId + ". Next steps: docs/MONETISATION.md §6.");
            else
                Debug.LogError("[Colour Cascade] Install failed" + (request.Error != null ? ": " + request.Error.message : "") + ". Install it from Window > Package Manager instead (docs/MONETISATION.md §6).");
            request = null;
            EditorApplication.delayCall += Sync;
        }

        /// <summary>
        /// Google distributes the Unity plugin through the OpenUPM registry (with its External Dependency Manager). Adds
        /// that registry to Packages/manifest.json once, scoped to Google's two packages only.
        /// </summary>
        static bool AddOpenUpmRegistry()
        {
            string manifest = Path.GetFullPath(Path.Combine(Application.dataPath, "../Packages/manifest.json"));
            if (!File.Exists(manifest))
            {
                Debug.LogError("[Colour Cascade] Packages/manifest.json not found.");
                return false;
            }
            string text = File.ReadAllText(manifest);
            if (text.Contains(OpenUpmUrl)) return true;
            if (text.Contains("\"scopedRegistries\""))
            {
                EditorUtility.DisplayDialog("Add the OpenUPM registry",
                    "This project already has scoped registries. Add OpenUPM (" + OpenUpmUrl + ") with the scopes com.google.ads.mobile and " +
                    "com.google.external-dependency-manager in Project Settings > Package Manager, then run this again.", "OK");
                return false;
            }
            int brace = text.IndexOf('{');
            if (brace < 0) return false;
            string registry = "\n  \"scopedRegistries\": [\n    {\n      \"name\": \"package.openupm.com\",\n      \"url\": \"" + OpenUpmUrl + "\",\n" +
                              "      \"scopes\": [\n        \"com.google.ads.mobile\",\n        \"com.google.external-dependency-manager\"\n      ]\n    }\n  ],";
            File.WriteAllText(manifest, text.Substring(0, brace + 1) + registry + text.Substring(brace + 1));
            Debug.Log("[Colour Cascade] Added the OpenUPM registry (Google packages only) to Packages/manifest.json.");
            return true;
        }
    }
}
