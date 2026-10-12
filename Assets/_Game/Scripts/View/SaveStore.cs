using System.IO;
using ChuchuGames.Core;
using ChuchuGames.UI;
using HumanError.Model;
using UnityEngine;

namespace HumanError.View
{
    /// <summary>
    /// The three save slots (GDD §7.2) as JSON files under persistentDataPath/saves, or the folder given by
    /// --he-save-dir (automated runs must not touch the player's saves).
    /// </summary>
    public static class SaveStore
    {
        public const int SlotCount = 3;

        static SaveSlots<SaveData> _slots;

        public static SaveSlots<SaveData> Slots => _slots ??= new SaveSlots<SaveData>(Folder,
            s => JsonUtility.ToJson(s, prettyPrint: true), JsonUtility.FromJson<SaveData>, SlotCount);

        public static string Folder => LaunchScreenshot.Arg("--he-save-dir") ?? Path.Combine(Application.persistentDataPath, "saves");

        static string ScoresPath => Path.Combine(Folder, "scores.json");

        /// <summary>Best scores for Endless and Daily (never null).</summary>
        public static Scores LoadScores()
        {
            try
            {
                if (File.Exists(ScoresPath)) return JsonUtility.FromJson<Scores>(File.ReadAllText(ScoresPath)) ?? new Scores();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveStore] scores unreadable, starting fresh: {e.Message}");
            }
            return new Scores();
        }

        public static void SaveScores(Scores scores)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(ScoresPath, JsonUtility.ToJson(scores, prettyPrint: true));
        }

        /// <summary>The most recently written slot, or 0 when there are no saves.</summary>
        public static int MostRecent()
        {
            int best = 0;
            System.DateTime when = System.DateTime.MinValue;
            for (int i = 1; i <= SlotCount; i++)
            {
                var t = Slots.LastWritten(i);
                if (t.HasValue && t.Value > when) { best = i; when = t.Value; }
            }
            return best;
        }
    }
}
