using System.IO;
using System.Linq;
using HumanError.Data;
using HumanError.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HumanError.EditorTools
{
    /// <summary>
    /// Builds scenes from code so they are reproducible. Menu: Tools/Human Error/Build Scenes.
    /// Batchmode: -executeMethod HumanError.EditorTools.SceneBuilder.BuildAll
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenesFolder = "Assets/_Game/Scenes";
        public const string MainScene = ScenesFolder + "/Main.unity";

        [MenuItem("Tools/Human Error/Build Scenes")]
        public static void BuildAll()
        {
            BuildMain();
        }

        public static void BuildMain()
        {
            Directory.CreateDirectory(ScenesFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.MossNight;
            cam.transform.position = new Vector3(0, 0, -10);

            var flow = new GameObject("GameFlow").AddComponent<GameFlow>();
            flow.Art = AssetDatabase.LoadAssetAtPath<ArtLibrarySO>(ArtGenMenu.LibraryPath);
            flow.Catalog = AssetDatabase.LoadAssetAtPath<ContentCatalogSO>(ContentImporter.CatalogPath);
            if (flow.Catalog == null)
                Debug.LogWarning($"[SceneBuilder] No catalog at {ContentImporter.CatalogPath}; run Import Content first.");
            if (flow.Art == null)
                Debug.LogWarning($"[SceneBuilder] No art library at {ArtGenMenu.LibraryPath}; run Generate Placeholder Art first.");

            EditorSceneManager.SaveScene(scene, MainScene);
            SetBuildScenes(MainScene);
            Debug.Log($"[SceneBuilder] Saved {MainScene}");
        }

        /// <summary>Only the game's own scenes ship; template scenes (SampleScene) and removed scenes are dropped.</summary>
        static void SetBuildScenes(params string[] paths)
        {
            var scenes = paths.Select(p => new EditorBuildSettingsScene(p, true))
                .Concat(EditorBuildSettings.scenes.Where(s =>
                    !paths.Contains(s.path) && s.path.StartsWith(ScenesFolder + "/") && File.Exists(s.path)))
                .ToArray();
            EditorBuildSettings.scenes = scenes;
        }
    }
}
