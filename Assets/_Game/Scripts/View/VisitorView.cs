using ChuchuGames.UI;
using HumanError.Model;
using UnityEngine;
using UnityEngine.UI;

namespace HumanError.View
{
    /// <summary>What a visitor's body shows, independent of the papers (built from the truth by the desk).</summary>
    public sealed class VisitorLookOptions
    {
        public bool RoundPupils, HumanEar, Blinks, CanSweat, StillGills, DeadGlow, CanFlicker;

        public static VisitorLookOptions For(VisitorInstance v) => new VisitorLookOptions
        {
            RoundPupils = v.Has(Tell.RoundPupils),
            HumanEar = v.Has(Tell.HumanEar),
            Blinks = v.ShowsBlinking,
            CanSweat = v.Has(Tell.Sweats),
            StillGills = v.ShowsStillGills,
            DeadGlow = v.Has(Tell.DeadGlow),
            CanFlicker = v.Has(Tell.SuitFlicker),
        };
    }

    /// <summary>
    /// A visitor assembled from kit parts (GDD §9): one full-rect Image per layer on a shared 512×512 anchor
    /// grid, skin parts tinted by clan and tone. Animated mode adds the idle life — breathing glow, pulsing
    /// gills — and the tells: blinks (6 frames), sweat and suit flicker under stress, still gills, dead glow.
    /// The same builder draws the static ID photo.
    /// </summary>
    public sealed class VisitorView : MonoBehaviour
    {
        Image _eyelids, _glow, _gills, _seam;
        Image[] _sweat, _suitImages;
        RectTransform _suit;
        VisitorLookOptions _o;
        float _nextBlink, _blinkUntil, _nextFlicker, _flickerUntil;
        float _stress;
        bool _sweating, _flickering;

        /// <summary>0–1; higher stress makes blinks and flickers more frequent (GDD §5.3).</summary>
        public float Stress { set => _stress = value; }

        public bool Sweating
        {
            set
            {
                if (_sweat == null || _sweating == value) return;
                _sweating = value;
                foreach (var s in _sweat) s.gameObject.SetActive(value);
            }
        }

        /// <summary>The suit glitches every few seconds while true (stress above the flicker threshold).</summary>
        public bool Flickering { set => _flickering = value && _o.CanFlicker; }

        public static VisitorView Build(Transform parent, Look look, VisitorLookOptions o, bool animated)
        {
            o = o ?? new VisitorLookOptions();
            var root = UiBuild.Rect("Visitor", parent);
            var view = root.gameObject.AddComponent<VisitorView>();
            view._o = o;
            var skin = Palette.Skin(look.Clan, look.Tone);
            string clan = look.Clan.ToString();

            // The suit layers live under one rect so a flicker can jolt them together.
            view._suit = UiBuild.Rect("Suit", root).Fill();
            var suit = view._suit;
            Layer(suit, $"outfit_{look.Outfit}", Color.white);
            Layer(suit, "neck", skin);
            view._gills = Layer(suit, "gills", Color.white);
            if (o.HumanEar) Layer(suit, $"ear_{look.Head}", Color.white);
            var crest = Layer(suit, $"crest_{clan}_{look.Head}", skin);
            if (o.DeadGlow)
            {
                // A stuck-on crest sits crooked (pivot at the crown).
                crest.rectTransform.pivot = new Vector2(0.5f, 0.75f);
                crest.rectTransform.localRotation = Quaternion.Euler(0, 0, -7f);
            }
            Layer(suit, $"head_{look.Head}", skin);
            Layer(suit, $"pattern_{look.Pattern}_{look.Head}", skin);
            view._glow = Layer(suit, $"glow_{clan}_{look.Head}", o.DeadGlow ? new Color(0.45f, 0.5f, 0.5f) : Color.white);
            Layer(suit, $"eyes_{clan}_{look.Head}_{(o.RoundPupils ? "round" : "slit")}", Color.white);
            view._eyelids = Layer(suit, $"eyelids_{clan}_{look.Head}", skin);
            view._eyelids.gameObject.SetActive(false);
            Layer(suit, $"mouth_{look.Mouth}", Color.white);

            if (o.CanSweat)
            {
                // Drops on the brow in grid units (fractions of the 512 canvas).
                view._sweat = new[] { Drop(suit, 0.70f, 0.36f, 0f), Drop(suit, 0.27f, 0.42f, 0.9f) };
                foreach (var s in view._sweat) s.gameObject.SetActive(false);
            }
            if (o.CanFlicker)
            {
                view._seam = Layer(root, "seam", Color.white);
                view._seam.gameObject.SetActive(false);
            }
            view._nextBlink = Time.unscaledTime + Random.Range(1.2f, 2.5f);
            view._nextFlicker = Time.unscaledTime + Random.Range(1.5f, 3f);
            view.enabled = animated;
            return view;
        }

        static Image Layer(RectTransform root, string sprite, Color tint)
        {
            var img = UiBuild.Picture(sprite, root, Theme.Sprite(sprite));
            img.preserveAspect = false;
            img.rectTransform.Fill();
            img.color = tint;
            return img;
        }

        static Image Drop(RectTransform root, float x, float y, float phase)
        {
            var img = UiBuild.Picture("Sweat", root, Theme.Sprite("sweat"));
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(x, 1 - y);
            rt.sizeDelta = new Vector2(30, 45);
            var drip = img.gameObject.AddComponent<Drip>();
            drip.phase = phase;
            return img;
        }

        void Update()
        {
            float t = Time.unscaledTime;
            // Life: real glow breathes and real gills pulse every ~2 s (GDD §9). Fakes do neither.
            if (_glow != null && !_o.DeadGlow) _glow.color = new Color(1, 1, 1, 0.75f + 0.25f * Mathf.Sin(t * 2.4f));
            if (_gills != null)
                _gills.color = new Color(1, 1, 1, _o.StillGills ? 0.55f : 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI)));

            if (_o.Blinks && _eyelids != null)
            {
                if (_eyelids.gameObject.activeSelf && t >= _blinkUntil) _eyelids.gameObject.SetActive(false);
                if (t >= _nextBlink)
                {
                    _eyelids.gameObject.SetActive(true);
                    _blinkUntil = t + 0.1f; // ~6 frames at 60 fps (GDD §5.4)
                    float gap = Mathf.Lerp(4.5f, 1.6f, _stress);
                    _nextBlink = t + Random.Range(gap * 0.7f, gap * 1.3f);
                }
            }

            if (_seam != null)
            {
                bool on = t < _flickerUntil;
                if (!on && _flickering && t >= _nextFlicker)
                {
                    _flickerUntil = t + 0.09f; // a handful of frames: hard to catch (GDD §5.4)
                    _nextFlicker = t + Random.Range(2.2f, 4.5f);
                    on = true;
                }
                _seam.gameObject.SetActive(on);
                _suit.anchoredPosition = on && !Tween.ReduceMotion ? new Vector2(Mathf.Sin(t * 90f) * 10f, 0) : Vector2.zero;
                if (_suitImages == null) _suitImages = _suit.GetComponentsInChildren<Image>(true);
                foreach (var img in _suitImages)
                    if (img != _eyelids) img.canvasRenderer.SetAlpha(on ? 0.55f : 1f);
            }
        }

        /// <summary>A sweat bead that slowly runs down and reappears.</summary>
        sealed class Drip : MonoBehaviour
        {
            public float phase;
            RectTransform _rt;
            Image _img;
            Vector2 _base;

            void Start()
            {
                _rt = (RectTransform)transform;
                _img = GetComponent<Image>();
                _base = _rt.anchoredPosition;
            }

            void Update()
            {
                float p = Mathf.Repeat(Time.unscaledTime / 2.2f + phase, 1f);
                _rt.anchoredPosition = _base + new Vector2(0, Tween.ReduceMotion ? 0 : -p * 40f);
                _img.color = new Color(1, 1, 1, p < 0.8f ? 1f : (1 - p) * 5f);
            }
        }
    }
}
