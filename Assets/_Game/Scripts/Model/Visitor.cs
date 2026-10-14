using System.Collections.Generic;
using System.Linq;

namespace HumanError.Model
{
    /// <summary>
    /// Everything a human can give away (GDD §5.1, §5.3, §5.4). A citizen never has any of these; a human
    /// has at least one, chosen before any evidence is generated, so every visitor is solvable.
    /// </summary>
    public enum Tell
    {
        // Visual: on the face, always, under stress, or when an action question makes it show.
        RoundPupils,
        Blinks,
        HumanEar,
        Sweats,
        FiveFingers,       // seen when a question makes them show a hand
        SuitFlicker,       // the bio-suit glitches for a frame at high stress
        MouthBreathing,    // gills don't move; fogs the glass when asked to breathe on it
        DeadGlow,          // fake glow spots that don't glow, a crooked sticker crest
        // Verbal: in what they say.
        SmallTalkGreeting, // arrival line is small talk instead of a mood
        SaysFine,
        WrongKesh,
        WrongDate,
        WrongClanLore,     // clan elder, greeting or home
        LaughsAtTrap,
        EngagesSmallTalk,
        WrongFloor,
        WrongTime,
        // Document: forged paperwork.
        PhotoRoundPupils,
        PhotoMismatch,
        NameMismatch,
        EarthDate,
        FlatHologram,      // only visible under the UV lamp
        GlyphUpsideDown,
        NoRecord,          // an invented identity: only the archive lookup shows it
    }

    public enum TellCategory { Visual, Verbal, Document }

    public static class Tells
    {
        public static readonly Tell[] All = (Tell[])System.Enum.GetValues(typeof(Tell));

        public static TellCategory Category(Tell t)
        {
            if (t <= Tell.DeadGlow) return TellCategory.Visual;
            if (t <= Tell.WrongTime) return TellCategory.Verbal;
            return TellCategory.Document;
        }

        /// <summary>Shift Report hint: what the player could have noticed (GDD §7.2 "the tell you missed").</summary>
        public static string Hint(Tell t)
        {
            switch (t)
            {
                case Tell.RoundPupils: return "Round pupils. Citizens' pupils are vertical slits.";
                case Tell.Blinks: return "Blinked. Citizens never blink (unless a medical pass says so).";
                case Tell.HumanEar: return "An ear peeked out from the suit. Citizens have no ears.";
                case Tell.Sweats: return "Sweated under pressure. Citizens never sweat.";
                case Tell.FiveFingers: return "Showed five fingers. Citizens have four.";
                case Tell.SuitFlicker: return "The suit flickered under stress, showing a seam.";
                case Tell.MouthBreathing: return "Gills never moved, and the breath fogged the glass. Citizens breathe through their gills.";
                case Tell.DeadGlow: return "Glow spots that didn't glow and a crooked crest: stuck-on parts.";
                case Tell.SmallTalkGreeting: return "Greeted you with small talk instead of stating a mood.";
                case Tell.SaysFine: return "Answered \"fine\" instead of a mood percentage.";
                case Tell.WrongKesh: return "Got tonight's colour of Kesh wrong.";
                case Tell.WrongDate: return "Gave the date the Earth way, or got it wrong.";
                case Tell.WrongClanLore: return "Didn't know their own clan's elder, greeting or home.";
                case Tell.LaughsAtTrap: return "Laughed along with a stranger's joke.";
                case Tell.EngagesSmallTalk: return "Made small talk back. Citizens don't chat with strangers.";
                case Tell.WrongFloor: return "Named a different floor or office from the one on their slip.";
                case Tell.WrongTime: return "Named a different time from the one on their slip.";
                case Tell.PhotoRoundPupils: return "The ID photo has round pupils.";
                case Tell.PhotoMismatch: return "The ID photo shows a different face.";
                case Tell.NameMismatch: return "The name on the slip is spelled differently from the ID.";
                case Tell.EarthDate: return "A date written the Earth way (412.6) instead of C412·6.";
                case Tell.FlatHologram: return "Under the UV lamp the hologram was a flat printed circle.";
                case Tell.GlyphUpsideDown: return "The Lattice glyph on the ID was upside down.";
                case Tell.NoRecord: return "The archive had no record of that name: an invented identity.";
                default: return t.ToString();
            }
        }

        /// <summary>How hard the tell is to spot (1 easy – 3 hard); drives clumsy vs skilled humans (GDD §5.4, §6).</summary>
        public static int Difficulty(Tell t)
        {
            switch (t)
            {
                case Tell.RoundPupils:
                case Tell.Sweats:
                case Tell.SmallTalkGreeting:
                case Tell.SaysFine:
                case Tell.EngagesSmallTalk:
                    return 1;
                case Tell.Blinks:
                case Tell.HumanEar:
                case Tell.FiveFingers:
                case Tell.DeadGlow:
                case Tell.LaughsAtTrap:
                case Tell.EarthDate:
                case Tell.NameMismatch:
                case Tell.PhotoMismatch:
                case Tell.NoRecord:
                    return 2;
                default:
                    return 3;
            }
        }
    }

    /// <summary>Why a real citizen should still be turned away (GDD §5.2): papers that break a rule.</summary>
    public enum Violation { None, Expired, FloorClosed, NoFloorAccess, MissingStamp, LateAppointment }

    /// <summary>A citizen's odd-but-explained behaviour (GDD §5.5): it looks human, but a medical pass covers it.</summary>
    public enum Quirk { None, LidReflex, GillRot }

    public enum VisitorKind { OrdinaryCitizen, OddCitizen, RuleBreakerCitizen, ClumsyHuman, SkilledHuman }

    public enum Motive { None, Refugee, Runaway, Spy, LostTourist }

    /// <summary>
    /// What the suit (or the real body) shows: the GDD §9 kit's six slots (eight options for the free ones). Eyes and crest are
    /// required by the clan (ClanSO "required parts"); the other four slots are free choices.
    /// </summary>
    public sealed class Look
    {
        public Clan Clan;
        public int Head;
        public int Mouth;
        public int Pattern;
        public int Outfit;
        /// <summary>Clan recolour: 0 = standard tone, 1 = deeper tone.</summary>
        public int Tone;

        /// <summary>Options per free slot (head, mouth, pattern, outfit): GDD §12 milestone 3 asks for 8 parts per slot.</summary>
        public const int Options = 8;

        public Look Clone() => (Look)MemberwiseClone();

        public bool SameFace(Look o) => o != null && o.Clan == Clan && o.Head == Head && o.Mouth == Mouth && o.Pattern == Pattern && o.Tone == Tone;

        public override string ToString() => $"{Clan} h{Head} m{Mouth} p{Pattern} o{Outfit} t{Tone}";
    }

    public sealed class IdCard
    {
        public string Name;
        public Clan Clan;
        public List<int> FloorAccess = new List<int>();
        public CycleDate Expiry;
        public DateStyle ExpiryStyle = DateStyle.Lattice;
        public Look Photo;
        public bool PhotoRoundPupils;
        public bool HologramGenuine = true;
        public bool GlyphUpright = true;

        public string ExpiryText => Expiry.Format(ExpiryStyle);
        public string AccessText => string.Join(" · ", FloorAccess.Select(f => $"{f} {Floors.Office(f)}"));
    }

    public sealed class AppointmentSlip
    {
        public string Name;
        public int Floor;
        public string Time;
        public CycleDate Date;
        public DateStyle DateStyle = DateStyle.Lattice;
        public bool BlueStamp = true;

        public string Office => Floors.Office(Floor);
        public string DateText => Date.Format(DateStyle);
    }

    /// <summary>What the Lattice archive holds for a name (GDD §5.6 archive lookup).</summary>
    public sealed class ArchiveRecord
    {
        public string Name;
        public Clan Clan;
        public Look Photo;
        public CycleDate Registered;
    }

    /// <summary>A Lattice medical pass (GDD §5.1 "special papers"): explains one odd-citizen quirk.</summary>
    public sealed class MedicalPass
    {
        public string Name;
        public Quirk Condition;

        public string ConditionText => Condition == Quirk.LidReflex ? "LID-REFLEX · MAY BLINK" : "GILL ROT · GILLS STILL";
    }

    /// <summary>The hidden truth, decided first and never shown until the Shift Report (GDD §11).</summary>
    public sealed class VisitorTruth
    {
        public Species Species;
        public VisitorKind Kind;
        public Motive Motive;
        public string TrueName;
        public Look Look;
        public List<Tell> Tells = new List<Tell>();
        public Violation Violation;
        public Quirk Quirk;

        public bool IsHuman => Species == Species.Human;
        public bool Has(Tell t) => Tells.Contains(t);
    }

    /// <summary>One reply, pre-computed at generation so the same seed always plays the same.</summary>
    public sealed class Answer
    {
        public string QuestionId;
        public string Question;
        public string Text;
        /// <summary>The tell this reply gives away, if any.</summary>
        public Tell? Reveals;
        /// <summary>Show-hand questions: the visitor gestures, showing this many fingers (0 = no gesture).</summary>
        public int FingersShown;
        /// <summary>Breathe-on-glass questions: true when the breath fogs the glass (mouth breathing).</summary>
        public bool Fog;
    }

    /// <summary>What the desk shows: generated from the truth, maybe forged (GDD §11).</summary>
    public sealed class VisitorInstance
    {
        public int Index;
        public VisitorTruth Truth;
        public IdCard Id;
        public AppointmentSlip Slip;
        public MedicalPass Medical;
        /// <summary>The archive's record for the name on the ID; null = no record (an invented identity).</summary>
        public ArchiveRecord Record;
        public string Greeting;
        /// <summary>Set for recurring story characters (Mira, Duun…); null for generated visitors.</summary>
        public StoryVisit Story;
        public readonly Dictionary<string, Answer> Answers = new Dictionary<string, Answer>();
        /// <summary>0–1. Rises with questions; some visual tells only appear above a threshold.</summary>
        public float BaseStress;

        public const float SweatThreshold = 0.5f;
        public const float FlickerThreshold = 0.6f;

        public string DisplayName => Id.Name;
        public bool Has(Tell t) => Truth.Has(t);

        /// <summary>Visible blinking: a human tell, or an odd citizen's lid reflex.</summary>
        public bool ShowsBlinking => Has(Tell.Blinks) || Truth.Quirk == Quirk.LidReflex;

        /// <summary>Gills that don't move: a human tell, or an odd citizen's gill rot.</summary>
        public bool ShowsStillGills => Has(Tell.MouthBreathing) || Truth.Quirk == Quirk.GillRot;
    }
}
