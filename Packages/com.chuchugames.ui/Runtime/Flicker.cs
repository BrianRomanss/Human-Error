using UnityEngine;
using UnityEngine.UI;

namespace ChuchuGames.UI
{
    /// <summary>Candle-like flicker on a Graphic's alpha: mostly steady, with occasional dips.</summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class Flicker : MonoBehaviour
    {
        public float min = 0.55f, max = 1f;
        public float speed = 7f;

        Graphic _g;
        float _baseAlpha, _seed;

        void Awake()
        {
            _g = GetComponent<Graphic>();
            _baseAlpha = _g.color.a;
            _seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.unscaledTime * speed);
            float dip = Mathf.PerlinNoise(_seed + 50f, Time.unscaledTime * 0.7f) > 0.78f ? 0.5f : 1f; // rare stutters
            var c = _g.color;
            c.a = _baseAlpha * Mathf.Lerp(min, max, n) * dip;
            _g.color = c;
        }
    }
}
