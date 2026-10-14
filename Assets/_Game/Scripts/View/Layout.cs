using ChuchuGames.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HumanError.View
{
    /// <summary>
    /// The stage every screen is built on (M6 phone layout): 1920×1080 in landscape; in portrait 1080 wide and 1920
    /// or more tall (taller phones get the extra height). Screens read this while they build; GameFlow rebuilds the
    /// current screen when it changes (rotation, window resize).
    /// </summary>
    public static class Layout
    {
        public static bool Portrait { get; private set; }
        public static float W { get; private set; } = 1920;
        public static float H { get; private set; } = 1080;

        public static void From(StageCanvas stage)
        {
            Portrait = stage.Portrait;
            W = stage.Size.x;
            H = stage.Size.y;
        }

        /// <summary>Pick by orientation.</summary>
        public static T Pick<T>(T landscape, T portrait) => Portrait ? portrait : landscape;

        /// <summary>A full-stage background picture that keeps its aspect ratio and crops what doesn't fit (portrait shows the middle).</summary>
        public static Image Cover(string name, RectTransform parent, Sprite sprite, float aspect = 16f / 9f)
        {
            var bg = UiBuild.Picture(name, parent, sprite);
            bg.preserveAspect = false;
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = aspect;
            return bg;
        }

        /// <summary>A vertical scroll page filling <paramref name="viewport"/>; returns the content rect (top-anchored, height set by the caller).</summary>
        public static RectTransform ScrollPage(RectTransform viewport)
        {
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.enabled = true;
            if (viewport.GetComponent<Image>() == null)
            {
                var hit = viewport.gameObject.AddComponent<Image>(); // drags anywhere on the page scroll it
                hit.color = Color.clear;
            }
            var content = UiBuild.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40;
            return content;
        }
    }
}
