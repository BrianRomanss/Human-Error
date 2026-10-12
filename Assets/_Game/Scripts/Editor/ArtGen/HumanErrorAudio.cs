using System;
using System.Collections.Generic;
using ChuchuGames.ProcArt;

namespace HumanError.EditorTools.ArtGen
{
    /// <summary>
    /// Placeholder SFX (GDD §10): voice blips per species, paper and stamp sounds, and one sound per decision
    /// (door hiss, shutter clack, alarm chirp). Same names are kept when final audio replaces them.
    /// </summary>
    public static class HumanErrorAudio
    {
        public static List<string> GenerateAll(string root)
        {
            var files = new List<string>();
            void Save(string name, float[] samples)
            {
                var path = System.IO.Path.Combine(root, name + ".wav");
                Wav.Write(path, Synth.Declick(Synth.Normalize(samples, 0.7f)));
                files.Add(path);
            }

            Save("click", Synth.Env(Synth.Tone(0.05f, 1400, 900, Synth.Wave.Triangle), 0.002f, 0.045f));
            // Citizens: soft two-step chime. Humans: same, with a pitch wobble (an audio tell for attentive players).
            Save("blip_citizen", Synth.Env(Synth.Mix(Synth.Tone(0.09f, 620, 700), Synth.Mul(Synth.Tone(0.09f, 1240, 1400), 0.3f)), 0.005f, 0.08f));
            Save("blip_human", Synth.Env(Wobble(0.09f, 600, 40), 0.005f, 0.08f));
            Save("paper", Synth.Env(Synth.LowPass(Synth.Noise(0.18f, 3), 5000, 1800), 0.01f, 0.16f));
            Save("stamp", Synth.Mix(Synth.Env(Synth.Tone(0.12f, 160, 60), 0.002f, 0.11f), Synth.Env(Synth.LowPass(Synth.Noise(0.06f, 5), 2500), 0.001f, 0.05f)));
            Save("door_hiss", Synth.Env(Synth.LowPass(Synth.Noise(0.6f, 9), 3500, 900), 0.05f, 0.5f));
            Save("shutter", Synth.Mix(
                Synth.Env(Synth.LowPass(Synth.Noise(0.25f, 11), 1500, 600), 0.01f, 0.2f),
                Synth.At(Synth.Silence(0.3f), Synth.Env(Synth.Tone(0.06f, 220, 90, Synth.Wave.Square, 0.4f), 0.001f, 0.05f), 0.2f)));
            var chirp = Synth.Env(Synth.Tone(0.14f, 880, 1320, Synth.Wave.Square, 0.35f), 0.005f, 0.12f);
            Save("alarm", Synth.Mix(chirp, Synth.At(Synth.Silence(0.5f), chirp, 0.2f)));
            Save("scan", Synth.Env(Synth.Tone(0.5f, 300, 1200, Synth.Wave.Sine, 0.4f), 0.05f, 0.4f, 0.3f));
            Save("report", Synth.Mix(Synth.Bell(1.2f, 523.25f), Synth.At(Synth.Silence(1.4f), Synth.Bell(1.0f, 659.25f), 0.18f)));
            // Home: a low, quiet two-note chime (GDD §10 "quiet room tone").
            Save("home", Synth.Mul(Synth.Mix(Synth.Bell(1.8f, 392f), Synth.At(Synth.Silence(2.1f), Synth.Bell(1.6f, 493.88f), 0.3f)), 0.6f));
            return files;
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
