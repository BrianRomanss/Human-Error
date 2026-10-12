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
        /// <summary>The guided night, when this is the tutorial shift; its scripted visitors go first.</summary>
        public Tutorial Tutorial;

        /// <summary>Tonight's context plus tools the player owns (bought at Home).</summary>
        public ShiftContext ContextWith(ulong seed, IEnumerable<string> ownedTools)
        {
            var ctx = Context(seed);
            foreach (var t in ownedTools ?? Enumerable.Empty<string>()) ctx.Tools.Add(t);
            return ctx;
        }

        public ShiftSession NewSession(ulong seed, IEnumerable<string> ownedTools = null)
        {
            var ctx = ContextWith(seed, ownedTools);
            var queue = VisitorGenerator.Generate(Config, ctx, seed);
            if (Tutorial != null) queue = Tutorial.Arrange(queue, Config, ctx, seed);
            return new ShiftSession(ctx, queue);
        }
    }

    /// <summary>Everything content.json defines, validated.</summary>
    public sealed class GameContent
    {
        public List<ShiftPlan> Shifts = new List<ShiftPlan>();
        public HomeContent Home = new HomeContent();
        /// <summary>The authored questions, for building Endless and Daily shifts.</summary>
        public QuestionData[] QuestionData = new QuestionData[0];

        public ShiftPlan Shift(int number) => Shifts.FirstOrDefault(p => p.Data.number == number);
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
                RevealEachVisitor = s.revealEach,
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
        public static List<ShiftPlan> Load(ContentFile file, out List<string> errors) => LoadGame(file, out errors).Shifts;

        /// <summary>Validates a whole content file: shifts, questions and the Home content.</summary>
        public static GameContent LoadGame(ContentFile file, out List<string> errors)
        {
            var f = new ContentFactory(file?.questions);
            var game = new GameContent { QuestionData = file?.questions ?? new QuestionData[0] };
            var numbers = new HashSet<int>();
            foreach (var s in (file?.shifts ?? new ShiftData[0]).OrderBy(s => s.number))
            {
                if (!numbers.Add(s.number)) f.Errors.Add($"duplicate shift number {s.number}");
                game.Shifts.Add(f.Shift(s));
            }
            if (game.Shifts.Count == 0) f.Errors.Add("no shifts");
            game.Home = f.HomeFrom(file, numbers.Count == 0 ? 0 : numbers.Max());
            if (file?.tutorial != null && file.tutorial.shift > 0) f.TutorialFrom(file.tutorial, game);
            errors = f.Errors.Distinct().ToList();
            return game;
        }

        HomeContent HomeFrom(ContentFile file, int lastShift)
        {
            var home = new HomeContent
            {
                News = (file?.news ?? new NewsData[0]).Where(n => n != null).ToList(),
                Messages = (file?.messages ?? new MessageData[0]).Where(m => m != null).ToList(),
                Upgrades = (file?.upgrades ?? new UpgradeData[0]).Where(u => u != null).ToList(),
            };
            void Tokens(string where, string[] when)
            {
                foreach (var t in when ?? new string[0])
                    if (!Conditions.IsKnown(t)) Errors.Add($"{where}: unknown condition '{t}'");
            }

            foreach (var n in home.News)
            {
                n.when = n.when ?? new string[0];
                string where = $"news after shift {n.after} \"{Short(n.text)}\"";
                if (string.IsNullOrWhiteSpace(n.text)) Errors.Add($"news after shift {n.after}: no text");
                if (n.after < 0 || n.after > lastShift) Errors.Add($"{where}: there is no shift {n.after}");
                Tokens(where, n.when);
            }
            // Every evening needs a full front page whatever happened, so reactions can only push fillers out.
            for (int after = 0; after <= lastShift; after++)
            {
                int always = home.News.Count(n => n.after == after && n.when.Length == 0);
                if (always < HomeContent.NewsPerNight)
                    Errors.Add($"news after shift {after}: {always} unconditional headlines (need {HomeContent.NewsPerNight})");
            }

            var ids = new HashSet<string>();
            foreach (var m in home.Messages)
            {
                m.when = m.when ?? new string[0];
                m.choices = m.choices ?? new ChoiceData[0];
                string where = $"message '{m.id}'";
                if (string.IsNullOrEmpty(m.id)) Errors.Add("message without an id");
                else if (!ids.Add(m.id)) Errors.Add($"duplicate message id '{m.id}'");
                if (string.IsNullOrWhiteSpace(m.from)) Errors.Add($"{where}: no sender");
                if (string.IsNullOrWhiteSpace(m.text)) Errors.Add($"{where}: no text");
                if (m.after < HomeContent.AnyEvening || m.after > lastShift) Errors.Add($"{where}: there is no shift {m.after}");
                if (m.choices.Length > 3) Errors.Add($"{where}: at most 3 choices");
                foreach (var c in m.choices)
                    if (c == null || string.IsNullOrWhiteSpace(c.text)) Errors.Add($"{where}: a choice without text");
                Tokens(where, m.when);
            }

            var upgradeIds = new HashSet<string>();
            foreach (var u in home.Upgrades)
            {
                string where = $"upgrade '{u.id}'";
                if (string.IsNullOrEmpty(u.id)) Errors.Add("upgrade without an id");
                else if (!upgradeIds.Add(u.id)) Errors.Add($"duplicate upgrade id '{u.id}'");
                if (string.IsNullOrWhiteSpace(u.name)) Errors.Add($"{where}: no name");
                if (u.price <= 0) Errors.Add($"{where}: price must be above 0");
                if (u.unlockShift < 1) Errors.Add($"{where}: unlockShift must be 1 or more");
                if (Array.IndexOf(Tools.All, u.tool) < 0) Errors.Add($"{where}: unknown tool '{u.tool}'");
            }
            return home;
        }

        void TutorialFrom(TutorialData data, GameContent game)
        {
            const string where = "tutorial";
            var plan = game.Shift(data.shift);
            if (plan == null) { Errors.Add($"{where}: there is no shift {data.shift}"); return; }
            int before = Errors.Count;
            var ctx = plan.Context(1);
            var available = VisitorGenerator.AvailableTells(ctx);
            var t = new Tutorial { Shift = data.shift };
            foreach (var v in data.visitors ?? new TutorialVisitorData[0])
            {
                if (v == null || !Enum.TryParse(v.kind, out VisitorKind kind)) { Errors.Add($"{where}: unknown visitor kind '{v?.kind}'"); continue; }
                var spec = new TutorialVisitor { Kind = kind };
                foreach (var name in v.tells ?? new string[0])
                {
                    if (!Enum.TryParse(name, out Tell tell)) Errors.Add($"{where}: unknown tell '{name}'");
                    else if (!available.Contains(tell)) Errors.Add($"{where}: tell {tell} can't be found on shift {data.shift}");
                    else spec.Tells.Add(tell);
                }
                if (!string.IsNullOrEmpty(v.violation))
                {
                    if (Enum.TryParse(v.violation, out Violation violation)) spec.Violation = violation;
                    else Errors.Add($"{where}: unknown violation '{v.violation}'");
                }
                t.Visitors.Add(spec);
            }
            int Count(VisitorKind k)
            {
                var c = plan.Config;
                switch (k)
                {
                    case VisitorKind.ClumsyHuman: return c.Humans - (int)Math.Round(c.Humans * c.SkilledShare);
                    case VisitorKind.SkilledHuman: return (int)Math.Round(c.Humans * c.SkilledShare);
                    case VisitorKind.RuleBreakerCitizen: return c.RuleBreakers;
                    case VisitorKind.OddCitizen: return c.OddCitizens;
                    default: return c.Visitors - c.Humans - c.RuleBreakers - c.OddCitizens;
                }
            }
            foreach (var g in t.Visitors.GroupBy(v => v.Kind))
                if (g.Count() > Count(g.Key)) Errors.Add($"{where}: {g.Count()} scripted {g.Key} but shift {data.shift} only has {Count(g.Key)}");

            int last = 0;
            var decided = new HashSet<int>();
            foreach (var s in data.steps ?? new TutorialStepData[0])
            {
                if (s == null) continue;
                string at = $"{where} step for visitor {s.visitor}";
                if (!Tutorial.IsTarget(s.target, ctx)) Errors.Add($"{at}: unknown target '{s.target}'");
                if (!Tutorial.IsWait(s.wait, ctx)) Errors.Add($"{at}: unknown wait '{s.wait}'");
                if (string.IsNullOrWhiteSpace(s.text)) Errors.Add($"{at}: no text");
                if (s.visitor < 1 || s.visitor > t.Visitors.Count + 1) Errors.Add($"{at}: visitor must be 1–{t.Visitors.Count + 1}");
                if (s.visitor < last) Errors.Add($"{at}: steps must go in visitor order");
                if (decided.Contains(s.visitor)) Errors.Add($"{at}: comes after that visitor's decision");
                var d = Tutorial.DecisionOf(s.wait);
                if (d.HasValue)
                {
                    decided.Add(s.visitor);
                    if (s.visitor > t.Visitors.Count) Errors.Add($"{at}: only scripted visitors can have a decision step");
                    else if (t.Visitors[s.visitor - 1].Decision != d.Value)
                        Errors.Add($"{at}: teaches {s.wait} but a {t.Visitors[s.visitor - 1].Kind} needs {t.Visitors[s.visitor - 1].Decision}");
                }
                last = Math.Max(last, s.visitor);
                t.Steps.Add(new TutorialStep { Visitor = s.visitor, Target = s.target, Wait = s.wait, Text = s.text });
            }
            if (t.Steps.Count == 0) Errors.Add($"{where}: no steps");
            if (Errors.Count > before) return;
            try { t.Arrange(VisitorGenerator.Generate(plan.Config, ctx, 1), plan.Config, ctx, 1); }
            catch (InvalidOperationException e) { Errors.Add($"{where}: {e.Message}"); return; }
            plan.Tutorial = t;
        }

        static string Short(string text) => text == null ? "" : text.Length <= 30 ? text : text.Substring(0, 30) + "…";
    }
}
