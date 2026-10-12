using ChuchuGames.UI;
using HumanError.Model;
using UnityEngine;

namespace HumanError.View
{
    /// <summary>
    /// Plays a <see cref="Tutorial"/> over the desk: each step spotlights a panel with a tip. "tap" steps
    /// continue on any tap; action steps leave only their target clickable until the player asks or decides.
    /// Lives on the desk, so it goes when the desk goes.
    /// </summary>
    public sealed class TutorialRunner : MonoBehaviour
    {
        DeskScreen _desk;
        Tutorial _tutorial;
        RectTransform _canvas;
        Spotlight _spot;
        int _step;
        /// <summary>1-based visitor currently at the window (0 until the first one is ready).</summary>
        int _visitor;

        TutorialStep Current => _step < _tutorial.Steps.Count ? _tutorial.Steps[_step] : null;

        public static TutorialRunner Attach(DeskScreen desk, Tutorial tutorial, RectTransform canvas, int startStep)
        {
            var r = desk.gameObject.AddComponent<TutorialRunner>();
            r._desk = desk;
            r._tutorial = tutorial;
            r._canvas = canvas;
            r._step = Mathf.Clamp(startStep, 0, tutorial.Steps.Count);
            desk.VisitorReady += r.OnVisitorReady;
            desk.Asked += r.OnAsked;
            desk.Decided += r.OnDecided;
            return r;
        }

        void OnDestroy()
        {
            if (_spot != null) _spot.Close();
        }

        void OnVisitorReady(int index)
        {
            _visitor = index + 1;
            ShowNext();
        }

        void ShowNext()
        {
            var s = Current;
            if (_spot != null || s == null || s.Visitor != _visitor) return;
            _spot = Spotlight.Show(_canvas, _desk.Target(s.Target), s.Text, s.IsTap ? Advance : (System.Action)null,
                outline: Palette.BioGlow, bubble: Palette.PaperSand, ink: Palette.Ink,
                waitForAction: !s.IsTap, onSkip: Skip);
        }

        void Advance()
        {
            _spot = null;
            _step++;
            ShowNext();
        }

        void Finish()
        {
            if (_spot != null) _spot.Close();
            Advance();
        }

        void OnAsked(string questionId)
        {
            var s = Current;
            if (_spot != null && s != null && !s.IsTap && Tutorial.AskSatisfies(s.Wait, questionId))
                Tween.Delay(this, 0.6f, Finish, "asked"); // let the answer pop up before the next tip
        }

        void OnDecided(Decision d)
        {
            var s = Current;
            if (_spot != null && s != null && Tutorial.DecisionOf(s.Wait) == d)
            {
                _spot.Close();
                _spot = null;
                _step++; // the next step belongs to the next visitor
            }
        }

        void Skip()
        {
            _spot = null;
            _step = _tutorial.Steps.Count;
        }
    }
}
