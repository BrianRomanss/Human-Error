using UnityEngine;

namespace ChuchuGames.UI
{
    /// <summary>Slow sideways drift and breathing opacity, e.g. fog layers. Off with Reduce Motion (opacity still breathes).</summary>
    public sealed class Drift : MonoBehaviour
    {
        public float distance = 60f;
        public float period = 24f;
        public float alphaMin = 0.6f, alphaMax = 1f;

        RectTransform _rt;
        CanvasGroup _group;
        Vector2 _base;
        float _phase;

        void OnEnable()
        {
            _rt = transform as RectTransform;
            _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _base = _rt != null ? _rt.anchoredPosition : Vector2.zero;
            _phase = Random.value * 10f;
        }

        void Update()
        {
            if (_rt == null) return;
            float t = (Time.unscaledTime + _phase) * Mathf.PI * 2 / period;
            if (!Tween.ReduceMotion) _rt.anchoredPosition = _base + new Vector2(Mathf.Sin(t) * distance, 0);
            _group.alpha = Mathf.Lerp(alphaMin, alphaMax, Mathf.Sin(t * 1.7f) * 0.5f + 0.5f);
        }
    }
}
