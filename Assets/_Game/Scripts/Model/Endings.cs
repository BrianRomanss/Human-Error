using System.Collections.Generic;
using System.Linq;

namespace HumanError.Model
{
    /// <summary>The five endings (GDD §3).</summary>
    public enum Ending { None, ModelOfficer, QuietDoor, Exposed, OpenLattice, HumanError }

    /// <summary>
    /// Which ending a finished (or failed) campaign gets, and its epilogue. Two hidden values drive it — Loyalty and
    /// Mercy — plus suspicion, a few one-off choices and, for the secret ending, every clue about Ilo's own past.
    /// </summary>
    public static class Endings
    {
        /// <summary>Clues about Ilo's past, each found through a message choice or a story visit (GDD §3 secret ending).</summary>
        public static readonly string[] Clues = { "clue-sleep", "clue-photo", "clue-file", "clue-w3", "clue-scar" };

        /// <summary>Suspicion at which the Lattice arrests you at the end, however loyal you look.</summary>
        public const int ExposedSuspicion = 10;

        /// <summary>Set by an Act 3 hearing: arrested at your own window, the story ends at once.</summary>
        public const string ArrestedFlag = "arrested";
        /// <summary>The finale choice: help Window 3 open the basement on the last night.</summary>
        public const string OpenFlag = "finale-open";
        /// <summary>Proof of the basement, found in Act 3 (without it, opening the doors fails).</summary>
        public const string EvidenceFlag = "basement-evidence";

        public static int CluesFound(SaveData s) => Clues.Count(s.Has);

        public static Ending Decide(SaveData s)
        {
            if (s.Has(ArrestedFlag)) return Ending.Exposed;
            if (CluesFound(s) == Clues.Length) return Ending.HumanError;
            if (s.Has(OpenFlag)) return s.Has(EvidenceFlag) ? Ending.OpenLattice : Ending.Exposed;
            if (s.suspicion >= ExposedSuspicion) return Ending.Exposed;
            return s.mercy > s.loyalty ? Ending.QuietDoor : Ending.ModelOfficer;
        }

        public static string Title(Ending e)
        {
            switch (e)
            {
                case Ending.ModelOfficer: return "MODEL OFFICER";
                case Ending.QuietDoor: return "THE QUIET DOOR";
                case Ending.Exposed: return "EXPOSED";
                case Ending.OpenLattice: return "OPEN LATTICE";
                case Ending.HumanError: return "HUMAN ERROR";
                default: return "";
            }
        }

        public static string Subtitle(Ending e)
        {
            switch (e)
            {
                case Ending.ModelOfficer: return "You were promoted into the Lattice.";
                case Ending.QuietDoor: return "You became a smuggler with a spotless record.";
                case Ending.Exposed: return "You were arrested at your own window.";
                case Ending.OpenLattice: return "The basement doors opened, and everyone saw.";
                case Ending.HumanError: return "Secret ending: your own ID check came back human.";
                default: return "";
            }
        }

        /// <summary>The epilogue: a few short paragraphs that follow what you did to Sel, Mira and Duun.</summary>
        public static List<string> Epilogue(Ending e, SaveData s)
        {
            var p = new List<string>();
            switch (e)
            {
                case Ending.ModelOfficer:
                    p.Add($"The Lattice gives you an office on floor 9 and a medal shaped like a window. {s.humansCaught} invaders were caught on your shifts. Nobody says the word \"processed\" at the ceremony. Nobody has to.");
                    p.Add("Your first order as an auditor is to review Night Window 3. The new receptionist blinks when she's nervous. You write that down.");
                    break;
                case Ending.QuietDoor:
                    p.Add($"Your record stays clean. Graav signs it without reading. Somewhere in the Moss Deeps, {s.humansHidden + s.humansLetIn} people sleep under a roof because of a door you didn't see.");
                    p.Add("Window 3 never thanks you in writing. Once a cycle, a blue thread is tied to your door handle. You never take them off.");
                    break;
                case Ending.Exposed:
                    p.Add(s.Has(ArrestedFlag)
                        ? "The hearing takes four minutes. The guards who walk you out are the ones you used to call. One of them hesitates at the window. You don't blame him."
                        : s.Has(OpenFlag)
                            ? "You opened the service lift for Window 3 with nothing to show the city. The doors slid back on an empty corridor and a waiting squad. Graav doesn't look surprised."
                            : "Too many invaders walked past Window 3. The Lattice doesn't need to prove why. They come for you during your shift, between one visitor and the next.");
                    p.Add("You learn where the service lift goes. You don't come back up to tell anyone.");
                    break;
                case Ending.OpenLattice:
                    p.Add("At 02:00 the service lift goes down with the Grand Auditor in it and comes back with the evidence on every screen in the Tower. By morning the whole city knows what the basement was for.");
                    p.Add("The Civic Lattice survives, barely. The bulletins stop using the word \"invader\". Night Window 3 gets a second chair, for an Earth interpreter.");
                    break;
                case Ending.HumanError:
                    p.Add("The final inspection checks everyone, staff included. Your own ID goes under the UV lamp, and the hologram stays flat.");
                    p.Add("The file says you were found at the Tower as a baby, cycle 393, wrapped in an Earth blanket, and given to an Ammaru family who wanted a child. You have always blinked when no one was looking. Now you know why.");
                    p.Add(s.mercy >= s.loyalty
                        ? "Window 3 gets you out through the door you kept open for so many others."
                        : "Graav looks at the result for a long time. Then he tears it up. \"Machine error,\" he says. \"Go home, Ilo.\"");
                    break;
            }

            // Sel, Mira and Duun carry the personal side (GDD §3: Sel keeps money and safety personal).
            if (s.selHealth >= Campaign.SelWell) p.Add("Sel grows up healthy and loud, and draws you at your window in every school picture.");
            else if (s.Has("sel-hospitalised")) p.Add("Sel spent nights in the Lattice Clinic because there wasn't money for medicine. Sel remembers that, even if you don't talk about it.");
            else p.Add("Sel's cough never quite goes away. Sel never stops saving you spore-meal.");

            bool miraCaught = s.flags.Any(f => f.StartsWith("mira") && f.EndsWith(":security"));
            bool miraHelped = s.flags.Count(f => f.StartsWith("mira") && (f.EndsWith(":letin") || f.EndsWith(":silent"))) >= 2;
            if (miraCaught) p.Add("Mira Selk's name is on a list in the basement. You know which window put it there.");
            else if (miraHelped) p.Add("Mira Selk found her brother's file. Whatever was in it, she sent you one word afterwards: \"Thank you.\"");

            if (s.flags.Any(f => f.StartsWith("duun") && f.EndsWith(":security"))) p.Add("The lifts hum unattended now. Nobody gossips at the window anymore.");
            else p.Add("Duun still passes the window every night, 80% cheerful, and still won't stop talking about the lifts.");
            return p;
        }
    }
}
