using System;
using System.Linq;
using ChuchuGames.UI;
using HumanError.Model;
using UnityEngine;
using UnityEngine.UI;

namespace HumanError.View
{
    /// <summary>Grey-box switches; become player settings in M4 (GDD §7.2 Settings).</summary>
    public static class GameSettings
    {
        /// <summary>Show the truth right after each decision (grey-box gate); the final game keeps it for the Shift Report.</summary>
        public static bool RevealEachVisitor = true;
    }

    /// <summary>Shift Briefing, Rulebook and Shift Report (GDD §7.2).</summary>
    public static class Screens
    {
        public static GameObject Briefing(RectTransform parent, ShiftContext ctx, Action start)
        {
            var root = UiBuild.Panel("Briefing", parent, Palette.MossNight, rounded: false);
            root.rectTransform.Fill();
            var card = Theme.Panel("Card", root.transform);
            card.rectTransform.Centre(1100, 760);
            var rt = card.rectTransform;
            Theme.Title("Kicker", rt, "LATTICE TOWER · NIGHT WINDOW 3", 24, Palette.BioGlow).rectTransform.PlaceTopLeft(60, 50, 980, 32);
            string title = string.IsNullOrEmpty(ctx.Title) ? "BRIEFING" : ctx.Title.ToUpperInvariant();
            Theme.Title("Title", rt, $"SHIFT {ctx.ShiftNumber} · {title}", 60, Palette.PaperSand).rectTransform.PlaceTopLeft(60, 88, 980, 80);
            string closed = ctx.ClosedFloors.Count == 0 ? "All floors are open."
                : string.Join(", ", ctx.ClosedFloors.Select(f => $"Floor {f} ({Floors.Office(f)})")) + " is CLOSED tonight.";
            string tools = ctx.Has(Tools.UvLamp) ? "\nTool issued: <b>UV LAMP</b>." : "";
            var body = Theme.Body("Body", rt,
                $"Date: <b>{ctx.Tonight.Lattice}</b>   ·   Kesh is <b>{ctx.KeshColour.ToUpperInvariant()}</b> tonight.\n" +
                $"{closed}{tools}\n\n" +
                (string.IsNullOrEmpty(ctx.Briefing) ? "Humans in stolen bio-suits have been reported in the Tower." : ctx.Briefing) + "\n\n" +
                $"Ask each visitor up to <b>{ctx.QuestionBudget} questions</b> — choose them well, and watch them while they answer.\n\n" +
                "<color=#8FA7A8>Humans → CALL SECURITY.  Citizens whose papers break a rule → TURN AWAY.  Everyone else → LET IN.</color>",
                27, Palette.PaperSand);
            body.supportRichText = true;
            body.rectTransform.PlaceTopLeft(60, 190, 980, 400);
            string graav = string.IsNullOrEmpty(ctx.SupervisorLine) ? "Don't waste questions, and don't arrest citizens." : ctx.SupervisorLine;
            Theme.Body("Graav", rt, $"SUPERVISOR GRAAV: \"{graav}\"", 24, Palette.Sand).rectTransform.PlaceTopLeft(60, 600, 980, 60);
            var b = Theme.Button("Start", rt, "START SHIFT", Palette.BioGlow, Palette.Ink, start, 32);
            ((RectTransform)b.transform).PlaceTopLeft(760, 666, 280, 70);
            return root.gameObject;
        }

        public static GameObject Rulebook(RectTransform parent, ShiftContext ctx, Action close)
        {
            var dim = UiBuild.Panel("Rulebook", parent, Palette.Ink.WithAlpha(0.75f), rounded: false);
            dim.rectTransform.Fill();
            var book = UiBuild.Panel("Book", dim.transform, Palette.PaperSand);
            book.rectTransform.Centre(1640, 960);
            var rt = book.rectTransform;
            Theme.Title("Title", rt, "CIVIC LATTICE RULEBOOK", 42, Palette.Ink).rectTransform.PlaceTopLeft(56, 30, 900, 54);

            string Bullets(System.Collections.Generic.IEnumerable<string> lines) => string.Join("\n", lines.Select(f => "•  " + f));
            var left = Theme.Body("Citizens", rt,
                "<b>CITIZENS</b>\n" + Bullets(HumanError.Model.Rulebook.CitizenFacts) +
                "\n\n<b>FORGERIES — a forged paper means a human</b>\n" + Bullets(HumanError.Model.Rulebook.PaperFacts.Concat(HumanError.Model.Rulebook.ExtraFacts(ctx))),
                22, Palette.Ink);
            left.supportRichText = true;
            left.rectTransform.PlaceTopLeft(56, 96, 760, 700);

            var docs = Theme.Body("Docs", rt,
                "<b>DESK RULES — real citizens who break these are TURNED AWAY</b>\n" + Bullets(ctx.Rules.Select(r => r.RulebookText)), 22, Palette.Ink);
            docs.supportRichText = true;
            docs.rectTransform.PlaceTopLeft(860, 96, 730, 240);

            var clans = Theme.Body("ClansHeader", rt, "<b>CLANS</b>  (the clan on the ID must match the face)", 22, Palette.Ink);
            clans.supportRichText = true;
            clans.rectTransform.PlaceTopLeft(860, 340, 730, 30);
            int i = 0;
            foreach (var c in Clans.All)
            {
                float y = 378 + i * 96;
                var chip = UiBuild.Panel("Chip", rt, Palette.Skin(c));
                chip.rectTransform.PlaceTopLeft(860, y + 4, 34, 34);
                var t = Theme.Body("Clan", rt,
                    $"<b>{c}</b> — {Clans.Feature(c)}\n<color=#3A4A50>{Clans.Elder(c)} · greeting: {Clans.ElderGreeting(c)} · nests in {Clans.Home(c)}</color>",
                    21, Palette.Ink);
                t.supportRichText = true;
                t.rectTransform.PlaceTopLeft(906, y, 690, 90);
                i++;
            }

            string closed = ctx.ClosedFloors.Count == 0 ? "none" : string.Join(", ", ctx.ClosedFloors);
            var tonight = Theme.Body("Tonight", rt,
                $"<b>TONIGHT · {ctx.Tonight.Lattice}</b>\nKesh: {ctx.KeshColour}   ·   Closed floors: {closed}   ·   Questions per visitor: {ctx.QuestionBudget}",
                23, Palette.Ink);
            tonight.supportRichText = true;
            tonight.rectTransform.PlaceTopLeft(56, 820, 1200, 80);

            var b = Theme.Button("Close", rt, "BACK TO DESK", Palette.DocHeader, Palette.PaperSand, () => UnityEngine.Object.Destroy(dim.gameObject), 26);
            b.onClick.AddListener(() => close?.Invoke());
            ((RectTransform)b.transform).PlaceTopLeft(1320, 860, 270, 66);
            return dim.gameObject;
        }

        public static GameObject Report(RectTransform parent, ShiftSession s, Action replay, Action next, string nextLabel)
        {
            Theme.Sfx("report", 0.6f);
            var root = UiBuild.Panel("Report", parent, Palette.MossNight, rounded: false);
            root.rectTransform.Fill();
            var rt = root.rectTransform;
            Theme.Title("Title", rt, $"SHIFT {s.Context.ShiftNumber} REPORT", 56, Palette.PaperSand).rectTransform.PlaceTopLeft(80, 34, 900, 70);
            Theme.Title("Date", rt, s.Context.Tonight.Lattice, 32, Palette.Muted, TextAnchor.UpperRight).rectTransform.PlaceTopLeft(1440, 50, 400, 40);

            float rowsTop = 116, rowsBottom = 884;
            int mistakes = s.Results.Count(r => !r.Verdict.IsGood || r.Verdict.Complaint);
            float good = 46, bad = 74;
            float scale = Mathf.Min(1f, (rowsBottom - rowsTop) / Mathf.Max(1f, (s.Results.Count - mistakes) * good + mistakes * bad));
            float y = rowsTop;
            foreach (var r in s.Results)
            {
                var v = r.Visitor;
                bool showHint = !r.Verdict.IsGood || r.Verdict.Complaint;
                float h = (showHint ? bad : good) * scale;
                var row = UiBuild.Panel("Row", rt, Palette.Panel);
                row.rectTransform.PlaceTopLeft(80, y, 1760, h - 5);
                string decided = r.Verdict.Decision == Decision.LetIn ? "Let in" : r.Verdict.Decision == Decision.TurnAway ? "Turned away" : "Security";
                string truth = v.Truth.IsHuman ? "Human" : v.Truth.Kind == VisitorKind.OddCitizen ? "Citizen (pass)" : "Citizen";
                Theme.Body("Name", row.transform, v.DisplayName, 23, Palette.PaperSand).rectTransform.PlaceTopLeft(20, 8, 440, 30);
                Theme.Body("Decision", row.transform, decided, 23, Palette.Muted).rectTransform.PlaceTopLeft(470, 8, 220, 30);
                Theme.Body("Truth", row.transform, truth, 23, v.Truth.IsHuman ? Palette.Coral : Palette.Muted).rectTransform.PlaceTopLeft(700, 8, 220, 30);
                string label = r.Verdict.Label + (r.Verdict.Complaint ? " + complaint" : "");
                Theme.Title("Outcome", row.transform, $"{label}   {Theme.Shards(r.Verdict.Shards)}", 23, Palette.For(r.Verdict.Outcome)).rectTransform.PlaceTopLeft(930, 8, 600, 30);
                if (showHint)
                {
                    var reasons = r.Verdict.IsGood ? new System.Collections.Generic.List<string> { "Too many pressure or trap questions for a citizen." } : Judge.Reasons(v, s.Context);
                    string hint = (v.Truth.IsHuman && !r.Verdict.IsGood ? "Missed: " : "") + string.Join("  ", reasons);
                    var t = Theme.Body("Hint", row.transform, hint, 19, Palette.Sand);
                    t.rectTransform.PlaceTopLeft(20, 40 * scale, 1720, 28);
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
                y += h;
            }

            var sum = UiBuild.Panel("Summary", rt, Palette.PaperSand);
            sum.rectTransform.PlaceTopLeft(80, 900, 1760, 150);
            void Stat(string label, string value, Color c, float x)
            {
                Theme.Mono("L", sum.transform, label, 18, Palette.Ink.WithAlpha(0.6f)).rectTransform.PlaceTopLeft(x, 20, 220, 24);
                Theme.Title("V", sum.transform, value, 40, c).rectTransform.PlaceTopLeft(x, 46, 220, 54);
            }
            var dark = Palette.Ink;
            var red = new Color(0.75f, 0.25f, 0.2f);
            Stat("PAY", Theme.Shards(s.Pay), dark, 30);
            Stat("PENALTIES", Theme.Shards(s.Penalties), s.Penalties < 0 ? red : dark, 230);
            Stat("ACCURACY", $"{Mathf.RoundToInt(s.Accuracy * 100)}%", dark, 440);
            Stat("STRIKES", s.Strikes.ToString(), s.Strikes > 0 ? red : dark, 610);
            Stat("COMPLAINTS", s.Complaints.ToString(), s.Complaints > 0 ? red : dark, 750);
            Theme.Mono("L", sum.transform, "SUPERVISOR", 18, Palette.Ink.WithAlpha(0.6f)).rectTransform.PlaceTopLeft(940, 20, 300, 24);
            Theme.Body("Note", sum.transform, s.SupervisorNote, 25, dark).rectTransform.PlaceTopLeft(940, 50, 440, 90);

            var again = Theme.Button("Replay", sum.transform, "REPLAY SHIFT", Palette.Sand, Palette.Ink, replay, 24);
            ((RectTransform)again.transform).PlaceTopLeft(1400, 18, 340, 52);
            var nextB = Theme.Button("Next", sum.transform, nextLabel, Palette.BioGlow, Palette.Ink, next, 24);
            ((RectTransform)nextB.transform).PlaceTopLeft(1400, 80, 340, 52);
            return root.gameObject;
        }
    }
}
