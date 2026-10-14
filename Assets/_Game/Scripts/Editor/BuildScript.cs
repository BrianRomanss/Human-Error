using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HumanError.EditorTools
{
    /// <summary>
    /// Player builds: the full game and the demo (M7), for Windows and Android. Batchmode, e.g.
    /// Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod HumanError.EditorTools.BuildScript.BuildWindows
    /// Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod HumanError.EditorTools.BuildScript.BuildAndroidDemo
    /// Every build stamps its version and time into Resources/build_stamp.txt (shown on the title and in the credits)
    /// and uses the generated app icon. The demo compiles with HUMANERROR_DEMO, is named "Human Error Demo" and has
    /// its own Android id, so it installs and saves beside the full game.
    /// </summary>
    public static class BuildScript
    {
        /// <summary>The game's version. Android's versionCode is derived from it (0.7.0 → 700).</summary>
        public const string Version = "0.7.0";
        public const string DemoDefine = "HUMANERROR_DEMO";

        public const string WindowsOutput = "Builds/Windows/HumanError.exe";
        public const string WindowsDemoOutput = "Builds/WindowsDemo/HumanErrorDemo.exe";
        public const string AndroidOutput = "Builds/Android/HumanError.apk";
        public const string AndroidDemoOutput = "Builds/AndroidDemo/HumanErrorDemo.apk";
        public const string AndroidId = "com.chuchugames.humanerror";

        const string IconPath = "Assets/_Game/Art/Generated/Icon/icon.png";
        const string StampPath = "Assets/_Game/Resources/build_stamp.txt";

        [MenuItem("Tools/Human Error/Build Windows Player")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, WindowsOutput, demo: false);

        [MenuItem("Tools/Human Error/Build Windows Demo")]
        public static void BuildWindowsDemo() => Build(BuildTarget.StandaloneWindows64, WindowsDemoOutput, demo: true);

        /// <summary>
        /// A sideloadable APK: IL2CPP, ARM64, debug-signed (no keystore here), portrait or landscape following the
        /// phone. The game draws under the notch and keeps its UI inside Screen.safeArea itself.
        /// </summary>
        [MenuItem("Tools/Human Error/Build Android APK")]
        public static void BuildAndroid()
        {
            ConfigureAndroid();
            Build(BuildTarget.Android, AndroidOutput, demo: false);
        }

        [MenuItem("Tools/Human Error/Build Android Demo APK")]
        public static void BuildAndroidDemo()
        {
            ConfigureAndroid();
            Build(BuildTarget.Android, AndroidDemoOutput, demo: true);
        }

        static void ConfigureAndroid()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false;
        }

        /// <summary>0.7.0 → 700; 1.2.3 → 10203.</summary>
        public static int VersionCode(string version)
        {
            var parts = version.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).Concat(new[] { 0, 0, 0 }).Take(3).ToArray();
            return Math.Max(1, parts[0] * 10000 + parts[1] * 100 + parts[2]);
        }

        static void Build(BuildTarget target, string output, bool demo)
        {
            PlayerSettings.companyName = "Chuchu Games";
            PlayerSettings.productName = demo ? "Human Error Demo" : "Human Error";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = VersionCode(Version);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, demo ? AndroidId + ".demo" : AndroidId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AndroidId);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            else Debug.LogWarning($"[BuildScript] no icon at {IconPath}: run Tools/ArtGen first");

            string edition = demo ? "Demo" : "Full";
            File.WriteAllText(StampPath, $"v{Version} · build {DateTime.Now:yyyy-MM-dd HH:mm}");
            AssetDatabase.ImportAsset(StampPath, ImportAssetOptions.ForceUpdate);

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || scenes.All(s => !s.StartsWith(SceneBuilder.ScenesFolder)))
            {
                SceneBuilder.BuildAll();
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            }

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = target,
                    options = BuildOptions.None,
                    extraScriptingDefines = demo ? new[] { DemoDefine } : null,
                });
            }
            finally
            {
                // The project itself stays the full game.
                PlayerSettings.productName = "Human Error";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidId);
            }

            var summary = report.summary;
            Debug.Log($"[BuildScript] {target} {edition} v{Version} {summary.result}: {summary.outputPath} " +
                      $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors)");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
