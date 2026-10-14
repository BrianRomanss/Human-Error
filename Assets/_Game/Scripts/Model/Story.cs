using System;
using System.Collections.Generic;
using System.Linq;
using ChuchuGames.Core;

namespace HumanError.Model
{
    /// <summary>
    /// What a visitor must be. Empty fields are rolled by the generator as usual, so a spec can fix just a
    /// face and a name (Duun) or a whole disguise down to its tells (Mira in her second suit).
    /// </summary>
    public sealed class VisitorSpec
    {
        public VisitorKind Kind;
        public Look Look;
        /// <summary>Name on the papers (an alias, for humans).</summary>
        public string Name;
        /// <summary>Humans: exactly these tells (null = rolled by kind).</summary>
        public List<Tell> Tells;
        public Violation Violation;
        public Quirk Quirk;
        public Motive Motive;
        /// <summary>Arrival line (null = generated). Must state a mood unless the visitor greets with small talk.</summary>
        public string Greeting;
    }

    /// <summary>
    /// One appearance of a recurring character (GDD §6, §11 StoryVisitorSO: fixed truth, fixed look, dialogue per
    /// visit, flags set by your decision). The decision sets the flag "<see cref="Key"/>:letin|away|security|silent".
    /// </summary>
    public sealed class StoryVisit
    {
        public string StoryId;
        /// <summary>The character's real name, shown on the Shift Report.</summary>
        public string StoryName;
        /// <summary>1-based visit number of this character.</summary>
        public int Number;
        public int Shift;
        /// <summary>1-based place in the queue.</summary>
        public int Position;
        public VisitorSpec Spec;
        /// <summary>Condition tokens (see <see cref="Conditions"/>) that must hold for the visit to happen.</summary>
        public string[] When = new string[0];
        /// <summary>What they say as you decide, by decision (missing = nothing).</summary>
        public readonly Dictionary<Decision, string> Lines = new Dictionary<Decision, string>();
        /// <summary>One line about who they really are, for the reveal and the Shift Report.</summary>
        public string Note;

        public string Key => StoryId + Number;
        public static string Flag(string key, Decision d) => $"{key}:{DecisionName(d)}";
        public string FlagFor(Decision d) => Flag(Key, d);

        public string LineFor(Decision d) => Lines.TryGetValue(d, out var l) ? l : null;

        public static string DecisionName(Decision d)
        {
            switch (d)
            {
                case Decision.LetIn: return "letin";
                case Decision.TurnAway: return "away";
                case Decision.Silent: return "silent";
                default: return "security";
            }
        }

        public static Decision? ParseDecision(string s)
        {
            switch (s)
            {
                case "letin": return Decision.LetIn;
                case "away": return Decision.TurnAway;
                case "security": return Decision.Security;
                case "silent": return Decision.Silent;
                default: return null;
            }
        }
    }

    /// <summary>Puts tonight's story visitors into a generated queue.</summary>
    public static class StoryCast
    {
        /// <summary>Visits for this shift whose conditions hold for the save (a null save = a fresh game).</summary>
        public static List<StoryVisit> Tonight(IEnumerable<StoryVisit> visits, SaveData save) =>
            (visits ?? Enumerable.Empty<StoryVisit>())
            .Where(v => Conditions.All(v.When, save ?? new SaveData()))
            .OrderBy(v => v.Position).ToList();

        /// <summary>
        /// Each story visitor replaces a generated visitor of the same kind (so the shift's mix and pay stay as the
        /// content says) and stands at its position. Scripted tutorial visitors at the front stay put.
        /// </summary>
        public static List<VisitorInstance> Place(List<VisitorInstance> queue, IList<StoryVisit> visits, ShiftConfig config,
            ShiftContext ctx, ulong seed, int keepFront = 0)
        {
            if (visits == null || visits.Count == 0) return queue;
            var rest = queue.ToList();
            var names = new HashSet<string>(rest.Select(v => v.Truth.TrueName));
            var placed = new List<(int pos, VisitorInstance v)>();
            for (int n = 0; n < visits.Count; n++)
            {
                var visit = visits[n];
                int replace = -1;
                for (int i = rest.Count - 1; i >= keepFront; i--)
                    if (rest[i].Truth.Kind == visit.Spec.Kind && rest[i].Story == null) { replace = i; break; }
                if (replace < 0) throw new InvalidOperationException($"shift {ctx.ShiftNumber} has no {visit.Spec.Kind} left for {visit.Key}");
                names.Remove(rest[replace].Truth.TrueName);
                rest.RemoveAt(replace);
                var v = VisitorGenerator.Create(visit.Spec, config, ctx, new SeededRandom(seed * 6151UL + (ulong)n * 7919UL + 17UL), 0, names);
                v.Story = visit;
                placed.Add((visit.Position, v));
            }
            foreach (var (pos, v) in placed.OrderBy(p => p.pos))
                rest.Insert(Math.Max(keepFront, Math.Min(rest.Count, pos - 1)), v);
            for (int i = 0; i < rest.Count; i++) rest[i].Index = i;
            return rest;
        }
    }
}
