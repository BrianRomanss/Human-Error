using UnityEngine;

namespace ChuchuGames.UI
{
    /// <summary>
    /// Fire-and-forget SFX with pitch control (rising chimes, voice blips) and one looping music
    /// track. A small pool of AudioSources means overlapping sounds don't cut each other off.
    /// Volumes are 0–1 and multiply: master × sfx / master × music.
    /// </summary>
    public static class AudioPlayer
    {
        public static float Master = 1f, Sfx = 1f, Music = 0.6f;

        const int PoolSize = 12;
        static AudioSource[] _pool;
        static AudioSource _music;
        static int _next;

        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            EnsurePool();
            var src = _pool[_next];
            _next = (_next + 1) % PoolSize;
            src.pitch = pitch;
            src.volume = volume * Master * Sfx;
            src.clip = clip;
            src.Play();
        }

        /// <summary>Plays with a small random pitch wobble so repeated sounds don't feel mechanical.</summary>
        public static void PlayVaried(AudioClip clip, float volume = 1f, float spread = 0.06f) =>
            Play(clip, volume, 1f + Random.Range(-spread, spread));

        public static void PlayMusic(AudioClip clip)
        {
            EnsurePool();
            if (_music.clip == clip && _music.isPlaying) return;
            _music.clip = clip;
            _music.loop = true;
            ApplyMusicVolume();
            if (clip != null) _music.Play();
            else _music.Stop();
        }

        public static void ApplyMusicVolume()
        {
            if (_music != null) _music.volume = Master * Music;
        }

        static void EnsurePool()
        {
            if (_pool != null && _pool[0] != null) return;
            var go = new GameObject("[Audio]") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                _pool[i] = go.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
            }
            _music = go.AddComponent<AudioSource>();
            _music.playOnAwake = false;
        }
    }
}
