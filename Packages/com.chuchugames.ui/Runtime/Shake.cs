using UnityEngine;

namespace ChuchuGames.UI
{
    /// <summary>Decaying positional shake for a RectTransform (GDD: only for big moments; off with Reduce Motion).</summary>
    public static class Shake
    {
        public static void Play(RectTransform target, float strength = 14f, float duration = 0.45f)
        {
            if (target == null || Tween.ReduceMotion) return;
            var origin = target.anchoredPosition;
            Tween.Run(target, "shake", duration, Ease.Linear, p =>
            {
                if (!target) return;
                float k = (1 - p) * strength;
                target.anchoredPosition = origin + new Vector2(Mathf.Sin(p * 71f) * k, Mathf.Cos(p * 53f) * k);
            }, () => { if (target) target.anchoredPosition = origin; });
        }
    }
}
