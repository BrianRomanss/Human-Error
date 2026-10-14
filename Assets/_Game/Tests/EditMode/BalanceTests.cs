using System;
using System.Collections.Generic;
using System.Linq;
using ChuchuGames.Core;
using HumanError.Model;
using HumanError.Model.Content;
using NUnit.Framework;

namespace HumanError.Tests
{
    /// <summary>
    /// A simulated attentive-but-human player, for the GDD §12 gates that need playtesters ("average accuracy 70–85%",
    /// "no one stuck"). It only uses what the desk shows: it notices each visible tell with a chance that falls with the
    /// tell's difficulty, asks its budget of questions (revealing ones first, but it can't know which will hit), reads
    /// rules imperfectly, and decides: any tell → security, a broken rule → turn away, else let in.
    /// </summary>
    public static class PlayerSim
    {
        public static float Notice(Tell t) => Tells.Difficulty(t) == 1 ? 0.9f : Tells.Difficulty(t) == 2 ? 0.72f : 0.5f;
        public const float NoticeRule = 0.85f, Paranoia = 0.03f, ReadsPass = 0.8f;

        public static Decision Decide(ShiftSession s, SeededRandom rng)
        {
            var v = s.Current;
            var ctx = s.Context;
            // Questions: prefer ones that can reveal something, in a random order.
            var order = ctx.Questions.OrderByDescending(q => q.Reveals.HasValue ? 1 : 0).ThenBy(_ => rng.Value()).ToList();
            foreach (var q in order)
                if (s.CanAsk(q)) s.Ask(q);

            bool seen = false;
            foreach (var t in v.Truth.Tells)
            {
                bool visible;
                switch (Tells.Category(t))
                {
                    case TellCategory.Verbal:
                        visible = t == Tell.SmallTalkGreeting || s.Asked.Any(a => a.Reveals == t);
                        break;
                    default:
                        switch (t)
                        {
                            case Tell.FiveFingers: visible = s.FingersShown == 5; break;
                            case Tell.MouthBreathing: visible = s.Fogged || ctx.Has(Tools.GillScope); break;
                            case Tell.Sweats: visible = s.Stress >= VisitorInstance.SweatThreshold; break;
                            case Tell.SuitFlicker: visible = s.Stress >= VisitorInstance.FlickerThreshold; break;
                            case Tell.FlatHologram: visible = ctx.Has(Tools.UvLamp); break;
                            case Tell.NoRecord: visible = ctx.Has(Tools.Archive); break;
                            default: visible = true; break;
                        }
                        break;
                }
                if (visible && rng.Chance(Notice(t))) seen = true;
            }
            // Odd citizens (GDD §5.5) blink or keep their gills still: a player who notices but skips the pass arrests them.
            if (v.Truth.Quirk != Quirk.None)
            {
                bool odd = v.Truth.Quirk == Quirk.LidReflex ? rng.Chance(Notice(Tell.Blinks))
                    : (s.Fogged || ctx.Has(Tools.GillScope)) && rng.Chance(Notice(Tell.MouthBreathing));
                if (odd && !rng.Chance(ReadsPass)) seen = true;
            }
            if (seen || rng.Chance(Paranoia)) return Decision.Security;
            if (Rulebook.BreaksAnyRule(v, ctx) && rng.Chance(NoticeRule)) return Decision.TurnAway;
            return Decision.LetIn;
        }

        /// <summary>Plays a whole shift; returns the session.</summary>
        public static ShiftSession Play(ShiftPlan plan, ulong seed, IEnumerable<string> tools = null, SaveData save = null)
        {
            var s = plan.NewSession(seed, tools, save);
            var rng = new SeededRandom(seed ^ 0xB07UL);
            while (!s.IsOver) s.Decide(Decide(s, rng));
            return s;
        }
    }

    public class BalanceTests
    {
        static float Accuracy(ShiftPlan plan, IEnumerable<string> tools, int seeds = 60)
        {
            int right = 0, total = 0;
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var s = PlayerSim.Play(plan, seed, tools);
                right += s.Correct;
                total += s.Results.Count;
            }
            return right / (float)total;
        }

        [Test]
        public void AnAttentivePlayerLandsInTheTargetAccuracyBand()
        {
            // GDD §12 milestone 4 gate: average accuracy 70–85%. Early shifts may be easier, later ones harder,
            // but no shift should be a coin toss or a walkover.
            var game = TestContent.Game();
            var lines = new List<string>();
            var all = new List<float>();
            foreach (var plan in game.Shifts)
            {
                float a = Accuracy(plan, game.Home.ToolsBy(plan.Data.number));
                all.Add(a);
                lines.Add($"shift {plan.Data.number}: {a:P0}");
            }
            TestContext.WriteLine(string.Join("\n", lines));
            float mean = all.Average();
            Assert.That(mean, Is.InRange(0.70f, 0.85f), string.Join("\n", lines));
            Assert.That(all.Min(), Is.GreaterThanOrEqualTo(0.6f), "a shift is close to guessing:\n" + string.Join("\n", lines));
            // The training night (tips and a reveal after every visitor) may be a walkover; nothing after it.
            Assert.That(all.Skip(1).Max(), Is.LessThanOrEqualTo(0.92f), "a shift is a walkover:\n" + string.Join("\n", lines));
        }

        [Test]
        public void TheSimulatedPlayerFinishesTheStoryWithoutGettingStuck()
        {
            // "No one stuck": an imperfect player reaches the end of the content with Sel alive and the rent paid.
            var game = TestContent.Game();
            var save = Campaign.NewGame(31);
            int last = game.Shifts.Max(p => p.Data.number);
            for (int shift = 1; shift <= last; shift++)
            {
                var home = new Home(save, game.Home);
                foreach (var u in home.Shop.ToList()) home.ToggleBuy(u);
                home.Leave();
                var s = PlayerSim.Play(game.Shift(shift), Campaign.ShiftSeed(save, shift), save.tools, save);
                Campaign.FinishShift(save, s);
            }
            Assert.AreEqual(last + 1, save.nextShift);
            Assert.That(save.shards, Is.GreaterThan(-Home.Rent), "in debt at the end");
        }
    }

    public class SettingsTests
    {
        [Test]
        public void SettingsSurviveJsonAndBadValuesAreClamped()
        {
            var s = new Settings { textSize = 2, colourblind = true, tellAssist = true, timerOff = true, music = 0.3f };
            var back = TestContent.FromJson<Settings>(TestContent.ToJson(s));
            Assert.AreEqual(2, back.textSize);
            Assert.IsTrue(back.colourblind && back.tellAssist && back.timerOff);
            Assert.AreEqual(0.3f, back.music, 1e-5);
            var bad = new Settings { textSize = 9, master = 4f, sfx = -1f }.Sanitised();
            Assert.AreEqual(Settings.TextScales.Length - 1, bad.textSize);
            Assert.AreEqual(1f, bad.master);
            Assert.AreEqual(0f, bad.sfx);
        }

        [Test]
        public void OnlyTextSizeAndPaletteForceARedraw()
        {
            var a = new Settings();
            var b = a.Clone();
            b.music = 0.1f;
            b.timerOff = true;
            Assert.AreEqual(a.LayoutKey, b.LayoutKey);
            b.textSize = 1;
            Assert.AreNotEqual(a.LayoutKey, b.LayoutKey);
        }
    }
}
