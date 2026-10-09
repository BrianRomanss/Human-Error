using ChuchuGames.UI;
using HumanError.Model;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanError.View
{
    /// <summary>
    /// Civic Lattice ID, appointment slip and medical pass (GDD §5.1, §7.1), drawn from a visitor's papers.
    /// Any field can be clicked to mark it, so the player can line up two fields when comparing; pressing a
    /// document brings it to the top of the pile.
    /// </summary>
    public static class DocumentViews
    {
        public const float IdWidth = 600, IdHeight = 316, SlipWidth = 400, SlipHeight = 316, PassWidth = 330, PassHeight = 200;

        /// <summary>The ID plus a handle to light its hologram under the UV lamp.</summary>
        public sealed class IdCardView
        {
            public RectTransform Root;
            Image _holo, _holoInner;
            bool _genuine;

            public void SetUv(bool on)
            {
                if (!on)
                {
                    _holo.color = Palette.BioGlow.WithAlpha(0.35f);
                    _holoInner.color = Palette.PaperSand;
                }
                else if (_genuine)
                {
                    _holo.color = Palette.BioGlow;
                    _holoInner.color = Palette.UvViolet;
                }
                else
                {
                    // A printed circle: no rings, no glow.
                    _holo.color = new Color(0.55f, 0.52f, 0.5f);
                    _holoInner.color = new Color(0.55f, 0.52f, 0.5f);
                }
            }

            public static IdCardView Build(Transform parent, VisitorInstance v)
            {
                var view = new IdCardView { _genuine = v.Id.HologramGenuine };
                var card = UiBuild.Panel("IdCard", parent, Palette.PaperSand);
                var rt = view.Root = card.rectTransform;
                rt.sizeDelta = new Vector2(IdWidth, IdHeight);
                card.gameObject.AddComponent<BringToFront>();

                var header = UiBuild.Panel("Header", rt, Palette.DocHeader);
                header.rectTransform.Band(RectTransform.Edge.Top, 54);
                Theme.Mono("Title", header.transform, "VEL'TARA · CIVIC LATTICE", 24, Palette.BioGlow, TextAnchor.MiddleLeft)
                    .rectTransform.Fill(18);
                var glyph = UiBuild.Picture("Glyph", header.transform, Theme.Sprite("glyph"));
                glyph.color = Palette.BioGlow;
                var g = glyph.rectTransform;
                g.anchorMin = g.anchorMax = g.pivot = new Vector2(1, 0.5f);
                g.sizeDelta = new Vector2(38, 38);
                g.anchoredPosition = new Vector2(-20, 0);
                if (!v.Id.GlyphUpright) g.localRotation = Quaternion.Euler(0, 0, 180);

                // Photo: the same kit parts as the face, in a clipped frame.
                var frame = UiBuild.Panel("PhotoFrame", rt, Palette.Ink);
                frame.rectTransform.PlaceTopLeft(22, 72, 170, 200);
                var photo = UiBuild.Panel("Photo", frame.transform, Palette.BoothGlass, rounded: false);
                photo.rectTransform.Fill(5);
                photo.gameObject.AddComponent<RectMask2D>();
                var face = VisitorView.Build(photo.transform, v.Id.Photo, new VisitorLookOptions { RoundPupils = v.Id.PhotoRoundPupils }, animated: false);
                var frt = (RectTransform)face.transform;
                frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0f);
                frt.pivot = new Vector2(0.5f, 0f);
                frt.sizeDelta = new Vector2(230, 230);
                frt.anchoredPosition = new Vector2(0, -24);

                float x = 214;
                Field(rt, "NAME", v.Id.Name, x, 74, 360);
                Field(rt, "CLAN", $"{v.Id.Clan.ToString().ToUpperInvariant()} · {Clans.Title(v.Id.Clan)}", x, 132, 360);
                Field(rt, "FLOOR ACCESS", v.Id.AccessText, x, 190, 360);
                Field(rt, "EXPIRES", v.Id.ExpiryText, x, 248, 200);

                view._holo = UiBuild.Picture("Hologram", rt, Theme.Sprite("circle"));
                view._holo.rectTransform.PlaceTopLeft(500, 228, 70, 70);
                view._holoInner = UiBuild.Picture("Inner", view._holo.transform, Theme.Sprite("circle"));
                view._holoInner.rectTransform.Fill(10);
                view.SetUv(false);
                return view;
            }
        }

        public static RectTransform Slip(Transform parent, VisitorInstance v)
        {
            var slip = UiBuild.Panel("Slip", parent, Palette.Slip);
            var rt = slip.rectTransform;
            rt.sizeDelta = new Vector2(SlipWidth, SlipHeight);
            slip.gameObject.AddComponent<BringToFront>();
            Theme.Mono("Title", rt, "APPOINTMENT", 26, Palette.Ink).rectTransform.PlaceTopLeft(24, 18, 300, 36);
            Field(rt, "NAME", v.Slip.Name, 24, 66, 352);
            Field(rt, "FLOOR · OFFICE", $"{v.Slip.Floor} · {v.Slip.Office}", 24, 124, 352);
            Field(rt, "TIME", v.Slip.Time, 24, 182, 150);
            Field(rt, "DATE", v.Slip.DateText, 190, 182, 160);

            if (v.Slip.BlueStamp)
            {
                var stamp = UiBuild.Picture("Stamp", rt, Theme.Sprite("circle"));
                stamp.color = Palette.StampBlue.WithAlpha(0.85f);
                stamp.rectTransform.PlaceTopLeft(300, 232, 70, 70);
                var inner = UiBuild.Picture("Inner", stamp.transform, Theme.Sprite("circle"));
                inner.color = Palette.Slip;
                inner.rectTransform.Fill(7);
                Theme.Mono("Mark", stamp.transform, "L", 26, Palette.StampBlue, TextAnchor.MiddleCenter).rectTransform.Fill();
            }
            return rt;
        }

        public static RectTransform MedicalPass(Transform parent, MedicalPass pass)
        {
            var card = UiBuild.Panel("MedicalPass", parent, Color.white);
            var rt = card.rectTransform;
            rt.sizeDelta = new Vector2(PassWidth, PassHeight);
            card.gameObject.AddComponent<BringToFront>();
            var band = UiBuild.Panel("Band", rt, Palette.Coral);
            band.rectTransform.Band(RectTransform.Edge.Top, 40);
            Theme.Mono("Title", band.transform, "+ LATTICE MEDICAL PASS", 19, Color.white, TextAnchor.MiddleLeft).rectTransform.Fill(14);
            Field(rt, "NAME", pass.Name, 18, 54, 290);
            Field(rt, "CONDITION", pass.ConditionText, 18, 116, 290);
            return rt;
        }

        static void Field(RectTransform doc, string label, string value, float x, float y, float width)
        {
            var box = UiBuild.Panel(label, doc, Palette.Warning.WithAlpha(0f));
            box.rectTransform.PlaceTopLeft(x - 6, y - 4, width + 12, 54);
            Theme.Mono("Label", box.transform, label, 15, Palette.Ink.WithAlpha(0.55f)).rectTransform.PlaceTopLeft(6, 4, width, 20);
            var t = Theme.Mono("Value", box.transform, value, 23, Palette.Ink);
            t.rectTransform.PlaceTopLeft(6, 22, width, 30);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 14;
            t.resizeTextMaxSize = 23;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            box.gameObject.AddComponent<Marker>();
        }

        /// <summary>Click to mark a field (yellow), click again to clear.</summary>
        sealed class Marker : MonoBehaviour, IPointerClickHandler
        {
            bool _on;

            public void OnPointerClick(PointerEventData e)
            {
                _on = !_on;
                GetComponent<Image>().color = Palette.Warning.WithAlpha(_on ? 0.45f : 0f);
                Theme.Sfx("paper", 0.4f, _on ? 1.1f : 0.9f);
            }
        }

        /// <summary>Pressing anywhere on a document puts it on top of the pile.</summary>
        sealed class BringToFront : MonoBehaviour, IPointerDownHandler
        {
            public void OnPointerDown(PointerEventData e) => transform.SetAsLastSibling();
        }
    }
}
