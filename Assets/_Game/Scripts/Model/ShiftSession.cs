using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanError.Model
{
    /// <summary>One processed visitor, for the Shift Report.</summary>
    public sealed class VisitorResult
    {
        public VisitorInstance Visitor;
        public Verdict Verdict;
        public List<Answer> Asked;
    }

    /// <summary>
    /// A shift in progress (GDD §4): visitors one at a time, a question budget each, a soft clock, then a
    /// decision. Engine-free so the whole loop is unit-tested; the view only calls these methods.
    /// </summary>
    public sealed class ShiftSession
    {
        public const int ArriveMinutes = 3, QuestionMinutes = 4, DecideMinutes = 5;

        readonly List<VisitorInstance> _queue;
        readonly List<Answer> _asked = new List<Answer>();

        public ShiftContext Context { get; }
        public IReadOnlyList<VisitorInstance> Queue => _queue;
        public List<VisitorResult> Results { get; } = new List<VisitorResult>();
        public int Index { get; private set; }
        public int ClockMinutes { get; private set; }
        public float Stress { get; private set; }
        /// <summary>Pressure/trap questions asked of the current visitor (citizens complain at 2).</summary>
        public int Annoyance { get; private set; }
        public IReadOnlyList<Answer> Asked => _asked;

        /// <summary>Raised after every state change so the view can refresh.</summary>
        public event Action Changed;

        public ShiftSession(ShiftContext ctx, List<VisitorInstance> queue)
        {
            Context = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            BeginVisitor();
        }

        public VisitorInstance Current => IsOver ? null : _queue[Index];
        public int QuestionsLeft => Context.QuestionBudget - _asked.Count;
        public int Remaining => _queue.Count - Index;
        public bool IsOver => Index >= _queue.Count || ClockMinutes >= ShiftContext.ShiftLengthMinutes;
        public bool WasAsked(string questionId) => _asked.Any(a => a.QuestionId == questionId);

        public bool CanAsk(QuestionDef q) => !IsOver && QuestionsLeft > 0 && !WasAsked(q.Id);

        /// <summary>Fingers the visitor is currently showing (after an Action question), else 0.</summary>
        public int FingersShown => _asked.Select(a => a.FingersShown).DefaultIfEmpty(0).Max();

        /// <summary>The visitor breathed on the glass and it fogged.</summary>
        public bool Fogged => _asked.Any(a => a.Fog);

        public bool IsSweating => Current != null && Current.Has(Tell.Sweats) && Stress >= VisitorInstance.SweatThreshold;
        public bool IsFlickering => Current != null && Current.Has(Tell.SuitFlicker) && Stress >= VisitorInstance.FlickerThreshold;

        public Answer Ask(QuestionDef q)
        {
            if (!CanAsk(q)) throw new InvalidOperationException($"Can't ask {q.Id} now");
            var answer = Current.Answers[q.Id];
            _asked.Add(answer);
            if (Current.Truth.IsHuman) Stress = Math.Min(1f, Stress + q.StressDelta);
            if (q.Annoys) Annoyance++;
            ClockMinutes += QuestionMinutes;
            Changed?.Invoke();
            return answer;
        }

        public Verdict Decide(Decision d)
        {
            if (IsOver) throw new InvalidOperationException("The shift is over");
            var verdict = Judge.Decide(Current, Context, d, Annoyance);
            Results.Add(new VisitorResult { Visitor = Current, Verdict = verdict, Asked = _asked.ToList() });
            ClockMinutes += DecideMinutes;
            Index++;
            BeginVisitor();
            Changed?.Invoke();
            return verdict;
        }

        void BeginVisitor()
        {
            _asked.Clear();
            Annoyance = 0;
            if (IsOver) return;
            Stress = Current.BaseStress;
            ClockMinutes += ArriveMinutes;
        }

        // ------------------------------------------------------------ report

        public int Pay => Results.Where(r => r.Verdict.Shards > 0).Sum(r => r.Verdict.Shards);
        public int Penalties => Results.Where(r => r.Verdict.Shards < 0).Sum(r => r.Verdict.Shards);
        public int Net => Pay + Penalties;
        public int Strikes => Results.Count(r => r.Verdict.Strike);
        public int Correct => Results.Count(r => r.Verdict.IsGood);
        public int Complaints => Results.Count(r => r.Verdict.Complaint);
        public int HumansCaught => Results.Count(r => r.Visitor.Truth.IsHuman && r.Verdict.Decision == Decision.Security);
        public float Accuracy => Results.Count == 0 ? 0 : Correct / (float)Results.Count;

        /// <summary>Supervisor Graav's one-line note (GDD §7.2).</summary>
        public string SupervisorNote
        {
            get
            {
                if (Results.Any(r => r.Verdict.Outcome == Outcome.WrongfulArrest)) return "\"You arrested a citizen. Again and you're on the basement shift.\"";
                if (Results.Any(r => r.Verdict.Outcome == Outcome.InvaderInside)) return "\"Something got past window 3. Sloppy.\"";
                if (Accuracy >= 0.99f) return "\"Clean work. The Lattice notices.\"";
                return "\"Adequate. Read the rulebook.\"";
            }
        }
    }
}
