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
        /// <summary>Story characters who may come tonight (each visit has its own conditions).</summary>
        public List<StoryVisit> Story = new List<StoryVisit>();

        /// <summary>Tonight's context plus tools the player owns (bought at Home).</summary>
        public ShiftContext ContextWith(ulong seed, IEnumerable<string> ownedTools)
        {
            var ctx = Context(seed);
            foreach (var t in ownedTools ?? Enumerable.Empty<string>()) ctx.Tools.Add(t);
            // The 4th question upgrade (GDD §5.6): one more question per visitor, if tonight's list has room.
            if (ctx.Has(Tools.FourthQuestion)) ctx.QuestionBudget = Math.Min(ctx.Questions.Count, ctx.QuestionBudget + 1);
            return ctx;
        }

        /// <param name="save">The campaign so far: decides which story visitors come (null = a fresh game).</param>
        public ShiftSession NewSession(ulong seed, IEnumerable<string> ownedTools = null, SaveData save = null)
        {
            var ctx = ContextWith(seed, ownedTools);
            ctx.Silent = save != null && save.Has(Campaign.SilentFlag);
            return new ShiftSession(ctx, Queue(ctx, seed, StoryCast.Tonight(Story, save)));
        }

        public List<VisitorInstance> Queue(ShiftContext ctx, ulong seed, IList<StoryVisit> story)
        {
            var queue = VisitorGenerator.Generate(Config, ctx, seed);
            if (Tutorial != null) queue = Tutorial.Arrange(queue, Config, ctx, seed);
            return StoryCast.Place(queue, story, Config, ctx, seed, Tutorial?.Visitors.Count ?? 0);
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
            Clan? wanted = null;
            if (!string.IsNullOrEmpty(s.wanted))
            {
                if (Enum.TryParse(s.wanted, out Clan w)) wanted = w;
                else Errors.Add($"{where}: unknown wanted clan '{s.wanted}'");
            }

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
                Timed = !s.untimed,
                WantedClan = wanted,
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
            if (f.Errors.Count == 0) f.StoryFrom(file?.story, game);
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
                // After the last shift comes the ending, not an evening at Home.
                if (n.after < 0 || n.after >= lastShift) Errors.Add($"{where}: there is no evening after shift {n.after}");
                Tokens(where, n.when);
            }
            // Every evening needs a full front page whatever happened, so reactions can only push fillers out.
            for (int after = 0; after < lastShift; after++)
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
                if (m.after < HomeContent.AnyEvening || m.after >= lastShift) Errors.Add($"{where}: there is no evening after shift {m.after}");
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

        void StoryFrom(StoryData[] story, GameContent game)
        {
            var ids = new HashSet<string>();
            foreach (var c in story ?? new StoryData[0])
            {
                if (c == null) continue;
                string who = $"story '{c.id}'";
                if (string.IsNullOrEmpty(c.id) || c.id.Any(ch => !char.IsLetter(ch) && ch != '-')) { Errors.Add($"{who}: ids are letters and dashes"); continue; }
                if (!ids.Add(c.id)) Errors.Add($"duplicate story id '{c.id}'");
                if (string.IsNullOrWhiteSpace(c.name)) Errors.Add($"{who}: no name");
                int number = 0, count = 0;
                foreach (var d in c.visits ?? new StoryVisitData[0])
                {
                    if (d == null) continue;
                    count++;
                    number = d.visit > 0 ? d.visit : number + 1;
                    var visit = StoryVisit(c, d, number, game);
                    if (visit != null) game.Shift(d.shift).Story.Add(visit);
                }
                if (count == 0) Errors.Add($"{who}: no visits");
            }
            if (Errors.Count > 0) return;

            // Worst case every visit's conditions hold: there must be enough generated visitors of each kind to replace,
            // and every story visitor must be fair (findable tells, papers that match their kind).
            foreach (var plan in game.Shifts.Where(p => p.Story.Count > 0))
            {
                string where = $"story on shift {plan.Data.number}";
                int front = plan.Tutorial?.Visitors.Count ?? 0;
                foreach (var v in plan.Story.Where(v => v.Position <= front))
                    Errors.Add($"{where}: {v.Key} at position {v.Position} would stand among the tutorial's scripted visitors");
                for (ulong seed = 1; seed <= 6; seed++)
                {
                    var ctx = plan.Context(seed);
                    List<VisitorInstance> queue;
                    // Alternatives for the same visit exclude each other: check one of each.
                    var worst = plan.Story.GroupBy(v => v.Key).Select(g => g.First()).OrderBy(v => v.Position).ToList();
                    try { queue = plan.Queue(ctx, seed, worst); }
                    catch (InvalidOperationException e) { Errors.Add($"{where}: {e.Message}"); break; }
                    foreach (var v in queue.Where(v => v.Story != null))
                        foreach (var problem in Fairness.Problems(v, ctx))
                            Errors.Add($"{where}: {v.Story.Key} is unfair: {problem}");
                }
            }
        }

        StoryVisit StoryVisit(StoryData c, StoryVisitData d, int number, GameContent game)
        {
            string at = $"story '{c.id}' visit {number}";
            var plan = game.Shift(d.shift);
            if (plan == null) { Errors.Add($"{at}: there is no shift {d.shift}"); return null; }
            int before = Errors.Count;
            var ctx = plan.Context(1);
            if (!Enum.TryParse(d.kind, out VisitorKind kind)) Errors.Add($"{at}: unknown visitor kind '{d.kind}'");
            bool human = kind == VisitorKind.ClumsyHuman || kind == VisitorKind.SkilledHuman;
            if (d.position < 1 || d.position > plan.Config.Visitors) Errors.Add($"{at}: position must be 1–{plan.Config.Visitors}");

            var spec = new VisitorSpec { Kind = kind, Name = string.IsNullOrEmpty(d.alias) ? c.name : d.alias, Greeting = d.greeting };
            if (d.look != null && !string.IsNullOrEmpty(d.look.clan))
            {
                var l = d.look;
                if (!Enum.TryParse(l.clan, out Clan clan)) Errors.Add($"{at}: unknown clan '{l.clan}'");
                foreach (var slot in new[] { l.head, l.mouth, l.pattern, l.outfit })
                    if (slot < 0 || slot >= Look.Options) Errors.Add($"{at}: look slots are 0–{Look.Options - 1}");
                if (l.tone < 0 || l.tone > 1) Errors.Add($"{at}: tone is 0 or 1");
                spec.Look = new Look { Clan = clan, Head = l.head, Mouth = l.mouth, Pattern = l.pattern, Outfit = l.outfit, Tone = l.tone };
            }
            var available = VisitorGenerator.AvailableTells(ctx);
            if ((d.tells?.Length ?? 0) > 0)
            {
                if (!human) Errors.Add($"{at}: only humans have tells");
                spec.Tells = new List<Tell>();
                foreach (var name in d.tells)
                {
                    if (!Enum.TryParse(name, out Tell t)) Errors.Add($"{at}: unknown tell '{name}'");
                    else if (!available.Contains(t)) Errors.Add($"{at}: tell {t} can't be found on shift {d.shift}");
                    else spec.Tells.Add(t);
                }
                if (spec.Tells.Count(t => Tells.Category(t) == TellCategory.Document) > plan.Config.MaxForgedFields)
                    Errors.Add($"{at}: more forged fields than shift {d.shift} allows");
            }
            if (!string.IsNullOrEmpty(d.violation))
            {
                if (!Enum.TryParse(d.violation, out Violation v)) Errors.Add($"{at}: unknown violation '{d.violation}'");
                else if (kind != VisitorKind.RuleBreakerCitizen) Errors.Add($"{at}: only rule-breakers have a violation");
                else if (!VisitorGenerator.ViolationsAllowed(ctx).Contains(v)) Errors.Add($"{at}: {v} isn't possible on shift {d.shift}");
                else spec.Violation = v;
            }
            if (!string.IsNullOrEmpty(d.quirk))
            {
                if (!Enum.TryParse(d.quirk, out Quirk q)) Errors.Add($"{at}: unknown quirk '{d.quirk}'");
                else if (kind != VisitorKind.OddCitizen) Errors.Add($"{at}: only odd citizens have a quirk");
                else if (!VisitorGenerator.QuirksAllowed(ctx).Contains(q)) Errors.Add($"{at}: {q} isn't possible on shift {d.shift}");
                else spec.Quirk = q;
            }
            if (!string.IsNullOrEmpty(d.motive))
            {
                if (!Enum.TryParse(d.motive, out Motive m)) Errors.Add($"{at}: unknown motive '{d.motive}'");
                else spec.Motive = m;
            }
            if (!string.IsNullOrEmpty(d.greeting))
            {
                // The greeting is evidence too (GDD §4): it must agree with the truth.
                bool smallTalk = spec.Tells != null && spec.Tells.Contains(Tell.SmallTalkGreeting);
                bool mood = d.greeting.Contains("%");
                if (smallTalk && mood) Errors.Add($"{at}: a small-talk greeter's greeting states a mood");
                if (!smallTalk && !mood) Errors.Add($"{at}: the greeting must state a mood (\"I am 60% …\") unless they greet with small talk");
                if (kind == VisitorKind.OddCitizen) Errors.Add($"{at}: odd citizens explain their medical pass on arrival; leave the greeting out");
            }
            foreach (var t in d.when ?? new string[0])
                if (!Conditions.IsKnown(t)) Errors.Add($"{at}: unknown condition '{t}'");
            if (Errors.Count > before) return null;

            var visit = new StoryVisit
            {
                StoryId = c.id,
                StoryName = c.name,
                Number = number,
                Shift = d.shift,
                Position = d.position,
                Spec = spec,
                When = d.when ?? new string[0],
                Note = d.note,
            };
            void Line(Decision dec, string text) { if (!string.IsNullOrWhiteSpace(text)) visit.Lines[dec] = text; }
            Line(Decision.LetIn, d.letin);
            Line(Decision.TurnAway, d.away);
            Line(Decision.Security, d.security);
            Line(Decision.Silent, d.silent);
            return visit;
        }

        static string Short(string text) => text == null ? "" : text.Length <= 30 ? text : text.Substring(0, 30) + "…";
    }
}
