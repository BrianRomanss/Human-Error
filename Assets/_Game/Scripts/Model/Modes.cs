using System;
using System.Collections.Generic;
using System.Linq;
using ChuchuGames.Core;
using HumanError.Model.Content;

namespace HumanError.Model
{
    /// <summary>
    /// Score for Endless and Daily (GDD §8: "score = correct decisions × streak"). Every good decision scores
    /// <see cref="PerDecision"/> × the streak so far (capped); a mistake or a complaint resets the streak.
    /// </summary>
    public sealed class ScoreRun
    {
        public const int PerDecision = 10, MaxMultiplier = 10, StrikesAllowed = 3;

        public int Score, Streak, BestStreak, Correct, Decisions, Strikes;
        /// <summary>Points for every decision so far, in order (for the report).</summary>
        public readonly List<int> Points = new List<int>();
        /// <summary>Endless: three strikes end the run.</summary>
        public bool EndsOnStrikes;

        public bool Over => EndsOnStrikes && Strikes >= StrikesAllowed;
        public int Multiplier => Math.Max(1, Math.Min(Streak, MaxMultiplier));

        /// <summary>Books one decision; returns the points it earned.</summary>
        public int Add(Verdict v)
        {
            Decisions++;
            if (v.Strike) Strikes++;
            int points = 0;
            if (!v.IsGood || v.Complaint) Streak = 0;
            else
            {
                Correct++;
                Streak++;
                BestStreak = Math.Max(BestStreak, Streak);
                points = PerDecision * Multiplier;
                Score += points;
            }
            Points.Add(points);
            return points;
        }
    }

    /// <summary>Best scores for the modes, saved next to the save slots.</summary>
    [Serializable]
    public class Scores
    {
        public int endlessBest;
        public int endlessBestLevel;
        /// <summary>yyyy-MM-dd of the daily that <see cref="dailyBest"/> belongs to.</summary>
        public string dailyDate = "";
        public int dailyBest;
        public int dailyPlays;

        /// <summary>True when it's a new best.</summary>
        public bool RecordEndless(int score, int level)
        {
            if (score <= endlessBest) return false;
            endlessBest = score;
            endlessBestLevel = level;
            return true;
        }

        public int DailyBestFor(string day) => dailyDate == day ? dailyBest : 0;

        public bool RecordDaily(string day, int score)
        {
            if (dailyDate != day) { dailyDate = day; dailyBest = 0; dailyPlays = 0; }
            dailyPlays++;
            if (score <= dailyBest) return false;
            dailyBest = score;
            return true;
        }
    }

    /// <summary>
    /// Endless and Daily shifts (GDD §8: "shifts generated from a seed; a daily seed everyone plays"). They reuse
    /// an authored story shift as a base — its questions, tells and rules are already validated as fair — and
    /// randomise tonight's date, Kesh and closed floor, with more and better-disguised humans as levels climb.
    /// </summary>
    public static class Modes
    {
        public const int DailyVisitors = 12;

        public static string DayKey(DateTime date) => date.ToString("yyyy-MM-dd");

        /// <summary>The same for everyone on the same calendar day.</summary>
        public static ulong DailySeed(DateTime date) => (ulong)(date.Year * 10000 + date.Month * 100 + date.Day) * 2654435761UL;

        public static ulong EndlessSeed(ulong runSeed, int level) => runSeed * 1000003UL + (ulong)level;

        public static ShiftPlan Endless(GameContent game, int level, ulong runSeed, out List<string> errors)
        {
            level = Math.Max(1, level);
            var shifts = game.Shifts;
            int last = shifts.Max(p => p.Data.number);
            var basis = game.Shift(Math.Min(level, last)).Data;
            var rng = new SeededRandom(EndlessSeed(runSeed, level) ^ 0xE1D1E55UL);
            var d = Vary(basis, rng);
            int beyond = Math.Max(0, level - last);
            if (beyond > 0)
            {
                d.visitors = Math.Min(DailyVisitors, basis.visitors + beyond);
                d.humans = Math.Min(d.visitors / 2, basis.humans + (beyond + 1) / 2);
                d.skilledShare = Math.Min(0.8f, basis.skilledShare + 0.1f * beyond);
                d.oddCitizens = Math.Min(3, basis.oddCitizens + beyond / 3);
            }
            d.title = $"Endless · Level {level}";
            d.briefing = "Shift after shift, each harder than the last, until your third strike. " +
                         $"Every right call scores {ScoreRun.PerDecision} × your streak; one mistake resets the streak.";
            d.supervisor = level == 1 ? "No pay tonight. Just the score." : "Again. Faster. Fewer mistakes.";
            return Build(game, d, game.Home.ToolsBy(d.number), out errors);
        }

        public static ShiftPlan Daily(GameContent game, DateTime date, out List<string> errors)
        {
            var rng = new SeededRandom(DailySeed(date) ^ 0xDA11UL);
            // Bases from shift 3 on: enough questions and tells for a full twelve-visitor night.
            var candidates = game.Shifts.Where(p => p.Data.number >= 3).ToList();
            if (candidates.Count == 0) candidates = game.Shifts;
            var basis = rng.Pick(candidates).Data;
            var d = Vary(basis, rng);
            d.visitors = DailyVisitors;
            d.humans = 4;
            d.ruleBreakers = 2;
            d.oddCitizens = Math.Min(2, Math.Max(basis.oddCitizens, 1));
            d.skilledShare = 0.5f;
            d.title = $"Daily · {date:d MMM yyyy}";
            d.briefing = "Everyone at every Night Window gets these same twelve visitors today. " +
                         $"Every right call scores {ScoreRun.PerDecision} × your streak; one mistake resets the streak.";
            d.supervisor = "The whole Lattice is watching today's numbers.";
            return Build(game, d, game.Home.ToolsBy(d.number), out errors);
        }

        /// <summary>A copy of the base shift with tonight's date, Kesh and closed floor rolled from the seed.</summary>
        static ShiftData Vary(ShiftData b, SeededRandom rng)
        {
            var d = new ShiftData
            {
                number = b.number,
                cycle = rng.Range(400, 430),
                day = rng.Range(1, CycleDate.DaysPerCycle + 1),
                kesh = rng.Pick(Kesh.Colours),
                visitors = b.visitors,
                humans = b.humans,
                ruleBreakers = b.ruleBreakers,
                oddCitizens = b.oddCitizens,
                skilledShare = b.skilledShare,
                clumsyMinTells = b.clumsyMinTells,
                clumsyMaxTells = b.clumsyMaxTells,
                skilledTells = b.skilledTells,
                maxForgedFields = b.maxForgedFields,
                questionBudget = b.questionBudget,
                questions = (b.questions ?? new string[0]).ToArray(),
                tells = (b.tells ?? new string[0]).ToArray(),
                rules = (b.rules ?? new string[0]).ToArray(),
                tools = (b.tools ?? new string[0]).ToArray(),
                closedFloors = new int[0],
            };
            int closed = (b.closedFloors ?? new int[0]).Length;
            if (closed > 0) d.closedFloors = Floors.All.OrderBy(_ => rng.Value()).Take(closed).ToArray();
            return d;
        }

        static ShiftPlan Build(GameContent game, ShiftData d, IEnumerable<string> tools, out List<string> errors)
        {
            var f = new ContentFactory(game.QuestionData);
            // Modes hand out every desk tool on sale by the base shift: there's no Home to buy them in.
            d.tools = d.tools.Concat(tools).Distinct().ToArray();
            var plan = f.Shift(d);
            errors = f.Errors.ToList();
            string mode = d.title.Split(' ')[0].ToUpperInvariant();
            var inner = plan.Context;
            plan.Context = seed =>
            {
                var ctx = inner(seed);
                ctx.Mode = mode;
                return ctx;
            };
            return plan;
        }
    }
}
