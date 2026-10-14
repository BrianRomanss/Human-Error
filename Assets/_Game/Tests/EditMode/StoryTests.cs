using System.Linq;
using HumanError.Model;
using HumanError.Model.Content;
using NUnit.Framework;

namespace HumanError.Tests
{
    public class StoryTests
    {
        static StoryVisit Visit(string id, int number) =>
            TestContent.Plans().SelectMany(p => p.Story).First(v => v.StoryId == id && v.Number == number);

        [Test]
        public void StoryVisitorsStandAtTheirPlaceWithTheirFaceAndTells()
        {
            var plan = TestContent.Plan(4);
            var mira = Visit("mira", 1);
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var plain = plan.NewSession(seed, null, null).Queue;
                var v = plain[mira.Position - 1];
                Assert.AreSame(mira, v.Story, $"seed {seed}");
                Assert.AreEqual("ESSA-VESSI MURR", v.Id.Name);
                Assert.AreEqual(Clan.Vessi, v.Truth.Look.Clan);
                CollectionAssert.AreEquivalent(mira.Spec.Tells, v.Truth.Tells);
                Assert.AreEqual(mira.Spec.Greeting, v.Greeting);
                Assert.IsEmpty(Fairness.Problems(v, plan.Context(seed)));
                // The shift's mix is unchanged: Mira took a generated clumsy human's place.
                Assert.AreEqual(plan.Config.Humans, plain.Count(x => x.Truth.IsHuman));
                Assert.AreEqual(plan.Config.Visitors, plain.Count);
                CollectionAssert.AreEqual(Enumerable.Range(0, plain.Count), plain.Select(x => x.Index));
            }
        }

        [Test]
        public void WhatYouDidLastTimeDecidesWhoComesBack()
        {
            var plan = TestContent.Plan(7);
            SaveData After(Decision d)
            {
                var s = new SaveData();
                s.Set(StoryVisit.Flag("mira1", d));
                return s;
            }
            StoryVisit MiraOn(SaveData s) => plan.NewSession(3, null, s).Queue.Select(v => v.Story).FirstOrDefault(v => v != null && v.StoryId == "mira");

            Assert.AreEqual(VisitorKind.SkilledHuman, MiraOn(After(Decision.TurnAway)).Spec.Kind, "turned away: a better suit");
            Assert.AreEqual(VisitorKind.ClumsyHuman, MiraOn(After(Decision.LetIn)).Spec.Kind, "let in: she comes back grateful");
            Assert.IsNull(MiraOn(After(Decision.Security)), "arrested: she never comes back");
            Assert.AreEqual(2, MiraOn(After(Decision.LetIn)).Number);
        }

        [Test]
        public void DecisionsOnStoryVisitorsBecomeFlags()
        {
            var game = TestContent.Game();
            var save = Campaign.NewGame(5);
            save.nextShift = 4;
            var session = game.Shift(4).NewSession(Campaign.ShiftSeed(save, 4), null, save);
            while (!session.IsOver)
            {
                var d = session.Current.Story?.StoryId == "mira" ? Decision.LetIn : Judge.Best(session.Current, session.Context);
                session.Decide(d);
            }
            Campaign.FinishShift(save, session);
            Assert.IsTrue(save.Has("mira1:letin"));
            Assert.That(save.mercy, Is.GreaterThanOrEqualTo(1));
            Assert.AreEqual(0, save.suspicion, "no questions asked, so no slip in the log: an honest mistake, not a suspicious one");
        }

        [Test]
        public void StoryVisitorsSentHomeAtClosingCountAsTurnedAway()
        {
            var game = TestContent.Game();
            var save = Campaign.NewGame(5);
            var session = game.Shift(4).NewSession(Campaign.ShiftSeed(save, 4), null, save);
            // Dawdle at the first visitor until 02:00, then decide: the window closes behind them.
            while (!session.LastCall) session.Tick(10f);
            session.Decide(Judge.Best(session.Current, session.Context));
            Assert.IsTrue(session.WindowClosed && session.IsOver);
            Assert.AreEqual(session.Queue.Count - 1, session.SentHome);
            Campaign.FinishShift(save, session);
            Assert.IsTrue(save.Has("mira1:away"));
            Assert.AreEqual(session.Queue.Count - 1, save.last.sentHome);
            Assert.IsTrue(Conditions.Holds("sent-home", save));
        }

        [Test]
        public void TheClockRunsInRealTimeAndClosesTheWindowAtTwo()
        {
            var session = TestContent.Plan(3).NewSession(2);
            int start = session.ClockMinutes;
            session.Tick(ShiftSession.SecondsPerMinute * 10 + 0.5f);
            Assert.AreEqual(start + 10, session.ClockMinutes);
            Assert.IsFalse(session.IsOver);
            session.Tick(10000f);
            Assert.AreEqual(ShiftContext.ShiftLengthMinutes, session.ClockMinutes, "the clock stops at 02:00");
            Assert.IsTrue(session.LastCall, "the visitor at the window still gets a decision");
            Assert.IsNotNull(session.Current);
            session.Decide(Decision.LetIn);
            Assert.IsTrue(session.IsOver);
            Assert.AreEqual(1, session.Results.Count);
        }

        [Test]
        public void TheTrainingNightIsUntimed()
        {
            var session = TestContent.Plan(1).NewSession(2);
            int start = session.ClockMinutes;
            session.Tick(10000f);
            Assert.AreEqual(start, session.ClockMinutes);
            Assert.IsFalse(session.LastCall);
        }

        [Test]
        public void AFullShiftOfActionsFitsBeforeClosing()
        {
            // Asking every question and deciding must never close the window on its own: only real time does.
            foreach (var plan in TestContent.Plans())
            {
                var s = plan.NewSession(1);
                while (!s.IsOver)
                {
                    foreach (var q in s.Context.Questions.Take(s.Context.QuestionBudget)) s.Ask(q);
                    s.Decide(Judge.Best(s.Current, s.Context));
                }
                Assert.IsFalse(s.WindowClosed, $"shift {plan.Data.number}");
                Assert.That(s.ClockMinutes, Is.LessThan(ShiftContext.ShiftLengthMinutes - 60), $"shift {plan.Data.number} leaves an hour of slack");
            }
        }

        [Test]
        public void WantedBulletinsPutMostHumansInThatClansSuits()
        {
            var plan = TestContent.Plan(7);
            Assert.AreEqual(Clan.Vessi, plan.Context(1).WantedClan);
            int humans = 0, vessi = 0;
            for (ulong seed = 0; seed < 200; seed++)
                foreach (var v in plan.NewSession(seed).Queue.Where(v => v.Truth.IsHuman && v.Story == null))
                {
                    humans++;
                    if (v.Truth.Look.Clan == Clan.Vessi) vessi++;
                }
            Assert.That(vessi / (float)humans, Is.InRange(0.55f, 0.8f));
        }

        [Test]
        public void TheSilentButtonHidesHumansAndLeaksOnCitizens()
        {
            var ctx = TestContent.Ctx();
            var human = VisitorGenerator.Create(VisitorKind.ClumsyHuman, TestContent.Config(), ctx, new ChuchuGames.Core.SeededRandom(1));
            var citizen = VisitorGenerator.Create(VisitorKind.OrdinaryCitizen, TestContent.Config(), ctx, new ChuchuGames.Core.SeededRandom(1));
            var hidden = Judge.Decide(human, ctx, Decision.Silent);
            Assert.AreEqual(Outcome.Hidden, hidden.Outcome);
            Assert.IsTrue(hidden.IsGood);
            Assert.IsFalse(hidden.Strike);
            var leak = Judge.Decide(citizen, ctx, Decision.Silent);
            Assert.AreEqual(Outcome.Leak, leak.Outcome);
            Assert.IsFalse(leak.IsGood);
            Assert.AreEqual(Judge.LeakPenalty, leak.Shards);
        }

        [Test]
        public void BrokenStoryContentReportsReadableErrors()
        {
            var file = TestContent.File();
            file.story = new[]
            {
                new StoryData { id = "x1", name = "X", visits = new[] { new StoryVisitData { shift = 2, position = 3, kind = "OrdinaryCitizen" } } },
                new StoryData { id = "bad", name = "B", visits = new[]
                {
                    new StoryVisitData { shift = 99, position = 1, kind = "OrdinaryCitizen" },
                    new StoryVisitData { shift = 2, position = 3, kind = "OrdinaryCitizen", tells = new[] { "Blinks" } },
                    new StoryVisitData { shift = 2, position = 3, kind = "ClumsyHuman", tells = new[] { "FlatHologram" } },
                    new StoryVisitData { shift = 2, position = 3, kind = "OrdinaryCitizen", greeting = "Hi! Busy night?" },
                    new StoryVisitData { shift = 2, position = 30, kind = "Ghost", when = new[] { "moonquake" } },
                } },
            };
            ContentFactory.LoadGame(file, out var errors);
            string all = string.Join("\n", errors);
            foreach (var expected in new[] { "ids are letters and dashes", "there is no shift 99", "only humans have tells",
                         "can't be found on shift 2", "must state a mood", "position must be", "unknown visitor kind 'Ghost'",
                         "unknown condition 'moonquake'" })
                StringAssert.Contains(expected, all);
        }

        [Test]
        public void TooManyStoryVisitorsOfAKindIsAnError()
        {
            var file = TestContent.File();
            file.story = new[]
            {
                new StoryData { id = "crowd", name = "CROWD", visits = Enumerable.Range(0, 3).Select(i =>
                    new StoryVisitData { visit = i + 1, shift = 2, position = 4, kind = "RuleBreakerCitizen" }).ToArray() },
            };
            ContentFactory.LoadGame(file, out var errors);
            StringAssert.Contains("has no RuleBreakerCitizen left", string.Join("\n", errors));
        }
    }
}
