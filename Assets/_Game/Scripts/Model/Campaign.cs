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
        /// <summary>Visitors still queuing when the window closed at 02:00.</summary>
        public int sentHome;
        /// <summary>Humans marked for Window 3 with the silent button.</summary>
        public int hidden;
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
        public int humansHidden;
        /// <summary>How closely the Lattice watches you (GDD §3 Act 3 "Graav watches you"): invaders let in, citizens shown to W3.</summary>
        public int suspicion;
        public List<string> flags = new List<string>();
        /// <summary>Desk tools bought at Home (GDD §5.6), e.g. "uv".</summary>
        public List<string> tools = new List<string>();
        public ShiftSummary last = new ShiftSummary();
        /// <summary>Set when the story is over (<see cref="Model.Ending"/> as a number; 0 = still playing).</summary>
        public int ending;

        public Ending Ending => (Ending)ending;
        public bool Finished => ending != 0;

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
        /// <summary>Suspicion at which Graav starts watching you (news and messages react).</summary>
        public const int SuspicionWatched = 4;
        /// <summary>Story flag that unlocks the silent button (a Window 3 message choice in Act 2).</summary>
        public const string SilentFlag = "w3-silent";

        public static SaveData NewGame(int seed) => new SaveData { seed = seed, shards = StartingShards };

        /// <summary>Each shift's queue comes from the campaign seed, so quitting mid-shift replays the same visitors.</summary>
        public static ulong ShiftSeed(SaveData s, int shift) => (ulong)(uint)s.seed * 1000003UL + (ulong)shift;

        /// <summary>
        /// Books a finished shift: shards, strikes (a hearing at three), Loyalty and Mercy. Moves the save to Home —
        /// or ends the story: a hearing from Act 3 on is an arrest (GDD §4), and the last shift decides the ending.
        /// </summary>
        public static void FinishShift(SaveData s, ShiftSession session, int lastShift = int.MaxValue)
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
                sentHome = session.SentHome,
                hidden = session.HumansHidden,
            };
            s.shards += session.Net + ShiftWage;
            foreach (var r in results)
            {
                // Story characters remember what you did: "mira1:security", "duun3:letin"…
                if (r.Visitor.Story != null) s.Set(r.Visitor.Story.FlagFor(r.Verdict.Decision));
                if (r.Verdict.Outcome == Outcome.Leak) s.suspicion++;
                if (!r.Visitor.Truth.IsHuman) continue;
                // GDD §5.5: arresting a human adds Loyalty; letting one in adds Mercy.
                switch (r.Verdict.Decision)
                {
                    case Decision.Security: s.loyalty++; s.humansCaught++; break;
                    case Decision.LetIn:
                        s.mercy++;
                        s.humansLetIn++;
                        // Graav reads the answer logs: a human let in after an obvious slip in front of you ("Fine, thanks",
                        // a laugh, five fingers) looks deliberate. A tell you never uncovered, or a subtle one (a wrong
                        // date or time), is an honest mistake: a strike, not suspicion.
                        if (r.Asked.Any(a => a.Reveals.HasValue && Tells.Difficulty(a.Reveals.Value) <= 2)) s.suspicion++;
                        break;
                    case Decision.Silent: s.mercy++; s.humansHidden++; break;
                }
            }

            // Story characters still queuing at 02:00 went home: for their story, that's being turned away.
            foreach (var v in session.Queue.Skip(session.Index).Where(v => v.Story != null))
                s.Set(v.Story.FlagFor(Decision.TurnAway));

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

            // GDD §4: three strikes are "not a game over until Act 3" — and then only for someone the Lattice already
            // suspects (GDD §3 Exposed: "too many suspicious mistakes"). Honest mistakes cost a fine and a warning.
            if (sum.hearing && Acts.For(shift) >= 3 && s.suspicion >= SuspicionWatched) s.Set(Endings.ArrestedFlag);
            if (s.Has(Endings.ArrestedFlag) || shift >= lastShift) s.ending = (int)Endings.Decide(s);
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
            "caught", "invader", "wrongful", "perfect", "sloppy", "hearing", "sent-home", "hid",
            "sel-sick", "sel-hospital", "meds-missed", "food-missed", "debt", "broke",
            "loyal", "merciful", "suspected", "act3",
        };

        public static bool IsKnown(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            var t = token.TrimStart('!');
            if (t.StartsWith("flag:")) return t.Length > 5;
            return Array.IndexOf(Known, t) >= 0;
        }

        /// <summary>A flag, or a pattern with one '*' ("mira*:security" = Mira was arrested on any visit).</summary>
        static bool HasFlag(SaveData s, string name)
        {
            int star = name.IndexOf('*');
            if (star < 0) return s.Has(name);
            string head = name.Substring(0, star), tail = name.Substring(star + 1);
            return s.flags.Any(f => f.Length >= head.Length + tail.Length && f.StartsWith(head) && f.EndsWith(tail));
        }

        public static bool All(IEnumerable<string> tokens, SaveData s) => tokens == null || tokens.All(t => Holds(t, s));

        public static bool Holds(string token, SaveData s)
        {
            if (token.StartsWith("!")) return !Holds(token.Substring(1), s);
            var last = s.last ?? new ShiftSummary();
            if (token.StartsWith("flag:")) return HasFlag(s, token.Substring(5));
            switch (token)
            {
                case "caught": return last.caught > 0;
                case "invader": return last.invaders > 0;
                case "wrongful": return last.wrongful > 0;
                case "perfect": return last.visitors > 0 && last.mistakes == 0;
                // Under 60% right.
                case "sloppy": return last.visitors > 0 && last.mistakes * 5 > last.visitors * 2;
                case "hearing": return last.hearing;
                case "sent-home": return last.sentHome > 0;
                case "hid": return last.hidden > 0;
                // Where your heart is so far (GDD §3: Loyalty to the Lattice, Mercy to humans).
                case "loyal": return s.loyalty > s.mercy;
                case "merciful": return s.mercy > s.loyalty;
                case "suspected": return s.suspicion >= Campaign.SuspicionWatched;
                // The last shift was in Act 3 or later: hearings there can end the story.
                case "act3": return Acts.For(last.shift) >= 3;
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
