using System.Collections.Generic;
using System.Linq;
using HumanError.Model;
using HumanError.Model.Content;
using NUnit.Framework;

namespace HumanError.Tests
{
    public class ContentTests
    {
        [Test]
        public void AuthoredContentLoadsWithoutErrors()
        {
            var plans = TestContent.Plans();
            Assert.That(plans.Count, Is.GreaterThanOrEqualTo(6));
            CollectionAssert.AreEqual(Enumerable.Range(1, plans.Count), plans.Select(p => p.Data.number));
            Assert.That(TestContent.File().questions.Length, Is.GreaterThanOrEqualTo(18));
        }

        [Test]
        public void EveryTellCanBeRevealedBySomething()
        {
            // Across the whole game, every tell must be findable in at least one shift (with the tools on sale by then).
            var home = TestContent.Game().Home;
            var found = TestContent.Plans()
                .SelectMany(p => VisitorGenerator.AvailableTells(p.ContextWith(1, home.ToolsBy(p.Data.number)))).Distinct().ToList();
            foreach (var t in Tells.All) Assert.Contains(t, found, $"{t} can never be found");
        }

        [Test]
        public void TellsAreIntroducedGradually()
        {
            // GDD §8 "introduce → combine → twist": once a tell is introduced it stays in play (each night's question
            // list decides which verbal ones can show), and from shift 6 every night offers a wide spread.
            HashSet<Tell> introduced = new HashSet<Tell>();
            foreach (var p in TestContent.Plans())
            {
                var ctx = p.Context(1);
                var allowed = ctx.AllowedTells ?? new HashSet<Tell>(Tells.All);
                foreach (var t in introduced) Assert.IsTrue(allowed.Contains(t), $"shift {p.Data.number} drops {t}");
                introduced.UnionWith(allowed);
                int count = VisitorGenerator.AvailableTells(ctx).Count;
                if (p.Data.number >= 6) Assert.That(count, Is.GreaterThanOrEqualTo(15), $"shift {p.Data.number} offers only {count} findable tells");
            }
        }

        [Test]
        public void ToolsOnSaleAddToTheShiftAndItsFindableTells()
        {
            var game = TestContent.Game();
            var plan = game.Shift(6);
            var uv = game.Home.Upgrades.Single(u => u.tool == Tools.UvLamp);
            Assert.AreEqual(6, uv.unlockShift);
            Assert.IsFalse(plan.Context(1).Has(Tools.UvLamp), "shift 6 doesn't issue the lamp: it's bought at Home");
            Assert.IsFalse(VisitorGenerator.AvailableTells(plan.Context(1)).Contains(Tell.FlatHologram));
            var owned = plan.ContextWith(1, new[] { Tools.UvLamp });
            Assert.IsTrue(owned.Has(Tools.UvLamp));
            Assert.IsTrue(VisitorGenerator.AvailableTells(owned).Contains(Tell.FlatHologram));
        }

        [Test]
        public void OnlyTheTrainingNightRevealsEachVisitor()
        {
            foreach (var p in TestContent.Plans())
                Assert.AreEqual(p.Data.number == 1, p.Context(1).RevealEachVisitor, $"shift {p.Data.number}");
        }

        [Test]
        public void EveryEveningHasAFullFrontPageAndAMessageWhateverHappened()
        {
            var game = TestContent.Game();
            int last = game.Shifts.Max(p => p.Data.number);
            var cases = new[]
            {
                new ShiftSummary { visitors = 8, caught = 2 },
                new ShiftSummary { visitors = 8, invaders = 2, wrongful = 1, mistakes = 4 },
                new ShiftSummary { visitors = 8, mistakes = 0 },
            };
            for (int after = 0; after < last; after++)
                foreach (var c in cases)
                {
                    c.shift = after;
                    var save = new SaveData { last = c, shards = 100 };
                    Assert.AreEqual(HomeContent.NewsPerNight, game.Home.NewsFor(save).Count, $"after shift {after}");
                    Assert.IsNotEmpty(game.Home.MessagesFor(save), $"no message after shift {after}");
                }
        }

        [Test]
        public void BrokenHomeContentReportsReadableErrors()
        {
            var file = Minimal();
            file.news = new[]
            {
                new NewsData { after = 0, text = "A", when = new[] { "moonquake" } },
                new NewsData { after = 7, text = "B" },
            };
            file.messages = new[]
            {
                new MessageData { id = "m", after = 1, from = "Sel", text = "Hi", choices = new[] { new ChoiceData() } },
                new MessageData { id = "m", after = 0, from = "", text = "Hi" },
            };
            file.upgrades = new[] { new UpgradeData { id = "x", name = "X", price = 0, unlockShift = 1, tool = "laser" } };
            ContentFactory.LoadGame(file, out var errors);
            string all = string.Join("\n", errors);
            foreach (var expected in new[] { "unknown condition 'moonquake'", "there is no evening after shift 7", "unconditional headlines",
                         "a choice without text", "duplicate message id 'm'", "no sender", "price must be above 0", "unknown tool 'laser'" })
                StringAssert.Contains(expected, all);
        }

        static ContentFile Minimal() => new ContentFile
        {
            questions = new[]
            {
                new QuestionData { id = "q1", kind = "Greeting", reveals = "SaysFine", text = "Hi?", citizen = new[] { "I am {mood}% content." }, human = new[] { "Fine!" } },
                new QuestionData { id = "q2", kind = "Pressure", text = "Why?", citizen = new[] { "No." } },
            },
            shifts = new[] { new ShiftData { number = 1, day = 1, visitors = 4, humans = 1, questions = new[] { "q1", "q2" }, questionBudget = 2 } },
            news = new[] { 0, 0, 0 }.Select((after, i) => new NewsData { after = after, text = $"Headline {i}" }).ToArray(),
        };

        [Test]
        public void MinimalContentIsValid()
        {
            ContentFactory.Load(Minimal(), out var errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void BrokenContentReportsReadableErrors()
        {
            var file = Minimal();
            file.questions[0].kind = "Gossip";
            file.questions[1].citizen = new[] { "Hello {nickname}" };
            file.shifts[0].questions = new[] { "q2", "missing" };
            file.shifts[0].tells = new[] { "Telepathy" };
            file.shifts[0].rules = new[] { "no-hats" };
            file.shifts[0].tools = new[] { "laser" };
            file.shifts[0].humans = 9;
            ContentFactory.Load(file, out var errors);
            string all = string.Join("\n", errors);
            foreach (var expected in new[] { "unknown kind 'Gossip'", "{nickname}", "unknown question 'missing'", "unknown tell 'Telepathy'",
                         "unknown rule 'no-hats'", "unknown tool 'laser'", "more special visitors than visitors" })
                StringAssert.Contains(expected, all);
        }

        [Test]
        public void VerbalQuestionsNeedHumanAnswers()
        {
            var file = Minimal();
            file.questions[0].human = new string[0];
            ContentFactory.Load(file, out var errors);
            StringAssert.Contains("no human answers", string.Join("\n", errors));
        }

        [Test]
        public void ShiftsCantUseQuestionsBeforeTheyUnlock()
        {
            var file = Minimal();
            file.questions[1].unlockShift = 3;
            ContentFactory.Load(file, out var errors);
            StringAssert.Contains("unlocks at shift 3", string.Join("\n", errors));
        }
    }
}
