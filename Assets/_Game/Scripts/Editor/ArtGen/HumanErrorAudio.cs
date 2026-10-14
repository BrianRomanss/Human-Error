using System;
using System.Collections.Generic;
using ChuchuGames.ProcArt;

namespace HumanError.EditorTools.ArtGen
{
    /// <summary>
    /// Placeholder audio (GDD §10): voice blips per clan, paper and stamp sounds, one sound per decision (door hiss,
    /// shutter clack, alarm chirp + boots), UV shimmer, and music loops — low synth pads with alien chimes, a tense
    /// pulse layer the desk fades in with the clock and the queue, and a quiet rainy room for Home. Every loop is
    /// 15 s at 96 bpm (24 beats) so layers stay on the beat. Same names are kept when final audio replaces them.
    /// </summary>
    public static class HumanErrorAudio
    {
        const float LoopSeconds = 15f, Beat = 60f / 96f, Tail = 2f;
        static readonly string[] Clans = { "Vessi", "Ammaru", "Plo", "Ruu" };

        public static List<string> GenerateAll(string root)
        {
            var files = new List<string>();
            void Save(string name, float[] samples)
            {
                var path = System.IO.Path.Combine(root, name + ".wav");
                Wav.Write(path, Synth.Declick(Synth.Normalize(samples, 0.7f)));
                files.Add(path);
            }
            // Loops must not be faded at the ends, or they'd dip at every repeat.
            void SaveLoop(string name, float[] samples, float peak)
            {
                var path = System.IO.Path.Combine(root, name + ".wav");
                Wav.Write(path, Synth.Normalize(samples, peak));
                files.Add(path);
            }

            Save("click", Synth.Env(Synth.Tone(0.05f, 1400, 900, Synth.Wave.Triangle), 0.002f, 0.045f));
            // Citizens: soft two-step chime. Humans: same, with a pitch wobble (an audio tell for attentive players).
            Save("blip_citizen", Synth.Env(Synth.Mix(Synth.Tone(0.09f, 620, 700), Synth.Mul(Synth.Tone(0.09f, 1240, 1400), 0.3f)), 0.005f, 0.08f));
            Save("blip_human", Synth.Env(Wobble(0.09f, 600, 40), 0.005f, 0.08f));
            // Each clan has its own voice (GDD §10); the desk wobbles the pitch of a stressed human's blips.
            float[] voice = { 700, 520, 380, 880 };
            Synth.Wave[] timbre = { Synth.Wave.Sine, Synth.Wave.Triangle, Synth.Wave.Sine, Synth.Wave.Triangle };
            for (int c = 0; c < 4; c++)
            {
                float f = voice[c];
                Save($"blip_{Clans[c]}", Synth.Env(Synth.Mix(
                    Synth.Tone(0.085f, f, f * (c == 2 ? 0.92f : 1.12f), timbre[c]),
                    Synth.Mul(Synth.Tone(0.085f, f * 2.01f, f * 2.2f), 0.25f)), 0.004f, 0.075f));
            }
            Save("paper", Synth.Env(Synth.LowPass(Synth.Noise(0.18f, 3), 5000, 1800), 0.01f, 0.16f));
            Save("stamp", Synth.Mix(Synth.Env(Synth.Tone(0.12f, 160, 60), 0.002f, 0.11f), Synth.Env(Synth.LowPass(Synth.Noise(0.06f, 5), 2500), 0.001f, 0.05f)));
            Save("door_hiss", Synth.Env(Synth.LowPass(Synth.Noise(0.6f, 9), 3500, 900), 0.05f, 0.5f));
            Save("shutter", Synth.Mix(
                Synth.Env(Synth.LowPass(Synth.Noise(0.25f, 11), 1500, 600), 0.01f, 0.2f),
                Synth.At(Synth.Silence(0.3f), Synth.Env(Synth.Tone(0.06f, 220, 90, Synth.Wave.Square, 0.4f), 0.001f, 0.05f), 0.2f)));
            var chirp = Synth.Env(Synth.Tone(0.14f, 880, 1320, Synth.Wave.Square, 0.35f), 0.005f, 0.12f);
            Save("alarm", Synth.Mix(chirp, Synth.At(Synth.Silence(0.5f), chirp, 0.2f)));
            Save("boots", Boots());
            Save("scan", Synth.Env(Synth.Tone(0.5f, 300, 1200, Synth.Wave.Sine, 0.4f), 0.05f, 0.4f, 0.3f));
            // UV lamp: a hum, and the hologram's shimmer (genuine) — a bright cluster of bells.
            Save("uv_on", Synth.Env(Synth.Mix(Synth.Tone(0.4f, 120, 120, Synth.Wave.Square, 0.15f), Synth.Tone(0.4f, 240, 250, Synth.Wave.Sine, 0.4f)), 0.03f, 0.35f));
            Save("shimmer", Synth.Mix(Synth.Bell(0.9f, 1760f, 0.4f), Synth.At(Synth.Silence(1f), Synth.Bell(0.8f, 2217f, 0.3f), 0.06f),
                Synth.At(Synth.Silence(1f), Synth.Bell(0.7f, 2637f, 0.25f), 0.12f)));
            Save("report", Synth.Mix(Synth.Bell(1.2f, 523.25f), Synth.At(Synth.Silence(1.4f), Synth.Bell(1.0f, 659.25f), 0.18f)));
            // The window closes at 02:00: three falling chimes.
            Save("closing", Synth.Mix(Synth.Bell(1.4f, 784f), Synth.At(Synth.Silence(1.8f), Synth.Bell(1.3f, 659.25f), 0.3f),
                Synth.At(Synth.Silence(2.2f), Synth.Bell(1.4f, 523.25f), 0.6f)));
            Save("act", Synth.Mix(Synth.Bell(2.4f, 196f, 0.8f), Synth.At(Synth.Silence(2.8f), Synth.Bell(2.2f, 293.66f, 0.6f), 0.35f),
                Synth.LowPass(Synth.Env(Synth.Tone(2.8f, 55, 55, Synth.Wave.Saw, 0.5f), 0.4f, 2.2f), 300)));
            // Home: a low, quiet two-note chime (GDD §10 "quiet room tone").
            Save("home", Synth.Mul(Synth.Mix(Synth.Bell(1.8f, 392f), Synth.At(Synth.Silence(2.1f), Synth.Bell(1.6f, 493.88f), 0.3f)), 0.6f));

            SaveLoop("music_desk", DeskPad(), 0.55f);
            SaveLoop("music_tension", TensionPulse(), 0.6f);
            SaveLoop("music_home", HomeRain(), 0.55f);
            SaveLoop("music_title", TitlePad(), 0.55f);
            return files;
        }

        // ------------------------------------------------------------------ music

        /// <summary>Night shift: four slow, slightly sour chords and a few alien chimes.</summary>
        static float[] DeskPad()
        {
            var buf = Synth.Silence(LoopSeconds + Tail);
            float[][] chords =
            {
                new[] { 73.42f, 146.83f, 220f, 261.63f, 349.23f },
                new[] { 58.27f, 116.54f, 174.61f, 233.08f, 293.66f },
                new[] { 65.41f, 130.81f, 196f, 246.94f, 329.63f },
                new[] { 55f, 110f, 164.81f, 220f, 277.18f },
            };
            float len = LoopSeconds / chords.Length;
            for (int c = 0; c < chords.Length; c++)
                foreach (var f in chords[c]) Add(buf, PadNote(len + 1.6f, f), c * len, f < 80 ? 0.35f : 0.18f);
            float[] chime = { 1046.5f, 1174.66f, 1396.91f, 1567.98f, 1760f };
            int[] at = { 3, 9, 14, 19, 22 };
            for (int i = 0; i < at.Length; i++) Add(buf, Synth.Bell(2.2f, chime[(i * 3) % chime.Length], 0.5f), at[i] * Beat, 0.12f);
            return Loop(Synth.LowPass(buf, 2400));
        }

        /// <summary>The tension layer (GDD §10 "a tense pulse that rises with the clock and the queue").</summary>
        static float[] TensionPulse()
        {
            var buf = Synth.Silence(LoopSeconds + Tail);
            var kick = Synth.Env(Synth.Tone(0.32f, 95, 42), 0.002f, 0.3f, 0f, 3f);
            var tick = Synth.Env(Synth.LowPass(Synth.Noise(0.05f, 21), 6000), 0.001f, 0.04f);
            for (int b = 0; b < 24; b++)
            {
                Add(buf, kick, b * Beat, b % 4 == 0 ? 0.9f : 0.6f);
                Add(buf, tick, b * Beat + Beat / 2, 0.25f);
            }
            // A low pulsing bass on eighth notes, a semitone rub on the last bar.
            for (int e = 0; e < 48; e++)
            {
                float f = e >= 36 ? 77.78f : 73.42f;
                Add(buf, Synth.LowPass(Synth.Env(Synth.Tone(Beat / 2, f, f, Synth.Wave.Saw, 0.5f), 0.005f, Beat / 2 * 0.9f), 500), e * Beat / 2, 0.35f);
            }
            return Loop(buf);
        }

        /// <summary>Home: rain on glass and a soft theme for Sel (GDD §10).</summary>
        static float[] HomeRain()
        {
            var rain = Synth.LowPass(Synth.Noise(LoopSeconds + Tail, 31), 1800);
            var rng = new Random(7);
            for (int i = 0; i < rain.Length; i++) rain[i] *= 0.6f + 0.15f * (float)Math.Sin(i * 0.00002);
            var buf = Synth.Mul(rain, 0.5f);
            for (int d = 0; d < 40; d++)
                Add(buf, Synth.Env(Synth.Tone(0.04f, 2400 + rng.Next(1600), 1800, Synth.Wave.Sine), 0.001f, 0.035f), (float)rng.NextDouble() * LoopSeconds, 0.08f);
            float[] theme = { 392f, 440f, 523.25f, 440f, 392f, 349.23f, 392f, 329.63f };
            for (int n = 0; n < theme.Length; n++) Add(buf, Synth.Bell(2.6f, theme[n], 0.5f), n * 3 * Beat, 0.22f);
            foreach (var f in new[] { 98f, 146.83f, 196f }) Add(buf, PadNote(LoopSeconds + Tail, f), 0, 0.12f);
            return Loop(buf);
        }

        /// <summary>Title: the tower at night. Wide, slow chords and distant chimes.</summary>
        static float[] TitlePad()
        {
            var buf = Synth.Silence(LoopSeconds + Tail);
            float[][] chords =
            {
                new[] { 65.41f, 130.81f, 196f, 311.13f },
                new[] { 51.91f, 103.83f, 207.65f, 311.13f },
                new[] { 58.27f, 116.54f, 174.61f, 293.66f },
                new[] { 49f, 98f, 196f, 293.66f },
            };
            float len = LoopSeconds / chords.Length;
            for (int c = 0; c < chords.Length; c++)
                foreach (var f in chords[c]) Add(buf, PadNote(len + 1.6f, f), c * len, f < 70 ? 0.35f : 0.2f);
            float[] chime = { 1244.51f, 1567.98f, 1864.66f };
            for (int i = 0; i < 6; i++) Add(buf, Synth.Bell(2.5f, chime[i % 3], 0.5f), (2 + i * 4) * Beat, 0.1f);
            return Loop(Synth.LowPass(buf, 2000));
        }

        /// <summary>A soft pad voice: a sine plus a quiet detuned triangle, fading in and out smoothly.</summary>
        static float[] PadNote(float seconds, float f)
        {
            var note = Synth.Mix(Synth.Tone(seconds, f), Synth.Mul(Synth.Tone(seconds, f * 1.004f, f * 1.004f, Synth.Wave.Triangle), 0.35f));
            int n = note.Length, ramp = Math.Min(n / 2, (int)(1.2f * Synth.SampleRate));
            for (int i = 0; i < ramp; i++)
            {
                float g = 0.5f - 0.5f * (float)Math.Cos(Math.PI * i / ramp);
                note[i] *= g;
                note[n - 1 - i] *= g;
            }
            return note;
        }

        /// <summary>Folds the <see cref="Tail"/> back over the start so the loop repeats without a seam.</summary>
        static float[] Loop(float[] buf)
        {
            int len = (int)(LoopSeconds * Synth.SampleRate), fade = buf.Length - len;
            var o = new float[len];
            Array.Copy(buf, o, len);
            for (int i = 0; i < fade && i < len; i++)
            {
                float g = (float)i / fade;
                o[i] = o[i] * g + buf[len + i] * (1 - g);
            }
            return o;
        }

        static void Add(float[] dst, float[] src, float seconds, float gain)
        {
            int off = (int)(seconds * Synth.SampleRate);
            for (int i = 0; i < src.Length && off + i < dst.Length; i++) dst[off + i] += src[i] * gain;
        }

        // ------------------------------------------------------------------ sfx

        /// <summary>Guards arriving: four heavy, slightly uneven steps.</summary>
        static float[] Boots()
        {
            var buf = Synth.Silence(1.1f);
            var step = Synth.Mix(Synth.Env(Synth.Tone(0.12f, 110, 50), 0.002f, 0.11f), Synth.Env(Synth.LowPass(Synth.Noise(0.08f, 41), 1200), 0.001f, 0.07f));
            float[] at = { 0f, 0.24f, 0.5f, 0.73f };
            for (int i = 0; i < at.Length; i++) Add(buf, step, at[i], i % 2 == 0 ? 1f : 0.8f);
            return buf;
        }

        static float[] Wobble(float seconds, float freq, float depth)
        {
            int n = (int)(seconds * Synth.SampleRate);
            var buf = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Synth.SampleRate;
                phase += 2 * Math.PI * (freq + depth * Math.Sin(2 * Math.PI * 23 * t)) / Synth.SampleRate;
                buf[i] = (float)Math.Sin(phase);
            }
            return buf;
        }
    }
}
