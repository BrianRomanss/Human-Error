using System.Linq;
using HumanError.Model;
using NUnit.Framework;

namespace HumanError.Tests
{
    public class EndingTests
    {
        /// <summary>The simulated player through the whole story, buying every tool; returns the final save.</summary>
        static SaveData Campaign(int seed, out string log)
        {
            var game = TestContent.Game();
            var save = Model.Campaign.NewGame(seed);
            int last = game.Shifts.Max(p => p.Data.number);
            var sb = new System.Text.StringBuilder();
            for (int shift = 1; shift <= last && !save.Finished; shift++)
            {
                var home = new Home(save, game.Home);
                foreach (var u in home.Shop.ToList()) home.ToggleBuy(u);
                home.Leave();
                var s = PlayerSim.Play(game.Shift(shift), Model.Campaign.ShiftSeed(save, shift), save.tools, save);
                Model.Campaign.FinishShift(save, s, last);
                sb.AppendLine($"shift {shift}: {s.Correct}/{s.Results.Count} strikes {s.Strikes} (act total {save.strikes}) hearings {save.hearings} suspicion {save.suspicion} shards {save.shards}");
            }
            log = sb.ToString();
            return save;
        }

        [Test]
        public void AnAveragePlayerReachesTheEndOfTheStory()
        {
            // GDD §12 milestone 5 gate: "all 30 shifts playable end to end". A careful-but-human player must not be
            // arrested or exposed by honest mistakes alone; those endings come from choices or real carelessness.
            int arrested = 0;
            string first = null;
            var tally = new System.Collections.Generic.SortedDictionary<Ending, int>();
            for (int seed = 1; seed <= 12; seed++)
            {
                var save = Campaign(seed, out var log);
                Assert.IsTrue(save.Finished, log);
                tally[save.Ending] = (tally.TryGetValue(save.Ending, out var n) ? n : 0) + 1;
                if (save.Ending == Ending.Exposed) { arrested++; first ??= log; }
            }
            TestContext.WriteLine("endings: " + string.Join(", ", tally.Select(kv => $"{kv.Key} {kv.Value}")));
            Assert.That(arrested, Is.LessThanOrEqualTo(2), $"{arrested}/12 average players exposed:\n{first}");
        }
    }
}
