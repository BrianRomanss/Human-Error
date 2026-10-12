using System.Linq;
using HumanError.Model;
using HumanError.Model.Content;
using NUnit.Framework;

namespace HumanError.Tests
{
    public class TutorialTests
    {
        static Tutorial Tut => TestContent.Plan(1).Tutorial;

        [Test]
        public void TheFirstShiftIsTheGuidedNight()
        {
            Assert.IsNotNull(Tut);
            Assert.AreEqual(1, Tut.Shift);
            Assert.IsNull(TestContent.Plan(2).Tutorial);
            Assert.That(Tut.Steps.Count, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void ScriptedVisitorsComeFirstAndShowWhatTheTipsSay()
        {
            for (ulong seed = 0; seed < 40; seed++)
            {
                var session = TestContent.Plan(1).NewSession(seed);
                var q = session.Queue;
                var ctx = session.Context;

                Assert.AreEqual(VisitorKind.OrdinaryCitizen, q[0].Truth.Kind, $"seed {seed}");
                Assert.AreEqual(Decision.LetIn, Judge.Best(q[0], ctx));

                Assert.AreEqual(VisitorKind.ClumsyHuman, q[1].Truth.Kind);
                Assert.IsTrue(q[1].Has(Tell.RoundPupils));
                Assert.AreEqual(Tell.SaysFine, q[1].Answers["greeting"].Reveals, "asking how they feel exposes them");
                Assert.AreEqual(Decision.Security, Judge.Best(q[1], ctx));

                Assert.AreEqual(VisitorKind.RuleBreakerCitizen, q[2].Truth.Kind);
                Assert.AreEqual(Violation.Expired, q[2].Truth.Violation);
                Assert.IsTrue(q[2].Id.Expiry < ctx.Tonight, "expired");
                Assert.AreEqual(Decision.TurnAway, Judge.Best(q[2], ctx));

                CollectionAssert.AreEqual(Enumerable.Range(0, q.Count), q.Select(v => v.Index));
                Assert.AreEqual(q.Count, q.Select(v => v.Truth.TrueName).Distinct().Count(), "names stay unique");
                foreach (var v in q) Assert.IsEmpty(Fairness.Problems(v, ctx), $"seed {seed} #{v.Index}");
            }
        }

        [Test]
        public void TheScriptedQueueIsTheSameForTheSameSeed()
        {
            var a = TestContent.Plan(1).NewSession(9).Queue;
            var b = TestContent.Plan(1).NewSession(9).Queue;
            CollectionAssert.AreEqual(a.Select(v => v.Id.Name), b.Select(v => v.Id.Name));
        }

        [Test]
        public void StepsStartFromTheVisitorAtTheWindow()
        {
            Assert.AreEqual(0, Tut.FirstStepFrom(0));
            int v2 = Tut.FirstStepFrom(1);
            Assert.AreEqual(2, Tut.Steps[v2].Visitor);
            Assert.IsTrue(Tut.Steps.Take(v2).All(s => s.Visitor == 1));
            Assert.AreEqual(Tut.Steps.Count, Tut.FirstStepFrom(10), "no steps for later visitors");
            Assert.IsTrue(Tutorial.AskSatisfies("ask", "floor"));
            Assert.IsTrue(Tutorial.AskSatisfies("ask:greeting", "greeting"));
            Assert.IsFalse(Tutorial.AskSatisfies("ask:greeting", "floor"));
        }

        [Test]
        public void BrokenTutorialContentReportsReadableErrors()
        {
            var file = TestContent.File();
            file.tutorial = new TutorialData
            {
                shift = 1,
                visitors = new[]
                {
                    new TutorialVisitorData { kind = "ClumsyHuman", tells = new[] { "FlatHologram" } },
                    new TutorialVisitorData { kind = "Ghost" },
                },
                steps = new[]
                {
                    new TutorialStepData { visitor = 1, target = "moon", wait = "tap", text = "Hi" },
                    new TutorialStepData { visitor = 1, target = "letin", wait = "letin", text = "Let them in" },
                    new TutorialStepData { visitor = 1, target = "question:kesh", wait = "ask:kesh", text = "Ask" },
                },
            };
            ContentFactory.LoadGame(file, out var errors);
            string all = string.Join("\n", errors);
            foreach (var expected in new[] { "tell FlatHologram can't be found on shift 1", "unknown visitor kind 'Ghost'", "unknown target 'moon'",
                         "teaches letin but a ClumsyHuman needs Security", "comes after that visitor's decision", "unknown target 'question:kesh'" })
                StringAssert.Contains(expected, all);
        }
    }
}
