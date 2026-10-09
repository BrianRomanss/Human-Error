using System.IO;
using System.Linq;
using ChuchuGames.ContentImport.Editor;
using HumanError.Data;
using HumanError.EditorTools.ArtGen;
using UnityEditor;
using UnityEngine;

namespace HumanError.EditorTools
{
    /// <summary>
    /// Tools/Human Error/Generate Placeholder Art: writes art + audio, then links everything under Art/ and
    /// Audio/ into the ArtLibrary asset. "Link Art Library" alone re-links after you swap in final files.
    /// Batchmode: -executeMethod HumanError.EditorTools.ArtGenMenu.GenerateBatch (or LinkBatch).
    /// </summary>
    public static class ArtGenMenu
    {
        public const string ArtRoot = "Assets/_Game/Art";
        public const string AudioRoot = "Assets/_Game/Audio";
        public const string DataFolder = "Assets/_Game/Data";
        public const string LibraryPath = DataFolder + "/ArtLibrary.asset";

        [MenuItem("Tools/Human Error/Generate Placeholder Art + Audio")]
        public static void Generate()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Human Error", "Generating art…", 0.2f);
                HumanErrorArt.GenerateAll(Path.Combine(ArtRoot, "Generated"));
                EditorUtility.DisplayProgressBar("Human Error", "Generating audio…", 0.7f);
                HumanErrorAudio.GenerateAll(Path.Combine(AudioRoot, "Generated"));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh();
            Link();
        }

        public static void GenerateBatch()
        {
            Generate();
            EditorApplication.Exit(0);
        }

        public static void LinkBatch()
        {
            Link();
            EditorApplication.Exit(0);
        }

        [MenuItem("Tools/Human Error/Link Art Library")]
        public static void Link()
        {
            Directory.CreateDirectory(DataFolder);
            var report = new ImportReport();
            AssetUpsert.Upsert<ArtLibrarySO>(DataFolder, "ArtLibrary", lib =>
            {
                lib.sprites = AssetDatabase.FindAssets("t:Sprite", new[] { ArtRoot })
                    .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(s => s != null).OrderBy(s => s.name).ToArray();
                lib.clips = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot })
                    .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(c => c != null).OrderBy(c => c.name).ToArray();
                lib.title = AssetDatabase.LoadAssetAtPath<Font>(ArtRoot + "/Fonts/ChakraPetch-Bold.ttf");
                lib.ui = AssetDatabase.LoadAssetAtPath<Font>(ArtRoot + "/Fonts/AtkinsonHyperlegible-Regular.ttf");
                lib.uiBold = AssetDatabase.LoadAssetAtPath<Font>(ArtRoot + "/Fonts/AtkinsonHyperlegible-Bold.ttf");
                lib.mono = AssetDatabase.LoadAssetAtPath<Font>(ArtRoot + "/Fonts/IBMPlexMono-Medium.ttf");
            }, report);
            AssetDatabase.SaveAssets();
            var l = AssetDatabase.LoadAssetAtPath<ArtLibrarySO>(LibraryPath);
            string F(Font f) => f != null ? f.name : "MISSING";
            Debug.Log($"[ArtLibrary] {l.sprites.Length} sprites, {l.clips.Length} clips, fonts: {F(l.title)}/{F(l.ui)}/{F(l.uiBold)}/{F(l.mono)}");
        }
    }
}
