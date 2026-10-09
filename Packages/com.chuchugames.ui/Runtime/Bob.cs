using UnityEngine;

namespace ChuchuGames.UI
{
    /// <summary>Gentle idle float (GDD §7.3: "sine bob, ~3 px"). Random phase so a crowd never moves in sync.</summary>
    public sealed class Bob : MonoBehaviour
    {
        public float amplitude = 4f;
        public float period = 2.6f;

        RectTransform _rt;
        Vector2 _base;
        float _phase;
        bool _hasBase;

        void OnEnable()
        {
            _rt = transform as RectTransform;
            _phase = Random.value * Mathf.PI * 2;
            _hasBase = false;
        }

        /// <summary>Call after moving the element so the bob centres on the new position.</summary>
        public void Rebase() => _hasBase = false;

        void LateUpdate()
        {
            if (_rt == null) return;
            if (!_hasBase)
            {
                _base = _rt.anchoredPosition;
                _hasBase = true;
            }
            float y = Tween.ReduceMotion ? 0f : Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / period + _phase) * amplitude;
            _rt.anchoredPosition = _base + new Vector2(0, y);
        }
    }
}
