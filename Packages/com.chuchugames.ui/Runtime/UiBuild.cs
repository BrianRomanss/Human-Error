using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChuchuGames.UI
{
    /// <summary>
    /// Helpers for building uGUI from code: fast grey-boxing without prefabs, and reproducible
    /// layouts that editor builder scripts can generate.
    /// </summary>
    public static class UiBuild
    {
        static Font _font;

        /// <summary>Font for <see cref="Label"/>. Defaults to Unity's built-in runtime font; assign your own (e.g. a TTF) at startup.</summary>
        public static Font DefaultFont
        {
            get => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            set => _font = value;
        }

        /// <summary>Optional 9-slice sprite used by <see cref="Panel"/> and <see cref="Button"/> for rounded corners.</summary>
        public static Sprite PanelSprite;
        public static Sprite ButtonSprite;

        /// <summary>Multiplies every <see cref="Label"/> font size (accessibility "text size"). Applies to UI built afterwards.</summary>
        public static float TextScale = 1f;

        /// <summary>Optional click sound for every <see cref="Button"/>.</summary>
        public static AudioClip ClickSound;

        public static Canvas Canvas(string name, Vector2 referenceResolution, float match = 0.5f, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = match;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Solid panel; rounded when <see cref="PanelSprite"/> is set and <paramref name="rounded"/> is true.</summary>
        public static Image Panel(string name, Transform parent, Color color, bool raycastTarget = true, bool rounded = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycastTarget;
            if (rounded && PanelSprite != null)
            {
                img.sprite = PanelSprite;
                img.type = UnityEngine.UI.Image.Type.Sliced;
            }
            return img;
        }

        /// <summary>Sprite image that keeps its aspect ratio inside its rect.</summary>
        public static Image Picture(string name, Transform parent, Sprite sprite, bool raycastTarget = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = raycastTarget;
            if (sprite == null) img.enabled = false; // missing art: hide rather than draw a white box
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = Mathf.RoundToInt(size * TextScale);
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(string name, Transform parent, string label, Color background, Color textColor,
            Action onClick, int fontSize = 28)
        {
            var img = Panel(name, parent, background);
            if (ButtonSprite != null) { img.sprite = ButtonSprite; img.type = UnityEngine.UI.Image.Type.Sliced; }
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() =>
            {
                if (ClickSound != null) AudioPlayer.Play(ClickSound, 0.6f);
                onClick();
            });
            Label("Label", img.transform, label, fontSize, textColor).rectTransform.Fill();
            return button;
        }

        /// <summary>Horizontal 0–1 slider with a track, fill and round handle (44 px+ touch target).</summary>
        public static Slider Slider(string name, Transform parent, float value, Color track, Color fill, Color handle,
            Action<float> onChanged)
        {
            var root = Rect(name, parent);
            var bg = Panel("Track", root, track);
            bg.rectTransform.anchorMin = new Vector2(0, 0.35f);
            bg.rectTransform.anchorMax = new Vector2(1, 0.65f);
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

            var fillArea = Rect("FillArea", root);
            fillArea.anchorMin = new Vector2(0, 0.35f);
            fillArea.anchorMax = new Vector2(1, 0.65f);
            fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
            var fillImg = Panel("Fill", fillArea, fill, raycastTarget: false);
            fillImg.rectTransform.sizeDelta = Vector2.zero;

            var handleArea = Rect("HandleArea", root).Fill();
            var h = Panel("Handle", handleArea, handle);
            h.rectTransform.anchorMin = new Vector2(0, 0); // Slider drives x anchors; y spans the full height
            h.rectTransform.anchorMax = new Vector2(0, 1);
            h.rectTransform.sizeDelta = new Vector2(44, 0);

            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fillImg.rectTransform;
            slider.handleRect = h.rectTransform;
            slider.targetGraphic = h;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = value;
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        /// <summary>Stretch to fill the parent, with optional inset.</summary>
        public static RectTransform Fill(this RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        /// <summary>
        /// Place a rect in reference-resolution pixels measured from the parent's top-left corner,
        /// which is how layouts are usually drawn in mock-ups.
        /// </summary>
        public static RectTransform PlaceTopLeft(this RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        /// <summary>Fixed size, centred on the parent.</summary>
        public static RectTransform Centre(this RectTransform rt, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        /// <summary>Full-length band of <paramref name="size"/> pixels along one edge of the parent, e.g. a top bar.</summary>
        public static RectTransform Band(this RectTransform rt, RectTransform.Edge edge, float size, float inset = 0f)
        {
            switch (edge)
            {
                case RectTransform.Edge.Top:
                    rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 1f);
                    rt.offsetMin = new Vector2(0f, -inset - size); rt.offsetMax = new Vector2(0f, -inset);
                    break;
                case RectTransform.Edge.Bottom:
                    rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f);
                    rt.offsetMin = new Vector2(0f, inset); rt.offsetMax = new Vector2(0f, inset + size);
                    break;
                case RectTransform.Edge.Left:
                    rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f);
                    rt.offsetMin = new Vector2(inset, 0f); rt.offsetMax = new Vector2(inset + size, 0f);
                    break;
                default:
                    rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(1f, 0.5f);
                    rt.offsetMin = new Vector2(-inset - size, 0f); rt.offsetMax = new Vector2(-inset, 0f);
                    break;
            }
            return rt;
        }

        public static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c) ? c : Color.magenta;
    }
}
