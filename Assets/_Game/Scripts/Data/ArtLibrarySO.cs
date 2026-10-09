using System.Collections.Generic;
using UnityEngine;

namespace HumanError.Data
{
    /// <summary>
    /// Every sprite, sound and font the code-built UI uses, looked up by file name (e.g. "head_2",
    /// "eyes_Vessi_0_slit", "alarm"). Filled by Tools/Human Error/Generate Placeholder Art, so swapping in
    /// final art is just replacing files with the same names.
    /// </summary>
    [CreateAssetMenu(menuName = "Human Error/Art Library", fileName = "ArtLibrary")]
    public sealed class ArtLibrarySO : ScriptableObject
    {
        public Font title;
        public Font ui;
        public Font uiBold;
        public Font mono;
        public Sprite[] sprites = new Sprite[0];
        public AudioClip[] clips = new AudioClip[0];

        Dictionary<string, Sprite> _sprites;
        Dictionary<string, AudioClip> _clips;

        public Sprite Sprite(string name)
        {
            if (_sprites == null)
            {
                _sprites = new Dictionary<string, Sprite>();
                foreach (var s in sprites) if (s != null) _sprites[s.name] = s;
            }
            return _sprites.TryGetValue(name, out var sprite) ? sprite : null;
        }

        public AudioClip Clip(string name)
        {
            if (_clips == null)
            {
                _clips = new Dictionary<string, AudioClip>();
                foreach (var c in clips) if (c != null) _clips[c.name] = c;
            }
            return _clips.TryGetValue(name, out var clip) ? clip : null;
        }

        void OnValidate()
        {
            _sprites = null;
            _clips = null;
        }
    }
}
