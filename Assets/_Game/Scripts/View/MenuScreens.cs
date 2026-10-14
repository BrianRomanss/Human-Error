using System;
using ChuchuGames.UI;
using HumanError.Model;
using UnityEngine;
using UnityEngine.UI;

namespace HumanError.View
{
    /// <summary>Title, Save Slots and the in-shift pause menu (GDD §7, §7.2).</summary>
    public static class MenuScreens
    {
        public sealed class TitleActions
        {
            /// <summary>Null when there is no save to continue.</summary>
            public Action Continue;
            public string ContinueInfo;
            public Action NewGame;
            public Action LoadGame;
            public Action Endless;
            public string EndlessInfo;
            public Action Daily;
            public string DailyInfo;
            public Action Settings;
            public Action Credits;
            public Action Quit;
            /// <summary>Bottom-left line: what this build contains.</summary>
            public string BuildInfo = "";
        }

        /// <summary>Title (GDD §7.2): the tower at night with Window 3 lit; Continue, New Game, Endless, Daily, Settings.</summary>
        public static GameObject Title(RectTransform parent, TitleActions a)
        {
            var root = UiBuild.Panel("Title", parent, Palette.MossNight, rounded: false);
            root.rectTransform.Fill();
            var rt = root.rectTransform;
            // The tower art keeps its shape: a phone shows its middle (Window 3) behind a full-width shade.
            Layout.Cover("Tower", rt, Theme.Sprite("title"));
            // Shade the left side so the menu reads over the art.
            var shade = UiBuild.Picture("Shade", rt, Theme.Sprite("shade_left"));
            shade.preserveAspect = false;
            shade.rectTransform.PlaceTopLeft(0, 0, Layout.Pick(1100, Layout.W), Layout.H);

            // Portrait: a narrower left margin, everything lower and the buttons taller for thumbs.
            float x = Layout.Pick(120, 60), top = Layout.Pick(150, 220), w = Layout.W;
            Theme.Title("Kicker", rt, "VEL'TARA · LATTICE TOWER · NIGHT WINDOW 3", 24, Palette.BioGlow).rectTransform.PlaceTopLeft(x, top, w - x - 40, 32);
            var name = Theme.Title("Name", rt, "HUMAN ERROR", Layout.Pick(140, 118), Palette.PaperSand);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.rectTransform.PlaceTopLeft(x - 8, top + 36, 1400, 160);
            Theme.Body("Tagline", rt, "Spot the invaders at the night window.\nThe invaders are human.", 32, Palette.Sand)
                .rectTransform.PlaceTopLeft(x, top + Layout.Pick(210, 190), 900, 90);

            float y = top + Layout.Pick(320, 330), bh = Layout.Pick(64, 84), step = Layout.Pick(76, 100);
            Button Item(string label, Color fill, Color text, Action onClick, string note = null)
            {
                var b = Theme.Button(label, rt, label, fill, text, onClick, 30);
                ((RectTransform)b.transform).PlaceTopLeft(x, y, 440, bh);
                if (note != null)
                    Theme.Title("Note", rt, note, 22, Palette.Muted, TextAnchor.MiddleLeft).rectTransform.PlaceTopLeft(x + 464, y, Mathf.Min(420, w - x - 504), bh);
                y += step;
                return b;
            }

            var cont = Item("CONTINUE", Palette.BioGlow, Palette.Ink, a.Continue, a.ContinueInfo);
            cont.interactable = a.Continue != null;
            Item("NEW GAME", Palette.PanelLight, Palette.PaperSand, a.NewGame);
            Item("LOAD GAME", Palette.PanelLight, Palette.PaperSand, a.LoadGame);
            // Score modes (GDD §8): generated shifts, correct decisions × streak. The demo shows Endless, locked.
            Item("ENDLESS", Palette.UvViolet, Palette.PaperSand, a.Endless, a.EndlessInfo).interactable = a.Endless != null;
            Item("DAILY", Palette.Sand, Palette.Ink, a.Daily, a.DailyInfo);
            Item("SETTINGS", Palette.PanelLight, Palette.PaperSand, a.Settings);
            if (a.Quit != null) Item("QUIT", Palette.Panel, Palette.PaperSand, a.Quit);

            Theme.Body("Build", rt, a.BuildInfo, 20, Palette.Muted, TextAnchor.LowerLeft)
                .rectTransform.PlaceTopLeft(x, Layout.H - 60, w - x - 260, 40);
            if (a.Credits != null)
            {
                var credits = Theme.Button("Credits", rt, "CREDITS", Palette.Panel, Palette.Muted, a.Credits, 22);
                ((RectTransform)credits.transform).PlaceTopLeft(w - 220, Layout.H - Layout.Pick(76, 96), 180, Layout.Pick(52, 64));
            }
            return root.gameObject;
        }

        /// <summary>One slot as the Save Slots screen shows it (GDD §7.2: shift, act, shards; ending flags stay hidden).</summary>
        public sealed class SlotInfo
        {
            public int Slot;
            public SaveData Save;
            public DateTime? LastPlayed;
        }

        /// <summary>
        /// Three slots. New game: pick any slot (an occupied one needs a second press to overwrite).
        /// Load: pick an occupied slot.
        /// </summary>
        public static GameObject Slots(RectTransform parent, bool newGame, SlotInfo[] slots, Action<int> pick, Action back)
        {
            var root = UiBuild.Panel("Slots", parent, Palette.MossNight, rounded: false);
            root.rectTransform.Fill();
            var rt = root.rectTransform;
            // Landscape: three cards side by side. Portrait: three wide cards stacked, the details in two columns.
            bool portrait = Layout.Portrait;
            Theme.Title("Title", rt, newGame ? "NEW GAME · CHOOSE A SLOT" : "LOAD GAME", Layout.Pick(56, 50), Palette.PaperSand, TextAnchor.UpperCenter)
                .rectTransform.PlaceTopLeft(0, Layout.Pick(120, 100), Layout.W, 80);

            float w = Layout.Pick(500, Layout.W - 80), h = Layout.Pick(560, 420), gap = Layout.Pick(60, 24);
            float x0 = (Layout.W - 3 * w - 2 * gap) / 2;
            float side = portrait ? 520 : 36, sideY = portrait ? -190 : 0;
            for (int i = 0; i < slots.Length; i++)
            {
                var info = slots[i];
                var s = info.Save;
                var card = Theme.Panel($"Slot{info.Slot}", rt, s != null ? Palette.BioGlow.WithAlpha(0.6f) : null);
                if (portrait) card.rectTransform.PlaceTopLeft(40, 210 + i * (h + gap), w, h);
                else card.rectTransform.PlaceTopLeft(x0 + i * (w + gap), 260, w, h);
                var c = card.rectTransform;
                Theme.Title("Name", c, $"SLOT {info.Slot}", 34, Palette.BioGlow).rectTransform.PlaceTopLeft(36, 30, 420, 44);
                if (s != null && s.Finished)
                {
                    Theme.Title("Done", c, "COMPLETE", 56, Palette.BioGlow).rectTransform.PlaceTopLeft(36, 90, 420, 70);
                    Theme.Title("Ending", c, Endings.Title(s.Ending), 34, s.Ending == Ending.Exposed ? Palette.Coral : Palette.PaperSand)
                        .rectTransform.PlaceTopLeft(36, 176, 430, 44);
                    Theme.Body("Sub", c, Endings.Subtitle(s.Ending), 22, Palette.Muted).rectTransform.PlaceTopLeft(36, 226, 430, 90);
                }
                else if (s != null)
                {
                    int act = Acts.For(s.nextShift);
                    string where = s.atHome ? (s.last.shift == 0 ? "At home, before shift 1" : $"At home after shift {s.last.shift}") : $"During shift {s.nextShift}";
                    Theme.Title("Shift", c, $"SHIFT {s.nextShift}", 64, Palette.PaperSand).rectTransform.PlaceTopLeft(36, 90, 420, 80);
                    Theme.Body("Act", c, $"Act {act} · {Acts.Title(act)}\n{where}", 26, Palette.Muted).rectTransform.PlaceTopLeft(36, 180, 430, 80);
                    Theme.Title("Shards", c, Theme.Balance(s.shards), 44, s.shards < 0 ? Palette.Coral : Palette.Sand).rectTransform.PlaceTopLeft(side, 280 + sideY, 420, 56);
                    string when = info.LastPlayed.HasValue ? info.LastPlayed.Value.ToString("d MMM yyyy, HH:mm") : "";
                    Theme.Body("When", c, when, 22, Palette.Muted).rectTransform.PlaceTopLeft(side, 350 + sideY, 420, 30);
                }
                else Theme.Body("Empty", c, "Empty", 40, Palette.Muted).rectTransform.PlaceTopLeft(36, 100, 420, 60);

                bool can = newGame || s != null;
                string label = !newGame ? "LOAD" : s == null ? "START HERE" : "OVERWRITE";
                Button b = null;
                bool armed = false;
                int slot = info.Slot;
                b = Theme.Button("Pick", c, label, s != null && newGame ? Palette.Sand : Palette.BioGlow, Palette.Ink, () =>
                {
                    if (newGame && s != null && !armed)
                    {
                        armed = true;
                        b.GetComponentInChildren<Text>().text = "PRESS AGAIN TO ERASE";
                        b.GetComponent<Image>().color = Palette.Coral;
                        return;
                    }
                    pick(slot);
                }, 26);
                ((RectTransform)b.transform).PlaceTopLeft(36, h - 100, w - 72, Layout.Pick(70, 76));
                b.interactable = can;
            }

            var backB = Theme.Button("Back", rt, "BACK", Palette.PanelLight, Palette.PaperSand, back, 28);
            ((RectTransform)backB.transform).PlaceTopLeft(Layout.W / 2 - 150, portrait ? 210 + 3 * (h + gap) + 20 : 900, 300, Layout.Pick(68, 80));
            return root.gameObject;
        }

        /// <summary>The desk's pause menu: resume, rulebook, settings, or quit (the shift restarts from its briefing).</summary>
        public static GameObject Pause(RectTransform parent, Action resume, Action rulebook, Action settings, Action quit)
        {
            var dim = UiBuild.Panel("Pause", parent, Palette.Ink.WithAlpha(0.8f), rounded: false);
            dim.rectTransform.Fill();
            var card = Theme.Panel("Card", dim.transform);
            card.rectTransform.Centre(640, 610);
            var c = card.rectTransform;
            Theme.Title("Title", c, "PAUSED", 56, Palette.PaperSand, TextAnchor.UpperCenter).rectTransform.PlaceTopLeft(0, 40, 640, 70);
            Theme.Body("Clock", c, "The clock is stopped while you're here.", 22, Palette.Muted, TextAnchor.UpperCenter)
                .rectTransform.PlaceTopLeft(40, 106, 560, 30);
            void Close() => UnityEngine.Object.Destroy(dim.gameObject);
            float y = 150;
            void Item(string label, Color fill, Color text, Action onClick)
            {
                var b = Theme.Button(label, c, label, fill, text, onClick, 30);
                ((RectTransform)b.transform).PlaceTopLeft(120, y, 400, 72);
                y += 90;
            }
            Item("RESUME", Palette.BioGlow, Palette.Ink, () => { Close(); resume?.Invoke(); });
            Item("RULEBOOK", Palette.Sand, Palette.Ink, () => { Close(); rulebook?.Invoke(); });
            Item("SETTINGS", Palette.PanelLight, Palette.PaperSand, () => settings?.Invoke());
            Item("QUIT TO TITLE", Palette.PanelLight, Palette.PaperSand, () => { Close(); quit?.Invoke(); });
            Theme.Body("Note", c, "Quitting restarts tonight's shift from its briefing.", 22, Palette.Muted, TextAnchor.UpperCenter)
                .rectTransform.PlaceTopLeft(40, 520, 560, 60);
            return dim.gameObject;
        }

        /// <summary>
        /// Settings (GDD §7.2 "Comfort"): text size, colourblind palette, reduce motion, tell assist, timer off, volumes
        /// and window mode. Every change is reported at once through <paramref name="changed"/> with a fresh copy.
        /// </summary>
        public static GameObject Settings(RectTransform parent, Model.Settings current, Action<Model.Settings> changed, Action back)
        {
            var s = current.Clone();
            var dim = UiBuild.Panel("Settings", parent, Palette.Ink.WithAlpha(0.88f), rounded: false);
            dim.rectTransform.Fill();
            // Portrait: the card fills the phone's width and each control sits under its label instead of beside it.
            bool portrait = Layout.Portrait;
            float cw = Layout.Pick(1180f, Layout.W - 40);
            var card = Theme.Panel("Card", dim.transform);
            card.rectTransform.Centre(cw, Layout.Pick(1000f, Layout.H - 40));
            var c = card.rectTransform;
            Theme.Title("Title", c, "SETTINGS", 52, Palette.PaperSand).rectTransform.PlaceTopLeft(60, 36, 600, 64);

            float y = 120;
            float labelW = Layout.Pick(560, cw - 120), controlX = Layout.Pick(640, 60);
            // Returns the y of the label's control.
            float Label(string name, string help)
            {
                Theme.Title("Name", c, name, 28, Palette.PaperSand).rectTransform.PlaceTopLeft(60, y, labelW, 36);
                Text helpText = null;
                if (help != null)
                {
                    helpText = Theme.Body("Help", c, help, 20, Palette.Muted);
                    helpText.rectTransform.PlaceTopLeft(60, y + 36, labelW, 50);
                }
                if (!portrait) return y;
                return y + 40 + (helpText != null ? helpText.preferredHeight + 8 : 0);
            }
            void Toggle(string name, string help, bool value, Action<bool> set)
            {
                float cy = Label(name, help);
                for (int i = 0; i < 2; i++)
                {
                    bool on = i == 1;
                    bool picked = value == on;
                    var b = Theme.Button(on ? "On" : "Off", c, on ? "ON" : "OFF", picked ? Palette.BioGlow : Palette.PanelLight,
                        picked ? Palette.Ink : Palette.PaperSand, () => { set(on); changed(s.Clone()); }, 26);
                    ((RectTransform)b.transform).PlaceTopLeft(controlX + i * 250, portrait ? cy : y + 6, 230, 62);
                }
                y = portrait ? cy + 62 + 30 : y + 102;
            }
            void Volume(string name, float value, Action<float> set)
            {
                float cy = Label(name, null);
                var slider = UiBuild.Slider(name, c, value, Palette.PanelLight, Palette.BioGlow, Palette.PaperSand, v => { set(v); changed(s.Clone()); });
                ((RectTransform)slider.transform).PlaceTopLeft(controlX, cy, portrait ? cw - 120 : 480, 44);
                y = portrait ? cy + 44 + 26 : y + 60;
            }

            float sizeY = Label("TEXT SIZE", "Applies to every screen.");
            for (int i = 0; i < Model.Settings.TextScales.Length; i++)
            {
                int index = i;
                bool picked = s.textSize == i;
                var b = Theme.Button("Size", c, Model.Settings.TextScaleNames[i], picked ? Palette.BioGlow : Palette.PanelLight,
                    picked ? Palette.Ink : Palette.PaperSand, () => { s.textSize = index; changed(s.Clone()); }, 24);
                ((RectTransform)b.transform).PlaceTopLeft(controlX + i * 166, portrait ? sizeY : y + 6, 156, 62);
            }
            y = portrait ? sizeY + 62 + 30 : y + 96;
            Toggle("COLOURBLIND PALETTE", "Blue for alien glow, orange for danger.", s.colourblind, v => s.colourblind = v);
            Toggle("REDUCE MOTION", "No shakes or slides; suit flickers become a gentle highlight.", s.reduceMotion, v => s.reduceMotion = v);
            Toggle("TELL ASSIST", "A ring briefly outlines every blink, flicker, sweat drop and hand as it happens.", s.tellAssist, v => s.tellAssist = v);
            Toggle("TIMER OFF", "The clock only moves when you act. The window never closes early.", s.timerOff, v => s.timerOff = v);
            if (!Application.isMobilePlatform)
                Toggle("FULLSCREEN", null, s.fullscreen, v => s.fullscreen = v);
            Volume("MASTER VOLUME", s.master, v => s.master = v);
            Volume("MUSIC", s.music, v => s.music = v);
            Volume("SOUND EFFECTS", s.sfx, v => s.sfx = v);

            var backB = Theme.Button("Back", c, "BACK", Palette.BioGlow, Palette.Ink, () =>
            {
                UnityEngine.Object.Destroy(dim.gameObject);
                back?.Invoke();
            }, 30);
            if (portrait)
            {
                ((RectTransform)backB.transform).PlaceTopLeft(cw - 360, y + 10, 300, 80);
                card.rectTransform.Centre(cw, Mathf.Min(Layout.H - 40, y + 10 + 80 + 50));
                return dim.gameObject;
            }
            ((RectTransform)backB.transform).PlaceTopLeft(820, Mathf.Max(y + 10, 900), 300, 70);
            card.rectTransform.Centre(1180, Mathf.Min(1060, Mathf.Max(y + 10, 900) + 100));
            return dim.gameObject;
        }

        /// <summary>Credits and licences (M7): who made it, the version, and the full SIL OFL texts for the three fonts.</summary>
        public static GameObject Credits(RectTransform parent, string stamp, string edition, Action back)
        {
            var root = UiBuild.Panel("Credits", parent, Palette.MossNight, rounded: false);
            root.rectTransform.Fill();
            var rt = root.rectTransform;
            float w = Mathf.Min(1200, Layout.W - 80), x = (Layout.W - w) / 2;
            Theme.Title("Title", rt, "CREDITS", Layout.Pick(56, 50), Palette.PaperSand).rectTransform.PlaceTopLeft(x, 40, w, 70);
            Theme.Title("Stamp", rt, $"{edition} · {stamp}", 22, Palette.Muted, TextAnchor.UpperRight).rectTransform.PlaceTopLeft(x, 60, w, 30);

            var viewport = UiBuild.Rect("Page", rt);
            viewport.PlaceTopLeft(x, 130, w, Layout.H - 130 - 130);
            var page = Layout.ScrollPage(viewport);
            float y = 0;
            void Add(Text t, float gap)
            {
                t.rectTransform.PlaceTopLeft(0, y, w - 20, 40);
                t.rectTransform.sizeDelta = new Vector2(w - 20, t.preferredHeight);
                y += t.preferredHeight + gap;
            }
            Text Rich(Text t)
            {
                t.supportRichText = true;
                return t;
            }
            Add(Theme.Title("Game", page, "HUMAN ERROR", 44, Palette.BioGlow), 6);
            Add(Rich(Theme.Body("By", page, "A game by <b>Chuchu Games</b>.", 28, Palette.PaperSand)), 28);
            Add(Theme.Body("Made", page,
                "Design, writing and code: Chuchu Games\n" +
                "Placeholder art and sound: generated in code, to be replaced by final art\n" +
                "Made with Unity", 24, Palette.Sand), 28);
            Add(Theme.Title("FontsHead", page, "FONTS", 30, Palette.BioGlow), 8);
            Add(Rich(Theme.Body("Fonts", page,
                "<b>Chakra Petch</b> — Copyright 2018 The Chakra Petch Project Authors\n" +
                "<b>Atkinson Hyperlegible</b> — Copyright 2020 Braille Institute of America, Inc.\n" +
                "<b>IBM Plex Mono</b> — Copyright 2017 IBM Corp., with Reserved Font Name \"Plex\"\n" +
                "All three are licensed under the SIL Open Font License, Version 1.1 (full texts below).", 24, Palette.Sand)), 36);
            foreach (var name in new[] { "OFL-ChakraPetch", "OFL-Atkinson", "OFL-IBMPlexMono" })
            {
                var licence = Resources.Load<TextAsset>("Licenses/" + name);
                if (licence == null) continue;
                Add(Theme.Title("LicenceHead", page, name.Replace("OFL-", "LICENCE · ").ToUpperInvariant(), 24, Palette.BioGlow), 8);
                // One label per paragraph: a single Text holding a whole licence would pass uGUI's vertex limit.
                foreach (var para in licence.text.Replace("\r", "").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                    Add(Theme.Mono("Licence", page, para.Trim(), 17, Palette.Muted), 12);
                y += 24;
            }
            page.sizeDelta = new Vector2(0, y);

            var b = Theme.Button("Back", rt, "BACK", Palette.BioGlow, Palette.Ink, back, 30);
            ((RectTransform)b.transform).PlaceTopLeft(Layout.W / 2 - 150, Layout.H - 110, 300, 76);
            return root.gameObject;
        }
    }
}
