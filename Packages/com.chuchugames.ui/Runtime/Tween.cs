using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChuchuGames.UI
{
    public enum Ease
    {
        Linear,
        OutQuad,
        InOutQuad,
        OutBack,
        OutElastic,
    }

    /// <summary>
    /// Dependency-free tweening for UI juice. One tween per (target, channel): starting a new one
    /// replaces the old, so rapid input never stacks animations. Uses unscaled time, so it keeps
    /// working while the game is paused.
    /// </summary>
    public static class Tween
    {
        /// <summary>Accessibility: when true, movement/scale/shake tweens jump to their end (fades still play).</summary>
        public static bool ReduceMotion;

        static Runner _runner;
        static readonly Dictionary<(int, string), Coroutine> Active = new Dictionary<(int, string), Coroutine>();

        public static float Evaluate(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.OutQuad: return 1 - (1 - t) * (1 - t);
                case Ease.InOutQuad: return t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
                case Ease.OutBack:
                    const float c1 = 1.70158f, c3 = c1 + 1;
                    return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
                case Ease.OutElastic:
                    if (t <= 0 || t >= 1) return t;
                    return Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * (2 * Mathf.PI / 3)) + 1;
                default: return t;
            }
        }

        /// <summary>Generic tween: calls <paramref name="step"/> with eased 0→1 progress.</summary>
        public static void Run(UnityEngine.Object target, string channel, float duration, Ease ease, Action<float> step,
            Action done = null, bool isMotion = true, float delay = 0f)
        {
            if (target == null) return;
            Stop(target, channel);
            if (duration <= 0f || (isMotion && ReduceMotion))
            {
                step(1f);
                done?.Invoke();
                return;
            }
            var key = (target.GetInstanceID(), channel);
            Active[key] = R.StartCoroutine(Routine(key, target, duration, ease, step, done, delay));
        }

        public static void Stop(UnityEngine.Object target, string channel)
        {
            if (target == null) return;
            var key = (target.GetInstanceID(), channel);
            if (Active.TryGetValue(key, out var co))
            {
                if (co != null && _runner != null) _runner.StopCoroutine(co);
                Active.Remove(key);
            }
        }

        public static void Scale(Transform t, Vector3 from, Vector3 to, float duration, Ease ease = Ease.OutBack, Action done = null, float delay = 0f)
        {
            if (t == null) return;
            t.localScale = from;
            Run(t, "scale", duration, ease, p => { if (t) t.localScale = Vector3.LerpUnclamped(from, to, p); }, done, true, delay);
        }

        /// <summary>Squash-and-stretch pop back to 1 (pick-up, drop, star appearing).</summary>
        public static void Punch(Transform t, float amount = 0.15f, float duration = 0.35f)
        {
            if (t == null) return;
            Run(t, "scale", duration, Ease.Linear, p =>
            {
                if (!t) return;
                float s = Mathf.Sin(p * Mathf.PI * 2.5f) * (1 - p) * amount;
                t.localScale = new Vector3(1 + s, 1 - s, 1);
            }, () => { if (t) t.localScale = Vector3.one; });
        }

        public static void Move(RectTransform t, Vector2 from, Vector2 to, float duration, Ease ease = Ease.OutQuad, Action done = null, float delay = 0f)
        {
            if (t == null) return;
            t.anchoredPosition = from;
            Run(t, "move", duration, ease, p => { if (t) t.anchoredPosition = Vector2.LerpUnclamped(from, to, p); }, done, true, delay);
        }

        public static void Fade(CanvasGroup g, float from, float to, float duration, Action done = null, float delay = 0f)
        {
            if (g == null) return;
            g.alpha = from;
            Run(g, "fade", duration, Ease.Linear, p => { if (g) g.alpha = Mathf.Lerp(from, to, p); }, done, isMotion: false, delay: delay);
        }

        /// <summary>Calls <paramref name="action"/> after a delay in unscaled seconds.</summary>
        public static void Delay(UnityEngine.Object owner, float seconds, Action action, string channel = "delay") =>
            Run(owner, channel, 0.0001f, Ease.Linear, _ => { }, action, isMotion: false, delay: seconds);

        static IEnumerator Routine((int, string) key, UnityEngine.Object target, float duration, Ease ease,
            Action<float> step, Action done, float delay)
        {
            if (delay > 0) yield return new WaitForSecondsRealtime(delay);
            float t = 0;
            while (t < duration)
            {
                if (target == null) { Active.Remove(key); yield break; }
                step(Evaluate(ease, t / duration));
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Active.Remove(key);
            if (target == null) yield break;
            step(1f);
            done?.Invoke();
        }

        static Runner R
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("[Tween]") { hideFlags = HideFlags.HideAndDontSave };
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _runner = go.AddComponent<Runner>();
                }
                return _runner;
            }
        }

        sealed class Runner : MonoBehaviour { }
    }
}
