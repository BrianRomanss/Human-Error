using System.Collections.Generic;
using System.Linq;
using ChuchuGames.Core;

namespace HumanError.Model
{
    public enum QuestionKind { Greeting, Culture, Paperwork, Action, Trap, Pressure, Personal }

    /// <summary>What an action question physically makes the visitor do.</summary>
    public enum QuestionEffect { None, ShowHand, BreatheOnGlass }

    /// <summary>
    /// A question the receptionist can ask (GDD §5.3). Answers are templates; see <see cref="AnswerBook"/> for
    /// the placeholders. A human with the <see cref="Reveals"/> tell answers from the human pool.
    /// </summary>
    public sealed class QuestionDef
    {
        public string Id;
        public string Text;
        public QuestionKind Kind;
        public QuestionEffect Effect;
        /// <summary>The tell a human gives away on this question (null: it only raises stress).</summary>
        public Tell? Reveals;
        public string[] CitizenAnswers;
        public string[] HumanAnswers;
        /// <summary>How likely a human is to slip on this question (weight when choosing their tells).</summary>
        public float SlipChance = 0.5f;
        /// <summary>How much asking it raises a human's stress.</summary>
        public float StressDelta = 0.1f;
        public int UnlockShift = 1;

        /// <summary>Pressure and trap questions annoy citizens (GDD §5.3: can cause a complaint).</summary>
        public bool Annoys => Kind == QuestionKind.Pressure || Kind == QuestionKind.Trap;
    }

    /// <summary>
    /// Turns a question + the visitor's truth into the reply they give. Placeholders:
    /// {mood} {floor} {office} {wrongFloor} {wrongOffice} {time} {wrongTime} {kesh} {wrongKesh} {date} {earthDate}
    /// {clan} {elder} {wrongElder} {greeting} {wrongGreeting} {home} {wrongHome}.
    /// </summary>
    public static class AnswerBook
    {
        public static Answer Make(QuestionDef q, VisitorInstance v, ShiftContext ctx, SeededRandom rng)
        {
            bool slips = q.Reveals.HasValue && v.Has(q.Reveals.Value);
            // Body facts (fingers, breath) show in the gesture, not the words: the text is the same either way.
            bool bodyTell = q.Effect != QuestionEffect.None;
            var pool = slips && !bodyTell && q.HumanAnswers != null && q.HumanAnswers.Length > 0 ? q.HumanAnswers : q.CitizenAnswers;
            return new Answer
            {
                QuestionId = q.Id,
                Question = q.Text,
                Text = Fill(rng.Pick(pool), v, ctx, rng),
                Reveals = slips ? q.Reveals : null,
                FingersShown = q.Effect == QuestionEffect.ShowHand ? (v.Has(Tell.FiveFingers) ? 5 : 4) : 0,
                Fog = q.Effect == QuestionEffect.BreatheOnGlass && v.Has(Tell.MouthBreathing),
            };
        }

        public static string Fill(string template, VisitorInstance v, ShiftContext ctx, SeededRandom rng)
        {
            var clan = v.Truth.Look.Clan;
            var other = Clans.All.Where(c => c != clan).ToList();
            int wrongFloor = rng.Pick(Floors.All.Where(f => f != v.Slip.Floor).ToList());
            string wrongKesh = rng.Pick(Kesh.Colours.Where(k => k != ctx.KeshColour).ToList());
            string wrongTime = WrongTime(v.Slip.Time, rng);
            return template
                .Replace("{mood}", (rng.Range(4, 10) * 10 + rng.Range(0, 2) * 5).ToString())
                .Replace("{floor}", v.Slip.Floor.ToString())
                .Replace("{office}", Floors.Office(v.Slip.Floor).ToLowerInvariant())
                .Replace("{wrongFloor}", wrongFloor.ToString())
                .Replace("{wrongOffice}", Floors.Office(wrongFloor).ToLowerInvariant())
                .Replace("{time}", v.Slip.Time)
                .Replace("{wrongTime}", wrongTime)
                .Replace("{kesh}", ctx.KeshColour)
                .Replace("{wrongKesh}", wrongKesh)
                .Replace("{date}", ctx.Tonight.Lattice)
                .Replace("{earthDate}", ctx.Tonight.Earth)
                .Replace("{clan}", clan.ToString())
                .Replace("{elder}", Clans.Elder(clan))
                .Replace("{wrongElder}", Clans.Elder(rng.Pick(other)))
                .Replace("{greeting}", Clans.ElderGreeting(clan))
                .Replace("{wrongGreeting}", Clans.ElderGreeting(rng.Pick(other)))
                .Replace("{home}", Clans.Home(clan))
                .Replace("{wrongHome}", Clans.Home(rng.Pick(other)));
        }

        static string WrongTime(string time, SeededRandom rng)
        {
            // Same format, shifted by 1–3 hours so it is clearly different from the slip.
            int h = int.Parse(time.Substring(0, 2)), m = int.Parse(time.Substring(3, 2));
            h = (h + rng.Range(1, 4) * (rng.Chance(0.5f) ? 1 : -1) + 24) % 24;
            return $"{h:00}:{m:00}";
        }

        /// <summary>The line a visitor says on arrival (GDD §4: the greeting itself can be a tell).</summary>
        public static string Greeting(VisitorInstance v, SeededRandom rng)
        {
            if (v.Has(Tell.SmallTalkGreeting))
                return rng.Pick(new[] { "Hi there! Busy night?", "Evening! How's it going?", "Hey. Lovely weather, huh?" });
            if (v.Truth.Quirk != Quirk.None)
                return $"I am {rng.Range(3, 7) * 10}% embarrassed. I have a medical pass.";
            return rng.Pick(new[]
            {
                "I am {mood}% content. I have an appointment.",
                "Greetings. I am {mood}% content.",
                "I am {mood}% weary, {rest}% patient.",
            }).Replace("{mood}", (rng.Range(4, 10) * 10).ToString()).Replace("{rest}", (rng.Range(2, 6) * 10).ToString());
        }
    }
}
