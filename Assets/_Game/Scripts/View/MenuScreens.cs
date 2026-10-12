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
            public Action Quit;
        }

        /// <summary>Title (GDD §7.2): the tower at night with Window 3 lit; Continue, New Game, Endless, Daily, Settings.</summary>
        public static GameObject Title(RectTransform parent, TitleActions a)
        {
            var root = UiBuild.Panel("Title", parent, Palette.MossNight, rounded: false);
            root.rectTransform.Fill();
            var rt = root.rectTransform;
            var bg = UiBuild.Picture("Tower", rt, Theme.Sprite("title"));
            bg.preserveAspect = false;
            bg.rectTransform.Fill();
            // Shade the left side so the menu reads over the art.
            var shade = UiBuild.Picture("Shade", rt, Theme.Sprite("shade_left"));
            shade.preserveAspect = false;
            shade.rectTransform.PlaceTopLeft(0, 0, 1100, 1080);

            Theme.Title("Kicker", rt, "VEL'TARA · LATTICE TOWER · NIGHT WINDOW 3", 24, Palette.BioGlow).rectTransform.PlaceTopLeft(120, 150, 900, 32);
            var name = Theme.Title("Name", rt, "HUMAN ERROR", 140, Palette.PaperSand);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.rectTransform.PlaceTopLeft(112, 186, 1400, 160);
            Theme.Body("Tagline", rt, "Spot the invaders at the night window.\nThe invaders are human.", 32, Palette.Sand)
                .rectTransform.PlaceTopLeft(120, 360, 900, 90);

            float y = 500;
            Button Item(string label, Color fill, Color text, Action onClick, string note = null)
            {
                var b = Theme.Button(label, rt, label, fill, text, onClick, 32);
                ((RectTransform)b.transform).PlaceTopLeft(120, y, 440, 72);
                if (note != null)
                    Theme.Title("Note", rt, note, 22, Palette.Muted, TextAnchor.MiddleLeft).rectTransform.PlaceTopLeft(584, y, 420, 72);
                y += 88;
                return b;
            }

            var cont = Item("CONTINUE", Palette.BioGlow, Palette.Ink, a.Continue, a.ContinueInfo);
            cont.interactable = a.Continue != null;
            Item("NEW GAME", Palette.PanelLight, Palette.PaperSand, a.NewGame);
            Item("LOAD GAME", Palette.PanelLight, Palette.PaperSand, a.LoadGame);
            // Score modes (GDD §8): generated shifts, correct decisions × streak.
            Item("ENDLESS", Palette.UvViolet, Palette.PaperSand, a.Endless, a.EndlessInfo);
            Item("DAILY", Palette.Sand, Palette.Ink, a.Daily, a.DailyInfo);
            if (a.Quit != null) Item("QUIT", Palette.Panel, Palette.PaperSand, a.Quit);

            Theme.Body("Build", rt, "Vertical slice · placeholder art · story shifts 1–6 · Endless · Daily", 20, Palette.Muted, TextAnchor.LowerLeft)
                .rectTransform.PlaceTopLeft(120, 1020, 900, 40);
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
            Theme.Title("Title", rt, newGame ? "NEW GAME · CHOOSE A SLOT" : "LOAD GAME", 56, Palette.PaperSand, TextAnchor.UpperCenter)
                .rectTransform.PlaceTopLeft(0, 120, 1920, 80);

            const float w = 500, h = 560, gap = 60;
            float x0 = (1920 - 3 * w - 2 * gap) / 2;
            for (int i = 0; i < slots.Length; i++)
            {
                var info = slots[i];
                var s = info.Save;
                var card = Theme.Panel($"Slot{info.Slot}", rt, s != null ? Palette.BioGlow.WithAlpha(0.6f) : null);
                card.rectTransform.PlaceTopLeft(x0 + i * (w + gap), 260, w, h);
                var c = card.rectTransform;
                Theme.Title("Name", c, $"SLOT {info.Slot}", 34, Palette.BioGlow).rectTransform.PlaceTopLeft(36, 30, 420, 44);
                if (s != null)
                {
                    int act = Acts.For(s.nextShift);
                    string where = s.atHome ? (s.last.shift == 0 ? "At home, before shift 1" : $"At home after shift {s.last.shift}") : $"During shift {s.nextShift}";
                    Theme.Title("Shift", c, $"SHIFT {s.nextShift}", 64, Palette.PaperSand).rectTransform.PlaceTopLeft(36, 90, 420, 80);
                    Theme.Body("Act", c, $"Act {act} · {Acts.Title(act)}\n{where}", 26, Palette.Muted).rectTransform.PlaceTopLeft(36, 180, 430, 80);
                    Theme.Title("Shards", c, Theme.Balance(s.shards), 44, s.shards < 0 ? Palette.Coral : Palette.Sand).rectTransform.PlaceTopLeft(36, 280, 420, 56);
                    string when = info.LastPlayed.HasValue ? info.LastPlayed.Value.ToString("d MMM yyyy, HH:mm") : "";
                    Theme.Body("When", c, when, 22, Palette.Muted).rectTransform.PlaceTopLeft(36, 350, 420, 30);
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
                ((RectTransform)b.transform).PlaceTopLeft(36, 460, 428, 70);
                b.interactable = can;
            }

            var backB = Theme.Button("Back", rt, "BACK", Palette.PanelLight, Palette.PaperSand, back, 28);
            ((RectTransform)backB.transform).PlaceTopLeft(810, 900, 300, 68);
            return root.gameObject;
        }

        /// <summary>The desk's pause menu: resume, rulebook, or quit (the shift restarts from its briefing).</summary>
        public static GameObject Pause(RectTransform parent, Action resume, Action rulebook, Action quit)
        {
            var dim = UiBuild.Panel("Pause", parent, Palette.Ink.WithAlpha(0.8f), rounded: false);
            dim.rectTransform.Fill();
            var card = Theme.Panel("Card", dim.transform);
            card.rectTransform.Centre(640, 520);
            var c = card.rectTransform;
            Theme.Title("Title", c, "PAUSED", 56, Palette.PaperSand, TextAnchor.UpperCenter).rectTransform.PlaceTopLeft(0, 40, 640, 70);
            void Close() => UnityEngine.Object.Destroy(dim.gameObject);
            float y = 140;
            void Item(string label, Color fill, Color text, Action onClick)
            {
                var b = Theme.Button(label, c, label, fill, text, onClick, 30);
                ((RectTransform)b.transform).PlaceTopLeft(120, y, 400, 72);
                y += 90;
            }
            Item("RESUME", Palette.BioGlow, Palette.Ink, () => { Close(); resume?.Invoke(); });
            Item("RULEBOOK", Palette.Sand, Palette.Ink, () => { Close(); rulebook?.Invoke(); });
            Item("QUIT TO TITLE", Palette.PanelLight, Palette.PaperSand, () => { Close(); quit?.Invoke(); });
            Theme.Body("Note", c, "Quitting restarts tonight's shift from its briefing.", 22, Palette.Muted, TextAnchor.UpperCenter)
                .rectTransform.PlaceTopLeft(40, 420, 560, 60);
            return dim.gameObject;
        }
    }
}
