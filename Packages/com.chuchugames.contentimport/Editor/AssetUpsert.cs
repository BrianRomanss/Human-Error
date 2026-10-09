using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ChuchuGames.ContentImport.Editor
{
    /// <summary>What an import did, for logs and editor windows.</summary>
    public sealed class ImportReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Unchanged = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool Ok => Errors.Count == 0;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Import {(Ok ? "OK" : "FAILED")}: {Created.Count} created, {Updated.Count} updated, {Unchanged.Count} unchanged");
            foreach (var e in Errors) sb.Append("\n  ERROR ").Append(e);
            foreach (var w in Warnings) sb.Append("\n  warning ").Append(w);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Creates or updates ScriptableObject assets keyed by id (file name = id). Updating keeps the
    /// asset's GUID, so references from scenes and other assets survive re-imports.
    /// </summary>
    public static class AssetUpsert
    {
        /// <param name="folder">Project-relative folder, e.g. "Assets/_Game/Data/Guests". Created if missing.</param>
        /// <param name="apply">Writes the imported values into the asset.</param>
        public static T Upsert<T>(string folder, string id, Action<T> apply, ImportReport report) where T : ScriptableObject
        {
            EnsureFolder(folder);
            var path = $"{folder}/{Sanitize(id)}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            bool isNew = asset == null;
            if (isNew)
            {
                asset = ScriptableObject.CreateInstance<T>();
                apply(asset);
                AssetDatabase.CreateAsset(asset, path);
                report?.Created.Add(path);
                return asset;
            }

            var before = EditorJsonUtility.ToJson(asset);
            apply(asset);
            if (EditorJsonUtility.ToJson(asset) == before)
            {
                report?.Unchanged.Add(path);
            }
            else
            {
                EditorUtility.SetDirty(asset);
                report?.Updated.Add(path);
            }
            return asset;
        }

        /// <summary>Assets of type T in the folder whose ids are not in <paramref name="keep"/>. Reported, never deleted.</summary>
        public static List<string> FindOrphans<T>(string folder, ICollection<string> keep) where T : ScriptableObject
        {
            var orphans = new List<string>();
            if (!AssetDatabase.IsValidFolder(folder)) return orphans;
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!keep.Contains(Path.GetFileNameWithoutExtension(path))) orphans.Add(path);
            }
            return orphans;
        }

        public static void EnsureFolder(string folder)
        {
            // Check the disk too: inside StartAssetEditing, IsValidFolder doesn't see folders created
            // moments ago, and CreateFolder would then make "Guests 1", "Guests 2", …
            if (AssetDatabase.IsValidFolder(folder) || Directory.Exists(folder)) return;
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static string Sanitize(string id)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
            return id;
        }
    }
}
