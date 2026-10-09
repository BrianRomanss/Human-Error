using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HumanError.EditorTools
{
    /// <summary>
    /// Player builds. Batchmode:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod HumanError.EditorTools.BuildScript.BuildWindows
    /// </summary>
    public static class BuildScript
    {
        public const string WindowsOutput = "Builds/Windows/HumanError.exe";

        [MenuItem("Tools/Human Error/Build Windows Player")]
        public static void BuildWindows()
        {
            PlayerSettings.companyName = "Chuchu Games";
            PlayerSettings.productName = "Human Error";
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || scenes.All(s => !s.StartsWith(SceneBuilder.ScenesFolder)))
            {
                SceneBuilder.BuildAll();
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WindowsOutput,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[BuildScript] {summary.result}: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors)");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
