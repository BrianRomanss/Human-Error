using System.Collections.Generic;
using System.Linq;
using ChuchuGames.Core;
using HumanError.Model;
using NUnit.Framework;

namespace HumanError.Tests
{
    public class CycleDateTests
    {
        [Test]
        public void FormatsLatticeAndEarthStyles()
        {
            var d = new CycleDate(412, 6);
            Assert.AreEqual("C412·6", d.Lattice);
            Assert.AreEqual("412.6", d.Earth);
        }

        [Test]
        public void AddDaysRollsOverCycles()
        {
            Assert.AreEqual(new CycleDate(413, 2), new CycleDate(412, 8).AddDays(3));
            Assert.AreEqual(new CycleDate(411, 9), new CycleDate(412, 1).AddDays(-1));
            Assert.IsTrue(new CycleDate(412, 5) < new CycleDate(412, 6));
            Assert.IsTrue(new CycleDate(411, 9) < new CycleDate(412, 1));
        }

        [Test]
        public void ClockStartsAtEightAndWrapsMidnight()
        {
            Assert.AreEqual("20:00", ShiftContext.Clock(0));
            Assert.AreEqual("23:45", ShiftContext.Clock(225));
            Assert.AreEqual("02:00", ShiftContext.Clock(360));
        }
    }

    public class RuleTests
    {
        static VisitorInstance Visitor(int slipFloor, int[] access, CycleDate expiry, Clan clan = Clan.Vessi, bool stamp = true) => new VisitorInstance
        {
            Truth = new VisitorTruth { Species = Species.Citizen },
            Id = new IdCard { FloorAccess = access.ToList(), Expiry = expiry, Clan = clan },
            Slip = new AppointmentSlip { Floor = slipFloor, BlueStamp = stamp },
        };

        static ShiftContext Ctx() => new ShiftContext
        {
            Tonight = new CycleDate(412, 6),
            ClosedFloors = new List<int> { 9 },
            Rules = Rulebook.Standard(),
        };

        [Test]
        public void ExpiredIdBreaksRuleButTonightIsStillValid()
        {
            var ctx = Ctx();
            Assert.IsTrue(new ExpiredIdRule().IsViolated(Visitor(5, new[] { 5 }, new CycleDate(412, 5)), ctx));
            Assert.IsFalse(new ExpiredIdRule().IsViolated(Visitor(5, new[] { 5 }, new CycleDate(412, 6)), ctx));
        }

        [Test]
        public void FloorAccessAndClosedFloors()
        {
            var ctx = Ctx();
            Assert.IsTrue(new FloorAccessRule().IsViolated(Visitor(14, new[] { 5 }, new CycleDate(413, 1)), ctx));
            Assert.IsFalse(new FloorAccessRule().IsViolated(Visitor(14, new[] { 5, 14 }, new CycleDate(413, 1)), ctx));
            Assert.IsTrue(new FloorClosedRule().IsViolated(Visitor(9, new[] { 9 }, new CycleDate(413, 1)), ctx));
            Assert.IsFalse(Rulebook.BreaksAnyRule(Visitor(14, new[] { 14 }, new CycleDate(413, 1)), ctx));
        }

        [Test]
        public void ClanStampOnlyAppliesToThatClan()
        {
            var ctx = Ctx();
            var rule = new ClanStampRule();
            Assert.IsTrue(rule.IsViolated(Visitor(5, new[] { 5 }, new CycleDate(413, 1), Clan.Ammaru, stamp: false), ctx));
            Assert.IsFalse(rule.IsViolated(Visitor(5, new[] { 5 }, new CycleDate(413, 1), Clan.Ammaru, stamp: true), ctx));
            Assert.IsFalse(rule.IsViolated(Visitor(5, new[] { 5 }, new CycleDate(413, 1), Clan.Plo, stamp: false), ctx));
        }
    }

    public class JudgeTests
    {
        static VisitorInstance Make(VisitorKind kind, ulong seed = 3) =>
            VisitorGenerator.Create(kind, TestContent.Config(), TestContent.Ctx(), new SeededRandom(seed));

        [Test]
        public void PayTableMatchesGdd()
        {
            var ctx = TestContent.Ctx();
            var citizen = Make(VisitorKind.OrdinaryCitizen);
            Assert.AreEqual(10, Judge.Decide(citizen, ctx, Decision.LetIn).Shards);
            Assert.AreEqual(-10, Judge.Decide(citizen, ctx, Decision.TurnAway).Shards);
            var arrest = Judge.Decide(citizen, ctx, Decision.Security);
            Assert.AreEqual(-40, arrest.Shards);
            Assert.IsTrue(arrest.Strike);

            var human = Make(VisitorKind.ClumsyHuman);
            Assert.AreEqual(-30, Judge.Decide(human, ctx, Decision.LetIn).Shards);
            Assert.AreEqual(Outcome.InvaderInside, Judge.Decide(human, ctx, Decision.LetIn).Outcome);
            Assert.AreEqual(5, Judge.Decide(human, ctx, Decision.TurnAway).Shards);
            Assert.AreEqual(25, Judge.Decide(human, ctx, Decision.Security).Shards);
        }

        [Test]
        public void RuleBreakingCitizenShouldBeTurnedAwayNotArrested()
        {
            var ctx = TestContent.Ctx();
            var breaker = Make(VisitorKind.RuleBreakerCitizen);
            Assert.AreEqual(Decision.TurnAway, Judge.Best(breaker, ctx));
            Assert.AreEqual(Outcome.Correct, Judge.Decide(breaker, ctx, Decision.TurnAway).Outcome);
            Assert.AreEqual(Outcome.Citation, Judge.Decide(breaker, ctx, Decision.LetIn).Outcome);
            Assert.AreEqual(Outcome.WrongfulArrest, Judge.Decide(breaker, ctx, Decision.Security).Outcome);
        }

        [Test]
        public void BestDecisionIsAlwaysCorrect()
        {
            var ctx = TestContent.Ctx();
            foreach (VisitorKind kind in System.Enum.GetValues(typeof(VisitorKind)))
                for (ulong s = 0; s < 50; s++)
                {
                    var v = Make(kind, s);
                    Assert.AreEqual(Outcome.Correct, Judge.Decide(v, ctx, Judge.Best(v, ctx)).Outcome, $"{kind} seed {s}");
                }
        }

        [Test]
        public void OddCitizensAreLetInAndExplainedByTheirPass()
        {
            var ctx = TestContent.Ctx();
            for (ulong s = 0; s < 30; s++)
            {
                var odd = Make(VisitorKind.OddCitizen, s);
                Assert.IsNotNull(odd.Medical);
                Assert.AreEqual(Decision.LetIn, Judge.Best(odd, ctx));
                // Shift 3 allows blinking but not mouth breathing, so only the lid-reflex quirk appears.
                Assert.AreEqual(Quirk.LidReflex, odd.Truth.Quirk);
                Assert.IsTrue(odd.ShowsBlinking);
                StringAssert.Contains("medical pass", string.Join(" ", Judge.Reasons(odd, ctx)));
            }
        }

        [Test]
        public void PushingACitizenTooHardCausesAComplaint()
        {
            var ctx = TestContent.Ctx();
            var citizen = Make(VisitorKind.OrdinaryCitizen);
            var shift = new ShiftSession(ctx, new List<VisitorInstance> { citizen });
            shift.Ask(ctx.Questions.First(q => q.Id == "trap"));
            shift.Ask(ctx.Questions.First(q => q.Id == "nervous"));
            Assert.AreEqual(2, shift.Annoyance);
            var v = shift.Decide(Decision.LetIn);
            Assert.IsTrue(v.Complaint);
            Assert.AreEqual(Judge.LetInCitizen + Judge.ComplaintPenalty, v.Shards);
            Assert.AreEqual(1, shift.Complaints);
        }
    }

    public class GeneratorTests
    {
        [Test]
        public void SameSeedSameShift()
        {
            var a = TestContent.NewShift(77);
            var b = TestContent.NewShift(77);
            Assert.AreEqual(a.Queue.Count, b.Queue.Count);
            for (int i = 0; i < a.Queue.Count; i++)
            {
                Assert.AreEqual(a.Queue[i].Id.Name, b.Queue[i].Id.Name);
                Assert.AreEqual(a.Queue[i].Truth.Species, b.Queue[i].Truth.Species);
                CollectionAssert.AreEqual(a.Queue[i].Truth.Tells, b.Queue[i].Truth.Tells);
                Assert.AreEqual(a.Queue[i].Answers["kesh"].Text, b.Queue[i].Answers["kesh"].Text);
            }
        }

        [Test]
        public void QueueMixMatchesConfig()
        {
            foreach (var plan in TestContent.Plans())
            {
                var shift = plan.NewSession(5);
                var cfg = plan.Config;
                Assert.AreEqual(cfg.Visitors, shift.Queue.Count, $"shift {plan.Data.number}");
                Assert.AreEqual(cfg.Humans, shift.Queue.Count(v => v.Truth.IsHuman));
                Assert.AreEqual(cfg.RuleBreakers, shift.Queue.Count(v => v.Truth.Kind == VisitorKind.RuleBreakerCitizen));
                Assert.AreEqual(cfg.OddCitizens, shift.Queue.Count(v => v.Truth.Kind == VisitorKind.OddCitizen));
            }
        }

        [Test]
        public void EveryShiftGeneratesOnlyFairVisitors()
        {
            // GDD §12 milestone 2 gate: "1,000 generated visitors, 0 unsolvable". We check every authored shift.
            int checkedCount = 0;
            foreach (var plan in TestContent.Plans())
                for (ulong seed = 0; seed < 150; seed++)
                {
                    var shift = plan.NewSession(seed);
                    foreach (var v in shift.Queue)
                    {
                        var problems = Fairness.Problems(v, shift.Context);
                        Assert.IsEmpty(problems, $"shift {plan.Data.number} seed {seed} visitor {v.Index} ({v.Truth.Kind}): {string.Join("; ", problems)}");
                        checkedCount++;
                    }
                }
            Assert.That(checkedCount, Is.GreaterThanOrEqualTo(1000));
        }

        [Test]
        public void OnlyIntroducedTellsAppear()
        {
            foreach (var plan in TestContent.Plans())
            {
                var ctx = plan.Context(1);
                if (ctx.AllowedTells == null) continue;
                for (ulong seed = 0; seed < 40; seed++)
                    foreach (var v in plan.NewSession(seed).Queue)
                        foreach (var t in v.Truth.Tells)
                            Assert.IsTrue(ctx.AllowedTells.Contains(t), $"shift {plan.Data.number}: {t} not introduced yet");
            }
        }

        [Test]
        public void ForgedFieldsRespectTheShiftLimit()
        {
            var plan = TestContent.Plan(1);
            for (ulong seed = 0; seed < 100; seed++)
                foreach (var v in plan.NewSession(seed).Queue)
                    Assert.That(v.Truth.Tells.Count(t => Tells.Category(t) == TellCategory.Document), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void SkilledHumansHaveOneTellClumsyHaveTwoOrThree()
        {
            var ctx = TestContent.Ctx();
            var cfg = TestContent.Config();
            for (ulong s = 0; s < 100; s++)
            {
                Assert.AreEqual(1, VisitorGenerator.Create(VisitorKind.SkilledHuman, cfg, ctx, new SeededRandom(s)).Truth.Tells.Count);
                Assert.That(VisitorGenerator.Create(VisitorKind.ClumsyHuman, cfg, ctx, new SeededRandom(s)).Truth.Tells.Count, Is.InRange(2, 3));
            }
        }

        [Test]
        public void ForgeryTellsShowUpInThePapers()
        {
            var ctx = TestContent.Plan(6).Context(1); // every tell allowed, UV lamp issued
            var cfg = new ShiftConfig { ClumsyMinTells = 3, ClumsyMaxTells = 3 };
            var seen = new HashSet<Tell>();
            for (ulong s = 0; s < 800; s++)
            {
                var v = VisitorGenerator.Create(VisitorKind.ClumsyHuman, cfg, ctx, new SeededRandom(s));
                foreach (var t in v.Truth.Tells) seen.Add(t);
                if (v.Has(Tell.EarthDate)) Assert.IsTrue(v.Id.ExpiryText.Contains(".") || v.Slip.DateText.Contains("."));
                Assert.AreEqual(v.Has(Tell.NameMismatch), v.Id.Name != v.Slip.Name);
                Assert.AreEqual(v.Has(Tell.PhotoRoundPupils), v.Id.PhotoRoundPupils);
                Assert.AreEqual(v.Has(Tell.PhotoMismatch), !v.Id.Photo.SameFace(v.Truth.Look));
                Assert.AreEqual(v.Has(Tell.FlatHologram), !v.Id.HologramGenuine);
                Assert.AreEqual(v.Has(Tell.GlyphUpsideDown), !v.Id.GlyphUpright);
                if (v.Id.Photo.Clan != v.Truth.Look.Clan) Assert.Fail("photo mismatch must keep the clan");
            }
            foreach (var t in new[] { Tell.EarthDate, Tell.NameMismatch, Tell.PhotoMismatch, Tell.FlatHologram, Tell.GlyphUpsideDown })
                Assert.IsTrue(seen.Contains(t), $"{t} never generated");
        }

        [Test]
        public void HologramForgeriesNeedTheUvLamp()
        {
            var ctx = TestContent.Plan(6).Context(1);
            Assert.Contains(Tell.FlatHologram, VisitorGenerator.AvailableTells(ctx));
            ctx.Tools.Clear();
            Assert.IsFalse(VisitorGenerator.AvailableTells(ctx).Contains(Tell.FlatHologram));
        }

        [Test]
        public void VerbalTellsNeedAQuestionThatRevealsThem()
        {
            var ctx = TestContent.Plan(1).Context(1); // greeting, floor, hands, nervous
            var available = VisitorGenerator.AvailableTells(ctx);
            Assert.Contains(Tell.SaysFine, available);
            Assert.IsFalse(available.Contains(Tell.WrongKesh));
        }

        [Test]
        public void HumansWhoSlipGiveHumanAnswers()
        {
            var ctx = TestContent.Ctx();
            var cfg = TestContent.Config();
            for (ulong s = 0; s < 300; s++)
            {
                var v = VisitorGenerator.Create(VisitorKind.ClumsyHuman, cfg, ctx, new SeededRandom(s));
                if (v.Has(Tell.WrongKesh)) StringAssert.DoesNotContain(ctx.KeshColour, v.Answers["kesh"].Text);
                else StringAssert.Contains(ctx.KeshColour, v.Answers["kesh"].Text.ToLowerInvariant());
                if (v.Has(Tell.WrongDate)) StringAssert.DoesNotContain(ctx.Tonight.Lattice, v.Answers["date"].Text);
                else StringAssert.Contains(ctx.Tonight.Lattice, v.Answers["date"].Text);
                Assert.AreEqual(v.Has(Tell.FiveFingers) ? 5 : 4, v.Answers["hands"].FingersShown);
                Assert.AreEqual(v.Has(Tell.SmallTalkGreeting), !v.Greeting.Contains("%"));
            }
        }

        [Test]
        public void ClanLoreAnswersMatchTheClan()
        {
            var plan = TestContent.Plan(5);
            var ctx = plan.Context(1);
            for (ulong s = 0; s < 100; s++)
            {
                var citizen = VisitorGenerator.Create(VisitorKind.OrdinaryCitizen, plan.Config, ctx, new SeededRandom(s));
                StringAssert.Contains(Clans.Elder(citizen.Truth.Look.Clan), citizen.Answers["elder"].Text);
                var human = VisitorGenerator.Create(VisitorKind.ClumsyHuman, plan.Config, ctx, new SeededRandom(s));
                if (human.Has(Tell.WrongClanLore)) StringAssert.DoesNotContain(Clans.Elder(human.Truth.Look.Clan), human.Answers["elder"].Text);
            }
        }

        [Test]
        public void MissingStampBreakersAreFromTheStampedClan()
        {
            var plan = TestContent.Plan(5);
            int seen = 0;
            for (ulong seed = 0; seed < 100; seed++)
                foreach (var v in plan.NewSession(seed).Queue.Where(v => v.Truth.Violation == Violation.MissingStamp))
                {
                    Assert.AreEqual(Clan.Ammaru, v.Id.Clan);
                    Assert.IsFalse(v.Slip.BlueStamp);
                    seen++;
                }
            Assert.That(seen, Is.GreaterThan(0));
        }
    }

    public class ShiftSessionTests
    {
        [Test]
        public void QuestionBudgetIsEnforced()
        {
            var shift = TestContent.NewShift(9);
            var qs = shift.Context.Questions;
            shift.Ask(qs[0]);
            Assert.IsFalse(shift.CanAsk(qs[0]), "can't ask the same question twice");
            shift.Ask(qs[1]);
            shift.Ask(qs[2]);
            Assert.AreEqual(0, shift.QuestionsLeft);
            Assert.IsFalse(shift.CanAsk(qs[3]));
            Assert.Throws<System.InvalidOperationException>(() => shift.Ask(qs[3]));
        }

        [Test]
        public void DecidingMovesToNextVisitorAndResetsBudget()
        {
            var shift = TestContent.NewShift(9);
            var first = shift.Current;
            shift.Ask(shift.Context.Questions[0]);
            shift.Decide(Decision.LetIn);
            Assert.AreNotSame(first, shift.Current);
            Assert.AreEqual(shift.Context.QuestionBudget, shift.QuestionsLeft);
            Assert.AreEqual(0, shift.Annoyance);
            Assert.AreEqual(1, shift.Results.Count);
            Assert.AreEqual(1, shift.Results[0].Asked.Count);
        }

        [Test]
        public void PerfectPlayGetsFullAccuracyAndNoStrikes()
        {
            foreach (var plan in TestContent.Plans())
            {
                var shift = plan.NewSession(21);
                while (!shift.IsOver) shift.Decide(Judge.Best(shift.Current, shift.Context));
                Assert.AreEqual(1f, shift.Accuracy);
                Assert.AreEqual(0, shift.Strikes);
                Assert.AreEqual(plan.Config.Humans, shift.HumansCaught);
                Assert.AreEqual(0, shift.Penalties);
            }
        }

        [Test]
        public void StressRisesOnlyForHumansAndTriggersSweat()
        {
            var ctx = TestContent.Ctx();
            var cfg = TestContent.Config();
            VisitorInstance sweaty = null;
            for (ulong s = 0; sweaty == null; s++)
            {
                var v = VisitorGenerator.Create(VisitorKind.ClumsyHuman, cfg, ctx, new SeededRandom(s));
                if (v.Has(Tell.Sweats)) sweaty = v;
            }
            var citizen = VisitorGenerator.Create(VisitorKind.OrdinaryCitizen, cfg, ctx, new SeededRandom(1));
            var shift = new ShiftSession(ctx, new List<VisitorInstance> { sweaty, citizen });
            Assert.IsFalse(shift.IsSweating);
            shift.Ask(ctx.Questions.First(q => q.Id == "trap"));
            shift.Ask(ctx.Questions.First(q => q.Id == "nervous"));
            Assert.IsTrue(shift.IsSweating);
            shift.Decide(Decision.Security);
            shift.Ask(ctx.Questions.First(q => q.Id == "trap"));
            Assert.AreEqual(0f, shift.Stress);
            Assert.IsFalse(shift.IsSweating);
        }

        [Test]
        public void ActionQuestionsShowFingersAndFog()
        {
            var plan = TestContent.Plan(4);
            var ctx = plan.Context(1);
            for (ulong s = 0; s < 200; s++)
            {
                var v = VisitorGenerator.Create(VisitorKind.ClumsyHuman, plan.Config, ctx, new SeededRandom(s));
                var shift = new ShiftSession(ctx, new List<VisitorInstance> { v });
                shift.Ask(ctx.Questions.First(q => q.Id == "palm"));
                shift.Ask(ctx.Questions.First(q => q.Id == "breathe"));
                Assert.AreEqual(v.Has(Tell.FiveFingers) ? 5 : 4, shift.FingersShown);
                Assert.AreEqual(v.Has(Tell.MouthBreathing), shift.Fogged);
            }
        }
    }
}
