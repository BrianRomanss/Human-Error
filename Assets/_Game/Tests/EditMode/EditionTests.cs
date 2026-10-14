using System;
using System.Collections.Generic;
using System.Linq;
using HumanError.Model;
using NUnit.Framework;

namespace HumanError.Tests
{
    public class EditionTests
    {
        [Test]
        public void TheDemoStopsAfterShiftFive()
        {
            Assert.IsTrue(Edition.Demo.CanPlay(1));
            Assert.IsTrue(Edition.Demo.CanPlay(Edition.DemoShifts));
            Assert.IsFalse(Edition.Demo.CanPlay(Edition.DemoShifts + 1));
            Assert.IsTrue(Edition.Full.CanPlay(30));
        }

        [Test]
        public void EndlessIsLockedInTheDemo()
        {
            Assert.IsFalse(Edition.Demo.EndlessUnlocked);
            Assert.IsTrue(Edition.Full.EndlessUnlocked);
        }

        [Test]
        public void TheDemoIsFinishedOnlyOnceShiftFiveIsDone()
        {
            var save = Campaign.NewGame(1);
            save.nextShift = Edition.DemoShifts;
            Assert.IsFalse(Edition.Demo.DemoFinished(save));
            save.nextShift = Edition.DemoShifts + 1;
            Assert.IsTrue(Edition.Demo.DemoFinished(save));
            Assert.IsFalse(Edition.Full.DemoFinished(save));
        }

        [Test]
        public void DemoDailiesOnlyUseTheDemosShifts()
        {
            var game = TestContent.Game();
            var demoQuestions = new HashSet<string>(game.Shifts.Where(p => p.Data.number <= Edition.DemoShifts).SelectMany(p => p.Data.questions));
            var start = new DateTime(2026, 1, 1);
            for (int i = 0; i < 120; i++)
            {
                var day = start.AddDays(i);
                var plan = Modes.Daily(game, day, out var errors, Edition.Demo.LastDailyBase);
                Assert.IsEmpty(errors, $"{day:d}: " + string.Join("; ", errors));
                var s = plan.NewSession(Modes.DailySeed(day));
                foreach (var q in s.Context.Questions) Assert.IsTrue(demoQuestions.Contains(q.Id), $"{day:d}: question {q.Id} is past the demo");
                Assert.IsFalse(s.Context.Has(Tools.UvLamp), $"{day:d}: the UV lamp arrives after the demo");
                foreach (var v in s.Queue) Assert.IsEmpty(Fairness.Problems(v, s.Context), $"{day:d} #{v.Index}");
            }
        }
    }
}
