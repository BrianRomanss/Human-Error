using System;
using System.Linq;
using ChuchuGames.UI;
using HumanError.Model;
using HumanError.Model.Content;
using UnityEngine;
using UnityEngine.UI;

namespace HumanError.View
{
    /// <summary>
    /// Home between shifts (GDD §7.2): the budget checklist, Sel, the Lattice store, the news feed and messages
    /// with their story choices, then START SHIFT. All rules live in <see cref="Home"/>; this only draws it.
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        public sealed class Actions
        {
            public Action StartShift;
            public Action Menu;
            /// <summary>With no next shift: the button's label and the line under it (the demo's ending).</summary>
            public string EndLabel, EndNote;
        }

        Home _home;
        ShiftContext _next;
        Actions _actions;
        RectTransform _root, _budget, _sel, _store, _messages;
        // Portrait (phones): every card is full width on one scrolling page, START SHIFT stays pinned below it.
        bool _portrait;
        float _cw = 560, _msgW = 576, _budgetH = 500, _newsH = 494;
        RectTransform _page, _budgetCard, _selCard, _storeCard, _messagesCard, _newsCard;

        public Home Home => _home;

        /// <param name="next">Tomorrow's shift, or null when this build has no more shifts.</param>
        public static HomeScreen Create(RectTransform parent, Home home, ShiftContext next, Actions actions)
        {
            var root = UiBuild.Rect("HomeScreen", parent).Fill();
            var screen = root.gameObject.AddComponent<HomeScreen>();
            screen._root = root;
            screen._home = home;
            screen._next = next;
            screen._actions = actions;
            screen.Build();
            screen.Refresh();
            Theme.Sfx("home", 0.5f);
            return screen;
        }

        // ---------------------------------------------------------------- layout

        void Build()
        {
            _portrait = Layout.Portrait;
            if (_portrait)
            {
                _cw = Layout.W - 80;
                _msgW = _cw - 24;
            }
            UiBuild.Panel("Background", _root, Palette.MossNight, rounded: false).rectTransform.Fill();
            var bg = Layout.Cover("Room", _root, Theme.Sprite("home"));
            if (_portrait) bg.color = new Color(0.55f, 0.55f, 0.55f); // dimmer behind a full page of cards

            var s = _home.Save;
            float tx = _portrait ? 40 : 60;
            Theme.Title("Title", _root, "HOME · BLOCK 22", _portrait ? 48 : 54, Palette.PaperSand).rectTransform.PlaceTopLeft(tx, 34, 900, 66);
            int act = Acts.For(s.nextShift);
            string after = s.last.shift == 0 ? "The evening before your first shift" : $"After shift {s.last.shift}";
            Theme.Body("Sub", _root, $"{after}   ·   Act {act} · {Acts.Title(act)}", 24, Palette.Muted).rectTransform.PlaceTopLeft(tx + 2, 96, Layout.W - tx - 40, 32);

            var menu = Theme.Button("Menu", _root, "MENU", Palette.PanelLight, Palette.PaperSand, () => _actions.Menu?.Invoke(), 24);
            ((RectTransform)menu.transform).PlaceTopLeft(Layout.W - 220, 40, 160, 56);
            if (_portrait) ((RectTransform)menu.transform).PlaceTopLeft(Layout.W - 200, 34, 160, 60);

            if (_portrait)
            {
                var viewport = UiBuild.Rect("Page", _root);
                viewport.PlaceTopLeft(0, 144, Layout.W, Layout.H - 144 - 200);
                _page = Layout.ScrollPage(viewport);
            }
            _budget = Card("Budget", 60, 150, 560, 500, Palette.PaperSand, out _budgetCard);
            _sel = Card("Sel", 60, 666, 560, 170, null, out _selCard);
            _store = Card("Store", 60, 852, 560, 190, null, out _storeCard);
            BuildNews();
            _messages = BuildMessagesPanel();
            BuildStart();
        }

        /// <summary>A card and its content rect; refreshing clears only the content, never the card's own fill.</summary>
        RectTransform Card(string name, float x, float y, float w, float h, Color? paper, out RectTransform card)
        {
            var parent = _portrait ? _page : _root;
            var img = paper.HasValue ? UiBuild.Panel(name, parent, paper.Value) : Theme.Panel(name, parent);
            card = img.rectTransform;
            // Portrait positions come from Relayout once the contents are known.
            card.PlaceTopLeft(x, y, _portrait ? _cw : w, h);
            return UiBuild.Rect("Content", img.transform).Fill();
        }

        /// <summary>Portrait: stack the cards down the page in reading order, each as tall as its contents.</summary>
        void Relayout()
        {
            if (!_portrait) return;
            float y = 16;
            void Put(RectTransform card, float h)
            {
                card.PlaceTopLeft(40, y, _cw, h);
                y += h + 20;
            }
            Put(_selCard, 170);
            Put(_budgetCard, _budgetH);
            Put(_storeCard, 190);
            Put(_messagesCard, 60 + _messages.sizeDelta.y + 12);
            Put(_newsCard, _newsH);
            _page.sizeDelta = new Vector2(0, y);
        }

        void BuildNews()
        {
            var panel = Card("News", 660, 548, 560, 494, null, out _newsCard);
            Theme.Title("Header", panel, "LATTICE NEWS", 26, Palette.BioGlow).rectTransform.PlaceTopLeft(24, 16, 400, 34);
            float y = 64;
            foreach (var n in _home.News)
            {
                var strip = UiBuild.Panel("Headline", panel, Palette.PaperSand);
                var t = Theme.Body("Text", strip.transform, n.text, 24, Palette.Ink, bold: true);
                t.rectTransform.PlaceTopLeft(18, 12, _cw - 84, 40);
                float h = Mathf.Max(64, t.preferredHeight + 26);
                strip.rectTransform.PlaceTopLeft(24, y, _cw - 48, h);
                y += h + 14;
            }
            if (_portrait) _newsH = y + 10;
        }

        RectTransform BuildMessagesPanel()
        {
            var panel = Card("Messages", 1260, 150, 600, 730, null, out _messagesCard);
            int count = _home.Messages.Count;
            Theme.Title("Header", panel, count == 1 ? "1 MESSAGE" : $"{count} MESSAGES", 26, Palette.BioGlow).rectTransform.PlaceTopLeft(24, 16, 500, 34);
            if (_portrait)
            {
                // The page scrolls; the messages simply run as long as they need.
                var list = UiBuild.Rect("Content", panel);
                list.anchorMin = new Vector2(0, 1);
                list.anchorMax = new Vector2(1, 1);
                list.pivot = new Vector2(0.5f, 1);
                list.anchoredPosition = new Vector2(0, -60);
                list.sizeDelta = new Vector2(-24, 0);
                return list;
            }

            var viewport = UiBuild.Panel("Viewport", panel, Color.clear, rounded: false);
            viewport.rectTransform.PlaceTopLeft(12, 60, 576, 660);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiBuild.Rect("Content", viewport.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            return content;
        }

        void BuildStart()
        {
            // Portrait: pinned under the scrolling page.
            float x = _portrait ? 40 : 1260, y = _portrait ? Layout.H - 180 : 900, w = _portrait ? _cw : 600;
            if (_next == null)
            {
                var b = Theme.Button("Start", _root, _actions.EndLabel ?? "TO BE CONTINUED", Palette.Panel, Palette.Muted, null, 36);
                ((RectTransform)b.transform).PlaceTopLeft(x, y, w, 100);
                b.interactable = false;
                Theme.Body("End", _root, _actions.EndNote ?? "That's every shift in this build. Thank you for playing!", 22, Palette.Muted, TextAnchor.UpperCenter)
                    .rectTransform.PlaceTopLeft(x, y + 110, w, 40);
                return;
            }
            var start = Theme.Button("Start", _root, $"START SHIFT {_next.ShiftNumber}", Palette.BioGlow, Palette.Ink, () => _actions.StartShift?.Invoke(), 40);
            ((RectTransform)start.transform).PlaceTopLeft(x, y, w, 100);
            Theme.Body("Tomorrow", _root, $"Tomorrow: {_next.Tonight.Lattice} · {_next.Title}", 22, Palette.Muted, TextAnchor.UpperCenter)
                .rectTransform.PlaceTopLeft(x, y + 110, w, 40);
        }

        // ---------------------------------------------------------------- dynamic parts

        void Refresh()
        {
            RefreshBudget();
            RefreshSel();
            RefreshStore();
            RefreshMessages();
            Relayout();
        }

        static void Clear(RectTransform rt)
        {
            foreach (Transform c in rt) Destroy(c.gameObject);
        }

        void RefreshBudget()
        {
            Clear(_budget);
            var s = _home.Save;
            Theme.Mono("Header", _budget, "TONIGHT'S BUDGET", 26, Palette.Ink).rectTransform.PlaceTopLeft(28, 22, _cw - 60, 34);
            float y = 72;
            void Line(string label, string amount, Color color, int size = 24, Action click = null, bool? ticked = null, bool enabled = true)
            {
                float x = 28;
                RectTransform row;
                if (click != null)
                {
                    var b = UiBuild.Button("Row", _budget, "", Palette.Ink.WithAlpha(0.05f), Palette.Ink, click);
                    b.interactable = enabled;
                    row = (RectTransform)b.transform;
                }
                else row = UiBuild.Rect("Row", _budget);
                row.PlaceTopLeft(16, y - 4, _cw - 32, 44);
                if (ticked.HasValue)
                {
                    var box = UiBuild.Panel("Box", row, Palette.Ink, raycastTarget: false);
                    box.rectTransform.PlaceTopLeft(12, 7, 30, 30);
                    var inner = UiBuild.Panel("Inner", box.transform, ticked.Value ? Palette.DocHeader : Palette.PaperSand, raycastTarget: false);
                    inner.rectTransform.Fill(ticked.Value ? 5 : 3);
                    x = 56;
                }
                var c = enabled ? color : color.WithAlpha(0.45f);
                Theme.Mono("Label", row, label, size, c, TextAnchor.MiddleLeft).rectTransform.PlaceTopLeft(x, 0, 360, 44);
                Theme.Title("Amount", row, amount, size + 2, c, TextAnchor.MiddleRight).rectTransform.PlaceTopLeft(_cw - 240, 0, 196, 44);
                y += 46;
            }
            void Rule()
            {
                UiBuild.Panel("Rule", _budget, Palette.Ink.WithAlpha(0.35f), raycastTarget: false, rounded: false).rectTransform.PlaceTopLeft(28, y + 2, _cw - 56, 3);
                y += 14;
            }

            var dim = Palette.Ink.WithAlpha(0.6f);
            var red = new Color(0.75f, 0.25f, 0.2f);
            if (!_home.HasBills)
            {
                Line("Savings", Theme.Balance(s.shards), Palette.Ink, 26);
                var t = Theme.Body("Note", _budget, $"No bills tonight. After each shift you get a {Campaign.ShiftWage}-shard wage, plus shards for every correct decision, minus citations.", 23, dim);
                t.rectTransform.PlaceTopLeft(28, y + 10, _cw - 56, 120);
                y += t.preferredHeight + 30;
            }
            else
            {
                Line($"Shift {s.last.shift} wage", Theme.Shards(s.last.wage), dim, 22);
                Line("Decisions", Theme.Shards(s.last.pay), dim, 22);
                if (s.last.hearing) Line("Hearing fine", Theme.Shards(-Campaign.HearingFine), red, 22);
                Line("Savings", Theme.Balance(s.shards), Palette.Ink, 26);
                Rule();
                foreach (var b in _home.Bills)
                {
                    var bill = b;
                    string label = bill.Required ? bill.Label + " (due)" : bill.Label;
                    Line(label, Theme.Shards(-bill.Cost), bill.Paid ? Palette.Ink : dim, 24,
                        bill.Required ? (Action)null : () => { if (_home.Toggle(bill)) { Theme.Sfx("paper", 0.5f); Refresh(); } },
                        bill.Paid, bill.Required || _home.CanToggle(bill));
                }
            }
            foreach (var u in _home.Shop.Where(_home.IsBuying)) Line(u.name, Theme.Shards(-u.price), Palette.Ink, 24);
            foreach (var m in _home.Messages)
                if (_home.Choice(m) is int i && m.choices[i].shards != 0)
                    Line($"For {m.from}", Theme.Shards(m.choices[i].shards), Palette.Ink, 24);
            Rule();
            Line("Left", Theme.Balance(_home.Left), _home.Left < 0 ? red : Palette.Ink, 30);
            if (_home.HasBills && s.strikes > 0)
                Line($"Strikes this act: {s.strikes} of {Campaign.StrikesForHearing}", "", red, 20);
            if (_portrait) _budgetH = y + 16;
        }

        static string SelState(int health)
        {
            switch (health)
            {
                case 3: return "Healthy. 90% cheerful.";
                case 2: return "Coughing at night.";
                case 1: return "Sick. Needs medicine every night.";
                default: return "In hospital.";
            }
        }

        void RefreshSel()
        {
            Clear(_sel);
            var face = UiBuild.Rect("Face", _sel);
            face.PlaceTopLeft(20, 20, 130, 130);
            Avatars.Build(face, "Sel");
            var s = _home.Save;
            Theme.Title("Name", _sel, "SEL", 28, Palette.BioGlow).rectTransform.PlaceTopLeft(172, 18, _cw - 200, 34);
            Theme.Body("Now", _sel, SelState(s.selHealth), 24, Palette.PaperSand).rectTransform.PlaceTopLeft(172, 54, _cw - 190, 34);
            if (!_home.HasBills) return;
            int tomorrow = _home.SelHealthTomorrow;
            string line;
            Color c;
            if (tomorrow <= 0) { line = $"Sel ends up in hospital: {Theme.Shards(-Home.Hospital)} tomorrow."; c = Palette.Coral; }
            else if (tomorrow < s.selHealth) { line = "Without medicine and food, Sel gets worse."; c = Palette.Coral; }
            else if (tomorrow > s.selHealth) { line = "Sel will feel better tomorrow."; c = Palette.BioGlow; }
            else { line = "Sel will stay the same."; c = Palette.Muted; }
            var t = Theme.Body("Tomorrow", _sel, line, 22, c);
            t.font = Theme.TitleFont; // the shard glyph only exists in the title font
            t.rectTransform.PlaceTopLeft(172, 96, _cw - 190, 60);
        }

        void RefreshStore()
        {
            Clear(_store);
            Theme.Title("Header", _store, "LATTICE STORE", 26, Palette.BioGlow).rectTransform.PlaceTopLeft(24, 16, 400, 34);
            if (_home.Shop.Count == 0)
            {
                Theme.Body("Empty", _store, "Nothing new on sale tonight. New desk tools arrive as the job gets harder.", 22, Palette.Muted)
                    .rectTransform.PlaceTopLeft(24, 60, _cw - 48, 100);
                return;
            }
            // Up to three tools can be on sale at once (UV lamp, gill-scope, 4th question): compact rows, longer text when alone.
            int n = _home.Shop.Count;
            float row = n == 1 ? 120 : (190 - 58) / (float)n;
            float y = 54;
            foreach (var u in _home.Shop)
            {
                var item = u;
                var t = Theme.Body("Name", _store, n == 1 ? $"<b>{item.name}</b>  ·  {item.text}" : $"<b>{item.name}</b>  ·  {item.text}", n == 1 ? 21 : 17, Palette.PaperSand);
                t.rectTransform.PlaceTopLeft(24, y, _cw - 210, row - 4);
                t.verticalOverflow = VerticalWrapMode.Truncate;
                bool buying = _home.IsBuying(item);
                var b = Theme.Button("Buy", _store, buying ? "CANCEL" : $"BUY {Theme.Shards(item.price).Replace("+", "")}",
                    buying ? Palette.Sand : Palette.UvViolet, buying ? Palette.Ink : Palette.PaperSand,
                    () => { if (_home.ToggleBuy(item)) { Theme.Sfx("paper", 0.6f, 1.2f); Refresh(); } }, n == 1 ? 24 : 20);
                ((RectTransform)b.transform).PlaceTopLeft(_cw - 174, y, 150, Mathf.Min(60, row - 8));
                b.interactable = _home.CanBuy(item);
                y += row;
            }
        }

        void RefreshMessages()
        {
            Clear(_messages);
            float y = 0;
            foreach (var m in _home.Messages)
                y += MessageCard(m, y) + 14;
            _messages.sizeDelta = new Vector2(_messages.sizeDelta.x, y);
        }

        float MessageCard(MessageData m, float y)
        {
            bool unknown = m.from == "Unknown";
            var card = UiBuild.Panel("Message", _messages, unknown ? Palette.Coral.WithAlpha(0.18f) : Palette.PanelLight);
            var c = card.rectTransform;
            float w = _msgW, textX = 116, textW = w - textX - 20;
            var face = UiBuild.Rect("Face", c);
            face.PlaceTopLeft(16, 16, 84, 84);
            Avatars.Build(face, m.from);
            Theme.Title("From", c, unknown ? "NEW MESSAGE · UNKNOWN" : m.from.ToUpperInvariant(), 22, unknown ? Palette.Coral : Palette.BioGlow)
                .rectTransform.PlaceTopLeft(textX, 14, textW, 30);
            var body = Theme.Body("Text", c, $"“{m.text}”", 23, Palette.PaperSand);
            body.rectTransform.PlaceTopLeft(textX, 46, textW, 40);
            float h = Mathf.Max(116, 46 + body.preferredHeight + 16);

            int? chosen = _home.Choice(m);
            for (int i = 0; i < m.choices.Length; i++)
            {
                int index = i;
                var choice = m.choices[i];
                bool picked = chosen == i;
                string cost = choice.shards != 0 ? $"  ({Theme.Shards(choice.shards)})" : "";
                var b = Theme.Button("Choice", c, choice.text + cost, picked ? Palette.BioGlow : Palette.Panel, picked ? Palette.Ink : Palette.PaperSand,
                    () => { if (_home.Choose(m, index)) { Theme.Sfx("paper", 0.5f); Refresh(); } }, 22);
                var label = b.GetComponentInChildren<Text>();
                if (choice.shards == 0) label.font = Theme.BoldFont; // the shard glyph only exists in the title font
                ((RectTransform)b.transform).PlaceTopLeft(16, h, w - 32, 50);
                b.interactable = _home.CanChoose(m, index);
                h += 58;
            }
            if (chosen is int k && !string.IsNullOrEmpty(m.choices[k].reply))
            {
                var reply = Theme.Body("Reply", c, $"{ShortName(m.from)}: “{m.choices[k].reply}”", 22, Palette.Sand);
                reply.rectTransform.PlaceTopLeft(16, h + 2, w - 32, 30);
                h += reply.preferredHeight + 12;
            }
            c.PlaceTopLeft(0, y, w, h + (m.choices.Length > 0 ? 8 : 0));
            return c.sizeDelta.y;
        }

        static string ShortName(string from) => from == "Unknown" ? "W3" : from.Split(' ').Last();
    }
}
