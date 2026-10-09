using System;
using System.Collections.Generic;

namespace ChuchuGames.Dialogue
{
    /// <summary>One line: who speaks, with which expression, what they say, and optional replies.</summary>
    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        public string expression = "neutral";
        public string text;
        /// <summary>Optional replies (GDD: "optional 2-choice replies"). Empty = just advance.</summary>
        public string[] choices = new string[0];
    }

    /// <summary>
    /// Plain-text script format, one line per row:
    /// <code>
    /// Bartholomew[happy]: Welcome back!
    /// Morrow: A room near the water, if you please.
    /// Just keep talking: no name means the previous (or default) speaker.
    /// &gt; Of course. | Not tonight.      (a choice row attaches replies to the line above)
    /// # comment
    /// </code>
    /// </summary>
    public static class DialogueScript
    {
        public static List<DialogueLine> Parse(string text, string defaultSpeaker = null)
        {
            var lines = new List<DialogueLine>();
            if (string.IsNullOrWhiteSpace(text)) return lines;
            string speaker = defaultSpeaker, expression = "neutral";

            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var row = raw.Trim();
                if (row.Length == 0 || row.StartsWith("#")) continue;

                if (row.StartsWith(">"))
                {
                    if (lines.Count == 0) continue;
                    var opts = row.Substring(1).Split('|');
                    for (int i = 0; i < opts.Length; i++) opts[i] = opts[i].Trim();
                    lines[lines.Count - 1].choices = opts;
                    continue;
                }

                int colon = row.IndexOf(':');
                // A speaker tag is a short prefix without spaces in the bracket-free name part.
                if (colon > 0 && colon <= 40 && IsSpeakerTag(row.Substring(0, colon), out var name, out var expr))
                {
                    speaker = name;
                    expression = expr ?? "neutral";
                    row = row.Substring(colon + 1).Trim();
                }
                lines.Add(new DialogueLine { speaker = speaker, expression = expression, text = row });
            }
            return lines;
        }

        static bool IsSpeakerTag(string tag, out string name, out string expr)
        {
            name = tag.Trim();
            expr = null;
            int open = name.IndexOf('[');
            if (open >= 0)
            {
                int close = name.IndexOf(']', open);
                if (close < 0) return false;
                expr = name.Substring(open + 1, close - open - 1).Trim();
                name = name.Substring(0, open).Trim();
            }
            if (name.Length == 0) return false;
            foreach (var c in name)
                if (!(char.IsLetterOrDigit(c) || c == ' ' || c == '.' || c == '\'' || c == '-' || c == '_')) return false;
            return name.Split(' ').Length <= 3; // "Captain Morrow", "Mr. Vane" — not a whole sentence
        }
    }

    /// <summary>Steps through a list of lines; the view renders <see cref="Current"/>.</summary>
    public sealed class DialogueRunner
    {
        readonly List<DialogueLine> _lines;
        int _index = -1;

        /// <summary>Every line shown so far, with the reply picked (for the log).</summary>
        public List<string> Log { get; } = new List<string>();
        /// <summary>Index of the reply picked on each line that had choices.</summary>
        public List<int> Choices { get; } = new List<int>();

        public event Action<DialogueLine> LineShown;
        public event Action Finished;

        public DialogueRunner(IEnumerable<DialogueLine> lines) => _lines = new List<DialogueLine>(lines);

        public DialogueLine Current => _index >= 0 && _index < _lines.Count ? _lines[_index] : null;
        public bool IsDone => _index >= _lines.Count;
        public bool AwaitingChoice => Current != null && Current.choices != null && Current.choices.Length > 0;
        public int Count => _lines.Count;
        public int Index => _index;

        public void Start()
        {
            _index = -1;
            Next();
        }

        /// <summary>Moves on. Ignored while a choice is pending.</summary>
        public void Advance()
        {
            if (AwaitingChoice || IsDone) return;
            Next();
        }

        public void Choose(int option)
        {
            if (!AwaitingChoice) return;
            if (option < 0 || option >= Current.choices.Length) throw new ArgumentOutOfRangeException(nameof(option));
            Choices.Add(option);
            Log.Add($"  > {Current.choices[option]}");
            Next();
        }

        /// <summary>Jumps to the end (choices left unanswered default to the first option).</summary>
        public void Skip()
        {
            while (!IsDone)
            {
                if (AwaitingChoice) Choose(0);
                else Next();
            }
        }

        void Next()
        {
            _index++;
            if (IsDone)
            {
                Finished?.Invoke();
                return;
            }
            var line = Current;
            Log.Add(string.IsNullOrEmpty(line.speaker) ? line.text : $"{line.speaker}: {line.text}");
            LineShown?.Invoke(line);
        }
    }
}
