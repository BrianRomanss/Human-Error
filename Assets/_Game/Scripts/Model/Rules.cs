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

    /// <summary>Bulletin rule (GDD §5.1, §11): three-eyed clans need a blue stamp on the slip.</summary>
    public sealed class ClanStampRule : IDeskRule
    {
        public Clan Clan = Clan.Ammaru;
        public string Id => "clan-stamp";
        public string RulebookText => $"{Clan} (three-eyed) visitors need a BLUE stamp on their slip.";
        public bool IsViolated(VisitorInstance v, ShiftContext ctx) => v.Id.Clan == Clan && !v.Slip.BlueStamp;
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

        /// <summary>Facts that only apply once a tool or paper is in play.</summary>
        public static IEnumerable<string> ExtraFacts(ShiftContext ctx)
        {
            if (ctx.Has(Tools.UvLamp)) yield return "Under the UV lamp a genuine hologram glows with rings.";
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
            switch (id)
            {
                case "expired": return new ExpiredIdRule();
                case "floor-access": return new FloorAccessRule();
                case "floor-closed": return new FloorClosedRule();
                case "clan-stamp": return new ClanStampRule();
                default: return null;
            }
        }
    }
}
