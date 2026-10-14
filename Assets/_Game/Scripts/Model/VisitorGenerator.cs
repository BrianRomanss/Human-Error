using System;
using System.Collections.Generic;
using System.Linq;
using ChuchuGames.Core;

namespace HumanError.Model
{
    /// <summary>How a shift's queue is mixed (GDD §6, §8: difficulty levers are data, not code).</summary>
    public sealed class ShiftConfig
    {
        public int Visitors = 8;
        public int Humans = 2;
        public int RuleBreakers = 1;
        /// <summary>Citizens with a medical-pass quirk (GDD §6 "odd citizen", punishes paranoia).</summary>
        public int OddCitizens = 0;
        /// <summary>Share of humans who are skilled (1 subtle tell) rather than clumsy.</summary>
        public float SkilledShare = 0.25f;
        public int ClumsyMinTells = 2;
        public int ClumsyMaxTells = 3;
        public int SkilledTells = 1;
        /// <summary>At most this many forged document fields per human (GDD §8: 0–3).</summary>
        public int MaxForgedFields = 3;
    }

    /// <summary>
    /// Builds a shift's visitors from a seed (GDD §11 VisitorGenerator): pick the truth → assemble the look
    /// → choose tells → generate documents (forging fields for the tells) → pre-compute every answer.
    /// The same seed and context always produce the same queue.
    /// </summary>
    public static class VisitorGenerator
    {
        /// <summary>Lowest starting stress of a human; used to decide whether stress tells can ever show.</summary>
        public const float MinHumanStress = 0.2f, MaxHumanStress = 0.35f;
        /// <summary>Share of humans in the wanted clan's suits when the bulletin names one.</summary>
        public const float WantedShare = 0.6f;

        public static List<VisitorInstance> Generate(ShiftConfig config, ShiftContext ctx, ulong seed)
        {
            var rng = new SeededRandom(seed);
            var kinds = new List<VisitorKind>();
            int humans = Math.Min(config.Humans, config.Visitors);
            int skilled = (int)Math.Round(humans * config.SkilledShare);
            for (int i = 0; i < humans; i++) kinds.Add(i < skilled ? VisitorKind.SkilledHuman : VisitorKind.ClumsyHuman);
            int breakers = Math.Min(config.RuleBreakers, config.Visitors - kinds.Count);
            for (int i = 0; i < breakers; i++) kinds.Add(VisitorKind.RuleBreakerCitizen);
            int odd = QuirksAllowed(ctx).Count == 0 ? 0 : Math.Min(config.OddCitizens, config.Visitors - kinds.Count);
            for (int i = 0; i < odd; i++) kinds.Add(VisitorKind.OddCitizen);
            while (kinds.Count < config.Visitors) kinds.Add(VisitorKind.OrdinaryCitizen);
            rng.Shuffle(kinds);

            var names = new HashSet<string>();
            var result = new List<VisitorInstance>();
            for (int i = 0; i < kinds.Count; i++)
                result.Add(Create(kinds[i], config, ctx, rng.Fork((ulong)i + 1), i, names));
            return result;
        }

        public static VisitorInstance Create(VisitorKind kind, ShiftConfig config, ShiftContext ctx, SeededRandom rng,
            int index = 0, HashSet<string> usedNames = null) =>
            Create(new VisitorSpec { Kind = kind }, config, ctx, rng, index, usedNames);

        /// <summary>A visitor from a spec: anything the spec fixes (story visitors' look, name, tells) is kept, the rest is rolled.</summary>
        public static VisitorInstance Create(VisitorSpec spec, ShiftConfig config, ShiftContext ctx, SeededRandom rng,
            int index = 0, HashSet<string> usedNames = null)
        {
            var kind = spec.Kind;
            bool human = kind == VisitorKind.ClumsyHuman || kind == VisitorKind.SkilledHuman;
            var truth = new VisitorTruth
            {
                Species = human ? Species.Human : Species.Citizen,
                Kind = kind,
                Motive = human ? rng.Pick(new[] { Motive.Refugee, Motive.Refugee, Motive.Runaway, Motive.Spy, Motive.LostTourist }) : Motive.None,
            };
            if (human && spec.Motive != Motive.None) truth.Motive = spec.Motive;

            if (human)
            {
                int count = kind == VisitorKind.SkilledHuman
                    ? config.SkilledTells
                    : rng.Range(config.ClumsyMinTells, config.ClumsyMaxTells + 1);
                truth.Tells = spec.Tells != null && spec.Tells.Count > 0
                    ? spec.Tells.ToList()
                    : PickTells(kind, Math.Max(1, count), config, ctx, rng);
            }
            else if (kind == VisitorKind.RuleBreakerCitizen)
            {
                var options = ViolationsAllowed(ctx);
                truth.Violation = spec.Violation != Violation.None ? spec.Violation : rng.Pick(options);
            }
            else if (kind == VisitorKind.OddCitizen)
            {
                var quirks = QuirksAllowed(ctx);
                truth.Quirk = spec.Quirk != Quirk.None ? spec.Quirk : quirks.Count > 0 ? rng.Pick(quirks) : Quirk.None;
            }

            var stampClan = ctx.Rules.OfType<ClanStampRule>().Select(r => (Clan?)r.Clan).FirstOrDefault();
            var clan = truth.Violation == Violation.MissingStamp && stampClan.HasValue ? stampClan.Value : rng.Pick(Clans.All);
            if (human && ctx.WantedClan.HasValue && rng.Chance(WantedShare)) clan = ctx.WantedClan.Value;
            truth.Look = new Look
            {
                Clan = clan,
                Head = rng.Range(0, Look.Options),
                Mouth = rng.Range(0, Look.Options),
                Pattern = rng.Range(0, Look.Options),
                Outfit = rng.Range(0, Look.Options),
                Tone = rng.Range(0, 2),
            };
            if (spec.Look != null) truth.Look = spec.Look.Clone();
            truth.TrueName = string.IsNullOrEmpty(spec.Name) ? Names.Make(truth.Look.Clan, rng, usedNames) : spec.Name;
            usedNames?.Add(truth.TrueName);

            var v = new VisitorInstance { Index = index, Truth = truth };
            MakeDocuments(v, ctx, rng, stampClan);
            foreach (var q in ctx.Questions) v.Answers[q.Id] = AnswerBook.Make(q, v, ctx, rng);
            v.Greeting = AnswerBook.Greeting(v, rng);
            if (!string.IsNullOrEmpty(spec.Greeting)) v.Greeting = spec.Greeting;
            v.BaseStress = human ? rng.Range(MinHumanStress, MaxHumanStress) : 0f;
            return v;
        }

        /// <summary>Ways a rule-breaker's papers can break tonight's rules.</summary>
        public static List<Violation> ViolationsAllowed(ShiftContext ctx)
        {
            var options = new List<Violation> { Violation.Expired, Violation.NoFloorAccess };
            if (ctx.ClosedFloors.Count > 0 && ctx.HasRule("floor-closed")) options.Add(Violation.FloorClosed);
            if (ctx.HasRule("clan-stamp")) options.Add(Violation.MissingStamp);
            if (ctx.HasRule("curfew")) options.Add(Violation.LateAppointment);
            return options;
        }

        public static List<Quirk> QuirksAllowed(ShiftContext ctx)
        {
            var q = new List<Quirk>();
            if (ctx.AllowedTells == null || ctx.AllowedTells.Contains(Tell.Blinks)) q.Add(Quirk.LidReflex);
            if (ctx.AllowedTells == null || ctx.AllowedTells.Contains(Tell.MouthBreathing)) q.Add(Quirk.GillRot);
            return q;
        }

        /// <summary>Highest stress a human can reach by spending the budget on the most stressful questions.</summary>
        public static float StressHeadroom(ShiftContext ctx) =>
            ctx.Questions.Select(q => q.StressDelta).OrderByDescending(d => d).Take(ctx.QuestionBudget).Sum();

        /// <summary>
        /// Tells the player can actually find tonight: introduced by the shift (AllowedTells), and with the
        /// question, tool or stress needed to see them (GDD §11 validator: "with the current tools and questions").
        /// </summary>
        public static List<Tell> AvailableTells(ShiftContext ctx)
        {
            var result = new List<Tell>();
            float maxStress = MinHumanStress + StressHeadroom(ctx);
            foreach (var t in Tells.All)
            {
                if (ctx.AllowedTells != null && !ctx.AllowedTells.Contains(t)) continue;
                bool findable;
                switch (t)
                {
                    case Tell.FiveFingers: findable = ctx.Questions.Any(q => q.Effect == QuestionEffect.ShowHand); break;
                    case Tell.FlatHologram: findable = ctx.Has(Tools.UvLamp); break;
                    case Tell.NoRecord: findable = ctx.Has(Tools.Archive); break;
                    // Still gills show when they breathe on the glass, or under the gill-scope.
                    case Tell.MouthBreathing:
                        findable = ctx.Has(Tools.GillScope) || ctx.Questions.Any(q => q.Effect == QuestionEffect.BreatheOnGlass);
                        break;
                    case Tell.Sweats: findable = maxStress >= VisitorInstance.SweatThreshold; break;
                    case Tell.SuitFlicker: findable = maxStress >= VisitorInstance.FlickerThreshold; break;
                    case Tell.SmallTalkGreeting: findable = true; break;
                    default:
                        findable = Tells.Category(t) != TellCategory.Verbal || ctx.Questions.Any(q => q.Reveals == t);
                        break;
                }
                if (findable) result.Add(t);
            }
            return result;
        }

        static List<Tell> PickTells(VisitorKind kind, int count, ShiftConfig config, ShiftContext ctx, SeededRandom rng)
        {
            var pool = AvailableTells(ctx);
            var slip = new Dictionary<Tell, float>();
            foreach (var q in ctx.Questions)
                if (q.Reveals.HasValue) slip[q.Reveals.Value] = Math.Max(slip.TryGetValue(q.Reveals.Value, out var s) ? s : 0, q.SlipChance);
            var chosen = new List<Tell>();
            while (chosen.Count < count && pool.Count > 0)
            {
                var weights = pool.Select(t =>
                {
                    int d = Tells.Difficulty(t);
                    // Clumsy humans give away the obvious things; skilled ones only the subtle ones.
                    float w = kind == VisitorKind.SkilledHuman ? d * d : 4 - d;
                    if (slip.TryGetValue(t, out var chance)) w *= 0.5f + chance;
                    return w;
                }).ToList();
                int i = rng.PickWeighted(weights);
                if (i < 0) break;
                chosen.Add(pool[i]);
                pool.RemoveAt(i);
                if (chosen.Count(t => Tells.Category(t) == TellCategory.Document) >= config.MaxForgedFields)
                    pool.RemoveAll(t => Tells.Category(t) == TellCategory.Document);
            }
            return chosen;
        }

        static void MakeDocuments(VisitorInstance v, ShiftContext ctx, SeededRandom rng, Clan? stampClan)
        {
            var t = v.Truth;
            var openFloors = Floors.All.Where(f => !ctx.ClosedFloors.Contains(f)).ToList();
            int slipFloor = t.Violation == Violation.FloorClosed ? rng.Pick(ctx.ClosedFloors) : rng.Pick(openFloors);

            var access = new List<int> { slipFloor };
            int extra = rng.Range(0, 3);
            foreach (var f in Floors.All.OrderBy(_ => rng.Value()))
                if (access.Count < 1 + extra && !access.Contains(f)) access.Add(f);
            if (t.Violation == Violation.NoFloorAccess)
            {
                access.Remove(slipFloor);
                if (access.Count == 0) access.Add(Floors.All.First(f => f != slipFloor));
            }
            access.Sort();

            var expiry = t.Violation == Violation.Expired
                ? ctx.Tonight.AddDays(-rng.Range(1, 14))
                : ctx.Tonight.AddDays(rng.Range(1, 40));

            var photo = t.Look.Clone();
            if (t.Has(Tell.PhotoMismatch))
            {
                // A different face: change one visible feature, never the clan (that would be a clan-rule case).
                switch (rng.Range(0, 3))
                {
                    case 0: photo.Head = (photo.Head + rng.Range(1, Look.Options)) % Look.Options; break;
                    case 1: photo.Mouth = (photo.Mouth + rng.Range(1, Look.Options)) % Look.Options; break;
                    default: photo.Pattern = (photo.Pattern + rng.Range(1, Look.Options)) % Look.Options; break;
                }
            }

            // Appointment between 20:00 and 01:45; under a curfew only before midnight (unless that's the rule they break).
            int minutes = !ctx.HasRule("curfew") ? rng.Range(0, 24) * 15
                : t.Violation == Violation.LateAppointment ? rng.Range(16, 24) * 15 : rng.Range(0, 16) * 15;
            v.Id = new IdCard
            {
                Name = t.TrueName,
                Clan = t.Look.Clan,
                FloorAccess = access,
                Expiry = expiry,
                Photo = photo,
                PhotoRoundPupils = t.Has(Tell.PhotoRoundPupils),
                HologramGenuine = !t.Has(Tell.FlatHologram),
                GlyphUpright = !t.Has(Tell.GlyphUpsideDown),
            };
            bool needsStamp = stampClan.HasValue && t.Look.Clan == stampClan.Value;
            v.Slip = new AppointmentSlip
            {
                Name = t.Has(Tell.NameMismatch) ? Names.Misspell(t.TrueName, rng) : t.TrueName,
                Floor = slipFloor,
                Time = ShiftContext.Clock(minutes),
                Date = ctx.Tonight,
                BlueStamp = t.Violation == Violation.MissingStamp ? false : needsStamp || rng.Chance(0.6f),
            };
            if (t.Has(Tell.EarthDate))
            {
                if (rng.Chance(0.5f)) v.Id.ExpiryStyle = DateStyle.Earth;
                else v.Slip.DateStyle = DateStyle.Earth;
            }
            if (t.Quirk != Quirk.None) v.Medical = new MedicalPass { Name = t.TrueName, Condition = t.Quirk };
            // The archive knows every real citizen, and the identities humans stole; invented ones aren't there.
            if (!t.Has(Tell.NoRecord))
                v.Record = new ArchiveRecord { Name = t.TrueName, Clan = t.Look.Clan, Photo = t.Look.Clone(), Registered = ctx.Tonight.AddDays(-rng.Range(200, 2000)) };
        }
    }

    /// <summary>Vel'Taran names: Given-CLAN Family, e.g. "ORR-VESSI TALM" (GDD §5.1).</summary>
    public static class Names
    {
        static readonly string[] Given =
        {
            "ORR", "KETT", "SAVA", "TEM", "PELL", "VAUN", "QUILL", "RESSA", "MOTT", "YELN", "BRIN", "ZARR",
            "OONA", "HESK", "TIBB", "LORU", "ANSE", "FENN", "IVRA", "DOSK",
        };
        static readonly string[] Family =
        {
            "TALM", "OURI", "VENN", "DRAA", "KOLO", "PHAE", "MURR", "ASKO", "TIRR", "SOLVE", "ENNIK", "VARRO",
        };

        public static string Make(Clan clan, SeededRandom rng, HashSet<string> used = null)
        {
            for (int attempt = 0; ; attempt++)
            {
                var name = $"{rng.Pick(Given)}-{clan.ToString().ToUpperInvariant()} {rng.Pick(Family)}";
                if (used == null || used.Add(name) || attempt > 50) return name;
            }
        }

        /// <summary>A forger's near-miss: one letter doubled, dropped or swapped (VESSI → VESI).</summary>
        public static string Misspell(string name, SeededRandom rng)
        {
            var letters = Enumerable.Range(0, name.Length).Where(i => char.IsLetter(name[i])).ToList();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int i = rng.Pick(letters);
                string result;
                switch (rng.Range(0, 3))
                {
                    case 0: result = name.Remove(i, 1); break;
                    case 1: result = name.Insert(i, name[i].ToString()); break;
                    default:
                        char c = "AEIOU".Contains(name[i]) ? rng.Pick(new[] { 'A', 'E', 'I', 'O', 'U' }) : rng.Pick(new[] { 'K', 'T', 'R', 'S', 'L', 'N' });
                        result = name.Substring(0, i) + c + name.Substring(i + 1);
                        break;
                }
                // Must still look like a name (no empty parts) and actually differ.
                if (result != name && !result.Contains("- ") && !result.StartsWith("-") && !result.EndsWith(" ")) return result;
            }
            return name + "E";
        }
    }

    /// <summary>
    /// "Always fair" (GDD §1, §11): every human has at least one tell the player can find tonight, and no
    /// citizen shows anything human that isn't explained by a medical pass. Run over thousands of seeds in
    /// tests and in the batch validator (Tools/Human Error/Validate Generator).
    /// </summary>
    public static class Fairness
    {
        public static List<string> Problems(VisitorInstance v, ShiftContext ctx)
        {
            var p = new List<string>();
            var t = v.Truth;
            var available = VisitorGenerator.AvailableTells(ctx);
            bool forged = v.Id.PhotoRoundPupils || !v.Id.Photo.SameFace(t.Look) || v.Slip.Name != v.Id.Name
                || v.Id.ExpiryStyle != DateStyle.Lattice || v.Slip.DateStyle != DateStyle.Lattice
                || !v.Id.HologramGenuine || !v.Id.GlyphUpright;

            if (t.IsHuman)
            {
                if (t.Tells.Count == 0) p.Add("human with no tells");
                foreach (var tell in t.Tells)
                    if (!available.Contains(tell)) p.Add($"tell {tell} can't be found tonight");
                if (Rulebook.BreaksAnyRule(v, ctx)) p.Add("human papers break a desk rule (should only show forgery tells)");
                if (v.Medical != null || t.Quirk != Quirk.None) p.Add("human with a medical pass");
                float max = v.BaseStress + VisitorGenerator.StressHeadroom(ctx);
                if (t.Has(Tell.Sweats) && max < VisitorInstance.SweatThreshold) p.Add("sweat can never appear");
                if (t.Has(Tell.SuitFlicker) && max < VisitorInstance.FlickerThreshold) p.Add("suit flicker can never appear");
                foreach (var q in ctx.Questions)
                {
                    bool shouldSlip = q.Reveals.HasValue && t.Has(q.Reveals.Value);
                    if (v.Answers[q.Id].Reveals.HasValue != shouldSlip) p.Add($"answer to {q.Id} doesn't match tells");
                }
                if (t.Has(Tell.NameMismatch) == (v.Slip.Name == v.Id.Name)) p.Add("name mismatch tell not reflected in papers");
                if (t.Has(Tell.PhotoMismatch) == v.Id.Photo.SameFace(t.Look)) p.Add("photo mismatch tell not reflected in papers");
                if (t.Has(Tell.NoRecord) != (v.Record == null)) p.Add("no-record tell not reflected in the archive");
                if (t.Tells.Count(x => Tells.Category(x) == TellCategory.Document) > 3) p.Add("more than 3 forged fields");
            }
            else
            {
                if (t.Tells.Count > 0) p.Add("citizen with human tells");
                if (v.Record == null || v.Record.Name != v.Id.Name || v.Record.Clan != v.Id.Clan) p.Add("citizen without a matching archive record");
                if (forged) p.Add("citizen with forged papers");
                if (v.Answers.Values.Any(a => a.Reveals.HasValue || a.FingersShown == 5 || a.Fog)) p.Add("citizen answer reveals a tell");
                if (!v.Greeting.Contains("%")) p.Add("citizen greeting doesn't state a mood");
                bool breaks = Rulebook.BreaksAnyRule(v, ctx);
                if (t.Kind == VisitorKind.RuleBreakerCitizen && !breaks) p.Add("rule-breaker who breaks no rule");
                if (t.Kind != VisitorKind.RuleBreakerCitizen && breaks) p.Add("citizen breaks a rule but isn't a rule-breaker");
                if (t.Kind == VisitorKind.OddCitizen && (v.Medical == null || v.Medical.Condition != t.Quirk || t.Quirk == Quirk.None))
                    p.Add("odd citizen without a matching medical pass");
                if (t.Kind != VisitorKind.OddCitizen && (v.Medical != null || t.Quirk != Quirk.None)) p.Add("unexpected medical pass");
            }
            return p;
        }
    }
}
