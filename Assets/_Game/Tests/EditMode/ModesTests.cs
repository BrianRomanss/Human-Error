using System;
using System.Linq;
using HumanError.Model;
using NUnit.Framework;

namespace HumanError.Tests
{
    public class ModesTests
    {
        static Verdict Good => new Verdict { Outcome = Outcome.Correct };
        static Verdict Bad => new Verdict { Outcome = Outcome.InvaderInside, Strike = true };

        [Test]
        public void ScoreIsDecisionsTimesStreak()
        {
            var run = new ScoreRun();
            Assert.AreEqual(10, run.Add(Good));
            Assert.AreEqual(20, run.Add(Good));
            Assert.AreEqual(30, run.Add(Good));
            Assert.AreEqual(0, run.Add(Bad));
            Assert.AreEqual(10, run.Add(Good), "a mistake resets the streak");
            Assert.AreEqual(70, run.Score);
            Assert.AreEqual(3, run.BestStreak);
            Assert.AreEqual(4, run.Correct);
            Assert.AreEqual(5, run.Decisions);
            CollectionAssert.AreEqual(new[] { 10, 20, 30, 0, 10 }, run.Points);
            Assert.AreEqual(0, run.Add(new Verdict { Outcome = Outcome.Correct, Complaint = true }), "complaints break the streak too");
        }

        [Test]
        public void TheMultiplierIsCapped()
        {
            var run = new ScoreRun();
            for (int i = 0; i < 15; i++) run.Add(Good);
            Assert.AreEqual(ScoreRun.PerDecision * ScoreRun.MaxMultiplier, run.Add(Good));
        }

        [Test]
        public void EndlessEndsOnTheThirdStrike()
        {
            var run = new ScoreRun { EndsOnStrikes = true };
            run.Add(Bad);
            run.Add(Bad);
            Assert.IsFalse(run.Over);
            run.Add(Bad);
            Assert.IsTrue(run.Over);
            Assert.IsFalse(new ScoreRun { Strikes = 5 }.Over, "the daily never ends early");
        }

        [Test]
        public void EndlessLevelsAreValidFairAndGetHarder()
        {
            var game = TestContent.Game();
            int lastHumans = 0;
            int story = TestContent.Plans().Count;
            for (int level = 1; level <= story + 4; level++)
            {
                var plan = Modes.Endless(game, level, 42, out var errors);
                Assert.IsEmpty(errors, $"level {level}: " + string.Join("; ", errors));
                Assert.That(plan.Config.Humans, Is.GreaterThanOrEqualTo(lastHumans), $"level {level}");
                lastHumans = plan.Config.Humans;
                for (ulong seed = 0; seed < 10; seed++)
                {
                    var s = plan.NewSession(Modes.EndlessSeed(seed, level));
                    Assert.AreEqual("ENDLESS", s.Context.Mode);
                    Assert.IsFalse(s.Context.RevealEachVisitor);
                    foreach (var v in s.Queue) Assert.IsEmpty(Fairness.Problems(v, s.Context), $"level {level} seed {seed} #{v.Index}");
                }
            }
            Assert.That(lastHumans, Is.GreaterThan(TestContent.Plans().Last().Config.Humans), "levels past the story keep getting harder");
            Assert.IsTrue(Modes.Endless(game, 6, 1, out _).Context(1).Has(Tools.UvLamp), "modes hand out the UV lamp");
        }

        [Test]
        public void TheDailyIsTheSameForEveryoneOnADay()
        {
            var game = TestContent.Game();
            var day = new DateTime(2026, 10, 12);
            var a = Modes.Daily(game, day, out var errors).NewSession(Modes.DailySeed(day));
            Assert.IsEmpty(errors);
            var b = Modes.Daily(game, day, out _).NewSession(Modes.DailySeed(day));
            CollectionAssert.AreEqual(a.Queue.Select(v => v.Id.Name), b.Queue.Select(v => v.Id.Name));
            Assert.AreEqual(Modes.DailyVisitors, a.Queue.Count);
            Assert.AreEqual("DAILY", a.Context.Mode);

            var other = Modes.Daily(game, day.AddDays(1), out _).NewSession(Modes.DailySeed(day.AddDays(1)));
            CollectionAssert.AreNotEqual(a.Queue.Select(v => v.Id.Name), other.Queue.Select(v => v.Id.Name));
        }

        [Test]
        public void ADailyForEveryDayOfTheYearIsFair()
        {
            var game = TestContent.Game();
            var start = new DateTime(2026, 1, 1);
            for (int i = 0; i < 365; i++)
            {
                var day = start.AddDays(i);
                var plan = Modes.Daily(game, day, out var errors);
                Assert.IsEmpty(errors, $"{day:d}: " + string.Join("; ", errors));
                var s = plan.NewSession(Modes.DailySeed(day));
                foreach (var v in s.Queue) Assert.IsEmpty(Fairness.Problems(v, s.Context), $"{day:d} #{v.Index}");
            }
        }

        [Test]
        public void BestScoresAreKeptPerModeAndDay()
        {
            var scores = new Scores();
            Assert.IsTrue(scores.RecordEndless(300, 4));
            Assert.IsFalse(scores.RecordEndless(200, 6));
            Assert.AreEqual(300, scores.endlessBest);
            Assert.AreEqual(4, scores.endlessBestLevel);

            Assert.IsTrue(scores.RecordDaily("2026-10-12", 150));
            Assert.IsFalse(scores.RecordDaily("2026-10-12", 90));
            Assert.AreEqual(150, scores.DailyBestFor("2026-10-12"));
            Assert.AreEqual(2, scores.dailyPlays);
            Assert.IsTrue(scores.RecordDaily("2026-10-13", 40), "a new day starts fresh");
            Assert.AreEqual(0, scores.DailyBestFor("2026-10-12"));

            var back = TestContent.FromJson<Scores>(TestContent.ToJson(scores));
            Assert.AreEqual(300, back.endlessBest);
            Assert.AreEqual("2026-10-13", back.dailyDate);
        }
    }
}
