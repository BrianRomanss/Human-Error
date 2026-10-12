using System;
using System.Collections.Generic;
using System.Linq;
using ChuchuGames.Core;

namespace HumanError.Model
{
    /// <summary>A scripted tutorial visitor: the generator is searched until a visitor of this kind shows these tells.</summary>
    public sealed class TutorialVisitor
    {
        public VisitorKind Kind;
        /// <summary>Tells the visitor must have (the tips point at them); others may come along.</summary>
        public List<Tell> Tells = new List<Tell>();
        public Violation Violation;

        public bool Matches(VisitorInstance v) =>
            v.Truth.Kind == Kind && Tells.All(v.Has) && (Violation == Violation.None || v.Truth.Violation == Violation);

        /// <summary>The decision the tutorial teaches for this visitor.</summary>
        public Decision Decision =>
            Kind == VisitorKind.ClumsyHuman || Kind == VisitorKind.SkilledHuman ? Decision.Security
            : Kind == VisitorKind.RuleBreakerCitizen ? Decision.TurnAway : Decision.LetIn;
    }

    /// <summary>One tip: what to point at, what to say, and what the player must do to move on.</summary>
    public sealed class TutorialStep
    {
        /// <summary>1-based visitor the step belongs to: it starts once that visitor is at the window.</summary>
        public int Visitor;
        public string Target;
        public string Wait;
        public string Text;

        public bool IsTap => Wait == Tutorial.Tap;
        public bool IsDecision => Tutorial.DecisionOf(Wait).HasValue;
    }

    /// <summary>
    /// The guided first night: scripted visitors at the front of the queue and spotlight tips over the desk.
    /// The desk view maps <see cref="Targets"/> to its own panels; everything else is plain data.
    /// </summary>
    public sealed class Tutorial
    {
        public const string Tap = "tap", Ask = "ask", LetIn = "letin", TurnAway = "away", Security = "security";
        public static readonly string[] Waits = { Tap, Ask, LetIn, TurnAway, Security };
        public static readonly string[] Targets =
            { "window", "topbar", "id", "slip", "questions", "log", "bulletin", "rulebook", "decisions", "letin", "away", "security" };

        public int Shift;
        public List<TutorialVisitor> Visitors = new List<TutorialVisitor>();
        public List<TutorialStep> Steps = new List<TutorialStep>();

        /// <summary>A panel name, or "question:id" for one question button.</summary>
        public static bool IsTarget(string target, ShiftContext ctx) =>
            Array.IndexOf(Targets, target) >= 0 || QuestionIn(target, "question:", ctx) != null;

        /// <summary>A wait name, or "ask:id" for one particular question.</summary>
        public static bool IsWait(string wait, ShiftContext ctx) =>
            Array.IndexOf(Waits, wait) >= 0 || QuestionIn(wait, "ask:", ctx) != null;

        /// <summary>The question id after <paramref name="prefix"/>, if it's on tonight's list.</summary>
        public static string QuestionIn(string value, string prefix, ShiftContext ctx)
        {
            if (value == null || !value.StartsWith(prefix)) return null;
            var id = value.Substring(prefix.Length);
            return ctx == null || ctx.Questions.Any(q => q.Id == id) ? id : null;
        }

        /// <summary>Does asking <paramref name="questionId"/> satisfy this wait?</summary>
        public static bool AskSatisfies(string wait, string questionId) => wait == Ask || wait == "ask:" + questionId;

        public static Decision? DecisionOf(string wait)
        {
            switch (wait)
            {
                case LetIn: return Decision.LetIn;
                case TurnAway: return Decision.TurnAway;
                case Security: return Decision.Security;
                default: return null;
            }
        }

        /// <summary>Index of the first step for the visitor at 0-based queue position <paramref name="visitorIndex"/> or later.</summary>
        public int FirstStepFrom(int visitorIndex)
        {
            int i = Steps.FindIndex(s => s.Visitor >= visitorIndex + 1);
            return i < 0 ? Steps.Count : i;
        }

        /// <summary>
        /// Puts the scripted visitors first. Each replaces a generated visitor of the same kind, so the shift's
        /// mix (and pay) stays as the content says. Deterministic for a seed.
        /// </summary>
        public List<VisitorInstance> Arrange(List<VisitorInstance> queue, ShiftConfig config, ShiftContext ctx, ulong seed)
        {
            var rest = queue.ToList();
            var names = new HashSet<string>(rest.Select(v => v.Truth.TrueName));
            var front = new List<VisitorInstance>();
            for (int n = 0; n < Visitors.Count; n++)
            {
                var spec = Visitors[n];
                int replace = rest.FindIndex(v => v.Truth.Kind == spec.Kind);
                if (replace >= 0)
                {
                    names.Remove(rest[replace].Truth.TrueName);
                    rest.RemoveAt(replace);
                }
                VisitorInstance found = null;
                for (ulong k = 0; k < 20000 && found == null; k++)
                {
                    var v = VisitorGenerator.Create(spec.Kind, config, ctx, new SeededRandom(seed * 7919UL + (ulong)n * 100003UL + k));
                    if (spec.Matches(v) && !names.Contains(v.Truth.TrueName)) found = v;
                }
                if (found == null) throw new InvalidOperationException($"No {spec.Kind} with {string.Join("+", spec.Tells)} on shift {ctx.ShiftNumber}");
                names.Add(found.Truth.TrueName);
                front.Add(found);
            }
            var result = front.Concat(rest).ToList();
            for (int i = 0; i < result.Count; i++) result[i].Index = i;
            return result;
        }
    }
}
