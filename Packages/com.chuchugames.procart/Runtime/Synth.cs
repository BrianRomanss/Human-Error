using System;

namespace ChuchuGames.ProcArt
{
    /// <summary>
    /// A tiny offline synth for placeholder SFX: oscillators, noise, envelopes, a low-pass filter
    /// and mixing. All buffers are mono floats at <see cref="SampleRate"/>.
    /// </summary>
    public static class Synth
    {
        public const int SampleRate = 44100;
        const double Tau = Math.PI * 2;

        public static float[] Silence(float seconds) => new float[(int)(seconds * SampleRate)];

        public enum Wave { Sine, Triangle, Saw, Square }

        /// <summary>Oscillator gliding from <paramref name="freqFrom"/> to <paramref name="freqTo"/> Hz.</summary>
        public static float[] Tone(float seconds, float freqFrom, float freqTo = -1, Wave wave = Wave.Sine, float amp = 1f)
        {
            if (freqTo < 0) freqTo = freqFrom;
            var buf = Silence(seconds);
            double phase = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                double t = (double)i / buf.Length;
                double f = freqFrom * Math.Pow(freqTo / freqFrom, t); // exponential glide sounds natural
                phase += f / SampleRate;
                double p = phase - Math.Floor(phase);
                double v;
                switch (wave)
                {
                    case Wave.Triangle: v = 1 - 4 * Math.Abs(p - 0.5); break;
                    case Wave.Saw: v = 2 * p - 1; break;
                    case Wave.Square: v = p < 0.5 ? 1 : -1; break;
                    default: v = Math.Sin(phase * Tau); break;
                }
                buf[i] = (float)(v * amp);
            }
            return buf;
        }

        /// <summary>A bell-like tone: a fundamental plus inharmonic partials, each decaying.</summary>
        public static float[] Bell(float seconds, float freq, float amp = 0.6f)
        {
            float[][] partials =
            {
                Env(Tone(seconds, freq), 0.002f, seconds * 0.9f, 0f, 3f),
                Mul(Env(Tone(seconds, freq * 2.76f), 0.002f, seconds * 0.5f, 0f, 4f), 0.4f),
                Mul(Env(Tone(seconds, freq * 5.4f), 0.002f, seconds * 0.25f, 0f, 5f), 0.2f),
            };
            return Mul(Mix(partials), amp);
        }

        /// <summary>Seeded white noise.</summary>
        public static float[] Noise(float seconds, int seed = 1, float amp = 1f)
        {
            var rng = new Random(seed);
            var buf = Silence(seconds);
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)(rng.NextDouble() * 2 - 1) * amp;
            return buf;
        }

        /// <summary>Attack / decay / sustain-level envelope; <paramref name="curve"/> &gt; 1 makes the decay exponential-ish.</summary>
        public static float[] Env(float[] buf, float attack, float decay, float sustain = 0f, float curve = 2f)
        {
            int a = Math.Max(1, (int)(attack * SampleRate)), d = Math.Max(1, (int)(decay * SampleRate));
            for (int i = 0; i < buf.Length; i++)
            {
                float g;
                if (i < a) g = (float)i / a;
                else if (i < a + d) g = sustain + (1 - sustain) * (float)Math.Pow(1 - (float)(i - a) / d, curve);
                else g = sustain;
                buf[i] *= g;
            }
            return buf;
        }

        /// <summary>One-pole low-pass. cutoff can glide from → to Hz over the buffer.</summary>
        public static float[] LowPass(float[] buf, float cutoffFrom, float cutoffTo = -1)
        {
            if (cutoffTo < 0) cutoffTo = cutoffFrom;
            float y = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                float fc = cutoffFrom + (cutoffTo - cutoffFrom) * i / buf.Length;
                float a = 1f - (float)Math.Exp(-Tau * fc / SampleRate);
                y += a * (buf[i] - y);
                buf[i] = y;
            }
            return buf;
        }

        public static float[] Mul(float[] buf, float k)
        {
            for (int i = 0; i < buf.Length; i++) buf[i] *= k;
            return buf;
        }

        /// <summary>Sums buffers (the result is as long as the longest).</summary>
        public static float[] Mix(params float[][] bufs)
        {
            int n = 0;
            foreach (var b in bufs) n = Math.Max(n, b.Length);
            var o = new float[n];
            foreach (var b in bufs)
                for (int i = 0; i < b.Length; i++) o[i] += b[i];
            return o;
        }

        /// <summary>Places <paramref name="b"/> into <paramref name="into"/> starting at a time offset (grows as needed).</summary>
        public static float[] At(float[] into, float[] b, float seconds)
        {
            int off = (int)(seconds * SampleRate);
            var o = new float[Math.Max(into.Length, off + b.Length)];
            Array.Copy(into, o, into.Length);
            for (int i = 0; i < b.Length; i++) o[off + i] += b[i];
            return o;
        }

        /// <summary>Scales so the loudest sample is at <paramref name="peak"/>.</summary>
        public static float[] Normalize(float[] buf, float peak = 0.8f)
        {
            float max = 0;
            foreach (var s in buf) max = Math.Max(max, Math.Abs(s));
            return max > 0 ? Mul(buf, peak / max) : buf;
        }

        /// <summary>Short fade in/out to avoid clicks.</summary>
        public static float[] Declick(float[] buf, float seconds = 0.004f)
        {
            int n = Math.Min(buf.Length / 2, (int)(seconds * SampleRate));
            for (int i = 0; i < n; i++)
            {
                float g = (float)i / n;
                buf[i] *= g;
                buf[buf.Length - 1 - i] *= g;
            }
            return buf;
        }
    }
}
