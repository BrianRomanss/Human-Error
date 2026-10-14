using System;
using System.Collections.Generic;

namespace HumanError.Model
{
    public enum Species { Citizen, Human }

    /// <summary>Clans share body features (GDD §3): the visual rulebook every disguise must copy.</summary>
    public enum Clan { Vessi, Ammaru, Plo, Ruu }

    public static class Clans
    {
        public static readonly Clan[] All = { Clan.Vessi, Clan.Ammaru, Clan.Plo, Clan.Ruu };

        /// <summary>The feature that identifies the clan (shown in the Rulebook).</summary>
        public static string Feature(Clan c)
        {
            switch (c)
            {
                case Clan.Vessi: return "fin crest, two eyes";
                case Clan.Ammaru: return "third eye, glow antennae";
                case Clan.Plo: return "frill crest, one great eye";
                default: return "stalk eyes on top of the head";
            }
        }

        public static string Title(Clan c)
        {
            switch (c)
            {
                case Clan.Vessi: return "FIN-BEARER";
                case Clan.Ammaru: return "THIRD-EYE";
                case Clan.Plo: return "FRILL-CROWN";
                default: return "STALK-SEER";
            }
        }

        /// <summary>Number of eyes a citizen of the clan has (drives eye art and the photo).</summary>
        public static int EyeCount(Clan c) => c == Clan.Plo ? 1 : c == Clan.Ammaru ? 3 : 2;

        // Clan knowledge every citizen has (Rulebook "Clans" page) — the source of Personal and Culture questions.
        static readonly string[] Elders = { "Elder Orrimae", "Elder Sunnek", "Elder Pluvia", "Elder Daroo" };
        static readonly string[] Greetings = { "a fin-tap to the brow", "three slow nods", "a frill flare", "crossed stalks" };
        static readonly string[] Homes = { "the Tide Terraces", "the Glow Spires", "the Coral Vaults", "the Moss Deeps" };

        public static string Elder(Clan c) => Elders[(int)c];
        public static string ElderGreeting(Clan c) => Greetings[(int)c];
        public static string Home(Clan c) => Homes[(int)c];
    }

    /// <summary>
    /// Vel'Taran date: cycle and day. Citizens write it "C412·6"; humans tend to write "412.6" (GDD §3).
    /// Days run 1–9 within a cycle.
    /// </summary>
    public readonly struct CycleDate : IEquatable<CycleDate>, IComparable<CycleDate>
    {
        public const int DaysPerCycle = 9;
        public readonly int Cycle;
        public readonly int Day;

        public CycleDate(int cycle, int day)
        {
            if (day < 1 || day > DaysPerCycle) throw new ArgumentOutOfRangeException(nameof(day), day, "Days are 1–9");
            Cycle = cycle;
            Day = day;
        }

        int Ordinal => Cycle * DaysPerCycle + (Day - 1);

        public CycleDate AddDays(int days)
        {
            int o = Ordinal + days;
            return new CycleDate(o / DaysPerCycle, o % DaysPerCycle + 1);
        }

        public string Lattice => $"C{Cycle}·{Day}";
        public string Earth => $"{Cycle}.{Day}";
        public string Format(DateStyle style) => style == DateStyle.Lattice ? Lattice : Earth;
        public override string ToString() => Lattice;

        public int CompareTo(CycleDate other) => Ordinal.CompareTo(other.Ordinal);
        public bool Equals(CycleDate other) => Ordinal == other.Ordinal;
        public override bool Equals(object obj) => obj is CycleDate d && Equals(d);
        public override int GetHashCode() => Ordinal;
        public static bool operator <(CycleDate a, CycleDate b) => a.Ordinal < b.Ordinal;
        public static bool operator >(CycleDate a, CycleDate b) => a.Ordinal > b.Ordinal;
        public static bool operator <=(CycleDate a, CycleDate b) => a.Ordinal <= b.Ordinal;
        public static bool operator >=(CycleDate a, CycleDate b) => a.Ordinal >= b.Ordinal;
    }

    public enum DateStyle { Lattice, Earth }

    /// <summary>Lattice Tower floors a visitor can have business on.</summary>
    public static class Floors
    {
        public static readonly int[] All = { 2, 5, 9, 14, 21 };

        public static string Office(int floor)
        {
            switch (floor)
            {
                case 2: return "CLINIC";
                case 5: return "PERMITS";
                case 9: return "AUDIT";
                case 14: return "RECORDS";
                case 21: return "HOUSING";
                default: return "UNKNOWN";
            }
        }
    }

    /// <summary>The small red moon changes colour nightly; every citizen knows tonight's colour (GDD §3).</summary>
    public static class Kesh
    {
        public static readonly string[] Colours = { "red", "amber", "violet", "white", "green" };
    }

    /// <summary>Desk tool ids (GDD §5.6).</summary>
    public static class Tools
    {
        public const string UvLamp = "uv";
        public const string GillScope = "gillscope";
        /// <summary>Archive lookup: check a name against Lattice records, once per visitor (GDD §5.6, shift 14).</summary>
        public const string Archive = "archive";
        /// <summary>The 4th question upgrade (GDD §5.6, shift 16): one more question per visitor.</summary>
        public const string FourthQuestion = "q4";
        public static readonly string[] All = { UvLamp, GillScope, Archive, FourthQuestion };

        public static string Name(string tool)
        {
            switch (tool)
            {
                case UvLamp: return "UV LAMP";
                case GillScope: return "GILL-SCOPE";
                case Archive: return "ARCHIVE";
                case FourthQuestion: return "4TH QUESTION";
                default: return tool.ToUpperInvariant();
            }
        }
    }

    /// <summary>Everything about tonight that rules and answers are checked against.</summary>
    public sealed class ShiftContext
    {
        public int ShiftNumber = 1;
        public CycleDate Tonight = new CycleDate(412, 6);
        public string KeshColour = "amber";
        public List<int> ClosedFloors = new List<int>();
        public int QuestionBudget = 3;
        public List<QuestionDef> Questions = new List<QuestionDef>();
        public List<IDeskRule> Rules = new List<IDeskRule>();
        /// <summary>Desk tools unlocked tonight (GDD §5.6), e.g. <see cref="Tools.UvLamp"/>.</summary>
        public HashSet<string> Tools = new HashSet<string>();
        /// <summary>Tells introduced so far (GDD §8 "introduce → combine → twist"); null means all.</summary>
        public HashSet<Tell> AllowedTells;
        public string Title = "";
        public string Briefing = "";
        public string SupervisorLine = "";
        /// <summary>Show the truth after each decision (the training night) instead of only on the Shift Report.</summary>
        public bool RevealEachVisitor;
        /// <summary>Real time moves the clock and the window closes at 02:00 (false on the training night).</summary>
        public bool Timed = true;
        /// <summary>Bulletin "invaders seen wearing X suits" (GDD §5.2): most humans tonight wear this clan. A hint, not a rule.</summary>
        public Clan? WantedClan;
        /// <summary>The silent button (GDD §5.6): mark a visitor for Window 3 instead of security. A story unlock.</summary>
        public bool Silent;
        /// <summary>"ENDLESS" or "DAILY" for the score modes; empty in the story. Screens show Title instead of a shift number.</summary>
        public string Mode = "";
        public bool IsMode => !string.IsNullOrEmpty(Mode);

        public bool Has(string tool) => Tools.Contains(tool);
        public bool HasRule(string id) => Rules.Exists(r => r.Id == id);

        /// <summary>In-game clock, minutes after 20:00 (soft pressure, GDD §4).</summary>
        public const int ShiftLengthMinutes = 6 * 60;

        public static string Clock(int minutesAfterEight)
        {
            int total = (20 * 60 + minutesAfterEight) % (24 * 60);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
