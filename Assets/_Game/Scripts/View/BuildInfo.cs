using ChuchuGames.UI;
using HumanError.Model;
using UnityEngine;

namespace HumanError.View
{
    /// <summary>
    /// Which edition this build is and its version stamp (M7). The demo is compiled with the HUMANERROR_DEMO define
    /// (BuildScript's demo targets); <c>--he-demo</c> plays any build as the demo, for screenshots. The stamp is
    /// written by BuildScript to Resources/build_stamp.txt just before a build.
    /// </summary>
    public static class BuildInfo
    {
#if HUMANERROR_DEMO
        static Edition _edition = Edition.Demo;
#else
        static Edition _edition = Edition.Full;
#endif
        static string _stamp;

        public static Edition Edition => _edition;

        public static void ApplyDebugFlags()
        {
            if (LaunchScreenshot.HasFlag("--he-demo")) _edition = Edition.Demo;
        }

        /// <summary>e.g. "v0.7.0 · build 2026-10-13 15:12"; "v0.7.0 · dev" when run from the editor without a build.</summary>
        public static string Stamp
        {
            get
            {
                if (_stamp != null) return _stamp;
                var asset = Resources.Load<TextAsset>("build_stamp");
                _stamp = asset != null && !string.IsNullOrWhiteSpace(asset.text) ? asset.text.Trim() : $"v{Application.version} · dev";
                return _stamp;
            }
        }
    }
}
