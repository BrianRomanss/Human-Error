using System.Linq;
using ChuchuGames.Core;
using HumanError.Model;
using HumanError.Model.Content;
using NUnit.Framework;

namespace HumanError.Tests
{
    public class DeskToolTests
    {
        [Test]
        public void TheArchiveKnowsCitizensAndStolenNamesButNotInventedOnes()
        {
            var plan = TestContent.Plan(14);
            Assert.IsTrue(plan.Context(1).Has(Tools.Archive), "shift 14 issues the archive");
            int invented = 0;
            for (ulong seed = 1; seed <= 40; seed++)
                foreach (var v in plan.NewSession(seed).Queue)
                {
                    if (!v.Truth.IsHuman) Assert.IsNotNull(v.Record, "every citizen has a record");
                    if (v.Has(Tell.NoRecord)) { Assert.IsNull(v.Record); invented++; }
                    else Assert.AreEqual(v.Id.Name == v.Truth.TrueName ? v.Id.Name : v.Record.Name, v.Record.Name);
                }
            Assert.That(invented, Is.GreaterThan(0), "some forgers invent names once the archive exists");
        }

        [Test]
        public void TheArchiveIsUsedOncePerVisitorAndCostsTime()
        {
            var s = TestContent.Plan(14).NewSession(3);
            int before = s.ClockMinutes;
            Assert.IsTrue(s.CanLookUp);
            var record = s.LookUp();
            Assert.AreSame(s.Current.Record, record);
            Assert.AreEqual(before + ShiftSession.LookUpMinutes, s.ClockMinutes);
            Assert.IsFalse(s.CanLookUp);
            s.Decide(Judge.Best(s.Current, s.Context));
            Assert.IsTrue(s.CanLookUp, "the next visitor can be looked up");
            Assert.IsFalse(TestContent.Plan(13).NewSession(3).CanLookUp, "no archive before shift 14");
        }

        [Test]
        public void TheGillScopeMakesStillGillsFindableWithoutTheBreathQuestion()
        {
            var plan = TestContent.Plan(9); // no breathe-on-glass question on shift 9
            Assert.IsFalse(plan.Context(1).Questions.Any(q => q.Effect == QuestionEffect.BreatheOnGlass));
            Assert.IsFalse(VisitorGenerator.AvailableTells(plan.Context(1)).Contains(Tell.MouthBreathing));
            Assert.IsTrue(VisitorGenerator.AvailableTells(plan.ContextWith(1, new[] { Tools.GillScope })).Contains(Tell.MouthBreathing));
        }

        [Test]
        public void TheFourthQuestionAddsOneToTheBudget()
        {
            var plan = TestContent.Plan(16);
            Assert.AreEqual(3, plan.Context(1).QuestionBudget);
            var s = plan.NewSession(1, new[] { Tools.FourthQuestion });
            Assert.AreEqual(4, s.Context.QuestionBudget);
            foreach (var q in s.Context.Questions.Take(4)) s.Ask(q);
            Assert.AreEqual(0, s.QuestionsLeft);
            var shop = TestContent.Game().Home.Upgrades.Select(u => u.tool).ToList();
            CollectionAssert.IsSubsetOf(new[] { Tools.UvLamp, Tools.GillScope, Tools.FourthQuestion }, shop);
        }

        [Test]
        public void TheCurfewTurnsAwayLateAppointmentsOnly()
        {
            var plan = TestContent.Plan(22);
            var ctx = plan.Context(1);
            Assert.IsTrue(ctx.HasRule("curfew"));
            int late = 0;
            for (ulong seed = 1; seed <= 40; seed++)
                foreach (var v in plan.NewSession(seed).Queue)
                {
                    int minutes = CurfewRule.Minutes(v.Slip.Time);
                    if (v.Truth.Violation == Violation.LateAppointment) { late++; Assert.That(minutes, Is.GreaterThanOrEqualTo(CurfewRule.Start)); }
                    else Assert.That(minutes, Is.LessThan(CurfewRule.Start), $"{v.Truth.Kind} at {v.Slip.Time}");
                }
            Assert.That(late, Is.GreaterThan(0));
            Assert.AreEqual(0, CurfewRule.Minutes("20:00"));
            Assert.AreEqual(240, CurfewRule.Minutes("00:00"));
            Assert.AreEqual(345, CurfewRule.Minutes("01:45"));
        }

        [Test]
        public void TheStampRuleCanMoveToAnyClan()
        {
            var rule = (ClanStampRule)Rulebook.Create("clan-stamp:Plo");
            Assert.AreEqual(Clan.Plo, rule.Clan);
            Assert.AreEqual("clan-stamp", rule.Id);
            Assert.IsNull(Rulebook.Create("clan-stamp:Martian"));
            var ctx = TestContent.Plan(19).Context(1);
            Assert.AreEqual(Clan.Plo, ctx.Rules.OfType<ClanStampRule>().Single().Clan);
            foreach (var v in TestContent.Plan(19).NewSession(5).Queue.Where(v => v.Truth.Violation == Violation.MissingStamp))
                Assert.AreEqual(Clan.Plo, v.Id.Clan);
        }

        [Test]
        public void TheSilentButtonIsAStoryUnlock()
        {
            var plan = TestContent.Plan(19);
            Assert.IsFalse(plan.NewSession(1, null, new SaveData()).Context.Silent);
            var save = new SaveData();
            save.Set(Campaign.SilentFlag);
            Assert.IsTrue(plan.NewSession(1, null, save).Context.Silent);
            var w3 = TestContent.Game().Home.Messages.Single(m => m.id == "w3-silent-offer");
            Assert.IsTrue(w3.choices.Any(c => c.flag == Campaign.SilentFlag));
        }

        [Test]
        public void WildcardFlagsMatchAnyVisit()
        {
            var s = new SaveData();
            Assert.IsTrue(Conditions.Holds("!flag:duun*:security", s));
            s.Set("duun4:security");
            Assert.IsTrue(Conditions.Holds("flag:duun*:security", s));
            Assert.IsFalse(Conditions.Holds("flag:mira*:security", s));
        }
    }

    public class EndingRuleTests
    {
        static SaveData With(params string[] flags)
        {
            var s = new SaveData();
            foreach (var f in flags) s.Set(f);
            return s;
        }

        [Test]
        public void EndingsFollowLoyaltyMercyAndChoices()
        {
            Assert.AreEqual(Ending.ModelOfficer, Endings.Decide(new SaveData { loyalty = 9, mercy = 3 }));
            Assert.AreEqual(Ending.QuietDoor, Endings.Decide(new SaveData { loyalty = 3, mercy = 9 }));
            Assert.AreEqual(Ending.Exposed, Endings.Decide(new SaveData { loyalty = 3, mercy = 9, suspicion = Endings.ExposedSuspicion }));
            Assert.AreEqual(Ending.OpenLattice, Endings.Decide(With(Endings.OpenFlag, Endings.EvidenceFlag)));
            Assert.AreEqual(Ending.Exposed, Endings.Decide(With(Endings.OpenFlag)), "opening the doors without proof is a trap");
            Assert.AreEqual(Ending.HumanError, Endings.Decide(With(Endings.Clues)), "every clue: the secret ending");
            Assert.AreEqual(Ending.Exposed, Endings.Decide(With(Endings.Clues.Append(Endings.ArrestedFlag).ToArray())), "an arrest ends everything");
        }

        [Test]
        public void EveryClueAndEvidenceRouteExistsInTheContent()
        {
            var game = TestContent.Game();
            var flags = game.Home.Messages.SelectMany(m => m.choices).Select(c => c.flag).Where(f => f != null).ToList();
            foreach (var clue in Endings.Clues) CollectionAssert.Contains(flags, clue, $"no choice sets {clue}");
            Assert.That(flags.Count(f => f == Endings.EvidenceFlag), Is.GreaterThanOrEqualTo(2), "two routes to the basement evidence");
            CollectionAssert.Contains(flags, Endings.OpenFlag);
        }

        [Test]
        public void ActThreeHearingsArrestOnlyTheSuspected()
        {
            var game = TestContent.Game();
            foreach (int suspicion in new[] { 0, Campaign.SuspicionWatched })
            {
                var save = Campaign.NewGame(1);
                save.nextShift = 20;
                save.strikes = 2;
                save.suspicion = suspicion;
                var s = game.Shift(20).NewSession(1);
                while (!s.IsOver) s.Decide(s.Current.Truth.IsHuman ? Decision.LetIn : Judge.Best(s.Current, s.Context));
                Campaign.FinishShift(save, s, 30);
                Assert.IsTrue(save.last.hearing);
                if (suspicion == 0) Assert.IsFalse(save.Finished, "an honest hearing is a fine and a warning");
                else Assert.AreEqual(Ending.Exposed, save.Ending, "a suspected receptionist is arrested at the hearing");
            }
        }

        [Test]
        public void SuspicionComesFromIgnoringObviousSlipsNotFromMissingThem()
        {
            var plan = TestContent.Plan(3);
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var s = plan.NewSession(seed);
                var save = Campaign.NewGame(1);
                bool sawSlip = false;
                while (!s.IsOver)
                {
                    if (s.Current.Truth.IsHuman)
                    {
                        foreach (var q in s.Context.Questions.Where(q => q.Reveals.HasValue && s.Current.Has(q.Reveals.Value) && Tells.Difficulty(q.Reveals.Value) <= 2).Take(1))
                        {
                            s.Ask(q);
                            sawSlip = true;
                        }
                        s.Decide(Decision.LetIn);
                    }
                    else s.Decide(Judge.Best(s.Current, s.Context));
                }
                Campaign.FinishShift(save, s);
                if (sawSlip) { Assert.That(save.suspicion, Is.GreaterThan(0)); return; }
            }
            Assert.Fail("no human slipped in 30 seeds");
        }

        [Test]
        public void TheLastShiftEndsTheStoryWithAnEpilogue()
        {
            var game = TestContent.Game();
            var save = Campaign.NewGame(9);
            save.nextShift = 30;
            save.loyalty = 5;
            var s = game.Shift(30).NewSession(9, null, save);
            while (!s.IsOver) s.Decide(Judge.Best(s.Current, s.Context));
            Campaign.FinishShift(save, s, 30);
            Assert.AreEqual(Ending.ModelOfficer, save.Ending);
            foreach (Ending e in System.Enum.GetValues(typeof(Ending)))
            {
                if (e == Ending.None) continue;
                Assert.IsNotEmpty(Endings.Title(e));
                Assert.That(Endings.Epilogue(e, save).Count, Is.GreaterThanOrEqualTo(3), e.ToString());
            }
            var back = TestContent.FromJson<SaveData>(TestContent.ToJson(save));
            Assert.AreEqual(Ending.ModelOfficer, back.Ending);
        }
    }
}
