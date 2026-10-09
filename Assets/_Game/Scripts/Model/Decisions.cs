using System.Collections.Generic;
using System.Linq;

namespace HumanError.Model
{
    public enum Decision { LetIn, TurnAway, Security }

    public enum Outcome
    {
        Correct,
        /// <summary>A human turned away: safe but no bounty; they may return (GDD §5.5).</summary>
        Safe,
        Citation,
        InvaderInside,
        WrongfulArrest,
    }

    public sealed class Verdict
    {
        public Decision Decision;
        public Outcome Outcome;
        public int Shards;
        public bool Strike;
        public bool Complaint;

        public bool IsGood => Outcome == Outcome.Correct || Outcome == Outcome.Safe;

        public string Label
        {
            get
            {
                switch (Outcome)
                {
                    case Outcome.Correct: return "Correct";
                    case Outcome.Safe: return "Safe — no bounty";
                    case Outcome.Citation: return "Wrong — citation";
                    case Outcome.InvaderInside: return "Invader got in";
                    default: return "Wrongful arrest";
                }
            }
        }
    }

    /// <summary>Pay and penalties (GDD §5.5).</summary>
    public static class Judge
    {
        public const int LetInCitizen = 10, TurnAwayBreaker = 10, CitationPenalty = -10;
        public const int TurnAwayHuman = 5, Bounty = 25, InvaderPenalty = -30, WrongfulArrestPenalty = -40;
        /// <summary>A citizen pushed with two or more pressure/trap questions files a complaint (GDD §5.3).</summary>
        public const int ComplaintPenalty = -5, AnnoyanceForComplaint = 2;

        /// <summary>The decision the rules call for (humans: security; rule-breakers: turn away; else let in).</summary>
        public static Decision Best(VisitorInstance v, ShiftContext ctx) =>
            v.Truth.IsHuman ? Decision.Security : Rulebook.BreaksAnyRule(v, ctx) ? Decision.TurnAway : Decision.LetIn;

        public static Verdict Decide(VisitorInstance v, ShiftContext ctx, Decision d, int annoyance)
        {
            var verdict = Decide(v, ctx, d);
            if (!v.Truth.IsHuman && annoyance >= AnnoyanceForComplaint && !verdict.Complaint)
            {
                verdict.Complaint = true;
                verdict.Shards += ComplaintPenalty;
            }
            return verdict;
        }

        public static Verdict Decide(VisitorInstance v, ShiftContext ctx, Decision d)
        {
            var verdict = new Verdict { Decision = d };
            if (v.Truth.IsHuman)
            {
                switch (d)
                {
                    case Decision.Security: verdict.Outcome = Outcome.Correct; verdict.Shards = Bounty; break;
                    case Decision.TurnAway: verdict.Outcome = Outcome.Safe; verdict.Shards = TurnAwayHuman; break;
                    default: verdict.Outcome = Outcome.InvaderInside; verdict.Shards = InvaderPenalty; verdict.Strike = true; break;
                }
                return verdict;
            }

            bool breaks = Rulebook.BreaksAnyRule(v, ctx);
            switch (d)
            {
                case Decision.Security:
                    verdict.Outcome = Outcome.WrongfulArrest; verdict.Shards = WrongfulArrestPenalty; verdict.Strike = true;
                    break;
                case Decision.TurnAway:
                    if (breaks) { verdict.Outcome = Outcome.Correct; verdict.Shards = TurnAwayBreaker; }
                    else { verdict.Outcome = Outcome.Citation; verdict.Shards = CitationPenalty; verdict.Complaint = true; }
                    break;
                default:
                    if (breaks) { verdict.Outcome = Outcome.Citation; verdict.Shards = CitationPenalty; }
                    else { verdict.Outcome = Outcome.Correct; verdict.Shards = LetInCitizen; }
                    break;
            }
            return verdict;
        }

        /// <summary>Why the visitor should or shouldn't have entered, for the reveal and the Shift Report.</summary>
        public static List<string> Reasons(VisitorInstance v, ShiftContext ctx)
        {
            if (v.Truth.IsHuman) return v.Truth.Tells.Select(Tells.Hint).ToList();
            var broken = Rulebook.ViolatedRules(v, ctx).Select(r => "Broke a rule: " + r.RulebookText).ToList();
            if (v.Medical != null)
                broken.Add($"A real citizen: the medical pass explains it ({v.Medical.ConditionText.ToLowerInvariant()}).");
            if (broken.Count == 0) broken.Add("A real citizen with valid papers.");
            return broken;
        }
    }
}
