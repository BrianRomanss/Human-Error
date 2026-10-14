using HumanError.Model;
using UnityEngine;

namespace HumanError.View
{
    /// <summary>GDD §9 palette. Teal glow = alien, coral = danger; colour is always paired with a word.</summary>
    public static class Palette
    {
        /// <summary>Colourblind palette (Settings): blue for alien, orange for danger, yellow for warnings.</summary>
        public static bool Colourblind;

        public static readonly Color MossNight = Hex(0x0E1A1F);
        public static readonly Color BoothGlass = Hex(0x173B44);
        public static Color BioGlow => Colourblind ? Hex(0x5AAEFF) : Hex(0x3FE0C5);
        public static Color Coral => Colourblind ? Hex(0xFF9A1F) : Hex(0xFF6B57);
        public static readonly Color PaperSand = Hex(0xE9DFC7);
        public static readonly Color Ink = Hex(0x111A1E);

        public static readonly Color Panel = Hex(0x13262C);
        public static readonly Color PanelLight = Hex(0x1C3740);
        public static readonly Color Counter = Hex(0x33251F);
        public static readonly Color CounterEdge = Hex(0x4A3A30);
        public static readonly Color Sand = Hex(0xD9C38E);
        public static readonly Color Muted = Hex(0x8FA7A8);
        public static Color Warning => Colourblind ? Hex(0xF4E86A) : Hex(0xE8B64A);
        public static readonly Color Slip = Hex(0xF4F0E4);
        public static readonly Color StampBlue = Hex(0x2F6FC0);
        public static readonly Color DocHeader = Hex(0x1F4D50);
        public static readonly Color UvViolet = Hex(0x6B3FD1);

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>A colour for rich text, e.g. $"&lt;color={Palette.Rich(Palette.BioGlow)}&gt;".</summary>
        public static string Rich(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Clan skin tints (GDD §9: lilac Vessi, mint Ammaru, peach Plo; Ruu sky blue from the kit sheet).</summary>
        public static Color Skin(Clan clan)
        {
            switch (clan)
            {
                case Clan.Vessi: return Hex(0xC7B8F5);
                case Clan.Ammaru: return Hex(0x9EE6B8);
                case Clan.Plo: return Hex(0xF6B48F);
                default: return Hex(0x8FC1EE);
            }
        }

        /// <summary>Clan recolour (GDD §8 "24 base + clan recolours"): tone 1 is a deeper shade of the clan colour.</summary>
        public static Color Skin(Clan clan, int tone) => tone == 0 ? Skin(clan) : Color.Lerp(Skin(clan), new Color(0.35f, 0.3f, 0.45f), 0.22f);

        public static Color KeshColour(string name)
        {
            switch (name)
            {
                case "red": return Hex(0xFF6B57);
                case "amber": return Hex(0xF2B84B);
                case "violet": return Hex(0xA98BF0);
                case "green": return Hex(0x7BE08F);
                default: return Hex(0xF2F2F2);
            }
        }

        public static Color For(Outcome o) =>
            o == Outcome.Correct ? BioGlow : o == Outcome.Safe ? Warning : o == Outcome.Citation ? Warning : Coral;

        static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
