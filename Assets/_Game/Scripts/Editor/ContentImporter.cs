using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChuchuGames.ContentImport.Editor;
using HumanError.Data;
using HumanError.Model.Content;
using UnityEditor;
using UnityEngine;

namespace HumanError.EditorTools
{
    /// <summary>
    /// content.json → QuestionSO / ShiftSO assets and the ContentCatalog (which also holds news, messages and upgrades). Everything is validated first
    /// (ContentFactory); nothing is written if there are errors. Re-imports keep GUIDs.
    /// Batchmode: -executeMethod HumanError.EditorTools.ContentImporter.ImportBatch
    /// </summary>
    public static class ContentImporter
    {
        public const string ContentFile = "Assets/_Game/Content/content.json";
        public const string DataFolder = ArtGenMenu.DataFolder;
        public const string CatalogPath = DataFolder + "/ContentCatalog.asset";

        [MenuItem("Tools/Human Error/Import Content (JSON → assets)")]
        public static void ImportMenu()
        {
            var report = Import();
            if (report.Ok) Debug.Log("[Import] " + report);
            else Debug.LogError("[Import] " + report);
        }

        public static void ImportBatch()
        {
            var report = Import();
            Debug.Log((report.Ok ? "Import OK " : "Import FAILED ") + report);
            EditorApplication.Exit(report.Ok ? 0 : 1);
        }

        public static ImportReport Import()
        {
            var report = new ImportReport();
            Model.Content.ContentFile file = null;
            if (!File.Exists(ContentFile)) report.Errors.Add($"Missing {ContentFile}");
            else
            {
                try { file = JsonUtility.FromJson<Model.Content.ContentFile>(File.ReadAllText(ContentFile)); }
                catch (System.Exception e) { report.Errors.Add($"{ContentFile}: {e.Message}"); }
            }
            if (!report.Ok) return report;

            ContentFactory.Load(file, out var errors);
            report.Errors.AddRange(errors);
            if (!report.Ok) return report;

            AssetUpsert.EnsureFolder(DataFolder + "/Questions");
            AssetUpsert.EnsureFolder(DataFolder + "/Shifts");
            AssetDatabase.StartAssetEditing();
            var questions = new List<QuestionSO>();
            var shifts = new List<ShiftSO>();
            try
            {
                foreach (var q in file.questions)
                    questions.Add(AssetUpsert.Upsert<QuestionSO>(DataFolder + "/Questions", q.id, a => a.data = q, report));
                foreach (var s in file.shifts.OrderBy(s => s.number))
                    shifts.Add(AssetUpsert.Upsert<ShiftSO>(DataFolder + "/Shifts", $"Shift{s.number:00}", a => a.data = s, report));
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetUpsert.Upsert<ContentCatalogSO>(DataFolder, "ContentCatalog", a =>
            {
                a.questions = questions.ToArray();
                a.shifts = shifts.ToArray();
                a.news = file.news ?? new NewsData[0];
                a.messages = file.messages ?? new MessageData[0];
                a.upgrades = file.upgrades ?? new UpgradeData[0];
                a.tutorial = file.tutorial ?? new TutorialData();
            }, report);
            AssetDatabase.SaveAssets();

            foreach (var orphan in AssetUpsert.FindOrphans<QuestionSO>(DataFolder + "/Questions", file.questions.Select(q => q.id).ToList()))
                report.Warnings.Add($"{orphan} is not in content.json (left untouched)");
            foreach (var orphan in AssetUpsert.FindOrphans<ShiftSO>(DataFolder + "/Shifts", file.shifts.Select(s => $"Shift{s.number:00}").ToList()))
                report.Warnings.Add($"{orphan} is not in content.json (left untouched)");
            return report;
        }
    }
}
