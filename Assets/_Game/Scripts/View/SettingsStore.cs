using System.IO;
using ChuchuGames.UI;
using HumanError.Model;
using UnityEngine;

namespace HumanError.View
{
    /// <summary>
    /// The player's <see cref="Settings"/> in settings.json next to the save slots, and applying them to the UI
    /// kit (text scale, reduce motion), the palette, audio volumes and the window mode.
    /// </summary>
    public static class SettingsStore
    {
        static Settings _current;

        static string FilePath => Path.Combine(SaveStore.Folder, "settings.json");

        public static Settings Current => _current ??= Load();

        static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath)) return (JsonUtility.FromJson<Settings>(File.ReadAllText(FilePath)) ?? new Settings()).Sanitised();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Settings] unreadable, using defaults: {e.Message}");
            }
            return new Settings();
        }

        public static void Save(Settings s)
        {
            _current = s.Sanitised();
            try
            {
                Directory.CreateDirectory(SaveStore.Folder);
                File.WriteAllText(FilePath, JsonUtility.ToJson(_current, prettyPrint: true));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Settings] couldn't save: {e.Message}");
            }
            Apply();
        }

        /// <summary>Pushes the current settings into the UI kit, palette and audio. Screens built afterwards pick them up.</summary>
        public static void Apply()
        {
            var s = Current;
            // Debug flags force options for screenshots without touching the player's file.
            UiBuild.TextScale = s.TextScale;
            Palette.Colourblind = s.colourblind || LaunchScreenshot.HasFlag("--he-colourblind");
            Tween.ReduceMotion = s.reduceMotion;
            VisitorView.TellAssist = s.tellAssist || LaunchScreenshot.HasFlag("--he-tell-assist");
            AudioPlayer.Master = s.master;
            AudioPlayer.Music = s.music;
            AudioPlayer.Sfx = s.sfx;
            AudioPlayer.ApplyMusicVolume();
            if (!Application.isEditor && !Application.isMobilePlatform && LaunchScreenshot.Arg("--screenshot") == null && Screen.fullScreen != s.fullscreen)
                Screen.fullScreen = s.fullscreen;
        }
    }
}
