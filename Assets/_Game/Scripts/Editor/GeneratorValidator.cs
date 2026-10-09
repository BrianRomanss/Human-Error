using System.Collections.Generic;
using System.Linq;
using System.Text;
using HumanError.Data;
using HumanError.Model;
using UnityEditor;
using UnityEngine;

namespace HumanError.EditorTools
{
    /// <summary>
    /// The M2 gate (GDD §12: "1,000 generated visitors, 0 unsolvable"): generates many seeds of every shift in
    /// the catalog and runs <see cref="Fairness.Problems"/> on each visitor. Prints one PASS/FAIL line per shift
    /// plus how often each tell appears, so designers can see a shift's flavour.
    /// Batchmode: -executeMethod HumanError.EditorTools.GeneratorValidator.ValidateBatch
    /// </summary>
    public static class GeneratorValidator
    {
        public const int SeedsPerShift = 200;

        [MenuItem("Tools/Human Error/Validate Generator")]
        public static void ValidateMenu()
        {
            bool ok = Validate(out var log);
            if (ok) Debug.Log(log);
            else Debug.LogError(log);
        }

        public static void ValidateBatch()
        {
            bool ok = Validate(out var log);
            System.Console.WriteLine(log);
            Debug.Log(log);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Validate(out string log)
        {
            var sb = new StringBuilder();
            var catalog = AssetDatabase.LoadAssetAtPath<ContentCatalogSO>(ContentImporter.CatalogPath);
            if (catalog == null)
            {
                log = $"FAIL no catalog at {ContentImporter.CatalogPath} — run Import Content first";
                return false;
            }
            var plans = catalog.Load(out var errors);
            if (errors.Count > 0)
            {
                log = "FAIL content errors:\n" + string.Join("\n", errors);
                return false;
            }

            bool ok = true;
            int total = 0;
            foreach (var plan in plans)
            {
                int visitors = 0, bad = 0;
                var firstProblems = new List<string>();
                var tellCounts = new Dictionary<Tell, int>();
                for (ulong seed = 0; seed < SeedsPerShift; seed++)
                {
                    var session = plan.NewSession(seed);
                    foreach (var v in session.Queue)
                    {
                        visitors++;
                        foreach (var t in v.Truth.Tells) tellCounts[t] = (tellCounts.TryGetValue(t, out var c) ? c : 0) + 1;
                        var problems = Fairness.Problems(v, session.Context);
                        if (problems.Count == 0) continue;
                        bad++;
                        if (firstProblems.Count < 3) firstProblems.Add($"seed {seed} #{v.Index} {v.Truth.Kind}: {string.Join("; ", problems)}");
                    }
                }
                total += visitors;
                ok &= bad == 0;
                var top = string.Join(", ", tellCounts.OrderByDescending(kv => kv.Value).Take(6).Select(kv => $"{kv.Key} {kv.Value}"));
                sb.AppendLine($"{(bad == 0 ? "PASS" : "FAIL")} shift {plan.Data.number} \"{plan.Data.title}\": {visitors} visitors, {bad} unsolvable · tells: {top}");
                foreach (var p in firstProblems) sb.AppendLine("     " + p);
            }
            sb.AppendLine($"{(ok ? "PASS" : "FAIL")} {total} visitors across {plans.Count} shifts");
            log = sb.ToString();
            return ok;
        }
    }
}
