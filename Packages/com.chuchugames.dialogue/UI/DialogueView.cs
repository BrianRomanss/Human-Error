using System;
using System.Collections.Generic;
using ChuchuGames.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChuchuGames.Dialogue.UI
{
    /// <summary>Look and lookups for <see cref="DialogueView"/>; everything optional has a sensible fallback.</summary>
    public sealed class DialogueStyle
    {
        public Color Box = new Color(0.96f, 0.93f, 0.86f);
        public Color Ink = new Color(0.16f, 0.14f, 0.2f);
        public Color NamePlate = new Color(0.69f, 0.54f, 0.24f);
        public Color NameText = Color.white;
        public Color Dim = new Color(0, 0, 0, 0.45f);
        public Font NameFont;
        public int TextSize = 34;
        public float CharsPerSecond = 45f;
        /// <summary>A voice blip every N characters typed (GDD §8: blips instead of voice acting).</summary>
        public int BlipEvery = 3;
        public Func<string, string, Sprite> Portrait;   // (speaker, expression) → sprite
        public Func<string, AudioClip> Voice;            // speaker → blip
        public Func<string, string> DisplayName = s => s; // id → shown name
    }

    /// <summary>
    /// Visual-novel overlay (GDD §5.2 Story Dialogue): portrait, name plate, typewriter text, tap to
    /// advance, optional 2-choice replies, log, auto and skip. Built in code; one instance per conversation.
    /// </summary>
    public sealed class DialogueView : MonoBehaviour
    {
        DialogueStyle _style;
        DialogueRunner _runner;
        Action<DialogueRunner> _onFinished;

        Image _portrait;
        Text _name, _text, _logText, _autoLabel;
        RectTransform _choices, _nameRoot;
        GameObject _logPanel;
        string _full = "";
        float _shown;
        bool _auto;
        float _autoTimer;
        int _lastBlip;

        public static DialogueView Play(RectTransform parent, IEnumerable<DialogueLine> lines, DialogueStyle style,
            Action<DialogueRunner> onFinished)
        {
            var root = UiBuild.Rect("Dialogue", parent).Fill();
            var view = root.gameObject.AddComponent<DialogueView>();
            view._style = style ?? new DialogueStyle();
            view._onFinished = onFinished;
            view.Build();
            view._runner = new DialogueRunner(lines);
            view._runner.LineShown += view.Show;
            view._runner.Finished += view.Finish;
            view._runner.Start();
            return view;
        }

        void Build()
        {
            var root = (RectTransform)transform;
            var dim = UiBuild.Panel("Dim", root, _style.Dim, raycastTarget: true, rounded: false);
            dim.rectTransform.Fill();
            var tap = dim.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(OnTap);

            _portrait = UiBuild.Picture("Portrait", root, null);
            _portrait.rectTransform.PlaceTopLeft(120, 420, 340, 340);
            _portrait.gameObject.AddComponent<Bob>().amplitude = 5f;

            var box = UiBuild.Panel("Box", root, _style.Box, raycastTarget: false);
            box.rectTransform.PlaceTopLeft(100, 780, 1720, 260);
            _text = UiBuild.Label("Text", box.transform, "", _style.TextSize, _style.Ink, TextAnchor.UpperLeft);
            _text.rectTransform.Fill(40);
            _text.rectTransform.offsetMax = new Vector2(-40, -48);

            var plate = UiBuild.Panel("NamePlate", root, _style.NamePlate, raycastTarget: false);
            _nameRoot = plate.rectTransform.PlaceTopLeft(470, 750, 420, 60);
            _name = UiBuild.Label("Name", plate.transform, "", 30, _style.NameText, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (_style.NameFont != null) _name.font = _style.NameFont;
            _name.rectTransform.Fill(6);

            _choices = UiBuild.Rect("Choices", root).PlaceTopLeft(1060, 560, 700, 200);
            var vl = _choices.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 14;
            vl.childControlHeight = vl.childControlWidth = true;
            vl.childForceExpandHeight = false;

            var bar = UiBuild.Rect("Controls", root).PlaceTopLeft(1440, 712, 380, 56);
            var log = UiBuild.Button("Log", bar, "Log", _style.Box, _style.Ink, ToggleLog, 22);
            ((RectTransform)log.transform).PlaceTopLeft(0, 0, 110, 56);
            var auto = UiBuild.Button("Auto", bar, "Auto", _style.Box, _style.Ink, ToggleAuto, 22);
            ((RectTransform)auto.transform).PlaceTopLeft(130, 0, 110, 56);
            _autoLabel = auto.GetComponentInChildren<Text>();
            var skip = UiBuild.Button("Skip", bar, "Skip", _style.Box, _style.Ink, () => _runner.Skip(), 22);
            ((RectTransform)skip.transform).PlaceTopLeft(260, 0, 110, 56);

            var lp = UiBuild.Panel("LogPanel", root, new Color(0, 0, 0, 0.85f), raycastTarget: true, rounded: false);
            lp.rectTransform.Fill();
            lp.gameObject.AddComponent<Button>().onClick.AddListener(ToggleLog);
            _logText = UiBuild.Label("LogText", lp.transform, "", 28, Color.white, TextAnchor.LowerLeft);
            _logText.rectTransform.Fill(120);
            _logPanel = lp.gameObject;
            _logPanel.SetActive(false);

            var group = gameObject.AddComponent<CanvasGroup>();
            Tween.Fade(group, 0, 1, 0.2f);
        }

        void Show(DialogueLine line)
        {
            var display = _style.DisplayName(line.speaker ?? "");
            _name.text = display;
            _nameRoot.gameObject.SetActive(!string.IsNullOrEmpty(display));
            var sprite = _style.Portrait?.Invoke(line.speaker, line.expression);
            _portrait.sprite = sprite;
            _portrait.color = Color.white;
            _portrait.enabled = sprite != null;
            if (sprite != null) Tween.Punch(_portrait.transform, 0.06f, 0.3f);

            _full = line.text ?? "";
            _shown = 0;
            _lastBlip = 0;
            _text.text = "";
            _autoTimer = 0;

            foreach (Transform c in _choices) Destroy(c.gameObject);
            _choices.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_runner == null || _runner.IsDone) return;
            int before = (int)_shown;
            if (_shown < _full.Length)
            {
                _shown = Mathf.Min(_full.Length, _shown + _style.CharsPerSecond * Time.unscaledDeltaTime);
                int n = (int)_shown;
                _text.text = _full.Substring(0, n);
                if (n / Mathf.Max(1, _style.BlipEvery) != _lastBlip && n > before && n < _full.Length && !char.IsWhiteSpace(_full[n - 1]))
                {
                    _lastBlip = n / Mathf.Max(1, _style.BlipEvery);
                    var clip = _style.Voice?.Invoke(_runner.Current.speaker);
                    if (clip != null) AudioPlayer.PlayVaried(clip, 0.35f, 0.08f);
                }
                if (_shown >= _full.Length) OnLineComplete();
            }
            else if (_auto && !_runner.AwaitingChoice)
            {
                _autoTimer += Time.unscaledDeltaTime;
                if (_autoTimer > 1.2f + _full.Length * 0.02f) _runner.Advance();
            }
        }

        void OnLineComplete()
        {
            _text.text = _full;
            if (!_runner.AwaitingChoice) return;
            _choices.gameObject.SetActive(true);
            var options = _runner.Current.choices;
            for (int i = 0; i < options.Length; i++)
            {
                int pick = i;
                var b = UiBuild.Button($"Choice{i}", _choices, options[i], _style.Box, _style.Ink, () => _runner.Choose(pick), 28);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
            }
        }

        void OnTap()
        {
            if (_runner == null || _runner.IsDone) return;
            if (_shown < _full.Length)
            {
                _shown = _full.Length; // first tap completes the line
                OnLineComplete();
                return;
            }
            _runner.Advance();
        }

        void ToggleAuto()
        {
            _auto = !_auto;
            _autoLabel.text = _auto ? "Auto: on" : "Auto";
        }

        void ToggleLog()
        {
            bool show = !_logPanel.activeSelf;
            _logPanel.SetActive(show);
            if (show) _logText.text = string.Join("\n", _runner.Log);
        }

        void Finish()
        {
            var runner = _runner;
            _runner = null;
            Destroy(gameObject);
            _onFinished?.Invoke(runner);
        }
    }
}
