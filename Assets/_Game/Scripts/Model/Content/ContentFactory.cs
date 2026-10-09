using System;
using System.Collections.Generic;
using System.Linq;
using ChuchuGames.Core;

namespace HumanError.Model.Content
{
    /// <summary>A shift ready to play: tonight's context plus the queue mix.</summary>
    public sealed class ShiftPlan
    {
        public ShiftData Data;
        public ShiftConfig Config;
        /// <summary>Builds tonight's context; the seed only picks things the data leaves open (e.g. Kesh).</summary>
        public Func<ulong, ShiftContext> Context;

        public ShiftSession NewSession(ulong seed)
        {
            var ctx = Context(seed);
            return new ShiftSession(ctx, VisitorGenerator.Generate(Config, ctx, seed));
        }
    }

    /// <summary>
    /// Authoring data → model objects, collecting readable errors instead of throwing, so the importer can
    /// refuse to write anything while content is broken.
    /// </summary>
    public sealed class ContentFactory
    {
        public const int MaxQuestionsPerShift = 8;

        readonly Dictionary<string, QuestionDef> _questions = new Dictionary<string, QuestionDef>();
        public List<string> Errors { get; } = new List<string>();

        public ContentFactory(IEnumerable<QuestionData> questions)
        {
            foreach (var q in questions ?? Enumerable.Empty<QuestionData>())
            {
                if (q == null || string.IsNullOrEmpty(q.id)) { Errors.Add("question without an id"); continue; }
                if (_questions.ContainsKey(q.id)) { Errors.Add($"duplicate question id '{q.id}'"); continue; }
                var def = Question(q);
                if (def != null) _questions[q.id] = def;
            }
        }

        public IEnumerable<string> QuestionIds => _questions.Keys;
        public QuestionDef QuestionById(string id) => _questions.TryGetValue(id, out var q) ? q : null;

        QuestionDef Question(QuestionData q)
        {
            string where = $"question '{q.id}'";
            if (!Enum.TryParse(q.kind, out QuestionKind kind)) { Errors.Add($"{where}: unknown kind '{q.kind}'"); return null; }
            var effect = QuestionEffect.None;
            if (!string.IsNullOrEmpty(q.effect) && !Enum.TryParse(q.effect, out effect)) Errors.Add($"{where}: unknown effect '{q.effect}'");
            Tell? reveals = null;
            if (!string.IsNullOrEmpty(q.reveals))
            {
                if (Enum.TryParse(q.reveals, out Tell t)) reveals = t;
                else Errors.Add($"{where}: unknown tell '{q.reveals}'");
            }
            if (string.IsNullOrWhiteSpace(q.text)) Errors.Add($"{where}: no text");
            if (q.citizen == null || q.citizen.Length == 0) Errors.Add($"{where}: no citizen answers");
            bool verbal = reveals.HasValue && effect == QuestionEffect.None;
            if (verbal && (q.human == null || q.human.Length == 0)) Errors.Add($"{where}: reveals {reveals} but has no human answers");
            if (effect == QuestionEffect.ShowHand && reveals != Tell.FiveFingers) Errors.Add($"{where}: ShowHand questions reveal FiveFingers");
            if (effect == QuestionEffect.BreatheOnGlass && reveals != Tell.MouthBreathing) Errors.Add($"{where}: BreatheOnGlass questions reveal MouthBreathing");
            foreach (var line in (q.citizen ?? new string[0]).Concat(q.human ?? new string[0]))
                if (line.Contains("{") && Unknown(line) is string bad) Errors.Add($"{where}: unknown placeholder {bad}");
            return new QuestionDef
            {
                Id = q.id,
                Text = q.text,
                Kind = kind,
                Effect = effect,
                Reveals = reveals,
                CitizenAnswers = q.citizen,
                HumanAnswers = q.human,
                SlipChance = q.slipChance > 0 ? q.slipChance : 0.5f,
                StressDelta = q.stress > 0 ? q.stress : 0.1f,
                UnlockShift = Math.Max(1, q.unlockShift),
            };
        }

        static readonly string[] Placeholders =
        {
            "{mood}", "{floor}", "{office}", "{wrongFloor}", "{wrongOffice}", "{time}", "{wrongTime}", "{kesh}", "{wrongKesh}",
            "{date}", "{earthDate}", "{clan}", "{elder}", "{wrongElder}", "{greeting}", "{wrongGreeting}", "{home}", "{wrongHome}",
        };

        static string Unknown(string line)
        {
            int i = 0;
            while ((i = line.IndexOf('{', i)) >= 0)
            {
                int j = line.IndexOf('}', i);
                if (j < 0) return line.Substring(i);
                var token = line.Substring(i, j - i + 1);
                if (Array.IndexOf(Placeholders, token) < 0) return token;
                i = j + 1;
            }
            return null;
        }

        public ShiftPlan Shift(ShiftData s)
        {
            string where = $"shift {s.number}";
            var questions = new List<QuestionDef>();
            foreach (var id in s.questions ?? new string[0])
            {
                var q = QuestionById(id);
                if (q == null) Errors.Add($"{where}: unknown question '{id}'");
                else if (q.UnlockShift > s.number) Errors.Add($"{where}: question '{id}' unlocks at shift {q.UnlockShift}");
                else questions.Add(q);
            }
            if (questions.Count == 0) Errors.Add($"{where}: no questions");
            if (questions.Count > MaxQuestionsPerShift) Errors.Add($"{where}: {questions.Count} questions (the desk shows at most {MaxQuestionsPerShift})");

            HashSet<Tell> allowed = null;
            if (s.tells != null && s.tells.Length > 0)
            {
                allowed = new HashSet<Tell>();
                foreach (var name in s.tells)
                    if (Enum.TryParse(name, out Tell t)) allowed.Add(t);
                    else Errors.Add($"{where}: unknown tell '{name}'");
            }
            var rules = Rulebook.Standard();
            foreach (var id in s.rules ?? new string[0])
            {
                var r = Rulebook.Create(id);
                if (r == null) Errors.Add($"{where}: unknown rule '{id}'");
                else if (!rules.Any(x => x.Id == r.Id)) rules.Add(r);
            }
            foreach (var tool in s.tools ?? new string[0])
                if (Array.IndexOf(Tools.All, tool) < 0) Errors.Add($"{where}: unknown tool '{tool}'");
            foreach (var f in s.closedFloors ?? new int[0])
                if (Array.IndexOf(Floors.All, f) < 0) Errors.Add($"{where}: closed floor {f} doesn't exist");
            if ((s.closedFloors?.Length ?? 0) >= Floors.All.Length) Errors.Add($"{where}: every floor is closed");
            if (!string.IsNullOrEmpty(s.kesh) && Array.IndexOf(Kesh.Colours, s.kesh) < 0) Errors.Add($"{where}: unknown Kesh colour '{s.kesh}'");
            if (s.day < 0 || s.day > CycleDate.DaysPerCycle) Errors.Add($"{where}: day must be 1–{CycleDate.DaysPerCycle}");

            var config = new ShiftConfig
            {
                Visitors = s.visitors > 0 ? s.visitors : 8,
                Humans = s.humans,
                RuleBreakers = s.ruleBreakers,
                OddCitizens = s.oddCitizens,
                SkilledShare = s.skilledShare,
                ClumsyMinTells = s.clumsyMinTells > 0 ? s.clumsyMinTells : 2,
                ClumsyMaxTells = s.clumsyMaxTells > 0 ? s.clumsyMaxTells : 3,
                SkilledTells = s.skilledTells > 0 ? s.skilledTells : 1,
                MaxForgedFields = s.maxForgedFields > 0 ? s.maxForgedFields : 3,
            };
            if (config.Humans + config.RuleBreakers + config.OddCitizens > config.Visitors) Errors.Add($"{where}: more special visitors than visitors");
            if (config.ClumsyMinTells > config.ClumsyMaxTells) Errors.Add($"{where}: clumsyMinTells > clumsyMaxTells");

            var closed = (s.closedFloors ?? new int[0]).ToList();
            var tools = new HashSet<string>(s.tools ?? new string[0]);
            int budget = s.questionBudget > 0 ? s.questionBudget : 3;
            if (budget > questions.Count && questions.Count > 0) Errors.Add($"{where}: budget {budget} is more than the {questions.Count} questions");
            var date = new CycleDate(s.cycle > 0 ? s.cycle : 412, s.day > 0 && s.day <= CycleDate.DaysPerCycle ? s.day : 1);

            ShiftContext Make(ulong seed) => new ShiftContext
            {
                ShiftNumber = s.number,
                Title = s.title ?? "",
                Tonight = date,
                KeshColour = string.IsNullOrEmpty(s.kesh) ? new SeededRandom(seed ^ 0x5EED).Pick(Kesh.Colours) : s.kesh,
                ClosedFloors = closed.ToList(),
                QuestionBudget = budget,
                Questions = questions.ToList(),
                Rules = rules.ToList(),
                Tools = new HashSet<string>(tools),
                AllowedTells = allowed == null ? null : new HashSet<Tell>(allowed),
                Briefing = s.briefing ?? "",
                SupervisorLine = s.supervisor ?? "",
            };

            if (Errors.Count == 0)
            {
                // Content can be well-formed and still unfair: e.g. no tell can be found tonight.
                var probe = Make(1);
                if (config.Humans > 0 && VisitorGenerator.AvailableTells(probe).Count == 0) Errors.Add($"{where}: humans but no findable tells");
            }
            return new ShiftPlan { Data = s, Config = config, Context = Make };
        }

        /// <summary>Validates a whole content file and returns the playable shifts in order.</summary>
        public static List<ShiftPlan> Load(ContentFile file, out List<string> errors)
        {
            var f = new ContentFactory(file?.questions);
            var plans = new List<ShiftPlan>();
            var numbers = new HashSet<int>();
            foreach (var s in (file?.shifts ?? new ShiftData[0]).OrderBy(s => s.number))
            {
                if (!numbers.Add(s.number)) f.Errors.Add($"duplicate shift number {s.number}");
                plans.Add(f.Shift(s));
            }
            if (plans.Count == 0) f.Errors.Add("no shifts");
            errors = f.Errors.Distinct().ToList();
            return plans;
        }
    }
}
