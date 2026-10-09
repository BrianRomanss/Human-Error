using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ChuchuGames.UI
{
    /// <summary>
    /// Automated screenshots for CI and tooling. Launch a player with
    /// <c>--screenshot &lt;path.png&gt; [--screenshot-frames N]</c>; it waits N frames (default 30),
    /// saves the PNG and quits. Does nothing without the flag.
    /// </summary>
    public sealed class LaunchScreenshot : MonoBehaviour
    {
        string _path;
        int _frames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var path = Arg("--screenshot");
            if (string.IsNullOrEmpty(path)) return;
            // Automated runs often start without window focus; a paused player would never reach the capture frame.
            Application.runInBackground = true;
            var go = new GameObject(nameof(LaunchScreenshot));
            DontDestroyOnLoad(go);
            var c = go.AddComponent<LaunchScreenshot>();
            c._path = Path.GetFullPath(path);
            c._frames = int.TryParse(Arg("--screenshot-frames"), out var n) ? n : 30;
        }

        IEnumerator Start()
        {
            for (int i = 0; i < _frames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(_path, tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log($"[LaunchScreenshot] Saved {_path}");
            Application.Quit();
        }

        /// <summary>Value after a command-line flag, or null.</summary>
        public static string Arg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }

        public static bool HasFlag(string flag) => Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0;
    }
}
