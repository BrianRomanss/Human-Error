namespace HumanError.Model
{
    /// <summary>
    /// Full game or demo (M7: "demo edition, shifts 1–5, Endless locked"). The demo is the full content with a gate:
    /// the story stops after <see cref="DemoShifts"/> (Home says so instead of starting shift 6, and no ending is
    /// reached), Endless is locked, and Daily draws its nights only from the shifts the demo contains.
    /// </summary>
    public sealed class Edition
    {
        public const int DemoShifts = 5;

        public static readonly Edition Full = new Edition(false);
        public static readonly Edition Demo = new Edition(true);

        public bool IsDemo { get; }

        Edition(bool demo) => IsDemo = demo;

        public string Name => IsDemo ? "Demo" : "Full game";

        /// <summary>Can this story shift be played? (The demo stops after shift 5.)</summary>
        public bool CanPlay(int shift) => !IsDemo || shift <= DemoShifts;

        public bool EndlessUnlocked => !IsDemo;

        /// <summary>Daily nights are built on a story shift up to this number.</summary>
        public int LastDailyBase => IsDemo ? DemoShifts : int.MaxValue;

        /// <summary>The demo's last shift is done: what Home shows instead of the next shift.</summary>
        public bool DemoFinished(SaveData save) => IsDemo && save != null && save.nextShift > DemoShifts;
    }
}
