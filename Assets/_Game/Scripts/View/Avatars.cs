using ChuchuGames.UI;
using HumanError.Model;
using UnityEngine;
using UnityEngine.UI;

namespace HumanError.View
{
    /// <summary>
    /// Faces for message senders at Home, built from the same visitor kit (GDD §9: only story characters
    /// get unique art later; until then they are fixed kit combinations). Unknown senders get a mark.
    /// </summary>
    public static class Avatars
    {
        public static Look LookOf(string from)
        {
            switch (from)
            {
                case "Sel": return new Look { Clan = Clan.Ammaru, Head = 0, Mouth = 0, Pattern = 2, Outfit = 3 };
                case "Supervisor Graav": return new Look { Clan = Clan.Plo, Head = 5, Mouth = 3, Pattern = 1, Outfit = 5, Tone = 1 };
                case "Landlord Mott": return new Look { Clan = Clan.Vessi, Head = 2, Mouth = 2, Pattern = 0, Outfit = 7, Tone = 1 };
                case "Neighbour Vaun": return new Look { Clan = Clan.Ruu, Head = 3, Mouth = 1, Pattern = 6, Outfit = 6 };
                default: return null;
            }
        }

        /// <summary>A square portrait filling <paramref name="parent"/>.</summary>
        public static void Build(RectTransform parent, string from)
        {
            var look = LookOf(from);
            if (look != null)
            {
                var frame = UiBuild.Panel("Portrait", parent, Palette.BoothGlass);
                frame.rectTransform.Fill();
                frame.gameObject.AddComponent<RectMask2D>();
                var face = VisitorView.Build(frame.transform, look, null, animated: false);
                var rt = (RectTransform)face.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
                float size = parent.rect.width > 0 ? parent.rect.width * 1.35f : 120;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(0, -size * 0.12f);
                return;
            }
            Color fill = Palette.Ink, mark = Palette.Coral;
            string text = "?";
            Sprite glyph = null;
            switch (from)
            {
                case "Civic Lattice": fill = Palette.DocHeader; mark = Palette.BioGlow; glyph = Theme.Sprite("glyph"); break;
                case "Lattice Clinic": fill = Color.white; mark = Palette.Coral; text = "+"; break;
                case "Unknown": break;
                default: fill = Palette.PanelLight; mark = Palette.PaperSand; text = string.IsNullOrEmpty(from) ? "?" : from.Substring(0, 1); break;
            }
            var bg = UiBuild.Panel("Portrait", parent, fill);
            bg.rectTransform.Fill();
            if (glyph != null)
            {
                var g = UiBuild.Picture("Glyph", bg.transform, glyph);
                g.color = mark;
                g.rectTransform.Fill(14);
            }
            else Theme.Title("Mark", bg.transform, text, 48, mark, TextAnchor.MiddleCenter).rectTransform.Fill();
        }
    }
}
