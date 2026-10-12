using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanError.Model
{
    /// <summary>The story acts (GDD §3 narrative arc). Strikes reset when a new act starts (GDD §4).</summary>
    public static class Acts
    {
        public static int For(int shift) => shift <= 8 ? 1 : shift <= 18 ? 2 : shift <= 27 ? 3 : 4;

        public static string Title(int act)
        {
            switch (act)
            {
                case 1: return "ORIENTATION";
                case 2: return "CRACKS";
                case 3: return "THE BASEMENT";
                default: return "FINALE";
            }
        }
    }

    /// <summary>How the last shift went. Kept in the save so Home shows the same news after a reload.</summary>
    [Serializable]
    public class ShiftSummary
    {
        /// <summary>0 = no shift played yet (a new game's first evening at home).</summary>
        public int shift;
        public int visitors;
        /// <summary>Net shards from decisions (pay + penalties), not counting the wage.</summary>
        public int pay;
        /// <summary>The flat wage for turning up.</summary>
        public int wage;
        public int caught;
        public int invaders;
        public int wrongful;
        public int mistakes;
        public int strikes;
        /// <summary>The shift's strikes reached three: a disciplinary hearing (GDD §4).</summary>
        public bool hearing;
    }

    /// <summary>
    /// One save slot (GDD §11 Save: shift, shards, Loyalty, Mercy, story flags, upgrades, seed). Plain fields
    /// so JsonUtility (Unity) and System.Text.Json (tests) both read and write it.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int seed;
        /// <summary>The shift the player will play next.</summary>
        public int nextShift = 1;
        /// <summary>True between a shift's end and leaving Home; false once the next shift has started.</summary>
        public bool atHome = true;
        public int shards;
        public int loyalty;
        public int mercy;
        /// <summary>Strikes this act (wrongful arrests and invaders let in).</summary>
        public int strikes;
        public int hearings;
        /// <summary>Sel's health, 0–3 (GDD §3: Sel needs medicine you pay for).</summary>
        public int selHealth = Campaign.SelWell;
        public bool hospitalDue;
        public bool medsMissed;
        public bool foodMissed;
        public int humansCaught;
        public int humansLetIn;
        public List<string> flags = new List<string>();
        /// <summary>Desk tools bought at Home (GDD §5.6), e.g. "uv".</summary>
        public List<string> tools = new List<string>();
        public ShiftSummary last = new ShiftSummary();

        public bool Has(string flag) => flags.Contains(flag);

        public void Set(string flag)
        {
            if (!string.IsNullOrEmpty(flag) && !flags.Contains(flag)) flags.Add(flag);
        }
    }

    /// <summary>Campaign rules between shifts: pay, strikes, Loyalty and Mercy (GDD §4, §5.5, §5.7).</summary>
    public static class Campaign
    {
        public const int StartingShards = 40;
        /// <summary>
        /// Flat pay per shift on top of per-decision pay. GDD §5.7: a careful player is comfortable and a careless
        /// one makes hard choices, "never so tight that the game becomes a grind" — without it, a player at the
        /// target 75% accuracy couldn't cover rent and Sel's medicine.
        /// </summary>
        public const int ShiftWage = 30;
        public const int StrikesForHearing = 3, HearingFine = 30;
        public const int SelWell = 3;

        public static SaveData NewGame(int seed) => new SaveData { seed = seed, shards = StartingShards };

        /// <summary>Each shift's queue comes from the campaign seed, so quitting mid-shift replays the same visitors.</summary>
        public static ulong ShiftSeed(SaveData s, int shift) => (ulong)(uint)s.seed * 1000003UL + (ulong)shift;

        /// <summary>Books a finished shift: shards, strikes (a hearing at three), Loyalty and Mercy. Moves the save to Home.</summary>
        public static void FinishShift(SaveData s, ShiftSession session)
        {
            int shift = session.Context.ShiftNumber;
            var results = session.Results;
            var sum = new ShiftSummary
            {
                shift = shift,
                visitors = results.Count,
                pay = session.Net,
                caught = session.HumansCaught,
                invaders = results.Count(r => r.Verdict.Outcome == Outcome.InvaderInside),
                wrongful = results.Count(r => r.Verdict.Outcome == Outcome.WrongfulArrest),
                mistakes = results.Count(r => !r.Verdict.IsGood),
                strikes = session.Strikes,
                wage = ShiftWage,
            };
            s.shards += session.Net + ShiftWage;
            foreach (var r in results.Where(r => r.Visitor.Truth.IsHuman))
            {
                // GDD §5.5: arresting a human adds Loyalty; letting one in adds Mercy.
                if (r.Verdict.Decision == Decision.Security) { s.loyalty++; s.humansCaught++; }
                else if (r.Verdict.Decision == Decision.LetIn) { s.mercy++; s.humansLetIn++; }
            }

            s.strikes += session.Strikes;
            if (s.strikes >= StrikesForHearing)
            {
                sum.hearing = true;
                s.hearings++;
                s.strikes = 0;
                s.shards -= HearingFine;
            }
            s.nextShift = shift + 1;
            if (Acts.For(s.nextShift) != Acts.For(shift)) s.strikes = 0;
            s.atHome = true;
            s.last = sum;
        }
    }

    /// <summary>
    /// Condition tokens that news and messages react to (GDD §8: headlines "react to your choices"). A list
    /// holds when every token holds; "!" negates one; "flag:name" checks a story flag.
    /// </summary>
    public static class Conditions
    {
        public static readonly string[] Known =
        {
            "caught", "invader", "wrongful", "perfect", "sloppy", "hearing",
            "sel-sick", "sel-hospital", "meds-missed", "food-missed", "debt", "broke",
        };

        public static bool IsKnown(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            var t = token.TrimStart('!');
            if (t.StartsWith("flag:")) return t.Length > 5;
            return Array.IndexOf(Known, t) >= 0;
        }

        public static bool All(IEnumerable<string> tokens, SaveData s) => tokens == null || tokens.All(t => Holds(t, s));

        public static bool Holds(string token, SaveData s)
        {
            if (token.StartsWith("!")) return !Holds(token.Substring(1), s);
            var last = s.last ?? new ShiftSummary();
            if (token.StartsWith("flag:")) return s.Has(token.Substring(5));
            switch (token)
            {
                case "caught": return last.caught > 0;
                case "invader": return last.invaders > 0;
                case "wrongful": return last.wrongful > 0;
                case "perfect": return last.visitors > 0 && last.mistakes == 0;
                // Under 60% right.
                case "sloppy": return last.visitors > 0 && last.mistakes * 5 > last.visitors * 2;
                case "hearing": return last.hearing;
                case "sel-sick": return s.selHealth <= 1;
                case "sel-hospital": return s.hospitalDue;
                case "meds-missed": return s.medsMissed;
                case "food-missed": return s.foodMissed;
                case "debt": return s.shards < 0;
                // Can't cover tonight's full budget.
                case "broke": return s.shards < Home.Rent + Home.Food + Home.Meds;
                default: return false;
            }
        }
    }
}
