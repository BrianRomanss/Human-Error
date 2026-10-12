using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChuchuGames.Core;
using HumanError.Model;
using HumanError.Model.Content;
using NUnit.Framework;

namespace HumanError.Tests
{
    public class CampaignTests
    {
        static ShiftSession Play(int shift, ulong seed, System.Func<VisitorInstance, ShiftContext, Decision> decide)
        {
            var session = TestContent.Plan(shift).NewSession(seed);
            while (!session.IsOver) session.Decide(decide(session.Current, session.Context));
            return session;
        }

        static Decision Best(VisitorInstance v, ShiftContext ctx) => Judge.Best(v, ctx);

        [Test]
        public void NewGameStartsAtHomeBeforeShiftOneWithNoBills()
        {
            var save = Campaign.NewGame(7);
            Assert.AreEqual(1, save.nextShift);
            Assert.IsTrue(save.atHome);
            Assert.AreEqual(Campaign.StartingShards, save.shards);
            var home = new Home(save, TestContent.Game().Home);
            Assert.IsFalse(home.HasBills);
            Assert.AreEqual(save.shards, home.Left);
            Assert.That(home.News.Count, Is.EqualTo(HomeContent.NewsPerNight));
            Assert.IsTrue(home.Messages.Any(m => m.from == "Sel"));
        }

        [Test]
        public void FinishingAShiftBooksPayLoyaltyMercyAndStrikes()
        {
            var save = Campaign.NewGame(1);
            int humansSeen = 0;
            // Arrest the first human, wave the others through.
            var session = Play(1, 3, (v, ctx) =>
            {
                if (!v.Truth.IsHuman) return Best(v, ctx);
                return humansSeen++ == 0 ? Decision.Security : Decision.LetIn;
            });
            Campaign.FinishShift(save, session);

            Assert.AreEqual(Campaign.StartingShards + session.Net + Campaign.ShiftWage, save.shards);
            Assert.AreEqual(1, save.loyalty);
            Assert.AreEqual(1, save.mercy);
            Assert.AreEqual(1, save.strikes);
            Assert.AreEqual(1, save.last.caught);
            Assert.AreEqual(1, save.last.invaders);
            Assert.AreEqual(1, save.last.mistakes);
            Assert.AreEqual(2, save.nextShift);
            Assert.IsTrue(save.atHome);
            Assert.IsTrue(Conditions.Holds("caught", save) && Conditions.Holds("invader", save) && !Conditions.Holds("perfect", save));
        }

        [Test]
        public void ThirdStrikeOfAnActMeansAHearingAndAFine()
        {
            var save = Campaign.NewGame(1);
            save.strikes = 2;
            var session = Play(1, 3, (v, ctx) => v.Truth.IsHuman ? Decision.LetIn : Best(v, ctx));
            int before = save.shards;
            Campaign.FinishShift(save, session);
            Assert.IsTrue(save.last.hearing);
            Assert.AreEqual(1, save.hearings);
            Assert.AreEqual(0, save.strikes);
            Assert.AreEqual(before + session.Net + Campaign.ShiftWage - Campaign.HearingFine, save.shards);
            var home = new Home(save, TestContent.Game().Home);
            Assert.IsTrue(home.Messages.Any(m => m.id == "hearing"));
        }

        [Test]
        public void StrikesResetWhenANewActStarts()
        {
            var ctx = TestContent.Ctx();
            ctx.ShiftNumber = 8;
            var queue = VisitorGenerator.Generate(TestContent.Config(), ctx, 4);
            var session = new ShiftSession(ctx, queue);
            while (!session.IsOver) session.Decide(Decision.Security); // arrests every citizen: many strikes
            var save = Campaign.NewGame(1);
            Campaign.FinishShift(save, session);
            Assert.AreEqual(9, save.nextShift);
            Assert.AreEqual(2, Acts.For(save.nextShift));
            Assert.AreEqual(0, save.strikes);
        }

        [Test]
        public void RentIsAlwaysPaidOtherBillsOnlyWhenAffordable()
        {
            var save = Campaign.NewGame(1);
            save.last = new ShiftSummary { shift = 1, visitors = 7 };
            save.shards = Home.Rent + Home.Food + 15;
            var home = new Home(save, TestContent.Game().Home);
            Assert.IsTrue(home.Bill("rent").Required && home.Bill("rent").Paid);
            Assert.IsTrue(home.Bill("food").Paid, "food is ticked while there's money for it");
            Assert.IsFalse(home.Bill("meds").Paid, "meds don't fit after rent and food");
            Assert.IsFalse(home.CanToggle(home.Bill("meds")));
            Assert.IsFalse(home.Toggle(home.Bill("rent")), "rent can't be skipped");
            Assert.IsTrue(home.Toggle(home.Bill("food")));
            Assert.IsTrue(home.Toggle(home.Bill("meds")), "skipping food frees enough for the medicine");
            Assert.AreEqual(save.shards - Home.Rent - Home.Meds, home.Left);

            home.Leave();
            Assert.AreEqual(Home.Food + 15 - Home.Meds, save.shards);
            Assert.IsTrue(save.foodMissed);
            Assert.IsFalse(save.medsMissed);
            Assert.IsFalse(save.atHome);
        }

        [Test]
        public void RentCanPutYouInDebt()
        {
            var save = Campaign.NewGame(1);
            save.last = new ShiftSummary { shift = 2, visitors = 8 };
            save.shards = 10;
            var home = new Home(save, TestContent.Game().Home);
            Assert.IsFalse(home.Bill("food").Paid);
            Assert.IsFalse(home.Bill("meds").Paid);
            home.Leave();
            Assert.AreEqual(10 - Home.Rent, save.shards);
            Assert.IsTrue(Conditions.Holds("debt", save));
        }

        [Test]
        public void SkippingSelsMedicineAndFoodEndsInHospital()
        {
            var save = Campaign.NewGame(1);
            save.shards = 1000;
            var content = TestContent.Game().Home;
            for (int night = 1; night <= 2; night++)
            {
                save.last = new ShiftSummary { shift = night, visitors = 8 };
                var home = new Home(save, content);
                home.Toggle(home.Bill("meds"));
                home.Toggle(home.Bill("food"));
                Assert.AreEqual(Campaign.SelWell - 2 * night < 0 ? 0 : Campaign.SelWell - 2 * night, home.SelHealthTomorrow);
                home.Leave();
            }
            Assert.IsTrue(save.hospitalDue);
            Assert.IsTrue(save.Has("sel-hospitalised"));

            save.last = new ShiftSummary { shift = 3, visitors = 8 };
            var next = new Home(save, content);
            Assert.IsNotNull(next.Bill("hospital"));
            Assert.IsTrue(next.Bill("hospital").Required);
            Assert.IsTrue(next.Messages.Any(m => m.id == "clinic"));
            Assert.AreEqual(Campaign.SelWell, next.SelHealthTomorrow, "treated, fed and medicated");
            next.Leave();
            Assert.IsFalse(save.hospitalDue);
        }

        [Test]
        public void MessageChoicesSetFlagsLoyaltyAndMercy()
        {
            var save = Campaign.NewGame(1);
            save.last = new ShiftSummary { shift = 2, visitors = 8 };
            save.shards = 200;
            var home = new Home(save, TestContent.Game().Home);
            var neighbour = home.Messages.Single(m => m.id == "neighbour-2");
            Assert.IsTrue(home.Choose(neighbour, 0));
            Assert.IsTrue(home.Choose(neighbour, 1), "you can change your mind until you leave");
            Assert.AreEqual(1, home.Choice(neighbour));
            home.Leave();
            Assert.IsTrue(save.Has("protected-22b"));
            Assert.IsTrue(save.Has("neighbour-2:1"));
            Assert.IsFalse(save.Has("reported-22b"));
            Assert.AreEqual(1, save.mercy);
            Assert.AreEqual(0, save.loyalty);

            // The next evening's news reacts to it.
            save.last = new ShiftSummary { shift = 3, visitors = 8 };
            var news = TestContent.Game().Home.NewsFor(save);
            StringAssert.Contains("ignores perfectly good tips", news[0].text);
        }

        [Test]
        public void ChoicesThatCostShardsNeedTheMoney()
        {
            var save = Campaign.NewGame(1);
            save.shards = 20;
            var content = new HomeContent();
            content.Messages.Add(new MessageData
            {
                id = "gift", after = 0, from = "Sel", text = "Buy me a sketchbook?",
                choices = new[] { new ChoiceData { text = "Yes", shards = -30 }, new ChoiceData { text = "Here's 5", shards = -5 } },
            });
            var home = new Home(save, content);
            var m = home.Messages[0];
            Assert.IsFalse(home.Choose(m, 0));
            Assert.IsTrue(home.Choose(m, 1));
            Assert.AreEqual(15, home.Left);
            home.Leave();
            Assert.AreEqual(15, save.shards);
        }

        [Test]
        public void TheUvLampIsSoldBeforeShiftSixAndJoinsTheDesk()
        {
            var game = TestContent.Game();
            var save = Campaign.NewGame(1);
            save.shards = 200;
            save.last = new ShiftSummary { shift = 4, visitors = 9 };
            save.nextShift = 5;
            Assert.IsEmpty(new Home(save, game.Home).Shop);

            save.last = new ShiftSummary { shift = 5, visitors = 10 };
            save.nextShift = 6;
            var home = new Home(save, game.Home);
            var lamp = home.Shop.Single();
            Assert.IsTrue(home.ToggleBuy(lamp));
            Assert.AreEqual(200 - Home.Rent - Home.Food - Home.Meds - lamp.price, home.Left);
            home.Leave();
            CollectionAssert.Contains(save.tools, Tools.UvLamp);

            var session = game.Shift(6).NewSession(Campaign.ShiftSeed(save, 6), save.tools);
            Assert.IsTrue(session.Context.Has(Tools.UvLamp));
            save.last = new ShiftSummary { shift = 6, visitors = 10 };
            save.nextShift = 7;
            Assert.IsEmpty(new Home(save, game.Home).Shop, "already owned");
        }

        [Test]
        public void ConditionsNegateAndCheckFlags()
        {
            var save = Campaign.NewGame(1);
            save.Set("met-duun");
            Assert.IsTrue(Conditions.Holds("flag:met-duun", save));
            Assert.IsFalse(Conditions.Holds("!flag:met-duun", save));
            Assert.IsTrue(Conditions.Holds("!caught", save));
            Assert.IsTrue(Conditions.IsKnown("!sel-sick"));
            Assert.IsFalse(Conditions.IsKnown("flag:"));
            Assert.IsFalse(Conditions.IsKnown("moonquake"));
        }

        [Test]
        public void ShiftSeedsAreStablePerCampaign()
        {
            var a = Campaign.NewGame(12345);
            var b = Campaign.NewGame(12345);
            Assert.AreEqual(Campaign.ShiftSeed(a, 3), Campaign.ShiftSeed(b, 3));
            Assert.AreNotEqual(Campaign.ShiftSeed(a, 3), Campaign.ShiftSeed(a, 4));
            Assert.AreNotEqual(Campaign.ShiftSeed(a, 3), Campaign.ShiftSeed(Campaign.NewGame(54321), 3));
        }

        [Test]
        public void SaveSurvivesAJsonRoundTripInASlot()
        {
            var save = Campaign.NewGame(99);
            save.shards = -15;
            save.loyalty = 3;
            save.mercy = 2;
            save.selHealth = 1;
            save.hospitalDue = true;
            save.Set("w3-replied");
            save.tools.Add(Tools.UvLamp);
            save.last = new ShiftSummary { shift = 4, visitors = 9, pay = 85, caught = 2, hearing = true };

            var dir = Path.Combine(Path.GetTempPath(), "he-save-test-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var slots = new SaveSlots<SaveData>(dir, TestContent.ToJson, TestContent.FromJson<SaveData>);
                slots.Save(2, save);
                var back = slots.Load(2);
                Assert.IsNotNull(back);
                Assert.AreEqual(TestContent.ToJson(save), TestContent.ToJson(back));
                Assert.AreEqual(-15, back.shards);
                Assert.IsTrue(back.Has("w3-replied"));
                Assert.AreEqual(4, back.last.shift);
                Assert.IsTrue(back.last.hearing);
                Assert.IsFalse(slots.Exists(1));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void AnAveragePlayerKeepsSelOutOfHospital()
        {
            // GDD §12 milestone 4 targets 70–85% accuracy: two mistakes a shift must not spiral into debt.
            var game = TestContent.Game();
            var save = Campaign.NewGame(77);
            for (int shift = 1; shift <= 5; shift++)
            {
                new Home(save, game.Home).Leave();
                var session = game.Shift(shift).NewSession(Campaign.ShiftSeed(save, shift), save.tools);
                int i = 0;
                while (!session.IsOver)
                {
                    var d = Judge.Best(session.Current, session.Context);
                    if (i == 1 || i == 4) d = d == Decision.LetIn ? Decision.TurnAway : Decision.LetIn;
                    session.Decide(d);
                    i++;
                }
                Campaign.FinishShift(save, session);
            }
            Assert.IsFalse(save.Has("sel-hospitalised"), $"Sel hospitalised; shards {save.shards}");
            Assert.That(save.shards, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ACarefulPlayerCanAffordEverythingThroughTheSlice()
        {
            // GDD §5.7: "a careful player is comfortable" — perfect play pays all bills every night and buys the lamp.
            var game = TestContent.Game();
            var save = Campaign.NewGame(2024);
            int last = game.Shifts.Max(p => p.Data.number);
            for (int shift = 1; shift <= last; shift++)
            {
                var home = new Home(save, game.Home);
                Assert.AreEqual(HomeContent.NewsPerNight, home.News.Count, $"news before shift {shift}");
                if (home.HasBills) Assert.IsTrue(home.Bills.All(b => b.Paid), $"bills before shift {shift}");
                foreach (var u in home.Shop.ToList()) Assert.IsTrue(home.ToggleBuy(u), $"can't afford {u.name}");
                home.Leave();
                var session = game.Shift(shift).NewSession(Campaign.ShiftSeed(save, shift), save.tools);
                while (!session.IsOver) session.Decide(Judge.Best(session.Current, session.Context));
                Campaign.FinishShift(save, session);
            }
            Assert.AreEqual(last + 1, save.nextShift);
            Assert.AreEqual(Campaign.SelWell, save.selHealth);
            Assert.That(save.shards, Is.GreaterThan(0));
            CollectionAssert.Contains(save.tools, Tools.UvLamp);
        }
    }
}
