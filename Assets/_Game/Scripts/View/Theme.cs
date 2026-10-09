using ChuchuGames.UI;
using HumanError.Data;
using UnityEngine;
using UnityEngine.UI;

namespace HumanError.View
{
    /// <summary>
    /// Look and sound of the code-built UI (GDD §9): Chakra Petch for titles, signage and buttons,
    /// Atkinson Hyperlegible for dialogue and UI, IBM Plex Mono for IDs and forms.
    /// </summary>
    public static class Theme
    {
        public static ArtLibrarySO Art { get; private set; }

        public static void Init(ArtLibrarySO art)
        {
            Art = art;
            if (art == null) return;
            if (art.ui != null) UiBuild.DefaultFont = art.ui;
            UiBuild.PanelSprite = art.Sprite("panel_9s");
            UiBuild.ButtonSprite = art.Sprite("button_9s");
            UiBuild.ClickSound = art.Clip("click");
        }

        static Font Or(Font f) => f != null ? f : UiBuild.DefaultFont;
        public static Font TitleFont => Or(Art != null ? Art.title : null);
        public static Font MonoFont => Or(Art != null ? Art.mono : null);
        public static Font BoldFont => Or(Art != null ? Art.uiBold : null);

        /// <summary>Signage style (Chakra Petch). Also the only font with the shard glyph ◆.</summary>
        public static Text Title(string name, Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var t = UiBuild.Label(name, parent, text, size, color, anchor);
            t.font = TitleFont;
            return t;
        }

        /// <summary>Typewriter style for documents (IBM Plex Mono).</summary>
        public static Text Mono(string name, Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var t = UiBuild.Label(name, parent, text, size, color, anchor);
            t.font = MonoFont;
            return t;
        }

        public static Text Body(string name, Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false)
        {
            var t = UiBuild.Label(name, parent, text, size, color, anchor);
            if (bold) t.font = BoldFont;
            return t;
        }

        /// <summary>Shard amount with sign, e.g. "+25 ◆" (Chakra Petch labels only).</summary>
        public static string Shards(int n) => (n > 0 ? "+" : n < 0 ? "−" : "") + Mathf.Abs(n) + " ◆";

        public static Sprite Sprite(string name) => Art != null ? Art.Sprite(name) : null;

        public static void Sfx(string name, float volume = 1f, float pitch = 1f)
        {
            if (Art != null) AudioPlayer.Play(Art.Clip(name), volume, pitch);
        }

        public static Button Button(string name, Transform parent, string label, Color fill, Color text, System.Action onClick, int size = 28)
        {
            var b = UiBuild.Button(name, parent, label, fill, text, onClick, size);
            b.GetComponentInChildren<Text>().font = TitleFont;
            return b;
        }

        /// <summary>Dark rounded panel with a thin teal edge (the desk's glass-and-metal look).</summary>
        public static Image Panel(string name, Transform parent, Color? edge = null, Color? fill = null)
        {
            var e = UiBuild.Panel(name, parent, edge ?? Palette.BioGlow.WithAlpha(0.35f));
            var inner = UiBuild.Panel("Fill", e.transform, fill ?? Palette.Panel);
            inner.rectTransform.Fill(2);
            return e;
        }
    }
}
