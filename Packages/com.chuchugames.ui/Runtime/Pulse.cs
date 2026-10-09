using UnityEngine;

namespace ChuchuGames.UI
{
    /// <summary>Breathing scale pulse, e.g. valid drop targets during a drag (GDD §8). Disabled by Reduce Motion.</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float amount = 0.03f;
        public float period = 0.9f;

        void OnDisable() => transform.localScale = Vector3.one;

        void Update()
        {
            float s = Tween.ReduceMotion ? 0f : (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / period) * 0.5f + 0.5f) * amount;
            transform.localScale = Vector3.one * (1 + s);
        }
    }
}
