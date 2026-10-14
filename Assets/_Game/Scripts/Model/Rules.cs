using System.Collections.Generic;
using System.Linq;

namespace HumanError.Model
{
    /// <summary>
    /// A rule the papers must satisfy (GDD §5.2, §11 "rules as data"). A real citizen who breaks one is
    /// turned away, not arrested; that separates "is this a human?" from "should this person enter?".
    /// </summary>
    public interface IDeskRule
    {
        string Id { get; }
        string RulebookText { get; }
        /// <summary>True when this visitor's papers break the rule.</summary>
        bool IsViolated(VisitorInstance v, ShiftContext ctx);
    }

    public sealed class ExpiredIdRule : IDeskRule
    {
        public string Id => "expired";
        public string RulebookText => "The ID must not be expired: its expiry must be tonight's date or later.";
        public bool IsViolated(VisitorInstance v, ShiftContext ctx) => v.Id.Expiry < ctx.Tonight;
    }

    public sealed class FloorAccessRule : IDeskRule
    {
        public string Id => "floor-access";
        public string RulebookText => "The appointment floor must be listed in the ID's floor access.";
        public bool IsViolated(VisitorInstance v, ShiftContext ctx) => !v.Id.FloorAccess.Contains(v.Slip.Floor);
    }

    public sealed class FloorClosedRule : IDeskRule
    {
        public string Id => "floor-closed";
        public string RulebookText => "Nobody may visit a floor the bulletin lists as closed.";
        public bool IsViolated(VisitorInstance v, ShiftContext ctx) => ctx.ClosedFloors.Contains(v.Slip.Floor);
    }

    /// <summary>Bulletin rule (GDD §5.1, §11): one clan needs a blue stamp on the slip ("clan-stamp" = Ammaru, "clan-stamp:Plo"…).</summary>
    public sealed class ClanStampRule : IDeskRule
    {
        public Clan Clan = Clan.Ammaru;
        public string Id => "clan-stamp";
        public string RulebookText => $"{Clan} ({Clans.Title(Clan).ToLowerInvariant()}) visitors need a BLUE stamp on their slip.";
        public bool IsViolated(VisitorInstance v, ShiftContext ctx) => v.Id.Clan == Clan && !v.Slip.BlueStamp;
    }

    /// <summary>Bulletin rule: no appointments after midnight tonight (Act 3 "the rules tighten").</summary>
    public sealed class CurfewRule : IDeskRule
    {
        /// <summary>Minutes after 20:00 when the curfew starts (00:00).</summary>
        public const int Start = 4 * 60;
        public string Id => "curfew";
        public string RulebookText => "Curfew: the appointment time must be before 00:00.";
        public bool IsViolated(VisitorInstance v, ShiftContext ctx) => Minutes(v.Slip.Time) >= Start;

        /// <summary>Minutes after 20:00 for a "HH:MM" time.</summary>
        public static int Minutes(string time)
        {
            int h = int.Parse(time.Substring(0, 2)), m = int.Parse(time.Substring(3, 2));
            return ((h * 60 + m) - 20 * 60 + 24 * 60) % (24 * 60);
        }
    }

    public static class Rulebook
    {
        /// <summary>Permanent facts about citizens (GDD §3 culture table). Every human tell breaks one.</summary>
        public static readonly string[] CitizenFacts =
        {
            "Citizens NEVER blink. Their pupils are vertical slits.",
            "Citizens have FOUR fingers and no ears.",
            "Citizens never sweat. Their neck gills pulse as they breathe.",
            "Citizens' glow spots always glow. Crests grow straight.",
            "Citizens greet by stating a mood: \"I am 60% content.\"",
            "Citizens never laugh at strangers or make small talk.",
            "Every citizen knows tonight's Kesh, the date and their clan's lore.",
        };

        /// <summary>Permanent facts about papers (forgery checks).</summary>
        public static readonly string[] PaperFacts =
        {
            "Dates are written C412·6. Any other style is a forgery.",
            "The name on the slip must match the ID exactly.",
            "The ID photo shows the same face, with slit pupils.",
            "The Lattice glyph points DOWN (V) in the ID's corner.",
        };

        /// <summary>Permanent facts about suspicious-but-innocent citizens (GDD §5.5: stop players arresting everyone).</summary>
        public static readonly string[] OddFacts =
        {
            "A citizen may be nervous, rude or silent. None of that is human.",
            "Elders with lid-reflex may blink. They carry a Lattice medical pass.",
            "Gill rot keeps gills still. It is covered by a medical pass too.",
        };

        /// <summary>Facts that only apply once a tool or paper is in play.</summary>
        public static IEnumerable<string> ExtraFacts(ShiftContext ctx)
        {
            if (ctx.Has(Tools.UvLamp)) yield return "Under the UV lamp a genuine hologram glows with rings.";
            if (ctx.Has(Tools.GillScope)) yield return "The gill-scope shows the neck up close: real gills pulse; suits' gills stay still.";
            if (ctx.Has(Tools.Archive)) yield return "The archive holds every citizen's record. No record means an invented identity.";
            if (ctx.AllowedTells == null || ctx.AllowedTells.Contains(Tell.Blinks) || ctx.AllowedTells.Contains(Tell.MouthBreathing))
                yield return "A Lattice MEDICAL PASS explains one condition: lid-reflex (may blink) or gill rot (gills still).";
        }

        public static IEnumerable<IDeskRule> ViolatedRules(VisitorInstance v, ShiftContext ctx) =>
            ctx.Rules.Where(r => r.IsViolated(v, ctx));

        public static bool BreaksAnyRule(VisitorInstance v, ShiftContext ctx) => ViolatedRules(v, ctx).Any();

        public static List<IDeskRule> Standard() =>
            new List<IDeskRule> { new ExpiredIdRule(), new FloorAccessRule(), new FloorClosedRule() };

        /// <summary>Rule by id, for content data; null if unknown.</summary>
        public static IDeskRule Create(string id)
        {
            if (id != null && id.StartsWith("clan-stamp:"))
                return System.Enum.TryParse(id.Substring(11), out Clan clan) ? new ClanStampRule { Clan = clan } : null;
            switch (id)
            {
                case "expired": return new ExpiredIdRule();
                case "floor-access": return new FloorAccessRule();
                case "floor-closed": return new FloorClosedRule();
                case "clan-stamp": return new ClanStampRule();
                case "curfew": return new CurfewRule();
                default: return null;
            }
        }
    }
}
