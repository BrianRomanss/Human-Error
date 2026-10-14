using System;

namespace HumanError.Model
{
    /// <summary>
    /// Comfort and accessibility options (GDD §7.2 Settings, §10, §11): text size, colourblind palette, reduce motion,
    /// tell assist, timer off, volumes. Saved once per player (not per save slot); plain fields for JsonUtility.
    /// </summary>
    [Serializable]
    public class Settings
    {
        public static readonly float[] TextScales = { 1f, 1.15f, 1.3f };
        public static readonly string[] TextScaleNames = { "Normal", "Large", "Largest" };

        public int version = 1;
        /// <summary>Index into <see cref="TextScales"/>.</summary>
        public int textSize;
        /// <summary>Blue/orange instead of teal/coral for alien/danger (always paired with words).</summary>
        public bool colourblind;
        /// <summary>No shakes, slides or jolts; a suit flicker becomes a gentle highlight.</summary>
        public bool reduceMotion;
        /// <summary>Rings briefly outline every animated tell as it happens (GDD §11 accessibility).</summary>
        public bool tellAssist;
        /// <summary>Real time never moves the clock, so the window never closes early.</summary>
        public bool timerOff;
        public float master = 1f;
        public float music = 0.6f;
        public float sfx = 1f;
        public bool fullscreen = true;

        public float TextScale => TextScales[Math.Max(0, Math.Min(TextScales.Length - 1, textSize))];

        /// <summary>Things that change how screens are built: a screen must be rebuilt when one of these changes.</summary>
        public string LayoutKey => $"{textSize}|{colourblind}";

        public Settings Clone() => (Settings)MemberwiseClone();

        /// <summary>Clamps values read from an old or hand-edited file.</summary>
        public Settings Sanitised()
        {
            var s = Clone();
            s.textSize = Math.Max(0, Math.Min(TextScales.Length - 1, s.textSize));
            s.master = Clamp01(s.master);
            s.music = Clamp01(s.music);
            s.sfx = Clamp01(s.sfx);
            return s;
        }

        static float Clamp01(float v) => float.IsNaN(v) ? 1f : Math.Max(0f, Math.Min(1f, v));
    }
}
