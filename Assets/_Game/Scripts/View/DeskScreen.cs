using System;
using System.Collections.Generic;
using System.Linq;
using ChuchuGames.UI;
using HumanError.Model;
using UnityEngine;
using UnityEngine.UI;

namespace HumanError.View
{
    /// <summary>
    /// The Reception Desk (GDD §7.1): top bar, booth window with the visitor and speech bubble, documents on
    /// the counter, and on the right the questions panel, answer log, bulletin card and decision buttons.
    /// All game logic lives in <see cref="ShiftSession"/>; this screen only calls it and redraws.
    /// </summary>
    public sealed class DeskScreen : MonoBehaviour
    {
        public sealed class Actions
        {
            public Action ShiftOver;
            public Action OpenRulebook;
            /// <summary>The pause menu (MENU button or Esc).</summary>
            public Action OpenMenu;
        }

        ShiftSession _session;
        Actions _actions;
        RectTransform _root, _window, _visitorSlot, _counter, _overlay;
        Text _topLeft, _clock, _topRight, _budget, _log, _bubbleText;
        RectTransform _bubble, _scan, _shutter, _hand;
        Image _handImage, _flash, _fog, _uvOverlay;
        DocumentViews.IdCardView _idCard;
        RectTransform _pass, _scope, _archiveCard;
        bool _uv, _scopeOn;
        Button _archiveButton;
        Image _scopeGills;
        readonly Dictionary<string, Button> _questionButtons = new Dictionary<string, Button>();
        readonly List<Button> _decisionButtons = new List<Button>();
        VisitorView _visitor;
        bool _busy;
        // Panels the tutorial can point at (Tutorial.Targets).
        RectTransform _topBar, _windowFrame, _slip, _questionsPanel, _logPanel, _bulletinPanel, _rulebookButton, _decisionsArea;
        readonly Dictionary<string, RectTransform> _decisionRects = new Dictionary<string, RectTransform>();

        public ShiftSession Session => _session;
        /// <summary>A verdict is playing (or the training-night reveal is up): not a good moment to rebuild the desk.</summary>
        public bool Busy => _busy;

        /// <summary>True while something stops the clock (the pause menu, settings).</summary>
        public Func<bool> ClockHeld;
        /// <summary>Settings "timer off": real time never moves the clock.</summary>
        public bool TimerOff;
        int _shownMinute = -1;

        /// <summary>The visitor (0-based queue index) has arrived and their papers have settled on the counter.</summary>
        public event Action<int> VisitorReady;
        public event Action<string> Asked;
        public event Action<Decision> Decided;

        /// <summary>A panel for the tutorial spotlight: a Tutorial.Targets name or "question:id".</summary>
        public RectTransform Target(string name)
        {
            var q = Tutorial.QuestionIn(name, "question:", _session.Context);
            if (q != null && _questionButtons.TryGetValue(q, out var b)) return (RectTransform)b.transform;
            switch (name)
            {
                case "topbar": return _topBar;
                case "id": return _idCard != null ? _idCard.Root : _counter;
                case "slip": return _slip != null ? _slip : _counter;
                case "questions": return _questionsPanel;
                case "log": return _logPanel;
                case "bulletin": return _bulletinPanel;
                case "rulebook": return _rulebookButton;
                case "decisions": return _decisionsArea;
                case "letin":
                case "away":
                case "security": return _decisionRects[name];
                default: return _windowFrame;
            }
        }

        ScoreRun _score;

        /// <param name="score">Endless and Daily: the run's score, shown in the top bar instead of shards.</param>
        public static DeskScreen Create(RectTransform parent, ShiftSession session, Actions actions, ScoreRun score = null)
        {
            var root = UiBuild.Rect("DeskScreen", parent).Fill();
            var screen = root.gameObject.AddComponent<DeskScreen>();
            screen._root = root;
            screen._session = session;
            screen._actions = actions;
            screen._score = score;
            screen.Build();
            screen.ShowVisitor();
            session.Changed += screen.OnSessionChanged;
            return screen;
        }

        // ---------------------------------------------------------------- layout

        // Panel rects (x, y from the stage's top-left, width, height). The booth window and the counter keep their size in
        // both layouts; portrait (phones) stacks the right-hand column under them, with the decisions at the bottom.
        Rect _rWindow, _rCounter, _rQuestions, _rLog, _rBulletin, _rDecisions;
        int _questionColumns = 1;

        void Plan()
        {
            if (!Layout.Portrait)
            {
                _rWindow = new Rect(24, 80, 1056, 620);
                _rCounter = new Rect(24, 712, 1056, 344);
                _rQuestions = new Rect(1104, 80, 792, 436);
                _rLog = new Rect(1104, 528, 792, 124);
                _rBulletin = new Rect(1104, 664, 792, 200);
                _rDecisions = new Rect(1104, 876, 792, 180);
                _questionColumns = 1;
                return;
            }
            const float x = 12, w = 1056;
            _questionColumns = 2;
            int rows = Mathf.Max(1, Mathf.CeilToInt(_session.Context.Questions.Count / 2f));
            // Minimum sizes fit a 1080×1920 phone. A taller phone's extra height goes to bigger question rows and
            // decision buttons (easier to hit), a longer answer log, then evenly into the gaps.
            float gap = 12, bulletinH = 176, decisionsH = 160, rowH = 76, logH = 110;
            float fixedH = 124 + 620 + 344 + bulletinH + decisionsH + 68 + 6 * gap;
            float extra = Layout.H - fixedH - rows * rowH - logH;
            if (extra > 0)
            {
                float grow = Mathf.Min(extra * 0.35f / rows, 24);
                rowH += grow;
                extra -= grow * rows;
                grow = Mathf.Min(extra * 0.2f, 36);
                decisionsH += grow;
                extra -= grow;
                grow = Mathf.Min(extra * 0.6f, 150);
                logH += grow;
                extra -= grow;
                gap += extra / 6;
            }
            _rWindow = new Rect(x, 112 + gap, w, 620);
            _rCounter = new Rect(x, _rWindow.yMax + gap, w, 344);
            _rBulletin = new Rect(x, _rCounter.yMax + gap, w, bulletinH);
            _rDecisions = new Rect(x, Layout.H - gap - decisionsH, w, decisionsH);
            float top = _rBulletin.yMax + gap, avail = _rDecisions.y - gap - top;
            // Too short (a squat window): the questions shrink, the log keeps its minimum.
            float qh = Mathf.Min(68 + rows * rowH, avail - gap - 110);
            _rQuestions = new Rect(x, top, w, qh);
            _rLog = new Rect(x, top + qh + gap, w, avail - qh - gap);
        }

        void Build()
        {
            Plan();
            UiBuild.Panel("Background", _root, Palette.MossNight, rounded: false).rectTransform.Fill();
            BuildTopBar();
            BuildWindow();
            BuildCounter();
            BuildQuestions();
            BuildLog();
            BuildBulletin();
            BuildDecisions();
            _overlay = UiBuild.Rect("Overlay", _root).Fill();
        }

        void BuildTopBar()
        {
            var bar = UiBuild.Panel("TopBar", _root, Palette.Panel, rounded: false);
            bar.rectTransform.Band(RectTransform.Edge.Top, Layout.Pick(64, 112));
            _topBar = bar.rectTransform;
            var menu = Theme.Button("Menu", bar.transform, "MENU", Palette.PanelLight, Palette.PaperSand, () => _actions.OpenMenu?.Invoke(), 22);
            ((RectTransform)menu.transform).PlaceTopLeft(16, 10, 110, 44);
            if (Layout.Portrait)
            {
                // Two rows: shift and clock, then the running stats.
                _topLeft = Theme.Title("Shift", bar.transform, "", 24, Palette.PaperSand, TextAnchor.MiddleLeft);
                _topLeft.rectTransform.PlaceTopLeft(142, 4, 560, 56);
                _clock = Theme.Title("Clock", bar.transform, "", 32, Palette.BioGlow, TextAnchor.MiddleRight);
                _clock.rectTransform.PlaceTopLeft(Layout.W - 436, 4, 420, 56);
                _topRight = Theme.Title("Stats", bar.transform, "", 23, Palette.PaperSand, TextAnchor.MiddleCenter);
                _topRight.rectTransform.PlaceTopLeft(16, 60, Layout.W - 32, 48);
                return;
            }
            _topLeft = Theme.Title("Shift", bar.transform, "", 28, Palette.PaperSand, TextAnchor.MiddleLeft);
            _topLeft.rectTransform.Fill(28);
            _topLeft.rectTransform.offsetMin = new Vector2(146, 0);
            _clock = Theme.Title("Clock", bar.transform, "", 34, Palette.BioGlow, TextAnchor.MiddleCenter);
            _clock.rectTransform.Fill();
            _topRight = Theme.Title("Stats", bar.transform, "", 26, Palette.PaperSand, TextAnchor.MiddleRight);
            _topRight.rectTransform.Fill(28);
        }

        static void Place(RectTransform rt, Rect r) => rt.PlaceTopLeft(r.x, r.y, r.width, r.height);

        void BuildWindow()
        {
            var frame = UiBuild.Panel("WindowFrame", _root, Palette.BioGlow);
            Place(frame.rectTransform, _rWindow);
            _windowFrame = frame.rectTransform;
            var glass = UiBuild.Panel("Glass", frame.transform, Palette.BoothGlass);
            _window = glass.rectTransform.Fill(6);
            glass.gameObject.AddComponent<RectMask2D>();

            var bg = UiBuild.Picture("City", _window, Theme.Sprite("booth"));
            bg.preserveAspect = false;
            bg.rectTransform.Fill();
            var aun = UiBuild.Picture("Aun", _window, Theme.Sprite("moon_aun"));
            aun.rectTransform.PlaceTopLeft(830, 10, 190, 190);
            var kesh = UiBuild.Picture("Kesh", _window, Theme.Sprite("moon_kesh"));
            kesh.color = Palette.KeshColour(_session.Context.KeshColour);
            kesh.rectTransform.PlaceTopLeft(730, 40, 80, 80);

            _visitorSlot = UiBuild.Rect("VisitorSlot", _window);
            _visitorSlot.PlaceTopLeft(50, 54, 566, 566);

            _hand = UiBuild.Rect("Hand", _window);
            _hand.PlaceTopLeft(590, 380, 210, 210);
            _handImage = UiBuild.Picture("HandImage", _hand, null);
            _handImage.rectTransform.Fill();
            _hand.gameObject.SetActive(false);

            // Breath fog on the glass (mouth breathing, GDD §5.4).
            _fog = UiBuild.Picture("Fog", _window, Theme.Sprite("fog"));
            _fog.preserveAspect = false;
            _fog.rectTransform.PlaceTopLeft(170, 300, 380, 230);
            _fog.color = new Color(1, 1, 1, 0);

            // Speech bubble with a small tail pointing at the visitor.
            var bubble = UiBuild.Panel("Bubble", _window, Palette.Ink);
            _bubble = bubble.rectTransform;
            _bubble.PlaceTopLeft(560, 196, 470, 150);
            var paper = UiBuild.Panel("Paper", _bubble, Palette.PaperSand);
            paper.rectTransform.Fill(4);
            var tail = UiBuild.Panel("Tail", _bubble, Palette.PaperSand, rounded: false);
            tail.rectTransform.PlaceTopLeft(-12, 96, 26, 26);
            tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            _bubbleText = Theme.Body("Text", paper.transform, "", 30, Palette.Ink, TextAnchor.MiddleLeft);
            _bubbleText.rectTransform.Fill(20);
            _bubbleText.resizeTextForBestFit = true;
            _bubbleText.resizeTextMinSize = 18;
            _bubbleText.resizeTextMaxSize = 30;

            var scan = UiBuild.Picture("Scan", _window, Theme.Sprite("scanline"));
            scan.preserveAspect = false;
            _scan = scan.rectTransform;
            _scan.anchorMin = new Vector2(0, 1);
            _scan.anchorMax = new Vector2(1, 1);
            _scan.pivot = new Vector2(0.5f, 0);
            _scan.sizeDelta = new Vector2(0, 60);
            _scan.gameObject.SetActive(false);

            var shutter = UiBuild.Panel("Shutter", _window, Palette.CounterEdge, rounded: false);
            _shutter = shutter.rectTransform;
            _shutter.Fill();
            for (int i = 1; i < 8; i++)
            {
                var slat = UiBuild.Panel("Slat", _shutter, Palette.Counter, rounded: false);
                slat.rectTransform.anchorMin = new Vector2(0, i / 8f);
                slat.rectTransform.anchorMax = new Vector2(1, i / 8f);
                slat.rectTransform.sizeDelta = new Vector2(0, 6);
            }
            _shutter.gameObject.SetActive(false);

            _flash = UiBuild.Panel("Flash", _window, Palette.Coral.WithAlpha(0f), raycastTarget: false, rounded: false);
            _flash.rectTransform.Fill();
        }

        void BuildCounter()
        {
            var counter = UiBuild.Panel("Counter", _root, Palette.Counter);
            Place(counter.rectTransform, _rCounter);
            var edge = UiBuild.Panel("Edge", counter.transform, Palette.CounterEdge, rounded: false);
            edge.rectTransform.Band(RectTransform.Edge.Top, 10);
            _counter = UiBuild.Rect("Docs", counter.transform).Fill();
            _uvOverlay = UiBuild.Panel("UvLight", counter.transform, Palette.UvViolet.WithAlpha(0.28f), raycastTarget: false);
            _uvOverlay.rectTransform.Fill();
            _uvOverlay.gameObject.SetActive(false);
        }

        void BuildQuestions()
        {
            var panel = Theme.Panel("Questions", _root);
            Place(panel.rectTransform, _rQuestions);
            _questionsPanel = panel.rectTransform;
            var list = _session.Context.Questions;
            int cols = _questionColumns, rows = Mathf.CeilToInt(list.Count / (float)cols);
            float row = Mathf.Min(Layout.Pick(66, 100), (_rQuestions.height - 66) / Mathf.Max(1, rows));
            float colW = (_rQuestions.width - 32 - (cols - 1) * 12) / cols;
            int font = cols > 1 ? 24 : row >= 60 ? 25 : 22;
            Theme.Title("Header", panel.transform, "QUESTIONS", 26, Palette.BioGlow).rectTransform.PlaceTopLeft(22, 14, 400, 36);
            _budget = Theme.Title("Budget", panel.transform, "", 26, Palette.PaperSand, TextAnchor.UpperRight);
            _budget.rectTransform.PlaceTopLeft(_rQuestions.width - 422, 14, 400, 36);
            int i = 0;
            foreach (var q in list)
            {
                var qq = q;
                var b = UiBuild.Button(q.Id, panel.transform, $"{i + 1}.  {q.Text}", Palette.PanelLight, Palette.PaperSand, () => Ask(qq), font);
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.Fill(Layout.Pick(16, 10));
                if (cols > 1)
                {
                    // Narrow columns: long questions wrap onto two lines and shrink a little if they must.
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 16;
                    label.resizeTextMaxSize = label.fontSize;
                }
                // Down the left column, then the right one, so the numbers read in order.
                int c = i / rows, r = i % rows;
                ((RectTransform)b.transform).PlaceTopLeft(16 + c * (colW + 12), 62 + r * row, colW, row - 6);
                _questionButtons[q.Id] = b;
                i++;
            }
        }

        void BuildLog()
        {
            var panel = Theme.Panel("Log", _root);
            Place(panel.rectTransform, _rLog);
            _logPanel = panel.rectTransform;
            Theme.Title("Header", panel.transform, "ANSWER LOG", 18, Palette.Muted).rectTransform.PlaceTopLeft(22, 8, 400, 24);
            _log = Theme.Body("Text", panel.transform, "", Layout.Pick(19, 22), Palette.PaperSand);
            _log.supportRichText = true;
            _log.rectTransform.PlaceTopLeft(22, 32, _rLog.width - 42, _rLog.height - 34);
            _log.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void BuildBulletin()
        {
            var ctx = _session.Context;
            var panel = Theme.Panel("Bulletin", _root, Palette.PaperSand.WithAlpha(0.5f));
            Place(panel.rectTransform, _rBulletin);
            _bulletinPanel = panel.rectTransform;
            Theme.Title("Header", panel.transform, $"TONIGHT'S BULLETIN · {ctx.Tonight.Lattice}", 22, Palette.Sand).rectTransform.PlaceTopLeft(22, 12, 560, 30);
            string closed = ctx.ClosedFloors.Count == 0 ? "All floors open."
                : "Closed: " + string.Join(", ", ctx.ClosedFloors.Select(f => $"floor {f} ({Floors.Office(f)})")) + ".";
            var lines = ctx.Rules.OfType<ClanStampRule>().Select(r => $"{r.Clan} visitors need a <b>blue stamp</b>.").ToList();
            if (ctx.WantedClan.HasValue) lines.Insert(0, $"<color={Palette.Rich(Palette.Coral)}>WANTED:</color> invaders seen in <b>{ctx.WantedClan.Value.ToString().ToUpperInvariant()}</b> suits.");
            string extra = lines.Count == 0 ? "Invaders in bio-suits reported in the Tower." : string.Join("\n", lines.Take(2));
            // Portrait has the width but not the height: the moon and the floors share a line.
            var body = Theme.Body("Text", panel.transform,
                $"Kesh is <b>{ctx.KeshColour.ToUpperInvariant()}</b> tonight.{Layout.Pick("\n", "   ")}{closed}\n{extra}", Layout.Pick(21, 22), Palette.PaperSand);
            body.supportRichText = true;
            body.rectTransform.PlaceTopLeft(22, 42, _rBulletin.width - 44, _rBulletin.height - 100);
            body.verticalOverflow = VerticalWrapMode.Truncate;

            // Desk tools in one row (GDD §5.6): the rulebook, then whatever the player owns or was issued.
            float x = 16;
            Button Tool(string name, string label, Color fill, Color text, Action onClick)
            {
                var b = Theme.Button(name, panel.transform, label, fill, text, onClick, 21);
                ((RectTransform)b.transform).PlaceTopLeft(x, _rBulletin.height - 54, 180, 42);
                x += 190;
                return b;
            }
            _rulebookButton = (RectTransform)Tool("Rulebook", "RULEBOOK", Palette.Sand, Palette.Ink, () => _actions.OpenRulebook?.Invoke()).transform;
            if (ctx.Has(Tools.UvLamp)) Tool("UvLamp", "UV LAMP", Palette.UvViolet, Palette.PaperSand, ToggleUv);
            if (ctx.Has(Tools.GillScope)) Tool("GillScope", "GILL-SCOPE", Palette.DocHeader, Palette.PaperSand, ToggleScope);
            if (ctx.Has(Tools.Archive)) _archiveButton = Tool("Archive", "ARCHIVE", Palette.PanelLight, Palette.PaperSand, LookUp);
        }

        void BuildDecisions()
        {
            var area = UiBuild.Rect("Decisions", _root);
            Place(area, _rDecisions);
            _decisionsArea = area;
            // The silent button (GDD §5.6) joins once Window 3 has wired it in: four narrower buttons.
            bool silent = _session.Context.Silent && _score == null;
            float gap = silent ? 20 : 18, w = (_rDecisions.width - (silent ? 3 : 2) * gap) / (silent ? 4 : 3);
            int size = silent ? 26 : 30;
            float bh = Layout.Pick(130, _rDecisions.height - 46), hintY = bh + 6;
            var letIn = Theme.Button("LetIn", area, "LET IN", Palette.BioGlow, Palette.Ink, () => Decide(Decision.LetIn), size);
            ((RectTransform)letIn.transform).PlaceTopLeft(0, 0, w, bh);
            var away = Theme.Button("TurnAway", area, "TURN\nAWAY", Palette.Sand, Palette.Ink, () => Decide(Decision.TurnAway), size);
            ((RectTransform)away.transform).PlaceTopLeft(w + gap, 0, w, bh);
            var sec = Theme.Button("Security", area, "CALL\nSECURITY", Palette.Coral, Palette.Ink, null, size);
            ((RectTransform)sec.transform).PlaceTopLeft(2 * (w + gap), 0, w, bh);
            HoldButton.Attach(sec, Palette.Coral * 0.7f + Color.black * 0.3f, 0.5f, () => Decide(Decision.Security));
            Theme.Body("HoldHint", area, "hold", 18, Palette.Muted, TextAnchor.UpperCenter).rectTransform.PlaceTopLeft(2 * (w + gap), hintY, w, 30);
            _decisionButtons.AddRange(new[] { letIn, away, sec });
            if (silent)
            {
                var w3 = Theme.Button("Silent", area, "SILENT\n· W3 ·", Palette.UvViolet, Palette.PaperSand, null, size);
                ((RectTransform)w3.transform).PlaceTopLeft(3 * (w + gap), 0, w, bh);
                HoldButton.Attach(w3, Palette.UvViolet * 0.6f + Color.black * 0.4f, 0.5f, () => Decide(Decision.Silent));
                Theme.Body("SilentHint", area, "hold", 18, Palette.Muted, TextAnchor.UpperCenter).rectTransform.PlaceTopLeft(3 * (w + gap), hintY, w, 30);
                _decisionButtons.Add(w3);
                _decisionRects["silent"] = (RectTransform)w3.transform;
            }
            Theme.Body("Hint", area, silent ? "Humans → security or W3 · broken rule → away" : "Humans → security · Citizens breaking a rule → turn away",
                18, Palette.Muted, TextAnchor.UpperLeft).rectTransform.PlaceTopLeft(0, hintY, 2 * w + gap, 30);
            _decisionRects["letin"] = (RectTransform)letIn.transform;
            _decisionRects["away"] = (RectTransform)away.transform;
            _decisionRects["security"] = (RectTransform)sec.transform;
        }

        // ---------------------------------------------------------------- flow

        void ShowVisitor()
        {
            var v = _session.Current;
            foreach (Transform c in _visitorSlot) Destroy(c.gameObject);
            foreach (Transform c in _counter) Destroy(c.gameObject);
            _hand.gameObject.SetActive(false);
            _shutter.gameObject.SetActive(false);
            if (_pass != null) Destroy(_pass.gameObject);
            if (_scope != null) Destroy(_scope.gameObject);
            _archiveCard = null;
            if (v == null) return;

            _fog.color = new Color(1, 1, 1, 0);
            _visitor = VisitorView.Build(_visitorSlot, v.Truth.Look, VisitorLookOptions.For(v), animated: true);
            var vrt = ((RectTransform)_visitor.transform).Fill();
            // Idle sway starts once they've stepped up (Bob pins its base position on its first frame).
            Tween.Move(vrt, new Vector2(0, -560), Vector2.zero, 0.45f, Ease.OutQuad, () =>
            {
                if (vrt == null) return;
                var bob = vrt.gameObject.AddComponent<Bob>();
                bob.amplitude = 5f;
                bob.period = 3.4f;
            });

            _scan.gameObject.SetActive(true);
            Tween.Move(_scan, new Vector2(0, 60), new Vector2(0, -620), 0.9f, Ease.InOutQuad, () => _scan.gameObject.SetActive(false), 0.35f);
            Theme.Sfx("scan", 0.35f);

            _idCard = DocumentViews.IdCardView.Build(_counter, v);
            _idCard.SetUv(_uv);
            var id = _idCard.Root;
            id.PlaceTopLeft(14, 18, DocumentViews.IdWidth, DocumentViews.IdHeight);
            Tween.Move(id, id.anchoredPosition + new Vector2(0, -360), id.anchoredPosition, 0.4f, Ease.OutBack, null, 0.5f);
            var slip = _slip = DocumentViews.Slip(_counter, v);
            slip.PlaceTopLeft(636, 18, DocumentViews.SlipWidth, DocumentViews.SlipHeight);
            slip.localRotation = Quaternion.Euler(0, 0, -1.5f);
            Tween.Move(slip, slip.anchoredPosition + new Vector2(0, -360), slip.anchoredPosition, 0.4f, Ease.OutBack, null, 0.62f);
            if (v.Medical != null)
            {
                // The visitor holds the pass up to the glass, so it never hides the papers on the counter.
                var pass = _pass = DocumentViews.MedicalPass(_window, v.Medical);
                pass.SetSiblingIndex(_bubble.GetSiblingIndex());
                pass.pivot = new Vector2(0, 1);
                pass.localScale = Vector3.one * 0.78f;
                pass.PlaceTopLeft(790, 400, DocumentViews.PassWidth, DocumentViews.PassHeight);
                pass.localRotation = Quaternion.Euler(0, 0, -4f);
                Tween.Move(pass, pass.anchoredPosition + new Vector2(0, -320), pass.anchoredPosition, 0.45f, Ease.OutBack, null, 0.6f);
            }
            Tween.Delay(this, 0.55f, () => Theme.Sfx("paper", 0.6f), "paperSfx");

            if (_scopeOn) BuildScope(v);
            Say(v.Greeting, 0.5f);
            Refresh();
            int index = _session.Index;
            Tween.Delay(this, 1.15f, () => VisitorReady?.Invoke(index), "ready");
        }

        void Say(string line, float delay = 0f)
        {
            _bubbleText.text = line;
            _bubble.gameObject.SetActive(true);
            Tween.Scale(_bubble, new Vector3(0.6f, 0.6f, 1), Vector3.one, 0.25f, Ease.OutBack, null, delay);
            var v = _session.Current;
            // Each clan has its own voice; a stressed human's blips wobble in pitch (GDD §10's audio tell).
            bool wobble = v != null && v.Truth.IsHuman && _session.Stress >= VisitorInstance.SweatThreshold;
            string clip = v != null ? $"blip_{v.Truth.Look.Clan}" : "blip_citizen";
            Tween.Delay(this, delay, () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    float pitch = wobble ? UnityEngine.Random.Range(0.86f, 1.16f) : 1f + i * 0.02f;
                    Tween.Delay(_bubble, i * 0.09f, () => Theme.Sfx(clip, 0.45f, pitch), "blip" + i);
                }
            }, "say");
        }

        public void Ask(QuestionDef q)
        {
            if (_busy || !_session.CanAsk(q)) return;
            var answer = _session.Ask(q);
            Say(answer.Text);
            Asked?.Invoke(q.Id);
            if (answer.FingersShown > 0)
            {
                _handImage.sprite = Theme.Sprite($"hand_{answer.FingersShown}");
                _handImage.enabled = _handImage.sprite != null;
                _handImage.color = Palette.Skin(_session.Current.Truth.Look.Clan);
                _hand.gameObject.SetActive(true);
                Tween.Scale(_hand, Vector3.one * 0.3f, Vector3.one, 0.35f, Ease.OutBack);
                if (answer.FingersShown == 5) VisitorView.Ring(_hand, 0.5f, 0.5f, 1.1f);
            }
            if (answer.Fog)
            {
                // The glass mists over, then slowly clears.
                Tween.Run(_fog, "fog", 4f, Ease.Linear, p => _fog.color = new Color(1, 1, 1, p < 0.15f ? p / 0.15f * 0.9f : 0.9f * (1 - (p - 0.15f) / 0.85f)),
                    isMotion: false, delay: 0.4f);
                Tween.Delay(_fog, 0.6f, () => VisitorView.Ring(_fog.rectTransform, 0.5f, 0.5f, 1.0f), "fogRing");
            }
            Refresh();
        }

        public void Ask(string questionId)
        {
            var q = _session.Context.Questions.FirstOrDefault(x => x.Id == questionId);
            if (q != null) Ask(q);
        }

        public void Decide(Decision d)
        {
            if (_busy || _session.IsOver) return;
            _busy = true;
            Decided?.Invoke(d);
            var visitor = _session.Current;
            var asked = _session.Asked.ToList();
            var verdict = _session.Decide(d);
            int points = _score?.Add(verdict) ?? 0;
            if (_score != null) ShowPoints(verdict, points);
            Theme.Sfx("stamp", 0.7f);
            // Story characters answer your decision (GDD §6: "caught humans start pleading").
            string line = visitor.Story?.LineFor(d);
            if (line != null) Say(line);
            else _bubble.gameObject.SetActive(false);
            float hold = line != null ? 1.6f : 0f;
            Tween.Delay(this, hold, () => PlayVerdict(d, () =>
            {
                _bubble.gameObject.SetActive(false);
                if (_session.Context.RevealEachVisitor) ShowReveal(visitor, verdict, Next);
                else Next();
            }), "storyLine");
            RefreshTopBar();
        }

        void Next()
        {
            foreach (Transform c in _overlay) Destroy(c.gameObject);
            if (_session.WindowClosed) { ShowClosing(); return; }
            _busy = false;
            if (_session.IsOver || _score != null && _score.Over) { _actions.ShiftOver?.Invoke(); return; }
            ShowVisitor();
        }

        /// <summary>02:00: the shutter comes down on the rest of the queue (GDD §4 soft clock).</summary>
        void ShowClosing()
        {
            Theme.Sfx("closing", 0.7f);
            _shutter.gameObject.SetActive(true);
            Tween.Move(_shutter, new Vector2(0, 640), Vector2.zero, 0.5f, Ease.OutQuad);
            var dim = UiBuild.Panel("Closed", _overlay, Palette.Ink.WithAlpha(0.7f), rounded: false);
            dim.rectTransform.Fill();
            float top = Layout.H / 2 - 160, wide = Layout.W;
            var title = Theme.Title("Title", dim.transform, "02:00 · WINDOW CLOSED", Layout.Pick(72, 60), Palette.Coral, TextAnchor.MiddleCenter);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.rectTransform.PlaceTopLeft(0, top, wide, 100);
            int n = _session.SentHome;
            Theme.Body("Sub", dim.transform, n == 1 ? "1 visitor was still waiting and went home." : $"{n} visitors were still waiting and went home.",
                34, Palette.PaperSand, TextAnchor.MiddleCenter).rectTransform.PlaceTopLeft(40, top + 110, wide - 80, 100);
            var b = Theme.Button("Report", dim.transform, "SHIFT REPORT", Palette.BioGlow, Palette.Ink, () => _actions.ShiftOver?.Invoke(), 30);
            ((RectTransform)b.transform).PlaceTopLeft(wide / 2 - 150, top + 230, 300, 72);
        }

        /// <summary>The verdict moment (GDD §7.2): door opens, shutter drops or guards arrive. No truth yet.</summary>
        void PlayVerdict(Decision d, Action done)
        {
            var vrt = _visitor != null ? (RectTransform)_visitor.transform : null;
            if (vrt != null) Destroy(vrt.GetComponent<Bob>());
            _hand.gameObject.SetActive(false);
            if (_pass != null) _pass.gameObject.SetActive(false);
            if (_scope != null) _scope.gameObject.SetActive(false);
            switch (d)
            {
                case Decision.LetIn:
                    Theme.Sfx("door_hiss", 0.7f);
                    if (vrt != null) Tween.Move(vrt, vrt.anchoredPosition, new Vector2(-700, 0), 0.6f, Ease.InOutQuad);
                    Tween.Delay(this, 0.75f, done, "verdict");
                    break;
                case Decision.Silent:
                    // Window 3 takes them quietly: a side door, no alarm, no guards.
                    Theme.Sfx("door_hiss", 0.4f, 0.7f);
                    if (vrt != null) Tween.Move(vrt, vrt.anchoredPosition, new Vector2(700, -80), 0.9f, Ease.InOutQuad);
                    Tween.Delay(this, 1.0f, done, "verdict");
                    break;
                case Decision.TurnAway:
                    Theme.Sfx("shutter", 0.8f);
                    _shutter.gameObject.SetActive(true);
                    Tween.Move(_shutter, new Vector2(0, 640), Vector2.zero, 0.3f, Ease.OutQuad);
                    Tween.Delay(this, 0.8f, done, "verdict");
                    break;
                default:
                    Theme.Sfx("alarm", 0.7f);
                    Tween.Delay(this, 0.25f, () => Theme.Sfx("boots", 0.8f), "boots");
                    Tween.Run(_flash, "flash", 0.9f, Ease.Linear, p => _flash.color = Palette.Coral.WithAlpha(0.35f * Mathf.Abs(Mathf.Sin(p * Mathf.PI * 3))), isMotion: false);
                    Shake.Play((RectTransform)_window.parent, 10f, 0.4f);
                    foreach (int side in new[] { -1, 1 })
                    {
                        var guard = UiBuild.Panel("Guard", _window, Palette.Ink, raycastTarget: false);
                        guard.transform.SetSiblingIndex(_bubble.GetSiblingIndex()); // behind a story character's last words
                        var g = guard.rectTransform;
                        g.anchorMin = g.anchorMax = new Vector2(0.3f, 0);
                        g.pivot = new Vector2(0.5f, 0);
                        g.sizeDelta = new Vector2(150, 420);
                        Tween.Move(g, new Vector2(side * 520, -20), new Vector2(side * 200, -20), 0.4f, Ease.OutQuad);
                        Destroy(guard.gameObject, 1.2f);
                    }
                    Tween.Delay(this, 1.1f, done, "verdict");
                    break;
            }
        }

        /// <summary>Truth reveal after each decision: the training night only (ShiftData.revealEach); later only the report shows it.</summary>
        void ShowReveal(VisitorInstance v, Verdict verdict, Action next)
        {
            var dim = UiBuild.Panel("Dim", _overlay, Palette.Ink.WithAlpha(0.6f), rounded: false);
            dim.rectTransform.Fill();
            var card = Theme.Panel("Reveal", dim.transform, Palette.For(verdict.Outcome));
            card.rectTransform.Centre(940, 520);
            var rt = card.rectTransform;

            bool human = v.Truth.IsHuman;
            Theme.Title("Truth", rt, human ? "HUMAN" : "CITIZEN", 64, human ? Palette.Coral : Palette.BioGlow).rectTransform.PlaceTopLeft(40, 30, 500, 80);
            string sub = human ? $"in a bio-suit · {v.Truth.Motive}" : v.Truth.Kind == VisitorKind.RuleBreakerCitizen ? "real citizen, papers break a rule" : "real citizen";
            if (v.Story?.Note != null) sub = v.Story.Note;
            Theme.Body("Sub", rt, $"{v.DisplayName} — {sub}", 26, Palette.Muted).rectTransform.PlaceTopLeft(40, 110, 860, 36);

            string decided = verdict.Decision == Decision.LetIn ? "LET IN" : verdict.Decision == Decision.TurnAway ? "TURNED AWAY"
                : verdict.Decision == Decision.Silent ? "SENT TO WINDOW 3" : "CALLED SECURITY";
            Theme.Title("Verdict", rt, $"You {decided}  →  {verdict.Label.ToUpperInvariant()}   {Theme.Shards(verdict.Shards)}", 30, Palette.For(verdict.Outcome))
                .rectTransform.PlaceTopLeft(40, 160, 860, 44);

            var reasons = Judge.Reasons(v, _session.Context);
            if (verdict.Complaint) reasons.Add($"They filed a complaint about your questions ({Judge.ComplaintPenalty} shards).");
            string header = human ? "What gave them away:" : "";
            var list = Theme.Body("Reasons", rt, (header.Length > 0 ? header + "\n" : "") + string.Join("\n", reasons.Select(r => "•  " + r)), 25, Palette.PaperSand);
            list.rectTransform.PlaceTopLeft(40, 222, 860, 200);

            bool last = _session.IsOver;
            var b = Theme.Button("Next", rt, last ? "END SHIFT" : "NEXT VISITOR", Palette.BioGlow, Palette.Ink, next, 28);
            ((RectTransform)b.transform).PlaceTopLeft(620, 428, 280, 66);
            Tween.Scale(rt, Vector3.one * 0.85f, Vector3.one, 0.25f, Ease.OutBack);
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !_busy) _actions.OpenMenu?.Invoke();

            bool held = ClockHeld != null && ClockHeld();
            if (!_busy && !held && !TimerOff) _session.Tick(Time.unscaledDeltaTime);

            // Music: the pulse rises with the clock and the queue (GDD §10).
            var ctx = _session.Context;
            float clock = Mathf.Clamp01((_session.ClockMinutes - 120f) / (ShiftContext.ShiftLengthMinutes - 120f));
            float queue = Mathf.Clamp01((_session.Remaining - 3) / 6f);
            float tension = ctx.Timed && !TimerOff ? Mathf.Max(clock, 0.6f * queue * clock + 0.15f) : 0.15f;
            if (_session.LastCall) tension = 1f;
            // The gill-scope shows real gills pulsing every ~2 s; still ones don't move.
            if (_scopeGills != null && _session.Current != null)
                _scopeGills.color = new Color(1, 1, 1, _session.Current.ShowsStillGills ? 0.6f : 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI)));
            AudioPlayer.SetLayerVolume("tension", held ? 0f : tension * 0.8f);
        }

        void OnDestroy()
        {
            if (_session != null) _session.Changed -= OnSessionChanged;
        }

        void OnSessionChanged()
        {
            if (_session.ClockMinutes != _shownMinute) RefreshTopBar();
        }

        /// <summary>Score modes: a quick "+30" or "STREAK LOST" over the window. The truth still waits for the report.</summary>
        void ShowPoints(Verdict verdict, int points)
        {
            bool good = points > 0;
            var t = Theme.Title("Points", _window, good ? $"+{points}" : _score.Streak == 0 && verdict.Strike ? "STRIKE!" : "STREAK LOST",
                64, good ? Palette.BioGlow : Palette.Coral, TextAnchor.MiddleCenter);
            t.rectTransform.PlaceTopLeft(0, 200, 1044, 120);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            Tween.Move(t.rectTransform, t.rectTransform.anchoredPosition, t.rectTransform.anchoredPosition + new Vector2(0, 80), 1.0f, Ease.OutQuad);
            Destroy(t.gameObject, 1.0f);
        }

        void ToggleUv()
        {
            _uv = !_uv;
            _uvOverlay.gameObject.SetActive(_uv);
            _idCard?.SetUv(_uv);
            Theme.Sfx("uv_on", 0.5f, _uv ? 1f : 0.8f);
            // Only a genuine hologram shimmers under the lamp (GDD §10).
            if (_uv && _session.Current != null && _session.Current.Id.HologramGenuine) Tween.Delay(this, 0.15f, () => Theme.Sfx("shimmer", 0.5f), "shimmer");
        }

        /// <summary>Gill-scope (GDD §5.6): a magnifier on the neck. Real gills pulse; a suit's gills (or gill rot) sit still.</summary>
        void ToggleScope()
        {
            _scopeOn = !_scopeOn;
            Theme.Sfx("scan", 0.3f, _scopeOn ? 1.6f : 1.1f);
            if (_scope != null) Destroy(_scope.gameObject);
            if (_scopeOn && _session.Current != null) BuildScope(_session.Current);
        }

        void BuildScope(VisitorInstance v)
        {
            if (_scope != null) Destroy(_scope.gameObject);
            var lens = UiBuild.Picture("GillScope", _window, Theme.Sprite("circle"));
            lens.color = Palette.BioGlow;
            _scope = lens.rectTransform;
            _scope.PlaceTopLeft(24, 340, 236, 236);
            _scope.SetSiblingIndex(_bubble.GetSiblingIndex());
            var inner = UiBuild.Picture("Lens", _scope, Theme.Sprite("circle"));
            inner.rectTransform.Fill(6);
            inner.color = Palette.Skin(v.Truth.Look.Clan, v.Truth.Look.Tone);
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            // The neck and gill layers at 3× around the gill point (0.5, 0.82 of the 512 grid).
            float size = 566 * 3f;
            foreach (var (sprite, tint) in new[] { ("neck", Palette.Skin(v.Truth.Look.Clan, v.Truth.Look.Tone)), ("gills", Color.white) })
            {
                var layer = UiBuild.Picture(sprite, inner.transform, Theme.Sprite(sprite));
                layer.preserveAspect = false;
                layer.color = tint;
                var rt = layer.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(0, (0.82f - 0.5f) * size);
                if (sprite == "gills") _scopeGills = layer;
            }
            var label = UiBuild.Panel("Label", _scope, Palette.Ink.WithAlpha(0.8f));
            label.rectTransform.PlaceTopLeft(48, -30, 140, 28);
            Theme.Title("Text", label.transform, "GILL-SCOPE", 17, Palette.BioGlow, TextAnchor.MiddleCenter).rectTransform.Fill();
        }

        /// <summary>Archive lookup (GDD §5.6): the name on the ID against Lattice records, once per visitor.</summary>
        void LookUp()
        {
            if (_busy || !_session.CanLookUp) return;
            var v = _session.Current;
            var record = _session.LookUp();
            Theme.Sfx("paper", 0.6f, 0.8f);
            var card = UiBuild.Panel("Archive", _counter, record != null ? Palette.DocHeader : Palette.Ink);
            _archiveCard = card.rectTransform;
            _archiveCard.PlaceTopLeft(636, 18, DocumentViews.SlipWidth, DocumentViews.SlipHeight);
            card.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (card != null) Destroy(card.gameObject); });
            Theme.Mono("Title", card.transform, "LATTICE ARCHIVE · RECORD", 20, Palette.BioGlow).rectTransform.PlaceTopLeft(20, 14, 360, 28);
            Theme.Mono("Query", card.transform, $"QUERY: {v.Id.Name}", 17, Palette.PaperSand.WithAlpha(0.7f)).rectTransform.PlaceTopLeft(20, 44, 360, 24);
            if (record == null)
            {
                Theme.Title("None", card.transform, "NO RECORD FOUND", 40, Palette.Coral, TextAnchor.MiddleCenter).rectTransform.PlaceTopLeft(0, 110, 400, 60);
                Theme.Body("Help", card.transform, "No citizen of that name has ever been registered.", 20, Palette.PaperSand, TextAnchor.UpperCenter)
                    .rectTransform.PlaceTopLeft(30, 176, 340, 60);
            }
            else
            {
                var frame = UiBuild.Panel("Photo", card.transform, Palette.BoothGlass, rounded: false);
                frame.rectTransform.PlaceTopLeft(20, 80, 140, 160);
                frame.gameObject.AddComponent<RectMask2D>();
                var face = VisitorView.Build(frame.transform, record.Photo, null, animated: false);
                var frt = (RectTransform)face.transform;
                frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0.5f, 0f);
                frt.sizeDelta = new Vector2(190, 190);
                frt.anchoredPosition = new Vector2(0, -20);
                Theme.Mono("Name", card.transform, record.Name, 19, Palette.PaperSand).rectTransform.PlaceTopLeft(176, 84, 210, 50);
                Theme.Mono("Clan", card.transform, $"{record.Clan.ToString().ToUpperInvariant()} · {Clans.Title(record.Clan)}", 16, Palette.PaperSand)
                    .rectTransform.PlaceTopLeft(176, 140, 210, 24);
                Theme.Mono("Since", card.transform, $"REGISTERED {record.Registered.Lattice}", 16, Palette.PaperSand).rectTransform.PlaceTopLeft(176, 170, 210, 24);
                Theme.Mono("Status", card.transform, "STATUS: ACTIVE", 18, Palette.BioGlow).rectTransform.PlaceTopLeft(176, 206, 210, 26);
            }
            Theme.Body("Close", card.transform, "click to put away", 16, Palette.Muted, TextAnchor.LowerCenter).rectTransform.PlaceTopLeft(0, 280, 400, 28);
            Tween.Scale(_archiveCard, Vector3.one * 0.9f, Vector3.one, 0.2f, Ease.OutBack);
            Refresh();
        }

        // ---------------------------------------------------------------- refresh

        void Refresh()
        {
            RefreshTopBar();
            _budget.text = $"{_session.QuestionsLeft} of {_session.Context.QuestionBudget} left";
            foreach (var q in _session.Context.Questions)
            {
                var b = _questionButtons[q.Id];
                bool asked = _session.WasAsked(q.Id);
                b.interactable = _session.CanAsk(q);
                b.GetComponent<Image>().color = asked ? Palette.Panel : Palette.PanelLight;
                // Asked questions and those out of budget read as spent, not just un-clickable.
                b.GetComponentInChildren<Text>().color = b.interactable ? Palette.PaperSand : asked ? Palette.Muted.WithAlpha(0.6f) : Palette.Muted;
            }
            var v = _session.Current;
            var lines = new List<string>();
            if (v != null) lines.Add($"<color=#8FA7A8>Arrival:</color> {v.Greeting}");
            foreach (var a in _session.Asked) lines.Add($"<color=#8FA7A8>{a.Question}</color>  {a.Text}");
            _log.text = string.Join("\n", lines);

            if (_archiveButton != null) _archiveButton.interactable = _session.CanLookUp;
            if (_visitor != null)
            {
                _visitor.Stress = _session.Stress;
                _visitor.Sweating = _session.IsSweating;
                _visitor.Flickering = _session.IsFlickering;
            }
        }

        void RefreshTopBar()
        {
            var ctx = _session.Context;
            _topLeft.text = ctx.IsMode ? $"{ctx.Title.ToUpperInvariant()} · {ctx.Tonight.Lattice}" : $"SHIFT {ctx.ShiftNumber} · {ctx.Tonight.Lattice}";
            _shownMinute = _session.ClockMinutes;
            int left = ShiftContext.ShiftLengthMinutes - _session.ClockMinutes;
            bool timed = ctx.Timed && !TimerOff;
            _clock.text = _session.LastCall ? "02:00 · LAST VISITOR" : ShiftContext.Clock(_session.ClockMinutes) + (timed ? "" : "  ·  NO CLOSING");
            _clock.color = timed && left <= 30 ? Palette.Coral : Palette.BioGlow;
            int mistakes = _session.Results.Count(r => !r.Verdict.IsGood);
            int shown = Math.Min(_session.Index + 1, _session.Queue.Count);
            _topRight.text = _score == null
                ? $"VISITOR {shown}/{_session.Queue.Count}   CAUGHT {_session.HumansCaught}   MISTAKES {mistakes}   {Theme.Shards(_session.Net)}"
                : $"VISITOR {shown}/{_session.Queue.Count}   STREAK ×{_score.Multiplier}   " +
                  (_score.EndsOnStrikes ? $"STRIKES {_score.Strikes}/{ScoreRun.StrikesAllowed}   " : "") + $"SCORE {_score.Score}";
        }
    }
}
