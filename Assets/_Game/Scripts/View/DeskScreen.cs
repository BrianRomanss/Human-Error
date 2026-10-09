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
        }

        ShiftSession _session;
        Actions _actions;
        RectTransform _root, _window, _visitorSlot, _counter, _overlay;
        Text _topLeft, _clock, _topRight, _budget, _log, _bubbleText;
        RectTransform _bubble, _scan, _shutter, _hand;
        Image _handImage, _flash, _fog, _uvOverlay;
        DocumentViews.IdCardView _idCard;
        RectTransform _pass;
        bool _uv;
        readonly Dictionary<string, Button> _questionButtons = new Dictionary<string, Button>();
        readonly List<Button> _decisionButtons = new List<Button>();
        VisitorView _visitor;
        bool _busy;

        public ShiftSession Session => _session;

        public static DeskScreen Create(RectTransform parent, ShiftSession session, Actions actions)
        {
            var root = UiBuild.Rect("DeskScreen", parent).Fill();
            var screen = root.gameObject.AddComponent<DeskScreen>();
            screen._root = root;
            screen._session = session;
            screen._actions = actions;
            screen.Build();
            screen.ShowVisitor();
            return screen;
        }

        // ---------------------------------------------------------------- layout

        void Build()
        {
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
            bar.rectTransform.Band(RectTransform.Edge.Top, 64);
            _topLeft = Theme.Title("Shift", bar.transform, "", 28, Palette.PaperSand, TextAnchor.MiddleLeft);
            _topLeft.rectTransform.Fill(28);
            _clock = Theme.Title("Clock", bar.transform, "", 34, Palette.BioGlow, TextAnchor.MiddleCenter);
            _clock.rectTransform.Fill();
            _topRight = Theme.Title("Stats", bar.transform, "", 26, Palette.PaperSand, TextAnchor.MiddleRight);
            _topRight.rectTransform.Fill(28);
        }

        void BuildWindow()
        {
            var frame = UiBuild.Panel("WindowFrame", _root, Palette.BioGlow);
            frame.rectTransform.PlaceTopLeft(24, 80, 1056, 620);
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
            counter.rectTransform.PlaceTopLeft(24, 712, 1056, 344);
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
            panel.rectTransform.PlaceTopLeft(1104, 80, 792, 476);
            var list = _session.Context.Questions;
            float row = Mathf.Min(66, (476 - 70) / (float)Mathf.Max(1, list.Count));
            int font = row >= 60 ? 25 : 22;
            Theme.Title("Header", panel.transform, "QUESTIONS", 26, Palette.BioGlow).rectTransform.PlaceTopLeft(22, 14, 400, 36);
            _budget = Theme.Title("Budget", panel.transform, "", 26, Palette.PaperSand, TextAnchor.UpperRight);
            _budget.rectTransform.PlaceTopLeft(370, 14, 400, 36);
            int i = 0;
            foreach (var q in list)
            {
                var qq = q;
                var b = UiBuild.Button(q.Id, panel.transform, $"{i + 1}.  {q.Text}", Palette.PanelLight, Palette.PaperSand, () => Ask(qq), font);
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                b.GetComponentInChildren<Text>().rectTransform.Fill(16);
                ((RectTransform)b.transform).PlaceTopLeft(16, 62 + i * row, 760, row - 6);
                _questionButtons[q.Id] = b;
                i++;
            }
        }

        void BuildLog()
        {
            var panel = Theme.Panel("Log", _root);
            panel.rectTransform.PlaceTopLeft(1104, 568, 792, 124);
            Theme.Title("Header", panel.transform, "ANSWER LOG", 18, Palette.Muted).rectTransform.PlaceTopLeft(22, 8, 400, 24);
            _log = Theme.Body("Text", panel.transform, "", 19, Palette.PaperSand);
            _log.supportRichText = true;
            _log.rectTransform.PlaceTopLeft(22, 32, 750, 90);
            _log.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void BuildBulletin()
        {
            var ctx = _session.Context;
            var panel = Theme.Panel("Bulletin", _root, Palette.PaperSand.WithAlpha(0.5f));
            panel.rectTransform.PlaceTopLeft(1104, 704, 792, 160);
            Theme.Title("Header", panel.transform, $"TONIGHT'S BULLETIN · {ctx.Tonight.Lattice}", 22, Palette.Sand).rectTransform.PlaceTopLeft(22, 12, 560, 30);
            string closed = ctx.ClosedFloors.Count == 0 ? "All floors open."
                : "Closed: " + string.Join(", ", ctx.ClosedFloors.Select(f => $"floor {f} ({Floors.Office(f)})")) + ".";
            string extra = string.Join("\n", ctx.Rules.OfType<ClanStampRule>().Select(r => $"{r.Clan} visitors need a <b>blue stamp</b>."));
            if (extra.Length == 0) extra = "Invaders in bio-suits reported in the Tower.";
            var body = Theme.Body("Text", panel.transform,
                $"Kesh is <b>{ctx.KeshColour.ToUpperInvariant()}</b> tonight.\n{closed}\n{extra}", 21, Palette.PaperSand);
            body.supportRichText = true;
            body.rectTransform.PlaceTopLeft(22, 44, 560, 110);
            bool uv = ctx.Has(Tools.UvLamp);
            var rb = Theme.Button("Rulebook", panel.transform, "RULEBOOK", Palette.Sand, Palette.Ink, () => _actions.OpenRulebook?.Invoke(), 24);
            ((RectTransform)rb.transform).PlaceTopLeft(600, uv ? 14 : 44, 172, uv ? 62 : 72);
            if (uv)
            {
                var lamp = Theme.Button("UvLamp", panel.transform, "UV LAMP", Palette.UvViolet, Palette.PaperSand, ToggleUv, 24);
                ((RectTransform)lamp.transform).PlaceTopLeft(600, 84, 172, 62);
            }
        }

        void BuildDecisions()
        {
            var area = UiBuild.Rect("Decisions", _root);
            area.PlaceTopLeft(1104, 876, 792, 180);
            var letIn = Theme.Button("LetIn", area, "LET IN", Palette.BioGlow, Palette.Ink, () => Decide(Decision.LetIn), 30);
            ((RectTransform)letIn.transform).PlaceTopLeft(0, 0, 252, 130);
            var away = Theme.Button("TurnAway", area, "TURN\nAWAY", Palette.Sand, Palette.Ink, () => Decide(Decision.TurnAway), 30);
            ((RectTransform)away.transform).PlaceTopLeft(270, 0, 252, 130);
            var sec = Theme.Button("Security", area, "CALL\nSECURITY", Palette.Coral, Palette.Ink, null, 30);
            ((RectTransform)sec.transform).PlaceTopLeft(540, 0, 252, 130);
            HoldButton.Attach(sec, Palette.Coral * 0.7f + Color.black * 0.3f, 0.5f, () => Decide(Decision.Security));
            Theme.Body("HoldHint", area, "hold", 18, Palette.Muted, TextAnchor.UpperCenter).rectTransform.PlaceTopLeft(540, 136, 252, 30);
            Theme.Body("Hint", area, "Humans → security · Citizens breaking a rule → turn away", 18, Palette.Muted, TextAnchor.UpperLeft)
                .rectTransform.PlaceTopLeft(0, 136, 530, 30);
            _decisionButtons.AddRange(new[] { letIn, away, sec });
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
            var slip = DocumentViews.Slip(_counter, v);
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

            Say(v.Greeting, 0.5f);
            Refresh();
        }

        void Say(string line, float delay = 0f)
        {
            _bubbleText.text = line;
            _bubble.gameObject.SetActive(true);
            Tween.Scale(_bubble, new Vector3(0.6f, 0.6f, 1), Vector3.one, 0.25f, Ease.OutBack, null, delay);
            var v = _session.Current;
            bool wobble = v != null && v.Truth.IsHuman && _session.Stress >= VisitorInstance.SweatThreshold;
            Tween.Delay(this, delay, () => { for (int i = 0; i < 3; i++) Tween.Delay(_bubble, i * 0.09f, () => Theme.Sfx(wobble ? "blip_human" : "blip_citizen", 0.45f), "blip" + i); }, "say");
        }

        public void Ask(QuestionDef q)
        {
            if (_busy || !_session.CanAsk(q)) return;
            var answer = _session.Ask(q);
            Say(answer.Text);
            if (answer.FingersShown > 0)
            {
                _handImage.sprite = Theme.Sprite($"hand_{answer.FingersShown}");
                _handImage.enabled = _handImage.sprite != null;
                _handImage.color = Palette.Skin(_session.Current.Truth.Look.Clan);
                _hand.gameObject.SetActive(true);
                Tween.Scale(_hand, Vector3.one * 0.3f, Vector3.one, 0.35f, Ease.OutBack);
            }
            if (answer.Fog)
            {
                // The glass mists over, then slowly clears.
                Tween.Run(_fog, "fog", 4f, Ease.Linear, p => _fog.color = new Color(1, 1, 1, p < 0.15f ? p / 0.15f * 0.9f : 0.9f * (1 - (p - 0.15f) / 0.85f)),
                    isMotion: false, delay: 0.4f);
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
            var visitor = _session.Current;
            var asked = _session.Asked.ToList();
            var verdict = _session.Decide(d);
            _bubble.gameObject.SetActive(false);
            PlayVerdict(d, () =>
            {
                if (GameSettings.RevealEachVisitor) ShowReveal(visitor, verdict, Next);
                else Next();
            });
            RefreshTopBar();
        }

        void Next()
        {
            foreach (Transform c in _overlay) Destroy(c.gameObject);
            _busy = false;
            if (_session.IsOver) { _actions.ShiftOver?.Invoke(); return; }
            ShowVisitor();
        }

        /// <summary>The verdict moment (GDD §7.2): door opens, shutter drops or guards arrive. No truth yet.</summary>
        void PlayVerdict(Decision d, Action done)
        {
            var vrt = _visitor != null ? (RectTransform)_visitor.transform : null;
            if (vrt != null) Destroy(vrt.GetComponent<Bob>());
            _hand.gameObject.SetActive(false);
            if (_pass != null) _pass.gameObject.SetActive(false);
            switch (d)
            {
                case Decision.LetIn:
                    Theme.Sfx("door_hiss", 0.7f);
                    if (vrt != null) Tween.Move(vrt, vrt.anchoredPosition, new Vector2(-700, 0), 0.6f, Ease.InOutQuad);
                    Tween.Delay(this, 0.75f, done, "verdict");
                    break;
                case Decision.TurnAway:
                    Theme.Sfx("shutter", 0.8f);
                    _shutter.gameObject.SetActive(true);
                    Tween.Move(_shutter, new Vector2(0, 640), Vector2.zero, 0.3f, Ease.OutQuad);
                    Tween.Delay(this, 0.8f, done, "verdict");
                    break;
                default:
                    Theme.Sfx("alarm", 0.7f);
                    Tween.Run(_flash, "flash", 0.9f, Ease.Linear, p => _flash.color = Palette.Coral.WithAlpha(0.35f * Mathf.Abs(Mathf.Sin(p * Mathf.PI * 3))), isMotion: false);
                    Shake.Play((RectTransform)_window.parent, 10f, 0.4f);
                    foreach (int side in new[] { -1, 1 })
                    {
                        var guard = UiBuild.Panel("Guard", _window, Palette.Ink, raycastTarget: false);
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

        /// <summary>M1 grey-box truth reveal after each decision (GDD §12 milestone 1). Later only the report shows it.</summary>
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
            Theme.Body("Sub", rt, $"{v.DisplayName} — {sub}", 26, Palette.Muted).rectTransform.PlaceTopLeft(40, 110, 860, 36);

            string decided = verdict.Decision == Decision.LetIn ? "LET IN" : verdict.Decision == Decision.TurnAway ? "TURNED AWAY" : "CALLED SECURITY";
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

        void ToggleUv()
        {
            _uv = !_uv;
            _uvOverlay.gameObject.SetActive(_uv);
            _idCard?.SetUv(_uv);
            Theme.Sfx("scan", 0.25f, _uv ? 1.4f : 0.9f);
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
            _topLeft.text = $"SHIFT {ctx.ShiftNumber} · {ctx.Tonight.Lattice}";
            _clock.text = ShiftContext.Clock(_session.ClockMinutes);
            int mistakes = _session.Results.Count(r => !r.Verdict.IsGood);
            int shown = Math.Min(_session.Index + 1, _session.Queue.Count);
            _topRight.text = $"VISITOR {shown}/{_session.Queue.Count}   CAUGHT {_session.HumansCaught}   MISTAKES {mistakes}   {Theme.Shards(_session.Net)}";
        }
    }
}
