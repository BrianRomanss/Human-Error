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
            // Across the whole game, every tell must be findable in at least one shift.
            var found = TestContent.Plans().SelectMany(p => VisitorGenerator.AvailableTells(p.Context(1))).Distinct().ToList();
            foreach (var t in Tells.All) Assert.Contains(t, found, $"{t} can never be found");
        }

        [Test]
        public void TellsAreIntroducedGradually()
        {
            int previous = 0;
            foreach (var p in TestContent.Plans())
            {
                int count = VisitorGenerator.AvailableTells(p.Context(1)).Count;
                Assert.That(count, Is.GreaterThanOrEqualTo(previous), $"shift {p.Data.number} has fewer tells than the one before");
                previous = count;
            }
        }

        static ContentFile Minimal() => new ContentFile
        {
            questions = new[]
            {
                new QuestionData { id = "q1", kind = "Greeting", reveals = "SaysFine", text = "Hi?", citizen = new[] { "I am {mood}% content." }, human = new[] { "Fine!" } },
                new QuestionData { id = "q2", kind = "Pressure", text = "Why?", citizen = new[] { "No." } },
            },
            shifts = new[] { new ShiftData { number = 1, day = 1, visitors = 4, humans = 1, questions = new[] { "q1", "q2" }, questionBudget = 2 } },
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
