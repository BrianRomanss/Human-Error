using System.Collections.Generic;
using System.IO;
using HumanError.Model;
using HumanError.Model.Content;
using NUnit.Framework;

namespace HumanError.Tests
{
    /// <summary>Loads Assets/_Game/Content/content.json in Unity (JsonUtility) or .NET (System.Text.Json).</summary>
    public static class TestContent
    {
        static List<ShiftPlan> _plans;

        public static string ContentDir()
        {
#if UNITY_5_3_OR_NEWER
            return Path.Combine(UnityEngine.Application.dataPath, "_Game", "Content");
#else
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets"))) dir = dir.Parent;
            Assert.IsNotNull(dir, "Couldn't find the Unity project root");
            return Path.Combine(dir.FullName, "Assets", "_Game", "Content");
#endif
        }

        public static ContentFile File()
        {
            var json = System.IO.File.ReadAllText(Path.Combine(ContentDir(), "content.json"));
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.FromJson<ContentFile>(json);
#else
            return System.Text.Json.JsonSerializer.Deserialize<ContentFile>(json, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
#endif
        }

        public static List<ShiftPlan> Plans()
        {
            if (_plans != null) return _plans;
            _plans = ContentFactory.Load(File(), out var errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            return _plans;
        }

        public static ShiftPlan Plan(int number) => Plans().Find(p => p.Data.number == number);

        /// <summary>Shift 3: kesh, date, floor, hands, trap and pressure questions; 3 humans.</summary>
        public static ShiftContext Ctx(ulong seed = 1) => Plan(3).Context(seed);
        public static ShiftConfig Config() => Plan(3).Config;
        public static ShiftSession NewShift(ulong seed) => Plan(3).NewSession(seed);
    }
}
