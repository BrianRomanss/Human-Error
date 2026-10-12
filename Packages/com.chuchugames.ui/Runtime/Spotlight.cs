using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChuchuGames.UI
{
    /// <summary>
    /// Tutorial spotlight: dims everything except one target, outlines it, points a bobbing hand at
    /// it and shows a tip. Tap anywhere to continue — or, with <c>waitForAction</c>, only the target
    /// stays clickable and the caller closes the spotlight when the player has done the action.
    /// No text walls: one short tip per step.
    /// </summary>
    public sealed class Spotlight : MonoBehaviour
    {
        Action _onDone;

        /// <param name="waitForAction">No tap-to-continue: clicks reach only the target; call <see cref="Close"/> when done.</param>
        /// <param name="onSkip">When set, a "skip tutorial" button closes the spotlight and calls it.</param>
        public static Spotlight Show(RectTransform canvasRoot, RectTransform target, string tip, Action onDone,
            Sprite hand = null, Color? outline = null, Color? bubble = null, Color? ink = null, float padding = 14f,
            bool waitForAction = false, Action onSkip = null)
        {
            var root = UiBuild.Rect("Spotlight", canvasRoot).Fill();
            root.SetAsLastSibling();
            var s = root.gameObject.AddComponent<Spotlight>();
            s._onDone = onDone;

            // Target rect in canvas-root space.
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = canvasRoot.InverseTransformPoint(corners[0]);
            Vector2 max = canvasRoot.InverseTransformPoint(corners[2]);
            min -= Vector2.one * padding;
            max += Vector2.one * padding;
            var size = canvasRoot.rect.size;
            var half = size / 2f;

            // Four dark panels around the hole (canvas-root local space is centred).
            var dim = new Color(0, 0, 0, 0.62f);
            Block(root, dim, new Vector2(-half.x, max.y), new Vector2(half.x, half.y));   // top
            Block(root, dim, new Vector2(-half.x, -half.y), new Vector2(half.x, min.y));  // bottom
            Block(root, dim, new Vector2(-half.x, min.y), new Vector2(min.x, max.y));     // left
            Block(root, dim, new Vector2(max.x, min.y), new Vector2(half.x, max.y));      // right

            // Outline around the hole (four thin bars).
            var oc = outline ?? new Color(0.69f, 0.54f, 0.24f);
            const float t = 5f;
            Block(root, oc, new Vector2(min.x - t, max.y), new Vector2(max.x + t, max.y + t), false);
            Block(root, oc, new Vector2(min.x - t, min.y - t), new Vector2(max.x + t, min.y), false);
            Block(root, oc, new Vector2(min.x - t, min.y), new Vector2(min.x, max.y), false);
            Block(root, oc, new Vector2(max.x, min.y), new Vector2(max.x + t, max.y), false);

            // Tip bubble: below the target if there's room, else above, else beside it; always on screen.
            const float bw = 640, gap = 130;
            // The bubble grows with the tip (measured with the label's font at the bubble's width).
            var measure = UiBuild.Label("Measure", root, tip, 30, Color.clear);
            measure.rectTransform.sizeDelta = new Vector2(bw - 40, 10);
            float bh = Mathf.Max(150, measure.preferredHeight + 70);
            Destroy(measure.gameObject);
            Vector2 centre = (min + max) / 2f;
            Vector2 bubblePos, handPos;
            float handAngle;
            if (min.y - gap - bh / 2 > -half.y) { bubblePos = new Vector2(centre.x, min.y - gap); handPos = new Vector2(centre.x, min.y - 40); handAngle = 0; }
            else if (max.y + gap + bh / 2 < half.y) { bubblePos = new Vector2(centre.x, max.y + gap); handPos = new Vector2(centre.x, max.y + 40); handAngle = 180; }
            else if (min.x - bw - 60 > -half.x) { bubblePos = new Vector2(min.x - bw / 2 - 90, centre.y); handPos = new Vector2(min.x - 45, centre.y); handAngle = -90; }
            else { bubblePos = new Vector2(max.x + bw / 2 + 90, centre.y); handPos = new Vector2(max.x + 45, centre.y); handAngle = 90; }
            float bx = Mathf.Clamp(bubblePos.x, -half.x + bw / 2 + 20, half.x - bw / 2 - 20);
            float by = Mathf.Clamp(bubblePos.y, -half.y + bh / 2 + 20, half.y - bh / 2 - 20);
            var bubbleImg = UiBuild.Panel("Tip", root, bubble ?? new Color(0.96f, 0.93f, 0.86f), raycastTarget: false);
            var brt = bubbleImg.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(bx, by);
            brt.sizeDelta = new Vector2(bw, bh);
            var text = UiBuild.Label("Text", brt, tip, 30, ink ?? new Color(0.16f, 0.14f, 0.2f));
            text.rectTransform.Fill(20);
            text.rectTransform.offsetMin = new Vector2(20, 34);
            if (!waitForAction)
                UiBuild.Label("Hint", brt, "tap to continue", 18, new Color(0.5f, 0.45f, 0.4f), TextAnchor.LowerRight).rectTransform.Fill(8);

            if (hand != null)
            {
                var h = UiBuild.Picture("Hand", root, hand);
                var hrt = h.rectTransform;
                hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, 0.5f);
                hrt.sizeDelta = new Vector2(110, 110);
                hrt.anchoredPosition = handPos;
                hrt.localRotation = Quaternion.Euler(0, 0, handAngle);
                h.gameObject.AddComponent<Bob>().amplitude = 10f;
            }

            if (!waitForAction)
            {
                // Whole-screen catcher so any tap continues.
                var catcher = UiBuild.Panel("Catcher", root, new Color(0, 0, 0, 0), raycastTarget: true, rounded: false);
                catcher.rectTransform.Fill();
                catcher.gameObject.AddComponent<Button>().onClick.AddListener(s.Done);
            }
            if (onSkip != null)
            {
                // A small link in the tip's corner, so it never covers a target. The bubble goes on top of the
                // tap catcher; it isn't a raycast target itself, so taps on its text still continue.
                var skip = UiBuild.Button("Skip", brt, "skip tutorial", new Color(0, 0, 0, 0.08f), ink ?? new Color(0.16f, 0.14f, 0.2f), () =>
                {
                    s._onDone = null;
                    s.Close();
                    onSkip();
                }, 18);
                var srt = (RectTransform)skip.transform;
                srt.anchorMin = srt.anchorMax = srt.pivot = Vector2.zero;
                srt.sizeDelta = new Vector2(150, 30);
                srt.anchoredPosition = new Vector2(10, 6);
                brt.SetAsLastSibling();
            }

            var group = root.gameObject.AddComponent<CanvasGroup>();
            Tween.Fade(group, 0, 1, 0.25f);
            return s;
        }

        void Done()
        {
            Destroy(gameObject);
            _onDone?.Invoke();
        }

        /// <summary>Removes the spotlight without calling onDone (for <c>waitForAction</c> steps).</summary>
        public void Close()
        {
            if (this != null) Destroy(gameObject);
        }

        static void Block(RectTransform parent, Color c, Vector2 min, Vector2 max, bool raycast = true)
        {
            var img = UiBuild.Panel("Dim", parent, c, raycast, rounded: false);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = min;
            rt.sizeDelta = new Vector2(Mathf.Max(0, max.x - min.x), Mathf.Max(0, max.y - min.y));
        }
    }
}
