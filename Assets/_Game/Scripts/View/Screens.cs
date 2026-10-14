using System;
using System.Linq;
using ChuchuGames.UI;
using HumanError.Model;
using UnityEngine;
using UnityEngine.UI;

namespace HumanError.View
{
    /// <summary>Shift Briefing, Rulebook and Shift Report (GDD §7.2).</summary>
    public static class Screens
    {
        /// <param name="newQuestions">Questions on tonight's list for the first time (GDD §7.2: "new tool or rule highlighted").</param>
        public static GameObject Briefing(RectTransform parent, ShiftContext ctx, System.Collections.Generic.IList<string> newQuestions, Action start)
        {
            var root = UiBuild.Panel("Briefing", parent, Palette.MossNight, rounded: false);
            root.rectTransform.Fill();
            // Portrait: the card fills the phone's width and grows down; its text wraps sooner.
            float cw = Layout.Pick(1100f, Layout.W - 40), iw = cw - 120;
            var card = Theme.Panel("Card", root.transform);
            card.rectTransform.Centre(cw, 760);
            var rt = card.rectTransform;
            int act = Acts.For(ctx.ShiftNumber);
            string kicker = ctx.IsMode ? $"LATTICE TOWER · NIGHT WINDOW 3 · {ctx.Mode} MODE" : $"LATTICE TOWER · NIGHT WINDOW 3 · ACT {act} · {Acts.Title(act)}";
            Theme.Title("Kicker", rt, kicker, 24, Palette.BioGlow).rectTransform.PlaceTopLeft(60, 50, iw, 32);
            string title = string.IsNullOrEmpty(ctx.Title) ? "BRIEFING" : ctx.Title.ToUpperInvariant();
            var heading = Theme.Title("Title", rt, ctx.IsMode ? title : $"SHIFT {ctx.ShiftNumber} · {title}", Layout.Pick(60, 54), Palette.PaperSand);
            heading.rectTransform.PlaceTopLeft(60, 88, iw, 80);
            float bodyTop = 88 + Mathf.Max(80, heading.preferredHeight) + 22;
            string closed = ctx.ClosedFloors.Count == 0 ? "All floors are open."
                : string.Join(", ", ctx.ClosedFloors.Select(f => $"Floor {f} ({Floors.Office(f)})")) + " is CLOSED tonight.";
            string tools = ctx.Has(Tools.UvLamp) ? "\nOn your desk: <b>UV LAMP</b>." : "";
            if (ctx.WantedClan.HasValue)
                tools += $"\n<color={Palette.Rich(Palette.Coral)}><b>WANTED:</b></color> invaders seen wearing <b>{ctx.WantedClan.Value.ToString().ToUpperInvariant()}</b> suits.";
            if (!ctx.Timed) tools += "\nTonight the window stays open until the last visitor.";
            string fresh = newQuestions == null || newQuestions.Count == 0 ? ""
                : $"\n<color={Palette.Rich(Palette.BioGlow)}><b>NEW QUESTIONS:</b></color> " + string.Join("  ·  ", newQuestions.Select(q => $"“{q}”"));
            var body = Theme.Body("Body", rt,
                $"Date: <b>{ctx.Tonight.Lattice}</b>   ·   Kesh is <b>{ctx.KeshColour.ToUpperInvariant()}</b> tonight.\n" +
                $"{closed}{tools}{fresh}\n\n" +
                (string.IsNullOrEmpty(ctx.Briefing) ? "Humans in stolen bio-suits have been reported in the Tower." : ctx.Briefing) + "\n\n" +
                $"Ask each visitor up to <b>{ctx.QuestionBudget} questions</b> — choose them well, and watch them while they answer.\n\n" +
                "<color=#8FA7A8>Humans → CALL SECURITY.  Citizens whose papers break a rule → TURN AWAY.  Everyone else → LET IN.</color>",
                27, Palette.PaperSand);
            body.supportRichText = true;
            body.rectTransform.PlaceTopLeft(60, bodyTop, iw, 400);
            // The card grows with tonight's text (new questions, long bulletins).
            float y = bodyTop + body.preferredHeight + 24;
            string graav = string.IsNullOrEmpty(ctx.SupervisorLine) ? "Don't waste questions, and don't arrest citizens." : ctx.SupervisorLine;
            var g = Theme.Body("Graav", rt, $"SUPERVISOR GRAAV: \"{graav}\"", 24, Palette.Sand);
            g.rectTransform.PlaceTopLeft(60, y, iw, 60);
            y += g.preferredHeight + 20;
            var b = Theme.Button("Start", rt, "START SHIFT", Palette.BioGlow, Palette.Ink, start, 32);
            ((RectTransform)b.transform).PlaceTopLeft(Layout.Pick(cw - 340, 60), y, Layout.Pick(280, iw), Layout.Pick(70, 90));
            card.rectTransform.Centre(cw, Mathf.Min(Layout.H - 40, y + Layout.Pick(70, 90) + 40));
            return root.gameObject;
        }

        /// <summary>A title card when an act begins (GDD §3 narrative arc): ACT II · CRACKS.</summary>
        public static GameObject ActCard(RectTransform parent, int act, Action next)
        {
            Theme.Sfx("act", 0.8f);
            var root = UiBuild.Panel("Act", parent, Palette.Ink, rounded: false);
            root.rectTransform.Fill();
            var rt = root.rectTransform;
            string[] numerals = { "", "I", "II", "III", "IV" };
            string[] lines =
            {
                "",
                "Learn the job. Catch the invaders. The news calls them dangerous.",
                "Caught humans start pleading. Someone called Window 3 wants to talk.",
                "You find out what happens to the people you report. Graav is watching.",
                "The last nights. The Lattice, the resistance and Sel all depend on you.",
            };
            float w = Layout.W, mid = Layout.H / 2, margin = Layout.Pick(260, 60);
            Theme.Title("Act", rt, $"ACT {numerals[Mathf.Clamp(act, 1, 4)]}", 44, Palette.BioGlow, TextAnchor.MiddleCenter).rectTransform.PlaceTopLeft(0, mid - 210, w, 60);
            var title = Theme.Title("Title", rt, Acts.Title(act), Layout.Pick(120, 96), Palette.PaperSand, TextAnchor.MiddleCenter);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.rectTransform.PlaceTopLeft(0, mid - 140, w, 150);
            Theme.Body("Line", rt, lines[Mathf.Clamp(act, 1, 4)], 32, Palette.Muted, TextAnchor.MiddleCenter).rectTransform.PlaceTopLeft(margin, mid + 30, w - 2 * margin, 140);
            var b = Theme.Button("Next", rt, "CONTINUE", Palette.BioGlow, Palette.Ink, next, 30);
            ((RectTransform)b.transform).PlaceTopLeft(w / 2 - 150, mid + 220, 300, 72);
            var g = root.gameObject.AddComponent<CanvasGroup>();
            Tween.Fade(g, 0f, 1f, 0.8f);
            return root.gameObject;
        }

        /// <summary>
        /// The Rulebook overlay (GDD §7.2): tabs for Citizens, Documents, Clans and Tonight. Lines that weren't in last
        /// night's rulebook glow and flag their tab NEW.
        /// </summary>
        /// <param name="previous">Last night's context (null on the first night or in score modes: nothing is flagged).</param>
        public static GameObject Rulebook(RectTransform parent, ShiftContext ctx, Action close, ShiftContext previous = null)
        {
            var dim = UiBuild.Panel("Rulebook", parent, Palette.Ink.WithAlpha(0.75f), rounded: false);
            dim.rectTransform.Fill();
            float bw = Mathf.Min(1640, Layout.W - 40), bh = Layout.Pick(Mathf.Min(960, Layout.H - 40), Layout.H - 160), pw = bw - 112, ph = bh - 280;
            var book = UiBuild.Panel("Book", dim.transform, Palette.PaperSand);
            book.rectTransform.Centre(bw, bh);
            var rt = book.rectTransform;
            Theme.Title("Title", rt, "CIVIC LATTICE RULEBOOK", 42, Palette.Ink).rectTransform.PlaceTopLeft(56, 30, pw, 54);

            var pages = Pages(ctx);
            var before = previous == null ? null : new System.Collections.Generic.HashSet<string>(Pages(previous).SelectMany(p => p.lines));
            bool Fresh(string line) => before != null && !before.Contains(line);

            var body = UiBuild.Rect("Page", rt);
            body.PlaceTopLeft(56, 170, pw, ph);
            var tabs = new System.Collections.Generic.List<Button>();
            void Open(int index)
            {
                foreach (Transform c in body) UnityEngine.Object.Destroy(c.gameObject);
                for (int t = 0; t < tabs.Count; t++)
                {
                    tabs[t].GetComponent<Image>().color = t == index ? Palette.DocHeader : Palette.Sand;
                    tabs[t].GetComponentInChildren<Text>().color = t == index ? Palette.PaperSand : Palette.Ink;
                }
                var page = pages[index];
                if (page.title == "CLANS") { ClansPage(body, pw); return; }
                string glow = Palette.Rich(Palette.DocHeader);
                var text = Theme.Body("Text", body, string.Join("\n", page.lines.Select(l => Fresh(l) ? $"<color={glow}><b>NEW  •  {l}</b></color>" : "•  " + l)),
                    25, Palette.Ink);
                text.supportRichText = true;
                text.rectTransform.PlaceTopLeft(0, 0, pw, ph);
                if (page.note != null)
                {
                    var note = Theme.Body("Note", body, page.note, 22, Palette.Ink.WithAlpha(0.65f));
                    note.rectTransform.PlaceTopLeft(0, text.preferredHeight + 24, pw, 120);
                }
            }
            int first = 0;
            // Four tabs in a row: fixed width on a monitor, a quarter of the page each on a phone.
            float step = Layout.Pick(300, (pw + 12) / 4);
            for (int i = 0; i < pages.Count; i++)
            {
                int index = i;
                bool isNew = pages[i].lines.Any(Fresh);
                if (isNew && first == 0) first = i;
                var tab = Theme.Button("Tab", rt, pages[i].title + (isNew ? Layout.Pick("  · NEW", " · NEW") : ""), Palette.Sand, Palette.Ink, () => Open(index), Layout.Pick(24, 21));
                ((RectTransform)tab.transform).PlaceTopLeft(56 + i * step, 100, step - Layout.Pick(16, 12), 54);
                tabs.Add(tab);
            }
            Open(first);

            var b = Theme.Button("Close", rt, "BACK TO DESK", Palette.DocHeader, Palette.PaperSand, () => UnityEngine.Object.Destroy(dim.gameObject), 26);
            b.onClick.AddListener(() => close?.Invoke());
            ((RectTransform)b.transform).PlaceTopLeft(bw - 320, bh - 100, 270, 66);
            return dim.gameObject;
        }

        sealed class Page
        {
            public string title;
            public System.Collections.Generic.List<string> lines;
            public string note;
        }

        static System.Collections.Generic.List<Page> Pages(ShiftContext ctx)
        {
            string closed = ctx.ClosedFloors.Count == 0 ? "none" : string.Join(", ", ctx.ClosedFloors.Select(f => $"{f} ({Floors.Office(f)})"));
            var tonight = new System.Collections.Generic.List<string>
            {
                $"Tonight is {ctx.Tonight.Lattice}. Kesh is {ctx.KeshColour.ToUpperInvariant()}.",
                $"Closed floors: {closed}.",
                $"Questions per visitor: {ctx.QuestionBudget}.",
            };
            if (ctx.WantedClan.HasValue) tonight.Add($"WANTED: invaders seen in {ctx.WantedClan.Value.ToString().ToUpperInvariant()} suits. A hint, not a rule.");
            foreach (var r in ctx.Rules.Where(r => r is ClanStampRule || r is CurfewRule)) tonight.Add("Bulletin rule: " + r.RulebookText);
            var tools = Tools.All.Where(ctx.Has).Select(Tools.Name).ToList();
            tonight.Add(tools.Count == 0 ? "Desk tools: none yet." : "On your desk: " + string.Join(", ", tools) + ".");
            if (!ctx.Timed) tonight.Add("The window stays open until the last visitor tonight.");
            else tonight.Add("At 02:00 the window closes, whoever is still queuing.");
            return new System.Collections.Generic.List<Page>
            {
                new Page { title = "CITIZENS", lines = HumanError.Model.Rulebook.CitizenFacts.Concat(HumanError.Model.Rulebook.OddFacts).ToList(),
                    note = "Every human tell breaks one of these. A citizen can break none of them, unless a medical pass says so." },
                new Page { title = "DOCUMENTS", lines = HumanError.Model.Rulebook.PaperFacts.Concat(HumanError.Model.Rulebook.ExtraFacts(ctx))
                        .Concat(ctx.Rules.Select(r => "Desk rule: " + r.RulebookText)).ToList(),
                    note = "A forged paper means a human: call security. A real citizen who breaks a desk rule is TURNED AWAY, never arrested." },
                new Page { title = "CLANS", lines = Clans.All.Select(c => $"{c}: {Clans.Feature(c)}").ToList() },
                new Page { title = "TONIGHT", lines = tonight },
            };
        }

        static void ClansPage(RectTransform body, float width)
        {
            var header = Theme.Body("Header", body, "The clan on the ID must match the face at the window. Every citizen knows their clan's lore.", 24, Palette.Ink);
            header.rectTransform.PlaceTopLeft(0, 0, width, 40);
            float y = Mathf.Max(60, header.preferredHeight + 20);
            foreach (var c in Clans.All)
            {
                var chip = UiBuild.Panel("Chip", body, Palette.Skin(c));
                chip.rectTransform.PlaceTopLeft(0, y + 6, 48, 48);
                var t = Theme.Body("Clan", body,
                    $"<b>{c.ToString().ToUpperInvariant()} · {Clans.Title(c)}</b> — {Clans.Feature(c)}\n" +
                    $"Elder: {Clans.Elder(c)}   ·   Greeting: {Clans.ElderGreeting(c)}   ·   Nests in {Clans.Home(c)}",
                    25, Palette.Ink);
                t.supportRichText = true;
                t.rectTransform.PlaceTopLeft(70, y, width - 78, 120);
                // Rows keep the monitor's rhythm; a phone's narrower rows wrap and push the next clan down.
                y += Mathf.Max(140, t.preferredHeight + 24);
            }
        }

        /// <summary>The end of the story (GDD §3): which of the five endings, its epilogue, and what you did.</summary>
        public static GameObject Ending(RectTransform parent, SaveData save, Action back)
        {
            Theme.Sfx("act", 0.8f);
            var e = save.Ending;
            var root = UiBuild.Panel("Ending", parent, Palette.Ink, rounded: false);
            root.rectTransform.Fill();
            var rt = root.rectTransform;
            int number = (int)e;
            float w = Layout.W, h = Layout.H;
            Theme.Title("Kicker", rt, e == Model.Ending.HumanError ? "SECRET ENDING" : $"ENDING {number} OF 4 · AND ONE SECRET", 26, Palette.BioGlow, TextAnchor.UpperCenter)
                .rectTransform.PlaceTopLeft(0, 70, w, 36);
            var title = Theme.Title("Title", rt, Endings.Title(e), Layout.Pick(110, 80), e == Model.Ending.Exposed ? Palette.Coral : Palette.PaperSand, TextAnchor.UpperCenter);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.rectTransform.PlaceTopLeft(0, Layout.Pick(112, 116), w, 130);
            var sub = Theme.Body("Sub", rt, Endings.Subtitle(e), 32, Palette.Sand, TextAnchor.UpperCenter);
            sub.rectTransform.PlaceTopLeft(40, 250, w - 80, 44);
            float textTop = 250 + Mathf.Max(44, sub.preferredHeight) + 36;

            // Portrait: the epilogue takes the phone's height; the stats wrap onto three lines above the button.
            float tx = Layout.Pick(310, 60), statsH = Layout.Pick(60, 150);
            var text = Theme.Body("Epilogue", rt, string.Join("\n\n", Endings.Epilogue(e, save)), 27, Palette.PaperSand);
            text.rectTransform.PlaceTopLeft(tx, textTop, w - 2 * tx, h - textTop - 140 - statsH - 40);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = Mathf.RoundToInt(27 * UiBuild.TextScale);
            text.verticalOverflow = VerticalWrapMode.Truncate;

            string stats = $"Shifts worked: {Mathf.Max(0, save.nextShift - 1)}   ·   Humans caught: {save.humansCaught}   ·   Let in: {save.humansLetIn}   ·   " +
                           $"Sent to Window 3: {save.humansHidden}   ·   Loyalty {save.loyalty} · Mercy {save.mercy}   ·   Clues about yourself: {Endings.CluesFound(save)}/{Endings.Clues.Length}";
            Theme.Body("Stats", rt, stats, 22, Palette.Muted, TextAnchor.UpperCenter).rectTransform.PlaceTopLeft(Layout.Pick(80, 40), h - 140 - statsH, w - 2 * Layout.Pick(80, 40), statsH);
            var b = Theme.Button("Back", rt, "BACK TO TITLE", Palette.BioGlow, Palette.Ink, back, 30);
            ((RectTransform)b.transform).PlaceTopLeft(w / 2 - 150, h - 120, 300, 72);
            var g = root.gameObject.AddComponent<CanvasGroup>();
            Tween.Fade(g, 0f, 1f, 1.2f);
            return root.gameObject;
        }

        /// <param name="replay">Debug runs only: replay the same shift. The campaign has no replays.</param>
        /// <param name="score">Endless and Daily: show the run's score instead of pay.</param>
        /// <param name="note">Replaces the supervisor's note (e.g. "New best!").</param>
        public static GameObject Report(RectTransform parent, ShiftSession s, Action next, string nextLabel, Action replay = null,
            string replayLabel = "REPLAY SHIFT", ScoreRun score = null, string note = null)
        {
            Theme.Sfx("report", 0.6f);
            var root = UiBuild.Panel("Report", parent, Palette.MossNight, rounded: false);
            root.rectTransform.Fill();
            var rt = root.rectTransform;
            // Portrait (phones): narrower rows on two lines (name and outcome, then what you did and who they were),
            // a third for hints; the summary stacks its stats, note and buttons.
            bool portrait = Layout.Portrait;
            float mx = Layout.Pick(80, 30), rw = Layout.W - 2 * mx, sumH = Layout.Pick(150, 320), sumY = Layout.H - 30 - sumH;
            string heading = s.Context.IsMode ? s.Context.Title.ToUpperInvariant() : $"SHIFT {s.Context.ShiftNumber}";
            Theme.Title("Title", rt, $"{heading} REPORT", Layout.Pick(56, 50), Palette.PaperSand).rectTransform.PlaceTopLeft(mx, 34, Layout.Pick(1300, rw), 70);
            if (portrait) Theme.Title("Date", rt, s.Context.Tonight.Lattice, 28, Palette.Muted).rectTransform.PlaceTopLeft(mx, 100, rw, 40);
            else Theme.Title("Date", rt, s.Context.Tonight.Lattice, 32, Palette.Muted, TextAnchor.UpperRight).rectTransform.PlaceTopLeft(1440, 50, 400, 40);

            float rowsTop = Layout.Pick(116, 150), rowsBottom = sumY - 16;
            // Rows with a second line: mistakes, complaints, and story characters (who they really were).
            bool TwoLines(VisitorResult r) => !r.Verdict.IsGood || r.Verdict.Complaint || r.Visitor.Story?.Note != null;
            int mistakes = s.Results.Count(TwoLines);
            float good = Layout.Pick(46, 76), bad = Layout.Pick(74, 108);
            // Portrait rows may grow a little into a tall phone's spare height.
            float scale = Mathf.Min(Layout.Pick(1f, 1.25f), (rowsBottom - rowsTop) / Mathf.Max(1f, (s.Results.Count - mistakes) * good + mistakes * bad));
            float y = rowsTop;
            // Score modes: this shift's decisions are the last ones in the run.
            int pointsFrom = score == null ? 0 : score.Points.Count - s.Results.Count;
            int rowIndex = 0;
            foreach (var r in s.Results)
            {
                int points = score == null ? 0 : score.Points[pointsFrom + rowIndex];
                rowIndex++;
                var v = r.Visitor;
                bool showHint = TwoLines(r);
                float h = (showHint ? bad : good) * scale;
                var row = UiBuild.Panel("Row", rt, Palette.Panel);
                row.rectTransform.PlaceTopLeft(mx, y, rw, h - 5);
                string decided = r.Verdict.Decision == Decision.LetIn ? "Let in" : r.Verdict.Decision == Decision.TurnAway ? "Turned away" : "Security";
                string truth = v.Truth.IsHuman ? "Human" : v.Truth.Kind == VisitorKind.OddCitizen ? "Citizen (pass)" : "Citizen";
                if (r.Verdict.Decision == Decision.Silent) decided = "Window 3";
                string label = r.Verdict.Label + (r.Verdict.Complaint ? " + complaint" : "");
                string outcome = score == null ? $"{label}   {Theme.Shards(r.Verdict.Shards)}" : $"{label}   {(points > 0 ? $"+{points} pts" : "streak lost")}";
                if (portrait)
                {
                    Theme.Body("Name", row.transform, v.DisplayName, 24, Palette.PaperSand).rectTransform.PlaceTopLeft(20, 6 * scale, rw - 520, 32);
                    Theme.Title("Outcome", row.transform, outcome, 23, Palette.For(r.Verdict.Outcome), TextAnchor.UpperRight).rectTransform.PlaceTopLeft(rw - 500, 6 * scale, 480, 32);
                    var what = Theme.Body("Decision", row.transform,
                        $"{decided}  ·  <color={Palette.Rich(v.Truth.IsHuman ? Palette.Coral : Palette.Muted)}>{truth}</color>", 21, Palette.Muted);
                    what.supportRichText = true;
                    what.rectTransform.PlaceTopLeft(20, 38 * scale, rw - 40, 30);
                }
                else
                {
                    Theme.Body("Name", row.transform, v.DisplayName, 23, Palette.PaperSand).rectTransform.PlaceTopLeft(20, 8, 440, 30);
                    Theme.Body("Decision", row.transform, decided, 23, Palette.Muted).rectTransform.PlaceTopLeft(470, 8, 220, 30);
                    Theme.Body("Truth", row.transform, truth, 23, v.Truth.IsHuman ? Palette.Coral : Palette.Muted).rectTransform.PlaceTopLeft(700, 8, 220, 30);
                    Theme.Title("Outcome", row.transform, outcome, 23, Palette.For(r.Verdict.Outcome)).rectTransform.PlaceTopLeft(930, 8, 600, 30);
                }
                if (showHint)
                {
                    var reasons = !r.Verdict.IsGood ? Judge.Reasons(v, s.Context)
                        : r.Verdict.Complaint ? new System.Collections.Generic.List<string> { "Too many pressure or trap questions for a citizen." }
                        : new System.Collections.Generic.List<string>();
                    string hint = (v.Truth.IsHuman && !r.Verdict.IsGood ? "Missed: " : "") + string.Join("  ", reasons);
                    if (v.Story?.Note != null) hint = v.Story.Note + (hint.Length > 0 ? "  " + hint : "");
                    var t = Theme.Body("Hint", row.transform, hint, 19, Palette.Sand);
                    if (portrait)
                    {
                        // One line on a phone: shrink a long hint rather than run off the row.
                        t.rectTransform.PlaceTopLeft(20, 68 * scale, rw - 40, 30 * scale);
                        t.resizeTextForBestFit = true;
                        t.resizeTextMinSize = 12;
                        t.resizeTextMaxSize = t.fontSize;
                    }
                    else
                    {
                        t.rectTransform.PlaceTopLeft(20, 40 * scale, 1720, 28);
                        t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    }
                }
                y += h;
            }

            var sum = UiBuild.Panel("Summary", rt, Palette.PaperSand);
            sum.rectTransform.PlaceTopLeft(mx, sumY, rw, sumH);
            float[] columns = { 30, 230, 440, 610, 750 };
            void Stat(string label, string value, Color c, float x)
            {
                // Portrait: five equal columns across the phone.
                if (portrait) x = 24 + System.Array.IndexOf(columns, x) * (rw - 48) / 5f;
                Theme.Mono("L", sum.transform, label, Layout.Pick(18, 16), Palette.Ink.WithAlpha(0.6f)).rectTransform.PlaceTopLeft(x, 20, Layout.Pick(220, 190), 24);
                Theme.Title("V", sum.transform, value, Layout.Pick(40, 36), c).rectTransform.PlaceTopLeft(x, 46, Layout.Pick(220, 190), 54);
            }
            var dark = Palette.Ink;
            var red = new Color(0.75f, 0.25f, 0.2f);
            if (score == null)
            {
                Stat("PAY", Theme.Shards(s.Pay), dark, 30);
                Stat("PENALTIES", Theme.Shards(s.Penalties), s.Penalties < 0 ? red : dark, 230);
            }
            else
            {
                Stat("SCORE", score.Score.ToString(), dark, 30);
                Stat("BEST STREAK", score.BestStreak.ToString(), dark, 230);
            }
            Stat("ACCURACY", $"{Mathf.RoundToInt(s.Accuracy * 100)}%", dark, 440);
            int strikes = score != null && score.EndsOnStrikes ? score.Strikes : s.Strikes;
            Stat(score != null && score.EndsOnStrikes ? $"STRIKES /{ScoreRun.StrikesAllowed}" : "STRIKES", strikes.ToString(), strikes > 0 ? red : dark, 610);
            Stat("COMPLAINTS", s.Complaints.ToString(), s.Complaints > 0 ? red : dark, 750);
            string said = note ?? s.SupervisorNote;
            if (s.SentHome > 0) said += s.SentHome == 1 ? "\n1 visitor went home unseen at 02:00." : $"\n{s.SentHome} visitors went home unseen at 02:00.";
            if (portrait)
            {
                Theme.Mono("L", sum.transform, note != null ? "RESULT" : "SUPERVISOR", 16, Palette.Ink.WithAlpha(0.6f)).rectTransform.PlaceTopLeft(24, 112, 300, 24);
                var noteText = Theme.Body("Note", sum.transform, said, 23, dark);
                noteText.rectTransform.PlaceTopLeft(24, 138, rw - 48, 80);
                noteText.resizeTextForBestFit = true;
                noteText.resizeTextMinSize = 16;
                noteText.resizeTextMaxSize = noteText.fontSize;
                float half = (rw - 48 - 16) / 2;
                if (replay != null)
                {
                    var again = Theme.Button("Replay", sum.transform, replayLabel, Palette.Sand, Palette.Ink, replay, 24);
                    ((RectTransform)again.transform).PlaceTopLeft(24, sumH - 94, half, 76);
                }
                var nb = Theme.Button("Next", sum.transform, nextLabel, Palette.BioGlow, Palette.Ink, next, 26);
                ((RectTransform)nb.transform).PlaceTopLeft(replay != null ? 24 + half + 16 : 24, sumH - 94, replay != null ? half : rw - 48, 76);
                return root.gameObject;
            }
            Theme.Mono("L", sum.transform, note != null ? "RESULT" : "SUPERVISOR", 18, Palette.Ink.WithAlpha(0.6f)).rectTransform.PlaceTopLeft(940, 20, 300, 24);
            Theme.Body("Note", sum.transform, said, 24, dark).rectTransform.PlaceTopLeft(940, 46, 440, 100);

            if (replay != null)
            {
                var again = Theme.Button("Replay", sum.transform, replayLabel, Palette.Sand, Palette.Ink, replay, 24);
                ((RectTransform)again.transform).PlaceTopLeft(1400, 18, 340, 52);
            }
            var nextB = Theme.Button("Next", sum.transform, nextLabel, Palette.BioGlow, Palette.Ink, next, replay != null ? 24 : 30);
            ((RectTransform)nextB.transform).PlaceTopLeft(1400, replay != null ? 80 : 30, 340, replay != null ? 52 : 90);
            return root.gameObject;
        }
    }
}
